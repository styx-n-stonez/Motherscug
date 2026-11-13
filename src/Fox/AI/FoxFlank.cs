using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public sealed partial class FoxFlank
    {
        internal enum Role { None, Solo, Leader, Follower }
        private enum Phase { Idle, Circling, Done }

        private readonly FoxAI ai;
        private readonly Fox fox;

        private const float TilePx = 20f;

        internal AbstractCreature LeadTarget { get; private set; }
        internal int LeadSince { get; private set; }
        private int lastOwnSight;

        private int lastTickRan = int.MinValue;

        private int roleIndex, roleOf;
        private AbstractCreature knownLeaderTarget;

        private AbstractCreature huntTarget;
        private int huntSerial;

        private Phase phase = Phase.Idle;
        private int phaseHuntSerial = -1;
        private int circleStart;
        private int circleTick = int.MinValue;
        private WorldCoordinate goal;
        private bool goalValid;
        private string goalSide = "none";

        private readonly List<Fox> mates = new List<Fox>(8);
        private readonly List<Fox> followers = new List<Fox>(8);
        private Vector2[] spreadPos = new Vector2[8];
        private IntVector2[] spreadTile = new IntVector2[8];
        private int spreadCount;
        private bool spreadOn;
        private int spreadTick = int.MinValue / 2;
        private float spreadRadius, spreadEach, spreadTileEach;

        private float winMax;
        private int lastSpreadLogTick = int.MinValue / 2;

        private bool wasEnabled;
        private bool loggedDisabled;

        public FoxFlank(FoxAI ai)
        {
            this.ai = ai;
            this.fox = ai.fox;
        }

        private int Clock
        {
            get { return (fox.room != null && fox.room.game != null) ? fox.room.game.clock : 0; }
        }

        internal void Tick(FoxBreedParams p, FoxAI.Behavior behavior, Tracker.CreatureRepresentation prey)
        {
            int now = Clock;
            bool hadGap = lastTickRan != int.MinValue && now - lastTickRan > 1;
            int gap = lastTickRan == int.MinValue ? 0 : now - lastTickRan;
            lastTickRan = now;

            if (p == null || !p.FlankEnabled)
            {
                if (wasEnabled) DisableUnderLiveFox(now);
                if (!loggedDisabled)
                {
                    loggedDisabled = true;
                }
                return;
            }
            wasEnabled = true;
            loggedDisabled = false;

            bool hunting = (behavior == FoxAI.Behavior.Hunt || behavior == FoxAI.Behavior.Stalk)
                           && prey != null && prey.representedCreature != null;
            AbstractCreature target = hunting ? prey.representedCreature : null;
            if (!ReferenceEquals(target, huntTarget))
            {
                huntTarget = target;
                if (target != null) huntSerial++;
            }

            ScanRoom(target, now);

            if (LeadTarget != null)
            {
                string why = null;
                if (hadGap) why = "absent";
                else if (target == null) why = "gave_up";
                else if (!ReferenceEquals(target, LeadTarget)) why = "target_changed";
                else if (OwnSight(target))
                {
                    RefreshSight(fox, now, p);
                }
                else
                {
                    Fox seer = PackSight(target);
                    if (seer != null) RefreshSight(seer, now, p);
                    else if (now - lastOwnSight > Math.Max(0, p.FlankLeaderLostTicks)) why = "lost_target";
                }
                if (why != null) StepDown(why, fox, now, gap);
            }

            Fox roomLeader = null;
            if (target != null)
            {
                for (int i = 0; i < mates.Count; i++)
                {
                    Fox m = mates[i];
                    FoxFlank mf = m.AI.flank;
                    if (!ReferenceEquals(mf.LeadTarget, target) || !IsHunting(m, target)) continue;
                    if (roomLeader == null || Precedes(m, roomLeader)) roomLeader = m;
                }
            }
            if (LeadTarget != null)
            {
                if (roomLeader != null && Precedes(roomLeader, fox)) StepDown("yield", fox, now, 0);
                else roomLeader = null;
            }

            if (target != null && LeadTarget == null && roomLeader == null && OwnSight(target))
            {
                Elect(target, now);
            }

            Role role;
            Fox leader = null;
            if (target == null) role = Role.None;
            else if (ReferenceEquals(LeadTarget, target)) role = Role.Leader;
            else if (roomLeader != null) { role = Role.Follower; leader = roomLeader; }
            else role = Role.Solo;
            if (role == Role.Leader || role == Role.Follower)
            {
                knownLeaderTarget = target;
            }
            else if (!ReferenceEquals(target, knownLeaderTarget))
            {
                knownLeaderTarget = null;
            }
            ComputeFollowerIndex(role, target, leader);

            BuildSpread(p, role == Role.Follower, now);

            UpdateTeam(p, role, target, leader, now);

            UpdatePincer(p, role, target, leader, now);

            SpreadHeartbeat(p, now);
        }

        private void DisableUnderLiveFox(int now)
        {
            wasEnabled = false;
            loggedDisabled = true;
            if (LeadTarget != null) StepDown("disabled", fox, now, 0);
            TeamResetAll(now);
            phase = Phase.Idle;
            goalValid = false;
            spreadOn = false;
            spreadCount = 0;
        }

        private bool IsFlankMate(Fox mate, AbstractCreature ac)
        {
            if (mate == null || mate == fox || mate.AI == null || mate.AI.flank == null) return false;
            if (mate.room != fox.room || !mate.Consious || mate.mainBodyChunk == null) return false;
            if (ac.rippleLayer != fox.abstractCreature.rippleLayer && !ac.rippleBothSides
                && !fox.abstractCreature.rippleBothSides) return false;
            if (ac.creatureTemplate.CreatureRelationship(fox.Template).type != CreatureTemplate.Relationship.Type.Pack)
                return false;
            FoxBreedParams mp = mate.P;
            return mp != null && mp.FlankEnabled;
        }

        private static bool IsHunting(Fox f, AbstractCreature target)
        {
            if (f == null || f.AI == null || f.AI.preyTracker == null) return false;
            FoxAI.Behavior b = f.AI.CurrentBehavior;
            if (b != FoxAI.Behavior.Hunt && b != FoxAI.Behavior.Stalk) return false;
            Tracker.CreatureRepresentation top = f.AI.preyTracker.MostAttractivePrey;
            return top != null && ReferenceEquals(top.representedCreature, target);
        }

        private static bool Precedes(Fox a, Fox b)
        {
            int la = a.AI.flank.LeadSince, lb = b.AI.flank.LeadSince;
            if (la != lb) return la < lb;
            return CompareIds(a, b) < 0;
        }

        private static int CompareIds(Fox a, Fox b)
        {
            EntityID ia = a.abstractCreature.ID, ib = b.abstractCreature.ID;
            if (ia.number != ib.number) return ia.number.CompareTo(ib.number);
            return ia.spawner.CompareTo(ib.spawner);
        }

        private bool OwnSight(AbstractCreature target)
        {
            Creature c = target != null ? target.realizedCreature : null;
            if (c == null || c.room != fox.room || c.bodyChunks == null || fox.room == null) return false;
            for (int i = 0; i < c.bodyChunks.Length; i++)
            {
                if (ai.VisualContact(c.bodyChunks[i])) return true;
            }
            return false;
        }

        private Fox PackSight(AbstractCreature target)
        {
            for (int i = 0; i < mates.Count; i++)
            {
                Fox m = mates[i];
                if (!IsHunting(m, target)) continue;
                if (m.AI.flank.OwnSight(target)) return m;
            }
            return null;
        }

        private void RefreshSight(Fox by, int now, FoxBreedParams p)
        {
            lastOwnSight = now;
        }

        private void Elect(AbstractCreature target, int now)
        {
            LeadTarget = target;
            LeadSince = now;
            lastOwnSight = now;
        }

        internal void StepDown(string why, Fox by, int now, int gap)
        {
            if (LeadTarget == null) return;
            LeadTarget = null;
        }

        private void ScanRoom(AbstractCreature target, int now)
        {
            mates.Clear();
            Room room = fox.room;
            if (room == null || room.abstractRoom == null) return;
            var crits = room.abstractRoom.creatures;
            for (int i = 0; i < crits.Count; i++)
            {
                AbstractCreature ac = crits[i];
                if (ac == null || ac == fox.abstractCreature) continue;
                Fox m = ac.realizedCreature as Fox;
                if (m == null || m.AI == null || m.AI.flank == null) continue;
                if (m.AI.flank.LeadTarget != null && !m.Consious && m.room == room)
                {
                    m.AI.flank.StepDown(m.dead ? "died" : "stunned", fox, now, 0);
                }
                if (IsFlankMate(m, ac)) mates.Add(m);
            }
        }

        private void ComputeFollowerIndex(Role role, AbstractCreature target, Fox leader)
        {
            roleIndex = 0;
            roleOf = 0;
            if (role != Role.Follower) return;
            followers.Clear();
            followers.Add(fox);
            for (int i = 0; i < mates.Count; i++)
            {
                Fox m = mates[i];
                if (m == leader || !IsHunting(m, target)) continue;
                followers.Add(m);
            }
            followers.Sort(CompareIds);
            roleOf = followers.Count;
            roleIndex = followers.IndexOf(fox);
            if (roleIndex < 0) roleIndex = 0;
        }

        private void BuildSpread(FoxBreedParams p, bool on, int now)
        {
            spreadOn = on && mates.Count > 0;
            spreadCount = 0;
            if (!spreadOn) return;
            if (spreadPos.Length < mates.Count)
            {
                spreadPos = new Vector2[mates.Count * 2];
                spreadTile = new IntVector2[mates.Count * 2];
            }
            Room room = fox.room;
            for (int i = 0; i < mates.Count; i++)
            {
                spreadPos[i] = mates[i].mainBodyChunk.pos;
                spreadTile[i] = room.GetTilePosition(mates[i].mainBodyChunk.pos);
            }
            spreadCount = mates.Count;
            float div = Math.Max(1, spreadCount);
            spreadRadius = Mathf.Max(0f, p.FlankSpreadRadiusPx);
            spreadEach = Mathf.Max(0f, p.FlankSpreadCost) / div;
            spreadTileEach = Mathf.Max(0f, p.FlankMateTileCost) / div;
            spreadTick = now;
        }

        internal PathCost ApplySpread(Room room, MovementConnection connection, PathCost cost, bool query)
        {
            if (!spreadOn || spreadCount == 0 || room == null) return cost;
            if (Clock - spreadTick > 1) return cost;
            Vector2 a = room.MiddleOfTile(connection.destinationCoord);
            IntVector2 dt = connection.destinationCoord.Tile;
            float add = 0f;
            for (int i = 0; i < spreadCount; i++)
            {
                if (spreadRadius > 0f)
                    add += Mathf.InverseLerp(spreadRadius, 0f, Vector2.Distance(a, spreadPos[i])) * spreadEach;
                if (dt.x == spreadTile[i].x && dt.y == spreadTile[i].y) add += spreadTileEach;
            }
            if (query) { }
            else
            {
                if (add > 0f)
                {
                    if (add > winMax) winMax = add;
                }
            }
            if (add <= 0f) return cost;
            return cost + new PathCost(add, PathCost.Legality.Allowed);
        }

        internal bool TryGetCircleGoal(out WorldCoordinate g)
        {
            if (TryGetTeamGoal(out g)) return true;
            g = goal;
            return phase == Phase.Circling && goalValid && circleTick == Clock;
        }

        private void UpdatePincer(FoxBreedParams p, Role role, AbstractCreature target, Fox leader, int now)
        {
            int maxTicks = p.FlankCircleMaxTicks;
            float commitPx = Mathf.Max(0f, p.FlankCommitDistTiles) * TilePx;

            if (phase == Phase.Circling)
            {
                string why = null;
                float dTarget = -1f, dGoal = -1f;
                Vector2 tPos;
                bool haveT = TargetPos(target, out tPos);
                if (haveT) dTarget = Vector2.Distance(fox.mainBodyChunk.pos, tPos);
                if (now - circleStart >= Math.Max(1, maxTicks)) why = "timeout";
                else if (target == null || huntSerial != phaseHuntSerial) why = "hunt_ended";
                else if (role != Role.Follower) why = role == Role.Leader ? "became_leader" : "no_leader";
                else if (!haveT) why = "target_elsewhere";
                else if (dTarget <= commitPx) why = "close";
                else
                {
                    if (!PickGoal(p, tPos, leader, out bool sideChanged)) why = "unreachable";
                    else
                    {
                        dGoal = Vector2.Distance(fox.mainBodyChunk.pos, fox.room.MiddleOfTile(goal));
                        if (dGoal <= Mathf.Max(0f, p.FlankArriveTiles) * TilePx) why = "arrived";
                    }
                }
                if (why != null)
                {
                    phase = Phase.Done;
                    goalValid = false;
                }
                else circleTick = now;
                return;
            }

            if (phase == Phase.Done && phaseHuntSerial != huntSerial) phase = Phase.Idle;
            if (phase != Phase.Idle || role != Role.Follower || target == null) return;
            if (maxTicks <= 0) return;
            if (ai.pathFinder == null || !ai.pathFinder.DoneMappingAccessibility) return;
            Vector2 tp;
            if (!TargetPos(target, out tp)) return;

            phaseHuntSerial = huntSerial;
            float dT = Vector2.Distance(fox.mainBodyChunk.pos, tp);
            if (dT <= commitPx)
            {
                phase = Phase.Done;
                goalValid = false;
                goalSide = "none";
                return;
            }
            if (PickGoal(p, tp, leader, out _))
            {
                phase = Phase.Circling;
                circleStart = now;
                circleTick = now;
            }
            else
            {
                phase = Phase.Done;
                goalValid = false;
                goalSide = "none";
            }
        }

        private bool TargetPos(AbstractCreature target, out Vector2 pos)
        {
            pos = default(Vector2);
            Creature c = target != null ? target.realizedCreature : null;
            if (c == null || c.room != fox.room || c.mainBodyChunk == null) return false;
            pos = c.mainBodyChunk.pos;
            return true;
        }

        private bool PickGoal(FoxBreedParams p, Vector2 tPos, Fox leader, out bool sideChanged)
        {
            sideChanged = false;
            if (leader == null || leader.mainBodyChunk == null || fox.room == null)
            {
                goalValid = false;
                return false;
            }
            Vector2 axis = leader.mainBodyChunk.pos - tPos;
            float baseDeg = axis.sqrMagnitude > 0.0001f ? Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg : 0f;
            float fan = 0f;
            if (roleOf > 1)
            {
                float half = Mathf.Max(0f, p.FlankPincerFanDeg);
                fan = Mathf.Lerp(-half, half, roleIndex / (float)(roleOf - 1));
            }
            float radiusPx = Mathf.Max(0f, p.FlankPincerRadiusTiles) * TilePx;
            float farDeg = baseDeg + p.FlankPincerAngleDeg + fan;

            string prev = goalSide;
            if (TryCandidate(p, tPos, farDeg, radiusPx, "far", fan)) { sideChanged = goalValid && prev != goalSide && prev != "none"; return true; }

            float degA = baseDeg + 90f, degB = baseDeg - 90f;
            Vector2 here = fox.mainBodyChunk.pos;
            float dA = Vector2.Distance(here, tPos + Dir(degA) * radiusPx);
            float dB = Vector2.Distance(here, tPos + Dir(degB) * radiusPx);
            bool aFirst = dA <= dB;
            if (TryCandidate(p, tPos, aFirst ? degA : degB, radiusPx, aFirst ? "side_a" : "side_b", 0f)
                || TryCandidate(p, tPos, aFirst ? degB : degA, radiusPx, aFirst ? "side_b" : "side_a", 0f))
            {
                sideChanged = prev != goalSide && prev != "none";
                return true;
            }
            goalValid = false;
            return false;
        }

        private static Vector2 Dir(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        private bool TryCandidate(FoxBreedParams p, Vector2 tPos, float deg, float radiusPx, string side, float fan)
        {
            Room room = fox.room;
            if (room == null || room.abstractRoom == null || ai.pathFinder == null) return false;
            Vector2 ideal = tPos + Dir(deg) * radiusPx;
            IntVector2 c = room.GetTilePosition(ideal);
            c.x = Mathf.Clamp(c.x, 0, room.TileWidth - 1);
            c.y = Mathf.Clamp(c.y, 0, room.TileHeight - 1);
            int search = Math.Max(0, p.FlankGoalSearchTiles);
            for (int r = 0; r <= search; r++)
            {
                bool found = false;
                IntVector2 best = default(IntVector2);
                float bestD = float.MaxValue;
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (x < 0 || y < 0 || x >= room.TileWidth || y >= room.TileHeight) continue;
                        var coord = new WorldCoordinate(room.abstractRoom.index, x, y, -1);
                        if (!ai.pathFinder.CoordinateReachableAndGetbackable(coord)) continue;
                        if (fox.PoleDestinationRefused(coord)) continue;
                        if (fox.WallDestinationRefused(coord)) continue;
                        float d = Vector2.Distance(room.MiddleOfTile(x, y), ideal);
                        if (d < bestD) { bestD = d; best = new IntVector2(x, y); found = true; }
                    }
                }
                if (found)
                {
                    goal = new WorldCoordinate(room.abstractRoom.index, best.x, best.y, -1);
                    goalValid = true;
                    goalSide = side;
                    return true;
                }
            }
            return false;
        }

        private void SpreadHeartbeat(FoxBreedParams p, int now)
        {
            int cadence = Math.Max(1, p.PheromoneLogTicks);
            if (now - lastSpreadLogTick < cadence) return;
            lastSpreadLogTick = now;
            winMax = 0f;
        }
    }
}
