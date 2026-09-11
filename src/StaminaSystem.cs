using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MotherMod
{
    public static class StaminaSystem
    {
        private static readonly ConditionalWeakTable<Player, StaminaData> data =
            new ConditionalWeakTable<Player, StaminaData>();

        private static readonly Func<Player, int> getWaterJumpDelay = BuildWaterJumpDelayGetter();
        private static bool warnedNoWaterJumpDelay;

        private static Func<Player, int> BuildWaterJumpDelayGetter()
        {
            FieldInfo field = typeof(Player).GetField("waterJumpDelay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) return null;
            ParameterExpression p = Expression.Parameter(typeof(Player), "p");
            return Expression.Lambda<Func<Player, int>>(Expression.Field(p, field), p).Compile();
        }

        public static bool TryGet(Player player, out StaminaData d)
        {
            d = null;
            return player != null && data.TryGetValue(player, out d);
        }

        public static void Register()
        {
            On.Player.ctor += Player_ctor;
            On.Player.Update += Player_Update;
            On.Player.Jump += Player_Jump;
            On.Player.WallJump += Player_WallJump;
            On.Player.ThrowObject += Player_ThrowObject;
            On.Player.ObjectEaten += Player_ObjectEaten;
            On.Lizard.Bite += Lizard_Bite;
            On.Creature.Violence += Creature_Violence;
        }

        private static void Player_ctor(On.Player.orig_ctor orig, Player self,
            AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);

            if (Plugin.Stamina.TryGet(self, out var config) && !data.TryGetValue(self, out _))
            {
                data.Add(self, new StaminaData(config));

                Plugin.DebugLogging = config.Debug;

                Plugin.LogSource?.LogInfo($"[Mother] stamina initialized for player (debug={config.Debug})");
                Plugin.Log("Config", $"effective {config}");
            }
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            if (!data.TryGetValue(self, out var d))
            {
                orig(self, eu);
                return;
            }

            int preDelay = ReadWaterJumpDelay(self);

            float mult = d.SpeedMult;
            var st = self.slugcatStats;
            if (mult < 0.999f && st != null)
            {
                float run = st.runspeedFac, pole = st.poleClimbSpeedFac, corr = st.corridorClimbSpeedFac, swim = st.swimForceFac;
                st.runspeedFac = run * mult;
                if (d.config.OverexertSlowClimb)
                {
                    st.poleClimbSpeedFac = pole * mult;
                    st.corridorClimbSpeedFac = corr * mult;
                }
                if (d.config.OverexertSlowSwim) st.swimForceFac = swim * mult;
                try { orig(self, eu); }
                finally
                {
                    st.runspeedFac = run;
                    st.poleClimbSpeedFac = pole;
                    st.corridorClimbSpeedFac = corr;
                    st.swimForceFac = swim;
                }
            }
            else
            {
                orig(self, eu);
            }

            bool boostEdge = preDelay <= 1
                && ReadWaterJumpDelay(self) > 0
                && self.animation == Player.AnimationIndex.DeepSwim;
            d.Update(self, boostEdge);
        }

        private static int ReadWaterJumpDelay(Player self)
        {
            if (getWaterJumpDelay == null)
            {
                if (!warnedNoWaterJumpDelay)
                {
                    warnedNoWaterJumpDelay = true;
                    Plugin.LogSource?.LogWarning("[Mother][Exh2] Player.waterJumpDelay not found — boost-swim cost disabled");
                }
                return int.MaxValue;
            }
            return getWaterJumpDelay(self);
        }

        private static void Player_Jump(On.Player.orig_Jump orig, Player self)
        {
            if (data.TryGetValue(self, out var d))
            {
                d.NotifyJump(self);
                d.suppressWallJumpDrain = true;

                try { orig(self); }
                finally { d.suppressWallJumpDrain = false; }
            }
            else
            {
                orig(self);
            }
        }

        private static void Player_WallJump(On.Player.orig_WallJump orig, Player self, int direction)
        {
            orig(self, direction);
            if (data.TryGetValue(self, out var d) && !d.suppressWallJumpDrain)
            {
                d.NotifyWallJump(self);
            }
        }

        private static void Player_ThrowObject(On.Player.orig_ThrowObject orig, Player self, int grasp, bool eu)
        {
            if (!data.TryGetValue(self, out var d))
            {
                orig(self, grasp, eu);
                return;
            }

            PhysicalObject held = (self.grasps != null && grasp >= 0 && grasp < self.grasps.Length)
                ? self.grasps[grasp]?.grabbed
                : null;
            float throwMult = d.ThrowMult;
            orig(self, grasp, eu);
            if (held != null && (self.grasps[grasp] == null || self.grasps[grasp].grabbed != held))
            {
                if (throwMult < 0.999f)
                {
                    for (int i = 0; i < held.bodyChunks.Length; i++)
                        held.bodyChunks[i].vel *= throwMult;
                    if (held is Weapon w)
                        w.overrideExitThrownSpeed = Mathf.Min(w.exitThrownModeSpeed, 20f * throwMult);
                }
                d.NotifyThrow(self, held, throwMult);
            }
        }

        private static void Creature_Violence(On.Creature.orig_Violence orig, Creature self, BodyChunk source,
            Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage,
            Creature.DamageType type, float damage, float stunBonus)
        {
            bool wasDead = self.dead;
            orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);

            if (!wasDead && !self.dead && self is Player player && data.TryGetValue(player, out var d))
            {
                d.NotifyHit(player, type, damage, stunBonus);
            }
        }

        private static void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            orig(self, edible);
            if (data.TryGetValue(self, out var d))
            {
                d.NotifyEat(self, edible);
            }
        }

        private static void Lizard_Bite(On.Lizard.orig_Bite orig, Lizard self, BodyChunk chunk)
        {
            if (chunk?.owner is Player player
                && data.TryGetValue(player, out var d)
                && d.ForcedStunActive
                && self.lizardParams != null)
            {
                float origChance = self.lizardParams.biteDamageChance;
                float biased = Mathf.Min(1f, origChance * d.config.StunBiteDeathMult);
                self.lizardParams.biteDamageChance = biased;
                Plugin.Log("Exh2", $"bite-bias applied lizard={self.abstractCreature?.creatureTemplate?.type} " +
                                   $"chance={origChance:0.###}->{biased:0.###} mult={d.config.StunBiteDeathMult}");
                try { orig(self, chunk); }
                finally { self.lizardParams.biteDamageChance = origChance; }
            }
            else
            {
                orig(self, chunk);
            }
        }
    }
}
