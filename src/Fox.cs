using System;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal struct FoxReachVerdict
    {
        public bool Ok;
        public float WantDist;
        public float WantRise;
        public float HeightAt;
        public float MaxRange;
        public float MaxApex;
        public string Arc;
        public float G;
        public float AirFric;
        public bool Evaluated;

        internal static FoxReachVerdict NotEvaluated()
        {
            return new FoxReachVerdict
            {
                Ok = false, WantDist = -1f, WantRise = -1f,
                HeightAt = -1f, MaxRange = -1f, MaxApex = -1f, Arc = "none", Evaluated = false,
                G = -1f, AirFric = -1f,
            };
        }

        public string Describe()
        {
            return null;
        }
    }

    internal struct FoxArcModel
    {
        internal float G;
        internal float Friction;
        internal float Boost;
        internal int BoostFrames;
        internal int MaxFrames;

        internal static FoxArcModel Of(FoxBreedParams p, float g, float airFriction)
        {
            return new FoxArcModel
            {
                G = Mathf.Max(0f, g),
                Friction = Mathf.Clamp(airFriction, 0.5f, 1f),
                Boost = p.PounceBoost,
                BoostFrames = Mathf.Clamp(p.PounceBoostFrames, 0, 600),
                MaxFrames = Mathf.Clamp(p.PounceFlightMaxFrames, 1, 600),
            };
        }

        internal void Step(ref Vector2 vel, ref float x, ref float y, int tick)
        {
            if (BoostFrames > 0 && tick <= BoostFrames)
                vel.y += Boost * ((float)(BoostFrames - tick + 1) / BoostFrames);
            vel.y -= G;
            vel *= Friction;
            x += vel.x;
            y += vel.y;
        }

        internal float HeightAtDist(Vector2 v0, float dist)
        {
            float vx0 = Mathf.Abs(v0.x);
            if (vx0 <= 0.01f)
                return (dist <= 0.01f) ? Apex(v0) : float.NegativeInfinity;
            if (dist <= 0.01f) return 0f;

            var vel = new Vector2(vx0, v0.y);
            float x = 0f, y = 0f;
            for (int tick = 1; tick <= MaxFrames; tick++)
            {
                float lastX = x, lastY = y;
                Step(ref vel, ref x, ref y, tick);
                if (x >= dist)
                {
                    float span = x - lastX;
                    float f = (span <= 0.0001f) ? 1f : (dist - lastX) / span;
                    return lastY + (y - lastY) * f;
                }
            }
            return float.NegativeInfinity;
        }

        internal float Apex(Vector2 v0)
        {
            var vel = new Vector2(Mathf.Abs(v0.x), v0.y);
            float x = 0f, y = 0f, best = 0f;
            for (int tick = 1; tick <= MaxFrames; tick++)
            {
                Step(ref vel, ref x, ref y, tick);
                if (y > best) best = y;
                if (tick > BoostFrames && vel.y <= 0f) break;
            }
            return best;
        }

        internal float Range(Vector2 v0)
        {
            var vel = new Vector2(Mathf.Abs(v0.x), v0.y);
            float x = 0f, y = 0f;
            bool rose = false;
            for (int tick = 1; tick <= MaxFrames; tick++)
            {
                float lastX = x, lastY = y;
                Step(ref vel, ref x, ref y, tick);
                if (y > 0f) { rose = true; continue; }
                if (!rose) continue;
                float drop = lastY - y;
                float f = (drop <= 0.0001f) ? 1f : lastY / drop;
                return lastX + (x - lastX) * f;
            }
            return x;
        }
    }

    public partial class Fox : Creature
    {
        public enum PounceState
        {
            None,
            Crouch,
            Propel,
            Recover
        }

        public enum ChargeState
        {
            None,
            Windup,
            Rush,
            Recover
        }

        public readonly string VariantId;

        public FoxBreedParams P => FoxParams.For(VariantId);

        public FoxAI AI;

        internal FoxMoveEngine Move { get { return move; } }
        private FoxMoveEngine move;

        internal BodyChunk[] Chunks { get { return bodyChunks; } }

        internal Room RoomRef { get { return room; } }

        internal int MoveTick { get { return GameTick; } }

        internal int PounceCooldownTicks { get { return move == null ? 0 : move.Pounce.Cooldown; } }

        internal void SetWeightApplied(FoxBreedParams p, bool apply, string writer)
        {
            if (applyGravity == apply) return;
            applyGravity = apply;
            ApplyBodyPhysics(p);
        }

        internal void RestoreCommittedStateBody(FoxBreedParams p, string tag)
        {
            RestoreAfterCommittedState(p, tag);
        }

        internal void AssertAsBuiltBodyNow()
        {
            AssertAsBuiltBody();
        }

        internal bool ThroughFloorsOwned { get { return throughFloorsOwned; } }
        private bool throughFloorsOwned;

        internal void SetGoThroughFloors(bool on, string writer)
        {
            if (throughFloorsOwned == on) return;
            throughFloorsOwned = on;
            GoThroughFloors = on;
        }

        internal bool TileIsPlatform(IntVector2 t)
        {
            return room != null && room.GetTile(t).Terrain == Room.Tile.TerrainType.Floor;
        }

        internal bool HasFreshDriveConnection
        {
            get
            {
                return lastFollowedConnection != default(MovementConnection)
                       && GameTick - lastFollowedConnectionTick <= CONN_INTENT_MAX_AGE_TICKS;
            }
        }

        internal const int NEVER_STAMPED = NEVER_LOGGED;

        internal bool SpineRelaxedNow { get { return spineRelaxed; } }

        internal bool SpineRecovering { get { return spineRecoverTicks > 0; } }

        internal int AirTicks { get { return airTicks; } }

        internal bool DriveDescentClamped { get; private set; }

        internal bool DriveSlopeFlattened { get; private set; }

        internal bool GatherIsActive { get { return move != null && move.Pounce.GatherActive; } }

        internal float SpineRestSpan { get { return RestSpan(); } }

        internal static float NeckDot(Vector2 head, Vector2 mid, Vector2 rear)
        {
            Vector2 a = mid - head, b = rear - head;
            float la = a.magnitude, lb = b.magnitude;
            if (la < 0.01f || lb < 0.01f) return 1f;
            return Vector2.Dot(a, b) / (la * lb);
        }

        internal float NeckDotNow
        {
            get
            {
                if (bodyChunks == null || bodyChunks.Length < 3) return 1f;
                return NeckDot(bodyChunks[0].pos, bodyChunks[1].pos, bodyChunks[2].pos);
            }
        }

        internal void RelaxSpineForTurnNow(FoxBreedParams p) { RelaxSpineForTurn(p); }

        internal void ReleaseSpineAfterTurnNow(FoxBreedParams p) { ReleaseSpineAfterTurn(p); }

        internal void SnapSpineBackNow(FoxBreedParams p)
        {
            spineRecoverTicks = 0;
            UpdateSpineSprings(p);
        }

        internal Vector2 CarryPointNow(FoxBreedParams p) { return CarryPoint(p); }

        internal void CarryThroughTurnNow(FoxBreedParams p, Vector2 carryBefore)
        {
            CarryThroughTurn(p, carryBefore);
        }

        internal void AssertLaunchTraction()
        {
            if (poleHeld || poleOwnsGravity) ReleasePoleHold(P, "launch_traction", true);
            if (wallHeld || wallOwnsGravity) ReleaseWallHold(P, "launch_traction", true);
            applyGravity = true;
            GoThroughFloors = false;
        }

        internal void ClearGoThroughFloors() { GoThroughFloors = false; }

        internal bool BodyDeflated
        {
            get
            {
                if (bodyChunks != null)
                {
                    for (int i = 0; i < bodyChunks.Length; i++)
                    {
                        if (bodyChunks[i] == null) continue;
                        if (bodyChunks[i].terrainSqueeze < 0.999f) return true;
                        if (!bodyChunks[i].collideWithSlopes) return true;
                    }
                }
                if (bodyChunkConnections != null)
                {
                    for (int i = 0; i < Math.Min(2, bodyChunkConnections.Length); i++)
                    {
                        if (bodyChunkConnections[i] == null) continue;
                        if (bodyChunkConnections[i].type != BodyChunkConnection.Type.Normal) return true;
                    }
                }
                return false;
            }
        }

        public readonly FoxVoice voice;

        public float runSpeed;

        public float StalkCrouch;

        public float LandingPose;

        public float EatPose;

        public bool MantleActive { get { return move != null && move.Mantle.Running; } }

        public Vector2 MantleGrip { get { return move == null ? Vector2.zero : move.Mantle.Grip; } }

        public bool MantleFrontUp { get { return move != null && move.Mantle.FrontUp; } }

        public float PounceGather { get; internal set; }

        public float PounceGatherReady { get; internal set; }

        public float EnterGather { get; internal set; }

        public float PounceFlight { get; internal set; }

        public float PounceLand { get; internal set; }

        public Vector2 EatAimPos;

        public sealed class ScentSniffSeam
        {
            public int RequestSerial;
            public int WithdrawnSerial;

            public int StartedSerial;
            public int ClosedSerial;
            public bool ClosedCompleted;

            public bool Open
            {
                get { return RequestSerial > ClosedSerial && RequestSerial > WithdrawnSerial; }
            }
        }

        public readonly ScentSniffSeam ScentSniff = new ScentSniffSeam();

        public float Hunger
        {
            get { return (abstractCreature.state is FoxState fs) ? fs.hunger : 0f; }
        }

        public float CarryGate
        {
            get { return TotalMass * P.CarryMaxMassRatio; }
        }

        public bool CanCarry(PhysicalObject target)
        {
            return target != null && target.TotalMass <= CarryGate;
        }

        public Creature HeldPrey
        {
            get
            {
                if (grasps == null || grasps.Length == 0 || grasps[0] == null) return null;
                return grasps[0].grabbed as Creature;
            }
        }

        public Creature chaseTarget
        {
            get { return _chaseTarget; }
            set { SetChaseTarget(value, null); }
        }

        public void SetChaseTarget(Creature value, string reason)
        {
            if (ReferenceEquals(_chaseTarget, value)) return;
            Creature previous = _chaseTarget;
            _chaseTarget = value;

            if (previous == null && value != null && voice != null) voice.Alert();
        }

        private Creature _chaseTarget;

        private static string Describe(Creature c)
        {
            if (c == null) return "none";
            return $"{c.abstractCreature.creatureTemplate.type}#{c.abstractCreature.ID}";
        }

        public PounceState pounce
        {
            get { return move == null ? PounceState.None : move.Pounce.Stage; }
        }

        public int PounceTicks { get { return move == null ? 0 : move.Pounce.StageTicks; } }

        public ChargeState charge
        {
            get { return move == null ? ChargeState.None : move.Charge.Stage; }
        }
        public float ChargeWindup { get; internal set; }
        public float ChargeRush { get; internal set; }

        private float jumpVariation = 1f;
        private bool jumpVariationRolled;
        private bool deathHandover;
        private bool shortcutHandover;
        private int biteCooldown;

        private int stuckMoveTicks;
        private int stuckMoveUpdatedTick = -1;
        private int wedgeRearms;
        private int wedgeSaturatedTicks;
        private const int STUCK_MOVE_TICKS_MAX = 600;
        private float squeezeProgressGoalDist = -1f;
        private int squeezeProgressTick = -1;
        private float spineRestSpan;
        private float spinePushRest;
        private float spineFoldFac = 1f;
        private bool spineFoldLoggedState;
        private bool spineFoldEverLogged;
        private int spanFloorGraceTicks;
        private int spanBelowFloorTicks;
        private bool spanFloorRunning;
        private int lastSqueezeBand;
        private bool spawnReported;

        private MovementConnection latchedConnection;
        private WorldCoordinate latchedDestination;
        private int latchTicks;
        private int latchRejects;

        private float[] spineElasticBase;
        private bool spineRelaxed;
        private int spineRecoverTicks;

        public float TurnProgress { get { return move == null ? 0f : move.Turn.Progress; } }

        public bool TurnActive { get { return move != null && move.Turn.Running; } }

        public bool TurnStalled { get { return false; } }

        public int TurnSwapCount { get { return 0; } }

        internal const int GATE_PAUSE_WHEN_DISABLED = 40;

        private const int NEVER_LOGGED = -1000000;

        private bool starveReported;

        private const int LANDING_LOG_GAP = 120;
        private int lastLandingLogTick = NEVER_LOGGED;

        private bool ShouldLogLanding(bool always)
        {
            if (!always && GameTick - lastLandingLogTick < LANDING_LOG_GAP)
            {
                return false;
            }
            lastLandingLogTick = GameTick;
            return true;
        }

        private int airTicks;
        private int groundTicks;
        private bool landingArmed;
        private int lastFallTicks;
        private float lastAirborneVelY;
        private float fallPeakY;
        private int PounceEndTick { get { return move == null ? NEVER_LOGGED : move.Pounce.EndTick; } }

        private const int LANDING_DISARM_GRACE = 3;

        private int lastStrandedLogTick = NEVER_LOGGED;
        private bool disabled;

        public MovementConnection lastFollowedConnection;
        private int lastFollowedConnectionTick = NEVER_LOGGED;
        private WorldCoordinate lastLoggedDestination;

        public Fox(AbstractCreature abstractCreature, string variantId)
            : base(abstractCreature, abstractCreature.world)
        {
            VariantId = variantId;
            voice = new FoxVoice(this);
            move = new FoxMoveEngine(this);

            try
            {
                FoxBreedParams p = P;

                float rad = 8f * p.BodySizeFac * p.BodyRadFac;
                float chunkMass = p.BodyMass / 3f;
                bodyChunks = new BodyChunk[3];
                for (int i = 0; i < 3; i++)
                {
                    bodyChunks[i] = new BodyChunk(this, i, new Vector2(0f, 0f), rad, chunkMass);
                }

                float link = 17f * p.BodyLengthFac * ((p.BodySizeFac + 1f) / 2f);
                spineRestSpan = link * 2f;
                spinePushRest = link * (1f + p.BodyStiffness);
                bodyChunkConnections = new BodyChunkConnection[3];
                bodyChunkConnections[0] = new BodyChunkConnection(
                    bodyChunks[0], bodyChunks[1], link, BodyChunkConnection.Type.Normal,
                    p.SpineElasticity, p.SpineWeightSymmetry);
                bodyChunkConnections[1] = new BodyChunkConnection(
                    bodyChunks[1], bodyChunks[2], link, BodyChunkConnection.Type.Normal,
                    p.SpineElasticity, p.SpineWeightSymmetry);
                bodyChunkConnections[2] = new BodyChunkConnection(
                    bodyChunks[0], bodyChunks[2], link * (1f + p.BodyStiffness),
                    BodyChunkConnection.Type.Push, 1f - Mathf.Lerp(0.9f, 0.5f, p.BodyStiffness), 0.5f);

                airFriction = p.PhysAirFriction;
                gravity = p.PhysGravity;
                bounce = p.PhysBounce;
                surfaceFriction = p.PhysSurfaceFriction;
                collisionLayer = 1;
                waterFriction = p.PhysWaterFriction;
                buoyancy = p.PhysBuoyancy;

                GoThroughFloors = p.PhysGoThroughFloors;
            }
            catch (Exception e)
            {
                disabled = true;
                FoxFault.Warn($"fox_spawn_failed variant={VariantId} err={e.GetType().Name} msg=\"{e.Message}\" " +
                            $"action=instance_disabled");
                bodyChunks = new BodyChunk[3];
                for (int i = 0; i < 3; i++)
                {
                    bodyChunks[i] = new BodyChunk(this, i, new Vector2(0f, 0f), 8f, 1f);
                }
                bodyChunkConnections = new BodyChunkConnection[0];
                FoxFault.Warn($"fox_spawn_fallback_body variant={VariantId} id={abstractCreature.ID} " +
                            $"chunks=3 rad=8 mass=1 conns=0 types=none " +
                            $"phys={(GetLocalGravity() == 0f ? "engine_defaults_weightless_will_hang_in_mid_air" : "set_before_the_throw")} " +
                            $"gravity={GetLocalGravity()} airFriction={airFriction} surfaceFriction={surfaceFriction} buoyancy={buoyancy} " +
                            $"note=disabled_instance_no_AC_fox_core_3_body");
            }
        }

        private int GameTick
        {
            get { return (room != null && room.game != null) ? room.game.clock : 0; }
        }

        public float SqueezeAmount
        {
            get
            {
                if (bodyChunks == null || bodyChunks.Length < 2) return 0f;
                FoxBreedParams p = P;
                if (p == null) return 0f;
                float min = Mathf.Clamp(p.SqueezeMinFac, 0.01f, 1f);
                if (min >= 0.999f) return 0f;
                return Mathf.Clamp01(Mathf.InverseLerp(1f, min, bodyChunks[1].terrainSqueeze));
            }
        }

        internal float DrawEnclosure
        {
            get
            {
                FoxBreedParams p = P;
                if (p == null || !p.SqueezeDrawFromTerrain) return 0f;

                int now = GameTick;
                if (now != drawEnclosureTick)
                {
                    drawEnclosureTick = now;
                    int n = (bodyChunks == null) ? 0 : bodyChunks.Length;
                    float target = (n <= 0 || room == null || room.aimap == null)
                        ? 0f
                        : (CorridorChunkCount() / (float)n) * Mathf.Clamp01(p.SqueezeDrawTerrainMax);
                    float rate = Mathf.Clamp(p.SqueezeDrawTerrainLerp, 0.01f, 1f);
                    drawEnclosure = (rate >= 1f) ? target : Mathf.Lerp(drawEnclosure, target, rate);
                    if (Mathf.Abs(drawEnclosure - target) < 0.001f) drawEnclosure = target;
                }
                return Mathf.Clamp01(drawEnclosure);
            }
        }

        private float drawEnclosure;
        private int drawEnclosureTick = int.MinValue;

        internal float RideCorridorAmount
        {
            get
            {
                FoxBreedParams p = P;
                if (p == null || p.BodyRideCorridorFac >= 1f) return 0f;

                int now = GameTick;
                if (now != rideCorridorTick)
                {
                    rideCorridorTick = now;
                    int n = (bodyChunks == null) ? 0 : bodyChunks.Length;
                    float target = (n <= 0 || room == null || room.aimap == null)
                        ? 0f
                        : CorridorChunkCount() / (float)n;
                    float rate = Mathf.Clamp(p.BodyRideCorridorLerp, 0.01f, 1f);
                    rideCorridor = (rate >= 1f) ? target : Mathf.Lerp(rideCorridor, target, rate);
                    if (Mathf.Abs(rideCorridor - target) < 0.001f) rideCorridor = target;
                }
                return Mathf.Clamp01(rideCorridor);
            }
        }

        private float rideCorridor;
        private int rideCorridorTick = int.MinValue;

        internal float DeathCollapseAmount
        {
            get
            {
                FoxBreedParams p = P;
                if (p == null || p.BodyRideDeathFac >= 1f) return 0f;

                int now = GameTick;
                if (now != deathCollapseTick)
                {
                    deathCollapseTick = now;
                    float target = DownTargetAmount(p);
                    int ticks = Math.Max(0, p.BodyRideDeathTicks);
                    float step = (ticks <= 0) ? 1f : 1f / ticks;
                    deathCollapse = Mathf.MoveTowards(deathCollapse, target, step);
                }
                return Mathf.Clamp01(deathCollapse);
            }
        }

        private float deathCollapse;
        private int deathCollapseTick = int.MinValue;

        private float DownTargetAmount(FoxBreedParams p)
        {
            if (dead) return 1f;
            if (!p.BodyRideStunCollapse || Consious) return 0f;
            if (!StunFallActive && stun < Math.Max(1, p.StunFallMinStun)) return 0f;
            return Mathf.Clamp01(p.BodyRideStunCollapseFac);
        }

        internal float DeathLimpAmount
        {
            get
            {
                FoxBreedParams p = P;
                if (p == null) return 0f;

                int now = GameTick;
                if (now != deathLimpTick)
                {
                    deathLimpTick = now;
                    float target = DownTargetAmount(p);
                    int ticks = Math.Max(0, p.DeathLimpTicks);
                    float step = (ticks <= 0) ? 1f : 1f / ticks;
                    deathLimp = Mathf.MoveTowards(deathLimp, target, step);
                }
                return Mathf.Clamp01(deathLimp);
            }
        }

        private float deathLimp;
        private int deathLimpTick = int.MinValue;

        internal bool WedgeActive { get { return wedgeHoldTicks > 0; } }

        internal int WedgeEscapeTick { get { return wedgeEscapeTick; } }
        private int wedgeEscapeTick = int.MinValue / 2;

        private bool fallLatched;

        private float rightFacingSign;

        private float lastSqueezeApplied = 1f;

        public float DrawSqueezeAmount
        {
            get { return Mathf.Max(SqueezeAmount, DrawEnclosure); }
        }

        internal bool StunFallActive { get { return stunFallActive; } }
        private bool stunFallActive;
        private int stunFallReleaseTick = int.MinValue / 2;

        private void UpdateStunPosture(FoxBreedParams p)
        {
            bool down = p.StunFallEnabled && !dead && stun >= Math.Max(1, p.StunFallMinStun);

            int tick = GameTick;
            if (down)
            {
                stunFallReleaseTick = tick + Math.Max(0, p.StunFallRecoverTicks);
            }
            else if (p.StunFallEnabled && !dead && tick < stunFallReleaseTick)
            {
                down = true;
            }

            if (down == stunFallActive) return;
            stunFallActive = down;

            ApplyBodyPhysics(p);
        }

        private void UpdateBlindState(FoxBreedParams p)
        {
            bool now = Blinded;
            if (now == wasBlinded) return;
            wasBlinded = now;

            if (now && p.BlindStunEnabled && !dead)
            {
                float f = Mathf.InverseLerp(20f, 400f, blind);
                int n = Mathf.RoundToInt(Mathf.Lerp(Math.Max(0, p.BlindStunTicksFar),
                                                    Math.Max(0, p.BlindStunTicksNear), f));
                if (n > 0)
                {
                    Stun(n);
                }
            }
        }

        private bool wasBlinded;

        private string lastDamageKind = "none";

        private float deathThroes;

        private void ClassifyDamage(BodyChunk hitChunk, Vector2? directionAndMomentum, DamageType type)
        {
            if (grabbedBy != null && grabbedBy.Count > 0 && grabbedBy[0].grabber != null)
            {
                lastDamageKind = "grabbed";
                return;
            }

            if (hitChunk != null && bodyChunks != null && bodyChunks.Length > 0
                && hitChunk == bodyChunks[0] && type == DamageType.Stab)
            {
                lastDamageKind = "headshot";
                return;
            }

            if (directionAndMomentum.HasValue && bodyChunks != null && bodyChunks.Length >= 3)
            {
                Vector2 blow = directionAndMomentum.Value;
                Vector2 facing = bodyChunks[0].pos - bodyChunks[2].pos;
                float cosHalf = Mathf.Cos(Mathf.Clamp(P == null ? 180f : P.DeathBehindConeDeg, 0f, 360f)
                                          * 0.5f * Mathf.Deg2Rad);
                if (blow.sqrMagnitude > 0.0001f && facing.sqrMagnitude > 0.0001f
                    && Vector2.Dot(blow.normalized, facing.normalized) > cosHalf)
                {
                    lastDamageKind = "behind";
                    return;
                }
            }

            lastDamageKind = "front";
        }

        private void ArmDeathThroes(FoxBreedParams p)
        {
            deathBehindActive = p.DeathBehindFadeEnabled && lastDamageKind == "behind";
            deathBehindTicks = 0;

            if (!p.DeathThroesEnabled) { deathThroes = 0f; return; }

            float fac;
            switch (lastDamageKind)
            {
                case "grabbed":  fac = p.DeathThroesGrabbedFac;  break;
                case "headshot": fac = p.DeathThroesHeadshotFac; break;
                case "behind":   fac = p.DeathThroesBehindFac;   break;
                default:         fac = 1f;                        break;
            }
            deathThroes = Mathf.Clamp01(Mathf.Max(0f, fac));
        }

        private void TickDeathThroes(FoxBreedParams p)
        {
            if (!p.DeathThroesEnabled || deathThroes <= 0f) return;
            if (bodyChunks == null || bodyChunks.Length < 2) return;

            float was = deathThroes;
            deathThroes = Mathf.Max(0f, deathThroes
                                        - UnityEngine.Random.value * Mathf.Max(0.0001f, p.DeathThroesDecay));

            float amp = Mathf.Pow(Mathf.Clamp01(was), 0.5f);
            if (Touching(bodyChunks[0]))
            {
                bodyChunks[0].vel += Custom.RNV() * (UnityEngine.Random.value * p.DeathThroesHeadForce * amp);
            }
            if (Touching(bodyChunks[1]))
            {
                bodyChunks[1].vel += Custom.RNV() * (UnityEngine.Random.value * p.DeathThroesBodyForce * amp);
            }

            if (deathThroes <= 0f)
            {
                return;
            }
        }

        private static bool Touching(BodyChunk c)
        {
            return c != null && (c.ContactPoint.x != 0 || c.ContactPoint.y != 0);
        }

        private bool deathBehindActive;
        private int deathBehindTicks;
        private bool deathBehindDoneLogged;

        public float DeathLayAmount
        {
            get
            {
                FoxBreedParams p = P;
                if (p == null) return 0f;

                bool all = p.DeathLayAllDeaths;
                int elapsed = all ? deadTicks : deathBehindTicks;
                if (!(all ? dead : deathBehindActive)) return 0f;

                int ticks = Math.Max(0, p.DeathBehindLayTicks);
                if (ticks <= 0) return 1f;
                return Mathf.Clamp01((float)elapsed / ticks);
            }
        }

        private int deadTicks;

        public float DeathFadeAlpha
        {
            get
            {
                if (!deathBehindActive) return 1f;
                FoxBreedParams p = P;
                if (p == null) return 1f;
                int ticks = Math.Max(0, p.DeathBehindFadeTicks);
                float min = Mathf.Clamp01(p.DeathBehindFadeMinAlpha);
                if (ticks <= 0) return min;
                return Mathf.Lerp(1f, min, Mathf.Clamp01((float)deathBehindTicks / ticks));
            }
        }

        private void TickDeathBehind(FoxBreedParams p)
        {
            if (!deathBehindActive) return;

            if (deathBehindTicks < int.MaxValue) deathBehindTicks++;

            int longest = Math.Max(Math.Max(0, p.DeathBehindFadeTicks), Math.Max(0, p.DeathBehindLayTicks));
            if (!deathBehindDoneLogged && deathBehindTicks >= longest)
            {
                deathBehindDoneLogged = true;
            }
        }

        private float camoAmount;
        private int camoHoldTicks;
        private string camoBreak = "none";
        private string camoStatePrinted;
        private int camoLogTick = -1000000;
        private bool camoEverArmed;
        private float camoPeak;

        public float CamoAmount { get { return camoAmount; } }

        public string CamoState
        {
            get
            {
                FoxBreedParams p = P;
                if (p == null || !p.CamoEnabled) return "off";
                if (camoHoldTicks > 0) return camoAmount > 0f ? "breaking" : "held";
                if (camoAmount <= 0f) return "visible";
                if (camoAmount >= Mathf.Clamp01(p.CamoHiddenThreshold)) return "hidden";
                return "arming";
            }
        }

        public override float VisibilityBonus
        {
            get
            {
                float baseBonus = base.VisibilityBonus;
                return CamoVisibility(baseBonus);
            }
        }

        private float CamoVisibility(float baseBonus)
        {
            if (camoAmount <= 0f) return baseBonus;
            FoxBreedParams p = P;
            if (p == null || !p.CamoEnabled) return baseBonus;

            float hidden = Mathf.Lerp(baseBonus, -Mathf.Clamp01(p.CamoVisibilityBonus),
                                      Mathf.Clamp01(camoAmount));
            return Mathf.Max(-1f, Mathf.Min(baseBonus, hidden));
        }

        public void BreakCamo(string reason)
        {
            FoxBreedParams p = P;
            if (p == null || !p.CamoEnabled) return;

            bool edge = camoHoldTicks <= 0 || camoBreak != reason;
            camoHoldTicks = Math.Max(camoHoldTicks, Math.Max(0, p.CamoBreakHoldTicks));
            camoBreak = reason;

            if (edge)
            {
                LogCamo(p, "break");
                camoStatePrinted = CamoState;
                camoLogTick = GameTick;
            }
        }

        private string CamoBreakTrigger(FoxBreedParams p)
        {
            if (dead) return "dead";
            if (p.CamoBreakOnStun && Stunned) return "stunned";
            if (p.CamoBreakOnAttack)
            {
                if (pounce != PounceState.None) return "pounce";
                if (charge != ChargeState.None) return "charge";
                if (move != null && move.Lunge.Lunging) return "lunge";
            }
            if (p.CamoBreakOnHurt)
            {
                HealthState hs = State as HealthState;
                if (hs != null && hs.health < p.CamoBreakHealth) return "hurt";
            }
            return null;
        }

        private void TickCamo(FoxBreedParams p)
        {
            if (p == null || !p.CamoEnabled)
            {
                if (camoAmount != 0f || camoHoldTicks != 0)
                {
                    camoAmount = 0f;
                    camoHoldTicks = 0;
                    camoBreak = "disabled";
                    camoStatePrinted = null;
                }
                return;
            }

            string trigger = CamoBreakTrigger(p);
            if (trigger != null) BreakCamo(trigger);
            else if (camoHoldTicks > 0) camoHoldTicks--;

            float target = (camoHoldTicks > 0)
                ? 0f
                : Mathf.Clamp01(Mathf.InverseLerp(p.CamoSpeedVisible, p.CamoSpeedHidden, runSpeed));

            float before = camoAmount;
            if (target > camoAmount)
                camoAmount = Mathf.Min(target, camoAmount + Mathf.Max(0f, p.CamoArmRate));
            else if (target < camoAmount)
                camoAmount = Mathf.Max(target, camoAmount - Mathf.Max(0f, p.CamoBreakRate));
            camoAmount = Mathf.Clamp01(camoAmount);

            if (camoAmount > camoPeak) camoPeak = camoAmount;
            if (camoAmount > 0f && !camoEverArmed)
            {
                camoEverArmed = true;
                LogCamo(p, "arm_start");
                camoStatePrinted = CamoState;
                camoLogTick = GameTick;
                return;
            }

            string state = CamoState;
            bool edge = state != camoStatePrinted;
            bool cleared = before > 0f && camoAmount <= 0f;
            int cadence = Math.Max(0, p.CamoLogTicks);
            bool due = cadence > 0 && camoAmount > 0f && GameTick - camoLogTick >= cadence;

            if (edge || cleared || due)
            {
                LogCamo(p, cleared ? "clear" : (edge ? "state" : "sample"));
                camoStatePrinted = state;
                camoLogTick = GameTick;
            }
        }

        private void LogCamo(FoxBreedParams p, string ev)
        {
        }

        public bool Grounded
        {
            get
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    if (bodyChunks[i].ContactPoint.y < 0) return true;
                }
                return false;
            }
        }

        public bool FullyAirborne
        {
            get
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    IntVector2 cp = bodyChunks[i].ContactPoint;
                    if (cp.x != 0 || cp.y != 0) return false;
                }
                return true;
            }
        }

        private bool InPounceWindow(FoxBreedParams p)
        {
            if (pounce != PounceState.None) return true;
            return GameTick - PounceEndTick < p.LandPounceGrace;
        }

        public override void PlaceInRoom(Room placeRoom)
        {
            base.PlaceInRoom(placeRoom);
            FoxBreedParams p = P;
            spanFloorGraceTicks = Math.Max(0, p.SpanFloorGraceTicks);
            spanBelowFloorTicks = 0;
        }

        public override void InitiateGraphicsModule()
        {
            if (graphicsModule == null) graphicsModule = new FoxGraphics(this);
            graphicsModule.Reset();
        }

        public override Color ShortCutColor()
        {
            return P.ColorTail;
        }

        public override void Update(bool eu)
        {
            TickCamo(P);

            TickEvasion();

            base.Update(eu);

            FoxParams.PollReload(GameTick);

            ApplySpineFold(P);

            if (disabled || room == null)
            {
                ReleaseClimbGravityClaim(disabled ? "disabled" : "no_room");
                ReleasePoleHold(P, disabled ? "disabled" : "no_room", true);
                ReleaseWallHold(P, disabled ? "disabled" : "no_room", true);
                if (move != null) move.AssertStandingBody(P, disabled ? "disabled" : "no_room", true);
                return;
            }

            if (!spawnReported)
            {
                spawnReported = true;
            }

            if (biteCooldown > 0) biteCooldown--;
            move.TickCooldownsEarly();

            move.Pounce.TickGatherRelease(P);
            TickAmbushGather(P);

            if (dead)
            {
                DieHandover(P);
                return;
            }

            if (enteringShortCut.HasValue)
            {
                ShortcutHandover(P);
                return;
            }
            if (shortcutHandover)
            {
                shortcutHandover = false;
            }

            UpdateSpineSprings(P);

            move.AssertStandingBody(P, "standing", false);

            if (AI == null)
            {
                if (!disabled)
                {
                    disabled = true;
                }
                StandDown(P, "no_ai");
                move.AssertStandingBody(P, "no_ai", true);
                return;
            }

            if (abstractCreature.state is FoxState fox_state)
            {
                int cycleLength = (room.world != null && room.world.rainCycle != null)
                    ? room.world.rainCycle.cycleLength : 0;
                fox_state.RealizedTick(P, cycleLength, VariantId);

                if (fox_state.ShouldStarveToDeath(P) && !starveReported)
                {
                    starveReported = true;
                    Die();
                    return;
                }
            }

            voice.Update();

            bool poleHeldAtTickStart = poleHeld;
            FoxSurface surfaceAtTickStart = surface;
            UpdateClimbing(P);

            UpdatePoleSurface(P);

            UpdateWallSurface(P, poleHeldAtTickStart, surfaceAtTickStart);

            UpdatePoleMeet(P);

            UpdateAirborneState(P);

            UpdateSwim(P);

            UpdateStunPosture(P);

            UpdateBlindState(P);

            UpdateLandingApproach(P);

            move.Turn.Guard(P);

            if (Consious)
            {
                Act(eu);
                move.Turn.Guard(P);
            }
            else
            {
                SetChaseTarget(null, "knocked_out");

                DropPrey("stunned");
            }

            move.TickActions(P);

            if (enteringShortCut.HasValue)
            {
                ShortcutHandover(P);
                return;
            }

            ApplyCorridorCentering(P);

            ApplyPoleHold(P);

            ApplyWallHold(P);

            UpdateTerrainSqueeze(P);
            PushOffShortcutMouth(P);
        }

        private void UpdateTerrainSqueeze(FoxBreedParams p)
        {
            if (bodyChunks == null) return;

            if (stuckMoveUpdatedTick != GameTick)
            {
                bool wedged = p.SqueezeArmWhenWedged
                              && Consious
                              && AI != null && AI.stuckTracker != null
                              && AI.stuckTracker.Utility() > p.SqueezeWedgeUtilityMin;
                if (wedged)
                {
                    if (wedgeHoldTicks < Math.Max(1, p.SqueezeWedgeMaxTicks))
                    {
                        wedgeHoldTicks++;
                        stuckMoveTicks = Math.Min(stuckMoveTicks + 1, STUCK_MOVE_TICKS_MAX);
                    }
                    else if (p.SqueezeWedgeRearmInCorridor
                             && (p.SqueezeWedgeMaxRearms <= 0 || wedgeRearms < p.SqueezeWedgeMaxRearms)
                             && BodyInCorridorTerrain())
                    {
                        wedgeHoldTicks = 1;
                        wedgeRearms++;
                        stuckMoveTicks = Math.Min(stuckMoveTicks + 1, STUCK_MOVE_TICKS_MAX);
                    }
                    else
                    {
                        stuckMoveTicks = 0;
                    }
                }
                else
                {
                    stuckMoveTicks = 0;
                    wedgeHoldTicks = 0;
                    wedgeRearms = 0;
                }
            }

            int escArm = Mathf.Clamp(p.SqueezeWedgeEscapeStuckTicks, 1, STUCK_MOVE_TICKS_MAX);
            if (p.SqueezeWedgeEscapeEnabled && Consious && stuckMoveTicks >= escArm)
            {
                wedgeSaturatedTicks++;
                if (wedgeSaturatedTicks >= Math.Max(1, p.SqueezeWedgeEscapeTicks)) ForceWedgeEscape(p);
            }
            else if (wedgeSaturatedTicks != 0)
            {
                wedgeSaturatedTicks = 0;
            }

            UpdateSqueezeProgress(p);

            float squeeze = 1f;
            bool slopes = true;
            if (p.SqueezeEnabled)
            {
                squeeze = Custom.LerpMap(stuckMoveTicks, p.SqueezeStartFrames, p.SqueezeFullFrames,
                                         1f, Mathf.Clamp(p.SqueezeMinFac, 0.01f, 1f));
                slopes = stuckMoveTicks < p.SqueezeSlopesOffFrames;
            }

            int reinflate = Consious ? Math.Max(0, p.SqueezeReinflateTicks) : 0;
            if (reinflate > 0 && squeeze > lastSqueezeApplied)
            {
                squeeze = Mathf.Min(squeeze, lastSqueezeApplied + 1f / reinflate);
            }
            lastSqueezeApplied = squeeze;
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                bodyChunks[i].terrainSqueeze = squeeze;
                bodyChunks[i].collideWithSlopes = slopes;
            }

            bool enterPull = move != null && move.Enter.HoldsSpinePull;
            if ((p.SqueezeSpinePull || enterPull) && bodyChunkConnections != null && bodyChunkConnections.Length > 1
                && bodyChunkConnections[0] != null && bodyChunkConnections[1] != null && room != null)
            {
                bool headLags = true;
                if (lastFollowedConnection != default(MovementConnection))
                {
                    Vector2 dest = room.MiddleOfTile(lastFollowedConnection.DestTile);
                    headLags = Vector2.Distance(bodyChunks[0].pos, dest)
                               > Vector2.Distance(bodyChunks[1].pos, dest);
                }
                bool stuck = p.SqueezeSpinePull && stuckMoveTicks > p.SqueezeStartFrames;
                bodyChunkConnections[0].type = (enterPull || (headLags && stuck))
                    ? BodyChunkConnection.Type.Pull : BodyChunkConnection.Type.Normal;
                bodyChunkConnections[1].type = (enterPull || stuck || (p.SqueezeSpinePull && p.SqueezeSpinePullAlways))
                    ? BodyChunkConnection.Type.Pull : BodyChunkConnection.Type.Normal;

            }

            EnforceSpanFloor(p, move.Turn.Running);

            int band = (squeeze > 0.999f) ? 0 : (slopes ? 1 : 2);
            if (band != lastSqueezeBand)
            {
                lastSqueezeBand = band;
            }
        }

        private void ForceWedgeEscape(FoxBreedParams p)
        {
            CorridorChunkCount();

            string acc = "no_room";
            if (room != null && room.aimap != null && bodyChunks != null && bodyChunks.Length > 0)
            {
                IntVector2 t = room.GetTilePosition(bodyChunks[0].pos);
                AItile ai = room.aimap.getAItile(t.x, t.y);
                acc = ai == null ? "null_tile" : ai.acc.ToString() + (ai.narrowSpace ? "+narrow" : "");
            }

            stuckMoveTicks = 0;
            wedgeEscapeTick = GameTick;
            wedgeHoldTicks = 0;
            wedgeRearms = 0;
            wedgeSaturatedTicks = 0;

            SetWeightApplied(p, true, "wedge_escape");
            ReleaseClimbGravityClaim("wedge_escape");
            ReleasePoleHold(p, "wedge_escape", false);
            ReleaseWallHold(p, "wedge_escape", false);
            inAllowedTerrainCounter = 0;

            if (AI != null) AI.ForceFreshDestination("wedge_escape");
        }

        private float GoalDistanceForSqueeze()
        {
            if (room == null || AI == null || AI.pathFinder == null) return -1f;
            WorldCoordinate dest = AI.pathFinder.GetDestination;
            if (!dest.TileDefined) return -1f;
            if (dest.room != room.abstractRoom.index) return -1f;
            return Vector2.Distance(bodyChunks[1].pos, room.MiddleOfTile(dest.Tile));
        }

        private void UpdateSqueezeProgress(FoxBreedParams p)
        {
            if (p.SqueezeProgressPx <= 0f || bodyChunks == null || bodyChunks.Length < 2) return;

            int tick = GameTick;

            float goalNow = GoalDistanceForSqueeze();
            if (goalNow < 0f)
            {
                return;
            }

            if (squeezeProgressTick < 0)
            {
                squeezeProgressGoalDist = goalNow;
                squeezeProgressTick = tick;
                return;
            }

            if (squeezeProgressGoalDist - goalNow >= p.SqueezeProgressPx)
            {
                stuckMoveTicks = 0;
                squeezeProgressGoalDist = goalNow;
                squeezeProgressTick = tick;
                return;
            }

            if (tick - squeezeProgressTick > Math.Max(1, p.SqueezeProgressWindow))
            {
                squeezeProgressGoalDist = goalNow;
                squeezeProgressTick = tick;
            }
        }

        private void Act(bool eu)
        {
            FoxBreedParams p = P;

            AI.Update();

            if (move.Pounce.Running && move.Pounce.LiveTicks > Math.Max(1, p.AttackStateCeilingTicks))
                move.Pounce.End(p, "overran_bite_gate");
            if (move.Charge.Running && move.Charge.LiveTicks > Math.Max(1, p.AttackStateCeilingTicks))
                move.Charge.End(p, "overran_bite_gate");

            if (move.Pounce.Running)
            {
                move.Guarded(p, move.Pounce);
                CarryObject(p, eu);
                return;
            }

            if (move.DriveOwner != null)
            {
                CarryObject(p, eu);
                return;
            }

            if (move.Charge.Running)
            {
                move.Guarded(p, move.Charge);
                CarryObject(p, eu);
                return;
            }

            if (EvadeStaggering)
            {
                CarryObject(p, eu);
                return;
            }

            if (move.SelectEnter(p))
            {
                CarryObject(p, eu);
                return;
            }

            TryBite(p);
            FollowPath(p);
            CarryObject(p, eu);
        }

        public bool Swimming
        {
            get
            {
                FoxBreedParams p = P;
                if (p == null || !p.SwimEnabled) return false;
                if (room == null || !room.water) return false;
                return Submersion > p.SwimSubmersionMin;
            }
        }

        private void UpdateSwim(FoxBreedParams p)
        {
            bool swimming = Swimming;
            if (swimming != wasSwimming)
            {
                wasSwimming = swimming;
            }
            if (!swimming)
            {
                swimDiving = false;
                return;
            }

            bool divedLastTick = swimDiving;
            swimDiving = false;

            if (!p.SwimSurfaceHold) return;

            if (p.SwimSurfaceHoldReleaseOnDive && divedLastTick)
            {
                LogSwimHoldSuppressed(p);
                return;
            }
            if (bodyChunks == null || bodyChunks.Length == 0 || bodyChunks[0] == null) return;
            BodyChunk head = bodyChunks[0];
            float sub = head.submersion;
            if (sub <= 0f || sub >= 1f) return;

            head.vel.y *= p.SwimSurfaceDamping;
            float clamp = Mathf.Max(0f, p.SwimSurfaceClamp);
            head.vel.y += Mathf.Clamp(room.FloatWaterLevel(head.pos) - head.pos.y, -clamp, clamp)
                          * p.SwimSurfacePull;
        }

        private bool SwimPaddle(FoxBreedParams p, MovementConnection conn)
        {
            if (!Swimming) return false;
            if (!conn.destinationCoord.TileDefined) return false;

            bool destWet = room.GetTile(conn.DestTile).AnyWater;
            if (!destWet)
            {
                if (p.SwimClimbOutFooting)
                {
                    inAllowedTerrainCounter = Math.Max(inAllowedTerrainCounter,
                                                       Math.Max(1, p.RegainFootingFrames) + 2);
                }
                swimDiving = false;
                LogSwimTick(p, conn, "climb_out", 0f, destWet, 0f);
                return false;
            }

            float dx = Mathf.Clamp(room.MiddleOfTile(conn.DestTile).x - mainBodyChunk.pos.x, -1f, 1f);
            float force = dx * p.SwimSpeed;
            mainBodyChunk.vel.x += force;

            float dive = SwimDive(p, conn);

            LogSwimTick(p, conn, dive < 0f ? "dive" : "paddle", force, destWet, dive);
            return true;
        }

        private float SwimDive(FoxBreedParams p, MovementConnection conn)
        {
            swimDiving = false;
            if (!p.SwimDiveEnabled || p.SwimDiveSpeed == 0f) return 0f;

            float surface = room.FloatWaterLevel(mainBodyChunk.pos);
            float limitY = surface - Mathf.Max(0f, p.SwimDiveDepthTiles) * 20f;
            float destY = room.MiddleOfTile(conn.DestTile).y;
            float wantY = Mathf.Max(destY, limitY);
            float dy = Mathf.Clamp(wantY - mainBodyChunk.pos.y, -1f, 1f);
            if (dy >= 0f) return 0f;

            float force = dy * p.SwimDiveSpeed;
            mainBodyChunk.vel.y += force;
            swimDiving = true;
            return force;
        }

        private bool swimDiving;

        private void LogSwimTick(FoxBreedParams p, MovementConnection conn, string reason,
                                 float force, bool destWet, float dive)
        {
        }

        private void LogSwimHoldSuppressed(FoxBreedParams p)
        {
        }

        private bool wasSwimming;

        private void FollowPath(FoxBreedParams p)
        {
            StandardPather pather = AI.pathFinder as StandardPather;
            if (pather == null)
            {
                if (!disabled)
                {
                    disabled = true;
                }
                return;
            }

            move.Turn.TickCooldown();

            if (ambushHoldTick == GameTick)
            {
                AmbushHoldStep(p);
                return;
            }

            WorldCoordinate originCoord = room.GetWorldCoordinate(mainBodyChunk.pos);
            MovementConnection asked = pather.FollowPath(originCoord, true);

            MovementConnection conn = ResolveLatchedStep(p, asked, pather.GetDestination);
            conn = PoleMeetStep(p, conn);
            if (conn == default(MovementConnection))
            {
                if (move.Turn.Running
                    && move.Turn.Drive(p, Vector2.zero, default(MovementConnection), false)) return;

                if (AI.stranded && GameTick - lastStrandedLogTick >= 200)
                {
                    lastStrandedLogTick = GameTick;
                }
                return;
            }

            if (AI.stuckTracker != null && AI.stuckTracker.moveBacklog != null)
            {
                bool repeat = AI.stuckTracker.moveBacklog.IsMoveInLog(conn, Math.Max(1, P.SqueezeBacklogSteps));
                stuckMoveTicks = repeat ? Math.Min(stuckMoveTicks + 1, STUCK_MOVE_TICKS_MAX) : 0;
                wedgeHoldTicks = 0;
                wedgeRearms = 0;
                stuckMoveUpdatedTick = GameTick;
                AI.stuckTracker.moveBacklog.ReportNewMove(conn);
            }

            if (pather.GetDestination != lastLoggedDestination)
            {
                lastLoggedDestination = pather.GetDestination;
            }

            lastFollowedConnection = conn;
            lastFollowedConnectionTick = GameTick;

            FoxMoveHandler handler = FoxMoveEngine.HandlerFor(conn.type);
            switch (handler)
            {
                case FoxMoveHandler.Reach:
                {
                    if (move.Reach.Running) { ClearLatch("reach"); return; }
                    var situation = new FoxMoveSituation { Conn = conn, ToTarget = Vector2.zero, Desired = Vector2.zero };
                    FoxMoveBid bid = move.Reach.Ask(p, situation);
                    if (bid.Ok)
                    {
                        move.Reach.Begin(p, situation, bid);
                        ClearLatch("reach");
                        return;
                    }
                    break;
                }

                case FoxMoveHandler.Shortcut:
                {
                    if (shortcutDelay > 0)
                    {
                        break;
                    }
                    enteringShortCut = conn.StartTile;
                    shortcutDelay = Math.Max(0, p.PipeReentryDelayTicks);
                    if (conn.type == MovementConnection.MovementType.NPCTransportation)
                    {
                        NPCTransportationDestination = conn.destinationCoord;
                    }
                    ClearLatch("shortcut");
                    return;
                }

                case FoxMoveHandler.None:
                {
                    break;
                }

                case FoxMoveHandler.Turn:
                    break;

                default:
                    break;
            }

            Vector2 target = room.MiddleOfTile(conn.DestTile);
            Vector2 toTarget = target - mainBodyChunk.pos;
            IntVector2 here = coord.Tile;
            AItile.Accessibility acc = room.aimap.getAItile(here.x, here.y).acc;
            float top = Mathf.Max(0f, p.BaseSpeed * p.TerrainSpeed(acc) * runSpeed * PolePulse(p));

            if (move.Turn.Drive(p, toTarget, conn, handler == FoxMoveHandler.Turn)) return;

            if (SwimPaddle(p, conn)) return;

            Vector2 desired = toTarget.normalized * top;

            bool stepDescends = conn.DestTile.y < conn.StartTile.y;
            bool clampApplies = p.DriveGroundDescentClamp || !stepDescends;
            DriveDescentClamped = Grounded && desired.y < 0f && clampApplies;
            if (DriveDescentClamped) desired.y = 0f;

            DriveSlopeFlattened = p.DriveSlopeHorizontal
                                  && (conn.type == MovementConnection.MovementType.Slope
                                      || conn.type == MovementConnection.MovementType.CeilingSlope)
                                  && desired.y != 0f;
            if (DriveSlopeFlattened) desired.y = 0f;

            if (p.DriveHeadLed && !PoleBacking(p, desired))
            {
                Drive(bodyChunks[0], desired, p.Acceleration);
                Vector2 travelDir = desired.sqrMagnitude > 0.0001f ? desired.normalized : Vector2.zero;
                DriveTrailing(p, bodyChunks[1], bodyChunks[0], 0,
                              top * p.DriveMidSpeedFac, p.Acceleration * p.DriveMidAccelFac, travelDir);
                DriveTrailing(p, bodyChunks[2], bodyChunks[1], 1,
                              top * p.DriveRearSpeedFac, p.Acceleration * p.DriveRearAccelFac, travelDir);
            }
            else
            {
                Drive(bodyChunks[0], desired, p.Acceleration);
                Drive(bodyChunks[1], desired * p.DriveMidSpeedFac, p.Acceleration * p.DriveMidAccelFac);
                Drive(bodyChunks[2], desired * p.DriveRearSpeedFac, p.Acceleration * p.DriveRearAccelFac);
            }

            if (!PoleStep(p, conn) && !WallStep(p, conn)) move.SelectClimb(p, conn, toTarget, desired);
        }

        private MovementConnection ResolveLatchedStep(FoxBreedParams p, MovementConnection asked,
                                                      WorldCoordinate pathDestination)
        {
            if (p.PathLatchTicks <= 0) return asked;
            if (latchedConnection == default(MovementConnection))
            {
                if (asked != default(MovementConnection)) AcceptStep(asked, pathDestination);
                return asked;
            }

            latchTicks++;

            if (!latchedConnection.destinationCoord.TileDefined) { ClearLatch("dest_undefined"); }
            else if (pathDestination != latchedDestination) { ClearLatch("goal_moved"); }
            else if (Custom.DistLess(mainBodyChunk.pos, room.MiddleOfTile(latchedConnection.DestTile),
                                     Mathf.Max(1f, p.PathLatchArrive)))
            {
                ClearLatch("arrived");
            }
            else if (latchTicks >= p.PathLatchTicks) { ClearLatch("timeout"); }

            if (latchedConnection == default(MovementConnection))
            {
                if (asked != default(MovementConnection)) AcceptStep(asked, pathDestination);
                return asked;
            }

            if (asked != default(MovementConnection)
                && asked.destinationCoord.Tile == latchedConnection.startCoord.Tile)
            {
                latchRejects++;
                if (p.PathLatchMaxRejects > 0 && latchRejects >= p.PathLatchMaxRejects)
                {
                    ClearLatch("rejects");
                    AcceptStep(asked, pathDestination);
                    return asked;
                }
                return latchedConnection;
            }
            if (asked != default(MovementConnection) && !SameStepTiles(asked, latchedConnection))
            {
                AcceptStep(asked, pathDestination);
                return asked;
            }
            return latchedConnection;
        }

        private static bool SameStepTiles(MovementConnection a, MovementConnection b)
        {
            if (!IsWalkedStepType(a.type) || !IsWalkedStepType(b.type)) return a == b;
            return a.startCoord.room == b.startCoord.room
                   && a.destinationCoord.room == b.destinationCoord.room
                   && a.startCoord.Tile == b.startCoord.Tile
                   && a.destinationCoord.Tile == b.destinationCoord.Tile;
        }

        private static bool IsWalkedStepType(MovementConnection.MovementType t)
        {
            return t == MovementConnection.MovementType.Standard
                   || t == MovementConnection.MovementType.LizardTurn;
        }

        private void AcceptStep(MovementConnection conn, WorldCoordinate pathDestination)
        {
            latchedConnection = conn;
            latchedDestination = pathDestination;
            latchTicks = 0;
            latchRejects = 0;
        }

        private void ClearLatch(string reason)
        {
            latchedConnection = default(MovementConnection);
            latchTicks = 0;
        }

        internal const string TURN_MODE_NONE = "none";
        internal const string TURN_MODE_FRONTPIVOT = "frontpivot";

        internal static bool TurnModeIs(FoxBreedParams p, string mode)
        {
            return string.Equals(p.TurnMode, mode, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TurnIsFrontPivot(FoxBreedParams p)
        {
            return TurnModeIs(p, TURN_MODE_FRONTPIVOT);
        }

        private float RestSpan()
        {
            if (bodyChunkConnections != null && bodyChunkConnections.Length >= 2
                && bodyChunkConnections[0] != null && bodyChunkConnections[1] != null)
            {
                return bodyChunkConnections[0].distance + bodyChunkConnections[1].distance;
            }
            return (bodyChunks != null && bodyChunks.Length >= 3)
                ? Vector2.Distance(bodyChunks[0].pos, bodyChunks[2].pos)
                : 1f;
        }

        private void CaptureSpineElasticity()
        {
            if (spineElasticBase != null || bodyChunkConnections == null) return;
            spineElasticBase = new float[bodyChunkConnections.Length];
            for (int i = 0; i < bodyChunkConnections.Length; i++)
            {
                spineElasticBase[i] = bodyChunkConnections[i] != null ? bodyChunkConnections[i].elasticity : 0f;
            }
        }

        private void ApplySpineElasticity(float fac)
        {
            if (spineElasticBase == null || bodyChunkConnections == null) return;
            int n = Math.Min(spineElasticBase.Length, bodyChunkConnections.Length);
            for (int i = 0; i < n; i++)
            {
                if (bodyChunkConnections[i] == null) continue;
                bodyChunkConnections[i].elasticity = spineElasticBase[i] * fac;
            }

            if (P != null && P.TurnPivotPushOff
                && bodyChunkConnections.Length > 2 && bodyChunkConnections[2] != null)
            {
                bodyChunkConnections[2].active = fac >= 0.999f;
            }
        }

        private void RelaxSpineForTurn(FoxBreedParams p)
        {
            CaptureSpineElasticity();
            if (spineElasticBase == null) return;
            float fac = Mathf.Clamp01(p.TurnSpineRelax);
            ApplySpineElasticity(fac);
            spineRecoverTicks = Math.Max(0, p.TurnSpineRecoverTicks);
            if (spineRelaxed) return;
            spineRelaxed = true;
        }

        private void ReleaseSpineAfterTurn(FoxBreedParams p)
        {
            if (!spineRelaxed) return;
            spineRecoverTicks = Math.Max(0, p.TurnSpineRecoverTicks);
            if (spineRecoverTicks <= 0) UpdateSpineSprings(p);
        }

        private void UpdateSpineSprings(FoxBreedParams p)
        {
            if (bodyChunkConnections == null || bodyChunkConnections.Length == 0) return;
            CaptureSpineElasticity();

            if (move != null && move.Turn.Running)
            {
                RelaxSpineForTurn(p);
                return;
            }

            int total = Math.Max(1, p.TurnSpineRecoverTicks);
            float fac = 1f;
            if (spineRelaxed && spineRecoverTicks > 0)
            {
                spineRecoverTicks--;
                float k = 1f - (float)spineRecoverTicks / total;
                fac = Mathf.Lerp(Mathf.Clamp01(p.TurnSpineRelax), 1f, k);
            }
            ApplySpineElasticity(fac);
            if (spineRelaxed && spineRecoverTicks <= 0)
            {
                spineRelaxed = false;
            }
        }

        private void EnforceSpanFloor(FoxBreedParams p, bool turnOwnsBody)
        {
            if (spanFloorRunning || bodyChunks == null || bodyChunks.Length < 3) return;

            if (turnOwnsBody) { spanBelowFloorTicks = 0; return; }
            if (spanFloorGraceTicks > 0) { spanFloorGraceTicks--; spanBelowFloorTicks = 0; return; }

            float minFac = Mathf.Clamp01(p.SqueezeSpanMinFac);
            if (minFac <= 0f || spineRestSpan <= 1f) { spanBelowFloorTicks = 0; return; }

            float floorPx = spineRestSpan * minFac;
            float fold = Mathf.Clamp(spineFoldFac, FOLD_FAC_MIN, 1f);
            float floorWorkPx = floorPx * fold;
            Vector2 off = bodyChunks[2].pos - bodyChunks[0].pos;
            float span = off.magnitude;
            if (span >= floorPx) { spanBelowFloorTicks = 0; return; }

            bool act = span < floorWorkPx;

            if (act && bodyChunkConnections != null && bodyChunkConnections.Length > 1
                && bodyChunkConnections[0] != null && bodyChunkConnections[1] != null)
            {
                bodyChunkConnections[0].type = BodyChunkConnection.Type.Normal;
                bodyChunkConnections[1].type = BodyChunkConnection.Type.Normal;
            }

            float step = Mathf.Max(0f, p.SpanFloorCorrectPx);
            if (act && step > 0f && !turnOwnsBody && span > 0.01f
                && NeckDot(bodyChunks[0].pos, bodyChunks[1].pos, bodyChunks[2].pos) < 0f)
            {
                Vector2 headToRear = off / span;
                float along = Vector2.Dot(bodyChunks[1].pos - bodyChunks[0].pos, headToRear);
                float pull = Mathf.Min(step, span * 0.5f - along);
                if (pull > 0f)
                {
                    bodyChunks[1].pos += headToRear * pull;
                    bodyChunks[1].vel += headToRear * pull;
                }
            }
            else if (act && step > 0f && !turnOwnsBody)
            {
                Vector2 dir = span > 0.01f ? off / span : Dir(bodyChunks[0].pos, bodyChunks[1].pos);
                if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(1f, 0f);
                float need = Mathf.Min(step, floorWorkPx - span) * 0.5f;
                bodyChunks[0].pos -= dir * need;
                bodyChunks[0].vel -= dir * need;
                bodyChunks[2].pos += dir * need;
                bodyChunks[2].vel += dir * need;
            }

            bool squeezing = p.SqueezeEnabled && p.SqueezeSpinePull
                             && stuckMoveTicks > p.SqueezeStartFrames;
            if (squeezing) { spanBelowFloorTicks = 0; return; }

            int warnAt = p.SpanCollapseWarnTicks;
            if (warnAt <= 0 || ++spanBelowFloorTicks < warnAt) return;

            spanBelowFloorTicks = 0;
            spanFloorRunning = true;
            try
            {
                if (move.Turn.Running) move.Turn.End(p, "span_collapse");
                spineRecoverTicks = 0;
                ApplySpineElasticity(1f);
                spineRelaxed = false;
            }
            finally { spanFloorRunning = false; }
        }

        private const float FOLD_FAC_MIN = 0.2f;

        private void ApplySpineFold(FoxBreedParams p)
        {
            bool inCorridor = false;
            float fac = 1f;

            if (p != null && !disabled && room != null && room.aimap != null
                && bodyChunks != null && bodyChunks.Length >= 3)
            {
                inCorridor = BodyInCorridorTerrain();
                if (inCorridor) fac = Mathf.Clamp(p.CorridorFoldFac, FOLD_FAC_MIN, 1f);
            }

            spineFoldFac = fac;

            if (bodyChunkConnections != null && bodyChunkConnections.Length > 2
                && bodyChunkConnections[2] != null)
            {
                bodyChunkConnections[2].distance = spinePushRest * fac;
            }

            bool folding = inCorridor && fac < 0.999f;
            if (!spineFoldEverLogged || folding != spineFoldLoggedState)
            {
                spineFoldEverLogged = true;
                spineFoldLoggedState = folding;
            }
        }

        private void CarryThroughTurn(FoxBreedParams p, Vector2 carryBefore)
        {
            if (!p.TurnCarryRotate) return;
            if (grasps == null || grasps.Length == 0 || grasps[0] == null) return;
            PhysicalObject held = grasps[0].grabbed;
            if (held == null || held.room != room || held.bodyChunks == null) return;

            Vector2 delta = CarryPoint(p) - carryBefore;
            if (delta.sqrMagnitude < 0.0001f) return;

            bool eu = evenUpdate;
            for (int i = 0; i < held.bodyChunks.Length; i++)
            {
                held.bodyChunks[i].RelativeMoveFromOutsideMyUpdate(eu, delta);
            }
            held.bodyChunks[grasps[0].chunkGrabbed].vel = mainBodyChunk.vel;
        }

        private Vector2 CarryPoint(FoxBreedParams p)
        {
            return mainBodyChunk.pos
                 + Dir(bodyChunks[1].pos, mainBodyChunk.pos) * p.CarryHoldDistance;
        }

        public bool ReachActive
        {
            get { return move != null && move.Reach.Running; }
        }

        internal int tieBreakCommitTicks;
        internal IntVector2 tieBreakCommitDest = new IntVector2(int.MinValue, int.MinValue);
        internal WorldCoordinate tieBreakCommitGoal;

        internal int tieBreakStreak;
        internal IntVector2 tieBreakStreakTile = new IntVector2(int.MinValue, int.MinValue);

        internal float LaunchAngleDeg(Vector2 aim)
        {
            Vector2 d = aim - mainBodyChunk.pos;
            if (d.sqrMagnitude < 0.01f) return 0f;
            return Mathf.Atan2(Mathf.Abs(d.y), Mathf.Abs(d.x)) * Mathf.Rad2Deg;
        }

        internal bool LaunchTooSteep(FoxBreedParams p, float deg)
        {
            float cap = Mathf.Clamp(p.PounceMaxLaunchDeg, 1f, 90f);
            return cap < 90f && deg > cap;
        }

        internal bool HeadroomAbove(FoxBreedParams p)
        {
            if (room == null) return false;
            IntVector2 t = room.GetTilePosition(bodyChunks[0].pos);
            int tiles = Mathf.Max(1, p.LedgePounceHeadroomTiles);
            for (int i = 1; i <= tiles; i++)
            {
                if (room.GetTile(new IntVector2(t.x, t.y + i)).Solid) return false;
            }
            return true;
        }

        internal static Vector2 Dir(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float m = d.magnitude;
            return m > 0.0001f ? d / m : new Vector2(0f, 1f);
        }

        internal static void PullChunk(BodyChunk c, Vector2 toward, float force)
        {
            if (c == null || force <= 0f) return;
            Vector2 d = toward - c.pos;
            float m = d.magnitude;
            if (m < 0.0001f) return;
            c.vel += d / m * Mathf.Min(force, m);
        }

        internal static void Drive(BodyChunk c, Vector2 desired, float accel)
        {
            Vector2 delta = desired - c.vel;
            float m = delta.magnitude;
            if (m > accel && m > 0f) delta = delta / m * accel;
            c.vel += delta;
        }

        private void DriveTrailing(FoxBreedParams p, BodyChunk follower, BodyChunk leader,
                                   int connectionIndex, float speed, float accel, Vector2 travelDir)
        {
            float rest = (bodyChunkConnections != null
                          && connectionIndex >= 0 && connectionIndex < bodyChunkConnections.Length
                          && bodyChunkConnections[connectionIndex] != null)
                ? bodyChunkConnections[connectionIndex].distance
                : 0f;

            float gap = Vector2.Distance(follower.pos, leader.pos);
            bool crowded = gap < rest - Mathf.Max(0f, p.DriveTrailSlackPx);

            if (speed <= 0f) return;
            if (crowded) return;

            Vector2 toLeader = Dir(follower.pos, leader.pos);
            if (Grounded && toLeader.y < 0f) toLeader.y = 0f;

            Vector2 want = toLeader * speed;
            if (travelDir.sqrMagnitude > 0.0001f)
            {
                float along = Vector2.Dot(want, travelDir);
                if (along < 0f)
                {
                    float keep = Mathf.Clamp01(p.DriveTrailBackstepFac);
                    Vector2 clamped = want - travelDir * (along * (1f - keep));
                    want = clamped;
                }
            }
            Drive(follower, want, accel);
        }

        internal Vector2 BodyFlightVelocity()
        {
            Vector2 sum = Vector2.zero;
            float mass = 0f;
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                sum += bodyChunks[i].vel * bodyChunks[i].mass;
                mass += bodyChunks[i].mass;
            }
            return (mass > 0.0001f) ? sum / mass : Vector2.zero;
        }

        internal void AlignBodyToVelocity(FoxBreedParams p, Vector2 v, string phase)
        {
            if (p.PounceAlignRate <= 0f || bodyChunks == null || bodyChunks.Length < 3) return;

            float speed = v.magnitude;
            if (speed < Mathf.Max(0.01f, p.PounceAlignMinSpeed)) return;

            Vector2 want = v / speed;

            float fade = 1f - Mathf.Clamp01(LandingPose) * Mathf.Clamp01(p.PounceAlignGroundFade);

            float maxDown = Mathf.Abs(p.PounceAlignMaxDownDeg);
            float downDeg = -Mathf.Asin(Mathf.Clamp(want.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (maxDown < 90f && downDeg > maxDown)
            {
                float hs = (Mathf.Abs(want.x) > 0.0001f) ? Mathf.Sign(want.x) : 1f;
                float r = maxDown * Mathf.Deg2Rad;
                want = new Vector2(Mathf.Cos(r) * hs, -Mathf.Sin(r));
                downDeg = maxDown;
            }

            Vector2 axis = bodyChunks[0].pos - bodyChunks[2].pos;
            float span = axis.magnitude;
            if (span < 1f) return;
            Vector2 cur = axis / span;

            float cross = cur.x * want.y - cur.y * want.x;
            float dot = Vector2.Dot(cur, want);
            float err = Mathf.Atan2(cross, dot);

            float maxStep = Mathf.Abs(p.PounceAlignMaxDeg) * Mathf.Deg2Rad;
            float step = Mathf.Clamp(err * Mathf.Clamp01(p.PounceAlignRate) * fade, -maxStep, maxStep);
            if (Mathf.Abs(step) < 0.0005f)
            {
                return;
            }

            Vector2 tangent = new Vector2(-cur.y, cur.x) * (step * span * 0.5f);

            float mHead = Mathf.Max(0.0001f, bodyChunks[0].mass);
            float mRear = Mathf.Max(0.0001f, bodyChunks[2].mass);
            float share = mRear / (mHead + mRear);
            bodyChunks[0].vel += tangent * (2f * share);
            bodyChunks[2].vel -= tangent * (2f * (1f - share));
        }

        internal bool ChargeEndsInRoom(FoxBreedParams p, Vector2 dir, float targetDist)
        {
            if (room == null) return true;
            float reach = Mathf.Min(Mathf.Abs(p.ChargeMaxSpeed) * Math.Max(1, p.ChargeMaxFrames),
                                    Mathf.Max(targetDist, p.ChargeDistance));
            Vector2 end = mainBodyChunk.pos + dir * reach;
            return InRoomBounds(end, Mathf.Max(0f, p.ChargeRoomMargin));
        }

        private float ChargeReach(FoxBreedParams p, float targetDist)
        {
            return Mathf.Min(Mathf.Abs(p.ChargeMaxSpeed) * Math.Max(1, p.ChargeMaxFrames),
                             Mathf.Max(targetDist, p.ChargeDistance));
        }

        internal bool ChargeLandingSurvivable(FoxBreedParams p, Vector2 dir, float targetDist,
                                              out int sample, out IntVector2 landTile, out string landWhy)
        {
            sample = 0;
            landTile = new IntVector2(-1, -1);
            landWhy = "disabled";
            if (!p.ChargeRequireLanding) return true;
            if (room == null) { landWhy = "no_room"; return true; }

            int samples = p.ChargeLandingSamples;
            if (samples <= 0) { landWhy = "no_samples"; return true; }
            samples = Mathf.Min(samples, CHARGE_LANDING_SAMPLE_CAP);

            float reach = ChargeReach(p, targetDist);
            Vector2 v = dir * Mathf.Abs(p.ChargeMaxSpeed);
            Vector2 predicted;

            for (int i = 1; i <= samples; i++)
            {
                Vector2 at = mainBodyChunk.pos + dir * (reach * i / samples);
                sample = i;
                if (!PredictedLandingSurvivable(p, at, v, out predicted, out landTile, out landWhy))
                {
                    return false;
                }
            }
            return true;
        }

        private const int CHARGE_LANDING_SAMPLE_CAP = 32;

        internal void RestoreAfterCommittedState(FoxBreedParams p, string tag)
        {
            if (bodyChunkConnections == null || bodyChunkConnections.Length == 0) return;
            CaptureSpineElasticity();
            spineRecoverTicks = 0;
            ApplySpineElasticity(1f);
            if (spineRelaxed)
            {
                spineRelaxed = false;
            }
        }

        internal bool PredictedLandingInRoom(FoxBreedParams p, Vector2 v, out Vector2 predicted)
        {
            predicted = mainBodyChunk.pos + new Vector2(PredictedRange(p, v) * Mathf.Sign(v.x), 0f);
            if (room == null) return true;
            return predicted.x >= p.PounceRoomMargin
                && predicted.x <= room.PixelWidth - p.PounceRoomMargin;
        }

        internal bool PredictedLandingSurvivable(FoxBreedParams p, Vector2 v, out Vector2 predicted,
                                                 out IntVector2 landTile, out string landWhy,
                                                 out string landEntry)
        {
            return WalkLandingArc(p, mainBodyChunk.pos, v,
                                  p.PounceLandingCheckObstruction,
                                  p.PounceLandingRequireStandingRoom,
                                  out predicted, out landTile, out landWhy, out landEntry);
        }

        internal bool PredictedLandingSurvivable(FoxBreedParams p, Vector2 from, Vector2 v,
                                                 out Vector2 predicted,
                                                 out IntVector2 landTile, out string landWhy)
        {
            string ignoredEntry;
            return WalkLandingArc(p, from, v, false, false,
                                  out predicted, out landTile, out landWhy, out ignoredEntry);
        }

        private bool WalkLandingArc(FoxBreedParams p, Vector2 from, Vector2 v,
                                    bool checkObstruction, bool requireStandingRoom,
                                    out Vector2 predicted,
                                    out IntVector2 landTile, out string landWhy, out string landEntry)
        {
            landEntry = "n/a";
            predicted = from;
            landTile = new IntVector2(-1, -1);
            if (room == null) { landWhy = "no_room"; return true; }

            FoxArcModel arc = Arc(p);
            int mask = p.LedgePounceLipSurfaceMask;
            int sign = (v.x >= 0f) ? 1 : -1;
            IntVector2 start = room.GetTilePosition(from);
            landTile = start;

            Vector2 vel = v;
            float x = from.x, y = from.y;
            float surfY;
            string kind;
            Vector2 prev = from;

            for (int tick = 1; tick <= arc.MaxFrames; tick++)
            {
                arc.Step(ref vel, ref x, ref y, tick);
                predicted = new Vector2(x, y);
                IntVector2 t = room.GetTilePosition(predicted);
                landTile = t;

                if (t.x == start.x && t.y == start.y) { prev = predicted; continue; }

                if (t.x < 0 || t.x >= room.TileWidth) { landWhy = "left_room"; return false; }
                if (t.y < 0) { landWhy = "below_room"; return false; }
                if (room.water && t.y <= room.defaultWaterLevel)
                {
                    landWhy = "water"; landEntry = "water"; return true;
                }
                if (t.y >= room.TileHeight) { prev = predicted; continue; }

                if (FoxLandingSurface.At(room, t.x, t.y, sign, mask, out surfY, out kind))
                {
                    if (checkObstruction)
                    {
                        string face = FoxLandingSurface.EntryFaceOf(room, t.x, t.y, prev, predicted);
                        if (face != "above")
                        {
                            if (kind == "rock")
                            {
                                landWhy = "wall_strike_" + face;
                                return false;
                            }
                            prev = predicted;
                            continue;
                        }
                    }
                    if (requireStandingRoom && !FoxLandingSurface.OpenForBody(room, t.x, t.y + 1))
                    {
                        landWhy = "no_standing_room";
                        return false;
                    }
                    landWhy = "surface_" + kind;
                    landEntry = checkObstruction ? "above" : "unchecked";
                    return true;
                }
                prev = predicted;
            }

            int rows = Mathf.Clamp(p.PounceLandingScanTiles, 0, 200);
            IntVector2 end = landTile;
            for (int r = 1; r <= rows; r++)
            {
                int row = end.y - r;
                landTile = new IntVector2(end.x, row);
                if (row < 0) { landWhy = "below_room"; return false; }
                if (room.water && row <= room.defaultWaterLevel)
                {
                    landWhy = "water"; landEntry = "water"; return true;
                }
                if (FoxLandingSurface.At(room, end.x, row, sign, mask, out surfY, out kind))
                {
                    if (requireStandingRoom && !FoxLandingSurface.OpenForBody(room, end.x, row + 1))
                    {
                        landWhy = "no_standing_room";
                        return false;
                    }
                    landWhy = "surface_" + kind;
                    landEntry = checkObstruction ? "look_down" : "unchecked";
                    return true;
                }
            }
            landWhy = "no_surface";
            return false;
        }

        private float ArcGravity { get { return (room != null) ? gravity : 0.9f; } }

        private FoxArcModel Arc(FoxBreedParams p)
        {
            return FoxArcModel.Of(p, ArcGravity, airFriction);
        }

        internal float PredictedRange(FoxBreedParams p, Vector2 v)
        {
            return Arc(p).Range(v);
        }

        internal float PredictedHeightAt(FoxBreedParams p, Vector2 v, float dist)
        {
            return Arc(p).HeightAtDist(v, dist);
        }

        private float JumpVariation
        {
            get
            {
                if (jumpVariationRolled) return jumpVariation;
                jumpVariationRolled = true;
                FoxBreedParams p = P;
                float dev = VariationDev(p);
                if (dev <= 0.0001f || abstractCreature == null) { jumpVariation = 1f; return 1f; }
                UnityEngine.Random.State entry = UnityEngine.Random.state;
                UnityEngine.Random.InitState(abstractCreature.ID.RandomSeed);
                jumpVariation = JumpVariationOf(
                    Custom.ClampedRandomVariation(0.5f, dev, Mathf.Max(0.01f, p.PounceVariationK)));
                UnityEngine.Random.state = entry;
                return jumpVariation;
            }
        }

        internal float EffectiveLaunchSpeed(FoxBreedParams p)
        {
            return p.PounceLaunchSpeed * JumpVariation;
        }

        internal static float JumpVariationOf(float clampedRandom01)
        {
            return clampedRandom01 * 2f;
        }

        internal static float VariationDev(FoxBreedParams p)
        {
            return Mathf.Clamp(p.PounceVariationDev, 0f, 0.49f);
        }

        internal static float MaxJumpVariation(FoxBreedParams p)
        {
            return JumpVariationOf(Mathf.Clamp(0.5f + VariationDev(p), 0f, 1f));
        }

        internal static float MaxEffectiveLaunchSpeed(FoxBreedParams p)
        {
            return Mathf.Max(0f, p.PounceLaunchSpeed) * MaxJumpVariation(p);
        }

        public bool TryPounce(Vector2 targetPos)
        {
            if (EvadeStaggering) return false;
            FoxBreedParams p = P;
            var s = new FoxMoveSituation { Target = targetPos, Why = "prey" };
            FoxMoveBid bid = move.Pounce.Ask(p, s);
            if (!bid.Ok) return false;
            move.Pounce.Begin(p, s, bid);
            return true;
        }

        public bool TryCharge(Vector2 targetPos)
        {
            if (EvadeStaggering) return false;
            FoxBreedParams p = P;
            var s = new FoxMoveSituation { Target = targetPos, Why = "prey" };
            FoxMoveBid bid = move.Charge.Ask(p, s);
            if (!bid.Ok) return false;
            move.Charge.Begin(p, s, bid);
            return true;
        }

        private static Vector2 LaunchVelocity(FoxBreedParams p, Vector2 dir, float speed)
        {
            Vector2 v = dir * speed;
            v.y += speed * p.PounceLaunchLift;
            return v;
        }

        internal void SolveLaunchScaling(FoxBreedParams p, Vector2 dir, Vector2 targetPos,
                                        out float frac, out float wantDist, out float wantRise)
        {
            WantTerms(p, targetPos, 0f, out wantDist, out wantRise);

            if (!p.PounceRangeScaling || EffectiveLaunchSpeed(p) <= 0f) { frac = 1f; return; }

            float lo = Mathf.Clamp(p.PounceLaunchMinFrac, 0.05f, 1f);
            if (Reaches(p, dir, lo, wantDist, wantRise)) { frac = lo; return; }
            if (!Reaches(p, dir, 1f, wantDist, wantRise)) { frac = 1f; return; }

            float hi = 1f;
            for (int i = 0; i < 14; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Reaches(p, dir, mid, wantDist, wantRise)) hi = mid; else lo = mid;
            }
            frac = hi;
        }

        internal void WantTerms(FoxBreedParams p, Vector2 targetPos, float extraDist,
                                out float wantDist, out float wantRise)
        {
            Vector2 from = mainBodyChunk.pos;
            WantTermsOf(p, targetPos.x - from.x, targetPos.y - from.y, extraDist,
                        out wantDist, out wantRise);
        }

        internal static void WantTermsOf(FoxBreedParams p, float dx, float dy, float extraDist,
                                         out float wantDist, out float wantRise)
        {
            wantDist = Mathf.Abs(dx) * Mathf.Max(0f, p.PounceRangeOvershootFac)
                       + Mathf.Max(0f, extraDist);
            wantRise = (dy > 0f) ? dy + Mathf.Max(0f, p.PounceRiseMargin) : 0f;
        }

        internal static float MaxLeapGapPx(FoxBreedParams p)
        {
            float speed = MaxEffectiveLaunchSpeed(p);
            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed)) return 0f;
            FoxArcModel arc = FoxArcModel.Of(p, Mathf.Max(0.01f, p.PhysGravity), p.PhysAirFriction);

            float cap = 0f;
            for (int deg = -80; deg <= 80; deg += 2)
            {
                float rad = deg * Mathf.Deg2Rad;
                var flat = new Vector2(Mathf.Cos(rad) * speed, Mathf.Sin(rad) * speed + speed * p.PounceLaunchLift);
                cap = Mathf.Max(cap, arc.Range(flat));
            }
            cap = Mathf.Min(4000f, cap / Mathf.Max(0.05f, p.PounceRangeOvershootFac));
            if (!(cap > 0f)) return 0f;

            float best = 0f;
            float maxRise = Mathf.Clamp(p.LedgePounceMaxRise, 0f, MAX_SWEEP_RISE_PX);
            for (float rise = 0f; rise <= maxRise + 0.01f; rise += 10f)
            {
                for (float dx = 2f; dx <= cap; dx += 2f)
                {
                    float len = Mathf.Sqrt(dx * dx + rise * rise);
                    if (len <= 0.01f) continue;
                    var v = new Vector2(speed * dx / len, speed * rise / len + speed * p.PounceLaunchLift);
                    float wantDist, wantRise;
                    WantTermsOf(p, dx, rise, 0f, out wantDist, out wantRise);
                    if (arc.HeightAtDist(v, wantDist) >= wantRise)
                        best = Mathf.Max(best, dx);
                }
            }
            return best;
        }

        internal const float MAX_SWEEP_RISE_PX = 2000f;

        internal const int MAX_LIP_SCAN_COLUMNS = 200;

        internal FoxReachVerdict CanReachLeap(FoxBreedParams p, Vector2 aim, float extraDist)
        {
            Vector2 dir = Dir(mainBodyChunk.pos, aim);
            return Verdict(p, aim, LaunchVelocity(p, dir, EffectiveLaunchSpeed(p)), extraDist, "solved");
        }

        internal FoxReachVerdict CanReachLeap(FoxBreedParams p, Vector2 aim, float extraDist, Vector2 flown)
        {
            return Verdict(p, aim, flown, extraDist, "plan");
        }

        private FoxReachVerdict Verdict(FoxBreedParams p, Vector2 aim, Vector2 v, float extraDist, string arc)
        {
            var r = new FoxReachVerdict { Arc = arc };
            r.G = -1f;
            r.AirFric = -1f;
            WantTerms(p, aim, extraDist, out r.WantDist, out r.WantRise);
            r.MaxRange = -1f;
            r.MaxApex = -1f;
            r.HeightAt = PredictedHeightAt(p, v, r.WantDist);
            r.Ok = r.HeightAt >= r.WantRise;
            r.Evaluated = true;
            return r;
        }

        private bool Reaches(FoxBreedParams p, Vector2 dir, float frac, float wantDist, float wantRise)
        {
            Vector2 v = LaunchVelocity(p, dir, EffectiveLaunchSpeed(p) * frac);
            return PredictedHeightAt(p, v, wantDist) >= wantRise;
        }

        internal int GatherFramesFor(FoxBreedParams p, float frac)
        {
            int full = Math.Max(0, p.PounceGatherFrames);
            if (!p.PounceRangeScaling) return full;
            int min = Mathf.Clamp(p.PounceGatherFramesMin, 0, full);
            float lo = Mathf.Clamp(p.PounceLaunchMinFrac, 0.05f, 1f);
            float t = (lo >= 1f) ? 1f : Mathf.Clamp01((frac - lo) / (1f - lo));
            float frames = Mathf.Lerp(min, full, t);
            frames *= 1f + (1f - JumpVariation) * Mathf.Clamp01(p.PounceVariationGatherFac);
            return Math.Max(0, Mathf.RoundToInt(frames));
        }

        internal bool InRoomBounds(Vector2 pos, float margin)
        {
            if (room == null) return true;
            return pos.x >= margin && pos.x <= room.PixelWidth - margin
                && pos.y >= margin && pos.y <= room.PixelHeight - margin;
        }

        internal void TryBite(FoxBreedParams p)
        {
            if (biteCooldown > 0)
            {
                move.Lunge.End(p, "cooldown");
                return;
            }
            if (grasps != null && grasps.Length > 0 && grasps[0] != null) { move.Lunge.End(p, "carrying"); return; }
            Creature target = chaseTarget;
            if (target == null || target.room != room) { move.Lunge.End(p, "no_target"); return; }
            if (target.dead && !p.EatsCarrion) { move.Lunge.End(p, "dead_target"); return; }

            BodyChunk hit = null;
            float best = float.MaxValue;
            for (int i = 0; i < target.bodyChunks.Length; i++)
            {
                float d = Vector2.Distance(mainBodyChunk.pos, target.bodyChunks[i].pos)
                          - target.bodyChunks[i].rad;
                if (d < best) { best = d; hit = target.bodyChunks[i]; }
            }

            float sweep = Vector2.Distance(mainBodyChunk.lastPos, mainBodyChunk.pos);
            if (p.BiteSweptContact && sweep >= Mathf.Max(0.01f, p.BiteSweptMinSpeed))
            {
                for (int i = 0; i < target.bodyChunks.Length; i++)
                {
                    float d = DistanceToSegment(target.bodyChunks[i].pos,
                                                mainBodyChunk.lastPos, mainBodyChunk.pos)
                              - target.bodyChunks[i].rad;
                    if (d < best) { best = d; hit = target.bodyChunks[i]; }
                }
            }
            if (hit == null || best > p.BiteRadius)
            {
                move.Lunge.End(p, "out_of_band");
                return;
            }
            float reach = p.BiteContactRadius > 0f ? p.BiteContactRadius : p.BiteRadius;

            if (!p.BiteThroughWalls && room != null && !room.VisualContact(mainBodyChunk.pos, hit.pos))
            {
                move.Lunge.End(p, "no_line_of_sight");
                return;
            }

            Vector2 forward = Dir(bodyChunks[2].pos, bodyChunks[0].pos);
            Vector2 toPrey = Dir(bodyChunks[0].pos, hit.pos);
            float facingDot = Vector2.Dot(forward, toPrey);
            float needDot = (pounce != PounceState.None) ? p.BiteFacingPounceDot
                          : (charge != ChargeState.None) ? p.BiteFacingChargeDot
                          : p.BiteFacingDot;
            if (facingDot < needDot)
            {
                move.Lunge.End(p, "facing");
                return;
            }

            float gap = best - mainBodyChunk.rad;
            bool inContact = (p.BiteContactGap >= 0f && gap <= p.BiteContactGap) || best <= reach;
            if (!inContact)
            {
                move.Lunge.Drive(p, target, toPrey, best, reach);
                return;
            }

            Vector2 dir = Dir(mainBodyChunk.pos, hit.pos);
            target.Violence(mainBodyChunk, dir * p.BiteDamage * p.BiteMomentumFac, hit, null,
                            DamageType.Bite, p.BiteDamage, p.BiteStunBonus);
            biteCooldown = p.BiteCooldown;

            move.Lunge.End(p, "hit");
            AI?.ReportHuntProgress("bite");

            TryGrab(p, target, hit);
        }

        private static float DistanceToSegment(Vector2 pt, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.0001f) return Vector2.Distance(pt, a);
            float t = Mathf.Clamp01(Vector2.Dot(pt - a, ab) / len2);
            return Vector2.Distance(pt, a + ab * t);
        }

        private void TryGrab(FoxBreedParams p, Creature target, BodyChunk hit)
        {
            if (grasps == null || grasps.Length == 0) return;
            if (grasps[0] != null) return;

            bool gate = CanCarry(target);
            if (!gate)
            {
                return;
            }

            if (!target.CanBeGrabbed(this))
            {
                return;
            }

            Grab(target, 0, hit.index, Grasp.Shareability.CanOnlyShareWithNonExclusive,
                             p.GraspDominance * UnityEngine.Random.value,
                             overrideEquallyDominant: true, pacifying: true);
        }

        private void CarryObject(FoxBreedParams p, bool eu)
        {
            if (grasps == null || grasps.Length == 0 || grasps[0] == null) return;

            Grasp g = grasps[0];
            PhysicalObject held = g.grabbed;
            if (held == null || held.room != room)
            {
                DropPrey("gone");
                return;
            }

            if (Submersion > p.CarryWaterDropSubmersion)
            {
                DropPrey("water");
                return;
            }

            BodyChunk chunk = held.bodyChunks[g.chunkGrabbed];
            Vector2 carryPoint = CarryPoint(p);
            Vector2 relative = chunk.vel - mainBodyChunk.vel;

            chunk.vel = mainBodyChunk.vel;

            bool lagging = !enteringShortCut.HasValue && !TurnActive
                           && (relative.magnitude * chunk.mass > p.CarryLagDropMomentum
                               || !Custom.DistLess(carryPoint, chunk.pos, p.CarryLagDropDistance + chunk.rad));
            if (lagging)
            {
                string why = null;
                DropPrey(why);
                return;
            }

            Vector2 moveTo = carryPoint;
            float gap = Vector2.Distance(chunk.pos, carryPoint);
            if (p.CarryEasePx > 0f && gap > p.CarryEasePx)
            {
                moveTo = chunk.pos + Dir(chunk.pos, carryPoint) * p.CarryEasePx;
            }
            chunk.MoveFromOutsideMyUpdate(eu, moveTo);

            for (int i = 0; i < 2 && i < bodyChunks.Length; i++)
            {
                held.PushOutOf(bodyChunks[i].pos, bodyChunks[i].rad, g.chunkGrabbed);
            }
        }

        public void DropPrey(string reason)
        {
            if (grasps == null || grasps.Length == 0 || grasps[0] == null) return;

            ReleaseGrasp(0);
        }

        public void FinishMeal(Creature corpse, FoxBreedParams p)
        {
            if (corpse.State != null && p.EatMeatPerMeal > 0)
            {
                corpse.State.meatLeft = Math.Max(0, corpse.State.meatLeft - p.EatMeatPerMeal);
            }

            if (abstractCreature.state is FoxState fs)
            {
                string detail = null;
                fs.Meal(p, VariantId, "eat", detail);
            }
        }

        private const float ZERO_GRAVITY_MOVEMENT_THRESHOLD = 0.3f;

        public bool applyGravity = true;

        private int inAllowedTerrainCounter;

        private bool climbOwnsGravity;

        internal bool ClimbOwnsGravity { get { return climbOwnsGravity; } }

        internal void ReleaseClimbGravityClaim(string why)
        {
            if (!climbOwnsGravity) return;
            climbOwnsGravity = false;
        }

        private int wedgeHoldTicks;

        public bool IsWallClimber
        {
            get
            {
                if (ModManager.MMF && room != null && room.gravity <= ZERO_GRAVITY_MOVEMENT_THRESHOLD)
                {
                    return true;
                }
                return LegacyClimberRule(P);
            }
        }

        internal bool TileIsCorridorLike(IntVector2 t)
        {
            if (room == null || room.aimap == null || abstractCreature == null) return false;
            if (!room.aimap.TileAccessibleToCreature(t.x, t.y, abstractCreature.creatureTemplate)) return false;
            AItile ai = room.aimap.getAItile(t.x, t.y);
            if (ai == null) return false;
            if (ai.acc == AItile.Accessibility.Corridor) return true;
            return NarrowSpaceHere(ai, t);
        }

        private bool NarrowSpaceHere(AItile ai, IntVector2 t)
        {
            if (!P.CorridorNarrowIgnoreDiagonals) return ai.narrowSpace;
            if (!ai.narrowSpace) return false;
            bool h = room.GetTile(t.x - 1, t.y).Solid && room.GetTile(t.x + 1, t.y).Solid;
            bool v = room.GetTile(t.x, t.y - 1).Solid && room.GetTile(t.x, t.y + 1).Solid;
            return h || v;
        }

        internal int CorridorChunkCount()
        {
            if (bodyChunks == null || room == null || room.aimap == null) return 0;
            int n = 0;
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                if (TileIsCorridorLike(room.GetTilePosition(bodyChunks[i].pos))) n++;
            }
            return n;
        }

        internal int CorridorMinChunks(FoxBreedParams p)
        {
            int n = (bodyChunks == null || bodyChunks.Length == 0) ? 1 : bodyChunks.Length;
            return Math.Max(1, Math.Min(p.CorridorTerrainMinChunks, n));
        }

        private bool CorridorFootingMet(FoxBreedParams p)
        {
            if (inAllowedTerrainCounter >= p.RegainFootingFrames) return true;

            int entryThr = p.CorridorEntryFootingFrames;
            if (entryThr <= 0) return false;
            if (!p.CorridorFootingGate) return false;
            if (!CorridorEntryIntent()) return false;
            if (CorridorChunkCount() < 1) return false;
            return inAllowedTerrainCounter >= entryThr;
        }

        private bool CorridorEntryIntent()
        {
            if (move != null && move.Enter.Running) return true;
            MovementConnection step;
            return TryCorridorEntryStep(out step);
        }

        internal bool TryCorridorEntryStep(out MovementConnection step)
        {
            step = default(MovementConnection);
            if (room == null || room.aimap == null) return false;
            if (lastFollowedConnection == default(MovementConnection)) return false;
            if (GameTick - lastFollowedConnectionTick > CONN_INTENT_MAX_AGE_TICKS) return false;
            if (!TileIsCorridorLike(lastFollowedConnection.DestTile)) return false;
            step = lastFollowedConnection;
            return true;
        }

        private bool TryTravelAxes(out bool travelX, out bool travelY, out string src)
        {
            Vector2 unused;
            return TryTravelAxes(out travelX, out travelY, out src, out unused);
        }

        private bool TryTravelAxes(out bool travelX, out bool travelY, out string src,
                                   out Vector2 travelDir)
        {
            travelX = false;
            travelY = false;
            src = "none";
            travelDir = Vector2.zero;

            IntVector2 from, dest;
            if (move != null && move.Enter.Running && move.Enter.TryTravelTiles(out from, out dest))
            {
                src = "enter";
            }
            else if (lastFollowedConnection != default(MovementConnection)
                     && GameTick - lastFollowedConnectionTick <= CONN_INTENT_MAX_AGE_TICKS)
            {
                from = lastFollowedConnection.StartTile;
                dest = lastFollowedConnection.DestTile;
                src = "drive";
            }
            else
            {
                return false;
            }

            travelX = dest.x != from.x;
            travelY = dest.y != from.y;
            if (!travelX && !travelY)
            {
                src = "none";
                return false;
            }
            Vector2 delta = new Vector2(dest.x - from.x, dest.y - from.y);
            travelDir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.zero;
            return true;
        }

        internal RWCustom.IntVector2 LastShortcutExitTile { get { return lastShortcutExitTile; } }
        internal int LastShortcutExitTick { get { return lastShortcutExitTick; } }
        private RWCustom.IntVector2 lastShortcutExitTile;
        private int lastShortcutExitTick = NEVER_LOGGED;

        private const int CONN_INTENT_MAX_AGE_TICKS = 1;

        private bool BodyInCorridorTerrain()
        {
            FoxBreedParams p = P;
            if (p == null) return false;
            if (!p.CorridorFootingGate) return CorridorChunkCount() >= CorridorMinChunks(p);

            if (!LegacyClimberRule(p) && !p.ZeroGravityInCorridors)
                return CorridorChunkCount() >= CorridorMinChunks(p);

            return CorridorFootingMet(p);
        }

        private void UpdateClimbing(FoxBreedParams p)
        {
            if (!LegacyClimberRule(p) && !p.ZeroGravityInCorridors)
            {
                if (!applyGravity && !ReachActive && !poleOwnsGravity && !wallOwnsGravity)
                {
                    applyGravity = true;
                    ApplyBodyPhysics(p);
                }
                inAllowedTerrainCounter = 0;
                ReleaseClimbGravityClaim("climb_inert");
                return;
            }

            bool corridorOnly = !LegacyClimberRule(p);

            int chunksInLegalTerrain = 0;
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                IntVector2 t = room.GetTilePosition(bodyChunks[i].pos);
                if (corridorOnly)
                {
                    if (!TileIsCorridorLike(t)) continue;
                }
                else if (!room.aimap.TileAccessibleToCreature(t.x, t.y, abstractCreature.creatureTemplate))
                {
                    continue;
                }
                chunksInLegalTerrain++;
            }

            bool ramp = p.CorridorFootingGate
                        ? chunksInLegalTerrain > 0
                        : chunksInLegalTerrain >= CorridorMinChunks(p);
            bool entryIntent = p.CorridorFootingGate
                               && p.CorridorEntryFootingFrames > 0
                               && CorridorEntryIntent();
            if (ramp)
            {
                if (inAllowedTerrainCounter < 100) inAllowedTerrainCounter++;
            }
            else if (!entryIntent) inAllowedTerrainCounter = Math.Max(0, inAllowedTerrainCounter - 10);

            int noGrip = (graphicsModule as FoxGraphics)?.noGripCounter ?? 0;
            bool wantGravity = !CorridorFootingMet(p)
                               || noGrip > p.NoGripReleaseFrames;

            bool poleOwns = poleOwnsGravity;
            if (poleOwns && !wantGravity)
            {
                YieldPoleHoldToCorridor(p);
                poleOwns = false;
            }
            bool wallOwns = wallOwnsGravity;
            if (wallOwns && !wantGravity)
            {
                YieldWallHoldToCorridor(p);
                wallOwns = false;
            }
            bool holdOwns = poleOwns || wallOwns;
            bool edge = wantGravity != applyGravity && !ReachActive && !holdOwns;
            if (edge)
            {
                applyGravity = wantGravity;
                ApplyBodyPhysics(p);
            }

            if (!applyGravity && !ReachActive && !holdOwns) climbOwnsGravity = true;
            else ReleaseClimbGravityClaim(applyGravity ? "weight_restored"
                                          : poleOwns ? "pole_owns" : wallOwns ? "wall_owns" : "reach_owns");

            if (!applyGravity && !holdOwns) ClimbHug(p);
            ApplyFloorLeverage(p);
        }

        private void ApplyBodyPhysics(FoxBreedParams p)
        {
            if (applyGravity)
            {
                gravity = p.PhysGravity;
                airFriction = p.PhysAirFriction;
                bool slumped = (StunFallActive && p.StunFallEnabled) || (dead && p.StunFallFrictionOnDead);
                surfaceFriction = slumped
                    ? Mathf.Clamp(p.StunFallSurfaceFriction, 0f, 1f)
                    : p.PhysSurfaceFriction;
                bounce = p.PhysBounce;
                GoThroughFloors = p.PhysGoThroughFloors;
            }
            else
            {
                gravity = 0f;
                airFriction = 0.8f;
                surfaceFriction = 0.5f;
                bounce = p.PhysBounce;
                GoThroughFloors = true;
            }
        }

        private void ClimbHug(FoxBreedParams p)
        {
            MovementConnection conn = lastFollowedConnection;
            if (conn == default(MovementConnection)) return;

            Vector2 mid = room.MiddleOfTile(mainBodyChunk.pos);
            bool horizontal = conn.startCoord.x != conn.destinationCoord.x
                              && conn.startCoord.y == conn.destinationCoord.y;
            bool vertical = conn.startCoord.y != conn.destinationCoord.y
                            && conn.startCoord.x == conn.destinationCoord.x;

            if (horizontal && (IsTileSolid(0, -1) || IsTileSolid(0, 1)))
            {
                mainBodyChunk.vel.y -= (mainBodyChunk.pos.y - mid.y) * p.ClimbTileCenterFac;
            }
            else if (vertical && (IsTileSolid(-1, 0) || IsTileSolid(1, 0)))
            {
                mainBodyChunk.vel.x -= (mainBodyChunk.pos.x - mid.x) * p.ClimbTileCenterFac;
            }
        }

        private void ApplyCorridorCentering(FoxBreedParams p)
        {
            if (p == null || bodyChunks == null || room == null || room.aimap == null) return;
            if (abstractCreature == null) return;

            int n = bodyChunks.Length;
            if (n <= 0) return;

            float fac = Mathf.Clamp(p.CorridorCenterFac, 0f, 1f);
            bool act = fac > 0f && Consious;

            EnsureCenterScratch(n);

            int sources = 0, votesX = 0, votesY = 0;
            for (int i = 0; i < n; i++)
            {
                IntVector2 t = room.GetTilePosition(bodyChunks[i].pos);
                ccTile[i] = t;
                bool corr = TileIsCorridorLike(t);
                ccCorr[i] = corr;
                ccShaft[i] = corr && room.GetTile(t.x - 1, t.y).Solid && room.GetTile(t.x + 1, t.y).Solid;
                ccPipe[i] = corr && room.GetTile(t.x, t.y - 1).Solid && room.GetTile(t.x, t.y + 1).Solid;
                if (ccShaft[i] || ccPipe[i])
                {
                    sources++;
                    if (ccShaft[i]) votesX++;
                    if (ccPipe[i]) votesY++;
                }
            }

            ccBodyX = sources > 0 && votesX == sources;
            ccBodyY = sources > 0 && votesY == sources;
            ccConflict = sources > 0 && !ccBodyX && !ccBodyY;

            ccLineTileX = ccBodyX ? SharedCenterTile(n, true) : 0;
            ccLineTileY = ccBodyY ? SharedCenterTile(n, false) : 0;
            float lineX = ccBodyX ? room.MiddleOfTile(ccLineTileX, 0).x : 0f;
            float lineY = ccBodyY ? room.MiddleOfTile(0, ccLineTileY).y : 0f;

            bool travelX = false, travelY = false;
            string travelSrc = "none";
            if (p.CorridorCenterYieldToTravel) TryTravelAxes(out travelX, out travelY, out travelSrc);
            bool yieldX = travelX && ccBodyX;
            bool yieldY = travelY && ccBodyY;

            float reachPx = Mathf.Max(0f, p.CorridorCenterReachTiles) * 20f;
            bool reachArmed = (ccBodyX || ccBodyY) && !ccConflict && reachPx > 0f && BodyInCorridorTerrain();

            for (int i = 0; i < n; i++)
            {
                BodyChunk c = bodyChunks[i];
                room.MiddleOfTile(ccTile[i]);

                bool src = ccShaft[i] || ccPipe[i];
                bool reach = !src && reachArmed && WithinCenterReach(i, n, reachPx);
                bool eligible = act && !ccConflict && (ccBodyX || ccBodyY) && (src || reach);

                bool openX = ccBodyX && !room.GetTile(ccLineTileX, ccTile[i].y).Solid;
                bool openY = ccBodyY && !room.GetTile(ccTile[i].x, ccLineTileY).Solid;

                float lineOffX = ccBodyX ? c.pos.x - lineX : 0f;
                float lineOffY = ccBodyY ? c.pos.y - lineY : 0f;

                bool pullX = eligible && openX && !yieldX;
                bool pullY = eligible && openY && !yieldY;
                if (pullX) c.vel.x -= lineOffX * fac;
                if (pullY) c.vel.y -= lineOffY * fac;
            }
        }

        private int SharedCenterTile(int n, bool axisX)
        {
            float mean = 0f;
            int contributors = 0;
            for (int i = 0; i < n; i++)
            {
                if (!(ccShaft[i] || ccPipe[i])) continue;
                mean += axisX ? bodyChunks[i].pos.x : bodyChunks[i].pos.y;
                contributors++;
            }
            if (contributors == 0) return 0;
            mean /= contributors;

            int bestIdx = -1, bestVotes = -1;
            float bestDist = 0f;
            for (int i = 0; i < n; i++)
            {
                if (!(ccShaft[i] || ccPipe[i])) continue;
                int coord = axisX ? ccTile[i].x : ccTile[i].y;
                int votes = 0;
                for (int j = 0; j < n; j++)
                {
                    if (!(ccShaft[j] || ccPipe[j])) continue;
                    if ((axisX ? ccTile[j].x : ccTile[j].y) == coord) votes++;
                }
                Vector2 m = room.MiddleOfTile(ccTile[i]);
                float dist = Mathf.Abs((axisX ? m.x : m.y) - mean);
                if (bestIdx < 0 || votes > bestVotes || (votes == bestVotes && dist < bestDist))
                {
                    bestIdx = i;
                    bestVotes = votes;
                    bestDist = dist;
                }
            }
            return axisX ? ccTile[bestIdx].x : ccTile[bestIdx].y;
        }

        private bool WithinCenterReach(int i, int n, float reachPx)
        {
            float r2 = reachPx * reachPx;
            for (int s = 0; s < n; s++)
            {
                if (!(ccShaft[s] || ccPipe[s])) continue;
                if (s - i != 1 && i - s != 1) continue;
                if ((bodyChunks[i].pos - bodyChunks[s].pos).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        private IntVector2[] ccTile;
        private bool[] ccCorr, ccShaft, ccPipe;

        private bool ccBodyX, ccBodyY, ccConflict;
        private int ccLineTileX, ccLineTileY;

        private void EnsureCenterScratch(int n)
        {
            if (ccTile != null && ccTile.Length == n) return;
            ccTile = new IntVector2[n];
            ccCorr = new bool[n];
            ccShaft = new bool[n];
            ccPipe = new bool[n];
        }

        private void ApplyFloorLeverage(FoxBreedParams p)
        {
            if (p.FloorLeverage == 0f || !Grounded) return;
            mainBodyChunk.vel.y += p.FloorLeverage;
        }

        private bool IsTileSolid(int dx, int dy)
        {
            IntVector2 t = room.GetTilePosition(mainBodyChunk.pos);
            return room.GetTile(t.x + dx, t.y + dy).Solid;
        }

        private void UpdateAirborneState(FoxBreedParams p)
        {
            if (bodyChunks == null || bodyChunks.Length == 0) return;

            if (Submersion > 0f)
            {
                airTicks = 0;
                groundTicks = 0;
                if (landingArmed)
                {
                    landingArmed = false;
                }
                return;
            }

            if (poleHeld || wallHeld)
            {
                airTicks = 0;
                groundTicks = 0;
                if (landingArmed)
                {
                    landingArmed = false;
                }
                return;
            }

            if (FullyAirborne)
            {
                groundTicks = 0;
                if (airTicks < int.MaxValue) airTicks++;

                lastAirborneVelY = mainBodyChunk.vel.y;

                if (airTicks == 1 || mainBodyChunk.pos.y > fallPeakY) fallPeakY = mainBodyChunk.pos.y;

                if (!landingArmed && airTicks >= Math.Max(1, p.LandMinAirTicks))
                {
                    landingArmed = true;
                }
                return;
            }

            if (airTicks > 0) lastFallTicks = airTicks;
            airTicks = 0;
            groundTicks++;

            if (landingArmed && groundTicks > LANDING_DISARM_GRACE)
            {
                landingArmed = false;
            }
        }

        private void UpdateLandingApproach(FoxBreedParams p)
        {
            if (bodyChunks == null || bodyChunks.Length < 3 || room == null) return;

            if (StunFallActive && p.StunFallRightingOff)
            {
                LandingPose = Mathf.Max(0f, LandingPose - 0.2f);
                return;
            }

            if (poleHeld || wallHeld)
            {
                LandingPose = Mathf.Max(0f, LandingPose - 0.2f);
                return;
            }

            float fall = -bodyChunks[1].vel.y;
            if (Grounded)
            {
                LandingPose = Mathf.Max(0f, LandingPose - 0.2f);
                return;
            }
            bool genuinelyAirborne = airTicks >= Math.Max(0, p.LandRightAirMinTicks);
            bool airAlways = p.LandRightAirAlways && genuinelyAirborne;

            float fallRelease = Mathf.Max(0f, p.LandRightMinFallSpeed - Mathf.Max(0f, p.LandRightFallHysteresis));
            bool falling = fallLatched ? (fall >= fallRelease) : (fall >= p.LandRightMinFallSpeed);
            fallLatched = falling;

            bool inCorridor = BodyInCorridorTerrain();
            bool corridorSuppress = inCorridor && p.LandRightSuppressInCorridor;
            float suppressFac = corridorSuppress ? Mathf.Clamp01(p.LandRightSuppressFac) : 1f;
            bool suppressed = corridorSuppress && suppressFac < 1f;

            if (!falling && !airAlways)
            {
                LandingPose = Mathf.Max(0f, LandingPose - 0.2f);
                return;
            }

            IntVector2 tile = room.GetTilePosition(bodyChunks[1].pos);
            int proximity = room.aimap.getTerrainProximity(tile.x, tile.y);

            if (proximity < 0)
            {
                LandingPose = Mathf.Max(0f, LandingPose - 0.2f);
                float edgeStrength = airAlways ? p.LandRightStrength * AirRightFac(p) * suppressFac : 0f;
                if (edgeStrength > 0f) ApplyRightingCouple(p, edgeStrength, inCorridor, suppressed);
                return;
            }

            LandingPose = falling
                ? Mathf.Clamp01(1f - (float)proximity / Mathf.Max(1, p.LandRightProximity))
                : Mathf.Max(0f, LandingPose - 0.2f);

            float near = (falling && proximity <= p.LandRightProximity)
                ? p.LandRightStrength * LandingPose : 0f;
            float always = airAlways ? p.LandRightStrength * AirRightFac(p) : 0f;
            float strength = Mathf.Max(near, always) * suppressFac;
            if (strength <= 0f)
            {
                return;
            }
            ApplyRightingCouple(p, strength, inCorridor, suppressed);
        }

        private float AirRightFac(FoxBreedParams p)
        {
            float fac = Mathf.Clamp01(p.LandRightAirFac);
            if (pounce != PounceState.Propel || p.PounceAlignRate <= 0f) return fac;
            float alignAuthority = 1f - Mathf.Clamp01(LandingPose) * Mathf.Clamp01(p.PounceAlignGroundFade);
            return fac * (1f - alignAuthority);
        }

        private void ApplyRightingCouple(FoxBreedParams p, float strength, bool inCorridor, bool suppressed)
        {
            if (strength <= 0f) return;

            Vector2 axis = Dir(bodyChunks[2].pos, bodyChunks[0].pos);
            Vector2 perp = new Vector2(-axis.y, axis.x);

            float facingSign = Mathf.Sign(axis.x);
            if (p.LandRightAxisSignLatch)
            {
                if (Mathf.Abs(axis.x) >= Mathf.Max(0f, p.LandRightAxisSignBand))
                {
                    rightFacingSign = facingSign;
                }
                else if (rightFacingSign != 0f)
                {
                    facingSign = rightFacingSign;
                }
            }
            float cross = -axis.y * facingSign;

            float k = strength * cross;
            bodyChunks[0].vel += perp * k;
            bodyChunks[2].vel -= perp * k;
        }

        public override void TerrainImpact(int chunk, IntVector2 direction, float speed, bool firstContact)
        {
            FoxBreedParams p = P;

            if (!firstContact)
            {
                base.TerrainImpact(chunk, direction, speed, firstContact);
                return;
            }

            float softFac = Mathf.Max(0f, p.LandSoftFac);
            float safeSpeed = p.LandSafeSpeed * softFac;
            float damageSpeed = p.LandDamageSpeed * softFac;

            LandingPose = 0f;

            if (!landingArmed)
            {
                return;
            }

            landingArmed = false;
            int fallTicks = airTicks > 0 ? airTicks : lastFallTicks;
            lastFallTicks = fallTicks;
            float vy = lastAirborneVelY;

            float fallSpeed = vy < 0f ? -vy : 0f;

            if (InPounceWindow(p))
            {
                ShouldLogLanding(true);
                return;
            }

            if (fallSpeed <= safeSpeed)
            {
                return;
            }

            base.TerrainImpact(chunk, direction, speed, firstContact);

            float excess = fallSpeed - safeSpeed;
            int stunTicks = Mathf.RoundToInt(excess * p.LandStunPerSpeed);
            float damage = fallSpeed > damageSpeed
                ? (fallSpeed - damageSpeed) * p.LandDamagePerSpeed
                : 0f;

            if (stunTicks > 0) Stun(stunTicks);

            if (damage > 0f)
            {
                Violence(null, null, bodyChunks[Mathf.Clamp(chunk, 0, bodyChunks.Length - 1)], null,
                         DamageType.Blunt, damage, 0f);
            }

            if (ExtEnumBase.TryParse(typeof(SoundID), p.LandSound, false, out ExtEnumBase soundEnum)
                && soundEnum.Index != -1)
            {
                room.PlaySound((SoundID)soundEnum, mainBodyChunk, false,
                               Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(safeSpeed, damageSpeed, fallSpeed)),
                               1f);
            }

            ShouldLogLanding(true);
        }

        public override void SpitOutOfShortCut(RWCustom.IntVector2 pos, Room newRoom, bool spitOutAllSticks)
        {
            base.SpitOutOfShortCut(pos, newRoom, spitOutAllSticks);
            int want = Math.Max(0, P.PipeExitDelayTicks);
            if (want > shortcutDelay) shortcutDelay = want;
            lastShortcutExitTile = pos;
            lastShortcutExitTick = GameTick;
        }

        public override void Violence(BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk,
                                      Appendage.Pos hitAppendage, DamageType type, float damage, float stunBonus)
        {
            base.Violence(source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);

            if (evadeRecs != null) EvadeNoteHit(source);

            if (grasps != null && grasps.Length > 0 && grasps[0] != null)
            {
                string why = null;
                if (!Consious)
                {
                    DropPrey(why);
                }
                else if (damage >= P.DropPreyDamage)
                {
                    DropPrey(why);
                }
            }

            if (move.Pounce.Running && !Consious) move.Pounce.End(P, "knocked_out");
            if (move.Charge.Running && !Consious) move.Charge.End(P, "knocked_out");

            PoleHit(P, damage);

            if (damage > 0f && !dead) voice.Hurt();

            ClassifyDamage(hitChunk, directionAndMomentum, type);
        }

        public override void Die()
        {
            bool wasAlive = !dead;
            base.Die();
            if (wasAlive)
            {
                voice.Death();

                ArmDeathThroes(P);

                if (room != null && bodyChunks != null && bodyChunks.Length >= 3) DieHandover(P);
            }
        }

        private void DieHandover(FoxBreedParams p)
        {
            bool edge = !deathHandover;
            if (edge)
            {
                deathHandover = true;
                StandDown(p, "died");
                DropPrey("died");
            }

            TickDeathThroes(p);

            TickDeathBehind(p);

            move.AssertStandingBody(p, "dead", true);

            if (p.DeathAssertPhysics)
            {
                bool held = grabbedBy != null && grabbedBy.Count > 0;
                bool hadThroughFloors = held && GoThroughFloors;
                ApplyBodyPhysics(p);
                if (held && hadThroughFloors && !GoThroughFloors) GoThroughFloors = true;
            }

            if (deadTicks < int.MaxValue) deadTicks++;

            if (edge)
            {
            }
        }

        public bool EngineOwnsBody
        {
            get { return dead || enteringShortCut.HasValue; }
        }

        private string StandDown(FoxBreedParams p, string reason)
        {
            move.StandDownActions(p, reason);

            ReleaseClimbGravityClaim("stand_down_" + reason);
            ReleasePoleHold(p, "stand_down_" + reason, true);
            ReleaseWallHold(p, "stand_down_" + reason, true);

            string report = null;
            return report;
        }

        private void PushOffShortcutMouth(FoxBreedParams p)
        {
            if (shortcutDelay <= 0 || room == null || bodyChunks == null) return;
            if (enteringShortCut.HasValue || dead) return;
            if (!Consious) return;

            for (int i = 0; i < bodyChunks.Length; i++)
            {
                RWCustom.IntVector2 t = room.GetTilePosition(bodyChunks[i].pos);
                if (room.GetTile(t).Terrain != Room.Tile.TerrainType.ShortcutEntrance) continue;
                RWCustom.IntVector2 dir = room.ShorcutEntranceHoleDirection(t);
                if (dir.x == 0 && dir.y == 0) continue;
                bodyChunks[i].vel += dir.ToVector2() * p.PipeMouthShovePx;
            }
        }

        private void ShortcutHandover(FoxBreedParams p)
        {
            bool edge = !shortcutHandover;
            if (edge)
            {
                shortcutHandover = true;
                StandDown(p, "entering_shortcut");
            }

            move.AssertStandingBody(p, "entering_shortcut", true);

            if (edge)
            {
            }
        }

        private void AssertAsBuiltBody()
        {
            if (bodyChunks != null)
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    if (bodyChunks[i] == null) continue;
                    bodyChunks[i].terrainSqueeze = 1f;
                    bodyChunks[i].collideWithSlopes = true;
                }
            }
            if (bodyChunkConnections != null)
            {
                for (int i = 0; i < Math.Min(2, bodyChunkConnections.Length); i++)
                {
                    if (bodyChunkConnections[i] == null) continue;
                    bodyChunkConnections[i].type = BodyChunkConnection.Type.Normal;
                }
            }
        }
    }
}
