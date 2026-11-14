using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public sealed partial class FoxFlank
    {
        private enum TeamPhase { Idle, Cover, Herd, Done }
        private enum PlanMode { None, Pincer, Block, Herd, Spent }

        private struct Way
        {
            internal bool Exit;
            internal float Deg;
            internal IntVector2 Mouth;
            internal int Steps;
            internal int Merged;
            internal bool ByLeader;
        }

        private static readonly IntVector2[] WayDirs =
        {
            new IntVector2(1, 0), new IntVector2(0, 1), new IntVector2(-1, 0), new IntVector2(0, -1),
            new IntVector2(1, 1), new IntVector2(-1, 1), new IntVector2(-1, -1), new IntVector2(1, -1)
        };

        private TeamPhase team = TeamPhase.Idle;
        private int teamHuntSerial = -1;
        private int teamStart;
        private int teamTick = int.MinValue;
        private Fox teamLeader;
        private int teamPlanSerial;
        private string teamKind = "none";
        private float teamDeg;
        private IntVector2 teamMouth;
        private float teamLastHerdDeg;
        private WorldCoordinate teamGoal;
        private bool teamGoalValid;
        private bool teamInPlace;
        private float teamHealth = -1f;
        internal bool TeamHolding { get; private set; }
        internal bool TeamHerding { get; private set; }

        private PlanMode plan = PlanMode.None;
        private int planHuntSerial = -1;
        private int planStart;
        private int planSerial;
        private int lastWaysCheck = int.MinValue / 2;
        internal int GoTick { get; private set; } = int.MinValue / 2;
        private Way[] ways = new Way[16];
        private int wayCount;
        private Way[] scratchWays = new Way[16];
        private readonly List<Fox> freeMates = new List<Fox>(8);
        private readonly List<Fox> lineFoxes = new List<Fox>(8);
        private readonly List<float> lineDeg = new List<float>(8);
        private IntVector2 deadEnd;
        private float herdDeg;
        private int planNeed;

        private Room exitRoom;
        private readonly List<int> exitIdx = new List<int>(8);
        private readonly List<IntVector2> exitMouth = new List<IntVector2>(8);

        private static int CoverCap(FoxBreedParams p) { return p.FlankSyncMaxWaitTicks; }

        private static int HerdCap(FoxBreedParams p) { return Math.Min(p.FlankHerdMaxTicks, p.FlankSyncMaxWaitTicks); }

        private bool InTeam { get { return team == TeamPhase.Cover || team == TeamPhase.Herd; } }

        private bool TryGetTeamGoal(out WorldCoordinate g)
        {
            g = teamGoal;
            return InTeam && teamGoalValid && teamTick == Clock;
        }

        private void UpdateTeam(FoxBreedParams p, Role role, AbstractCreature target, Fox leader, int now)
        {
            TeamHolding = false;
            TeamHerding = false;
            if (team == TeamPhase.Done && teamHuntSerial != huntSerial) team = TeamPhase.Idle;

            if (role == Role.Leader) LeaderTick(p, target, now);
            else if (plan == PlanMode.Block || plan == PlanMode.Herd)
            {
                plan = PlanMode.Spent;
            }

            if (InTeam) MemberTick(p, role, target, leader, now);
        }

        private void TeamResetAll(int now)
        {
            if (InTeam) EndTeam("disabled", now, -1f, -1f);
            team = TeamPhase.Idle;
            plan = PlanMode.None;
            TeamHolding = false;
            TeamHerding = false;
        }

        private void LeaderTick(FoxBreedParams p, AbstractCreature target, int now)
        {
            if (target == null) return;
            if (!p.FlankBlockEnabled && !p.FlankHerdEnabled)
            {
                if (plan != PlanMode.None) plan = PlanMode.None;
                return;
            }
            if (planHuntSerial != huntSerial)
            {
                planHuntSerial = huntSerial;
                plan = PlanMode.None;
                lastWaysCheck = int.MinValue / 2;
            }

            if (plan != PlanMode.Block && plan != PlanMode.Herd && plan != PlanMode.Spent && HandoverPending(target))
            {
                PublishGo("handover", now);
                plan = PlanMode.Spent;
                return;
            }

            if (plan == PlanMode.Block || plan == PlanMode.Herd)
            {
                RunPlan(p, target, now);
                return;
            }
            if (plan == PlanMode.Spent) return;
            if (now - lastWaysCheck < Math.Max(1, p.FlankWaysRecheckTicks)) return;
            lastWaysCheck = now;
            MakePlan(p, target, now);
        }

        private bool HandoverPending(AbstractCreature target)
        {
            if (InTeam && !ReferenceEquals(teamLeader, fox)) return true;
            for (int i = 0; i < mates.Count; i++)
            {
                FoxFlank mf = mates[i].AI.flank;
                if (mf.InTeam && !ReferenceEquals(mf.teamLeader, fox) && IsHunting(mates[i], target)) return true;
            }
            return false;
        }

        private void MakePlan(FoxBreedParams p, AbstractCreature target, int now)
        {
            Room room = fox.room;
            Vector2 tPos;
            if (room == null || room.aimap == null || !room.readyForAI || !TargetPos(target, out tPos)) return;
            if (ai.pathFinder == null || !ai.pathFinder.DoneMappingAccessibility) return;

            freeMates.Clear();
            int hunters = 0;
            for (int i = 0; i < mates.Count; i++)
            {
                Fox m = mates[i];
                if (!IsHunting(m, target)) continue;
                hunters++;
                FoxFlank mf = m.AI.flank;
                if (mf.InTeam || (mf.team == TeamPhase.Done && mf.teamHuntSerial == mf.huntSerial)) continue;
                if (mf.phase == Phase.Done && mf.phaseHuntSerial == mf.huntSerial) continue;
                if (m.AI.pathFinder == null || !m.AI.pathFinder.DoneMappingAccessibility) continue;
                freeMates.Add(m);
            }
            if (hunters == 0) return;

            IntVector2 pt;
            int w = PlayerTile(p, target, out pt) ? CountWays(p, pt, ways) : -1;
            wayCount = Math.Max(0, w);
            int byLeader = MarkLeaderWay(p, tPos);

            int freeBlock = 0, freeHerd = 0;
            for (int i = 0; i < freeMates.Count; i++)
            {
                FoxBreedParams mp = freeMates[i].P;
                if (mp == null) continue;
                if (mp.FlankBlockEnabled) freeBlock++;
                if (mp.FlankHerdEnabled) freeHerd++;
            }
            int need = Math.Max(0, w - byLeader);
            planNeed = need;

            PlanMode mode = PlanMode.Pincer;
            if (w < 0) { }
            else if (w == 0) { }
            else
            {
                bool blockFits = p.FlankBlockEnabled && CoverCap(p) > 0 && w <= Math.Max(0, p.FlankBlockMaxWays)
                                 && freeBlock >= Math.Max(1, need);
                if (blockFits)
                {
                    if (StartBlock(p, tPos, now)) { mode = PlanMode.Block; }
                }
                else
                {
                    if (!p.FlankHerdEnabled) { }
                    else if (freeHerd == 0) { }
                    else if (HerdCap(p) <= 0) { }
                    else if (!FindDeadEnd(p, pt, out deadEnd)) { }
                    else if (StartHerd(p, tPos, now)) { mode = PlanMode.Herd; }
                }
            }
            plan = mode;
        }

        private bool StartBlock(FoxBreedParams p, Vector2 tPos, int now)
        {
            planSerial++;
            int joined = 0, wayJoined = 0;
            lineFoxes.Clear();
            float r = Mathf.Max(0f, p.FlankBlockDistTiles) * TilePx;
            for (int w = 0; w < wayCount; w++)
            {
                if (ways[w].ByLeader)
                {
                    continue;
                }
                Fox best = null;
                float bestD = float.MaxValue;
                Vector2 ideal = WayIdeal(ways[w].Exit, ways[w].Mouth, ways[w].Deg, tPos, r);
                for (int i = 0; i < freeMates.Count; i++)
                {
                    Fox f = freeMates[i];
                    if (lineFoxes.Contains(f) || f.P == null || !f.P.FlankBlockEnabled) continue;
                    IntVector2 tile;
                    if (!FindSpot(f, p, ideal, out tile)) continue;
                    float d = Vector2.Distance(f.mainBodyChunk.pos, fox.room.MiddleOfTile(tile));
                    if (d < bestD) { bestD = d; best = f; }
                }
                if (best == null)
                {
                    continue;
                }
                lineFoxes.Add(best);
                best.AI.flank.JoinTeam(TeamPhase.Cover, fox, planSerial, w, ways[w].Exit ? "exit" : "open",
                                       ways[w].Deg, ways[w].Mouth, now);
                joined++;
                wayJoined++;
            }
            if (wayJoined == 0 && planNeed > 0)
            {
                lineFoxes.Clear();
                return false;
            }
            float baseDeg = DegOf(fox.mainBodyChunk.pos - tPos);
            float farDeg = baseDeg + p.FlankPincerAngleDeg;
            for (int i = 0; i < freeMates.Count; i++)
            {
                Fox f = freeMates[i];
                if (lineFoxes.Contains(f) || f.P == null || !f.P.FlankBlockEnabled) continue;
                Vector2 ideal = tPos + Dir(farDeg) * r;
                IntVector2 tile;
                if (!FindSpot(f, p, ideal, out tile))
                {
                    continue;
                }
                lineFoxes.Add(f);
                f.AI.flank.JoinTeam(TeamPhase.Cover, fox, planSerial, -1, "far", 0f, default(IntVector2), now);
                joined++;
            }
            lineFoxes.Clear();
            if (joined == 0) return false;
            if (p.FlankSyncEnabled) JoinTeam(TeamPhase.Cover, fox, planSerial, -2, "direct", 0f, default(IntVector2), now);
            plan = PlanMode.Block;
            planStart = now;
            return true;
        }

        private bool StartHerd(FoxBreedParams p, Vector2 tPos, int now)
        {
            Vector2 de = fox.room.MiddleOfTile(deadEnd);
            herdDeg = DegOf(tPos - de);
            lineFoxes.Clear();
            lineDeg.Clear();
            lineFoxes.Add(fox);
            for (int i = 0; i < freeMates.Count; i++)
            {
                if (freeMates[i].P != null && freeMates[i].P.FlankHerdEnabled) lineFoxes.Add(freeMates[i]);
            }
            for (int i = 0; i < lineFoxes.Count; i++)
                lineDeg.Add(Mathf.DeltaAngle(herdDeg, DegOf(lineFoxes[i].mainBodyChunk.pos - tPos)));
            for (int i = 1; i < lineFoxes.Count; i++)
            {
                for (int j = i; j > 0 && lineDeg[j] < lineDeg[j - 1]; j--)
                {
                    float td = lineDeg[j]; lineDeg[j] = lineDeg[j - 1]; lineDeg[j - 1] = td;
                    Fox tf = lineFoxes[j]; lineFoxes[j] = lineFoxes[j - 1]; lineFoxes[j - 1] = tf;
                }
            }
            planSerial++;
            int n = lineFoxes.Count, followersJoined = 0;
            float half = Mathf.Max(0f, p.FlankHerdFanDeg);
            float gap = Mathf.Max(0f, p.FlankHerdGapTiles) * TilePx;
            bool leaderJoined = false;
            for (int k = 0; k < n; k++)
            {
                Fox f = lineFoxes[k];
                float off = n > 1 ? Mathf.Lerp(-half, half, k / (float)(n - 1)) : 0f;
                Vector2 ideal = tPos + Dir(herdDeg + off) * gap;
                IntVector2 tile;
                bool ok = FindSpot(f, p, ideal, out tile);
                if (!ok) continue;
                if (ReferenceEquals(f, fox)) leaderJoined = true; else followersJoined++;
                f.AI.flank.JoinTeam(TeamPhase.Herd, fox, planSerial, k, "line", off, deadEnd, now);
                f.AI.flank.teamLastHerdDeg = herdDeg;
            }
            lineFoxes.Clear();
            lineDeg.Clear();
            if (followersJoined == 0)
            {
                if (leaderJoined) { team = TeamPhase.Idle; teamHuntSerial = -1; }
                return false;
            }
            plan = PlanMode.Herd;
            planStart = now;
            return true;
        }

        private void RunPlan(FoxBreedParams p, AbstractCreature target, int now)
        {
            int cap = plan == PlanMode.Herd ? HerdCap(p) : CoverCap(p);
            if (now - planStart >= Math.Max(1, cap))
            {
                PublishGo("timeout", now);
                plan = PlanMode.Spent;
                return;
            }
            int m = 0, n = 0;
            for (int i = 0; i < mates.Count; i++)
            {
                FoxFlank mf = mates[i].AI.flank;
                if (!mf.InTeam || !ReferenceEquals(mf.teamLeader, fox) || mf.teamPlanSerial != planSerial) continue;
                m++;
                if (mf.teamInPlace) n++;
            }
            if (m == 0)
            {
                PublishGo("no_followers", now);
                plan = PlanMode.Spent;
                return;
            }
            if (plan == PlanMode.Block)
            {
                if (p.FlankSyncEnabled && n == m)
                {
                    PublishGo("all_ready", now);
                    plan = PlanMode.Spent;
                }
                return;
            }
            if (now - lastWaysCheck < Math.Max(1, p.FlankWaysRecheckTicks)) return;
            lastWaysCheck = now;
            IntVector2 pt;
            if (!PlayerTile(p, target, out pt)) return;
            int w = CountWays(p, pt, scratchWays);
            if (w >= 0 && w <= 1)
            {
                PublishGo("cornered", now);
                plan = PlanMode.Spent;
            }
        }

        private void RequestGo(string reason, int now)
        {
            PublishGo(reason, now);
            if (plan == PlanMode.Block || plan == PlanMode.Herd) plan = PlanMode.Spent;
        }

        private void PublishGo(string reason, int now)
        {
            if (GoTick > now) return;
            GoTick = now + 1;
            for (int i = 0; i < mates.Count; i++)
            {
                FoxFlank mf = mates[i].AI.flank;
                if (!mf.InTeam) continue;
                if (!ReferenceEquals(mf.teamLeader, fox) && reason != "handover") continue;
                if (ai.pheromone != null) ai.pheromone.NoteSignal(mates[i]);
            }
        }

        private void JoinTeam(TeamPhase kindPhase, Fox leader, int serial, int way, string kind, float deg,
                              IntVector2 mouth, int now)
        {
            if (!ReferenceEquals(leader, fox))
            {
                phase = Phase.Done;
                phaseHuntSerial = huntSerial;
                goalValid = false;
            }
            team = kindPhase;
            teamHuntSerial = huntSerial;
            teamStart = now;
            teamTick = int.MinValue;
            teamLeader = leader;
            teamPlanSerial = serial;
            teamKind = kind;
            teamDeg = deg;
            teamMouth = mouth;
            teamGoalValid = false;
            teamInPlace = false;
            teamHealth = Health();
        }

        private void MemberTick(FoxBreedParams p, Role role, AbstractCreature target, Fox leader, int now)
        {
            Fox L = role == Role.Leader ? fox : (role == Role.Follower ? leader : null);
            int cap = team == TeamPhase.Herd ? HerdCap(p) : CoverCap(p);
            float commitPx = Mathf.Max(0f, p.FlankCommitDistTiles) * TilePx;
            float dT = -1f, dG = -1f;
            Vector2 tPos;
            bool haveT = TargetPos(target, out tPos);
            if (haveT) dT = Vector2.Distance(fox.mainBodyChunk.pos, tPos);
            bool hit = HealthDropped();
            string why = null;
            if (now - teamStart >= Math.Max(1, cap)) why = "timeout";
            else if (L != null && L.AI.flank.GoTick > teamStart && now >= L.AI.flank.GoTick)
            {
                why = "go";
            }
            else if (team == TeamPhase.Cover ? !p.FlankBlockEnabled : !p.FlankHerdEnabled) why = "disabled";
            else if (target == null || huntSerial != teamHuntSerial) why = "hunt_ended";
            else if (L == null) why = "no_leader";
            else if (!haveT) why = "target_elsewhere";
            else if (hit) why = "attacked";
            else if (dT <= commitPx) why = "close";
            else
            {
                Vector2 ideal = MemberIdeal(p, tPos, L);
                IntVector2 tile;
                if (!FindSpot(fox, p, ideal, out tile)) why = "unreachable";
                else
                {
                    teamGoal = new WorldCoordinate(fox.room.abstractRoom.index, tile.x, tile.y, -1);
                    teamGoalValid = true;
                    dG = Vector2.Distance(fox.mainBodyChunk.pos, fox.room.MiddleOfTile(tile));
                    teamInPlace = dG <= Mathf.Max(0f, p.FlankArriveTiles) * TilePx;
                    if (team == TeamPhase.Cover && teamInPlace && !p.FlankSyncEnabled) why = "arrived";
                }
            }
            if (why != null)
            {
                if (why == "attacked" && L != null) L.AI.flank.RequestGo("attacked", now);
                EndTeam(why, now, dT, dG);
                return;
            }
            teamTick = now;
            TeamHolding = team == TeamPhase.Cover && teamInPlace;
            TeamHerding = team == TeamPhase.Herd && teamInPlace;
        }

        private Vector2 MemberIdeal(FoxBreedParams p, Vector2 tPos, Fox L)
        {
            float r = Mathf.Max(0f, p.FlankBlockDistTiles) * TilePx;
            switch (teamKind)
            {
                case "exit": return WayIdeal(true, teamMouth, 0f, tPos, r);
                case "open": return WayIdeal(false, default(IntVector2), teamDeg, tPos, r);
                case "far":
                    return tPos + Dir(DegOf(L.mainBodyChunk.pos - tPos) + p.FlankPincerAngleDeg) * r;
                case "direct":
                    return tPos + Dir(DegOf(fox.mainBodyChunk.pos - tPos)) * r;
                default:
                {
                    Vector2 de = fox.room.MiddleOfTile(teamMouth);
                    if ((tPos - de).sqrMagnitude > TilePx * TilePx) teamLastHerdDeg = DegOf(tPos - de);
                    return tPos + Dir(teamLastHerdDeg + teamDeg) * (Mathf.Max(0f, p.FlankHerdGapTiles) * TilePx);
                }
            }
        }

        private void EndTeam(string why, int now, float dT, float dG)
        {
            team = TeamPhase.Done;
            teamGoalValid = false;
            TeamHolding = false;
            TeamHerding = false;
        }

        private float Health()
        {
            HealthState hs = fox.State as HealthState;
            return hs != null ? hs.health : 1f;
        }

        private bool HealthDropped()
        {
            float h = Health();
            bool dropped = teamHealth >= 0f && h < teamHealth - 0.0001f;
            teamHealth = h;
            return dropped;
        }

        private bool PlayerTile(FoxBreedParams p, AbstractCreature target, out IntVector2 t)
        {
            t = default(IntVector2);
            Creature c = target != null ? target.realizedCreature : null;
            Room room = fox.room;
            if (c == null || c.room != room || c.mainBodyChunk == null || room.aimap == null) return false;
            t = room.aimap.TryForAccessibleNeighbor(room.GetTilePosition(c.mainBodyChunk.pos), fox.Template);
            return SnapDown(ref t, Math.Max(0, p.FlankGoalSearchTiles));
        }

        private bool SnapDown(ref IntVector2 t, int maxDown)
        {
            for (int d = 0; d <= maxDown; d++)
            {
                if (Acc(t.x, t.y - d)) { t.y -= d; return true; }
            }
            return false;
        }

        private bool Acc(int x, int y)
        {
            Room room = fox.room;
            if (x < 0 || y < 0 || x >= room.TileWidth || y >= room.TileHeight) return false;
            return room.aimap.TileAccessibleToCreature(x, y, fox.Template);
        }

        private int CountWays(FoxBreedParams p, IntVector2 t, Way[] buf)
        {
            Room room = fox.room;
            if (room == null || room.aimap == null || !Acc(t.x, t.y)) return -1;
            int n = 0;
            float merge = Mathf.Max(0f, p.FlankWaysMergeDeg);
            RefreshExitCache(room);
            int maxSteps = p.FlankWaysExitSteps;
            Vector2 here = room.MiddleOfTile(t);
            for (int i = 0; i < exitIdx.Count; i++)
            {
                int d = room.aimap.ExitDistanceForCreature(t, exitIdx[i], fox.Template);
                if (d < 1 || d > maxSteps) continue;
                AddWay(buf, ref n, true, DegOf(room.MiddleOfTile(exitMouth[i]) - here), exitMouth[i], d, merge);
            }
            int len = Math.Max(1, p.FlankWaysOpenTiles);
            for (int k = 0; k < WayDirs.Length; k++)
            {
                if (OpenRay(t, WayDirs[k], len)) AddWay(buf, ref n, false, DegOf(new Vector2(WayDirs[k].x, WayDirs[k].y)), default(IntVector2), len, merge);
            }
            return n;
        }

        private static void AddWay(Way[] buf, ref int n, bool exit, float deg, IntVector2 mouth, int steps, float merge)
        {
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(buf[i].Deg, deg)) < merge) { buf[i].Merged++; return; }
            }
            if (n >= buf.Length) return;
            buf[n] = new Way { Exit = exit, Deg = deg, Mouth = mouth, Steps = steps, Merged = 0, ByLeader = false };
            n++;
        }

        private bool OpenRay(IntVector2 t, IntVector2 dir, int len)
        {
            IntVector2 cur = t;
            IntVector2 sideA = dir.x != 0 ? new IntVector2(0, 1) : new IntVector2(1, 0);
            for (int k = 0; k < len; k++)
            {
                IntVector2 nx = new IntVector2(cur.x + dir.x, cur.y + dir.y);
                if (Acc(nx.x, nx.y)) { cur = nx; continue; }
                if (Acc(nx.x + sideA.x, nx.y + sideA.y)) { cur = new IntVector2(nx.x + sideA.x, nx.y + sideA.y); continue; }
                if (Acc(nx.x - sideA.x, nx.y - sideA.y)) { cur = new IntVector2(nx.x - sideA.x, nx.y - sideA.y); continue; }
                return false;
            }
            return true;
        }

        private int MarkLeaderWay(FoxBreedParams p, Vector2 tPos)
        {
            float leaderDeg = DegOf(fox.mainBodyChunk.pos - tPos);
            float merge = Mathf.Max(0f, p.FlankWaysMergeDeg);
            int best = -1;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < wayCount; i++)
            {
                ways[i].ByLeader = false;
                float d = Mathf.Abs(Mathf.DeltaAngle(ways[i].Deg, leaderDeg));
                if (d < merge && d < bestDiff) { bestDiff = d; best = i; }
            }
            if (best < 0) return 0;
            ways[best].ByLeader = true;
            return 1;
        }

        private void RefreshExitCache(Room room)
        {
            if (ReferenceEquals(exitRoom, room)) return;
            exitRoom = room;
            exitIdx.Clear();
            exitMouth.Clear();
            if (room.abstractRoom == null || room.abstractRoom.nodes == null || room.shortcuts == null) return;
            var nodes = room.abstractRoom.nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].type != AbstractRoomNode.Type.Exit) continue;
                int idx = room.abstractRoom.CommonToCreatureSpecificNodeIndex(i, fox.Template);
                if (idx < 0) continue;
                exitIdx.Add(idx);
                exitMouth.Add(room.LocalCoordinateOfNode(i).Tile);
            }
        }

        private bool FindDeadEnd(FoxBreedParams p, IntVector2 pt, out IntVector2 best)
        {
            best = default(IntVector2);
            int step = Math.Max(1, p.FlankHerdSearchStepTiles);
            int max = Math.Max(0, p.FlankHerdSearchTiles);
            Room room = fox.room;
            bool found = false;
            int bestD2 = int.MaxValue;
            for (int r = step; r <= max; r += step)
            {
                for (int k = 0; k < WayDirs.Length; k++)
                {
                    IntVector2 c = new IntVector2(pt.x + WayDirs[k].x * r, pt.y + WayDirs[k].y * r);
                    if (c.x < 0 || c.y < 0 || c.x >= room.TileWidth || c.y >= room.TileHeight) continue;
                    if (!SnapDown(ref c, step)) continue;
                    int d2 = (c.x - pt.x) * (c.x - pt.x) + (c.y - pt.y) * (c.y - pt.y);
                    if (d2 >= bestD2) continue;
                    var coord = new WorldCoordinate(room.abstractRoom.index, c.x, c.y, -1);
                    if (!ai.pathFinder.CoordinateReachableAndGetbackable(coord)) continue;
                    int w = CountWays(p, c, scratchWays);
                    if (w < 0 || w > 1) continue;
                    best = c;
                    bestD2 = d2;
                    found = true;
                }
            }
            return found;
        }

        private Vector2 WayIdeal(bool exit, IntVector2 mouth, float deg, Vector2 tPos, float r)
        {
            if (!exit) return tPos + Dir(deg) * r;
            Vector2 m = fox.room.MiddleOfTile(mouth);
            Vector2 d = m - tPos;
            float len = d.magnitude;
            if (len < 1f) return m;
            return tPos + d / len * Mathf.Min(r, len);
        }

        private bool FindSpot(Fox f, FoxBreedParams p, Vector2 ideal, out IntVector2 best)
        {
            best = default(IntVector2);
            Room room = fox.room;
            if (room == null || room.abstractRoom == null || f == null || f.AI == null || f.AI.pathFinder == null
                || f.room != room) return false;
            IntVector2 c = room.GetTilePosition(ideal);
            c.x = Mathf.Clamp(c.x, 0, room.TileWidth - 1);
            c.y = Mathf.Clamp(c.y, 0, room.TileHeight - 1);
            int search = Math.Max(0, p.FlankGoalSearchTiles);
            for (int r = 0; r <= search; r++)
            {
                bool found = false;
                float bestD = float.MaxValue;
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (x < 0 || y < 0 || x >= room.TileWidth || y >= room.TileHeight) continue;
                        var coord = new WorldCoordinate(room.abstractRoom.index, x, y, -1);
                        if (!f.AI.pathFinder.CoordinateReachableAndGetbackable(coord)) continue;
                        if (f.PoleDestinationRefused(coord)) continue;
                        if (f.WallDestinationRefused(coord)) continue;
                        float d = Vector2.Distance(room.MiddleOfTile(x, y), ideal);
                        if (d < bestD) { bestD = d; best = new IntVector2(x, y); found = true; }
                    }
                }
                if (found) return true;
            }
            return false;
        }

        private static float DegOf(Vector2 v)
        {
            return v.sqrMagnitude > 0.0001f ? Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg : 0f;
        }
    }
}
