using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal sealed class FoxHopPlanner
    {
        private const int FADE_DROP = 40;
        private const int FADE_NOT_AHEAD = 10;
        private const int TRACE_CACHE = 64;

        private readonly FoxAI ai;
        private readonly Fox fox;
        private readonly List<WorldCoordinate> futurePath = new List<WorldCoordinate>(32);
        private readonly List<HopFinder> finders = new List<HopFinder>();

        private PathFinder.PathingCell hereCell;

        private bool wasEnabled;

        internal FoxHopPlanner(FoxAI ai, Fox fox)
        {
            this.ai = ai;
            this.fox = fox;
        }

        internal int FinderCount { get { return finders.Count; } }

        internal void Update(FoxBreedParams p)
        {
            if (p == null) return;
            if (!p.HopEnabled)
            {
                if (wasEnabled)
                {
                    wasEnabled = false;
                }
                Clear();
                return;
            }
            if (!wasEnabled)
            {
                wasEnabled = true;
            }

            Room room = fox.RoomRef;
            if (room == null || room.aimap == null) { Clear(); return; }
            StandardPather pather = ai.pathFinder as StandardPather;
            if (pather == null) { Clear(); return; }

            string gate = BehaviourGate(p);
            if (gate != null) { Clear(); return; }

            if (fox.Move != null && fox.Move.Pounce.Running) { return; }

            PathFinder.PathingCell c = pather.PathingCellAtWorldCoordinate(fox.abstractCreature.pos);
            if (c != null && c != pather.fallbackPathingCell) hereCell = c;

            PathCost.Legality ceiling = FoxPounceRouter.Ceiling(p);
            float g, airFric;
            fox.FlightArcInputs(p, out g, out airFric);
            FoxArcModel arc = FoxArcModel.Of(p, g, airFric);

            TickFinders(p, pather, room, arc, ceiling);
            bool onPath = PeekAhead(p, pather);
            Seed(p, pather, room, ceiling, onPath);
        }

        private string BehaviourGate(FoxBreedParams p)
        {
            FoxAI.Behavior b = ai.CurrentBehavior;
            if (b == FoxAI.Behavior.Bait) return "bait_setup";
            if (b == FoxAI.Behavior.Stalk && !p.HopWhileStalking) return "stalking";
            if (p.HopWhen == "hunting") return b == FoxAI.Behavior.Hunt ? null : "not_hunting";
            if (p.HopWhen == "hurry")
                return (b == FoxAI.Behavior.Hunt || b == FoxAI.Behavior.Flee) ? null : "not_in_a_hurry";
            return null;
        }

        private void TickFinders(FoxBreedParams p, StandardPather pather, Room room, FoxArcModel arc,
                                 PathCost.Legality ceiling)
        {
            if (finders.Count == 0) return;
            int iters = Math.Max(1, Math.Max(1, p.HopItersPerTick) / finders.Count);
            WorldCoordinate dest = pather.GetDestination;
            bool destHere = dest.TileDefined && dest.room == fox.abstractCreature.pos.room;

            for (int i = finders.Count - 1; i >= 0; i--)
            {
                HopFinder f = finders[i];
                f.Fade++;
                if (!FoxPounceRouter.PathWeightComparison(hereCell, f.StartCell, ceiling)) f.Fade += FADE_NOT_AHEAD;
                if (f.Fade > FADE_DROP)
                {
                    finders.RemoveAt(i);
                    continue;
                }
                for (int n = 0; n < iters; n++)
                {
                    f.Iterate(p, pather, room, arc, ceiling, dest, destHere);
                }
                f.TakeTrials();
            }
        }

        private bool PeekAhead(FoxBreedParams p, StandardPather pather)
        {
            futurePath.Clear();
            bool onPath = false;
            WorldCoordinate walk = fox.abstractCreature.pos;
            int steps = Math.Max(0, p.HopPeekSteps);
            for (int i = 0; i < steps; i++)
            {
                MovementConnection step = pather.FollowPath(walk, false);
                if (step == default(MovementConnection)) break;
                walk = step.destinationCoord;
                futurePath.Add(walk);
                for (int j = 0; j < finders.Count; j++)
                {
                    if (finders[j].StartTile == walk.Tile) { finders[j].Fade = 0; onPath = true; }
                }
            }
            return onPath;
        }

        private void Seed(FoxBreedParams p, StandardPather pather, Room room, PathCost.Legality ceiling,
                          bool onPath)
        {
            int cap = onPath ? 1 : Math.Max(0, p.HopMaxFinders);
            if (finders.Count >= cap || futurePath.Count <= 1) return;

            WorldCoordinate here = fox.abstractCreature.pos;
            WorldCoordinate cand = futurePath[UnityEngine.Random.Range(0, futurePath.Count)];
            string refuse = null;
            if (!cand.TileDefined || cand.room != here.room) refuse = "other_room";
            else if (cand.Tile.FloatDist(here.Tile) <= 2f) refuse = "too_near";
            else
            {
                for (int i = 0; i < finders.Count; i++)
                {
                    if (finders[i].StartTile == cand.Tile) { refuse = "duplicate"; break; }
                }
            }
            if (refuse == null && fox.TileIsCorridorLike(cand.Tile)) refuse = "corridor";
            if (refuse == null && p.HopBlockNarrow && room.aimap.getAItile(cand.x, cand.y).narrowSpace) refuse = "narrow";
            PathFinder.PathingCell cell = null;
            if (refuse == null)
            {
                cell = pather.PathingCellAtWorldCoordinate(cand);
                if (!FoxPounceRouter.PathWeightComparison(hereCell, cell, ceiling)) refuse = "not_progress";
            }
            if (refuse != null) { return; }

            var f = new HopFinder(fox, room, cand.Tile, cell);
            f.NewTrial(p, room);
            finders.Add(f);
        }

        internal HopFinder LaunchHere(FoxBreedParams p, out string why)
        {
            why = "no_finder_here";
            StandardPather pather = ai.pathFinder as StandardPather;
            if (pather == null) { why = "no_pather"; return null; }
            IntVector2 tile = fox.abstractCreature.pos.Tile;
            PathCost.Legality ceiling = FoxPounceRouter.Ceiling(p);
            for (int i = 0; i < finders.Count; i++)
            {
                HopFinder f = finders[i];
                if (f.StartTile != tile) continue;
                string w;
                if (Beneficial(p, pather, f, ceiling, out w)) { why = w; return f; }
                why = w;
            }
            return null;
        }

        private bool Beneficial(FoxBreedParams p, StandardPather pather, HopFinder f,
                                PathCost.Legality ceiling, out string why)
        {
            PathFinder.PathingCell land = f.BestCell;
            if (land == null) { why = "no_arc_found"; return false; }
            IntVector2 lt = land.worldCoordinate.Tile;
            if (f.StartTile.FloatDist(lt) <= p.HopMinTilesOrigin) { why = "too_short_from_origin"; return false; }
            WorldCoordinate dest = pather.GetDestination;
            if (dest.TileDefined && dest.room == fox.abstractCreature.pos.room
                && dest.Tile.FloatDist(lt) <= p.HopMinTilesDest)
            {
                why = "too_close_to_dest"; return false;
            }
            if (FoxPounceRouter.PathWeightComparison(land, hereCell, ceiling)) { why = "worse_than_standing_here"; return false; }
            if (FoxPounceRouter.PathWeightComparison(land, f.StartCell, ceiling)) { why = "worse_than_launch_tile"; return false; }
            if (land.generation != pather.pathGeneration) { why = "stale_generation"; return false; }
            why = "beneficial";
            return true;
        }

        internal Vector2 LandingDirection(FoxBreedParams p, HopFinder f)
        {
            StandardPather pather = ai.pathFinder as StandardPather;
            Room room = fox.RoomRef;
            if (pather == null || room == null || f == null || f.BestCell == null) return Vector2.zero;
            PathCost.Legality ceiling = FoxPounceRouter.Ceiling(p);
            PathFinder.PathingCell goal = f.BestCell;
            PathFinder.PathingCell best = goal;
            IntVector2 gt = goal.worldCoordinate.Tile;
            for (int i = 0; i < Custom.eightDirections.Length; i++)
            {
                IntVector2 d = Custom.eightDirections[i];
                PathFinder.PathingCell c = pather.PathingCellAtWorldCoordinate(
                    room.GetWorldCoordinate(new IntVector2(gt.x + d.x, gt.y + d.y)));
                if (c == null || c == pather.fallbackPathingCell) continue;
                if (FoxPounceRouter.PathWeightComparison(best, c, ceiling)) best = c;
            }
            if (best == goal) return Vector2.zero;
            IntVector2 bt = best.worldCoordinate.Tile;
            Vector2 v = room.MiddleOfTile(bt.x, bt.y) - room.MiddleOfTile(gt.x, gt.y);
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.zero;
        }

        internal void OnPicked()
        {
            finders.Clear();
        }

        internal void NoteEnded(bool landed)
        {
        }

        private static string Res(PathFinder.PathingCell c)
        {
            return null;
        }

        internal void NewRoom()
        {
            Clear();
            hereCell = null;
        }

        private void Clear()
        {
            if (finders.Count > 0) finders.Clear();
            futurePath.Clear();
        }

        internal sealed class HopFinder
        {
            private readonly Fox fox;
            private IntVector2[] trace = new IntVector2[TRACE_CACHE];

            internal readonly IntVector2 StartTile;
            internal readonly PathFinder.PathingCell StartCell;
            private readonly bool startOpenAbove;

            internal int Fade;
            private int trialsSinceTaken;

            internal PathFinder.PathingCell BestCell;
            internal Vector2 BestVel;
            internal int BestTicks;
            internal float BestPower;

            private Vector2 pos, lastPos, vel, trialVel;
            private float trialPower;
            private int trialTick;
            private bool ventured;

            internal HopFinder(Fox fox, Room room, IntVector2 startTile, PathFinder.PathingCell startCell)
            {
                this.fox = fox;
                StartTile = startTile;
                StartCell = startCell;
                startOpenAbove = !room.GetTile(startTile.x, startTile.y + 1).Solid;
                pos = room.MiddleOfTile(startTile.x, startTile.y);
                lastPos = pos;
            }

            internal int TakeTrials()
            {
                int n = trialsSinceTaken;
                trialsSinceTaken = 0;
                return n;
            }

            internal void NewTrial(FoxBreedParams p, Room room)
            {
                trialsSinceTaken++;
                float power = UnityEngine.Random.value * Mathf.Sqrt(Mathf.Clamp01(fox.runSpeed));
                if (room.aimap.getTerrainProximity(StartTile.x, StartTile.y) > 1) power *= 0.5f;
                float speed = Mathf.Lerp(Mathf.Clamp01(p.HopMinSpeedFrac), 1f, power)
                              * fox.EffectiveLaunchSpeed(p) * Mathf.Max(0f, p.HopSpeedFac);
                if (fox.grasps != null && fox.grasps.Length > 0 && fox.grasps[0] != null)
                    speed *= Mathf.Max(0f, p.HopCarrySpeedFac);
                HealthState hs = fox.abstractCreature != null ? fox.abstractCreature.state as HealthState : null;
                if (hs != null) speed *= Mathf.Lerp(1f, hs.ClampedHealth, 0.5f + 0.5f * UnityEngine.Random.value);

                float deg = 45f * Mathf.Pow(UnityEngine.Random.value, 0.75f)
                            + 135f * Mathf.Pow(UnityEngine.Random.value, 2f);
                if (UnityEngine.Random.value < 0.5f) deg = -deg;
                float rad = deg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

                trialVel = Fox.LaunchVelocity(p, dir, speed);
                trialPower = power;
                pos = room.MiddleOfTile(StartTile.x, StartTile.y);
                lastPos = pos;
                vel = trialVel;
                trialTick = 0;
                ventured = false;
            }

            internal bool Iterate(FoxBreedParams p, StandardPather pather, Room room, FoxArcModel arc,
                                  PathCost.Legality ceiling, WorldCoordinate dest, bool destHere)
            {
                bool improved = false;
                lastPos = pos;
                trialTick++;
                arc.Step(ref vel, ref pos.x, ref pos.y, trialTick);

                int n;
                for (n = SharedPhysics.RayTracedTilesArray(lastPos, pos, trace);
                     n >= trace.Length;
                     n = SharedPhysics.RayTracedTilesArray(lastPos, pos, trace))
                {
                    Array.Resize(ref trace, trace.Length + TRACE_CACHE);
                }

                Vector2 seg = pos - lastPos;
                Vector2 perp = seg.sqrMagnitude > 0.000001f ? new Vector2(-seg.y, seg.x).normalized : Vector2.right;
                float clear = Mathf.Lerp(p.HopArcClearNearPx, p.HopArcClearFarPx, Mathf.InverseLerp(5f, 20f, trialTick));
                int minOrigin = Math.Max(p.HopMinTilesOrigin, Math.Min(20, (int)(trialVel.magnitude / 3f)));

                for (int i = 0; i < n; i++)
                {
                    int tx = trace[i].x, ty = trace[i].y;
                    if (ty < 0 || room.GetTile(tx, ty).Solid || ty < room.defaultWaterLevel) { NewTrial(p, room); return improved; }
                    int prevTy = i > 0 ? trace[i - 1].y : room.GetTilePosition(lastPos).y;
                    if (ty < prevTy && room.GetTile(tx, ty).Terrain == Room.Tile.TerrainType.Floor) { NewTrial(p, room); return improved; }
                    if (p.HopBlockNarrow && room.aimap.getAItile(tx, ty).narrowSpace) { NewTrial(p, room); return improved; }

                    bool beam = room.GetTile(tx, ty).verticalBeam || room.GetTile(tx, ty).horizontalBeam;
                    int prox = room.aimap.getTerrainProximity(tx, ty);
                    if (!ventured && prox > 1 && !beam) ventured = true;

                    if (ventured && (prox == 1 || beam)
                        && room.aimap.TileAccessibleToCreature(tx, ty, fox.Template)
                        && !fox.ClimbTileBarsLeap(tx, ty)
                        && StartTile.FloatDist(trace[i]) > minOrigin
                        && (!destHere || dest.Tile.FloatDist(trace[i]) > p.HopMinTilesDest))
                    {
                        PathFinder.PathingCell cell = pather.PathingCellAtWorldCoordinate(room.GetWorldCoordinate(trace[i]));
                        if (cell != null && cell.costToGoal.legality <= ceiling
                            && FoxPounceRouter.PathWeightComparison(BestCell, cell, ceiling))
                        {
                            BestCell = cell;
                            BestVel = trialVel;
                            BestTicks = trialTick;
                            BestPower = trialPower;
                            improved = true;
                        }
                    }

                    Vector2 mid = room.MiddleOfTile(tx, ty);
                    if ((startOpenAbove && room.GetTile(tx, ty + 1).Solid)
                        || SolidAt(room, mid + perp * clear) || SolidAt(room, mid - perp * clear))
                    {
                        NewTrial(p, room);
                        return improved;
                    }
                }

                if (trialTick > p.PounceFlightMaxFrames) NewTrial(p, room);
                return improved;
            }

            private static bool SolidAt(Room room, Vector2 at)
            {
                IntVector2 t = room.GetTilePosition(at);
                return room.GetTile(t.x, t.y).Solid;
            }
        }
    }

    internal sealed class FoxHopAction : FoxMoveAction
    {
        internal FoxHopAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "hop"; } }
        internal override bool Persistent { get { return false; } }

        private FoxHopPlanner.HopFinder picked;
        private int pickTick = -1000000;

        private IntVector2 hopStart, hopLand;
        private int hopCharge;

        internal bool Consider(FoxBreedParams p, FoxMoveSituation s)
        {
            if (p == null || !p.HopEnabled) return false;
            FoxMoveBid bid = Ask(p, s);
            if (!bid.Ok) return false;
            Begin(p, s, bid);
            return true;
        }

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            picked = null;
            string gate = StateGate(p);
            FoxHopPlanner planner = fox.AI != null ? fox.AI.HopPlanner : null;
            if (gate == null && planner == null) gate = "no_planner";
            if (gate != null)
            {
                return FoxMoveBid.No(gate, null);
            }

            string why;
            FoxHopPlanner.HopFinder f = planner.LaunchHere(p, out why);
            if (f == null)
            {
                return FoxMoveBid.No(why, null);
            }
            picked = f;
            pickTick = fox.MoveTick;
            return FoxMoveBid.Yes("route", null);
        }

        private string StateGate(FoxBreedParams p)
        {
            if (!p.HopEnabled) return "disabled";
            if (Cooldown > 0) return "cooldown";
            if (fox.dead || !fox.Consious) return "not_conscious";
            if (fox.EngineOwnsBody) return "engine_owns_body";
            FoxMoveEngine move = fox.Move;
            if (move == null) return "no_engine";
            if (move.Pounce.Running) return "leap_running";
            if (move.RunningActionNameExcept(this) != null) return "action_running";
            if (fox.PounceGather > 0f) return "coiled";
            if (fox.AI != null && fox.AI.Bait != null && fox.AI.Bait.Active) return "bait_setup";
            if (fox.grabbedBy != null && fox.grabbedBy.Count > 0) return "grabbed";
            if (fox.Swimming || fox.Submersion > 0f) return "water";
            if (fox.PoleHeld) return "pole_hold";
            if (fox.WallHeld) return "wall_hold";
            if (fox.HopBodyInCorridor()) return "corridor";
            if (!fox.Grounded) return "airborne";
            if (fox.EvadeStaggering) return "evade_stagger";
            return null;
        }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            FoxHopPlanner.HopFinder f = picked;
            picked = null;
            FoxHopPlanner planner = fox.AI != null ? fox.AI.HopPlanner : null;
            Room room = fox.RoomRef;
            if (f == null || f.BestCell == null || planner == null || room == null || pickTick != fox.MoveTick)
            {
                return;
            }

            hopStart = f.StartTile;
            hopLand = f.BestCell.worldCoordinate.Tile;
            hopCharge = ChargeTicksFor(p, f.BestPower);
            Vector2 aim = room.MiddleOfTile(hopLand.x, hopLand.y);

            planner.OnPicked();

            Vector2 landingDir = planner.LandingDirection(p, f);
            fox.Move.Pounce.Begin(p,
                new FoxMoveSituation
                {
                    Conn = s.Conn, ToTarget = s.ToTarget, Desired = s.Desired,
                    Target = aim, Why = "hop", HopVel = f.BestVel, HopChargeTicks = hopCharge,
                    HopPower = f.BestPower, HopFlightTicks = f.BestTicks, HopStartTile = hopStart,
                    HopLandingDir = landingDir,
                },
                FoxMoveBid.Yes("hop", null));
        }

        internal static int ChargeTicksFor(FoxBreedParams p, float power)
        {
            if (power > p.HopPowerLarge) return Math.Max(0, p.HopChargeTicksLarge);
            if (power > p.HopPowerMedium) return Math.Max(0, p.HopChargeTicksMedium);
            return Math.Max(0, p.HopChargeTicksSmall);
        }

        protected override int CooldownOnEnd(FoxBreedParams p, string reason, bool handover) { return 0; }

        internal void NoteEnded(FoxBreedParams p, string reason, string stage, bool handover, bool touched,
                                string outcome, Vector2 touchPos, int touchFrames, float aimErr,
                                string launchRefusal, int ran)
        {
            Cooldown = Math.Max(Cooldown, Math.Max(0, p.HopCooldownTicks));
            FoxHopPlanner planner = fox.AI != null ? fox.AI.HopPlanner : null;
            if (planner != null) planner.NoteEnded(touched);
            if (touched && !handover) fox.ClearPathLatchAfterHop();
        }

        protected override string Detail(FoxBreedParams p)
        {
            return null;
        }
    }

    public partial class Fox
    {
        internal bool HopBodyInCorridor()
        {
            if (BodyInCorridorTerrain()) return true;
            if (room == null || bodyChunks == null) return false;
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                if (TileIsCorridorLike(room.GetTilePosition(bodyChunks[i].pos))) return true;
            }
            return false;
        }

        internal void ClearPathLatchAfterHop()
        {
            if (latchedConnection != default(MovementConnection)) ClearLatch("hop_landed");
        }
    }
}
