using System;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal static class FoxPathTieBreak
    {
        private const int MAX_CONNECTIONS = 32;

        private const int FALL_RISK_TILES = 5;

        internal static void Install()
        {
            On.StandardPather.FollowPath += FollowPath;
        }

        [ThreadStatic] private static bool suspended;

        internal static MovementConnection FollowPathUnfiltered(StandardPather pather,
            WorldCoordinate originPos, bool actuallyFollowingThisPath)
        {
            if (pather == null) return default(MovementConnection);
            bool prev = suspended;
            suspended = true;
            try { return pather.FollowPath(originPos, actuallyFollowingThisPath); }
            finally { suspended = prev; }
        }

        private static MovementConnection FollowPath(On.StandardPather.orig_FollowPath orig,
            StandardPather self, WorldCoordinate originPos, bool actuallyFollowingThisPath)
        {
            MovementConnection chosen = orig(self, originPos, actuallyFollowingThisPath);

            if (suspended) return chosen;

            if (self == null || self.creature == null) return chosen;
            if (!FoxCritob.IsFoxType(self.creature.creatureTemplate.type)) return chosen;

            Fox fox = self.creature.realizedCreature as Fox;
            if (fox == null) return chosen;

            FoxBreedParams p = fox.P;
            if (!p.PathBodyTieBreak) return chosen;

            Room room = self.realizedRoom;
            if (room == null || room.aimap == null) return chosen;
            if (chosen == default(MovementConnection)) return chosen;
            if (!originPos.TileDefined) return chosen;

            bool following = actuallyFollowingThisPath;
            IntVector2 t = originPos.Tile;
            if (following && (fox.tieBreakStreakTile.x != t.x || fox.tieBreakStreakTile.y != t.y))
            {
                fox.tieBreakStreakTile = t;
                fox.tieBreakStreak = 0;
                fox.tieBreakCommitTicks = 0;
            }

            bool inScope = chosen.type == MovementConnection.MovementType.Standard
                           || chosen.type == MovementConnection.MovementType.LizardTurn;
            bool isTurn = chosen.type == MovementConnection.MovementType.LizardTurn;
            if (!inScope) return chosen;

            int commitMax = p.PathTiebreakCommitTicks;
            if (commitMax > 0 && fox.tieBreakCommitTicks > 0
                && t.x == fox.tieBreakStreakTile.x && t.y == fox.tieBreakStreakTile.y
                && !SameDestTile(chosen, fox.tieBreakCommitDest))
            {
                int streakNow = fox.tieBreakStreak;
                int maxStreak = p.PathTieBreakMaxStreak;
                if (maxStreak > 0 && streakNow >= maxStreak)
                {
                    if (following) fox.tieBreakCommitTicks = 0;
                }
                else
                {
                    bool goalHeld = self.GetDestination == fox.tieBreakCommitGoal;
                    MovementConnection held = default(MovementConnection);
                    if (goalHeld)
                    {
                        for (int i = 0; i < MAX_CONNECTIONS; i++)
                        {
                            MovementConnection c = self.ConnectionAtCoordinate(true, originPos, i);
                            if (c == default(MovementConnection)) break;
                            if (c.type != MovementConnection.MovementType.Standard) continue;
                            if (!c.destinationCoord.TileDefined) continue;
                            if (!SameDestTile(c, fox.tieBreakCommitDest)) continue;
                            held = c;
                            break;
                        }
                    }

                    bool allowed = held != default(MovementConnection)
                                   && StepCost(room, self.creature.creatureTemplate, fox, p, held, following).Allowed;

                    if (allowed)
                    {
                        if (following)
                        {
                            fox.tieBreakCommitTicks--;
                            fox.tieBreakStreak++;
                        }
                        return held;
                    }

                    if (following) fox.tieBreakCommitTicks = 0;
                }
            }

            AItile at = room.aimap.getAItile(t.x, t.y);
            if (at == null) return NoSwap(fox, following, chosen);
            if (at.acc != AItile.Accessibility.Floor) return NoSwap(fox, following, chosen);

            bool highAndOpen = !at.narrowSpace && at.fallRiskTile.y < originPos.y - FALL_RISK_TILES;
            if (highAndOpen) return NoSwap(fox, following, chosen);

            IntVector2 step = StepOf(chosen);
            if (step.x == 0 && !at.narrowSpace) return NoSwap(fox, following, chosen);

            IntVector2 bodyDir = BodyDirection(fox);
            if (bodyDir.x == 0 && bodyDir.y == 0) return NoSwap(fox, following, chosen);
            if (!Custom.IntVectorsOpposite(step, bodyDir)) return NoSwap(fox, following, chosen);

            CreatureTemplate tmpl = self.creature.creatureTemplate;
            PathFinder.PathingCell chosenCell = self.PathingCellAtWorldCoordinate(chosen.destinationCoord);
            if (chosenCell == null) return NoSwap(fox, following, chosen);

            MovementConnection priced = chosen;
            if (isTurn && p.PathTiebreakNormaliseTurn)
            {
                for (int i = 0; i < MAX_CONNECTIONS; i++)
                {
                    MovementConnection c = self.ConnectionAtCoordinate(true, originPos, i);
                    if (c == default(MovementConnection)) break;
                    if (c.type != MovementConnection.MovementType.Standard) continue;
                    if (!SameDestination(c, chosen)) continue;
                    priced = c;
                    break;
                }
            }

            PathCost chosenConn = StepCost(room, tmpl, fox, p, priced, following);
            PathCost chosenTotal = chosenCell.costToGoal + chosenConn;

            MovementConnection best = default(MovementConnection);
            PathCost bestTotal = default(PathCost);
            bool haveBest = false;

            for (int i = 0; i < MAX_CONNECTIONS; i++)
            {
                MovementConnection c = self.ConnectionAtCoordinate(true, originPos, i);
                if (c == default(MovementConnection)) break;
                if (c.type != MovementConnection.MovementType.Standard) continue;
                if (!c.destinationCoord.TileDefined) continue;
                if (Custom.IntVectorsOpposite(StepOf(c), bodyDir)) continue;

                PathCost connCost = StepCost(room, tmpl, fox, p, c, following);
                if (!connCost.Allowed) continue;

                PathFinder.PathingCell cell = self.PathingCellAtWorldCoordinate(c.destinationCoord);
                if (cell == null || !cell.reachable) continue;

                PathCost total = cell.costToGoal + connCost;
                if (total > chosenTotal) continue;
                if (haveBest && !(total < bestTotal)) continue;

                best = c;
                bestTotal = total;
                haveBest = true;
            }

            if (!haveBest) return NoSwap(fox, following, chosen);

            int max = p.PathTieBreakMaxStreak;
            if (max > 0 && fox.tieBreakStreak >= max)
            {
                if (following) fox.tieBreakCommitTicks = 0;
                return chosen;
            }

            if (following) fox.tieBreakStreak++;

            if (following && p.PathTiebreakCommitTicks > 0)
            {
                fox.tieBreakCommitDest = new IntVector2(best.destinationCoord.x, best.destinationCoord.y);
                fox.tieBreakCommitGoal = self.GetDestination;
                fox.tieBreakCommitTicks = p.PathTiebreakCommitTicks;
            }

            return best;
        }

        private static bool SameDestTile(MovementConnection c, IntVector2 dest)
        {
            return c.destinationCoord.TileDefined
                   && c.destinationCoord.x == dest.x
                   && c.destinationCoord.y == dest.y;
        }

        private static MovementConnection NoSwap(Fox fox, bool following, MovementConnection chosen)
        {
            if (following) fox.tieBreakStreak = 0;
            return chosen;
        }

        private static PathCost StepCost(Room room, CreatureTemplate tmpl, Fox fox, FoxBreedParams p,
                                         MovementConnection c, bool following)
        {
            PathCost engine = room.aimap.ConnectionCostForCreature(c, tmpl);
            if (!p.PathTiebreakTravelPreference) return engine;
            FoxAI ai = fox.AI;
            if (ai == null) return engine;
            PathCost folded = ai.TravelPreferenceForQuery(c, engine);
            return folded;
        }

        private static bool SameDestination(MovementConnection a, MovementConnection b)
        {
            return a.destinationCoord.room == b.destinationCoord.room
                   && a.destinationCoord.x == b.destinationCoord.x
                   && a.destinationCoord.y == b.destinationCoord.y;
        }

        private static IntVector2 StepOf(MovementConnection c)
        {
            return IntVector2.ClampAtOne(new IntVector2(
                c.destinationCoord.x - c.startCoord.x,
                c.destinationCoord.y - c.startCoord.y));
        }

        private static IntVector2 BodyDirection(Fox fox)
        {
            if (fox.bodyChunks == null || fox.bodyChunks.Length < 3) return new IntVector2(0, 0);
            Vector2 axis = fox.bodyChunks[0].pos - fox.bodyChunks[2].pos;
            if (axis.sqrMagnitude < 0.0001f) return new IntVector2(0, 0);
            if (Mathf.Abs(axis.x) >= Mathf.Abs(axis.y))
            {
                return new IntVector2(axis.x >= 0f ? 1 : -1, 0);
            }
            return new IntVector2(0, axis.y >= 0f ? 1 : -1);
        }
    }
}
