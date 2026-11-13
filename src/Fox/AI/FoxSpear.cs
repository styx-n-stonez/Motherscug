using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public sealed class FoxSpear
    {
        private const int BRAKE_MAX_TICKS = 40;
        private const float STOPPED_SPEED = 0.5f;
        internal const int SWING_TICKS = 8;
        internal const int SETTLE_TICKS = 40;
        private const int FLIGHT_LOG_MAX_TICKS = 200;

        private readonly FoxAI ai;
        private readonly Fox fox;

        private Spear fetchTarget;
        private WorldCoordinate fetchCoord;
        private int fetchStartTick;
        private int nextScanTick;
        private int noFetchUntil;
        private readonly List<AbstractPhysicalObject> skipObj = new List<AbstractPhysicalObject>();
        private readonly List<int> skipUntil = new List<int>();
        private bool lastScanEmpty;

        internal enum Phase { None, Brake, Hold }
        internal Phase State { get; private set; }
        internal int StartTick { get; private set; } = int.MinValue;
        internal int StopTick { get; private set; } = int.MaxValue;
        internal int RelTick { get; private set; } = int.MaxValue;
        internal int RelEndTick { get; private set; } = int.MaxValue;
        internal int RecEndTick { get; private set; } = int.MaxValue;
        internal int EndTick { get; private set; } = int.MinValue;
        internal bool Released { get; private set; }
        internal float Face { get; private set; } = 1f;
        internal float BrakeRemainTicks { get; private set; }
        internal Creature Target { get; private set; }
        internal Spear Flying { get; private set; }

        private int noThrowUntil;
        private string lastAimRefusal;
        private int lastAimRefusalTick = -100000;
        private int flyStartTick;

        internal FoxSpear(FoxAI ai, Fox fox)
        {
            this.ai = ai;
            this.fox = fox;
        }

        private int Now { get { return (fox.room != null && fox.room.game != null) ? fox.room.game.clock : 0; } }

        internal static bool IsOn(FoxBreedParams p)
        {
            return p != null && p.SpearEnabled;
        }

        internal bool HoldsBody { get { return State != Phase.None; } }

        internal bool Fetching { get { return fetchTarget != null; } }

        internal bool LayerActive(int now) { return StartTick != int.MinValue && now >= StartTick && now <= EndTick; }

        internal static void InstallHooks()
        {
            On.PathFinder.SetDestination += PathFinder_SetDestination;
            On.Creature.Violence += Creature_Violence;
        }

        private static void PathFinder_SetDestination(On.PathFinder.orig_SetDestination orig, PathFinder self,
                                                      WorldCoordinate newDestination)
        {
            if (self != null && self.creature != null && FoxCritob.IsFoxType(self.creature.creatureTemplate.type))
            {
                Fox f = self.creature.realizedCreature as Fox;
                FoxSpear s = f != null && f.AI != null ? f.AI.spear : null;
                if (s != null && s.fetchTarget != null && IsOn(f.P)) newDestination = s.fetchCoord;
            }
            orig(self, newDestination);
        }

        private static void Creature_Violence(On.Creature.orig_Violence orig, Creature self, BodyChunk source,
            Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage,
            Creature.DamageType type, float damage, float stunBonus)
        {
            if (source != null && type == Creature.DamageType.Stab && source.owner is Spear sp && sp.thrownBy is Fox f
                && f.AI != null && f.AI.spear != null && ReferenceEquals(f.AI.spear.Flying, sp))
            {
                FoxBreedParams p = f.P;
                float want = p.SpearStunBonus;
                f.AI.spear.NoteHit(self, hitChunk, damage, stunBonus, want);
                stunBonus = want;
            }
            orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
        }

        private static bool FetchBehaviour(FoxAI.Behavior b)
        {
            return b == FoxAI.Behavior.Idle || b == FoxAI.Behavior.Wander
                || b == FoxAI.Behavior.Hunt || b == FoxAI.Behavior.Stalk;
        }

        private static bool ThrowBehaviour(FoxAI.Behavior b)
        {
            return b == FoxAI.Behavior.Hunt || b == FoxAI.Behavior.Stalk;
        }

        internal void Tick(FoxBreedParams p, FoxAI.Behavior behavior)
        {
            if (!IsOn(p))
            {
                if (fetchTarget != null) EndFetch(p, "spear_off", false);
                if (State != Phase.None) Abort(p, "spear_off");
                return;
            }
            int now = Now;

            Spear held = fox.HeldItem as Spear;

            if (State != Phase.None && !Released && !ThrowBehaviour(behavior)) Abort(p, "behaviour_" + behavior);

            bool mouthFree = held == null && fox.grasps != null && fox.grasps.Length > 0 && fox.grasps[0] == null;
            string noFetch = null;
            if (!mouthFree) noFetch = "mouth_full";
            else if (!FetchBehaviour(behavior)) noFetch = "behaviour_" + behavior;
            else if (State != Phase.None) noFetch = "throwing";
            else if (now < noFetchUntil) noFetch = "refetch_cooldown";
            else if (p.SpearSeekTiles <= 0f) noFetch = "seek_off";
            else if (fox.chaseTarget != null && InBiteBand(p, fox.chaseTarget)) noFetch = "prey_in_bite_band";
            if (noFetch != null)
            {
                if (fetchTarget != null) EndFetch(p, noFetch, false);
                return;
            }

            if (fetchTarget != null)
            {
                string why = FreeSpearRefusal(p, fetchTarget, true);
                if (why == null && now - fetchStartTick > p.SpearFetchMaxTicks) why = "timeout";
                if (why != null) EndFetch(p, why, why == "timeout" || why == "unreachable");
            }

            if (fetchTarget == null && now >= nextScanTick)
            {
                nextScanTick = now + Math.Max(1, p.SpearSeekScanTicks);
                Scan(p);
            }
            else if (fetchTarget != null && now >= nextScanTick)
            {
                nextScanTick = now + Math.Max(1, p.SpearSeekScanTicks);
                WorldCoordinate c;
                if (ReachableCoord(fetchTarget.firstChunk.pos, out c)) fetchCoord = c;
            }

            if (fetchTarget != null) ai.creature.abstractAI.SetDestination(fetchCoord);
        }

        internal void AfterAct(FoxBreedParams p, FoxAI.Behavior behavior, float stuckBoost)
        {
            if (!IsOn(p)) return;
            if (State != Phase.None) { fox.runSpeed = 0f; return; }
            if (fetchTarget != null && (behavior == FoxAI.Behavior.Idle || behavior == FoxAI.Behavior.Wander))
            {
                fox.runSpeed = Mathf.Max(fox.runSpeed, p.RunSpeedIdle, stuckBoost);
            }
        }

        internal void HuntTick(FoxBreedParams p)
        {
            if (!IsOn(p)) return;
            Creature t = fox.chaseTarget;
            Spear held = fox.HeldItem as Spear;
            if (held != null && t != null && t.room == fox.room && InBiteBand(p, t))
            {
                if (State != Phase.None && !Released) Abort(p, "bite_band");
                if (State == Phase.None) DropForBite(p, held, t);
                return;
            }
            if (State != Phase.None || held == null || t == null) return;
            string why = ThrowRefusal(p, t, out float dist, out Vector2 aimDir);
            if (why == null)
            {
                StartThrow(p, t, dist, aimDir);
            }
            else if (dist <= p.SpearThrowDistPx * 1.5f)
            {
                int now = Now;
                if (why != lastAimRefusal || now - lastAimRefusalTick >= 120)
                {
                    lastAimRefusal = why;
                    lastAimRefusalTick = now;
                }
            }
        }

        internal void BodyTick(FoxBreedParams p, bool eu)
        {
            TrackFlight(p);
            if (!IsOn(p)) return;
            int now = Now;
            if (fetchTarget != null) TryPickUp(p);
            if (State == Phase.None) return;

            Spear held = fox.HeldItem as Spear;
            if (!Released)
            {
                string why = null;
                if (held == null) why = "spear_lost";
                else if (Target == null || Target.room != fox.room || Target.dead || Target.mainBodyChunk == null) why = "target_gone";
                else if (!OnFloor()) why = "off_floor";
                else if (fox.room != null && !fox.room.VisualContact(fox.mainBodyChunk.pos, Target.mainBodyChunk.pos)) why = "no_line_of_sight";
                if (why != null) { Abort(p, why); return; }
            }

            if (State == Phase.Brake)
            {
                float acc = Mathf.Max(0.05f, p.Acceleration);
                float vx = fox.mainBodyChunk.vel.x;
                BrakeRemainTicks = Mathf.Abs(vx) / acc;
                if (Mathf.Abs(vx) < STOPPED_SPEED || now - StartTick >= BRAKE_MAX_TICKS) Stop(p, now);
            }
            else if (!Released && now >= RelTick)
            {
                Release(p, held, eu);
            }
            if (State != Phase.None && now >= RecEndTick)
            {
                State = Phase.None;
                noThrowUntil = now + Math.Max(0, p.SpearThrowCooldownTicks);
            }
        }

        internal bool HoldStill(FoxBreedParams p)
        {
            if (State == Phase.None || !IsOn(p)) return false;
            fox.runSpeed = 0f;
            float acc = Mathf.Max(0.05f, p.Acceleration);
            for (int i = 0; i < fox.bodyChunks.Length; i++)
            {
                BodyChunk c = fox.bodyChunks[i];
                c.vel.x = Mathf.MoveTowards(c.vel.x, 0f, acc);
            }
            return true;
        }

        internal void OnKnockedOut(FoxBreedParams p)
        {
            if (State != Phase.None && !Released) Abort(p, "stunned");
            else if (State != Phase.None) State = Phase.None;
            if (fetchTarget != null) EndFetch(p, "stunned", false);
        }

        private string FreeSpearRefusal(FoxBreedParams p, Spear s, bool current)
        {
            if (s == null || s.slatedForDeletetion || s.room == null || s.room != fox.room) return "gone";
            if (s.grabbedBy.Count > 0) return "taken";
            if (s.mode != Weapon.Mode.Free && s.mode != Weapon.Mode.StuckInWall) return "mode_" + s.mode;
            float maxPx = p.SpearSeekTiles * 20f * (current ? 1.5f : 1f);
            if (!Custom.DistLess(fox.mainBodyChunk.pos, s.firstChunk.pos, maxPx)) return "too_far";
            return null;
        }

        private void Scan(FoxBreedParams p)
        {
            Room room = fox.room;
            int now = Now;
            if (room == null || ai.pathFinder == null || !ai.pathFinder.DoneMappingAccessibility)
            {
                return;
            }
            for (int i = skipObj.Count - 1; i >= 0; i--)
            {
                if (now >= skipUntil[i]) { skipObj.RemoveAt(i); skipUntil.RemoveAt(i); }
            }
            Spear best = null;
            WorldCoordinate bestCoord = default;
            float bestD = float.MaxValue;
            int candidates = 0, unreachable = 0;
            for (int layer = 0; layer < room.physicalObjects.Length; layer++)
            {
                List<PhysicalObject> list = room.physicalObjects[layer];
                for (int j = 0; j < list.Count; j++)
                {
                    Spear s = list[j] as Spear;
                    if (s == null || FreeSpearRefusal(p, s, false) != null) continue;
                    if (skipObj.Contains(s.abstractPhysicalObject)) continue;
                    candidates++;
                    float d = Vector2.Distance(fox.mainBodyChunk.pos, s.firstChunk.pos);
                    if (d >= bestD) continue;
                    WorldCoordinate c;
                    if (!ReachableCoord(s.firstChunk.pos, out c)) { unreachable++; continue; }
                    best = s; bestD = d; bestCoord = c;
                }
            }
            if (best == null)
            {
                if (!lastScanEmpty)
                {
                    lastScanEmpty = true;
                }
                return;
            }
            lastScanEmpty = false;
            fetchTarget = best;
            fetchCoord = bestCoord;
            fetchStartTick = now;
        }

        private bool ReachableCoord(Vector2 pos, out WorldCoordinate c)
        {
            c = fox.room.GetWorldCoordinate(pos);
            if (ai.pathFinder.CoordinateReachableAndGetbackable(c)) return true;
            for (int i = 0; i < 4; i++)
            {
                WorldCoordinate n = c + Custom.fourDirections[i];
                if (ai.pathFinder.CoordinateReachableAndGetbackable(n)) { c = n; return true; }
            }
            return false;
        }

        private void EndFetch(FoxBreedParams p, string reason, bool skip)
        {
            Spear s = fetchTarget;
            fetchTarget = null;
            if (s == null) return;
            if (skip)
            {
                skipObj.Add(s.abstractPhysicalObject);
                skipUntil.Add(Now + Math.Max(0, p.SpearFetchRetryTicks));
            }
        }

        private void TryPickUp(FoxBreedParams p)
        {
            Spear s = fetchTarget;
            if (s == null || fox.grasps == null || fox.grasps.Length == 0 || fox.grasps[0] != null) return;
            if (FreeSpearRefusal(p, s, true) != null) return;
            float d = Vector2.Distance(fox.mainBodyChunk.pos, s.firstChunk.pos);
            if (d > p.SpearGrabPx) return;
            if (fox.room != null && !fox.room.VisualContact(fox.mainBodyChunk.pos, s.firstChunk.pos)) return;
            bool took = fox.GrabSpear(s);
            fetchTarget = null;
            if (took)
            {
                Target = null;
            }
            else
            {
                skipObj.Add(s.abstractPhysicalObject);
                skipUntil.Add(Now + Math.Max(0, p.SpearFetchRetryTicks));
            }
        }

        internal bool InBiteBand(FoxBreedParams p, Creature t)
        {
            if (t == null || t.bodyChunks == null || fox.mainBodyChunk == null) return false;
            float best = float.MaxValue;
            for (int i = 0; i < t.bodyChunks.Length; i++)
            {
                float d = Vector2.Distance(fox.mainBodyChunk.pos, t.bodyChunks[i].pos) - t.bodyChunks[i].rad;
                if (d < best) best = d;
            }
            return best <= p.BiteRadius;
        }

        private void DropForBite(FoxBreedParams p, Spear held, Creature t)
        {
            fox.ReleaseSpear();
            noFetchUntil = Now + Math.Max(0, p.SpearRefetchCooldownTicks);
        }

        private bool OnFloor()
        {
            return fox.Grounded && !fox.PoleHeld && !fox.WallHeld && !fox.enteringShortCut.HasValue
                   && fox.pounce == Fox.PounceState.None && fox.charge == Fox.ChargeState.None;
        }

        private string ThrowRefusal(FoxBreedParams p, Creature t, out float dist, out Vector2 aimDir)
        {
            Vector2 head = fox.mainBodyChunk.pos;
            dist = t.mainBodyChunk != null ? Vector2.Distance(head, t.mainBodyChunk.pos) : float.MaxValue;
            aimDir = t.mainBodyChunk != null ? Fox.Dir(head, t.mainBodyChunk.pos) : Vector2.zero;
            if (!(t is Player)) return "not_player";
            if (t.dead) return "target_dead";
            if (t.room != fox.room) return "other_room";
            int now = Now;
            if (now < noThrowUntil) return "cooldown";
            if (dist > p.SpearThrowDistPx) return "too_far";
            if (InBiteBand(p, t)) return "bite_band";
            if (Mathf.Abs(aimDir.x) <= p.SpearThrowLevelMin) return "not_level";
            Vector2 fwd = Fox.Dir(fox.bodyChunks[2].pos, fox.bodyChunks[0].pos);
            if (Vector2.Dot(fwd, aimDir) <= 0f) return "facing_away";
            if (!OnFloor()) return "off_floor";
            if (fox.TurnActive) return "turning";
            if (fox.room == null || !fox.room.VisualContact(head, t.mainBodyChunk.pos)) return "no_line_of_sight";
            return null;
        }

        private void StartThrow(FoxBreedParams p, Creature t, float dist, Vector2 aimDir)
        {
            int now = Now;
            State = Phase.Brake;
            StartTick = now;
            StopTick = RelTick = RelEndTick = RecEndTick = int.MaxValue;
            EndTick = int.MaxValue;
            Released = false;
            Target = t;
            Face = aimDir.x < 0f ? -1f : 1f;
            BrakeRemainTicks = Mathf.Abs(fox.mainBodyChunk.vel.x) / Mathf.Max(0.05f, p.Acceleration);
            lastAimRefusal = null;
        }

        private void Stop(FoxBreedParams p, int now)
        {
            State = Phase.Hold;
            StopTick = now;
            BrakeRemainTicks = 0f;
            int W = Math.Max(1, p.SpearWindupTicks), RT = Math.Max(1, p.SpearRearTicks), R = Math.Max(1, p.SpearReleaseTicks);
            RelTick = now + Math.Max(W, RT);
            RelEndTick = RelTick + R;
            RecEndTick = RelEndTick + Math.Max(SWING_TICKS, RT);
            EndTick = RecEndTick + SETTLE_TICKS;
        }

        private void Release(FoxBreedParams p, Spear s, bool eu)
        {
            int now = Now;
            Creature t = Target;
            Vector2 head = fox.mainBodyChunk.pos;
            Vector2 aim = t.mainBodyChunk.pos;
            float d = Vector2.Distance(head, aim);
            float k = Mathf.Lerp(0.08f, 0.18f, UnityEngine.Random.value);
            float lift = d > 200f ? Mathf.Pow(d, 1.5f) * 0.05f * k : 0f;
            aim.y += lift;
            Vector2 from = p.SpearReleaseFrom >= 1 ? s.firstChunk.pos : head + Fox.Dir(head, aim) * 50f;
            if (fox.room != null && fox.room.GetTile(from).Solid) { from = head; }
            Vector2 dir = Fox.Dir(from, aim);
            IntVector2 td = new IntVector2(dir.x < 0f ? -1 : 1, 0);
            float frc = ModManager.DLCShared ? 0.35f : 1.3f;
            s.Thrown(fox, from, from - dir * 5f, td, frc, eu);
            s.firstChunk.vel = dir * p.SpearThrowSpeed;
            s.spearDamageBonus = p.SpearDamage;
            fox.ReleaseSpear();
            Released = true;
            Flying = s;
            flyStartTick = now;
        }

        private void Abort(FoxBreedParams p, string reason)
        {
            if (State == Phase.None) return;
            int now = Now;
            if (StopTick == int.MaxValue) StopTick = now;
            RelTick = RelEndTick = RecEndTick = now;
            int RT = Math.Max(1, p != null ? p.SpearRearTicks : 10);
            EndTick = now + 3 * RT + SETTLE_TICKS;
            State = Phase.None;
            noThrowUntil = now + Math.Max(0, p != null ? p.SpearThrowCooldownTicks : 40);
        }

        private void NoteHit(Creature victim, BodyChunk chunk, float damage, float vanillaStun, float stun)
        {
            Flying = null;
        }

        private void TrackFlight(FoxBreedParams p)
        {
            Spear s = Flying;
            if (s == null) return;
            int now = Now;
            string why = null;
            if (s.slatedForDeletetion || s.room == null || s.room != fox.room) why = "gone";
            else if (s.mode != Weapon.Mode.Thrown) why = s.mode == Weapon.Mode.StuckInWall ? "wall" : "mode_" + s.mode;
            else if (now - flyStartTick > FLIGHT_LOG_MAX_TICKS) why = "timeout";
            if (why == null) return;
            Flying = null;
        }

        private string Tile(Vector2 pos)
        {
            if (fox.room == null) return "none";
            IntVector2 t = fox.room.GetTilePosition(pos);
            return $"({t.x},{t.y})";
        }
    }

    public partial class Fox
    {
        internal bool GrabSpear(Spear s)
        {
            if (s == null || grasps == null || grasps.Length == 0 || grasps[0] != null) return false;
            bool wasWall = s.mode == Weapon.Mode.StuckInWall;
            bool took = Grab(s, 0, 0, Grasp.Shareability.CanNotShare, 0.5f, overrideEquallyDominant: true, pacifying: true);
            if (!took) return false;
            if (wasWall)
            {
                s.PickedUp(this);
                if (s.abstractSpear != null) s.abstractSpear.stuckInWallCycles = 0;
            }
            return true;
        }

        internal void ReleaseSpear()
        {
            if (grasps == null || grasps.Length == 0 || grasps[0] == null || !(grasps[0].grabbed is Spear)) return;
            ReleaseGrasp(0);
        }

        internal bool SpearCarryOwned(PhysicalObject held)
        {
            return held is Spear && FoxSpear.IsOn(P);
        }

        public override void GraphicsModuleUpdated(bool actuallyViewed, bool eu)
        {
            base.GraphicsModuleUpdated(actuallyViewed, eu);
            if (grasps == null || grasps.Length == 0 || grasps[0] == null) return;
            Spear s = grasps[0].grabbed as Spear;
            if (s == null || !FoxSpear.IsOn(P)) return;
            Vector2 pos, dir;
            FoxGraphics g = graphicsModule as FoxGraphics;
            if (actuallyViewed && g != null && g.SpearHoldValid)
            {
                pos = g.SpearHoldPos;
                dir = g.SpearHoldDir;
            }
            else
            {
                pos = CarryPoint(P);
                dir = Dir(bodyChunks[1].pos, bodyChunks[0].pos);
            }
            s.firstChunk.MoveFromOutsideMyUpdate(eu, pos);
            s.firstChunk.vel = Vector2.zero;
            s.setRotation = dir;
        }
    }
}
