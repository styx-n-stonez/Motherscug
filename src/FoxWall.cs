using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal enum FoxWallTile
    {
        NotWall,
        Face,
        Overhang,
        Wallbehind,
    }

    internal sealed class FoxWallAccess : IAccessibilityModifier
    {
        private readonly Room room;

        private static CreatureTemplate.Type[] climberTypes = new CreatureTemplate.Type[0];
        private static string[] climberVariants = new string[0];

        private FoxWallAccess(Room room)
        {
            this.room = room;
        }

        internal static void Install()
        {
            On.Room.ctor += Room_ctor;
        }

        internal static void Register(CreatureTemplate.Type type, string variantId, bool wallAllowed)
        {
            var types = new List<CreatureTemplate.Type>(climberTypes);
            var ids = new List<string>(climberVariants);
            int at = types.IndexOf(type);
            if (at >= 0) { types.RemoveAt(at); ids.RemoveAt(at); }
            if (wallAllowed) { types.Add(type); ids.Add(variantId); }
            climberTypes = types.ToArray();
            climberVariants = ids.ToArray();
        }

        private static void Room_ctor(On.Room.orig_ctor orig, Room self, RainWorldGame game, World world,
                                      AbstractRoom abstractRoom, bool devUI)
        {
            orig(self, game, world, abstractRoom, devUI);
            if (self != null && self.accessModifiers != null) self.accessModifiers.Add(new FoxWallAccess(self));
        }

        private static string VariantFor(CreatureTemplate crit)
        {
            CreatureTemplate.Type[] types = climberTypes;
            if (types.Length == 0 || crit == null) return null;
            for (int i = 0; i < types.Length; i++)
            {
                if (crit.type == types[i]) return climberVariants[i];
            }
            return null;
        }

        public bool IsTileAccessible(IntVector2 tile, CreatureTemplate crit)
        {
            if (climberTypes.Length == 0) return true;
            string variant = VariantFor(crit);
            if (variant == null) return true;
            AImap map = room.aimap;
            if (map == null) return true;
            AItile ai = map.getAItile(tile.x, tile.y);
            if (ai == null || ai.acc != AItile.Accessibility.Wall) return true;
            if (ai.AnyWater) return true;
            if (ModManager.MMF && room.gravity <= Fox.ZeroGravityThreshold) return true;
            int side;
            FoxWallTile cls = Fox.ClassifyWallTile(room, tile.x, tile.y, out side);
            if (cls == FoxWallTile.Face) return true;
            if (cls == FoxWallTile.Overhang) return FoxParams.For(variant).WallOverhangAllowed;
            return false;
        }
    }

    public partial class Fox
    {
        internal static bool LegacyClimberRule(FoxBreedParams p)
        {
            return p != null && p.ClimbsWalls && p.ClimbLegacyAnyTileFooting;
        }

        internal static float ZeroGravityThreshold { get { return ZERO_GRAVITY_MOVEMENT_THRESHOLD; } }

        internal bool WallHeld { get { return wallHeld; } }
        private bool wallHeld;

        internal bool WallOwnsGravity { get { return wallOwnsGravity; } }
        private bool wallOwnsGravity;

        internal float WallFaceSide { get { return wallFaceSide; } }
        private float wallFaceSide = 1f;

        internal float WallFaceX { get { return wallFaceX; } }
        private float wallFaceX;
        private int wallFaceCol;
        private IntVector2 wallTile;

        internal float WallPhase { get { return wallPhase; } }
        private float wallPhase;

        internal float WallHeadSign { get { return wallHeadSign; } }
        private float wallHeadSign = 1f;

        private int wallRegrabTick = int.MinValue / 2;

        private struct WallProbe
        {
            public bool AnyOn, AnchorFace, RearFace, FootDown, Near;
            public bool Fresh, DestFace, StartFace, Ascending, Descending, Supported;
            public int Anchor, Side;
            public IntVector2 Tile;
            public float Speed;
            public string Refused;
        }
        private WallProbe wallProbe;

        internal static FoxWallTile ClassifyWallTile(Room room, int x, int y, out int side)
        {
            side = 0;
            if (room == null || room.aimap == null) return FoxWallTile.NotWall;
            AItile ai = room.aimap.getAItile(x, y);
            if (ai == null || ai.acc != AItile.Accessibility.Wall) return FoxWallTile.NotWall;
            bool left = room.GetTile(x - 1, y).Solid;
            bool right = room.GetTile(x + 1, y).Solid;
            if (left || right)
            {
                side = right ? 1 : -1;
                return FoxWallTile.Face;
            }
            if (!room.GetTile(x, y + 1).Solid)
            {
                bool upRight = room.GetTile(x + 1, y + 1).Solid;
                if (upRight || room.GetTile(x - 1, y + 1).Solid)
                {
                    side = upRight ? 1 : -1;
                    return FoxWallTile.Overhang;
                }
            }
            return FoxWallTile.Wallbehind;
        }

        internal bool IsHoldableWallTile(IntVector2 t, FoxBreedParams p, out int side)
        {
            FoxWallTile cls = ClassifyWallTile(room, t.x, t.y, out side);
            if (cls == FoxWallTile.Face) return true;
            if (cls == FoxWallTile.Overhang && p != null && p.WallOverhangAllowed) return true;
            side = 0;
            return false;
        }

        private string WallRefusalToken(IntVector2 t, FoxBreedParams p)
        {
            int side;
            FoxWallTile cls = ClassifyWallTile(room, t.x, t.y, out side);
            if (cls == FoxWallTile.Wallbehind) return "wallbehind";
            if (cls == FoxWallTile.Overhang && !p.WallOverhangAllowed) return "overhang";
            if (cls == FoxWallTile.NotWall)
            {
                AItile ai = room.aimap.getAItile(t.x, t.y);
                if (ai != null && ai.acc == AItile.Accessibility.Ceiling) return "ceiling";
            }
            return null;
        }

        internal bool WallTileBarsLeap(int x, int y)
        {
            FoxBreedParams p = P;
            if (p == null || !p.ClimbsWalls) return false;
            if (room == null || room.aimap == null) return false;
            AItile ai = room.aimap.getAItile(x, y);
            return ai != null && ai.acc == AItile.Accessibility.Wall && !ai.AnyWater;
        }

        internal bool ClimbTileBarsLeap(int x, int y)
        {
            return PoleTileBarsLeap(x, y) || WallTileBarsLeap(x, y);
        }

        internal bool WallBarsLaunch
        {
            get
            {
                if (!wallHeld) return false;
                FoxBreedParams p = P;
                return p == null || !p.WallLaunchAllowed;
            }
        }

        internal bool WallDestinationRefused(WorldCoordinate d)
        {
            FoxBreedParams p = P;
            if (p == null || !p.ClimbsWalls || p.WallWanderDest) return false;
            if (!InThisRoom(d)) return false;
            int side;
            if (!IsHoldableWallTile(d.Tile, p, out side)) return false;
            return true;
        }

        private bool WallStep(FoxBreedParams p, MovementConnection conn)
        {
            if (!p.ClimbsWalls) return false;
            if (wallHeld) return true;
            if (conn == default(MovementConnection)) return false;
            int side;
            return (InThisRoom(conn.destinationCoord) && IsHoldableWallTile(conn.DestTile, p, out side))
                   || (InThisRoom(conn.startCoord) && IsHoldableWallTile(conn.StartTile, p, out side));
        }

        private void UpdateWallSurface(FoxBreedParams p, bool poleBefore, FoxSurface surfaceBefore)
        {
            if (p == null) return;
            if (!p.ClimbsWalls)
            {
                if (wallHeld || wallOwnsGravity)
                    ReleaseWallHold(p, "climbs_walls_off", !ReachActive && !climbOwnsGravity && !poleOwnsGravity);
                return;
            }
            if (bodyChunks == null || bodyChunks.Length < 3 || room == null || room.aimap == null)
            {
                if (wallHeld || wallOwnsGravity) ReleaseWallHold(p, "no_body", true);
                return;
            }

            string why;
            bool forced = false;
            bool restore = true;
            bool want = false;
            wallProbe = default(WallProbe);
            wallProbe.Anchor = -1;

            if (!Consious)
            {
                why = Stunned ? "stunned" : "unconscious";
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
            else if (poleHeld || poleOwnsGravity)
            {
                why = "pole_owns";
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
                want = WallGeometryWantsHold(p, poleBefore, surfaceBefore, out why);
            }

            if (want)
            {
                if (!wallHeld)
                {
                    GripWall(p, why);
                }
                else if (applyGravity)
                {
                    SetWeightApplied(p, false, "wall_hold");
                }
                wallOwnsGravity = true;
            }
            else if (wallHeld || wallOwnsGravity)
            {
                ReleaseWallHold(p, why, restore);
                if (forced) wallRegrabTick = GameTick + Math.Max(0, p.WallRegrabTicks);
            }

            UpdateSurfaceState(p, why);
            CheckWallTileIsNotCorridorLike(p);
        }

        private bool WallGeometryWantsHold(FoxBreedParams p, bool poleBefore, FoxSurface surfaceBefore,
                                           out string why)
        {
            bool anchorOnHeld = false;
            for (int i = 0; i < 3; i++)
            {
                IntVector2 t = room.GetTilePosition(bodyChunks[i].pos);
                int side;
                if (!IsHoldableWallTile(t, p, out side))
                {
                    if (wallProbe.Refused == null && i < 2)
                    {
                        string tok = WallRefusalToken(t, p);
                        if (tok != null) { wallProbe.Refused = tok; }
                    }
                    continue;
                }
                wallProbe.AnyOn = true;
                if (i < 2) wallProbe.AnchorFace = true;
                else wallProbe.RearFace = true;
                bool onHeld = wallHeld && t.x == wallFaceCol && side == (int)wallFaceSide;
                if (wallProbe.Anchor < 0 || (onHeld && !anchorOnHeld))
                {
                    wallProbe.Anchor = i;
                    wallProbe.Tile = t;
                    wallProbe.Side = side;
                    anchorOnHeld = onHeld;
                }
            }

            wallProbe.Fresh = HasFreshDriveConnection;
            MovementConnection c = lastFollowedConnection;
            int destSide = 0;
            if (wallProbe.Fresh)
            {
                bool destHere = InThisRoom(c.destinationCoord);
                bool startHere = InThisRoom(c.startCoord);
                int sSide;
                wallProbe.DestFace = destHere && IsHoldableWallTile(c.DestTile, p, out destSide);
                wallProbe.StartFace = startHere && IsHoldableWallTile(c.StartTile, p, out sSide);
                wallProbe.Ascending = destHere && startHere && c.DestTile.y > c.StartTile.y;
                wallProbe.Descending = destHere && startHere && c.DestTile.y < c.StartTile.y;
                if (wallProbe.DestFace)
                {
                    for (int i = 0; i < 3 && !wallProbe.Near; i++)
                    {
                        IntVector2 t = room.GetTilePosition(bodyChunks[i].pos);
                        if (Math.Abs(t.x - c.DestTile.x) + Math.Abs(t.y - c.DestTile.y) <= 1) wallProbe.Near = true;
                    }
                }
            }
            wallProbe.Speed = bodyChunks[0].vel.magnitude;

            if (wallProbe.Anchor < 0 && wallProbe.DestFace && wallProbe.Near)
            {
                wallProbe.Tile = c.DestTile;
                wallProbe.Side = destSide;
            }

            if (!wallProbe.AnyOn && !(wallProbe.DestFace && wallProbe.Near))
            {
                why = wallProbe.Refused ?? "off_wall";
                return false;
            }

            int col = wallProbe.Tile.x, fs = wallProbe.Side;
            if (fs != 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (bodyChunks[i].ContactPoint.y >= 0) continue;
                    IntVector2 t = room.GetTilePosition(bodyChunks[i].pos);
                    if (t.x == col && room.GetTile(t.x + fs, t.y).Solid) { wallProbe.FootDown = true; break; }
                }
            }

            bool hold = wallProbe.AnchorFace
                        || (wallProbe.RearFace && wallProbe.Fresh)
                        || (wallProbe.DestFace && (wallProbe.Near || wallProbe.AnyOn));
            if (!hold) { why = "no_anchor"; return false; }
            if (wallProbe.FootDown && (wallProbe.Descending || !wallProbe.Fresh))
            {
                why = "foot_on_ground";
                return false;
            }

            if (!wallHeld)
            {
                bool intent = wallProbe.DestFace || (wallProbe.StartFace && !wallProbe.Descending);
                if (!intent) { why = "no_intent"; return false; }
                bool fromPole = poleBefore || surfaceBefore == FoxSurface.Pole;
                bool fromCorridor = surfaceBefore == FoxSurface.Corridor;
                wallProbe.Supported = Grounded || fromPole || fromCorridor;
                if (!wallProbe.Supported) { why = "airborne"; return false; }
                if (GameTick < wallRegrabTick) { why = "regrab_block"; return false; }
                if (wallProbe.Speed > Mathf.Max(0f, p.WallGrabMaxSpeed)) { why = "too_fast"; return false; }
                why = fromPole ? "from_pole"
                    : (fromCorridor && !Grounded) ? "from_corridor"
                    : wallProbe.DestFace ? "step_onto_wall" : "step_along_wall";
            }
            else
            {
                why = wallProbe.AnchorFace ? "on_wall"
                    : (wallProbe.RearFace && wallProbe.Fresh) ? "rear_on_wall_travelling"
                    : "mounting";
            }

            wallTile = wallProbe.Tile;
            wallFaceCol = wallProbe.Tile.x;
            if (wallProbe.Side != 0) wallFaceSide = wallProbe.Side;
            wallFaceX = room.MiddleOfTile(wallTile.x, wallTile.y).x + wallFaceSide * 10f;
            return true;
        }

        private void GripWall(FoxBreedParams p, string why)
        {
            wallHeld = true;
            float dy = bodyChunks[0].pos.y - bodyChunks[2].pos.y;
            if (Mathf.Abs(dy) > POLE_HEAD_SIGN_BAND) wallHeadSign = (dy < 0f) ? -1f : 1f;
            SetWeightApplied(p, false, "wall_grip");
            wallOwnsGravity = true;
        }

        internal void ReleaseWallHold(FoxBreedParams p, string why, bool restoreWeight)
        {
            if (!wallHeld && !wallOwnsGravity) return;
            wallHeld = false;
            wallOwnsGravity = false;
            if (restoreWeight && p != null)
            {
                applyGravity = true;
                ApplyBodyPhysics(p);
            }
        }

        private void YieldWallHoldToCorridor(FoxBreedParams p)
        {
            ReleaseWallHold(p, "corridor_wins", false);
        }

        private void CheckWallTileIsNotCorridorLike(FoxBreedParams p)
        {
            if (!wallHeld) return;
            IntVector2 t = wallTile;
            if (!TileIsCorridorLike(t)) return;
        }

        private void ApplyWallHold(FoxBreedParams p)
        {
            if (!wallHeld || p == null || bodyChunks == null || bodyChunks.Length < 3 || room == null) return;

            bool leaving = HasFreshDriveConnection
                           && (!InThisRoom(lastFollowedConnection.destinationCoord)
                               || lastFollowedConnection.DestTile.x != wallFaceCol);
            int side = wallFaceSide < 0f ? -1 : 1;
            float a = Mathf.Clamp01(p.WallCenterFac);
            for (int i = 0; i < 3; i++)
            {
                if (leaving || a <= 0f) continue;
                BodyChunk ch = bodyChunks[i];
                IntVector2 t = room.GetTilePosition(ch.pos);
                if (t.x != wallFaceCol || !room.GetTile(t.x + side, t.y).Solid) continue;
                float line = wallFaceX - side * (ch.rad + Mathf.Max(0f, p.WallStandoffPx));
                float off = ch.pos.x - line;
                ch.vel.x = ch.vel.x * (1f - a) - off * a;
            }

            float dy = bodyChunks[0].pos.y - bodyChunks[2].pos.y;
            if (Mathf.Abs(dy) > POLE_HEAD_SIGN_BAND) wallHeadSign = (dy < 0f) ? -1f : 1f;

            float travel = (bodyChunks[1].pos.y - bodyChunks[1].lastPos.y) * wallHeadSign;
            wallPhase += travel / Mathf.Max(4f, p.PoleCyclePx);
            wallPhase -= Mathf.Floor(wallPhase);
        }

        internal float WallAmount
        {
            get
            {
                FoxBreedParams p = P;
                if (p == null || !p.ClimbsWalls) return 0f;
                int now = GameTick;
                if (now != wallAmountTick)
                {
                    wallAmountTick = now;
                    float target = wallHeld ? 1f : 0f;
                    float rate = Mathf.Clamp(p.WallPoseLerp, 0.01f, 1f);
                    wallAmount = (rate >= 1f) ? target : Mathf.Lerp(wallAmount, target, rate);
                    if (Mathf.Abs(wallAmount - target) < 0.001f) wallAmount = target;
                }
                return Mathf.Clamp01(wallAmount);
            }
        }
        private float wallAmount;
        private int wallAmountTick = int.MinValue;
    }
}
