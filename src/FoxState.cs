using System;
using UnityEngine;

namespace MotherMod
{
    public class FoxState : HealthState
    {
        public float hunger;

        private double realizedCarry;

        private float lastLoggedHunger;

        private bool starvedToDeath;

        public FoxState(AbstractCreature acrit) : base(acrit)
        {
            lastLoggedHunger = hunger;
        }

        public override void CycleTick()
        {
            FoxBreedParams p = FoxParams.For(FoxCritob.VariantIdFor(creature.creatureTemplate.type));

            float before = hunger;
            float healthBefore = health;
            float regenFac = Mathf.Lerp(1f, p.StarveRegenFac, Mathf.Clamp01(hunger));

            base.CycleTick();
            health = Mathf.Min(healthBefore + 0.25f * regenFac, health);

            hunger = Mathf.Clamp01(hunger + p.HungerPerCycle);
            realizedCarry = 0.0;

            string extra = null;
            LogHunger(p, "region_load_tick", before, regenFac, extra);
        }

        public void RealizedTick(FoxBreedParams p, int cycleLength, string variantId)
        {
            if (cycleLength <= 0) return;

            float before = hunger;
            realizedCarry += (double)p.HungerPerCycle / cycleLength;
            if (realizedCarry <= 0.0) return;

            const double SPEND_AT = 0.0005;
            if (realizedCarry < SPEND_AT) return;

            hunger = Mathf.Clamp01(hunger + (float)realizedCarry);
            realizedCarry = 0.0;
            LogHunger(p, "realized", before, Mathf.Lerp(1f, p.StarveRegenFac, Mathf.Clamp01(hunger)),
                      $"cycleLength={cycleLength}");
        }

        public void Meal(FoxBreedParams p, string variantId, string source, string detail)
        {
            hunger = Mathf.Clamp01(hunger - p.MealValue);
            lastLoggedHunger = hunger;
        }

        public bool ShouldStarveToDeath(FoxBreedParams p)
        {
            if (!p.StarveLethal || starvedToDeath || hunger < 1f) return false;
            starvedToDeath = true;
            return true;
        }

        private void LogHunger(FoxBreedParams p, string source, float before, float regenFac, string extra)
        {
            if (Mathf.Abs(hunger - lastLoggedHunger) < 0.05f && hunger != 1f) return;
            if (hunger == lastLoggedHunger) return;
            lastLoggedHunger = hunger;
        }

        public static void Register()
        {
            On.AbstractCreature.IsEnteringDen += AbstractCreature_IsEnteringDen;
        }

        private static void AbstractCreature_IsEnteringDen(On.AbstractCreature.orig_IsEnteringDen orig,
                                                           AbstractCreature self, WorldCoordinate den)
        {
            if (!FoxCritob.IsFoxType(self.creatureTemplate.type))
            {
                orig(self, den);
                return;
            }

            AbstractCreature carried = null;
            try
            {
                for (int i = 0; i < self.stuckObjects.Count; i++)
                {
                    var stick = self.stuckObjects[i] as AbstractPhysicalObject.CreatureGripStick;
                    if (stick != null && stick.A == self && stick.B is AbstractCreature prey)
                    {
                        carried = prey;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                FoxFault.Warn($"stash_scan_failed id={self.ID} err={e.GetType().Name} msg=\"{e.Message}\"");
            }

            orig(self, den);

            if (carried == null) return;

            try
            {
                CreditStash(self, den, carried);
            }
            catch (Exception e)
            {
                FoxFault.Warn($"stash_credit_failed id={self.ID} err={e.GetType().Name} " +
                            $"msg=\"{e.Message}\" action=meal_not_credited");
            }
        }

        private static void CreditStash(AbstractCreature self, WorldCoordinate den, AbstractCreature carried)
        {
            string variantId = FoxCritob.VariantIdFor(self.creatureTemplate.type);
            FoxBreedParams p = FoxParams.For(variantId);

            if (self.state is FoxState fs)
            {
                string detail = null;
                fs.Meal(p, variantId, "stash", detail);
            }

            (self.abstractAI?.RealAI as FoxAI)?.Fed(p.DenRestFrames);
        }
    }
}
