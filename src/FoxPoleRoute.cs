using System;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal static class FoxPoleDestination
    {
        internal static void Install()
        {
            On.PathFinder.SetDestination += SetDestination;
        }

        private static void SetDestination(On.PathFinder.orig_SetDestination orig, PathFinder self,
                                           WorldCoordinate newDestination)
        {
            if (self != null && self.creature != null
                && FoxCritob.IsFoxType(self.creature.creatureTemplate.type))
            {
                Fox fox = self.creature.realizedCreature as Fox;
                if (fox != null) newDestination = fox.FilterPoleDestination(newDestination);
            }
            orig(self, newDestination);
        }
    }

    public partial class Fox
    {
        private const int POLE_FLOOD_MAX_AGE = 80;

        private Room prRoom;
        private int prW, prH;
        private int[] prStampNoH;
        private int[] prStampAll;
        private int[] prQueue;
        private int prGenNoH;
        private int prGenAll;
        private bool prAllValid;
        private IntVector2 prStart;
        private int prFloodTick = int.MinValue;
        private Room prHRoom;
        private bool prHRoomHas;
        private WorldCoordinate prSubAsked;
        private WorldCoordinate prSub;
        private int prSubGen = -1;
        private bool prSubOk;

        internal WorldCoordinate FilterPoleDestination(WorldCoordinate d)
        {
            FoxBreedParams p = P;
            if (p == null || !p.ClimbsPoles || p.PoleHorizontalAllowed || !p.PoleDestFilter) return d;
            string why;
            if (!PoleDestinationRefusedCore(d, out why)) return d;
            WorldCoordinate sub;
            if (!NearestPoleSafeTile(d, out sub))
            {
                return d;
            }
            return sub;
        }

        internal bool PoleDestinationRefused(WorldCoordinate d)
        {
            FoxBreedParams p = P;
            if (p == null || !p.ClimbsPoles || p.PoleHorizontalAllowed || !p.PoleDestFilter) return false;
            string why;
            if (!PoleDestinationRefusedCore(d, out why)) return false;
            return true;
        }

        private bool PoleDestinationRefusedCore(WorldCoordinate d, out string why)
        {
            why = null;
            if (room == null || room.aimap == null || bodyChunks == null) return false;
            if (!d.TileDefined || d.room != room.abstractRoom.index) return false;
            if (d.x < 0 || d.y < 0 || d.x >= room.TileWidth || d.y >= room.TileHeight) return false;
            if (!RoomHasHorizontalPole()) return false;
            if (FoxAI.IsHorizontalOnlyPole(room, d))
            {
                why = "on_horizontal_pole";
                return true;
            }
            if (!EnsurePoleSafeFlood()) return false;
            int idx = d.y * prW + d.x;
            if (prStampNoH[idx] == prGenNoH) return false;
            EnsureFullFlood();
            if (prStampAll[idx] == prGenAll)
            {
                why = "behind_horizontal_pole";
                return true;
            }
            return false;
        }

        private bool RoomHasHorizontalPole()
        {
            if (prHRoom == room) return prHRoomHas;
            prHRoom = room;
            prHRoomHas = false;
            int idx = room.abstractRoom.index;
            for (int x = 0; x < room.TileWidth && !prHRoomHas; x++)
            {
                for (int y = 0; y < room.TileHeight; y++)
                {
                    if (FoxAI.IsHorizontalOnlyPole(room, new WorldCoordinate(idx, x, y, -1)))
                    {
                        prHRoomHas = true;
                        break;
                    }
                }
            }
            return prHRoomHas;
        }

        private bool EnsurePoleSafeFlood()
        {
            if (prRoom != room || prW != room.TileWidth || prH != room.TileHeight || prStampNoH == null)
            {
                prRoom = room;
                prW = room.TileWidth;
                prH = room.TileHeight;
                prStampNoH = new int[prW * prH];
                prStampAll = new int[prW * prH];
                prQueue = new int[prW * prH];
                prGenNoH = 0;
                prGenAll = 0;
                prAllValid = false;
                prFloodTick = int.MinValue;
                prSubGen = -1;
            }
            IntVector2 start;
            if (!PoleFloodStart(out start)) return false;
            int sidx = start.y * prW + start.x;
            bool fresh = prGenNoH != 0 && prStampNoH[sidx] == prGenNoH
                         && GameTick - prFloodTick < POLE_FLOOD_MAX_AGE && GameTick >= prFloodTick;
            if (fresh) return true;
            prGenNoH++;
            prAllValid = false;
            prStart = start;
            prFloodTick = GameTick;
            PoleFlood(prStampNoH, prGenNoH, start, true);
            return true;
        }

        private void EnsureFullFlood()
        {
            if (prAllValid) return;
            prGenAll++;
            PoleFlood(prStampAll, prGenAll, prStart, false);
            prAllValid = true;
        }

        private bool PoleFloodStart(out IntVector2 start)
        {
            CreatureTemplate tpl = Template;
            for (int i = 0; i < bodyChunks.Length && i < 3; i++)
            {
                IntVector2 t = room.GetTilePosition(bodyChunks[i].pos);
                for (int dy = 0; dy >= -1; dy--)
                {
                    int x = t.x, y = t.y + dy;
                    if (x < 0 || y < 0 || x >= prW || y >= prH) continue;
                    if (room.aimap.TileCostForCreature(x, y, tpl).Allowed)
                    {
                        start = new IntVector2(x, y);
                        return true;
                    }
                }
            }
            start = new IntVector2(0, 0);
            return false;
        }

        private int PoleFlood(int[] stamp, int gen, IntVector2 start, bool refuseHorizontal)
        {
            AImap map = room.aimap;
            CreatureTemplate tpl = Template;
            int roomIdx = room.abstractRoom.index;
            int head = 0, tail = 0;
            int sidx = start.y * prW + start.x;
            stamp[sidx] = gen;
            prQueue[tail++] = sidx;
            while (head < tail)
            {
                int cur = prQueue[head++];
                AItile ai = map.getAItile(cur % prW, cur / prW);
                if (ai == null || ai.outgoingPaths == null) continue;
                var paths = ai.outgoingPaths;
                for (int k = 0; k < paths.Count; k++)
                {
                    MovementConnection c = paths[k];
                    WorldCoordinate dc = c.destinationCoord;
                    if (!dc.TileDefined || dc.room != roomIdx) continue;
                    if (dc.x < 0 || dc.y < 0 || dc.x >= prW || dc.y >= prH) continue;
                    int di = dc.y * prW + dc.x;
                    if (stamp[di] == gen) continue;
                    if (!map.IsConnectionAllowedForCreature(c, tpl)) continue;
                    if (!map.TileCostForCreature(dc.x, dc.y, tpl).Allowed) continue;
                    if (refuseHorizontal && FoxAI.IsHorizontalOnlyPole(room, dc)) continue;
                    stamp[di] = gen;
                    prQueue[tail++] = di;
                }
            }
            return tail;
        }

        private bool NearestPoleSafeTile(WorldCoordinate d, out WorldCoordinate sub)
        {
            if (prSubGen == prGenNoH && prSubAsked.x == d.x && prSubAsked.y == d.y && prSubAsked.room == d.room)
            {
                sub = prSub;
                return prSubOk;
            }
            int roomIdx = room.abstractRoom.index;
            int best = -1;
            int bestD = int.MaxValue;
            bool bestFloor = false;
            for (int i = 0; i < prStampNoH.Length; i++)
            {
                if (prStampNoH[i] != prGenNoH) continue;
                int x = i % prW, y = i / prW;
                int dd = (x - d.x) * (x - d.x) + (y - d.y) * (y - d.y);
                if (dd > bestD) continue;
                AItile ai = room.aimap.getAItile(x, y);
                bool floor = ai != null && ai.acc == AItile.Accessibility.Floor;
                if (dd < bestD || (floor && !bestFloor))
                {
                    best = i;
                    bestD = dd;
                    bestFloor = floor;
                }
            }
            prSubAsked = d;
            prSubGen = prGenNoH;
            prSubOk = best >= 0;
            prSub = prSubOk ? new WorldCoordinate(roomIdx, best % prW, best / prW, -1) : d;
            sub = prSub;
            return prSubOk;
        }
    }
}
