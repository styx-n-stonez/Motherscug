using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal sealed class FoxReachAction : FoxMoveAction
    {
        internal FoxReachAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "reach"; } }

        internal override FoxBodyClaim Claims
        {
            get { return FoxBodyClaim.Gravity | FoxBodyClaim.Drive; }
        }

        internal override bool CooldownTicksLate { get { return true; } }

        protected override string OrphanReason { get { return "orphaned_suspension"; } }

        private bool gravitySuspended;
        private Vector2 target;
        private Vector2 start;
        private MovementConnection.MovementType type;
        private int lastLogTick;
        private string lastRefusal;

        internal Vector2 Target { get { return target; } }

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            if (fox.RoomRef == null || fox.Chunks == null || fox.Chunks.Length < 3)
                return FoxMoveBid.No("no_body", "");

            string refuse = null;
            if (!p.ReachEnabled) refuse = "reach_disabled";
            else if (Cooldown > 0) refuse = "reach_cooldown";
            else if (!s.Conn.destinationCoord.TileDefined) refuse = "dest_undefined";
            else if (fox.WallBarsLaunch) refuse = "wall_hold";

            if (refuse != null)
            {
                if (refuse != lastRefusal
                    || fox.MoveTick - lastLogTick >= Math.Max(1, p.ReachLogTicks) * 8)
                {
                    lastRefusal = refuse;
                    lastLogTick = fox.MoveTick;
                }
                string m = null;
                return FoxMoveBid.No(refuse, m);
            }
            lastRefusal = null;

            string mOk = null;
            return FoxMoveBid.Yes("reach_connection", mOk);
        }

        protected override int BudgetFor(FoxBreedParams p, FoxMoveSituation s)
        {
            int dx = s.Conn.destinationCoord.x - s.Conn.startCoord.x;
            int dy = s.Conn.destinationCoord.y - s.Conn.startCoord.y;
            int tiles = Math.Max(1, Math.Max(Math.Abs(dx), Math.Abs(dy)));
            return Math.Max(1, Math.Min(tiles * Math.Max(1, p.ReachTicksPerTile),
                                        Math.Max(1, p.ReachMaxTicks)));
        }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            type = s.Conn.type;
            start = fox.mainBodyChunk.pos;
            target = fox.RoomRef.MiddleOfTile(s.Conn.DestTile);
            lastLogTick = fox.MoveTick;

            if (p.ReachGravityOff && !gravitySuspended)
            {
                gravitySuspended = true;
                fox.SetWeightApplied(p, false, "reach_begin");
            }
        }

        protected override string ShouldAbort(FoxBreedParams p)
        {
            if (fox.Chunks == null || fox.Chunks.Length < 3 || fox.RoomRef == null) return "no_body";
            if (fox.dead) return "died";
            if (fox.Stunned) return "stunned";
            if (fox.EngineOwnsBody) return "engine_owns";
            return null;
        }

        protected override string IsComplete(FoxBreedParams p)
        {
            return Custom.DistLess(fox.Chunks[0].pos, target, Mathf.Max(1f, p.ReachArrivePx))
                   ? "arrived" : null;
        }

        protected override void OnTick(FoxBreedParams p)
        {
            BodyChunk[] c = fox.Chunks;
            float top = Mathf.Max(0.1f, p.ReachSpeed);
            float accel = Mathf.Max(0.01f, p.Acceleration * Mathf.Max(0.01f, p.ReachAccelFac));
            Fox.Drive(c[0], Fox.Dir(c[0].pos, target) * top, accel);
            Fox.Drive(c[1], Fox.Dir(c[1].pos, c[0].pos) * top * p.DriveMidSpeedFac,
                      accel * p.DriveMidAccelFac);
            Fox.Drive(c[2], Fox.Dir(c[2].pos, c[1].pos) * top * p.DriveRearSpeedFac,
                      accel * p.DriveRearAccelFac);

            int every = Math.Max(1, p.ReachLogTicks);
            if (fox.MoveTick - lastLogTick >= every)
            {
                lastLogTick = fox.MoveTick;
            }
        }

        protected override bool LeakedClaim(FoxBreedParams p) { return gravitySuspended; }

        protected override int CooldownOnEnd(FoxBreedParams p, string reason, bool handover)
        {
            return Math.Max(0, p.ReachCooldown);
        }

        protected override bool OnEnd(FoxBreedParams p, string reason, int ran, bool wasRunning, bool handover)
        {
            bool restored = false;
            if (gravitySuspended)
            {
                gravitySuspended = false;
                fox.SetWeightApplied(p, true, "reach_end");
                restored = true;
            }

            if (!wasRunning) return restored;

            return true;
        }

        protected override int HeartbeatTicks(FoxBreedParams p) { return p.MoveActionLogTicks; }

        protected override string Detail(FoxBreedParams p)
        {
            return null;
        }
    }

    internal sealed class FoxStepUpAction : FoxMoveAction
    {
        internal FoxStepUpAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "step_up"; } }
        internal override bool Persistent { get { return false; } }

        private int suppressed;
        private string lastReason;

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            if (fox.pounce != Fox.PounceState.None)
            {
                string m = null;
                return FoxMoveBid.No("pouncing", m);
            }
            if (!fox.Grounded || Cooldown != 0 || fox.mainBodyChunk.vel.y >= p.StepUpVelYGate)
            {
                string m = null;
                return FoxMoveBid.No(!fox.Grounded ? "airborne"
                                     : (Cooldown != 0 ? "hop_cooldown" : "vel_y_gate"), m);
            }

            float g0 = (fox.RoomRef != null) ? Mathf.Max(0.05f, fox.gravity) : 0.9f;
            float hopClears = (p.StepUpClearPx > 0f)
                ? p.StepUpClearPx
                : (p.StepUpBoost * p.StepUpBoost / (2f * g0));
            float wantRise = Mathf.Max(0f, s.ToTarget.y);
            bool ballisticallyShort = wantRise > hopClears * Mathf.Max(0.01f, p.StepUpClearTolerance);
            bool insideLeapBand = p.LedgePounceEnabled && p.PounceEnabled
                                  && FoxLedgePounceAction.RefuseRiseBand(p, wantRise) != "below_leap_band";
            string measure = null;
            if (ballisticallyShort || insideLeapBand)
            {
                string why = ballisticallyShort ? "hop_cannot_clear" : "leap_band_owns_rise";
                return FoxMoveBid.No(why, measure);
            }

            return FoxMoveBid.Yes("hop_sized_for_rise", measure);
        }

        internal static float HopImpulse(FoxBreedParams p, float g)
        {
            return (p.StepUpClearPx > 0f)
                ? Mathf.Sqrt(2f * g * p.StepUpClearPx)
                : p.StepUpBoost;
        }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            float g = (fox.RoomRef != null) ? Mathf.Max(0.05f, fox.gravity) : 0.9f;
            float impulse = HopImpulse(p, g);
            BodyChunk[] c = fox.Chunks;
            for (int i = 0; i < c.Length; i++) c[i].vel.y = Mathf.Max(c[i].vel.y, impulse);

            bool fromConn = s.Conn.destinationCoord.y > s.Conn.startCoord.y;
            string reason = fromConn ? "conn" : "toTarget";
            bool changed = reason != lastReason;
            if (!changed && suppressed < 40) { suppressed++; return; }
            suppressed = 0;
            lastReason = reason;
        }

        protected override int CooldownOnEnd(FoxBreedParams p, string reason, bool handover)
        {
            return p.StepUpCooldown;
        }
    }

    internal static class FoxLandingSurface
    {
        internal const int None = 0;
        internal const int Rock = 1;
        internal const int Platform = 2;
        internal const int Ramp = 4;
        internal const int All = Rock | Platform | Ramp;

        internal const string Default = "solid+platform+ramp";

        internal static int MaskOf(string cfg, out string bad)
        {
            bad = null;
            if (string.IsNullOrEmpty(cfg)) { bad = "(empty)"; return None; }
            int mask = None;
            bool recognised = false;
            string[] parts = cfg.ToLowerInvariant().Split('+', ',', ' ');
            for (int i = 0; i < parts.Length; i++)
            {
                string w = parts[i].Trim();
                if (w.Length == 0) continue;
                if (w == "solid" || w == "rock") mask |= Rock;
                else if (w == "platform" || w == "floor") mask |= Platform;
                else if (w == "ramp" || w == "slope") mask |= Ramp;
                else if (w == "all") mask |= All;
                else if (w == "none") { }
                else { bad = w; return None; }
                recognised = true;
            }
            if (!recognised) { bad = "(no_surface_words)"; return None; }
            return mask;
        }

        internal static string Name(int mask)
        {
            if (mask == None) return "none";
            string s = "";
            if ((mask & Rock) != 0) s += "solid";
            if ((mask & Platform) != 0) s += (s.Length > 0 ? "+" : "") + "platform";
            if ((mask & Ramp) != 0) s += (s.Length > 0 ? "+" : "") + "ramp";
            return s;
        }

        internal static bool At(Room room, int col, int row, int sign, int mask,
                                out float surfaceY, out string kind)
        {
            surfaceY = 0f;
            kind = "none";
            if (room == null) return false;
            Room.Tile.TerrainType t = room.GetTile(col, row).Terrain;
            if (t == Room.Tile.TerrainType.Air || t == Room.Tile.TerrainType.ShortcutEntrance)
                return false;

            Vector2 mid = room.MiddleOfTile(col, row);
            if (t == Room.Tile.TerrainType.Solid)
            {
                if ((mask & Rock) == 0) return false;
                surfaceY = mid.y + 10f;
                kind = "rock";
                return true;
            }
            if (t == Room.Tile.TerrainType.Floor)
            {
                if ((mask & Platform) == 0) return false;
                surfaceY = mid.y + 10f;
                kind = "platform";
                return true;
            }
            if (t == Room.Tile.TerrainType.Slope)
            {
                if ((mask & Ramp) == 0) return false;
                int solidSide;
                Room.SlopeDirection d = room.IdentifySlope(col, row);
                if (d == Room.SlopeDirection.UpLeft) solidSide = 1;
                else if (d == Room.SlopeDirection.UpRight) solidSide = -1;
                else return false;
                surfaceY = (-sign == solidSide) ? mid.y + 10f : mid.y - 10f;
                kind = "ramp";
                return true;
            }
            return false;
        }

        internal static string EntryFaceOf(Room room, int col, int row, Vector2 from, Vector2 to)
        {
            if (room == null) return "side";
            float top = room.MiddleOfTile(col, row).y + 10f;
            if (to.y > from.y) return "below";
            if (from.y >= top) return "above";
            return "side";
        }

        internal static bool OpenForBody(Room room, int col, int row)
        {
            if (room == null) return true;
            if (col < 0 || col >= room.TileWidth) return false;
            if (row < 0) return false;
            if (row >= room.TileHeight) return true;
            return room.GetTile(col, row).Terrain != Room.Tile.TerrainType.Solid;
        }
    }

    internal struct FoxLipMeasure
    {
        public bool Found;
        public string Why;
        public float EdgeX;
        public float TopY;
        public float FootY;
        public float Rise;
        public float Gap;
        public bool LandingClear;
        public int Sign;

        public string LipSurface;
        public int VoidCols;

        public string Describe()
        {
            return null;
        }
    }

    internal static class FoxAscentSeam
    {
        internal static FoxAscentPlan UsablePlan(Fox fox, string caller)
        {
            if (fox == null) return null;
            if (fox.AI == null) { return null; }

            FoxAscentPlan plan;
            try
            {
                plan = fox.AI.AscentPlan;
            }
            catch (Exception)
            {
                return null;
            }
            if (plan == null) { return null; }

            string token = plan.UsabilityToken(fox.AI.AscentPathGeneration, fox.MoveTick);
            bool ok = token == "usable";
            return ok ? plan : null;
        }
    }

    internal sealed class FoxLedgePounceAction : FoxMoveAction
    {
        internal FoxLedgePounceAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "ledge_pounce"; } }
        internal override bool Persistent { get { return false; } }

        private Vector2 aim;

        private int probeTick = -1000000;
        private int probeSign;
        private FoxLipMeasure probeCache;

        private int wallPressSince = -1;

        private struct Solved
        {
            public FoxLipMeasure Lip;
            public Vector2 Aim;
            public float Rise;
            public float WantDist;
            public float LaunchDeg;
            public string Refuse;
            public bool FromPlan;
            public string Kind;

            public FoxReachVerdict Reach;
        }

        private static Solved Unsolved()
        {
            return new Solved { Refuse = null, FromPlan = false, Kind = "none",
                                Reach = FoxReachVerdict.NotEvaluated() };
        }

        private int HeadingSign(FoxMoveSituation s, FoxAscentPlan plan)
        {
            BodyChunk[] c = fox.Chunks;
            if (c == null || c.Length < 3) return 0;
            if (plan != null && plan.Kind == FoxAscentKind.Pounce)
            {
                float dxPlan = plan.AimAt.x - c[0].pos.x;
                if (Mathf.Abs(dxPlan) > 0.01f) return dxPlan >= 0f ? 1 : -1;
            }
            float dx = s.ToTarget.x;
            if (Mathf.Abs(dx) < 0.01f) dx = s.Desired.x;
            if (Mathf.Abs(dx) < 0.01f) dx = Fox.Dir(c[2].pos, c[0].pos).x;
            if (Mathf.Abs(dx) < 0.01f) return 0;
            return dx >= 0f ? 1 : -1;
        }

        private float FootLine()
        {
            BodyChunk[] c = fox.Chunks;
            if (c == null || c.Length == 0) return fox.mainBodyChunk.pos.y;
            float y = float.MaxValue;
            for (int i = 0; i < c.Length; i++) y = Mathf.Min(y, c[i].pos.y - c[i].rad);
            return y;
        }

        internal FoxLipMeasure ProbeLip(FoxBreedParams p, int sign)
        {
            if (probeTick == fox.MoveTick && probeSign == sign) return probeCache;
            probeTick = fox.MoveTick;
            probeSign = sign;
            probeCache = ProbeLipUncached(p, sign);
            return probeCache;
        }

        private FoxLipMeasure ProbeLipUncached(FoxBreedParams p, int sign)
        {
            var m = new FoxLipMeasure { Found = false, Why = "none", Sign = sign };
            Room room = fox.RoomRef;
            BodyChunk[] c = fox.Chunks;
            if (room == null || c == null || c.Length < 3 || sign == 0) { m.Why = "no_room"; return m; }

            m.FootY = FootLine();
            IntVector2 foot = room.GetTilePosition(new Vector2(c[0].pos.x, m.FootY + 1f));
            int scan = Math.Max(1, p.LedgePounceLipScanEffective);
            int band = Math.Max(1, Mathf.CeilToInt(Mathf.Max(20f, p.LedgePounceMaxRise) / 20f));
            int downRows = Math.Max(0, p.LedgePounceDescendRows);

            int mask = p.LedgePounceLipSurfaceMask;

            FoxLipMeasure best = m;
            bool haveAny = false;
            int voidRun = 0;

            for (int d = 1; d <= scan; d++)
            {
                FoxLipMeasure cand;
                if (!MeasureColumn(room, c, foot.x + sign * d, d, foot.y, band, downRows, sign, mask,
                                   m, out cand))
                {
                    voidRun++;
                    continue;
                }
                cand.VoidCols = voidRun;
                voidRun = 0;

                string refuse = RefuseLip(p, cand);
                if (refuse == null)
                {
                    return cand;
                }
                if (!haveAny) { best = cand; haveAny = true; }

                bool canWalkPast = refuse == "below_leap_band" || refuse == "platform_overhang"
                                   || refuse == "overhang_not_a_step" || refuse == "gap_too_small"
                                   || refuse == "drop_too_far";
                if (!canWalkPast)
                {
                    best = cand;
                    haveAny = true;
                    break;
                }
            }

            if (haveAny)
            {
                return best;
            }
            m.Why = "no_face";
            return m;
        }

        private static bool MeasureColumn(Room room, BodyChunk[] c, int col, int d, int footRow,
                                          int band, int downRows, int sign, int mask,
                                          FoxLipMeasure baseM, out FoxLipMeasure cand)
        {
            cand = baseM;
            float surfY;
            string surfKind;

            int faceRow = int.MinValue;
            float topY = 0f;
            string lipKind = "none";
            for (int r = footRow; r <= footRow + band; r++)
            {
                if (FoxLandingSurface.At(room, col, r, sign, mask, out surfY, out surfKind))
                {
                    faceRow = r;
                    topY = surfY;
                    lipKind = surfKind;
                    break;
                }
            }

            if (faceRow == int.MinValue && downRows > 0)
            {
                for (int r = footRow - 1; r >= footRow - downRows; r--)
                {
                    if (r < 0) break;
                    if (FoxLandingSurface.At(room, col, r, sign, mask, out surfY, out surfKind))
                    {
                        faceRow = r;
                        topY = surfY;
                        lipKind = surfKind;
                        break;
                    }
                }
            }

            if (faceRow == int.MinValue) return false;

            int lipRow = faceRow;
            int up = 0;
            while (up <= band)
            {
                if (!FoxLandingSurface.At(room, col, lipRow + 1, sign, mask, out surfY, out surfKind))
                    break;
                lipRow++;
                topY = surfY;
                lipKind = surfKind;
                up++;
            }

            Vector2 mid = room.MiddleOfTile(col, lipRow);
            cand.Found = true;
            cand.TopY = topY;
            cand.EdgeX = mid.x - sign * 10f;
            cand.Rise = cand.TopY - cand.FootY;
            cand.Gap = Mathf.Abs(cand.EdgeX - c[0].pos.x);
            cand.LipSurface = lipKind;
            cand.LandingClear = !FoxLandingSurface.At(room, col, lipRow + 1, sign, mask,
                                                     out surfY, out surfKind);
            cand.Why = (up > band) ? "wall" : (faceRow > footRow ? "overhang" : "ok");
            return true;
        }

        internal static string RefuseRiseBand(FoxBreedParams p, float rise)
        {
            if (rise < p.LedgePounceMinRise) return "below_leap_band";
            if (rise > p.LedgePounceMaxRise) return "ledge_too_high";
            return null;
        }

        internal static string LeapKindOf(FoxBreedParams p, float rise)
        {
            float band = Mathf.Max(0f, p.LedgePounceMinRise);
            if (rise >= band) return "up";
            if (rise <= -band) return "down";
            return "level";
        }

        internal static string RefuseLeapBand(FoxBreedParams p, float rise, float gapPx, out string kind)
        {
            kind = LeapKindOf(p, rise);
            if (kind == "up") return RefuseRiseBand(p, rise);

            if (kind == "level")
            {
                if (!p.LedgePounceAllowLevel) return "below_leap_band";
                if (gapPx < Mathf.Max(0f, p.LedgePounceMinGapPx)) return "gap_too_small";
                return null;
            }

            if (!p.LedgePounceAllowDescend) return "below_leap_band";
            if (-rise > Mathf.Max(0f, p.LedgePounceMaxDrop)) return "drop_too_far";
            if (gapPx < Mathf.Max(0f, p.LedgePounceMinGapPx)) return "gap_too_small";
            return null;
        }

        private static string RefuseLip(FoxBreedParams p, FoxLipMeasure lip)
        {
            if (!lip.Found) return "lip_unmeasured";
            string kindUnused;
            string band = RefuseLeapBand(p, lip.Rise, lip.VoidCols * 20f, out kindUnused);
            if (band != null) return band;
            if (!lip.LandingClear) return "no_landing_room";
            if (lip.Why == "overhang")
                return lip.LipSurface == "platform" ? "platform_overhang" : "overhang_not_a_step";
            return null;
        }

        private Solved Solve(FoxBreedParams p, FoxMoveSituation s, int sign, FoxAscentPlan plan)
        {
            var so = Unsolved();
            if (sign == 0 || fox.Chunks == null || fox.Chunks.Length < 3)
            {
                so.Refuse = "no_body";
                return so;
            }

            so.Lip = ProbeLip(p, sign);
            Vector2 from = fox.mainBodyChunk.pos;

            bool routedArc = plan != null && plan.Kind == FoxAscentKind.Pounce
                             && plan.LaunchVel.sqrMagnitude > 0.0001f;
            if (routedArc)
            {
                so.FromPlan = true;
                so.Aim = plan.AimAt;
                so.Rise = plan.AimAt.y - (so.Lip.Found ? so.Lip.FootY : FootLine());
                so.WantDist = Mathf.Abs(plan.AimAt.x - from.x);
            }
            else
            {
                if (!so.Lip.Found) { so.Refuse = "lip_unmeasured"; return so; }
                so.Rise = so.Lip.Rise;
                float landX = so.Lip.EdgeX + sign * Mathf.Max(0f, p.LedgePounceLandMarginPx);
                float aimRise = Mathf.Max(0f, so.Rise);
                so.WantDist = Mathf.Max(Mathf.Abs(landX - from.x), aimRise * p.LedgePounceLeadFac);
                so.Aim = from + new Vector2(sign * so.WantDist, aimRise);
            }

            so.LaunchDeg = fox.LaunchAngleDeg(so.Aim);

            so.Kind = LeapKindOf(p, so.Rise);
            if (so.FromPlan)
            {
                if (so.Rise < p.PounceRouteMinRise) so.Refuse = "below_leap_band";
                else if (so.Rise > p.PounceRouteMaxRise) so.Refuse = "ledge_too_high";
            }
            else
            {
                so.Refuse = RefuseLip(p, so.Lip);
            }
            if (so.Refuse == null && !fox.HeadroomAbove(p)) so.Refuse = "ledge_no_headroom";
            if (so.Refuse == null && fox.LaunchTooSteep(p, so.LaunchDeg)) so.Refuse = "too_steep";

            if (so.Refuse == null && so.Kind != "up" && !so.FromPlan
                && fox.AI != null && fox.AI.WalkedRoute == "walked_path_ok")
            {
                so.Refuse = "walked_path_ok";
            }

            if (so.Refuse == null)
            {
                bool fliesPlan = so.FromPlan && p.PounceRouteFlyLaunchVel && plan != null;
                so.Reach = fliesPlan
                           ? fox.CanReachLeap(p, so.Aim, GatherSlackPx(p), plan.LaunchVel)
                           : fox.CanReachLeap(p, so.Aim, GatherSlackPx(p));
                if (!so.Reach.Ok) so.Refuse = "out_of_reach";
            }
            return so;
        }

        private static float GatherSlackPx(FoxBreedParams p)
        {
            float fac = Mathf.Clamp01(p.LedgePounceGatherSlackFac);
            if (fac <= 0f) return 0f;
            return fac * GatherSlackMaxPx(p);
        }

        private static float GatherSlackMaxPx(FoxBreedParams p)
        {
            float n = Math.Max(0, p.PounceGatherFrames);
            return Mathf.Max(0f, p.PounceGatherPull) * n * (n + 1f) * 0.5f;
        }

        private static string Measure(FoxBreedParams p, Solved so)
        {
            return null;
        }

        private static string Reach(FoxBreedParams p, Solved so)
        {
            return null;
        }

        internal bool ConsiderApproach(FoxBreedParams p, FoxMoveSituation s)
        {
            string exit = null;
            if (!p.PounceRouteEnabled || !p.LedgePounceEnabled || !p.PounceEnabled) exit = "approach_disabled";
            else if (fox.RoomRef == null || fox.Chunks == null || fox.Chunks.Length < 3) exit = "no_body";
            else if (Cooldown > 0) exit = "leap_cooldown";
            else if (fox.PounceCooldownTicks > 0) exit = "pounce_cooldown";
            else if (fox.pounce != Fox.PounceState.None) exit = "already_pouncing";
            else if (fox.Move.Mantle.Running) exit = "clamber_running";
            else if (fox.TurnActive) exit = "turning";
            else if (fox.WallBarsLaunch) exit = "wall_hold";
            else if (!fox.Grounded) exit = "airborne";
            else if (fox.EngineOwnsBody) exit = "engine_owns_body";
            else if (fox.dead) exit = "dead";
            if (exit != null) { return false; }

            FoxAscentPlan plan = FoxAscentSeam.UsablePlan(fox, "approach");
            if (plan != null && plan.Kind != FoxAscentKind.Pounce)
            {
                return false;
            }

            int sign = HeadingSign(s, plan);
            if (sign == 0) { return false; }

            if (plan == null && s.Desired.x * sign <= 0f)
            {
                return false;
            }

            Solved so = Solve(p, s, sign, plan);

            float ahead = so.Lip.Found ? so.Lip.Gap : Mathf.Abs(so.Aim.x - fox.mainBodyChunk.pos.x);
            bool routedTile = so.FromPlan && plan != null && plan.LaunchFrom.TileDefined
                              && fox.RoomRef != null;

            if (routedTile)
            {
                Vector2 launchAt = fox.RoomRef.MiddleOfTile(plan.LaunchFrom.Tile);
                if (!Custom.DistLess(fox.mainBodyChunk.pos, launchAt, 20f))
                {
                    return false;
                }
            }

            if (so.Refuse != null)
            {
                return false;
            }

            if (!routedTile)
            {
                bool atLaunch = p.LedgePounceStandoffPx <= 0f || ahead >= p.LedgePounceStandoffPx;
                if (!atLaunch)
                {
                    return false;
                }
            }

            string source = so.FromPlan ? "plan" : "approach";
            if (!so.FromPlan && fox.AI != null)
            {
                float landY = so.Lip.Found ? so.Lip.TopY - 1f : so.Aim.y;
                FoxAscentPlan answer = fox.AI.RequestAscent(new Vector2(so.Aim.x, landY), ahead);
                if (answer != null)
                {
                    if (answer.Kind != FoxAscentKind.Pounce)
                    {
                        return false;
                    }
                    source = "routed";
                }
            }

            Latch(p, s, so, ahead, source, true);
            return true;
        }

        internal string RuleOutPounce(FoxBreedParams p, FoxMoveSituation s)
        {
            if (!p.LedgePounceEnabled || !p.PounceEnabled) return "leap_disabled";

            FoxAscentPlan plan = FoxAscentSeam.UsablePlan(fox, "rule_out");
            if (plan != null && plan.Kind == FoxAscentKind.Mantle)
            {
                return string.IsNullOrEmpty(plan.PounceRuledOutBy) ? "plan_chose_mantle" : plan.PounceRuledOutBy;
            }
            if (plan != null && plan.Kind == FoxAscentKind.StepUp) return "plan_chose_step_up";

            int sign = HeadingSign(s, plan);
            if (sign == 0) return "no_direction";

            Solved so = Solve(p, s, sign, plan);
            if (so.Refuse != null) return so.Refuse;
            if (Cooldown > 0 || fox.PounceCooldownTicks > 0) return "leap_cooldown";
            if (!so.FromPlan && p.LedgePounceStandoffPx > 0f && so.Lip.Gap < p.LedgePounceStandoffPx)
            {
                return "no_standoff_room";
            }
            return null;
        }

        internal string BailFromMantle(FoxBreedParams p, int sign)
        {
            if (!p.LedgePounceEnabled || !p.PounceEnabled) return "ledge_disabled";
            if (fox.pounce != Fox.PounceState.None) return "already_pouncing";
            if (fox.WallBarsLaunch) return "wall_hold";
            if (!fox.Grounded) return "airborne";
            if (fox.PounceCooldownTicks > 0) return "pounce_cooldown";
            if (sign == 0) return "no_direction";

            var s = new FoxMoveSituation { ToTarget = new Vector2(sign * 20f, 0f), Desired = new Vector2(sign, 0f) };
            Solved so = Solve(p, s, sign, null);
            if (so.Refuse != null) { return so.Refuse; }

            Cooldown = 0;
            Latch(p, s, so, so.Lip.Gap, "mantle_bail", true);
            return "leap_started";
        }

        private void Latch(FoxBreedParams p, FoxMoveSituation s, Solved so, float ahead, string source,
                           bool begin)
        {
            aim = so.Aim;
            wallPressSince = -1;

            if (!begin) return;
            var sit = new FoxMoveSituation
            {
                Conn = s.Conn,
                ToTarget = s.ToTarget,
                Desired = s.Desired,
                Target = so.Aim,
                Why = "ledge_climb",
            };
            Begin(p, sit, FoxMoveBid.Yes(source, Measure(p, so)));
        }

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            if (!p.LedgePounceEnabled || !p.PounceEnabled)
            {
                string m = null;
                return FoxMoveBid.No("ledge_disabled", m);
            }

            FoxAscentPlan plan = FoxAscentSeam.UsablePlan(fox, "arbiter");
            if (plan != null && plan.Kind != FoxAscentKind.Pounce)
            {
                string mPlan = null;
                return FoxMoveBid.No("plan_chose_" + plan.Kind, mPlan);
            }

            int sign = HeadingSign(s, plan);
            Solved so = Solve(p, s, sign, plan);
            float ahead = so.Lip.Found ? so.Lip.Gap : Mathf.Abs(s.ToTarget.x);

            string refuse = so.Refuse;
            if (refuse == null)
            {
                if (fox.pounce != Fox.PounceState.None) refuse = "ledge_already_pouncing";
                else if (fox.PounceCooldownTicks > 0) refuse = "ledge_pounce_cooldown";
                else if (Cooldown > 0) refuse = "ledge_cooldown";
                else if (fox.WallBarsLaunch) refuse = "ledge_wall_hold";
                else if (!fox.Grounded) refuse = "ledge_airborne";
                else if (!so.FromPlan && p.LedgePounceStandoffPx > 0f && ahead < p.LedgePounceStandoffPx
                         && !WallPressExhausted(p))
                {
                    refuse = "no_standoff_room";
                }
            }

            if (refuse != null)
            {
                if (refuse != "no_standoff_room") wallPressSince = -1;
                if (refuse != "no_standoff_room") Cooldown = Math.Max(Cooldown, p.LedgePounceCooldown / 2);
                return FoxMoveBid.No(refuse, Measure(p, so));
            }

            string source = (p.LedgePounceStandoffPx > 0f && ahead < p.LedgePounceStandoffPx)
                            ? "wall_fallback" : "arbiter";
            Latch(p, s, so, ahead, source, false);
            return FoxMoveBid.Yes(source, Measure(p, so));
        }

        private bool WallPressExhausted(FoxBreedParams p)
        {
            if (wallPressSince < 0) { wallPressSince = fox.MoveTick; return false; }
            return fox.MoveTick - wallPressSince >= Math.Max(1, p.LedgePounceCooldown);
        }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            if (fox.Move.Mantle.Running) fox.Move.Mantle.End(p, "leap_took_over");

            fox.Move.Pounce.Begin(p,
                new FoxMoveSituation { Target = aim, Why = "ledge_climb" },
                FoxMoveBid.Yes("ledge_climb", LedgeClimbMeasure()));
        }

        protected override int CooldownOnEnd(FoxBreedParams p, string reason, bool handover)
        {
            return p.LedgePounceCooldown;
        }

        private string LedgeClimbMeasure()
        {
            return null;
        }

        protected override string Detail(FoxBreedParams p)
        {
            return null;
        }
    }

    internal sealed class FoxMantleAction : FoxMoveAction
    {
        internal FoxMantleAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "mantle"; } }

        internal override FoxBodyClaim Claims { get { return FoxBodyClaim.None; } }

        private Vector2 grip;
        private float lipY;
        private float dirSign;
        private float rise;
        private int headroom;
        private bool frontUp;
        private bool gripped;
        private int lastLogTick = -1000;

        private string pounceRuledOutBy = "none";
        private float lastClimbY = float.MinValue;
        private float startFrontY;
        private float bestFrontY;

        private int probeCol = int.MinValue;
        private int probeLipRow = int.MinValue;

        private string routeVerdict = "not_asked";
        private float routeHereRes = float.NaN;
        private float routeLandRes = float.NaN;

        internal Vector2 Grip { get { return grip; } }
        internal bool FrontUp { get { return frontUp; } }

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            string mGate = null;
            if (!p.MantleEnabled || fox.RoomRef == null)
            {
                return FoxMoveBid.No("mantle_disabled", mGate);
            }
            if (Cooldown > 0)
            {
                return FoxMoveBid.No("mantle_cooldown", mGate);
            }
            if (!fox.Grounded) return FoxMoveBid.No("airborne", "");
            if (fox.pounce != Fox.PounceState.None) return FoxMoveBid.No("pouncing", "");
            if (fox.TurnActive) return FoxMoveBid.No("turning", "");

            BodyChunk[] c = fox.Chunks;
            float dirX = s.Desired.x;
            if (Mathf.Abs(dirX) < 0.01f) dirX = Fox.Dir(c[2].pos, c[0].pos).x;
            if (Mathf.Abs(dirX) < 0.01f) return FoxMoveBid.No("no_direction", "");
            int sign = dirX >= 0f ? 1 : -1;

            float probeRise, probeLipY, probeLipEdgeX;
            int probeHeadroom;
            string why, probeMeasure;
            if (!ProbeStepAhead(p, sign, out probeRise, out probeLipY, out probeLipEdgeX,
                                out probeHeadroom, out why, out probeMeasure))
                return FoxMoveBid.No(why, probeMeasure);

            if (probeRise < p.MantleMinRise)
            {
                string m = null;
                return FoxMoveBid.No("below_mantle_band", m);
            }
            if (probeRise > p.MantleMaxRise)
            {
                string m2 = null;
                return FoxMoveBid.No("above_mantle_band", m2);
            }

            float gap = Mathf.Abs(probeLipEdgeX - c[0].pos.x);
            if (gap > p.MantleReachPx)
            {
                string m = null;
                return FoxMoveBid.No("out_of_reach", m);
            }

            pounceRuledOutBy = fox.Move.LedgePounce.RuleOutPounce(p, s);
            if (pounceRuledOutBy == null)
            {
                pounceRuledOutBy = "none";
                string m = null;
                return FoxMoveBid.No("pounce_takes_rise", m);
            }

            routeVerdict = ConsultRoutePrice(p);
            if (routeVerdict == ROUTE_NOT_CHEAPER && p.MantleRouteVeto)
            {
                string mRoute = null;
                return FoxMoveBid.No("route_not_cheaper", mRoute);
            }

            grip = new Vector2(probeLipEdgeX + sign * Mathf.Max(0f, p.MantleGripInsetPx), probeLipY);
            lipY = probeLipY;
            dirSign = sign;
            rise = probeRise;
            headroom = probeHeadroom;
            string mYes = null;
            return FoxMoveBid.Yes(why, mYes);
        }

        private const string ROUTE_NOT_CHEAPER = "not_cheaper";

        private string ConsultRoutePrice(FoxBreedParams p)
        {
            routeHereRes = float.NaN;
            routeLandRes = float.NaN;

            if (!p.MantleRouteEnabled) return "route_off";

            Room room = fox.RoomRef;
            if (room == null) return "no_room";
            if (fox.AI == null) return "no_ai";

            if (probeCol == int.MinValue || probeLipRow == int.MinValue) return "no_lip_tile";

            FoxAscentPlan plan = FoxAscentSeam.UsablePlan(fox, "mantle_price");
            if (plan != null && plan.Kind == FoxAscentKind.Mantle && plan.Target.TileDefined
                && plan.Target.room == room.abstractRoom.index
                && plan.Target.x == probeCol && plan.Target.y == probeLipRow + 1)
            {
                return "plan_approved";
            }

            PathFinder pf = fox.AI.pathFinder;
            if (pf == null) return "no_pather";
            int gen = fox.AI.AscentPathGeneration;
            if (gen < 0) return "no_router";

            int landRow = probeLipRow + 1;

            PathFinder.PathingCell hereCell = pf.PathingCellAtWorldCoordinate(fox.abstractCreature.pos);
            PathFinder.PathingCell landCell = pf.PathingCellAtWorldCoordinate(
                new WorldCoordinate(room.abstractRoom.index, probeCol, landRow, -1));
            if (hereCell == null || landCell == null) return "no_cell";

            if (hereCell.generation != gen)
                return p.MantleRouteVetoOnSilence ? ROUTE_NOT_CHEAPER : "here_unflooded";
            if (landCell.generation != gen)
                return p.MantleRouteVetoOnSilence ? ROUTE_NOT_CHEAPER : "land_unflooded";

            PathCost.Legality ceiling = RouteCeiling(p);

            if (landCell.costToGoal.legality > ceiling)
                return p.MantleRouteVetoOnIllegalLanding ? ROUTE_NOT_CHEAPER : "land_illegal";

            float penalty = Mathf.Max(0f, p.MantleRoutePenalty);
            routeLandRes = landCell.costToGoal.resistance;

            if (hereCell.costToGoal.legality > ceiling)
            {
                routeHereRes = float.MaxValue;
                return "here_illegal";
            }

            routeHereRes = hereCell.costToGoal.resistance;
            return (routeLandRes + penalty < routeHereRes) ? "cheaper" : ROUTE_NOT_CHEAPER;
        }

        private static PathCost.Legality RouteCeiling(FoxBreedParams p)
        {
            if (string.Equals(p.PounceRouteMaxLegality, "allowed", StringComparison.OrdinalIgnoreCase))
                return PathCost.Legality.Allowed;
            return PathCost.Legality.Unwanted;
        }

        protected override int BudgetFor(FoxBreedParams p, FoxMoveSituation s)
        {
            return Math.Max(1, p.MantleMaxTicks) + 1;
        }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            frontUp = false;
            gripped = false;
            lastLogTick = fox.MoveTick;
            lastClimbY = float.MinValue;
            BodyChunk[] c0 = fox.Chunks;
            startFrontY = c0[0].pos.y - c0[0].rad;
            bestFrontY = startFrontY;
        }

        protected override string ShouldAbort(FoxBreedParams p)
        {
            BodyChunk[] c = fox.Chunks;
            if (c == null || c.Length < 3) return "no_body";
            if (!p.MantleEnabled) return "disabled";
            if (fox.RoomRef == null || fox.dead || !fox.Consious) return "not_acting";
            if (fox.pounce != Fox.PounceState.None) return "pounce";
            if (fox.TurnActive) return "turn";
            if (Mathf.Abs(grip.x - c[0].pos.x) > p.MantleReachPx * 2f) return "left_ledge";
            if (!fox.RoomRef.GetTile(new Vector2(grip.x, lipY - 10f)).Solid) return "terrain_gone";

            float need = (lipY - Mathf.Max(0f, p.MantleClearMarginPx)) - startFrontY;
            if (Ticks >= Budget / 2 && need > 0f && (bestFrontY - startFrontY) < need * 0.5f)
                return "stalled";
            return null;
        }

        protected override string IsComplete(FoxBreedParams p)
        {
            bool front, rear;
            Ends(p, out front, out rear);
            return (front && rear && fox.Grounded) ? "on_ledge" : null;
        }

        protected override void OnTick(FoxBreedParams p)
        {
            BodyChunk[] c = fox.Chunks;
            bool front, rear;
            Ends(p, out front, out rear);

            if (!gripped && FrontGrips() > 0)
            {
                gripped = true;
            }

            if (front != frontUp)
            {
                frontUp = front;
                lastClimbY = float.MinValue;
            }

            float g = Mathf.Max(0.05f, fox.gravity);
            float pull = Mathf.Max(0f, p.MantlePullGravities) * g;
            float fwd = Mathf.Max(0f, p.MantleForwardGravities) * g;
            float cap = Mathf.Max(0.5f, p.MantleMaxRiseSpeed);
            float rearFac = Mathf.Max(0f, p.MantleRearPullFac);

            float climbY = front ? (c[2].pos.y - c[2].rad) : (c[0].pos.y - c[0].rad);
            float gained = (lastClimbY > float.MinValue) ? climbY - lastClimbY : 0f;
            lastClimbY = climbY;
            bestFrontY = Mathf.Max(bestFrontY, c[0].pos.y - c[0].rad);

            float ceiling = lipY + Mathf.Max(0f, p.MantleClearMarginPx);
            float remaining = (lipY - Mathf.Max(0f, p.MantleClearMarginPx)) - climbY;
            int ticksLeft = Math.Max(1, (Budget - Ticks) / 2);
            float want = Mathf.Min(cap, Mathf.Max(0f, remaining) / ticksLeft);
            float assist = (remaining > 0f) ? Mathf.Clamp(want - gained, 0f, cap) : 0f;

            if (!front)
            {
                ClimbLift(c[0], pull, cap);
                ClimbLift(c[1], pull * rearFac, cap);
                c[0].vel.x += dirSign * fwd;
                AssistLift(c[0], assist, ceiling);
                AssistLift(c[1], assist * rearFac, ceiling);
            }
            else
            {
                if (!rear)
                {
                    ClimbLift(c[1], pull, cap);
                    ClimbLift(c[2], pull, cap);
                    AssistLift(c[1], assist, ceiling);
                    AssistLift(c[2], assist, ceiling);
                }
                c[0].vel.x += dirSign * fwd;
                c[2].vel.x += dirSign * fwd * rearFac;
            }
        }

        protected override bool OnEnd(FoxBreedParams p, string reason, int ran, bool wasRunning, bool handover)
        {
            if (!wasRunning) { frontUp = false; return false; }
            frontUp = false;
            gripped = false;
            lastClimbY = float.MinValue;

            if (!handover && (reason == "stalled" || reason == "timeout"))
            {
                fox.Move.LedgePounce.BailFromMantle(p, dirSign >= 0f ? 1 : -1);
            }
            return true;
        }

        protected override int CooldownOnEnd(FoxBreedParams p, string reason, bool handover)
        {
            return handover ? 0 : p.MantleCooldown;
        }

        protected override int HeartbeatTicks(FoxBreedParams p) { return p.MoveActionLogTicks; }

        protected override string Detail(FoxBreedParams p)
        {
            return null;
        }

        private void Ends(FoxBreedParams p, out bool front, out bool rear)
        {
            BodyChunk[] c = fox.Chunks;
            float margin = Mathf.Max(0f, p.MantleClearMarginPx);
            front = (c[0].pos.y - c[0].rad) >= lipY - margin;
            rear = (c[2].pos.y - c[2].rad) >= lipY - margin;
        }

        private static void ClimbLift(BodyChunk c, float pull, float cap)
        {
            if (c == null || c.vel.y >= cap) return;
            c.vel.y = Mathf.Min(c.vel.y + pull, cap);
        }

        private void AssistLift(BodyChunk c, float px, float ceiling)
        {
            if (c == null || px <= 0.001f) return;
            float step = Mathf.Min(px, ceiling - (c.pos.y - c.rad));
            if (step <= 0.001f) return;
            Room room = fox.RoomRef;
            if (room != null && room.GetTile(new Vector2(c.pos.x, c.pos.y + c.rad + step)).Solid) return;
            c.pos.y += step;
        }

        private int FrontGrips()
        {
            FoxGraphics g = fox.graphicsModule as FoxGraphics;
            if (g == null || g.limbs == null || g.limbs.Length < 4) return 0;
            int n = 0;
            for (int i = 0; i < 2; i++)
                if (g.limbs[i] != null && g.limbs[i].gripCounter > 0) n++;
            return n;
        }

        private bool ProbeStepAhead(FoxBreedParams p, int sign, out float outRise, out float outLipY,
                                    out float outLipEdgeX, out int outHeadroom, out string why,
                                    out string measure)
        {
            outRise = 0f; outLipY = 0f; outLipEdgeX = 0f; outHeadroom = 0; why = "none"; measure = "";
            probeCol = int.MinValue;
            probeLipRow = int.MinValue;
            Room room = fox.RoomRef;
            BodyChunk[] c = fox.Chunks;
            if (room == null || c == null || c.Length < 3)
            {
                why = "no_room";
                return false;
            }

            IntVector2 here = room.GetTilePosition(fox.mainBodyChunk.pos);
            int col = here.x + sign * Math.Max(1, p.MantleProbeTiles);

            float climbFootY = Mathf.Min(c[0].pos.y - c[0].rad, c[2].pos.y - c[2].rad);
            float bodyFootY = float.MaxValue;
            for (int i = 0; i < c.Length; i++) bodyFootY = Mathf.Min(bodyFootY, c[i].pos.y - c[i].rad);
            float footY = p.MantleRiseFromClimbEnds ? climbFootY : bodyFootY;

            int footRow = room.GetTilePosition(new Vector2(fox.mainBodyChunk.pos.x, footY + 1f)).y;

            int faceMask = p.MantleFacePlatforms
                ? (FoxLandingSurface.Rock | FoxLandingSurface.Platform)
                : FoxLandingSurface.Rock;
            float faceTopY;
            string faceKind;
            if (!FoxLandingSurface.At(room, col, footRow, sign, faceMask, out faceTopY, out faceKind))
            {
                why = "no_face";
                return false;
            }

            int scan = Math.Max(1, p.MantleMaxScanTiles);
            int lipRow = footRow;
            while (lipRow - footRow < scan && room.GetTile(col, lipRow + 1).Solid) lipRow++;
            if (room.GetTile(col, lipRow + 1).Solid)
            {
                why = "wall_not_step";
                return false;
            }

            probeCol = col;
            probeLipRow = lipRow;

            Vector2 lipMid = room.MiddleOfTile(col, lipRow);
            outLipY = lipMid.y + 10f;
            outLipEdgeX = lipMid.x - sign * 10f;
            outRise = outLipY - footY;

            int want = Math.Max(1, p.MantleHeadroomTiles);
            while (outHeadroom < want && !room.GetTile(col, lipRow + 1 + outHeadroom).Solid) outHeadroom++;
            if (outHeadroom < want)
            {
                why = "no_headroom";
                return false;
            }
            for (int i = 1; i <= want; i++)
            {
                if (room.GetTile(here.x, here.y + i).Solid)
                {
                    why = "no_rear_room";
                    return false;
                }
            }

            why = "ok";
            return true;
        }
    }
    internal sealed class FoxEnterAction : FoxMoveAction
    {
        internal FoxEnterAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "enter"; } }

        internal override FoxBodyClaim Claims
        {
            get
            {
                FoxBodyClaim c = FoxBodyClaim.Drive;
                if (spinePullWanted) c |= FoxBodyClaim.SpineLinks;
                if (throughFloorsWanted) c |= FoxBodyClaim.ThroughFloors;
                return c;
            }
        }

        internal enum Phase { Gather, Insert }

        internal enum ShortcutVerdict
        {
            off,
            never,
            stale,
            delay_gone,
            far,
            no_hole_dir,
            no_slot,
            ready,
        }

        private Phase phase;
        private IntVector2 destTile;
        private IntVector2 fromTile;
        private string trigger = "none";
        private bool spinePullWanted;
        private bool throughFloorsWanted;
        private int lastLogTick;
        private IntVector2 pendingDest;
        private IntVector2 pendingFrom;
        private string pendingTrigger = "none";
        private ShortcutVerdict sc = ShortcutVerdict.never;
        private IntVector2 scMouth;
        private IntVector2 scSlot;

        internal bool HoldsSpinePull { get { return Running && spinePullWanted; } }

        internal bool TryTravelTiles(out IntVector2 from, out IntVector2 dest)
        {
            from = fromTile;
            dest = destTile;
            return Running;
        }

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            Room room = fox.RoomRef;
            if (room == null || room.aimap == null || fox.Chunks == null || fox.Chunks.Length < 3)
            {
                sc = ShortcutVerdict.never;
                return Refuse(p, "no_body", null);
            }

            if (!p.EnterEnabled) { sc = ShortcutVerdict.off; return Refuse(p, "enter_disabled", null); }
            if (Cooldown > 0)
            {
                sc = ShortcutOf(p, room);
                string d = null;
                return Refuse(p, "enter_cooldown", d);
            }
            if (!fox.Consious) { sc = ShortcutOf(p, room); return Refuse(p, "unconscious", null); }
            if (fox.EngineOwnsBody)
            {
                sc = ShortcutOf(p, room);
                string d = null;
                return Refuse(p, "engine_owns", d);
            }

            if (fox.Move.AnyRunning)
            {
                sc = ShortcutOf(p, room);
                return Refuse(p, "busy", BusyDetail());
            }

            IntVector2 dest, from;
            string how;
            bool haveTarget = ResolveTarget(p, room, out dest, out from, out how);
            if (haveTarget) { pendingDest = dest; pendingFrom = from; pendingTrigger = how; }
            if (!haveTarget)
            {
                string reason = (sc == ShortcutVerdict.no_slot || sc == ShortcutVerdict.no_hole_dir)
                                ? "no_slot_at_mouth" : "no_corridor_step";
                return Refuse(p, reason, NoTargetDetail(p, room));
            }

            bool viaPipe = how == "shortcut_exit";

            if (!fox.Grounded && (!viaPipe || p.EnterShortcutNeedsGround))
            {
                string d = null;
                return Refuse(p, "airborne", d);
            }

            int chunks = fox.CorridorChunkCount();
            int need = fox.CorridorMinChunks(p);
            if (!viaPipe && chunks >= need)
            {
                string d = null;
                return Refuse(p, "already_in", d);
            }

            Vector2 aim = room.MiddleOfTile(dest);
            float gap = Vector2.Distance(fox.Chunks[0].pos, aim);
            if (gap > Mathf.Max(0f, p.EnterReachPx))
            {
                string d = null;
                return Refuse(p, "out_of_reach", d);
            }

            IntVector2 headTile = room.GetTilePosition(fox.Chunks[0].pos);
            if (headTile.x == dest.x && headTile.y == dest.y)
            {
                string d = null;
                return Refuse(p, "head_already_at_dest", d);
            }

            ClearRefusal();
            string m = null;
            return FoxMoveBid.Yes(how, m);
        }

        private bool ResolveTarget(FoxBreedParams p, Room room, out IntVector2 dest, out IntVector2 from, out string how)
        {
            dest = new IntVector2(0, 0);
            from = new IntVector2(0, 0);
            how = "none";

            sc = ShortcutOf(p, room);

            MovementConnection step;
            if (fox.TryCorridorEntryStep(out step))
            {
                dest = step.DestTile;
                from = step.StartTile;
                how = "drive";
                return true;
            }

            if (sc == ShortcutVerdict.ready)
            {
                dest = scSlot;
                from = scMouth;
                how = "shortcut_exit";
                return true;
            }
            return false;
        }

        private ShortcutVerdict ShortcutOf(FoxBreedParams p, Room room)
        {
            scMouth = new IntVector2(0, 0);
            scSlot = new IntVector2(0, 0);

            if (!p.EnterShortcutEnabled) return ShortcutVerdict.off;
            if (room == null || fox.Chunks == null || fox.Chunks.Length < 1) return ShortcutVerdict.never;

            int age = fox.MoveTick - fox.LastShortcutExitTick;
            if (fox.LastShortcutExitTick <= Fox.NEVER_STAMPED || age < 0) return ShortcutVerdict.never;
            if (age > Math.Max(1, p.PipeExitDelayTicks)) return ShortcutVerdict.stale;
            if (fox.shortcutDelay <= 0) return ShortcutVerdict.delay_gone;

            IntVector2 mouth = fox.LastShortcutExitTile;
            IntVector2 head = room.GetTilePosition(fox.Chunks[0].pos);
            int near = Math.Max(0, p.EnterShortcutNearTiles);
            if (Math.Abs(head.x - mouth.x) > near || Math.Abs(head.y - mouth.y) > near)
                return ShortcutVerdict.far;
            scMouth = mouth;

            IntVector2 d = room.ShorcutEntranceHoleDirection(mouth);
            if (d.x == 0 && d.y == 0) return ShortcutVerdict.no_hole_dir;

            bool found = false;
            int scan = Math.Max(1, p.EnterShortcutScanTiles);
            for (int i = 1; i <= scan; i++)
            {
                IntVector2 t = new IntVector2(mouth.x + d.x * i, mouth.y + d.y * i);
                if (fox.TileIsCorridorLike(t)) { scSlot = t; found = true; }
            }
            if (!found) return ShortcutVerdict.no_slot;
            return ShortcutVerdict.ready;
        }

        private string NoTargetDetail(FoxBreedParams p, Room room)
        {
            return null;
        }

        private string BusyDetail()
        {
            return null;
        }

        protected override int BudgetFor(FoxBreedParams p, FoxMoveSituation s)
        {
            return Math.Max(1, p.EnterMaxFrames);
        }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            destTile = pendingDest;
            fromTile = pendingFrom;
            trigger = pendingTrigger;
            phase = (p.EnterGatherFrames > 0) ? Phase.Gather : Phase.Insert;
            spinePullWanted = false;
            throughFloorsWanted = false;
            lastLogTick = fox.MoveTick;
            fox.CorridorChunkCount();
            fox.EnterGather = (phase == Phase.Gather) ? 0f : 1f;
        }

        protected override string ShouldAbort(FoxBreedParams p)
        {
            if (fox.Chunks == null || fox.Chunks.Length < 3 || fox.RoomRef == null
                || fox.RoomRef.aimap == null) return "no_body";
            if (fox.dead) return "died";
            if (fox.Stunned) return "stunned";
            if (fox.EngineOwnsBody) return "engine_owns";
            if (fox.Move.Pounce.Running || fox.Move.Charge.Running
                || fox.Move.Lunge.Running || fox.Move.Turn.Running) return "preempted";
            if (!fox.TileIsCorridorLike(destTile)) return "dest_lost";
            return null;
        }

        protected override string IsComplete(FoxBreedParams p)
        {
            if (phase != Phase.Insert) return null;
            Room room = fox.RoomRef;
            if (room == null) return null;

            IntVector2 headTile = room.GetTilePosition(fox.Chunks[0].pos);
            if (headTile.x == destTile.x && headTile.y == destTile.y) return "head_in";

            if (trigger != "shortcut_exit" && fox.CorridorChunkCount() >= fox.CorridorMinChunks(p))
                return "body_in";
            return null;
        }

        protected override void OnTick(FoxBreedParams p)
        {
            Room room = fox.RoomRef;
            BodyChunk[] c = fox.Chunks;
            Vector2 aim = room.MiddleOfTile(destTile);

            if (phase == Phase.Gather)
            {
                int frames = Math.Max(1, p.EnterGatherFrames);
                fox.EnterGather = Mathf.Clamp01((float)(Ticks - 1) / Math.Max(1, frames - 1));
                spinePullWanted = false;
                UpdateThroughFloors(p, room, false);
                if (Ticks >= p.EnterGatherFrames)
                {
                    phase = Phase.Insert;
                    fox.EnterGather = 1f;
                }
                return;
            }

            fox.EnterGather = 1f;
            spinePullWanted = p.EnterSpinePull;
            UpdateThroughFloors(p, room, true);
            Fox.PullChunk(c[0], aim, p.EnterPull * (1f + Mathf.Max(0f, p.EnterHeadLead)));
            Fox.PullChunk(c[2], room.MiddleOfTile(fromTile), p.EnterPull * (1f - Mathf.Clamp01(p.EnterRearLag)));
        }

        private void UpdateThroughFloors(FoxBreedParams p, Room room, bool inserting)
        {
            bool want = inserting
                        && p.EnterThroughFloors
                        && fox.TileIsPlatform(destTile)
                        && room != null && fox.Chunks != null && fox.Chunks.Length > 0
                        && room.GetTilePosition(fox.Chunks[0].pos).y >= destTile.y;

            if (want == throughFloorsWanted) return;
            throughFloorsWanted = want;
            fox.SetGoThroughFloors(want, want ? "enter_insert" : "enter_clear");
        }

        internal override void TickAuxCooldowns()
        {
            if (Running) return;
            if (fox.EnterGather <= 0f) return;
            FoxBreedParams p = fox.P;
            int ticks = (p == null) ? 0 : Math.Max(0, p.PounceGatherReleaseTicks);
            fox.EnterGather = (ticks <= 0) ? 0f : Mathf.Max(0f, fox.EnterGather - 1f / ticks);
        }

        protected override bool LeakedClaim(FoxBreedParams p)
        {
            return spinePullWanted || throughFloorsWanted;
        }

        protected override int CooldownOnEnd(FoxBreedParams p, string reason, bool handover)
        {
            return Math.Max(0, p.EnterCooldown);
        }

        protected override bool OnEnd(FoxBreedParams p, string reason, int ran, bool wasRunning, bool handover)
        {
            bool restored = false;

            if (throughFloorsWanted)
            {
                throughFloorsWanted = false;
                fox.SetGoThroughFloors(false, "enter_end_" + reason);
                restored = true;
            }

            if (spinePullWanted)
            {
                spinePullWanted = false;
                if (!p.SqueezeSpinePull) fox.AssertAsBuiltBodyNow();
                restored = true;
            }

            if (!wasRunning) return restored;

            fox.CorridorChunkCount();
            return true;
        }

        private FoxMoveBid Refuse(FoxBreedParams p, string reason, string detail)
        {
            if (reason == "out_of_reach" || reason == "head_already_at_dest")
            {
                int half = Math.Max(0, p.EnterCooldown) / 2;
                if (half > 0 && Cooldown < half) Cooldown = half;
            }
            string m = null;
            return FoxMoveBid.No(reason, m);
        }

        private void ClearRefusal()
        {
        }

        protected override string Detail(FoxBreedParams p)
        {
            return null;
        }
    }
}
