using System;
using System.Collections.Generic;
using System.Diagnostics;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal struct FoxBaitLine
    {
        internal bool Ok;
        internal int Fail;
        internal float DistPx, DyPx, Deg, BandMin, BandMax, WantDist, WantRise, HeightAt;
        internal float ReachPx;
        internal float RangePx;
    }

    public partial class Fox
    {
        public PhysicalObject HeldItem
        {
            get
            {
                if (grasps == null || grasps.Length == 0 || grasps[0] == null) return null;
                PhysicalObject held = grasps[0].grabbed;
                return held is Creature ? null : held;
            }
        }

        internal bool TryGrabItem(FoxBreedParams p, PhysicalObject item)
        {
            if (item == null || grasps == null || grasps.Length == 0) return false;
            if (grasps[0] != null) return false;

            bool took = Grab(item, 0, 0, Grasp.Shareability.CanNotShare, p.BaitGraspDominance,
                             overrideEquallyDominant: true, pacifying: true);
            return took;
        }

        internal bool ReleaseItem(PhysicalObject item)
        {
            if (item == null || HeldItem != item) return false;
            ReleaseGrasp(0);
            return true;
        }

        internal FoxBaitLine BaitLine(FoxBreedParams p, Vector2 from, Vector2 aim, IntVector2[] ray)
        {
            var l = new FoxBaitLine();
            float dx = aim.x - from.x, dy = aim.y - from.y;
            l.DistPx = Mathf.Sqrt(dx * dx + dy * dy);
            l.DyPx = dy;
            BaitBand(p, out l.BandMin, out l.BandMax, out l.ReachPx);
            l.Deg = (l.DistPx < 0.1f) ? 0f : Mathf.Atan2(Mathf.Abs(dy), Mathf.Abs(dx)) * Mathf.Rad2Deg;

            if (l.DistPx < l.BandMin || l.DistPx > l.BandMax) { l.Fail = 1; return l; }
            if (LaunchTooSteep(p, l.Deg)) { l.Fail = 2; return l; }

            int n = SharedPhysics.RayTracedTilesArray(from, aim, ray);
            if (room == null || n >= ray.Length) { l.Fail = 3; return l; }
            for (int i = 0; i < n; i++)
            {
                if (room.GetTile(ray[i].x, ray[i].y).Solid) { l.Fail = 3; return l; }
            }
            int headroom = Math.Max(0, p.BaitPrepHeadroomTiles);
            for (int i = 0; i < n && headroom > 0; i++)
            {
                for (int k = 1; k <= headroom; k++)
                {
                    if (room.GetTile(ray[i].x, ray[i].y + k).Solid) { l.Fail = 4; return l; }
                }
            }

            WantTermsOf(p, dx, dy, 0f, out l.WantDist, out l.WantRise);
            Vector2 dir = Dir(from, aim);
            Vector2 full = LaunchVelocity(p, dir, EffectiveLaunchSpeed(p));
            l.HeightAt = PredictedHeightAt(p, full, l.WantDist);
            l.RangePx = PredictedRange(p, full);
            if (!Reaches(p, dir, 1f, l.WantDist, l.WantRise)) { l.Fail = 5; return l; }

            l.Ok = true;
            return l;
        }

        private int baitReachTick = NEVER_LOGGED;
        private FoxBreedParams baitReachParams;
        private float baitReachPx;

        private bool LevelReaches(FoxBreedParams p, float d)
        {
            float wantDist, wantRise;
            WantTermsOf(p, d, 0f, 0f, out wantDist, out wantRise);
            return Reaches(p, Vector2.right, 1f, wantDist, wantRise);
        }

        internal float BaitLevelReachPx(FoxBreedParams p)
        {
            int tick = GameTick;
            if (baitReachTick == tick && ReferenceEquals(baitReachParams, p)) return baitReachPx;

            float cap = Mathf.Max(0f, p.PounceDistance);
            float reach;
            if (cap <= 0f) reach = 0f;
            else if (LevelReaches(p, cap)) reach = cap;
            else
            {
                float lo = 0f, hi = cap;
                for (int i = 0; i < 16; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (LevelReaches(p, mid)) lo = mid; else hi = mid;
                }
                reach = lo;
            }

            baitReachTick = tick;
            baitReachParams = p;
            baitReachPx = reach;
            return reach;
        }

        internal void BaitBand(FoxBreedParams p, out float min, out float max, out float reach)
        {
            reach = BaitLevelReachPx(p);
            max = reach * Mathf.Clamp01(p.BaitPrepMaxDistFac);
            min = Mathf.Min(max, Mathf.Max(p.PounceMinDistance, reach * Mathf.Max(0f, p.BaitPrepMinDistFac)));
        }

        private int ambushHoldTick = NEVER_LOGGED;
        private Vector2 ambushFace;
        private float ambushGather;
        private int ambushGatherTick = NEVER_LOGGED;

        internal bool TurnRunningNow { get { return move != null && move.Turn.Running; } }

        internal float FacingDot(Vector2 dir)
        {
            if (bodyChunks == null || bodyChunks.Length < 3) return 1f;
            return Vector2.Dot(Dir(bodyChunks[2].pos, bodyChunks[0].pos), dir);
        }

        internal void HoldGroundThisTick(Vector2 face)
        {
            ambushHoldTick = GameTick;
            ambushFace = face;
        }

        private void AmbushHoldStep(FoxBreedParams p)
        {
            if (latchedConnection != default(MovementConnection)) ClearLatch("ambush_hold");

            if ((ambushFace.sqrMagnitude > 0.01f || move.Turn.Running)
                && move.Turn.Drive(p, ambushFace, default(MovementConnection), false))
            {
                return;
            }

            if (!Grounded || bodyChunks == null) return;
            float cap = Mathf.Max(0f, p.Acceleration);
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                bodyChunks[i].vel.x += Mathf.Clamp(-bodyChunks[i].vel.x, -cap, cap);
            }
        }

        internal float HoldAmbushGather(FoxBreedParams p)
        {
            if (move == null || move.Pounce.Running || move.Pounce.GatherRelease > 0)
            {
                ambushGather = 0f;
                return 0f;
            }
            ambushGather = Mathf.Min(1f, ambushGather + 1f / Math.Max(1, p.PounceGatherFrames));
            ambushGatherTick = GameTick;
            PounceGather = ambushGather;
            return ambushGather;
        }

        internal void YieldAmbushGather()
        {
            ambushGather = 0f;
        }

        private void TickAmbushGather(FoxBreedParams p)
        {
            if (ambushGather <= 0f) return;
            if (ambushGatherTick >= GameTick - 1) return;
            if (move == null || move.Pounce.Running || move.Pounce.GatherRelease > 0)
            {
                ambushGather = 0f;
                return;
            }
            ambushGather = Mathf.Max(0f, ambushGather - 1f / Math.Max(1, p.PounceGatherReleaseTicks));
            PounceGather = ambushGather;
        }

        internal FoxMoveBid TryBaitPounce(FoxBreedParams p, Vector2 targetPos, bool roll)
        {
            if (EvadeStaggering) return FoxMoveBid.No("evade_stagger", "");
            var s = new FoxMoveSituation { Target = targetPos, Why = "bait", Prepared = true, NoChanceRoll = !roll };
            FoxMoveBid bid = move.Pounce.Ask(p, s);
            if (bid.Ok) move.Pounce.Begin(p, s, bid);
            return bid;
        }

        internal string PounceLastEnd { get { return move == null ? "none" : move.Pounce.LastEndReason; } }
    }

    internal sealed class FoxBait
    {
        internal enum Stage
        {
            None,
            Seek,
            Plan,
            Carry,
            Settle,
            GoPrep,
            Wait,
            Strike
        }

        private const float TilePx = 20f;
        private const int DominanceSeedSalt = 5;
        internal const int SeeAroundCorners = 10;

        private readonly FoxAI ai;
        private readonly Fox fox;

        private Stage stage = Stage.None;
        private int stageTicks;
        private AbstractPhysicalObject item;
        private IntVector2 baitTile, prepTile;
        private Vector2 placedPos;
        private int cooldown, chooseCountdown, planCountdown, planAttempts;

        private string typesSource;
        private readonly HashSet<string> types = new HashSet<string>();
        private readonly IntVector2[] ray = new IntVector2[96];

        private int faceTicks;
        private bool faceGaveUp;
        private bool armed;
        private bool launchPending;
        private Creature strikeTarget;
        private bool strikeBit;
        private Creature trigSubject;
        private string trigCause;
        private bool chasing;

        internal FoxBait(FoxAI ai, Fox fox)
        {
            this.ai = ai;
            this.fox = fox;
        }

        internal bool Active { get { return stage != Stage.None; } }
        internal bool Striking { get { return stage == Stage.Strike; } }

        internal bool HoldsHunt(FoxBreedParams p)
        {
            if (!Active || p == null || !p.BaitEnabled) return false;
            if (stage == Stage.Strike) return true;
            if (p.BaitHoldsAgainstHunt) return true;
            return p.BaitWaitHoldsAgainstHunt && stage == Stage.Wait;
        }

        internal bool TrackItem(AbstractPhysicalObject obj)
        {
            FoxBreedParams p = fox.P;
            if (p == null || !p.BaitEnabled || obj == null || obj.type == null) return false;

            string type = obj.type.ToString();
            if (!Types(p).Contains(type))
            {
                return false;
            }

            if (p.BaitPearlMiscOnly && obj is DataPearl.AbstractDataPearl)
            {
                var pearl = (DataPearl.AbstractDataPearl)obj;
                if (!(pearl.dataPearlType == DataPearl.AbstractDataPearl.DataPearlType.Misc))
                {
                    return false;
                }
            }

            if (p.BaitDominanceGate)
            {
                float roll = ai.creature.world.game.SeededRandom(obj.ID.RandomSeed + DominanceSeedSalt);
                float dominance = ai.creature.personality.dominance;
                if (roll > dominance)
                {
                    return false;
                }
            }

            return true;
        }

        private HashSet<string> Types(FoxBreedParams p)
        {
            string src = p.BaitItemTypes ?? string.Empty;
            if (src != typesSource)
            {
                types.Clear();
                string[] parts = src.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    string t = parts[i].Trim();
                    if (t.Length > 0) types.Add(t);
                }
            }
            typesSource = src;
            return types;
        }

        internal void Update(FoxBreedParams p)
        {
            ItemTracker t = ai.itemTracker;
            if (t != null)
            {
                t.maxTrackedItems = Math.Max(1, p.BaitTrackMaxItems);
                t.framesToRememberItems = p.BaitTrackRememberTicks;
                t.forgetDistance = p.BaitTrackForgetTiles;
            }
            if (cooldown > 0) cooldown--;
            if (stage != Stage.None) stageTicks++;
        }

        internal FoxAI.Behavior Arbitrate(FoxBreedParams p, FoxAI.Behavior current)
        {
            if (current == FoxAI.Behavior.Bait) current = FoxAI.Behavior.Idle;

            if (!Active && fox.HeldItem != null)
            {
                item = fox.HeldItem.abstractPhysicalObject;
                Abort(p, "orphaned_item", null);
                return current;
            }

            if (!p.BaitEnabled)
            {
                if (Active) Abort(p, "disabled", null);
                chasing = false;
                return current;
            }

            bool hunting = current == FoxAI.Behavior.Hunt || current == FoxAI.Behavior.Stalk;

            if (chasing)
            {
                if (hunting)
                {
                    return current;
                }
                chasing = false;
                cooldown = Math.Max(cooldown, Math.Max(0, p.BaitCooldownTicks));
            }

            if (launchPending) ReadLaunch(p);

            if (stage == Stage.Strike)
            {
                string decided;
                string result = StrikeVerdict(out decided);
                if (result == null) return FoxAI.Behavior.Bait;
                return EndStrike(p, current, result, decided);
            }

            bool free = current == FoxAI.Behavior.Idle || current == FoxAI.Behavior.Wander
                        || (hunting && (p.BaitHoldsAgainstHunt || HoldsHunt(p)));

            if (Active)
            {
                if (free) return FoxAI.Behavior.Bait;
                string by = null;
                Abort(p, "interrupted", by);
                return current;
            }

            if (!free || cooldown > 0) return current;
            if (fox.grasps == null || fox.grasps.Length == 0 || fox.grasps[0] != null) return current;

            if (--chooseCountdown > 0) return current;
            chooseCountdown = Math.Max(1, p.BaitChooseIntervalTicks);
            return Choose(p) ? FoxAI.Behavior.Bait : current;
        }

        internal void Act(FoxBreedParams p, float stuckBoost)
        {
            fox.runSpeed = stuckBoost;
            if (fox.room == null) return;

            switch (stage)
            {
                case Stage.Seek:   ActSeek(p, stuckBoost); break;
                case Stage.Plan:   ActPlan(p); break;
                case Stage.Carry:  ActCarry(p, stuckBoost); break;
                case Stage.Settle: ActSettle(p); break;
                case Stage.GoPrep: ActGoPrep(p, stuckBoost); break;
                case Stage.Wait:   ActWait(p, stuckBoost); break;
                case Stage.Strike: ActStrike(p, stuckBoost); break;
            }
        }

        internal void NewRoom()
        {
            if (Active) Abort(fox.P, "room_changed", null);
            trigSubject = null;
            strikeTarget = null;
        }

        internal void NoteHuntProgress(string reason)
        {
            if (stage == Stage.Strike && reason == "bite") strikeBit = true;
        }

        private bool Choose(FoxBreedParams p)
        {
            ItemTracker t = ai.itemTracker;
            if (t == null || fox.room == null) return false;

            IntVector2 here = fox.room.GetTilePosition(fox.mainBodyChunk.pos);
            AbstractPhysicalObject best = null;
            float bestScore = float.MinValue, bestTiles = 0f;
            bool bestVisible = false, bestInGrab = false;

            for (int i = 0; i < t.ItemCount; i++)
            {
                ItemTracker.ItemRepresentation rep = t.GetRep(i);
                AbstractPhysicalObject apo = rep.representedItem;
                string no = Unusable(apo);
                bool inGrab = false;
                if (no == null)
                {
                    PhysicalObject obj = apo.realizedObject;
                    inGrab = Vector2.Distance(obj.firstChunk.pos, fox.mainBodyChunk.pos) < p.BaitGrabRangePx;
                    if (!inGrab)
                    {
                        if (!p.BaitSeekEnabled) no = "out_of_grab_range_and_seek_off";
                        else if (!Reachable(TileOf(obj))) no = "unreachable";
                    }
                }
                if (no != null)
                {
                    continue;
                }

                float tiles = rep.BestGuessForPosition().Tile.FloatDist(here);
                bool visible = rep.VisualContact;
                float score = (visible ? Mathf.Max(1f, p.BaitPickVisibleFac) : 1f) / (1f + tiles);
                score /= Mathf.Lerp(rep.forgetCounter, 100f, 0.7f);
                if (score > bestScore)
                {
                    best = apo; bestScore = score; bestTiles = tiles;
                    bestVisible = visible; bestInGrab = inGrab;
                }
            }

            if (best == null)
            {
                return false;
            }

            item = best;
            Enter(Stage.Seek);
            return true;
        }

        private string Unusable(AbstractPhysicalObject apo)
        {
            if (apo == null) return "none";
            PhysicalObject obj = apo.realizedObject;
            if (obj == null) return "not_realized";
            if (obj.room != fox.room) return "other_room";
            if (obj.slatedForDeletetion) return "deleted";
            if (obj.grabbedBy.Count > 0) return "held";
            if (apo.stuckObjects.Count > 0) return "stuck";
            Weapon w = obj as Weapon;
            if (w != null && !(w.mode == Weapon.Mode.Free)) return "weapon_not_free";
            if (!fox.CanCarry(obj)) return "too_heavy";
            return null;
        }

        private void ActSeek(FoxBreedParams p, float stuckBoost)
        {
            string no = Unusable(item);
            if (no != null) { Abort(p, "item_lost", no); return; }

            PhysicalObject obj = item.realizedObject;
            float dist = Vector2.Distance(obj.firstChunk.pos, fox.mainBodyChunk.pos);
            if (dist < p.BaitGrabRangePx)
            {
                bool took = fox.TryGrabItem(p, obj);
                if (!took) { Abort(p, "grab_refused", null); return; }
                planAttempts = 0;
                planCountdown = 0;
                Enter(Stage.Plan);
                return;
            }

            if (!p.BaitSeekEnabled) { Abort(p, "seek_disabled", null); return; }
            if (stageTicks > Math.Max(1, p.BaitSeekMaxTicks)) { Abort(p, "seek_timeout", null); return; }
            Go(p, TileOf(obj), stuckBoost);
        }

        private void ActPlan(FoxBreedParams p)
        {
            if (!Holding()) { Abort(p, "lost_grasp", null); return; }
            if (--planCountdown > 0) return;
            planCountdown = Math.Max(1, p.BaitChooseIntervalTicks);
            planAttempts++;

            if (PlanSpots(p))
            {
                Enter(Stage.Carry);
                return;
            }
            if (planAttempts >= Math.Max(1, p.BaitPlanMaxAttempts)) Abort(p, "no_spot", null);
        }

        private void ActCarry(FoxBreedParams p, float stuckBoost)
        {
            if (!Holding()) { Abort(p, "lost_grasp", null); return; }

            if (AtTile(p, baitTile))
            {
                PhysicalObject held = fox.HeldItem;
                bool released = fox.ReleaseItem(held);
                if (!released) { Abort(p, "release_failed", null); return; }
                Enter(Stage.Settle);
                return;
            }

            if (stageTicks > Math.Max(1, p.BaitCarryMaxTicks)) { Abort(p, "carry_timeout", null); return; }
            Go(p, baitTile, stuckBoost);
        }

        private void ActSettle(FoxBreedParams p)
        {
            string gone = BaitGone(p, false);
            if (gone != null) { Abort(p, gone, null); return; }
            if (stageTicks < Math.Max(0, p.BaitSettleTicks)) return;

            PhysicalObject obj = item.realizedObject;
            placedPos = obj.firstChunk.pos;
            IntVector2 actual = fox.room.GetTilePosition(placedPos);
            var c = new Census();
            IntVector2 prep;
            FoxBaitLine line;
            float score, d, lv, cv;
            bool ok = BestPrepFor(p, actual, prepTile, true, ref c, out prep, out line, out score, out d, out lv, out cv);
            if (!ok) { Abort(p, "line_lost", null); return; }
            baitTile = actual;
            prepTile = prep;
            Enter(Stage.GoPrep);
        }

        private void ActGoPrep(FoxBreedParams p, float stuckBoost)
        {
            string gone = BaitGone(p, true);
            if (gone != null) { Abort(p, gone, null); return; }

            if (AtTile(p, prepTile))
            {
                ResetStand();
                Enter(Stage.Wait);
                return;
            }
            if (stageTicks > Math.Max(1, p.BaitCarryMaxTicks)) { Abort(p, "prep_timeout", null); return; }
            Go(p, prepTile, stuckBoost);
        }

        private void ActWait(FoxBreedParams p, float stuckBoost)
        {
            PhysicalObject bait = item != null ? item.realizedObject : null;
            bool present = bait != null && bait.room == fox.room && !bait.slatedForDeletetion;

            string cause = null;
            float baitDist = -1f;
            Creature subject = present ? FindSubject(p, bait, out cause, out baitDist) : null;
            NoteTrigger(p, subject, cause, baitDist);

            if (subject == null)
            {
                string gone = BaitGone(p, true);
                if (gone != null) { Abort(p, gone, null); return; }

                if (p.BaitWaitMaxTicks > 0 && stageTicks > p.BaitWaitMaxTicks)
                {
                    Abort(p, "wait_timeout", null);
                    return;
                }
            }

            if (!AtTile(p, prepTile))
            {
                ResetStand();
                Go(p, prepTile, stuckBoost);
                return;
            }

            ai.creature.abstractAI.SetDestination(Coord(prepTile));

            Vector2 face;
            float dot;
            bool facing = Facing(p, out face, out dot);
            bool turning = fox.TurnRunningNow;
            bool wantTurn = !facing && !faceGaveUp;
            if (wantTurn)
            {
                faceTicks++;
                if (faceTicks > Math.Max(0, p.BaitFaceMaxTicks))
                {
                    faceGaveUp = true;
                    wantTurn = false;
                }
            }
            if (wantTurn || turning)
            {
                armed = false;
                fox.runSpeed = Mathf.Max(p.BaitFaceRunSpeed, stuckBoost);
                fox.HoldGroundThisTick(wantTurn ? face : Vector2.zero);
                return;
            }

            fox.runSpeed = Mathf.Max(p.BaitWaitRunSpeed, stuckBoost);
            fox.HoldGroundThisTick(Vector2.zero);
            armed = fox.HoldAmbushGather(p) >= 1f;

            if (subject != null)
            {
                if (!armed)
                {
                }
                else
                {
                    FoxMoveBid bid = fox.TryBaitPounce(p, subject.mainBodyChunk.pos, p.BaitPounceRoll);
                    if (!bid.Ok)
                    {
                    }
                    else
                    {
                        fox.YieldAmbushGather();
                        armed = false;
                        launchPending = true;
                        strikeTarget = subject;
                        strikeBit = false;
                        return;
                    }
                }
            }
        }

        private void ReadLaunch(FoxBreedParams p)
        {
            launchPending = false;
            if (stage != Stage.Wait) { strikeTarget = null; return; }

            if (fox.pounce == Fox.PounceState.None && fox.PounceLastEnd == "launch_refused")
            {
                strikeTarget = null;
                return;
            }

            Enter(Stage.Strike);
        }

        private void ActStrike(FoxBreedParams p, float stuckBoost)
        {
            fox.runSpeed = Mathf.Max(p.RunSpeedHunt, stuckBoost);
            Creature t = strikeTarget;
            if (t == null || t.room != fox.room || t.mainBodyChunk == null) return;
            if (!ReferenceEquals(fox.chaseTarget, t)) fox.SetChaseTarget(t, "bait_strike");
            ai.creature.abstractAI.SetDestination(fox.room.GetWorldCoordinate(t.mainBodyChunk.pos));
        }

        private string StrikeVerdict(out string decided)
        {
            Creature t = strikeTarget;
            if (t != null && ReferenceEquals(fox.HeldPrey, t)) { decided = "grabbed"; return "hit"; }
            if (strikeBit) { decided = "bite_landed"; return "hit"; }
            if (t != null && t.dead) { decided = "target_dead"; return "hit"; }

            Fox.PounceState ps = fox.pounce;
            if (ps == Fox.PounceState.Crouch || ps == Fox.PounceState.Propel)
            {
                decided = null;
                return null;
            }
            if (t == null || t.room != fox.room) decided = "target_gone";
            else if (ps == Fox.PounceState.Recover) decided = "landed_no_contact";
            else decided = "pounce_ended";
            return "miss";
        }

        private FoxAI.Behavior EndStrike(FoxBreedParams p, FoxAI.Behavior current, string result, string decided)
        {
            Creature t = strikeTarget;

            bool handed = false;
            string detail = null;
            if (t != null && !t.dead && fox.HeldPrey == null)
            {
                handed = ai.BeginAmbushChase(t, p, ref detail);
            }

            bool free = current == FoxAI.Behavior.Idle || current == FoxAI.Behavior.Wander
                        || current == FoxAI.Behavior.Hunt || current == FoxAI.Behavior.Stalk;
            FoxAI.Behavior next = (handed && free) ? FoxAI.Behavior.Hunt : current;

            Abort(p, result == "hit" ? "pounce_hit" : "pounce_miss", null);
            chasing = handed;
            return next;
        }

        private Creature FindSubject(FoxBreedParams p, PhysicalObject bait, out string cause, out float baitDist)
        {
            cause = null;
            baitDist = -1f;
            Vector2 at = bait.firstChunk.pos;

            for (int i = 0; i < bait.grabbedBy.Count; i++)
            {
                Creature g = bait.grabbedBy[i] != null ? bait.grabbedBy[i].grabber : null;
                if (g == null || g == fox || g.room != fox.room) continue;
                if (IsSubject(p, g))
                {
                    cause = "grab";
                    baitDist = NearestChunkDist(g, at);
                    return g;
                }
            }

            float range = Mathf.Max(0f, p.BaitTriggerRangePx);
            Creature best = null;
            float bestDist = float.MaxValue;
            List<AbstractCreature> here = fox.room.abstractRoom.creatures;
            for (int i = 0; i < here.Count; i++)
            {
                Creature c = here[i].realizedCreature;
                if (c == null || c == fox || c.room != fox.room || c.dead || c.bodyChunks == null) continue;
                float d = NearestChunkDist(c, at);
                if (d > range) continue;
                if (!IsSubject(p, c))
                {
                    continue;
                }
                if (d < bestDist) { best = c; bestDist = d; }
            }
            if (best != null)
            {
                cause = "range";
                baitDist = bestDist;
            }
            return best;
        }

        private bool IsSubject(FoxBreedParams p, Creature c)
        {
            if (c == null || c == fox || c.dead || c.abstractCreature == null) return false;
            if (FoxAI.IsRouteKey(p.BaitSpringRule, "anything")) return true;
            if (FoxAI.IsRouteKey(p.BaitSpringRule, "player")) return c is Player;
            string src;
            return FoxAI.RouteFor(ai.AmbushRelationship(c.abstractCreature, out src).type, p)
                   == FoxAI.RelRoute.Prey;
        }

        private static float NearestChunkDist(Creature c, Vector2 at)
        {
            float best = float.MaxValue;
            for (int i = 0; i < c.bodyChunks.Length; i++)
            {
                float d = Vector2.Distance(c.bodyChunks[i].pos, at);
                if (d < best) best = d;
            }
            return best;
        }

        private bool Facing(FoxBreedParams p, out Vector2 face, out float dot)
        {
            float dx = placedPos.x - fox.bodyChunks[1].pos.x;
            if (Mathf.Abs(dx) < 1f)
            {
                face = Vector2.zero;
                dot = 1f;
                return true;
            }
            face = new Vector2(Mathf.Sign(dx), 0f);
            dot = fox.FacingDot(face);
            return dot > p.TurnTriggerDot;
        }

        private void ResetStand()
        {
            faceTicks = 0;
            faceGaveUp = false;
            armed = false;
        }

        private void NoteTrigger(FoxBreedParams p, Creature subject, string cause, float baitDist)
        {
            if (subject == null)
            {
                trigSubject = null;
                trigCause = null;
                return;
            }
            if (!ReferenceEquals(subject, trigSubject) || cause != trigCause)
            {
                trigSubject = subject;
                trigCause = cause;
            }
        }

        private struct Census
        {
            internal int BaitExamined;
            internal int Reach;
            internal bool HasNear;
            internal FoxBaitLine Near;
        }

        private static void NoteRejected(ref Census c, FoxBaitLine line, int px, int py, IntVector2 bait)
        {
            bool closer;
            if (!c.HasNear) closer = true;
            else if (line.Fail != c.Near.Fail) closer = line.Fail > c.Near.Fail;
            else if (line.Fail >= 5) closer = (line.RangePx - line.WantDist) > (c.Near.RangePx - c.Near.WantDist);
            else if (line.Fail == 1) closer = BandMiss(line) < BandMiss(c.Near);
            else closer = false;
            if (!closer) return;
            c.HasNear = true;
            c.Near = line;
        }

        private static float BandMiss(FoxBaitLine l)
        {
            if (l.DistPx < l.BandMin) return l.BandMin - l.DistPx;
            if (l.DistPx > l.BandMax) return l.DistPx - l.BandMax;
            return 0f;
        }

        private bool PlanSpots(FoxBreedParams p)
        {
            Room room = fox.room;
            IntVector2 here = room.GetTilePosition(fox.mainBodyChunk.pos);
            int radius = Math.Max(1, p.BaitSpotRadiusTiles);
            int want = Math.Max(1, p.BaitSpotCandidates);
            var c = new Census();

            bool have = false;
            float bestScore = float.MinValue, bestDist = 0f, bestLevel = 0f, bestCover = 0f, bestTravel = 0f;
            IntVector2 bestBait = here, bestPrep = here;

            bool exitOn = p.BaitExitWeight != 0f;
            string exitData = exitOn ? ExitDataState(room) : "off";
            float bestBase = 0f, baseBestScore = float.MinValue;
            int scored = 0, penalised = 0;
            if (baseScores == null || baseScores.Length < want) baseScores = new float[want];

            for (int draw = 0; draw < want * 4 && c.BaitExamined < want; draw++)
            {
                int x = draw == 0 ? here.x : here.x + UnityEngine.Random.Range(-radius, radius + 1);
                int y = draw == 0 ? here.y : here.y + UnityEngine.Random.Range(-radius, radius + 1);
                int floorY = -1;
                for (int k = 0; k <= radius; k++)
                {
                    if (FloorSpot(room, x, y - k)) { floorY = y - k; break; }
                }
                if (floorY < 0) { continue; }
                var bait = new IntVector2(x, floorY);
                if (!Reachable(bait)) { continue; }
                c.BaitExamined++;

                IntVector2 prep;
                FoxBaitLine line;
                float prepScore, d, lv, cv;
                if (!BestPrepFor(p, bait, bait, false, ref c, out prep, out line, out prepScore, out d, out lv, out cv))
                    continue;

                float travel = Mathf.Clamp01(bait.FloatDist(here) / radius);
                float baseScore = prepScore - travel * p.BaitSpotTravelWeight;
                float score = baseScore;
                ExitTerms exit = default(ExitTerms);
                if (exitOn)
                {
                    exit = ExitTermsFor(p, room, bait, prep, exitData);
                    score += p.BaitExitWeight * exit.Total;
                    if (exit.Total < 0f) penalised++;
                }
                if (scored < baseScores.Length) baseScores[scored] = baseScore;
                scored++;
                if (baseScore > baseBestScore) { baseBestScore = baseScore; }

                if (!have || score > bestScore)
                {
                    have = true; bestScore = score; bestBait = bait; bestPrep = prep;
                    bestDist = d; bestLevel = lv; bestCover = cv; bestTravel = travel;
                    bestBase = baseScore;
                }
            }

            if (!have) return false;
            baitTile = bestBait;
            prepTile = bestPrep;
            return true;
        }

        private struct ExitTerms
        {
            internal float Reach, Centre, Near, Pipe, None, Total;
            internal int Exits, Reachable, Min, Max, Pipes;
        }

        private float[] baseScores;

        private static string ExitDataState(Room room)
        {
            if (room == null || room.abstractRoom == null || room.aimap == null) return "no_room";
            if (!room.readyForAI) return "not_ready";
            CreatureTemplate sgc = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.StandardGroundCreature);
            if (sgc == null || sgc.PreBakedPathingIndex < 0) return "no_table";
            if (room.abstractRoom.exits <= 0) return "no_exits";
            return "ok";
        }

        private ExitTerms ExitTermsFor(FoxBreedParams p, Room room, IntVector2 bait, IntVector2 prep,
                                       string data)
        {
            var t = new ExitTerms();

            t.Pipe = -PipePenalty(p, room, bait, ref t.Pipes);
            if (p.BaitExitPipeAtPrep) t.Pipe -= PipePenalty(p, room, prep, ref t.Pipes);

            if (data == "ok")
            {
                try
                {
                    CreatureTemplate sgc =
                        StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.StandardGroundCreature);
                    t.Exits = room.abstractRoom.exits;
                    int min = int.MaxValue, max = 0;
                    for (int j = 0; j < t.Exits; j++)
                    {
                        int steps = room.aimap.ExitDistanceForCreature(
                            bait, room.abstractRoom.CommonToCreatureSpecificNodeIndex(j, sgc), sgc);
                        if (steps <= 0) continue;
                        t.Reachable++;
                        if (steps < min) min = steps;
                        if (steps > max) max = steps;
                    }

                    if (t.Reachable > 0)
                    {
                        t.Min = min;
                        t.Max = max;
                        t.Reach = t.Reachable * p.BaitExitReachBonus;
                        t.Centre = (float)min / max * p.BaitExitCentreBonus;
                        if (min < p.BaitExitNearSteps) t.Near = -p.BaitExitNearPenalty;
                    }
                    else
                    {
                        t.None = -p.BaitExitNonePenalty;
                    }
                }
                catch (Exception)
                {
                    t.Reach = t.Centre = t.Near = t.None = 0f;
                    t.Reachable = 0;
                }
            }

            t.Total = t.Reach + t.Centre + t.Near + t.Pipe + t.None;
            return t;
        }

        private static float PipePenalty(FoxBreedParams p, Room room, IntVector2 tile, ref int count)
        {
            if (room.shortcuts == null) return 0f;
            float sum = 0f;
            for (int k = 0; k < room.shortcuts.Length; k++)
            {
                if (room.shortcuts[k].StartTile.FloatDist(tile) >= p.BaitExitPipeTiles) continue;
                count++;
                sum += (room.shortcuts[k].shortCutType == ShortcutData.Type.RoomExit)
                    ? p.BaitExitPipeExitPenalty : p.BaitExitPipeOtherPenalty;
            }
            return sum;
        }

        private bool BestPrepFor(FoxBreedParams p, IntVector2 bait, IntVector2 preferred, bool hasPreferred,
                                 ref Census c, out IntVector2 prep, out FoxBaitLine bestLine,
                                 out float bestScore, out float distTerm, out float levelTerm, out float coverTerm)
        {
            Room room = fox.room;
            Vector2 aim = room.MiddleOfTile(bait.x, bait.y);
            int rise = Math.Max(0, p.BaitPrepMaxRiseTiles);
            float bandMin, bandMax, reachPx;
            fox.BaitBand(p, out bandMin, out bandMax, out reachPx);
            int maxCols = Mathf.FloorToInt(bandMax / TilePx);
            int minCols = Math.Max(1, Mathf.FloorToInt(bandMin / TilePx) - rise);

            prep = bait;
            bestLine = default(FoxBaitLine);
            bestScore = float.MinValue;
            distTerm = levelTerm = coverTerm = 0f;
            bool have = false;

            for (int sign = -1; sign <= 1; sign += 2)
            {
                for (int dx = minCols; dx <= maxCols; dx++)
                {
                    for (int dy = -rise; dy <= rise; dy++)
                    {
                        int x = bait.x + sign * dx, y = bait.y + dy;
                        if (!FloorSpot(room, x, y)) { continue; }

                        FoxBaitLine line = fox.BaitLine(p, room.MiddleOfTile(x, y), aim, ray);
                        if (!line.Ok)
                        {
                            switch (line.Fail)
                            {
                                case 1: break;
                                case 2: break;
                                case 3: break;
                                case 4: break;
                                default: c.Reach++; break;
                            }
                            NoteRejected(ref c, line, x, y, bait);
                            continue;
                        }
                        var tile = new IntVector2(x, y);
                        if (!Reachable(tile))
                        {
                            line.Fail = 6;
                            NoteRejected(ref c, line, x, y, bait);
                            continue;
                        }

                        float d = Mathf.InverseLerp(line.BandMin, line.BandMax, line.DistPx);
                        float lv = 1f - Mathf.Clamp01(Mathf.Abs(line.DyPx) / (Mathf.Max(1, rise) * TilePx));
                        float cv = 1f - Mathf.Clamp01(room.aimap.getAItile(x, y).visibility / 900f);
                        float score = d * p.BaitPrepDistWeight + lv * p.BaitPrepLevelWeight
                                      + cv * p.BaitPrepCoverWeight;
                        bool isPreferred = hasPreferred && x == preferred.x && y == preferred.y;
                        if (isPreferred || !have || score > bestScore)
                        {
                            have = true; prep = tile; bestLine = line; bestScore = score;
                            distTerm = d; levelTerm = lv; coverTerm = cv;
                            if (isPreferred) return true;
                        }
                    }
                }
            }
            return have;
        }

        private bool FloorSpot(Room room, int x, int y)
        {
            if (x < 1 || y < 1 || x >= room.TileWidth - 1 || y >= room.TileHeight - 2) return false;
            if (y <= room.defaultWaterLevel) return false;
            Room.Tile t = room.GetTile(x, y);
            if (t.Solid || t.Terrain != Room.Tile.TerrainType.Air) return false;
            if (room.GetTile(x, y + 1).Solid) return false;
            Room.Tile below = room.GetTile(x, y - 1);
            if (!below.Solid && below.Terrain != Room.Tile.TerrainType.Floor) return false;
            AItile a = room.aimap.getAItile(x, y);
            if (a == null || a.acc != AItile.Accessibility.Floor || a.narrowSpace) return false;
            return room.aimap.TileAccessibleToCreature(x, y, fox.Template);
        }

        private bool Holding()
        {
            PhysicalObject held = fox.HeldItem;
            return item != null && held != null && held.abstractPhysicalObject == item;
        }

        private string BaitGone(FoxBreedParams p, bool checkMoved)
        {
            if (item == null) return "bait_gone";
            PhysicalObject obj = item.realizedObject;
            if (obj == null || obj.room != fox.room || obj.slatedForDeletetion) return "bait_gone";
            if (obj.grabbedBy.Count > 0) return "bait_taken";
            if (checkMoved && Vector2.Distance(obj.firstChunk.pos, placedPos) > p.BaitMovedPx) return "bait_moved";
            return null;
        }

        private bool AtTile(FoxBreedParams p, IntVector2 tile)
        {
            if (fox.room == null) return false;
            Vector2 c = fox.room.MiddleOfTile(tile.x, tile.y);
            float r = Mathf.Max(5f, p.BaitArrivePx);
            for (int i = 0; i < fox.bodyChunks.Length; i++)
            {
                Vector2 d = fox.bodyChunks[i].pos - c;
                if (Mathf.Abs(d.x) <= r && Mathf.Abs(d.y) <= r + TilePx) return true;
            }
            return false;
        }

        private IntVector2 TileOf(PhysicalObject obj)
        {
            return fox.room.GetTilePosition(obj.firstChunk.pos);
        }

        private WorldCoordinate Coord(IntVector2 tile)
        {
            return new WorldCoordinate(fox.room.abstractRoom.index, tile.x, tile.y, -1);
        }

        private bool Reachable(IntVector2 tile)
        {
            return ai.pathFinder.CoordinateReachableAndGetbackable(Coord(tile));
        }

        private void Go(FoxBreedParams p, IntVector2 tile, float stuckBoost)
        {
            fox.runSpeed = Mathf.Max(p.BaitRunSpeed, stuckBoost);
            ai.creature.abstractAI.SetDestination(Coord(tile));
        }

        private void Enter(Stage next)
        {
            stage = next;
            stageTicks = 0;
        }

        private void Abort(FoxBreedParams p, string cause, string detail)
        {
            if (Holding()) fox.ReleaseItem(fox.HeldItem);
            cooldown = (p != null) ? Math.Max(0, p.BaitCooldownTicks) : 0;
            item = null;
            launchPending = false;
            strikeTarget = null;
            strikeBit = false;
            trigSubject = null;
            trigCause = null;
            ResetStand();
            Enter(Stage.None);
        }
    }
}
