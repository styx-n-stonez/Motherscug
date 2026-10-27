using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal sealed class FoxPounceRouter
    {
        private const float TRIAL_MIN_FRAC = 0.25f;
        private const float TRIAL_MIN_DEG = -10f;
        private const float TRIAL_MAX_DEG = 80f;
        private const int TRACE_CACHE = 64;

        private static PathCost.Legality Ceiling(FoxBreedParams p)
        {
            if (string.Equals(p.PounceRouteMaxLegality, "allowed", System.StringComparison.OrdinalIgnoreCase))
                return PathCost.Legality.Allowed;
            return PathCost.Legality.Unwanted;
        }

        private readonly FoxAI ai;
        private readonly Fox fox;
        private readonly List<WorldCoordinate> futurePath = new List<WorldCoordinate>(32);
        private readonly List<ArcFinder> finders = new List<ArcFinder>();

        private FoxAscentPlan plan;

        private bool unavailableLogged;

        private bool disabledLogged;

        private bool clamberDisabledLogged;

        internal FoxPounceRouter(FoxAI ai, Fox fox)
        {
            this.ai = ai;
            this.fox = fox;
        }

        internal FoxAscentPlan Plan { get { return plan; } }

        internal int PathGeneration
        {
            get
            {
                PathFinder pf = ai.pathFinder;
                return pf != null ? pf.pathGeneration : -1;
            }
        }

        internal void Update(FoxBreedParams p)
        {
            Room room = fox.RoomRef;
            if (room == null || room.aimap == null) { Clear("no_room"); return; }

            StandardPather pather = ai.pathFinder as StandardPather;
            if (pather == null)
            {
                if (!unavailableLogged)
                {
                    unavailableLogged = true;
                }
                Clear("no_pather");
                return;
            }

            bool leapRouting = true;
            if (!p.PounceRouteEnabled)
            {
                leapRouting = false;
                if (!disabledLogged)
                {
                    disabledLogged = true;
                }
            }
            else
            {
                disabledLogged = false;
            }

            if (leapRouting && !p.PounceRouteWhilePatrolling
                && ai.CurrentBehavior != FoxAI.Behavior.Hunt
                && ai.CurrentBehavior != FoxAI.Behavior.Stalk)
            {
                leapRouting = false;
            }

            if (!leapRouting) ClearLeapSearch(p.PounceRouteEnabled ? "not_hunting" : "disabled");

            int now = fox.MoveTick;

            WorldCoordinate here = fox.abstractCreature.pos;
            PathFinder.PathingCell hereCell = pather.PathingCellAtWorldCoordinate(here);

            MaintainPlan(p, pather, room, here, hereCell, now);

            if (leapRouting)
            {
                PeekAhead(p, pather, here);
                TickFinders(p, pather, room, hereCell);
                SeedFinder(p, pather, room, here, hereCell);
                Commit(p, pather, room, here, hereCell, now);
            }

            WorldCoordinate walkDest;
            bool walkReachable;
            walkedRoute = WalkedRouteStatusOf(p, pather, here, futurePath.Count,
                                              out walkDest, out walkReachable);

            ClamberUpdate(p, pather, room, here, hereCell, now);
        }

        private void PeekAhead(FoxBreedParams p, StandardPather pather, WorldCoordinate here)
        {
            futurePath.Clear();
            WorldCoordinate walk = here;
            int steps = Math.Max(1, p.PounceRoutePeekSteps);
            for (int i = 0; i < steps; i++)
            {
                MovementConnection step = pather.FollowPath(walk, false);
                if (step == default(MovementConnection)) break;
                walk = step.destinationCoord;
                futurePath.Add(walk);

                for (int j = 0; j < finders.Count; j++)
                {
                    if (finders[j].StartTile == walk.Tile) finders[j].Fade = 0;
                }
            }
        }

        private void TickFinders(FoxBreedParams p, StandardPather pather, Room room,
                                 PathFinder.PathingCell hereCell)
        {
            if (finders.Count == 0) return;

            int iters = Math.Max(1, Math.Max(1, p.PounceRouteItersPerTick) / finders.Count);

            bool destReachable = pather.CoordinateReachable(pather.GetDestination);

            for (int i = finders.Count - 1; i >= 0; i--)
            {
                ArcFinder f = finders[i];
                f.Fade++;
                bool stillWorthIt = f.GoalDirected
                    ? f.ClosesOnGoal(pather, fox.abstractCreature.pos.Tile, p.PounceRouteGoalSeedMinGainTiles)
                    : PathWeightComparison(hereCell, f.StartCell, Ceiling(p));
                if (!stillWorthIt) f.Fade += 10;
                if (f.Fade > 40)
                {
                    finders.RemoveAt(i);
                    continue;
                }
                for (int n = 0; n < iters; n++) f.Iterate(p, pather, room, destReachable);
            }
        }

        private void SeedFinder(FoxBreedParams p, StandardPather pather, Room room,
                                WorldCoordinate here, PathFinder.PathingCell hereCell)
        {
            if (finders.Count >= Math.Max(1, p.PounceRouteMaxFinders)) return;

            if (SeedFromWalkedPath(p, pather, room, here, hereCell)) return;
            SeedTowardGoal(p, pather, room, here, hereCell);
        }

        private bool SeedFromWalkedPath(FoxBreedParams p, StandardPather pather, Room room,
                                        WorldCoordinate here, PathFinder.PathingCell hereCell)
        {
            if (futurePath.Count < 2)
            {
                return false;
            }

            int attempts = 0, narrowHits = 0;
            WorldCoordinate pick = default(WorldCoordinate);
            PathFinder.PathingCell cell = null;
            bool have = false;
            int lastNarrowX = -1, lastNarrowY = -1;

            for (int a = 0; a < Math.Max(1, p.PounceRouteSeedAttemptsPerTick) && !have; a++)
            {
                attempts++;
                WorldCoordinate cand = futurePath[UnityEngine.Random.Range(0, futurePath.Count)];
                if (!cand.TileDefined || cand.room != here.room) continue;
                if (cand.Tile.FloatDist(here.Tile) <= 2f) continue;
                bool dup = false;
                for (int i = 0; i < finders.Count; i++)
                {
                    if (finders[i].StartTile == cand.Tile) { dup = true; break; }
                }
                if (dup) continue;

                if (p.PounceRouteBlockNarrow && room.aimap.getAItile(cand.x, cand.y).narrowSpace)
                {
                    narrowHits++;
                    lastNarrowX = cand.x; lastNarrowY = cand.y;
                    continue;
                }

                pick = cand;
                cell = pather.PathingCellAtWorldCoordinate(cand);
                have = true;
            }

            if (!have)
            {
                return false;
            }

            string seedWhy;
            if (!Compare(hereCell, cell, Ceiling(p), out seedWhy))
            {
                return false;
            }

            var f = new ArcFinder(fox, room, pick.Tile, cell, false);
            f.NewTrial(fox.P, room);
            finders.Add(f);
            return true;
        }

        private string GoalSeedMode(FoxBreedParams p, StandardPather pather, WorldCoordinate here,
                                    out WorldCoordinate dest, out bool destReachable)
        {
            string status = WalkedRouteStatusOf(p, pather, here, futurePath.Count,
                                                out dest, out destReachable);
            if (!p.PounceRouteGoalSeedEnabled) return "off";
            return status;
        }

        private string WalkedRouteStatusOf(FoxBreedParams p, StandardPather pather, WorldCoordinate here,
                                           int peekCount, out WorldCoordinate dest, out bool destReachable)
        {
            dest = pather.GetDestination;
            destReachable = pather.CoordinateReachable(dest);
            if (!pather.DoneMappingAccessibility) return "mapping";
            if (!dest.TileDefined || dest.room != here.room) return "other_room";
            if (dest.Tile == here.Tile) return "at_dest";
            bool pathShort = peekCount < p.PounceRouteGoalSeedPathFloor;
            if (!pathShort && destReachable && !pather.lookingForImpossiblePath) return "walked_path_ok";
            return "on";
        }

        internal string WalkedRoute { get { return walkedRoute; } }
        private string walkedRoute = "none";

        private void SeedTowardGoal(FoxBreedParams p, StandardPather pather, Room room,
                                    WorldCoordinate here, PathFinder.PathingCell hereCell)
        {
            WorldCoordinate dest;
            bool destReachable;
            string mode = GoalSeedMode(p, pather, here, out dest, out destReachable);
            if (mode != "on")
            {
                return;
            }

            IntVector2 me = here.Tile, goal = dest.Tile;
            Vector2 toGoal = new Vector2(goal.x - me.x, goal.y - me.y);
            if (toGoal.sqrMagnitude < 0.0001f)
            {
                return;
            }
            float distMe = me.FloatDist(goal);
            Vector2 dir = toGoal.normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            IntVector2 pick = default(IntVector2);
            PathFinder.PathingCell cell = null;
            bool have = false;

            for (int a = 0; a < Math.Max(1, p.PounceRouteSeedAttemptsPerTick) && !have; a++)
            {
                float along = Mathf.Lerp(2f, p.PounceRouteGoalSeedRadiusTiles, UnityEngine.Random.value);
                float lat = (UnityEngine.Random.value * 2f - 1f) * p.PounceRouteGoalSeedLateralTiles;
                Vector2 off = dir * along + perp * lat;
                IntVector2 cand = new IntVector2(me.x + Mathf.RoundToInt(off.x),
                                                 me.y + Mathf.RoundToInt(off.y));

                if (cand.x < 0 || cand.y < 0 || cand.x >= room.TileWidth || cand.y >= room.TileHeight)
                {
                    continue;
                }
                if (cand.FloatDist(me) <= 2f || cand.FloatDist(me) > p.PounceRouteGoalSeedRadiusTiles)
                {
                    continue;
                }
                if (cand.FloatDist(goal) > distMe - p.PounceRouteGoalSeedMinGainTiles)
                {
                    continue;
                }

                int snapRows = Math.Max(0, p.PounceRouteGoalSeedSnapRows);
                bool standable = room.aimap.getAItile(cand.x, cand.y).walkable
                                 && room.aimap.TileAccessibleToCreature(cand.x, cand.y, fox.Template)
                                 && !fox.ClimbTileBarsLeap(cand.x, cand.y);
                for (int d = 1; !standable && d <= snapRows; d++)
                {
                    for (int sgn = -1; sgn <= 1 && !standable; sgn += 2)
                    {
                        int ty = cand.y + d * sgn;
                        if (ty < 0 || ty >= room.TileHeight) continue;
                        if (room.aimap.getAItile(cand.x, ty).walkable
                            && room.aimap.TileAccessibleToCreature(cand.x, ty, fox.Template)
                            && !fox.ClimbTileBarsLeap(cand.x, ty))
                        {
                            cand = new IntVector2(cand.x, ty);
                            standable = true;
                        }
                    }
                }
                if (!standable)
                {
                    continue;
                }

                WorldCoordinate candCoord = room.GetWorldCoordinate(cand);

                if (!pather.CoordinateReachable(candCoord))
                {
                    continue;
                }

                bool dup = false;
                for (int i = 0; i < finders.Count; i++)
                {
                    if (finders[i].StartTile == cand) { dup = true; break; }
                }
                if (dup) { continue; }

                if (p.PounceRouteBlockNarrow && room.aimap.getAItile(cand.x, cand.y).narrowSpace)
                {
                    continue;
                }

                PathFinder.PathingCell candCell = pather.PathingCellAtWorldCoordinate(candCoord);
                string why;
                if (!GoalSeedProgress(p, pather, hereCell, candCell, destReachable, out why))
                {
                    continue;
                }

                pick = cand; cell = candCell; have = true;
            }

            if (!have)
            {
                return;
            }

            var f = new ArcFinder(fox, room, pick, cell, true);
            f.NewTrial(fox.P, room);
            finders.Add(f);
        }

        private bool GoalSeedProgress(FoxBreedParams p, StandardPather pather,
                                      PathFinder.PathingCell hereCell, PathFinder.PathingCell cell,
                                      bool destReachable, out string why)
        {
            string cw;
            if (Compare(hereCell, cell, Ceiling(p), out cw)) { why = "cost_" + cw; return true; }

            if (destReachable) { why = "field_says_" + cw; return false; }

            bool hereUninformative = hereCell == null
                                     || hereCell.generation != pather.pathGeneration
                                     || hereCell.costToGoal.legality > Ceiling(p);
            if (!hereUninformative) { why = "field_says_" + cw; return false; }

            why = "geometric_gain";
            return true;
        }

        private void Commit(FoxBreedParams p, StandardPather pather, Room room,
                            WorldCoordinate here, PathFinder.PathingCell hereCell, int now)
        {
            if (plan != null) return;

            bool destReachable = pather.CoordinateReachable(pather.GetDestination);

            for (int i = 0; i < finders.Count; i++)
            {
                ArcFinder f = finders[i];
                string why;
                if (!f.Beneficial(p, pather, hereCell, destReachable, out why))
                {
                    continue;
                }

                PathFinder.PathingCell land = f.BestCell;

                if (hereCell == null || land.generation != pather.pathGeneration)
                {
                    continue;
                }

                bool hereFilled = hereCell.generation == pather.pathGeneration;
                bool opening = f.GoalDirected && !destReachable
                               && land.costToGoal.legality <= Ceiling(p);
                if (!hereFilled && !opening)
                {
                    continue;
                }

                bool hereScored = hereFilled && hereCell.costToGoal.legality <= Ceiling(p);
                bool opened = opening && !hereScored;

                var made = new FoxAscentPlan
                {
                    Kind = FoxAscentKind.Pounce,
                    RoutePriced = true,
                    Target = land.worldCoordinate,
                    LaunchFrom = new WorldCoordinate(here.room, f.StartTile.x, f.StartTile.y, -1),
                    AimAt = room.MiddleOfTile(land.worldCoordinate.x, land.worldCoordinate.y),
                    LaunchVel = f.BestVel,
                    Generation = land.generation,
                    ExpiresAtTick = now + Math.Max(1, p.PounceRouteReplanTicks),
                    CostSaved = hereScored
                                ? hereCell.costToGoal.resistance - land.costToGoal.resistance
                                : 0f,
                    Why = opened ? "route_opened" : (f.GoalDirected ? "route_cheaper_goal" : "route_cheaper"),
                    PounceRuledOutBy = string.Empty,
                };
                plan = made;

                return;
            }
        }

        private void MaintainPlan(FoxBreedParams p, StandardPather pather, Room room,
                                  WorldCoordinate here, PathFinder.PathingCell hereCell, int now)
        {
            if (plan == null) return;

            if (plan.Kind == FoxAscentKind.None) { ExpirePlan(p, pather, now, "no_kind"); return; }
            if (now >= plan.ExpiresAtTick) { ExpirePlan(p, pather, now, "replan_ticks"); return; }

            if (plan.Generation == pather.pathGeneration) { plan.HeldTicks = 0; return; }

            if (!plan.RoutePriced)
            {
                plan.Generation = pather.pathGeneration;
                plan.Revalidations++;
                plan.HeldTicks = 0;
                return;
            }

            if (plan.Revalidations >= p.PounceRouteRevalidationsMax) { ExpirePlan(p, pather, now, "revalidate_cap"); return; }

            string why;
            if (Revalidate(p, pather, room, here, hereCell, out why))
            {
                plan.Generation = pather.pathGeneration;
                plan.Revalidations++;
                plan.HeldTicks = 0;
                return;
            }

            if (why == "cells_not_filled_yet")
            {
                plan.HeldTicks++;
                if (plan.HeldTicks > p.PounceRouteHoldMaxTicks) { ExpirePlan(p, pather, now, "hold_timeout"); return; }
                return;
            }

            ExpirePlan(p, pather, now, why);
        }

        private bool Revalidate(FoxBreedParams p, StandardPather pather, Room room,
                                WorldCoordinate here, PathFinder.PathingCell hereCell, out string why)
        {
            int gen = pather.pathGeneration;

            if (hereCell == null) { why = "no_cell_here"; return false; }
            if (plan.Target.room != here.room) { why = "landing_other_room"; return false; }

            PathFinder.PathingCell land = pather.PathingCellAtWorldCoordinate(plan.Target);
            if (land == null) { why = "no_cell_landing"; return false; }

            if (land.generation != gen) { why = "cells_not_filled_yet"; return false; }

            bool opened = plan.Kind == FoxAscentKind.Pounce && plan.Why == "route_opened";
            bool destReachable = pather.CoordinateReachable(pather.GetDestination);
            bool hereFilled = hereCell.generation == gen;
            if (!hereFilled && !(opened && !destReachable))
            {
                why = "cells_not_filled_yet";
                return false;
            }

            if (land.costToGoal.legality > Ceiling(p)) { why = "landing_illegal_now"; return false; }

            IntVector2 t = plan.Target.Tile;
            if (room.aimap == null) { why = "no_aimap"; return false; }
            if (!room.aimap.TileAccessibleToCreature(t.x, t.y, fox.Template))
            {
                why = "landing_not_accessible"; return false;
            }
            if (fox.ClimbTileBarsLeap(t.x, t.y))
            {
                why = fox.PoleTileBarsLeap(t.x, t.y) ? "landing_is_pole" : "landing_is_wall"; return false;
            }
            if (p.PounceRouteBlockNarrow && room.aimap.getAItile(t.x, t.y).narrowSpace)
            {
                why = "landing_narrow"; return false;
            }

            if (plan.Kind == FoxAscentKind.Mantle)
            {
                float penalty = Mathf.Max(0f, p.MantleRoutePenalty);
                float hereRes = hereCell.costToGoal.legality <= Ceiling(p)
                                ? hereCell.costToGoal.resistance : float.MaxValue;
                float landRes = land.costToGoal.resistance;
                if (!(landRes + penalty < hereRes)) { why = "not_cheaper_with_penalty"; return false; }
                plan.CostSaved = Mathf.Min(9999f, hereRes - landRes - penalty);
                plan.Generation = land.generation;
                why = "clamber_still_cheaper";
                return true;
            }

            bool hereScored = hereFilled && hereCell.costToGoal.legality <= Ceiling(p);
            if (opened && !hereScored)
            {
                plan.CostSaved = 0f;
                plan.Generation = land.generation;
                why = "route_still_open";
                return true;
            }

            if (PathWeightComparison(land, hereCell, Ceiling(p))) { why = "worse_than_standing_here"; return false; }

            PathFinder.PathingCell launch = pather.PathingCellAtWorldCoordinate(plan.LaunchFrom);
            if (launch != null && launch.generation != gen) { why = "cells_not_filled_yet"; return false; }
            if (launch != null && PathWeightComparison(land, launch, Ceiling(p)))
            {
                why = "worse_than_launch_tile"; return false;
            }

            plan.CostSaved = hereCell.costToGoal.resistance - land.costToGoal.resistance;
            plan.Generation = land.generation;
            why = "leap_still_beneficial";
            return true;
        }

        private void ExpirePlan(FoxBreedParams p, StandardPather pather, int now, string reason)
        {
            plan = null;
        }

        private void Clear(string reason)
        {
            if (plan != null)
            {
                plan = null;
            }
            if (finders.Count > 0) finders.Clear();
            futurePath.Clear();
            walkedRoute = "none";
        }

        private void ClearLeapSearch(string reason)
        {
            if (plan != null && plan.Kind == FoxAscentKind.Pounce)
            {
                plan = null;
            }
            if (finders.Count > 0) finders.Clear();
            futurePath.Clear();
        }

        internal void NewRoom()
        {
            Clear("new_room");
        }

        internal FoxAscentPlan RequestAscent(FoxBreedParams p, Vector2 lipAt, float runUpPx)
        {
            Room room = fox.RoomRef;
            if (room == null) return null;

            int now = fox.MoveTick;
            IntVector2 lipTile = room.GetTilePosition(lipAt);
            WorldCoordinate lipCoord = new WorldCoordinate(room.abstractRoom.index, lipTile.x, lipTile.y, -1);

            bool haveRunUp = runUpPx >= p.LedgePounceStandoffPx;

            int headroom = 0;
            int want = Math.Max(1, p.LedgePounceHeadroomTiles);
            while (headroom < want && !room.GetTile(lipTile.x, lipTile.y + 1 + headroom).Solid) headroom++;
            bool haveHeadroom = headroom >= want;

            string ruledOut = null;
            if (!haveRunUp) ruledOut = "no_run_up";
            else if (!haveHeadroom) ruledOut = "headroom_low";

            var made = new FoxAscentPlan
            {
                Kind = ruledOut == null ? FoxAscentKind.Pounce : FoxAscentKind.Mantle,
                RoutePriced = false,
                Target = lipCoord,
                LaunchFrom = fox.abstractCreature.pos,
                AimAt = lipAt,
                LaunchVel = Vector2.zero,
                Generation = PathGeneration,
                ExpiresAtTick = now + Math.Max(1, p.PounceRouteReplanTicks),
                CostSaved = 0f,
                Why = ruledOut == null ? "lip_ascent" : "pounce_impossible",
                PounceRuledOutBy = ruledOut ?? string.Empty,
            };
            plan = made;

            return made;
        }

        private void ClamberUpdate(FoxBreedParams p, StandardPather pather, Room room,
                                   WorldCoordinate here, PathFinder.PathingCell hereCell, int now)
        {
            if (!p.MantleRouteEnabled)
            {
                if (!clamberDisabledLogged)
                {
                    clamberDisabledLogged = true;
                }
                return;
            }
            clamberDisabledLogged = false;

            string skip = ClamberSkipReason(p);
            if (skip != null)
            {
                return;
            }

            BodyChunk[] c = fox.Chunks;

            float footY = float.MaxValue;
            for (int i = 0; i < c.Length; i++) footY = Mathf.Min(footY, c[i].pos.y - c[i].rad);
            IntVector2 footTile = room.GetTilePosition(new Vector2(c[0].pos.x, footY + 1f));

            int scan = Math.Max(1, p.MantleRouteScanTiles);
            float maxRise = Mathf.Min(p.MantleRouteMaxRise, p.MantleMaxRise);
            int band = Math.Max(1, Mathf.CeilToInt(Mathf.Max(20f, maxRise) / 20f));

            float faceX = Fox.Dir(c[2].pos, c[0].pos).x;
            int first = faceX >= 0f ? 1 : -1;
            bool committed = TryClamber(p, pather, room, hereCell, footTile, footY, first, scan, band, maxRise, now);
            if (!committed)
            {
                committed = TryClamber(p, pather, room, hereCell, footTile, footY, -first, scan, band, maxRise, now);
            }
        }

        private string ClamberSkipReason(FoxBreedParams p)
        {
            if (plan != null) return "plan_held";
            if (!p.MantleEnabled) return "mantle_disabled";
            if (fox.Chunks == null || fox.Chunks.Length < 3) return "no_body";
            if (fox.dead || !fox.Consious) return "not_acting";
            if (!fox.Grounded) return "airborne";
            if (fox.pounce != Fox.PounceState.None) return "pouncing";
            if (fox.TurnActive) return "turning";
            if (fox.Move == null || fox.Move.Mantle == null || fox.Move.Mantle.Running) return "clamber_running";
            return null;
        }

        private bool TryClamber(FoxBreedParams p, StandardPather pather, Room room,
                                PathFinder.PathingCell hereCell, IntVector2 footTile, float footY,
                                int sign, int scan, int band, float maxRise, int now)
        {
            int faceRow = int.MinValue;
            int col = footTile.x + sign;
            for (int d = 1; d <= scan && faceRow == int.MinValue; d++)
            {
                col = footTile.x + sign * d;
                for (int r = footTile.y; r <= footTile.y + band; r++)
                {
                    if (room.GetTile(col, r).Solid) { faceRow = r; break; }
                }
            }
            if (faceRow == int.MinValue) { return false; }

            int lipRow = faceRow, up = 0;
            while (up <= band && room.GetTile(col, lipRow + 1).Solid) { lipRow++; up++; }
            if (room.GetTile(col, lipRow + 1).Solid)
            {
                return false;
            }

            int landRow = lipRow + 1;
            float topY = room.MiddleOfTile(col, lipRow).y + 10f;
            float rise = topY - footY;

            if (rise < p.MantleMinRise)
            {
                return false;
            }
            if (rise > maxRise)
            {
                return false;
            }

            if (!room.aimap.TileAccessibleToCreature(col, landRow, fox.Template)
                || fox.ClimbTileBarsLeap(col, landRow))
            {
                return false;
            }
            if (p.PounceRouteBlockNarrow && room.aimap.getAItile(col, landRow).narrowSpace)
            {
                return false;
            }
            int want = Math.Max(1, p.MantleHeadroomTiles);
            int headroom = 0;
            while (headroom < want && !room.GetTile(col, landRow + headroom).Solid) headroom++;
            if (headroom < want)
            {
                return false;
            }

            float runUpPx;
            int lipHeadroom;
            string ruledOut = RuleOutLeapAtLip(p, room, col, lipRow, sign, rise, footTile.y,
                                               out runUpPx, out lipHeadroom);
            if (ruledOut == null)
            {
                return false;
            }

            WorldCoordinate landCoord = room.GetWorldCoordinate(new IntVector2(col, landRow));
            PathFinder.PathingCell landCell = pather.PathingCellAtWorldCoordinate(landCoord);
            if (landCell == null || hereCell == null)
            {
                return false;
            }

            if (landCell.generation != pather.pathGeneration || hereCell.generation != pather.pathGeneration)
            {
                return false;
            }
            if (landCell.costToGoal.legality > Ceiling(p))
            {
                return false;
            }

            float penalty = Mathf.Max(0f, p.MantleRoutePenalty);
            float hereRes = hereCell.costToGoal.legality <= Ceiling(p)
                            ? hereCell.costToGoal.resistance : float.MaxValue;
            float landRes = landCell.costToGoal.resistance;
            if (!(landRes + penalty < hereRes))
            {
                return false;
            }

            var made = new FoxAscentPlan
            {
                Kind = FoxAscentKind.Mantle,
                RoutePriced = true,
                Target = landCoord,
                LaunchFrom = new WorldCoordinate(room.abstractRoom.index, col - sign, footTile.y, -1),
                AimAt = new Vector2(room.MiddleOfTile(col, lipRow).x - sign * 10f, topY),
                LaunchVel = Vector2.zero,
                Generation = landCell.generation,
                ExpiresAtTick = now + Math.Max(1, p.MantleRouteReplanTicks),
                CostSaved = Mathf.Min(9999f, hereRes - landRes - penalty),
                Why = "clamber_cheaper",
                PounceRuledOutBy = ruledOut,
            };
            plan = made;

            return true;
        }

        private string RuleOutLeapAtLip(FoxBreedParams p, Room room, int col, int lipRow, int sign,
                                        float rise, int footRow, out float runUpPx, out int lipHeadroom)
        {
            runUpPx = 0f;
            lipHeadroom = 0;

            if (!p.LedgePounceEnabled || !p.PounceEnabled) return "leap_disabled";
            string band = FoxLedgePounceAction.RefuseRiseBand(p, rise);
            if (band != null) return band;

            int want = Math.Max(1, p.LedgePounceHeadroomTiles);
            while (lipHeadroom < want && !room.GetTile(col, lipRow + 1 + lipHeadroom).Solid) lipHeadroom++;
            if (lipHeadroom < want) return "headroom_low";

            int maxTiles = Mathf.CeilToInt(Mathf.Max(0f, p.LedgePounceStandoffPx) / 20f) + 1;
            int clear = 0;
            for (int k = 1; k <= maxTiles; k++)
            {
                int cx = col - sign * k;
                if (room.GetTile(cx, footRow).Solid) break;
                if (!room.aimap.getAItile(cx, footRow).walkable) break;
                bool roofed = false;
                for (int u = 1; u <= want; u++)
                {
                    if (room.GetTile(cx, footRow + u).Solid) { roofed = true; break; }
                }
                if (roofed) break;
                clear++;
            }
            runUpPx = clear * 20f;
            if (runUpPx < p.LedgePounceStandoffPx) return "no_run_up";

            return null;
        }

        internal static bool PathWeightComparison(PathFinder.PathingCell A, PathFinder.PathingCell B,
                                                  PathCost.Legality ceiling)
        {
            string why;
            return Compare(A, B, ceiling, out why);
        }

        internal static bool Compare(PathFinder.PathingCell A, PathFinder.PathingCell B,
                                     PathCost.Legality ceiling, out string why)
        {
            if (A == null) { why = B != null ? "no_reference_cell" : "no_cell"; return B != null; }
            if (B == null) { why = "no_cell"; return false; }
            if (B.costToGoal.legality > ceiling) { why = "illegal"; return false; }
            if (B.generation != A.generation)
            {
                bool newer = B.generation > A.generation;
                why = newer ? "newer_generation" : "stale_gen";
                return newer;
            }
            if (B.costToGoal.legality != A.costToGoal.legality)
            {
                bool better = B.costToGoal.legality < A.costToGoal.legality;
                why = better ? "better_legality" : "worse_legality";
                return better;
            }
            bool cheaper = B.costToGoal.resistance < A.costToGoal.resistance;
            why = cheaper ? "cheaper" : "not_cheaper";
            return cheaper;
        }

        private sealed class ArcFinder
        {
            private readonly Fox fox;
            private IntVector2[] trace = new IntVector2[TRACE_CACHE];

            internal readonly IntVector2 StartTile;
            internal readonly PathFinder.PathingCell StartCell;

            internal readonly bool GoalDirected;

            internal int Fade;

            internal PathFinder.PathingCell BestCell;
            internal Vector2 BestVel;

            private Vector2 pos, lastPos, vel, trialVel;
            private int trialTick, boostLeft;
            private bool venturedAwayFromTerrain;

            internal ArcFinder(Fox fox, Room room, IntVector2 startTile, PathFinder.PathingCell startCell,
                               bool goalDirected)
            {
                this.fox = fox;
                StartTile = startTile;
                StartCell = startCell;
                GoalDirected = goalDirected;
                pos = room.MiddleOfTile(startTile.x, startTile.y);
                lastPos = pos;
            }

            internal bool IsOpeningRoute(bool destReachable)
            {
                return GoalDirected && !destReachable;
            }

            internal bool ClosesOnGoal(StandardPather pather, IntVector2 foxTile, float minGain)
            {
                WorldCoordinate dest = pather.GetDestination;
                if (!dest.TileDefined) return true;
                return StartTile.FloatDist(dest.Tile) <= foxTile.FloatDist(dest.Tile) - minGain;
            }

            internal void NewTrial(FoxBreedParams p, Room room)
            {
                float frac = Mathf.Lerp(TRIAL_MIN_FRAC, 1f, Mathf.Sqrt(UnityEngine.Random.value));
                float speed = fox.EffectiveLaunchSpeed(p) * frac;
                float deg = Mathf.Lerp(TRIAL_MIN_DEG, TRIAL_MAX_DEG, UnityEngine.Random.value);
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                float rad = deg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad) * side, Mathf.Sin(rad));

                trialVel = dir * speed;
                trialVel.y += speed * p.PounceLaunchLift;

                pos = room.MiddleOfTile(StartTile.x, StartTile.y);
                lastPos = pos;
                vel = trialVel;
                boostLeft = Math.Max(0, p.PounceBoostFrames);
                trialTick = 0;
                venturedAwayFromTerrain = false;
            }

            internal void Iterate(FoxBreedParams p, StandardPather pather, Room room, bool destReachable)
            {
                lastPos = pos;
                bool opening = IsOpeningRoute(destReachable);

                if (boostLeft > 0 && p.PounceBoostFrames > 0)
                {
                    vel.y += p.PounceBoost * ((float)boostLeft / p.PounceBoostFrames);
                    boostLeft--;
                }
                vel.y -= fox.gravity;
                vel *= fox.airFriction;
                pos += vel;

                int n;
                for (n = SharedPhysics.RayTracedTilesArray(lastPos, pos, trace);
                     n >= trace.Length;
                     n = SharedPhysics.RayTracedTilesArray(lastPos, pos, trace))
                {
                    Array.Resize(ref trace, trace.Length + TRACE_CACHE);
                }

                for (int i = 0; i < n; i++)
                {
                    int tx = trace[i].x, ty = trace[i].y;

                    if (ty < 0 || room.GetTile(tx, ty).Solid || ty < room.defaultWaterLevel)
                    {
                        NewTrial(p, room);
                        return;
                    }
                    if (p.PounceRouteBlockNarrow && room.aimap.getAItile(tx, ty).narrowSpace)
                    {
                        NewTrial(p, room);
                        return;
                    }

                    if (!venturedAwayFromTerrain
                        && room.aimap.getTerrainProximity(tx, ty) > 1
                        && !room.GetTile(tx, ty).verticalBeam
                        && !room.GetTile(tx, ty).horizontalBeam)
                    {
                        venturedAwayFromTerrain = true;
                    }

                    if (!venturedAwayFromTerrain) continue;

                    bool nearTerrain = room.aimap.getTerrainProximity(tx, ty) == 1
                                       || room.GetTile(tx, ty).verticalBeam
                                       || room.GetTile(tx, ty).horizontalBeam;
                    if (!nearTerrain) continue;
                    if (!room.aimap.TileAccessibleToCreature(tx, ty, fox.Template)) continue;
                    if (fox.ClimbTileBarsLeap(tx, ty)) continue;

                    float minOrigin = opening ? p.PounceRouteGoalMinTilesOrigin : p.PounceRouteMinTilesOrigin;
                    if (StartTile.FloatDist(trace[i]) <= minOrigin) continue;
                    if (!opening && pather.GetDestination.Tile.FloatDist(trace[i]) <= p.PounceRouteMinTilesDest) continue;

                    PathFinder.PathingCell cell =
                        pather.PathingCellAtWorldCoordinate(room.GetWorldCoordinate(trace[i]));

                    if (cell == null || cell.costToGoal.legality > Ceiling(p)) continue;
                    if (PathWeightComparison(BestCell, cell, Ceiling(p)))
                    {
                        BestCell = cell;
                        BestVel = trialVel;
                    }
                }

                trialTick++;
                if (trialTick > p.PounceFlightMaxFrames) NewTrial(p, room);
            }

            internal bool Beneficial(FoxBreedParams p, StandardPather pather,
                                     PathFinder.PathingCell hereCell, bool destReachable, out string why)
            {
                bool opening = IsOpeningRoute(destReachable);
                if (BestCell == null) { why = "no_arc_found"; return false; }
                float minOrigin = opening ? p.PounceRouteGoalMinTilesOrigin : p.PounceRouteMinTilesOrigin;
                if (StartTile.FloatDist(BestCell.worldCoordinate.Tile) <= minOrigin)
                {
                    why = "too_short_from_origin"; return false;
                }
                if (!opening
                    && pather.GetDestination.Tile.FloatDist(BestCell.worldCoordinate.Tile) <= p.PounceRouteMinTilesDest)
                {
                    why = "too_close_to_dest"; return false;
                }
                if (PathWeightComparison(BestCell, hereCell, Ceiling(p))) { why = "worse_than_standing_here"; return false; }
                if (PathWeightComparison(BestCell, StartCell, Ceiling(p))) { why = "worse_than_launch_tile"; return false; }
                why = opening ? "beneficial_opening" : "beneficial";
                return true;
            }
        }
    }
}
