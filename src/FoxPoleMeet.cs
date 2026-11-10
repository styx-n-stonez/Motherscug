using System;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public partial class Fox
    {
        private enum PoleMeetPhase { None, Yield, Wait }

        private PoleMeetPhase meetPhase = PoleMeetPhase.None;
        private int meetBlockedTicks;
        private float meetCheckY;
        private int meetCheckDir;
        private int meetDetectTick;
        private int meetPhaseTick;
        private int meetCol;
        private float meetAxisX;
        private int meetDir;
        private Creature meetOther;
        private Room meetRoom;
        private bool meetRetreatEnded;
        private int meetAsideSide;
        private float meetAsideX;
        private int meetAsideRow;

        private const float MEET_ASIDE_ARRIVE_PX = 6f;

        private const int MEET_FLOOR_SCAN_ROWS = 4;

        private const int MEET_WENT_ELSEWHERE_TILES = 3;

        private void UpdatePoleMeet(FoxBreedParams p)
        {
            if (p == null) return;
            if (!p.ClimbsPoles)
            {
                if (meetPhase != PoleMeetPhase.None) EndPoleMeet(p, "climbs_poles_off");
                meetBlockedTicks = 0;
                return;
            }
            if (meetPhase != PoleMeetPhase.None && (room == null || room != meetRoom))
            {
                EndPoleMeet(p, "room_changed");
            }
            if (room == null || bodyChunks == null || bodyChunks.Length < 3)
            {
                meetBlockedTicks = 0;
                return;
            }
            if (!Consious)
            {
                if (meetPhase != PoleMeetPhase.None) EndPoleMeet(p, "unconscious");
                meetBlockedTicks = 0;
                return;
            }
            if (p.PoleMeetDetectTicks <= 0)
            {
                if (meetPhase != PoleMeetPhase.None) EndPoleMeet(p, "disabled");
                meetBlockedTicks = 0;
                return;
            }
            switch (meetPhase)
            {
                case PoleMeetPhase.None: DetectPoleMeet(p); break;
                case PoleMeetPhase.Yield: UpdatePoleYield(p); break;
                case PoleMeetPhase.Wait: UpdatePoleWait(p); break;
            }
        }

        private void DetectPoleMeet(FoxBreedParams p)
        {
            int dir = poleHeld ? PoleStepDir() : 0;
            if (dir == 0)
            {
                meetBlockedTicks = 0;
                meetCheckDir = 0;
                return;
            }
            float gap;
            Creature other = FindPoleBlocker(p, dir, out gap);
            float midY = bodyChunks[1].pos.y;
            if (other == null || dir != meetCheckDir || meetBlockedTicks == 0)
            {
                meetCheckY = midY;
                meetCheckDir = dir;
                if (other == null) { meetBlockedTicks = 0; return; }
            }
            if ((midY - meetCheckY) * dir >= p.PoleMeetProgressPx)
            {
                meetBlockedTicks = 0;
                meetCheckY = midY;
                return;
            }
            meetBlockedTicks++;
            int detect = p.PoleMeetDetectTicks;
            if (meetBlockedTicks < detect) return;

            string rule;
            int otherDir;
            bool headOn;
            bool yield = PoleMeetShouldYield(p, other, dir, out rule, out otherDir, out headOn);
            if (meetBlockedTicks == detect)
            {
                meetDetectTick = GameTick;
            }
            if (!yield && rule != "chase_target"
                && meetBlockedTicks >= detect + Math.Max(0, p.PoleMeetEscalateTicks))
            {
                yield = true;
                rule = "escalated";
            }
            if (yield) BeginPoleYield(p, other, dir, gap, otherDir, headOn, rule);
        }

        private Creature FindPoleBlocker(FoxBreedParams p, int dir, out float gap)
        {
            gap = 0f;
            int lead = 0;
            for (int i = 1; i < 3; i++)
            {
                if (bodyChunks[i].pos.y * dir > bodyChunks[lead].pos.y * dir) lead = i;
            }
            BodyChunk lc = bodyChunks[lead];
            float lane = Mathf.Max(0f, p.PoleMeetLanePx);
            float ahead = Mathf.Max(0f, p.PoleMeetAheadPx);
            Creature best = null;
            float bestAlong = float.MaxValue;
            var crits = room.abstractRoom.creatures;
            for (int k = 0; k < crits.Count; k++)
            {
                Creature c = crits[k] == null ? null : crits[k].realizedCreature;
                if (c == null || c == this || c.room != room || c.inShortcut || c.bodyChunks == null) continue;
                if (PoleMeetGrabRelated(c)) continue;
                for (int j = 0; j < c.bodyChunks.Length; j++)
                {
                    BodyChunk ch = c.bodyChunks[j];
                    if (ch == null || Mathf.Abs(ch.pos.x - poleAxisX) > lane) continue;
                    float along = (ch.pos.y - lc.pos.y) * dir;
                    if (along < -lc.rad || along > ahead) continue;
                    if (along < bestAlong)
                    {
                        bestAlong = along;
                        best = c;
                    }
                }
            }
            gap = best == null ? 0f : bestAlong;
            return best;
        }

        private bool PoleMeetGrabRelated(Creature c)
        {
            if (grasps != null)
            {
                for (int i = 0; i < grasps.Length; i++)
                {
                    if (grasps[i] != null && grasps[i].grabbed == c) return true;
                }
            }
            if (grabbedBy != null)
            {
                for (int i = 0; i < grabbedBy.Count; i++)
                {
                    if (grabbedBy[i] != null && grabbedBy[i].grabber == c) return true;
                }
            }
            return false;
        }

        private bool PoleMeetShouldYield(FoxBreedParams p, Creature other, int dir,
                                         out string rule, out int otherDir, out bool headOn)
        {
            otherDir = 0;
            headOn = false;
            if (other == chaseTarget)
            {
                rule = "chase_target";
                return false;
            }
            Fox of = other as Fox;
            if (of == null)
            {
                rule = "non_fox";
                return true;
            }
            if (of.PoleHeld && Mathf.Abs(of.PoleAxisX - poleAxisX) < 1f) otherDir = of.PoleStepDir();
            headOn = otherDir == -dir;
            if (!headOn)
            {
                rule = "not_head_on";
                return true;
            }
            string key = p.PoleMeetYieldRule ?? "";
            if (string.Equals(key, "descending", StringComparison.OrdinalIgnoreCase))
            {
                rule = "descending";
                return dir < 0;
            }
            if (string.Equals(key, "lower_rank", StringComparison.OrdinalIgnoreCase))
            {
                rule = "lower_rank";
                return PoleMeetRanksBelow(of);
            }
            rule = "ascending";
            return dir > 0;
        }

        private bool PoleMeetRanksBelow(Fox of)
        {
            float mine = abstractCreature.personality.dominance;
            float theirs = of.abstractCreature.personality.dominance;
            if (mine != theirs) return mine < theirs;
            return abstractCreature.ID.number < of.abstractCreature.ID.number;
        }

        private void BeginPoleYield(FoxBreedParams p, Creature other, int dir, float gap,
                                    int otherDir, bool headOn, string rule)
        {
            if (meetBlockedTicks < p.PoleMeetDetectTicks) meetDetectTick = GameTick;
            meetPhase = PoleMeetPhase.Yield;
            meetPhaseTick = GameTick;
            meetCol = poleTile.x;
            meetAxisX = poleAxisX;
            meetDir = dir;
            meetOther = other;
            meetRoom = room;
            meetRetreatEnded = false;
            meetAsideSide = 0;
            meetBlockedTicks = 0;
        }

        private void UpdatePoleYield(FoxBreedParams p)
        {
            if (!poleHeld)
            {
                BeginPoleWait(p, "off_pole");
                return;
            }
            if (!PoleMeetBlockerStillThere(p))
            {
                EndPoleMeet(p, "clear");
                return;
            }
            if (p.PoleMeetGiveUpTicks > 0 && GameTick - meetDetectTick >= p.PoleMeetGiveUpTicks)
            {
                ReleasePoleHold(p, "meet_give_up", !ReachActive && !climbOwnsGravity);
                poleRegrabTick = GameTick + Math.Max(0, p.PoleRegrabTicks);
                BeginPoleWait(p, "gave_up");
                return;
            }
        }

        private void BeginPoleWait(FoxBreedParams p, string why)
        {
            meetPhase = PoleMeetPhase.Wait;
            meetPhaseTick = GameTick;
            PoleMeetChooseAside(p);
        }

        private void UpdatePoleWait(FoxBreedParams p)
        {
            if (poleHeld)
            {
                EndPoleMeet(p, "holding_again");
                return;
            }
            if (!PoleMeetBlockerStillThere(p))
            {
                EndPoleMeet(p, "clear");
                return;
            }
            if (GameTick - meetPhaseTick >= Math.Max(0, p.PoleMeetWaitTicks))
            {
                EndPoleMeet(p, "wait_timeout");
                return;
            }
            if (Mathf.Abs(bodyChunks[1].pos.x - meetAxisX) > (Math.Max(0, p.PoleMeetAsideTiles) + MEET_WENT_ELSEWHERE_TILES) * 20f)
            {
                EndPoleMeet(p, "went_elsewhere");
                return;
            }
        }

        private bool PoleMeetBlockerStillThere(FoxBreedParams p)
        {
            Creature o = meetOther;
            if (o == null || o.room != room || o.inShortcut || o.slatedForDeletetion || o.bodyChunks == null) return false;
            float lane = Mathf.Max(0f, p.PoleMeetLanePx);
            float myY = bodyChunks[1].pos.y;
            for (int j = 0; j < o.bodyChunks.Length; j++)
            {
                BodyChunk ch = o.bodyChunks[j];
                if (ch == null) continue;
                if (Mathf.Abs(ch.pos.x - meetAxisX) <= lane && (ch.pos.y - myY) * meetDir > 0f) return true;
            }
            return false;
        }

        private void PoleMeetChooseAside(FoxBreedParams p)
        {
            meetAsideSide = 0;
            int k = Math.Max(0, p.PoleMeetAsideTiles);
            if (k == 0 || room.aimap == null) return;
            IntVector2 mid = room.GetTilePosition(bodyChunks[1].pos);
            int row = int.MinValue;
            for (int r = mid.y, n = 0; n <= MEET_FLOOR_SCAN_ROWS; r--, n++)
            {
                Room.Tile below = room.GetTile(meetCol, r - 1);
                if (below.Solid || below.Terrain == Room.Tile.TerrainType.Floor
                    || below.Terrain == Room.Tile.TerrainType.Slope)
                {
                    row = r;
                    break;
                }
            }
            if (row == int.MinValue) return;
            float lean = bodyChunks[1].pos.x - meetAxisX;
            int first = lean < 0f ? -1 : 1;
            for (int pass = 0; pass < 2; pass++)
            {
                int s = pass == 0 ? first : -first;
                int good = 0;
                for (int j = 1; j <= k; j++)
                {
                    int x = meetCol + s * j;
                    if (room.GetTile(x, row).Solid) break;
                    if (IsVerticalPoleTile(new IntVector2(x, row))) break;
                    if (!room.aimap.TileAccessibleToCreature(x, row, Template)) break;
                    good = j;
                }
                if (good > 0)
                {
                    meetAsideSide = s;
                    meetAsideRow = row;
                    meetAsideX = room.MiddleOfTile(meetCol + s * good, row).x;
                    return;
                }
            }
        }

        private bool PoleMeetBlocksGrip(int tileX)
        {
            return meetPhase == PoleMeetPhase.Wait && tileX == meetCol;
        }

        private MovementConnection PoleMeetStep(FoxBreedParams p, MovementConnection conn)
        {
            if (meetPhase == PoleMeetPhase.None) return conn;
            if (room == null || bodyChunks == null || bodyChunks.Length < 3) return conn;

            if (meetPhase == PoleMeetPhase.Yield)
            {
                if (!poleHeld) return conn;
                if (conn != default(MovementConnection) && InThisRoom(conn.startCoord)
                    && InThisRoom(conn.destinationCoord)
                    && (conn.DestTile.x != meetCol || (conn.DestTile.y - conn.StartTile.y) * meetDir < 0))
                {
                    return conn;
                }
                IntVector2 from = room.GetTilePosition(bodyChunks[0].pos);
                from.x = meetCol;
                IntVector2 to = new IntVector2(meetCol, from.y - meetDir);
                if (!IsVerticalPoleTile(to))
                {
                    if (!meetRetreatEnded)
                    {
                        meetRetreatEnded = true;
                    }
                    return default(MovementConnection);
                }
                return new MovementConnection(MovementConnection.MovementType.Standard,
                    room.GetWorldCoordinate(from), room.GetWorldCoordinate(to), 1);
            }

            if (conn != default(MovementConnection) && InThisRoom(conn.startCoord) && InThisRoom(conn.destinationCoord))
            {
                int fromCol = Math.Abs(conn.StartTile.x - meetCol);
                int toCol = Math.Abs(conn.DestTile.x - meetCol);
                if (toCol > 0 && toCol >= fromCol) return conn;
            }
            if (meetAsideSide == 0) return default(MovementConnection);
            float dx = meetAsideX - bodyChunks[1].pos.x;
            if (Mathf.Abs(dx) <= MEET_ASIDE_ARRIVE_PX) return default(MovementConnection);
            IntVector2 here = room.GetTilePosition(bodyChunks[1].pos);
            IntVector2 start = new IntVector2(here.x, meetAsideRow);
            IntVector2 dest = room.GetTilePosition(new Vector2(meetAsideX, room.MiddleOfTile(start).y));
            if (dest.x == start.x) return default(MovementConnection);
            return new MovementConnection(MovementConnection.MovementType.Standard,
                room.GetWorldCoordinate(start), room.GetWorldCoordinate(dest), 1);
        }

        private void EndPoleMeet(FoxBreedParams p, string reason)
        {
            meetPhase = PoleMeetPhase.None;
            meetOther = null;
            meetBlockedTicks = 0;
            meetCheckDir = 0;
            meetRetreatEnded = false;
            meetAsideSide = 0;
        }
    }
}
