using System;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal enum FoxSurface
    {
        Floor,
        Corridor,
        Pole,
        Wall,
    }

    public partial class Fox
    {
        internal bool PoleHeld { get { return poleHeld; } }
        private bool poleHeld;

        internal bool PoleOwnsGravity { get { return poleOwnsGravity; } }
        private bool poleOwnsGravity;

        private FoxSurface surface = FoxSurface.Floor;

        internal float PoleAxisX { get { return poleAxisX; } }
        private float poleAxisX;
        private IntVector2 poleTile;

        internal float PolePhase { get { return polePhase; } }
        private float polePhase;

        internal float PoleHeadSign { get { return poleHeadSign; } }
        private float poleHeadSign = 1f;

        private int poleRegrabTick = int.MinValue / 2;

        private struct PoleProbe
        {
            public bool AnyOn, AnchorAir, RearAir, FootDown;
            public bool Fresh, DestPole, StartPole, Ascending, Descending;
            public int Anchor;
            public IntVector2 Tile;
            public float Speed;
        }
        private PoleProbe poleProbe;

        internal bool IsVerticalPoleTile(IntVector2 t)
        {
            if (room == null) return false;
            Room.Tile tile = room.GetTile(t.x, t.y);
            return tile.verticalBeam && !tile.Solid;
        }

        internal bool IsAirPoleTile(IntVector2 t)
        {
            if (!IsVerticalPoleTile(t) || room.aimap == null) return false;
            AItile ai = room.aimap.getAItile(t.x, t.y);
            if (ai == null || ai.acc == AItile.Accessibility.Corridor) return false;
            Room.Tile below = room.GetTile(t.x, t.y - 1);
            if (below.Solid) return false;
            if (below.Terrain == Room.Tile.TerrainType.Floor) return false;
            if (below.Terrain == Room.Tile.TerrainType.Slope) return false;
            return true;
        }

        internal bool PoleTileBarsLeap(int x, int y)
        {
            FoxBreedParams p = P;
            if (p == null || !p.ClimbsPoles) return false;
            if (room == null || room.aimap == null) return false;
            AItile ai = room.aimap.getAItile(x, y);
            return ai != null && ai.acc == AItile.Accessibility.Climb;
        }

        private bool InThisRoom(WorldCoordinate c)
        {
            return room != null && c.TileDefined && c.room == room.abstractRoom.index;
        }

        private void UpdatePoleSurface(FoxBreedParams p)
        {
            if (p == null) return;
            if (!p.ClimbsPoles)
            {
                if (poleHeld || poleOwnsGravity)
                    ReleasePoleHold(p, "climbs_poles_off", !ReachActive && !climbOwnsGravity);
                return;
            }
            if (bodyChunks == null || bodyChunks.Length < 3 || room == null || room.aimap == null)
            {
                if (poleHeld || poleOwnsGravity) ReleasePoleHold(p, "no_body", true);
                return;
            }

            string why;
            bool forced = false;
            bool restore = true;
            bool want = false;
            poleProbe = default(PoleProbe);
            poleProbe.Anchor = -1;

            if (!Consious)
            {
                why = Stunned ? "stunned" : "unconscious";
                forced = true;
            }
            else if (p.PoleReleaseOnGrab && grabbedBy != null && grabbedBy.Count > 0)
            {
                why = "grabbed";
                forced = true;
            }
            else if (p.PoleReleaseOnBlind && Blinded)
            {
                why = "blinded";
                forced = true;
            }
            else if (climbOwnsGravity)
            {
                why = "corridor_owns";
                restore = false;
            }
            else if (ReachActive)
            {
                why = "reach_owns";
                restore = false;
            }
            else if (move != null && (move.Pounce.Running || move.Charge.Running || move.DriveOwner != null))
            {
                why = "action_owns";
            }
            else if (Swimming)
            {
                why = "swimming";
            }
            else
            {
                want = PoleGeometryWantsHold(p, out why);
            }

            if (want)
            {
                if (!poleHeld)
                {
                    GripPole(p, why);
                }
                else if (applyGravity)
                {
                    SetWeightApplied(p, false, "pole_hold");
                }
                poleOwnsGravity = true;
            }
            else if (poleHeld || poleOwnsGravity)
            {
                ReleasePoleHold(p, why, restore);
                if (forced) poleRegrabTick = GameTick + Math.Max(0, p.PoleRegrabTicks);
                if (why == "foot_on_ground") ArmFloorStepOffBlock(p);
            }

            UpdateFloorStepOffBlock(p);
            UpdateSurfaceState(p, why);
            CheckPoleTileIsNotCorridorLike(p);
        }

        private bool PoleGeometryWantsHold(FoxBreedParams p, out string why)
        {
            bool anchorIsAir = false;
            bool anchorOnHeldColumn = false;
            for (int i = 0; i < 3; i++)
            {
                IntVector2 t = room.GetTilePosition(bodyChunks[i].pos);
                if (!IsVerticalPoleTile(t)) continue;
                bool air = IsAirPoleTile(t);
                poleProbe.AnyOn = true;
                if (bodyChunks[i].ContactPoint.y < 0) poleProbe.FootDown = true;
                if (air)
                {
                    if (i < 2) poleProbe.AnchorAir = true;
                    else poleProbe.RearAir = true;
                }

                bool onHeld = poleHeld && t.x == poleTile.x;
                bool better = poleProbe.Anchor < 0
                              || (onHeld && !anchorOnHeldColumn)
                              || (!anchorOnHeldColumn && air && !anchorIsAir);
                if (better)
                {
                    poleProbe.Anchor = i;
                    poleProbe.Tile = t;
                    anchorIsAir = air;
                    anchorOnHeldColumn = onHeld;
                }
            }

            poleProbe.Fresh = HasFreshDriveConnection;
            if (poleProbe.Fresh)
            {
                MovementConnection c = lastFollowedConnection;
                bool destHere = InThisRoom(c.destinationCoord);
                bool startHere = InThisRoom(c.startCoord);
                poleProbe.DestPole = destHere && IsAirPoleTile(c.DestTile);
                poleProbe.StartPole = startHere && IsAirPoleTile(c.StartTile);
                poleProbe.Ascending = destHere && startHere && c.DestTile.y > c.StartTile.y;
                poleProbe.Descending = destHere && startHere && c.DestTile.y < c.StartTile.y;
            }
            poleProbe.Speed = bodyChunks[0].vel.magnitude;

            if (!poleProbe.AnyOn) { why = "off_pole"; return false; }

            bool hold = poleProbe.AnchorAir
                        || (poleProbe.RearAir && poleProbe.Fresh)
                        || poleProbe.DestPole;
            if (!hold) { why = "no_air_chunk"; return false; }
            if (poleProbe.FootDown && (poleProbe.Descending || !poleProbe.Fresh))
            {
                why = "foot_on_ground";
                return false;
            }

            if (!poleHeld)
            {
                bool intent = poleProbe.DestPole || poleProbe.StartPole;
                if (!intent && surface != FoxSurface.Corridor) { why = "no_intent"; return false; }
                if (GameTick < poleRegrabTick) { why = "regrab_block"; return false; }
                if (FloorStepOffBlocks(p)) { why = "floor_stepoff_block"; return false; }
                if (PoleMeetBlocksGrip(poleProbe.Tile.x)) { why = "meet_wait"; return false; }
                if (poleProbe.Speed > Mathf.Max(0f, p.PoleGrabMaxSpeed)) { why = "too_fast"; return false; }
                why = !intent ? "from_corridor" : (poleProbe.DestPole ? "step_onto_pole" : "step_along_pole");
            }
            else
            {
                why = poleProbe.AnchorAir ? "on_pole"
                    : (poleProbe.RearAir && poleProbe.Fresh) ? "rear_on_pole_travelling"
                    : "mounting";
            }

            poleTile = poleProbe.Tile;
            poleAxisX = room.MiddleOfTile(poleTile.x, poleTile.y).x;
            return true;
        }

        private void GripPole(FoxBreedParams p, string why)
        {
            poleHeld = true;
            float dy = bodyChunks[0].pos.y - bodyChunks[2].pos.y;
            if (Mathf.Abs(dy) > POLE_HEAD_SIGN_BAND) poleHeadSign = (dy < 0f) ? -1f : 1f;
            SetWeightApplied(p, false, "pole_grip");
            poleOwnsGravity = true;
        }

        private const float POLE_HEAD_SIGN_BAND = 8f;

        internal void ReleasePoleHold(FoxBreedParams p, string why, bool restoreWeight)
        {
            if (!poleHeld && !poleOwnsGravity) return;
            poleHeld = false;
            poleOwnsGravity = false;
            if (restoreWeight && p != null)
            {
                applyGravity = true;
                ApplyBodyPhysics(p);
            }
        }

        private void YieldPoleHoldToCorridor(FoxBreedParams p)
        {
            ReleasePoleHold(p, "corridor_wins", false);
        }

        private void PoleHit(FoxBreedParams p, float damage)
        {
            if (!poleHeld || p == null || !p.PoleReleaseOnHit || damage <= 0f) return;
            ReleasePoleHold(p, "hit", !ReachActive && !climbOwnsGravity);
            poleRegrabTick = GameTick + Math.Max(0, p.PoleRegrabTicks);
        }

        private void UpdateSurfaceState(FoxBreedParams p, string why)
        {
            FoxSurface now = poleHeld ? FoxSurface.Pole
                           : wallHeld ? FoxSurface.Wall
                           : (climbOwnsGravity || BodyInCorridorTerrain()) ? FoxSurface.Corridor
                           : FoxSurface.Floor;
            if (now == surface) return;
            surface = now;
        }

        private void CheckPoleTileIsNotCorridorLike(FoxBreedParams p)
        {
            if (!poleProbe.AnyOn || poleProbe.Anchor < 0) return;
            IntVector2 t = poleProbe.Tile;
            AItile ai = room.aimap.getAItile(t.x, t.y);
            if (ai == null || ai.acc != AItile.Accessibility.Climb) return;
            if (!TileIsCorridorLike(t)) return;
        }

        private void ApplyPoleHold(FoxBreedParams p)
        {
            if (!poleHeld || p == null || bodyChunks == null || bodyChunks.Length < 3 || room == null) return;

            bool leaving = HasFreshDriveConnection
                           && (!InThisRoom(lastFollowedConnection.destinationCoord)
                               || lastFollowedConnection.DestTile.x != poleTile.x);
            float reach = Mathf.Max(0f, p.PoleCenterReachPx);

            int stepDir = PoleStepDir();
            float bodyDy = bodyChunks[0].pos.y - bodyChunks[2].pos.y;
            float orient = PoleAlignOrient(p, stepDir, bodyDy);
            bool ascentHeadFirst = p.PoleAscentHeadFirst && stepDir > 0;
            for (int i = 0; i < 3; i++) { alignW[i] = 0f; alignBehind[i] = 0f; alignMode[i] = leaving ? "leaving" : "near"; }
            if (!leaving)
            {
                for (int i = 0; i < 3; i++)
                {
                    BodyChunk c = bodyChunks[i];
                    float off = c.pos.x - poleAxisX;
                    float fac = Mathf.Clamp01((i == 0) ? p.PoleCenterHeadFac
                                              : (i == 1) ? p.PoleCenterMidFac : p.PoleCenterRearFac);
                    float w = 1f;
                    if (i > 0)
                    {
                        float half = Mathf.Max(1f, 0.5f * PoleLinkRest(i - 1));
                        float behind = (bodyChunks[i - 1].pos.y - c.pos.y) * orient;
                        alignBehind[i] = behind;
                        if (p.PoleAlignOrder) w = Mathf.Clamp01(behind / half);

                        if (ascentHeadFirst && p.PoleAlignSwingPx > 0f && behind < -POLE_ALIGN_SWING_BAND)
                        {
                            float side = PoleSwingSide(i, off);
                            float link = 2f * half;
                            if (side != 0f && Mathf.Abs(off) < link)
                            {
                                c.vel.x += side * p.PoleAlignSwingPx * Mathf.Clamp01(-behind / half);
                                alignMode[i] = "swing";
                            }
                            else
                            {
                                alignMode[i] = (side == 0f) ? "swing_walled" : "swing_out";
                            }
                            continue;
                        }
                    }
                    alignW[i] = w;
                    if (w <= 0f) { alignMode[i] = "gated"; continue; }

                    bool far = Mathf.Abs(off) > reach;
                    if (far)
                    {
                        if (i == 0 || p.PoleAlignFarMaxPx <= 0f) { alignMode[i] = "far_skip"; continue; }
                        if (!PoleLineClear(c.pos)) { alignMode[i] = "far_blocked"; continue; }
                    }
                    float a = fac * w;
                    float v = c.vel.x * (1f - a) - off * a;
                    if (far)
                    {
                        v = Mathf.Clamp(v, -p.PoleAlignFarMaxPx, p.PoleAlignFarMaxPx);
                        alignMode[i] = "far";
                    }
                    else if (w < 1f)
                    {
                        alignMode[i] = "partial";
                    }
                    c.vel.x = v;
                }
            }

            float dy = bodyChunks[0].pos.y - bodyChunks[2].pos.y;
            if (Mathf.Abs(dy) > POLE_HEAD_SIGN_BAND) poleHeadSign = (dy < 0f) ? -1f : 1f;

            float travel = (bodyChunks[1].pos.y - bodyChunks[1].lastPos.y) * poleHeadSign;
            polePhase += travel / Mathf.Max(4f, p.PoleCyclePx);
            polePhase -= Mathf.Floor(polePhase);
        }

        private const float POLE_ALIGN_SWING_BAND = 4f;

        private readonly float[] alignW = new float[3];
        private readonly float[] alignBehind = new float[3];
        private readonly string[] alignMode = new string[3];

        internal int PoleStepDir()
        {
            if (!HasFreshDriveConnection) return 0;
            MovementConnection c = lastFollowedConnection;
            if (!InThisRoom(c.startCoord) || !InThisRoom(c.destinationCoord)) return 0;
            int dy = c.DestTile.y - c.StartTile.y;
            if (dy == 0) return 0;
            if (Math.Abs(c.DestTile.x - c.StartTile.x) > Math.Abs(dy)) return 0;
            return dy > 0 ? 1 : -1;
        }

        private float PoleAlignOrient(FoxBreedParams p, int stepDir, float bodyDy)
        {
            if (stepDir > 0 && p.PoleAscentHeadFirst) return 1f;
            if (Mathf.Abs(bodyDy) > POLE_HEAD_SIGN_BAND) return bodyDy < 0f ? -1f : 1f;
            if (stepDir < 0) return -1f;
            return poleHeadSign < 0f ? -1f : 1f;
        }

        private float PoleLinkRest(int link)
        {
            if (bodyChunkConnections != null && link >= 0 && link < bodyChunkConnections.Length
                && bodyChunkConnections[link] != null)
            {
                return bodyChunkConnections[link].distance;
            }
            return 17f;
        }

        private float PoleSwingSide(int i, float off)
        {
            float side = off > 0.5f ? 1f : off < -0.5f ? -1f : 0f;
            if (side == 0f)
            {
                float rearOff = bodyChunks[2].pos.x - poleAxisX;
                side = Mathf.Abs(rearOff) > 0.5f ? Mathf.Sign(rearOff) : 1f;
            }
            Vector2 pos = bodyChunks[i].pos;
            if (room.GetTile(room.GetTilePosition(pos + new Vector2(side * 10f, 0f))).Solid)
            {
                side = -side;
                if (room.GetTile(room.GetTilePosition(pos + new Vector2(side * 10f, 0f))).Solid) side = 0f;
            }
            return side;
        }

        private bool PoleLineClear(Vector2 pos)
        {
            float dx = poleAxisX - pos.x;
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(dx) / 8f));
            for (int k = 1; k <= n; k++)
            {
                Vector2 q = new Vector2(pos.x + dx * k / n, pos.y);
                if (room.GetTile(room.GetTilePosition(q)).Solid) return false;
            }
            return true;
        }

        private int poleFloorBlockCol;
        private int poleFloorBlockUntil = NO_FLOOR_BLOCK;
        private const int NO_FLOOR_BLOCK = int.MinValue;

        private void ArmFloorStepOffBlock(FoxBreedParams p)
        {
            if (p.PoleFloorRegrabTicks <= 0) return;
            if (PoleContinuesBelowFoot())
            {
                return;
            }
            poleFloorBlockCol = poleTile.x;
            poleFloorBlockUntil = GameTick + p.PoleFloorRegrabTicks;
        }

        private bool PoleContinuesBelowFoot()
        {
            if (room == null || bodyChunks == null) return false;
            for (int i = 0; i < bodyChunks.Length && i < 3; i++)
            {
                if (bodyChunks[i].ContactPoint.y >= 0) continue;
                IntVector2 t = room.GetTilePosition(bodyChunks[i].pos);
                if (!IsVerticalPoleTile(t)) continue;
                if (room.GetTile(t.x, t.y - 1).Solid) return false;
                return IsVerticalPoleTile(new IntVector2(t.x, t.y - 1))
                       || IsVerticalPoleTile(new IntVector2(t.x, t.y - 2));
            }
            return false;
        }

        private bool FloorStepOffBlocks(FoxBreedParams p)
        {
            if (poleFloorBlockUntil == NO_FLOOR_BLOCK) return false;
            if (poleProbe.Tile.x != poleFloorBlockCol) return false;
            if (poleProbe.Ascending && poleProbe.DestPole) return false;
            return true;
        }

        private void UpdateFloorStepOffBlock(FoxBreedParams p)
        {
            if (poleFloorBlockUntil == NO_FLOOR_BLOCK) return;
            string why = null;
            if (poleHeld) why = "gripped";
            else if (GameTick >= poleFloorBlockUntil) why = "expired";
            else if (!poleProbe.AnchorAir && !poleProbe.RearAir) why = "stepped_off";
            if (why == null) return;
            poleFloorBlockUntil = NO_FLOOR_BLOCK;
        }

        private float PolePulse(FoxBreedParams p)
        {
            if (!poleHeld || p.PolePullPulseFac <= 0f) return 1f;
            return 1f + Mathf.Clamp(p.PolePullPulseFac, 0f, 0.9f) * Mathf.Sin(polePhase * Mathf.PI * 2f);
        }

        private bool PoleBacking(FoxBreedParams p, Vector2 desired)
        {
            if (!poleHeld || !p.PoleBackDown) return false;
            if (Mathf.Abs(desired.y) <= Mathf.Abs(desired.x)) return false;
            if (p.PoleAscentHeadFirst && desired.y > 0f) return false;
            float dy = bodyChunks[0].pos.y - bodyChunks[2].pos.y;
            if (Mathf.Abs(dy) <= POLE_HEAD_SIGN_BAND) return false;
            return desired.y * dy < 0f;
        }

        private bool PoleStep(FoxBreedParams p, MovementConnection conn)
        {
            if (!p.ClimbsPoles) return false;
            if (poleHeld) return true;
            if (conn == default(MovementConnection)) return false;
            return (InThisRoom(conn.destinationCoord) && IsAirPoleTile(conn.DestTile))
                   || (InThisRoom(conn.startCoord) && IsAirPoleTile(conn.StartTile));
        }

        internal float PoleAmount
        {
            get
            {
                FoxBreedParams p = P;
                if (p == null || !p.ClimbsPoles) return 0f;
                int now = GameTick;
                if (now != poleAmountTick)
                {
                    poleAmountTick = now;
                    float target = poleHeld ? 1f : 0f;
                    float rate = Mathf.Clamp(p.PoleRideLerp, 0.01f, 1f);
                    poleAmount = (rate >= 1f) ? target : Mathf.Lerp(poleAmount, target, rate);
                    if (Mathf.Abs(poleAmount - target) < 0.001f) poleAmount = target;
                }
                return Mathf.Clamp01(poleAmount);
            }
        }
        private float poleAmount;
        private int poleAmountTick = int.MinValue;
    }
}
