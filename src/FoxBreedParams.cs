using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SlugBase;
using UnityEngine;

namespace MotherMod
{
    public sealed class FoxBreedParams
    {
        public float Toughness = 1.0f;
        public float StunToughness = 0.9f;
        public float BiteResistance = 2.0f;
        public float BiteStunResistance = 2.5f;

        public float BodySize = 1.6f;
        public int   MeatPoints = 6;
        public float DangerousToPlayer = 0.45f;
        public float Scaryness = 1.2f;

        public float VisualRadius = 900f;
        public float WaterVision = 0.3f;
        public float ThroughSurfaceVision = 0.5f;
        public float MovementBasedVision = 0.4f;

        public float OffScreenSpeed = 0.3f;
        public int   AbstractedLaziness = 40;
        public float RoamInRoomChance = 0.12f;
        public float RoamBetweenRoomsChance = 0.09f;

        public int   Grasps = 1;
        public int   ShortcutSegments = 3;
        public bool  StowFoodInDen = true;
        public bool  CanSwim = false;
        public string WaterRelationship = "AirAndSurface";
        public float WaterPathingResistance = 4f;

        public float LoadedPerformanceCost = 50f;

        public readonly List<string> RoomAttraction = new List<string> { "LikesOutside", "Dark" };

        public readonly Dictionary<AItile.Accessibility, PathCost> Tiles =
            new Dictionary<AItile.Accessibility, PathCost>
            {
                { AItile.Accessibility.Floor,       new PathCost(1f,   PathCost.Legality.Allowed) },
                { AItile.Accessibility.CurvedFloor, new PathCost(1f,   PathCost.Legality.Allowed) },
                { AItile.Accessibility.Corridor,    new PathCost(1.5f, PathCost.Legality.Allowed) },
                { AItile.Accessibility.Climb,       new PathCost(4f,   PathCost.Legality.Unallowed) },
                { AItile.Accessibility.Wall,        new PathCost(4f,   PathCost.Legality.Unallowed) },
                { AItile.Accessibility.Ceiling,     new PathCost(4f,   PathCost.Legality.Unallowed) },
            };

        public readonly Dictionary<MovementConnection.MovementType, PathCost> Connections =
            new Dictionary<MovementConnection.MovementType, PathCost>
            {
                { MovementConnection.MovementType.Standard,          new PathCost(1f,   PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.OpenDiagonal,      new PathCost(2f,   PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.ReachOverGap,      new PathCost(2.5f, PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.ReachUp,           new PathCost(2f,   PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.DoubleReachUp,     new PathCost(3f,   PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.ReachDown,         new PathCost(2f,   PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.SemiDiagonalReach, new PathCost(2f,   PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.Slope,             new PathCost(1.5f, PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.DropToFloor,       new PathCost(8f,   PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.DropToWater,       new PathCost(20f,  PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.DropToClimb,       new PathCost(30f,  PathCost.Legality.Unallowed) },
                { MovementConnection.MovementType.LizardTurn,        new PathCost(10f,  PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.ShortCut,          new PathCost(1.5f, PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.NPCTransportation, new PathCost(25f,  PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.BetweenRooms,      new PathCost(10f,  PathCost.Legality.Allowed) },
                { MovementConnection.MovementType.OffScreenMovement, new PathCost(1f,   PathCost.Legality.Allowed) },
            };

        public readonly List<FoxRelationship> Relationships = FoxRelationship.Defaults();

        public float ConspecificPackIntensity = 0.3f;

        public int RelationshipsExpectedSkipped = 0;

        public string RelRouteAttacks = "prey";

        public string RelRouteSocialDependent = "prey";

        public bool DiscomfortEnabled = true;

        public float BodyMass = 3.2f;
        public float BodySizeFac = 0.9f;
        public float BodyRadFac = 1f;
        public float BodyLengthFac = 1f;
        public float BodyStiffness = 0.7f;

        public float SpineElasticity = 0.95f;
        public float SpineWeightSymmetry = 0.5f;

        public bool RouteHoleAvoid = true;
        public float RouteHolePenalty = 10.0f;
        public float RouteCorridorPenalty = 0.0f;
        public bool  RouteCorridorDetectSideways = true;
        public string RouteHoleLegality = "unwanted";
        public int RouteHoleProbeTiles = 1;
        public bool RouteObstacleEnabled = true;
        public float RouteObstacleWeight = 5.0f;
        public float RouteObstacleExponent = 3.0f;

        public bool RouteObstacleSkipWhenWedged = true;

        public int RouteObstacleEscapeMuteTicks = 200;
        public bool PounceRouteEnabled = true;
        public bool PounceRouteWhilePatrolling = true;
        public int PounceRoutePeekSteps = 10;
        public int PounceRouteMaxFinders = 4;
        public int PounceRouteItersPerTick = 100;
        public int PounceRouteMinTilesOrigin = 5;
        public int PounceRouteMinTilesDest = 3;
        public bool PounceRouteBlockNarrow = true;
        public int PounceRouteReplanTicks = 60;
        public string PounceRouteMaxLegality = "unwanted";
        public int MantleRouteScanTiles = 2;
        public int MantleRouteReplanTicks = 20;
        public float PartsSpineRevMinSpeed = 0.5f;
        public int PounceRouteSeedAttemptsPerTick = 4;
        public bool MantleRiseFromClimbEnds = true;
        public bool StepUpRampConnWalks = true;
        public float StepArcPeak = 0.32f;
        public float StepTargetFreezeFrac = 0.80f;
        public int StepSwingMinTicks = 9;
        public int PipeReentryDelayTicks = 100;
        public float PipeMouthShovePx = 4.0f;
        public int PipeExitDelayTicks = 80;
        public bool GaitWalkEnabled = true;
        public float GaitWalkTrotSpeed = 2.2f;
        public float StepGaitGapShort = 0.15f;
        public float StepGaitGapLong = 0.35f;
        public int PounceRouteRevalidationsMax = 8;
        public int PounceRouteHoldMaxTicks = 20;
        public float PounceRouteMinRise = 0f;
        public float PounceRouteMaxRise = 80f;
        public int SqueezeWedgeMaxTicks = 90;
        public bool SqueezeWedgeRearmInCorridor = true;
        public int SqueezeWedgeMaxRearms = 6;
        public bool SqueezeWedgeEscapeEnabled = true;
        public int SqueezeWedgeEscapeTicks = 120;

        public int SqueezeWedgeEscapeStuckTicks = 400;

        public int SqueezeReinflateTicks = 20;

        public int CorridorTerrainMinChunks = 2;
        public bool CorridorNarrowIgnoreDiagonals = true;
        public bool CorridorFootingGate = true;
        public int CorridorEntryFootingFrames = 2;

        public float CorridorCenterFac = 0.5f;

        public float CorridorCenterReachTiles = 1.25f;

        public bool CorridorCenterYieldToTravel = true;

        public float CorridorFoldFac = 0.45f;

        public bool SqueezeArmWhenWedged = true;
        public float SqueezeWedgeUtilityMin = 0f;
        public bool ZeroGravityInCorridors = true;
        public bool PounceRouteFlyLaunchVel = false;
        public bool TurnRearAnchorEnabled = true;
        public bool TurnBlockedProbeLevel = true;
        public int PounceRouteGoalSeedSnapRows = 5;
        public bool PounceRouteGoalSeedEnabled = true;
        public int PounceRouteGoalSeedPathFloor = 4;
        public float PounceRouteGoalSeedRadiusTiles = 6.0f;
        public float PounceRouteGoalSeedLateralTiles = 3.0f;
        public float PounceRouteGoalSeedMinGainTiles = 1.0f;
        public int PounceRouteGoalMinTilesOrigin = 2;
        public bool MantleRouteVeto = true;
        public bool MantleRouteVetoOnSilence = false;
        public bool MantleRouteVetoOnIllegalLanding = false;
        public bool MantleRouteEnabled = true;
        public float MantleRoutePenalty = 6.0f;
        public float MantleRouteMaxRise = 30.0f;

        public float WeightThreat = 1.0f;
        public float WeightPrey = 0.6f;
        public float WeightRain = 0.9f;
        public int   PathStepsPerFrame = 12;

        public int   RememberCreatureFrames = 1200;
        public float PreySureToGetDistance = 3f;
        public float PreySureToLoseDistance = 70f;
        public float PreyPersistenceBias = 2f;
        public float HearingSkill = 1f;

        public int   PreyGiveUpUnreachable = 900;
        public int   PreyGiveUpGhostGeneration = 90;
        public int   HuntPersistFrames = 400;
        public bool  HuntGhostPursue = true;

        public float PhysGravity = 0.9f;
        public float PhysSurfaceFriction = 0.5f;
        public float PhysAirFriction = 0.999f;
        public float PhysBounce = 0.1f;
        public bool PhysGoThroughFloors = false;
        public float PhysWaterFriction = 0.92f;
        public float PhysBuoyancy = 1.06f;

        public bool SwimEnabled = true;
        public float SwimSpeed = 0.2f;
        public float SwimSubmersionMin = 0.5f;
        public bool SwimSurfaceHold = true;
        public float SwimSurfaceDamping = 0.8f;
        public float SwimSurfacePull = 0.1f;
        public float SwimSurfaceClamp = 10f;
        public bool SwimClimbOutFooting = true;
        public int SwimLogTicks = 20;

        public bool SwimDiveEnabled = true;
        public float SwimDiveDepthTiles = 3f;
        public float SwimDiveSpeed = 0.2f;
        public bool SwimSurfaceHoldReleaseOnDive = true;

        public bool StunFallEnabled = true;
        public int StunFallMinStun = 10;
        public bool StunFallRightingOff = true;
        public float StunFallSurfaceFriction = 0.3f;
        public int StunFallRecoverTicks = 0;

        public bool BlindEnabled = true;
        public float BlindOmniFac = 0f;
        public float BlindHeadScatterPx = 400f;
        public int BlindHeadScatterTicks = 1;
        public bool BlindEyeFlicker = true;

        public bool BlindStunEnabled = true;

        public int BlindStunTicksNear = 30;

        public int BlindStunTicksFar = 6;

        public bool DeathThroesEnabled = true;
        public float DeathThroesDecay = 0.0125f;
        public float DeathThroesHeadForce = 8f;
        public float DeathThroesBodyForce = 4f;
        public float DeathThroesHeadshotFac = 1.6f;
        public float DeathThroesBehindFac = 0f;
        public float DeathThroesGrabbedFac = 0.35f;
        public float DeathHeadPitchDeg = -30f;
        public float DeathHeadPitchRate = 0.05f;

        public bool DeathBehindFadeEnabled = false;
        public int DeathBehindFadeTicks = 160;
        public float DeathBehindFadeMinAlpha = 0.35f;
        public int DeathBehindLayTicks = 40;
        public float DeathBehindLayLimbFac = 0.35f;

        public bool DeathLayAllDeaths = true;

        public float DeathBehindConeDeg = 90f;

        public int DeathLimpTicks = 40;

        public float DeathBodyFlexMaxDeg = 9f;

        public float DeathBodyBendFac = 0.25f;

        public float DeathBodyBendMax = 3f;

        public float DeathTailLimpFac = 0f;

        public float DeathTailRootDroop = 0.45f;

        public float DeathHeadGravity = 0f;

        public float BaseSpeed = 3.0f;
        public float Acceleration = 0.55f;
        public float SpeedFloor = 1f;
        public float SpeedCorridor = 0.85f;
        public float SpeedClimb = 0.55f;
        public float SpeedWall = 0.6f;
        public float SpeedCeiling = 0.5f;
        public float StepUpBoost = 2.4f;
        public float StepUpClearPx = 26f;
        public float StepUpClearTolerance = 1.15f;
        public float StepUpHeightThreshold = 12f;
        public int   StepUpCooldown = 12;
        public float StepUpVelYGate = 1f;

        public bool LedgePounceEnabled = true;
        public float LedgePounceMinRise = 30f;
        public float LedgePounceMaxRise = 80f;
        public int LedgePounceCooldown = 60;
        public float LedgePounceLeadFac = 0.8f;
        public int LedgePounceHeadroomTiles = 2;
        public float LedgePounceStandoffPx = 40f;
        public float LedgePounceLandMarginPx = 20f;

        public bool  LedgePounceAllowLevel = true;
        public float LedgePounceMinGapPx = 40f;
        public bool  LedgePounceAllowDescend = true;
        public float LedgePounceMaxDrop = 120f;

        public int LedgePounceDescendRows
        {
            get
            {
                if (LedgePounceAllowDescend)
                    return 1 + Mathf.CeilToInt(Mathf.Max(0f, LedgePounceMaxDrop) / 20f);
                return LedgePounceAllowLevel ? 1 : 0;
            }
        }

        public bool MantleEnabled = true;
        public float MantleMinRise = 10f;
        public float MantleMaxRise = 30f;
        public float MantleReachPx = 26f;
        public float MantleGripInsetPx = 6f;
        public float MantlePullGravities = 2.2f;
        public float MantleForwardGravities = 0.9f;
        public float MantleRearPullFac = 0.45f;
        public float MantleMaxRiseSpeed = 4f;
        public int MantleMaxTicks = 60;
        public int MantleCooldown = 30;
        public int MantleHeadroomTiles = 2;
        public int MantleProbeTiles = 1;
        public int MantleMaxScanTiles = 2;
        public bool  MantleFacePlatforms = true;
        public float MantleClearMarginPx = 3f;
        public float MantleFootAboveFac = 1.1f;
        public int MantleLogTicks = 10;

        public bool ReachEnabled = true;
        public int ReachMaxTicks = 30;
        public int ReachTicksPerTile = 10;
        public float ReachSpeed = 5f;
        public float ReachAccelFac = 2f;
        public float ReachArrivePx = 12f;
        public bool ReachGravityOff = true;
        public int ReachCooldown = 15;
        public int ReachLogTicks = 5;

        public bool EnterEnabled = true;
        public int EnterGatherFrames = 22;
        public float EnterGatherCompress = 0.12f;
        public float EnterHeadLead = 0.10f;
        public float EnterRearLag = 0.08f;
        public float EnterPull = 1.5f;
        public bool EnterSpinePull = true;
        public int EnterMaxFrames = 60;
        public int EnterCooldown = 15;
        public float EnterReachPx = 30f;

        public bool EnterThroughFloors = true;
        public bool EnterShortcutEnabled = true;
        public bool EnterShortcutNeedsGround = false;
        public int EnterShortcutScanTiles = 2;
        public int EnterShortcutNearTiles = 2;

        public int MoveActionLogTicks = 20;
        public int MoveSelectLogTicks = 120;

        public int PathOfferLogTicks = 200;

        public int LedgeApproachLogBurst = 12;

        public int LedgeApproachBurstGapTicks = 80;

        public int LedgePounceLipScanTiles = 0;

        private float _ledgePounceReachPx;

        private int _ledgePounceReachTiles = 1;

        private int _ledgePounceLipScanEffective = 1;
        public int LedgePounceLipScanEffective { get { EnsureLeapEnvelope(); return _ledgePounceLipScanEffective; } }

        internal const string SCAN_GATE_UNDERIVED = "derived_missing";

        private bool _leapEnvelopeDerived;

        internal void DeriveLeapEnvelope()
        {
            _leapEnvelopeDerived = true;
            try
            {
                _ledgePounceReachPx = Fox.MaxLeapGapPx(this);
                _ledgePounceReachTiles = Mathf.Clamp(Mathf.CeilToInt(
                    Mathf.Max(0f, _ledgePounceReachPx - Mathf.Max(0f, LedgePounceLandMarginPx)) / 20f) + 1,
                    1, Fox.MAX_LIP_SCAN_COLUMNS);
                bool scanOverridden = LedgePounceLipScanTiles > 0;
                _ledgePounceLipScanEffective = scanOverridden ? LedgePounceLipScanTiles
                                                             : _ledgePounceReachTiles;
            }
            catch (Exception e)
            {
                FoxFault.Warn($"leap_envelope_failed err={e.GetType().Name} msg=\"{e.Message}\" " +
                            $"action=scan_falls_back_to_one_column scan_gate={SCAN_GATE_UNDERIVED}");
            }
        }

        private void EnsureLeapEnvelope()
        {
            if (!_leapEnvelopeDerived) DeriveLeapEnvelope();
        }

        public float LedgePounceGatherSlackFac = 0f;

        public string LedgePounceLipSurfaces = FoxLandingSurface.Default;

        public int LedgePounceLipSurfaceMask = FoxLandingSurface.All;

        public float PounceMaxLaunchDeg = 75f;

        public bool SqueezeEnabled = true;
        public float SqueezeStartFrames = 10f;
        public float SqueezeFullFrames = 30f;
        public float SqueezeMinFac = 0.05f;
        public float SqueezeLimbFac = 0.45f;
        public float SqueezeBodyFac = 0.8f;
        public float SqueezeTailFac = 0.8f;
        public float SqueezeSpineLenFac = 0.8f;
        public bool SqueezeDrawFromTerrain = true;
        public float SqueezeDrawTerrainMax = 1f;
        public float SqueezeDrawTerrainLerp = 0.25f;
        public int SqueezeSlopesOffFrames = 30;
        public bool SqueezeSpinePull = true;
        public int SqueezeBacklogSteps = 20;
        public float SqueezeProgressPx = 12f;
        public int SqueezeProgressWindow = 20;
        public float SqueezeSpanMinFac = 0.70f;
        public float SpanFloorCorrectPx = 2f;
        public int SpanCollapseWarnTicks = 40;
        public int SpanLogTicks = 40;
        public int SpanLogFloorTicks = 10;
        public int SpanFloorGraceTicks = 20;
        public bool SqueezeSpinePullAlways = false;
        public float DriveMidSpeedFac = 0.85f;
        public float DriveMidAccelFac = 0.8f;
        public float DriveRearSpeedFac = 0.7f;
        public float DriveRearAccelFac = 0.6f;
        public bool  DriveHeadLed = true;
        public bool  DriveGroundDescentClamp = false;
        public bool  DriveSlopeHorizontal = true;
        public float DriveTrailSlackPx = 2f;
        public float DriveTrailBackstepFac = 0f;

        public float RunSpeedIdle = 0.35f;
        public float RunSpeedHunt = 1f;
        public float RunSpeedFlee = 1f;
        public float IdleWanderChance = 0.02f;
        public float RunSpeedIdleStillFac = 0.5f;
        public float IdleUtilityThreshold = 0.05f;
        public float IdleStayChance = 0.35f;
        public int   IdleSwitchMinFrames = 100;
        public int   IdleSwitchMaxFrames = 400;
        public int   IdleCommitWinStreak = 50;
        public int   IdleCommitLockMin = 1000;
        public int   IdleCommitLockMax = 4000;
        public float PanicUtilityThreshold = 0.95f;
        public int   FleeReevaluations = 15;
        public int   FleeMaxDistance = 30;

        public float BiteDamage = 1.1f;
        public float BiteStunBonus = 45f;
        public float BiteRadius = 45f;
        public int   BiteCooldown = 60;
        public float BiteMomentumFac = 2f;
        public bool  BiteThroughWalls = false;
        public float BiteContactRadius = 10f;
        public float BiteContactGap = 3.0f;
        public float BiteFacingDot = 0.3f;
        public float BiteFacingPounceDot = -0.2f;
        public float BiteFacingChargeDot = -0.2f;
        public bool  BiteSweptContact = true;
        public float BiteSweptMinSpeed = 3.0f;
        public float LungeSpeed = 0.35f;
        public float LungeMaxSpeed = 8f;
        public float CarryEasePx = 4f;

        public bool  PounceEnabled = true;
        public float PounceDistance = 170f;
        public float PounceMinDistance = 40f;
        public int   PounceCrouchFrames = 22;
        public int   PouncePropulsionFrames = 9;
        public int   PounceMaxFrames = 26;
        public float PounceSpeed = 10f;
        public float PounceLift = 0.5f;
        public int   PounceCooldown = 110;
        public int   PounceRecoveryStun = 18;
        public float PounceChance = 0.6f;
        public int   PounceRetryDelay = 10;
        public float PounceCrouchPullback = 0.08f;
        public float PounceThrustFalloff = 0.5f;
        public int   PounceGateRetryDelay = 5;

        public int   PounceGatherFrames = 22;
        public float PounceGatherPull = 0.2f;
        public float PounceGatherCompress = 0.12f;
        public float PounceGatherArch = 4f;
        public float PounceGatherReadyRise = 3f;
        public int   PounceGatherReleaseTicks = 8;

        public float PounceLaunchSpeed = 9f;
        public float PounceLaunchLift = 0.55f;
        public float PounceLaunchPop = 6f;
        public float PouncePitchCouple = 3f;
        public float PounceHeadLead = 0.10f;
        public float PounceRearLag = 0.08f;
        public float PounceBoost = 2f;
        public int   PounceBoostFrames = 3;

        public bool  PounceRangeScaling = true;
        public float PounceLaunchMinFrac = 0.45f;
        public float PounceRangeOvershootFac = 1.15f;
        public float PounceRiseMargin = 20f;
        public int   PounceGatherFramesMin = 8;

        public bool  PounceAttackEnabled = true;

        public float PounceVariationDev = 0.10f;
        public float PounceVariationK = 0.4f;
        public float PounceVariationGatherFac = 0.5f;

        public float PounceAlignRate = 0.25f;
        public float PounceAlignMaxDeg = 7f;
        public float PounceAlignMinSpeed = 3f;
        public float PounceAlignGroundFade = 1f;
        public float PounceAlignMaxDownDeg = 25f;

        public bool  PounceHindPlant = true;
        public int   PounceHindReleaseTicks = 4;

        public int   PounceFlightMaxFrames = 80;
        public float PounceFlightStretch = 4f;

        public int   PounceLandFrames = 8;
        public float PounceLandDamp = 0.5f;
        public float PounceLandCompress = 3f;

        public bool  PounceRoomClamp = true;
        public float PounceRoomMargin = 60f;

        public bool  PounceRequireLanding = true;
        public int   PounceLandingScanTiles = 10;

        public bool  PounceLandingCheckObstruction = true;

        public bool  PounceLandingRequireStandingRoom = true;

        public bool  ChargeEnabled = false;
        public float ChargeDistance = 260f;
        public float ChargeMinDistance = 70f;
        public float ChargeChance = 0.5f;
        public float ChargeFacingDot = 0.4f;
        public int   ChargeWindupFrames = 14;
        public float ChargeWindupPull = 0.25f;
        public float ChargeThrust = 1.1f;
        public int   ChargeThrustFrames = 26;
        public int   ChargeMaxFrames = 34;
        public float ChargeMaxSpeed = 14f;
        public float ChargeAimTrack = 0f;
        public bool  ChargeRequireGrounded = true;
        public int   ChargeRecoveryStun = 34;
        public int   ChargeCooldown = 150;
        public int   AttackStateCeilingTicks = 400;
        public int   ChargeGateRetryDelay = 5;
        public float ChargeRoomMargin = 60f;
        public bool  ChargeRequireLanding = true;
        public int   ChargeLandingSamples = 6;
        public float ChargeStallSpeed = 1.5f;
        public int   ChargeStallGrace = 6;

        public int   MoveLogTicks = 20;

        public int   PathLatchTicks = 14;
        public float PathLatchArrive = 10f;

        public int   PathLatchMaxRejects = 3;

        public bool  PathBodyTieBreak = true;

        public int   PathTieBreakMaxStreak = 8;

        public int   PathTiebreakCommitTicks = 6;

        public bool  PathTiebreakNormaliseTurn = true;

        public bool  PathTiebreakTravelPreference = true;

        public int   TurnFrames = 16;
        public float TurnTriggerDot = -0.3f;
        public float TurnTriggerMinHorizontal = 0.35f;
        public float TurnRate = 0.12f;
        public int   TurnCooldown = 40;
        public float TurnTuck = 0.6f;
        public bool  TurnSwap = true;

        public string TurnMode = "frontpivot";

        public float TurnPivotSpeed = 1.0f;
        public float TurnPivotFriction = 0.99f;
        public float TurnPivotLead = 0.35f;
        public float TurnPivotMidFac = 0.6f;
        public float TurnPivotBrake = 0.5f;
        public float TurnPivotJitter = 0.12f;
        public float TurnPivotStretchPx = 10f;
        public bool TurnPivotSpanClamp = true;
        public float TurnPivotCrossFac = 0.12f;
        public float TurnPivotFinish = 0.15f;
        public float TurnPivotReleaseDot = 0.5f;
        public float TurnPivotReleaseSpanFac = 0.6f;
        public float TurnPivotSwingPx = 6f;
        public float TurnPivotGoalDot = 0.5f;
        public int   TurnPivotMaxTicks = 60;
        public bool  TurnPivotPushOff = true;
        public float TurnPivotSpanFloorFac = 0.60f;

        public bool  TurnFocusHold = true;
        public float TurnFocusHoldDot = 0f;
        public int   TurnMinIntervalTicks = 40;

        public float TurnMaxStepPx = 4f;
        public int   TurnLogTicks = 4;
        public float TurnBrake = 0.35f;
        public int   TurnAbortAirTicks = 3;
        public bool  TurnInWater = true;
        public bool  TurnCarryRotate = true;
        public float TurnSpineRelax = 0f;
        public int   TurnSpineRecoverTicks = 8;
        public float TurnVerticalHold = 1f;
        public int   TurnHoldMaxTicks = 60;

        public float BodyTurnForeshorten = 0.6f;

        public bool DepthVerticalHold = true;

        public float DepthVerticalHoldBand = 0.30f;

        public bool DrawUpSignLatch = true;

        public float DrawUpSignDeadband = 0.08f;

        public float PounceTailWhip = 6f;

        public float LimbSweepSpeedGate = 0.1f;
        public int   LimbSweepMinGrip = 2;
        public float LimbSweepBodySpeedGate = 0.3f;

        public bool  EatInPlaceEnabled = false;

        public bool  DenFedEnabled = false;

        public bool  ConspecificAwareness = false;

        public int   RoamRadiusTiles = 20;
        public int   RoamAttemptsPerCandidate = 6;

        public int   HuntForgetFrames = 300;
        public int   HuntForgetCooldownTicks = 200;

        public float DenRainUtilityThreshold = 0.01f;
        public float DenHealthThreshold = 0.5f;
        public int DenGiveUpTicks = 120;

        public float VisionPerfectAngle = 0.8f;
        public float VisionPeripheralAngle = -0.25f;

        public float SenseRadius = 500f;
        public float SenseMaxScore = 0.55f;
        public bool SenseThroughWalls = true;
        public float SenseWallRadiusFac = 0.6f;
        public float SenseWallScoreFac = 0.5f;
        public int SenseOmniIntervalTicks = 10;
        public bool SenseOmniInLosFull = false;

        public bool ScentEnabled = true;
        public float ScentRadiusTiles = 75f;
        public float ScentMaxScore = 0.55f;
        public float ScentWallRadiusFac = 0.6f;
        public float ScentWallScoreFac = 0.5f;
        public int ScentIntervalTicks = 20;
        public float ScentPassiveFac = 0.3f;
        public float ScentAppetiteScoreFac = 0f;
        public float ScentAppetiteRangeFac = 1f;
        public float ScentSniffPitchDeg = 22f;
        public int ScentSniffTicks = 60;
        public int ScentSniffCooldownTicks = 200;

        public float CarryMaxMassRatio = 0.3f;
        public float GraspDominance = 0.8f;
        public float CarryHoldDistance = 25f;
        public float CarryLagDropDistance = 70f;
        public float CarryLagDropMomentum = 30f;
        public float CarryWaterDropSubmersion = 0.4f;
        public float DropPreyDamage = 0.3f;

        public int EatFrames = 160;
        public float EatSafeThreat = 0.35f;
        public float EatReach = 40f;
        public float EatPoseRate = 0.04f;
        public float EatCrouchDrop = 4f;
        public float EatHeadFac = 0.85f;
        public int EatMeatPerMeal = 1;
        public bool EatsCarrion = true;
        public float CarrionIntensityFac = 1.3f;
        public bool CarrionFromAllPrey = true;

        public float ConspecificSpacing = 12f;

        public float RoamThreatWeight = 1f;
        public float RoamNoiseWeight = 0.6f;
        public float RoamDistanceWeight = 0.35f;
        public float RoamPickJitter = 0.25f;
        public int RoamCandidates = 8;

        public int DenRestFrames = 400;
        public float DenFedPriority = 0.5f;

        public bool StalkEnabled = true;
        public float StalkDistance = 260f;
        public float RunSpeedStalk = 0.45f;
        public float StalkBreakDistance = 140f;
        public float StalkPreySpeedBreak = 6f;
        public float StalkCrouchFac = 0.5f;

        public float HungerPerCycle = 0.35f;
        public float StarveRegenFac = 0.2f;
        public float StarveAggressionFac = 2f;
        public float MealValue = 0.5f;
        public bool StarveLethal = false;

        public float LandSafeSpeed = 12f;
        public float LandDamageSpeed = 20f;
        public float LandStunPerSpeed = 4f;
        public float LandDamagePerSpeed = 0.06f;

        public float LandSoftFac = 1f;

        public int LandMinAirTicks = 3;

        public int LandPounceGrace = 20;
        public float LandRightStrength = 2.5f;
        public int LandRightProximity = 3;
        public float LandRightMinFallSpeed = 2f;

        public float LandRightFallHysteresis = 0.6f;

        public float LandRightSuppressFac = 0f;

        public bool LandRightAxisSignLatch = true;

        public float LandRightAxisSignBand = 0.12f;

        public bool LandRightAirAlways = true;
        public float LandRightAirFac = 0.4f;
        public int   LandRightAirMinTicks = 3;
        public bool  LandRightSuppressInCorridor = true;

        public string LandSound = "Lizard_Heavy_Terrain_Impact";

        public bool RepEnabled = true;
        public float RepCommunityWeight = 1f;
        public float RepMemoryWeight = 1f;
        public float RepPersonalityAmount = 0.25f;
        public float RepHostileThreshold = -0.5f;
        public float RepFriendlyThreshold = 0.5f;
        public float RepAggressionExponent = 1f;
        public bool RepFriendlyStayOutOfWay = false;
        public float RepDebugLike = -99f;
        public int RepLogMinInterval = 40;
        public bool RepMemoryFromAttacks = false;
        public float RepMemoryEventScale = 1f;
        public float RepMemoryDecay = 0.0005f;
        public float RepCommunityInfluence = 1f;

        public Color ColorBody = new Color(0x2B / 255f, 0x2B / 255f, 0x2B / 255f);
        public Color ColorTail = new Color(0x97 / 255f, 0x97 / 255f, 0x97 / 255f);
        public Color ColorTailRoot = new Color(0x56 / 255f, 0x56 / 255f, 0x56 / 255f);
        public Color ColorLegs = new Color(0x5E / 255f, 0x5E / 255f, 0x5E / 255f);
        public Color ColorPaws = new Color(0x45 / 255f, 0x45 / 255f, 0x45 / 255f);
        public Color ColorLegLower = new Color(0x77 / 255f, 0x77 / 255f, 0x77 / 255f);
        public Color ColorEye = new Color(0x1A / 255f, 0x1A / 255f, 0x1A / 255f);
        public float EyeInkTintFloor = 1f;
        public Color EyeHoleFloor = new Color(0x0A / 255f, 0x0A / 255f, 0x0A / 255f);
        public Color ColorHead = new Color(0x97 / 255f, 0x97 / 255f, 0x97 / 255f);

        public bool SandboxGlowEnabled = true;

        public Color SandboxGlowColor = new Color(0x80 / 255f, 0x80 / 255f, 0x80 / 255f);
        public bool SandboxHideArtCopy = true;

        public Color IconColor = new Color(1f, 1f, 1f);

        public float SandboxGlowAlpha = 0.5f;

        public int PoseLogTicks = 80;
        public int FeetLogTicks = 40;

        public int BodyCircles = 4;
        public float BodyMeshRadFac = 1.15f;
        public int BodyMeshPoints = 7;
        public float BodyProfileNeck = 0.80f;
        public float BodyProfileChest = 1.30f;
        public float BodyProfileWaist = 0.72f;
        public float BodyProfileHaunch = 1.00f;
        public float BodyFrontCap = 4.0f;
        public float BodyMeshOverlap = 0.08f;
        public float BodyRideHeight = 9f;

        public float BodyRideCorridorFac = 0.40f;

        public float BodyRideCorridorLerp = 0.06f;

        public float BodyRideDeathFac = 0.10f;

        public int BodyRideDeathTicks = 40;

        public bool BodyRideStunCollapse = true;

        public float BodyRideStunCollapseFac = 0.7f;

        public bool StunFallFrictionOnDead = true;

        public bool DeathAssertPhysics = true;

        public float DrawSmoothing = 0f;

        public float HeadSize = 1f;
        public bool HeadSprite = true;
        public string HeadSpriteElement = "FoxRef_HeadMask";
        public string HeadEyeElement = "FoxRef_HeadEye";
        public bool HeadSpriteMask = true;
        public float HeadSpriteScale = 1f;
        public float HeadSpriteAnchorX = 0.06f;
        public float HeadSpriteAnchorY = 0.31f;
        public float HeadDrop = 2.5f;
        public float HeadNeckSlack = 0.45f;
        public float HeadGravity = 0.35f;
        public float HeadLookFac = 0.55f;
        public float HeadPitchMax = 32f;
        public float HeadMuzzleDroop = 0.08f;
        public float HeadMuzzleLength = 10f;
        public float HeadMuzzleWidth = 0.62f;
        public float HeadMuzzleTip = 0.24f;
        public float HeadForeshorten = 0.55f;

        public float EyeLength = 5f;
        public float EyeThickness = 1.6f;
        public float EyeAlong = 2f;
        public float EyeUp = 1f;
        public float NeckStiffness = 0.5f;

        public float LimbLengthFac = 0.8f;
        public float LimbSize = 3f;
        public float LimbThickness = 3.0f;
        public float LimbKneeBend = 0.35f;
        public float LimbUpperFac = 0.5f;
        public float LimbPawLength = 3.5f;
        public float LimbPawWidth = 1.4f;
        public float LimbBendSmooth = 0.15f;
        public float LimbBendFlipDeadband = 0.15f;
        public bool BodySprite = true;
        public string BodySpriteElement = "FoxRef_BodyMask";
        public bool BodySpriteMask = true;
        public float BodySpriteScaleFac = 1f;
        public float BodySpriteStretch = 0.15f;
        public bool LegSprites = false;
        public string LegSpriteFront = "FoxRef_LegStraightMask";
        public string LegSpriteFrontFar = "FoxRef_LegDiagonalMask";
        public string LegSpriteHind = "FoxRef_LegFrontBMask";
        public string LegSpriteHindFar = "FoxRef_LegFrontAMask";
        public bool LegSpriteMask = true;
        public string TailSpriteElement = "FoxRef_TailMask";
        public float LegSpriteWidthFac = 1f;

        public bool PartsRig = true;
        public string PartsPrefix = "FoxTofu_";
        public bool PartsMask = false;

        public int TobyDraw = 1;
        public int TobyFrameTicks = 10;
        public float TobyFrameScale = 1f;
        public float TobyFrameFeetPx = 17f;
        public float PartsScaleFac = 1f;
        public float PartsRefSpine = 32.3f;
        public float PartsBodyReach = 1f;
        public float PartsBodyStretch = 0.6f;

        public bool PartsBodyMesh = true;
        public int PartsBodyQuads = 4;
        public int PartsChestQuads = 3;
        public float PartsBodyBendFac = 1f;
        public float PartsBodyBendMax = 3f;
        public float PartsBodyLag = 0.35f;
        public float PartsBodyLagFac = 1.2f;
        public float PartsBodyFlexSmooth = 0.3f;
        public float PartsBodySquashAmp = 0.35f;
        public float PartsBodySquashLenGain = 2.5f;
        public float PartsBodySquashGather = 0.5f;
        public float PartsBodySquashFlight = 0.4f;
        public float PartsBodySquashLand = 0.7f;
        public float PartsBodySquashMax = 0.6f;
        public float PartsBodyFlexMaxDeg = 9f;
        public float PartsBodyWidthMax = 0.12f;
        public bool PartsLegRootFollowBend = false;
        public float PartsBodyFlexWave = 0.9f;
        public float PartsBodyFlexWavePhase = 1.57f;
        public float PartsBodyFlexAccel = 0.6f;
        public float PartsBodyFlexLand = 1f;
        public float PartsBodyFlexGather = 1.2f;
        public float PartsBodyFlexFlight = -0.8f;
        public float PartsBodyFlexTurn = 0.6f;
        public float PartsBodySquashSpeed = 0.22f;
        public float PartsBodySquashAccel = 0.5f;
        public float PartsBodySquashWave = 0.25f;
        public float PartsBodySquashTurn = 0.2f;
        public float PartsBodyLandSpeed = 12f;
        public float PartsBodyLandDecay = 0.07f;
        public float PartsChestAlong = 0f;
        public float PartsChestDrop = 0f;
        public bool CosmeticAnchorDrawnBody = true;
        public float PartsTailRootOverlap = 4f;
        public float PartsNeckFac = 0.35f;
        public float PartsLegSlack = 1.14f;
        public float PartsLegBendFront = -1f;
        public float PartsLegBendHind = 1f;
        public float PartsLegThicknessFac = 0.85f;

        public float PartsLegMetaFacFront = 0.20f;
        public float PartsLegMetaFacHind = 0.24f;
        public float PartsLegMetaRestDegFront = 15f;
        public float PartsLegMetaRestDegHind = 20f;
        public float PartsLegMetaFoldDeg = 42f;
        public float PartsLegPhaseLead = 0.17f;
        public float PartsLegFoldPeak = 0.22f;
        public float PartsLegTuckFac = 0.14f;
        public float PartsLegKneeFoldFac = 0.10f;
        public float PartsLegProxLagFac = 0.10f;
        public float PartsPawAnchorFac = 0.35f;

        public float PartsKneeSmooth = 0.4f;
        public float PartsKneeMaxStep = 2.5f;
        public float PartsKneeStepSpeedFac = 1.2f;
        public float PartsKneeReseedDist = 40f;
        public bool  PartsLegSingleSprite = false;

        public float PartsLegForeOffset = 1.5f;
        public float PartsHaunchLean = 25f;
        public bool PartsHaunchInChain = false;
        public float PartsHeadTqGate = 0.55f;
        public float PartsHeadAwayGate = 0.2f;
        public float PartsHeadTqGateEnter = 0.50f;
        public float PartsHeadTqGateExit = 0.60f;
        public float PartsHeadAwayGateEnter = 0.15f;
        public float PartsHeadAwayGateExit = 0.25f;
        public int PartsHeadFrameDwellTicks = 12;
        public float PartsWaveAmp = 1.6f;
        public float PartsWaveLen = 60f;
        public float PartsWaveSpeedGate = 2f;
        public float PartsWaveSpeedFull = 6f;
        public float PartsWaveGroundFade = 0.15f;
        public float PartsLegMaxExtend = 0.93f;
        public bool PartsTurnStep = true;
        public float PartsTurnStepFrontAt = 0.12f;
        public float PartsTurnStepHindAt = 0.58f;
        public float PartsTurnStepStagger = 0.1f;
        public float PartsTurnLeanDeg = 7f;

        public float IdleBreathAmp = 0.03f;
        public float IdleBreathTicks = 110f;
        public float IdleBreathBob = 0.35f;
        public float IdleGlanceChance = 0.004f;
        public float IdleGlanceRange = 45f;
        public int IdleGlanceTicks = 60;
        public float IdleTailSwayAmp = 0.5f;
        public float IdleTailSwayTicks = 150f;
        public float TailStreamFac = 0.5f;
        public float BodyLeanAccel = 1.2f;
        public float BodyLeanMax = 6f;

        public float HeadRestPitchDeg = 4f;
        public float HeadCarryPx = 3f;
        public float HeadPitchRate = 0.08f;
        public float HeadLookPitchFac = 0.7f;

        public bool HeadIdleEnabled = true;
        public float HeadIdleSpeed = 1.2f;
        public float HeadSniffChance = 0.006f;
        public int HeadSniffTicks = 90;
        public float HeadSniffPitchDeg = -26f;
        public float HeadScanChance = 0.008f;
        public int HeadScanTicks = 110;
        public float HeadScanPitchDeg = 10f;
        public float HeadScanYaw = 0.9f;
        public float HeadScanSweepTicks = 55f;
        public int HeadLookCooldownTicks = 120;
        public float HeadYawRate = 0.06f;
        public float HeadYawDepth = 0.95f;

        public bool HeadNoiseEnabled = true;
        public int HeadNoiseMaxAge = 40;
        public int HeadNoiseHoldTicks = 80;
        public float HeadBehindYaw = 0.9f;

        public float TurnHeadLead = 0.8f;
        public float TurnHeadLeadRate = 0.14f;
        public float TurnHeadLeadPitchDeg = 5f;
        public float TurnTailSwing = 0.35f;

        public float StepLeadFac = 1f;
        public float StepLeadSpeedFac = 0.5f;
        public float StepBehindTrigger = 0.75f;
        public float StepSwingSpeedFac = 2.2f;
        public float StepArcLift = 2.5f;
        public float StepArcAsym = 1f;
        public float StepSwingFold = 1.15f;
        public float PawCurlDeg = 20f;
        public bool PawCurlPivotAnkle = true;
        public float GaitPairSpeed = 4.5f;

        public int StepPlantCommitTicks = 6;
        public int StepMinGrip = 2;
        public int StepSwingMaxTicks = 8;
        public bool PartsGaitChainAll = false;
        public bool StepHindAxisFlip = false;
        public bool StepPlantWorldLock = true;
        public bool StepTargetClamp = true;
        public float StepTargetMaxAboveFac = 0.25f;
        public float StepReachClampFac = 1f;

        public bool StepReachFloorFollowsFold = true;

        public bool StepPlantAhead = true;
        public float StepPlantAheadMinSpeed = 0.3f;
        public float StepPlantAheadFullSpeed = 0.9f;
        public float StepPlantAheadTravelFac = 1f;
        public bool StepTrueStance = true;
        public bool StepEventLog = true;

        public float RunPitchAmp = 18f;
        public float RunHeightAmp = 2.5f;
        public float RunTailAmp = 0.4f;
        public float PounceBowPitch = 38f;
        public float PounceBowTailUp = 1.2f;
        public float PouncePropelPitch = 10f;
        public float PartsLegRootFront = 0.78f;
        public float PartsLegRootHind = 0.175f;
        public bool GaitDiagonal = true;

        public bool TailNoTuck = true;
        public float TailRearBias = 0.35f;
        public float TailUpBias = 0.25f;
        public float TailFloorClear = 2f;

        public float TurnDepthFac = 1f;
        public float LimbSpeed = 4f;
        public float LimbQuickness = 0.5f;
        public int LimbGripDelay = 2;
        public float StepLength = 0.5f;
        public float LiftFeet = 0.1f;
        public float FeetDown = 1f;
        public float LegPairDisplacement = 3f;
        public bool SmoothenLegMovement = true;
        public float WalkBob = 1f;
        public float LimbAttachFront = 0.35f;
        public float LimbAttachRear = 0.35f;
        public float LimbAttachDrop = 2f;

        public int TailSegments = 5;
        public float TailLengthFac = 1f;
        public float TailThicknessFac = 1f;
        public float TailLengthVsBody = 1f;
        public float TailHeightVsBody = 0.85f;
        public float TailTipTaper = 0.6f;
        public float TailRootThickness = 0.90f;
        public float TailTipThickness = 0.12f;
        public float TailSwellPos = 0.25f;
        public float TailRootDrop = 1f;
        public float TailRootDroop = 0.45f;
        public float TailGravityCurve = 2f;
        public float TailMeshOverlap = 0.2f;
        public float TailStiffness = 120f;
        public float TailStiffnessDecline = 0.5f;
        public float TailGravity = 0.9f;

        public float StalkCrouchDrop = 5f;
        public float StalkCrouchLimbFac = 0.6f;
        public float LandingPoseReach = 8f;
        public float LandingPoseLevel = 0.5f;

        public bool ClimbsWalls = false;
        public bool ClimbLegacyAnyTileFooting = false;

        public int RegainFootingFrames = 10;
        public int NoGripReleaseFrames = 10;
        public int ClimbLogTicks = 20;
        public float FloorLeverage = 0f;
        public float ClimbTileCenterFac = 0.5f;

        public bool ClimbsPoles = false;
        public bool PoleHorizontalAllowed = false;
        public float PoleGrabMaxSpeed = 6f;
        public int PoleRegrabTicks = 20;
        public bool PoleReleaseOnHit = false;
        public bool PoleReleaseOnGrab = false;
        public bool PoleReleaseOnBlind = false;
        public float PoleCenterHeadFac = 0.5f;
        public float PoleCenterMidFac = 0.25f;
        public float PoleCenterRearFac = 0.125f;
        public float PoleCenterReachPx = 14f;
        public float PoleRideFac = 0.8f;
        public float PoleRideLerp = 0.2f;
        public float PoleCyclePx = 20f;
        public float PoleReachFrac = 0.4f;
        public float PolePawPhaseOffset = 0.5f;
        public float PoleHindPhaseOffset = 0.35f;
        public float PoleFrontAmpPx = 6f;
        public float PoleFrontLeadPx = 4f;
        public float PoleHindAmpPx = 6f;
        public float PoleHindLeadPx = -2f;
        public float PolePawSidePx = 1.5f;
        public float PoleLegBendFront = -1f;
        public float PoleLegBendHind = 1f;
        public float PoleLegMetaDegFront = 60f;
        public float PoleLegMetaDegHind = 45f;
        public float PolePawSpeedFac = 1.5f;
        public bool PoleBackDown = true;
        public float PolePullPulseFac = 0.15f;
        public float PoleBodySwayPx = 1.25f;
        public int PoleBodySwayCycles = 1;
        public float PoleBodySwayLag = 0.25f;
        public float PoleStrokeJitter = 0.5f;
        public float PoleStrokeJitterPx = 1.0f;
        public int PoleLogTicks = 20;

        public int PoleMeetDetectTicks = 60;
        public string PoleMeetYieldRule = "ascending";
        public float PoleMeetProgressPx = 6f;
        public float PoleMeetAheadPx = 24f;
        public float PoleMeetLanePx = 16f;
        public int PoleMeetEscalateTicks = 120;
        public int PoleMeetWaitTicks = 400;
        public int PoleMeetAsideTiles = 2;
        public int PoleMeetGiveUpTicks = 1200;

        public bool PoleAscentHeadFirst = true;
        public bool PoleAlignOrder = true;
        public float PoleAlignFarMaxPx = 2f;
        public float PoleAlignSwingPx = 1f;

        public int PoleFloorRegrabTicks = 40;

        public bool PoleDestFilter = true;

        public bool WallOverhangAllowed = false;

        public bool WallWanderDest = false;

        public float WallGrabMaxSpeed = 6f;

        public int WallRegrabTicks = 20;

        public float WallCenterFac = 0.25f;

        public float WallStandoffPx = 1f;

        public float WallPawFacePx = 1.5f;

        public float WallPoseLerp = 0.2f;

        public bool WallLaunchAllowed = false;

        public int WallLogTicks = 20;

        public string VoiceHunt = "Lizard_Voice_Green_A";
        public string VoiceAlert = "Lizard_Voice_Blue_A";
        public string VoiceAttack = "Lizard_Voice_Pink_B";
        public string VoiceHurt = "Lizard_Voice_Pink_D";
        public string VoiceDeath = "Lizard_Voice_Pink_E";
        public string VoicePheromone = "";
        public float VoiceVolume = 1f;
        public float VoicePitch = 1f;

        public bool PheromoneEnabled = false;
        public string PheromoneShare = "all";
        public float PheromoneRangeTiles = 0f;
        public bool PheromoneReceiverNeedsLos = false;
        public string PheromoneReceiverResponse = "none";
        public int PheromoneCommMinTicks = 3;
        public int PheromoneCommMaxTicks = 50;
        public int PheromoneFlickerFullTicks = 14;
        public float PheromoneFlickerRise = 0.25f;
        public float PheromoneFlickerFall = 0.025f;
        public int PheromoneLogTicks = 40;

        public int PheromoneStyle = 1;
        public float PheromoneWiggleDeg = 12f;
        public float PheromoneWiggleHz = 3f;
        public float PheromoneWiggleLagDeg = 60f;
        public int PheromoneReleaseTicks = 48;
        public int PheromoneWiggleRiseTicks = 4;
        public int PheromoneWiggleFallTicks = 10;
        public float PheromoneMistOpacity = 0.25f;
        public float PheromoneMistHueDeg = 340f;
        public float PheromoneMistSaturation = 0.55f;
        public float PheromoneMistLightness = 0.76f;
        public float PheromoneMistFog = 0.2f;
        public float PheromoneMistSize = 0.25f;
        public int PheromoneMistCount = 6;
        public float PheromoneMistAim = 0.7f;
        public float PheromoneMistDrift = 3f;
        public int PheromoneMistLifetimeTicks = 180;
        public bool PheromoneReceiverSniff = true;
        public float PheromoneSniffReachPx = 8f;
        public float PheromoneSniffReachFac = 0.4f;
        public int PheromoneSniffDelayTicks = 6;

        public bool EvasionEnabled = false;
        public float EvasionChance = 0.6f;
        public int EvasionReactionMinTicks = 3;
        public int EvasionReactionMaxTicks = 12;
        public int EvasionCooldownTicks = 40;
        public int EvasionStaggerTicks = 12;

        public int VoiceCooldownFrames = 25;

        public readonly List<FoxCosmeticSpec> Cosmetics = FoxCosmeticSpec.Defaults();

        public bool CamoEnabled = false;

        public float CamoVisibilityBonus = 0.8f;

        public float CamoSpeedHidden = 0.35f;

        public float CamoSpeedVisible = 0.75f;

        public float CamoArmRate = 0.01f;

        public float CamoBreakRate = 0.2f;

        public int CamoBreakHoldTicks = 80;

        public float CamoBreakHealth = 0.6f;

        public bool CamoBreakOnStun = true;

        public bool CamoBreakOnAttack = true;

        public bool CamoBreakOnHurt = true;

        public float CamoBlendMax = 0.85f;

        public string CamoColorSource = "background";

        internal const string CAMO_SOURCE_BACKGROUND = "background";
        internal const string CAMO_SOURCE_PALETTE = "palette";

        internal static bool IsCamoSource(string value, string want)
        {
            return value != null && value.Trim().Equals(want, StringComparison.OrdinalIgnoreCase);
        }

        internal bool CamoSamplesBackground
        {
            get { return !IsCamoSource(CamoColorSource, CAMO_SOURCE_PALETTE); }
        }

        public float CamoColorDrift = 0.1f;

        public bool CamoSampleDrawnBody = true;

        public float CamoColorFogMix = 0.65f;

        public float CamoHiddenThreshold = 0.75f;

        public int CamoLogTicks = 40;

        public bool BaitEnabled = false;

        public string BaitItemTypes = "Spear,DangleFruit,ScavengerBomb,DataPearl,FlareBomb,PuffBall,BubbleGrass,EggBugEgg,FirecrackerPlant,FlyLure,JellyFish,Lantern,Mushroom,SlimeMold,VultureMask,WaterNut";

        public bool BaitPearlMiscOnly = true;

        public bool BaitDominanceGate = true;

        public int BaitTrackMaxItems = 10;

        public int BaitTrackRememberTicks = 400;

        public int BaitTrackForgetTiles = 30;

        public float BaitGrabRangePx = 40f;

        public float BaitGraspDominance = 0.5f;

        public bool BaitSeekEnabled = true;

        public float BaitPickVisibleFac = 2f;

        public int BaitChooseIntervalTicks = 40;

        public int BaitSeekMaxTicks = 400;

        public float BaitRunSpeed = 0.8f;

        public int BaitSpotRadiusTiles = 12;

        public int BaitSpotCandidates = 16;

        public float BaitPrepMinDistFac = 0.6f;

        public float BaitPrepMaxDistFac = 0.9f;

        public int BaitPrepMaxRiseTiles = 2;

        public int BaitPrepHeadroomTiles = 2;

        public float BaitPrepDistWeight = 1f;

        public float BaitPrepLevelWeight = 0.5f;

        public float BaitPrepCoverWeight = 0.3f;

        public float BaitSpotTravelWeight = 0.3f;

        public int BaitPlanMaxAttempts = 5;

        public float BaitArrivePx = 25f;

        public int BaitCarryMaxTicks = 800;

        public int BaitSettleTicks = 20;

        public float BaitMovedPx = 40f;

        public float BaitWaitRunSpeed = 0f;

        public int BaitWaitMaxTicks = 2400;

        public int BaitCooldownTicks = 400;

        public bool BaitHoldsAgainstHunt = false;

        public bool BaitWaitHoldsAgainstHunt = false;

        public string BaitSpringRule = "prey";

        public float BaitTriggerRangePx = 40f;

        public bool BaitPounceRoll = false;

        public float BaitFaceRunSpeed = 0.35f;

        public int BaitFaceMaxTicks = 200;

        public float BaitExitWeight = 0.01f;

        public float BaitExitReachBonus = 3f;

        public float BaitExitCentreBonus = 100f;

        public int BaitExitNearSteps = 20;

        public float BaitExitNearPenalty = 500f;

        public float BaitExitPipeTiles = 5f;

        public float BaitExitPipeExitPenalty = 500f;

        public float BaitExitPipeOtherPenalty = 50f;

        public bool BaitExitPipeAtPrep = false;

        public float BaitExitNonePenalty = 500f;

        public float TerrainSpeed(AItile.Accessibility acc)
        {
            switch (acc)
            {
                case AItile.Accessibility.Corridor: return SpeedCorridor;
                case AItile.Accessibility.Climb:    return SpeedClimb;
                case AItile.Accessibility.Wall:     return SpeedWall;
                case AItile.Accessibility.Ceiling:  return SpeedCeiling;
                default:                            return SpeedFloor;
            }
        }

        public static FoxBreedParams FromJson(JsonObject o)
        {
            var p = new FoxBreedParams();

            p.Toughness = F(o, "toughness", p.Toughness);
            p.StunToughness = F(o, "stun_toughness", p.StunToughness);
            p.BiteResistance = F(o, "bite_resistance", p.BiteResistance);
            p.BiteStunResistance = F(o, "bite_stun_resistance", p.BiteStunResistance);
            p.BodySize = F(o, "body_size", p.BodySize);
            p.MeatPoints = I(o, "meat_points", p.MeatPoints);
            p.DangerousToPlayer = F(o, "dangerous_to_player", p.DangerousToPlayer);
            p.Scaryness = F(o, "scaryness", p.Scaryness);
            p.VisualRadius = F(o, "visual_radius", p.VisualRadius);
            p.WaterVision = F(o, "water_vision", p.WaterVision);
            p.ThroughSurfaceVision = F(o, "through_surface_vision", p.ThroughSurfaceVision);
            p.MovementBasedVision = F(o, "movement_based_vision", p.MovementBasedVision);
            p.OffScreenSpeed = F(o, "off_screen_speed", p.OffScreenSpeed);
            p.AbstractedLaziness = I(o, "abstracted_laziness", p.AbstractedLaziness);
            p.RoamInRoomChance = F(o, "roam_in_room_chance", p.RoamInRoomChance);
            p.RoamBetweenRoomsChance = F(o, "roam_between_rooms_chance", p.RoamBetweenRoomsChance);
            p.Grasps = I(o, "grasps", p.Grasps);
            p.ShortcutSegments = I(o, "shortcut_segments", p.ShortcutSegments);
            p.StowFoodInDen = B(o, "stow_food_in_den", p.StowFoodInDen);
            p.CanSwim = B(o, "can_swim", p.CanSwim);
            p.WaterRelationship = S(o, "water_relationship", p.WaterRelationship);
            p.WaterPathingResistance = F(o, "water_pathing_resistance", p.WaterPathingResistance);
            p.LoadedPerformanceCost = F(o, "loaded_performance_cost", p.LoadedPerformanceCost);
            p.ConspecificPackIntensity = F(o, "conspecific_pack_intensity", p.ConspecificPackIntensity);
            p.RelationshipsExpectedSkipped = I(o, "relationships_expected_skipped", p.RelationshipsExpectedSkipped);
            p.RelRouteAttacks = S(o, "rel_route_attacks", p.RelRouteAttacks);
            p.RelRouteSocialDependent = S(o, "rel_route_social_dependent", p.RelRouteSocialDependent);
            p.DiscomfortEnabled = B(o, "discomfort_enabled", p.DiscomfortEnabled);
            ReadRelationships(o, p);
            ReadRoomAttraction(o, p);
            ReadTiles(o, p);
            ReadConnections(o, p);

            p.BodyMass = F(o, "body_mass", p.BodyMass);
            p.BodySizeFac = F(o, "body_size_fac", p.BodySizeFac);
            p.BodyRadFac = F(o, "body_rad_fac", p.BodyRadFac);
            p.BodyLengthFac = F(o, "body_length_fac", p.BodyLengthFac);
            p.BodyStiffness = F(o, "body_stiffness", p.BodyStiffness);
            p.SpineElasticity = F(o, "spine_elasticity", p.SpineElasticity);
            p.SpineWeightSymmetry = F(o, "spine_weight_symmetry", p.SpineWeightSymmetry);
            p.WeightThreat = F(o, "weight_threat", p.WeightThreat);
            p.WeightPrey = F(o, "weight_prey", p.WeightPrey);
            p.WeightRain = F(o, "weight_rain", p.WeightRain);
            p.PathStepsPerFrame = I(o, "path_steps_per_frame", p.PathStepsPerFrame);
            p.RouteHoleAvoid = B(o, "route_hole_avoid", p.RouteHoleAvoid);
            p.RouteHolePenalty = F(o, "route_hole_penalty", p.RouteHolePenalty);
            p.RouteCorridorPenalty = F(o, "route_corridor_penalty", p.RouteCorridorPenalty);
            p.RouteCorridorDetectSideways = B(o, "route_corridor_detect_sideways", p.RouteCorridorDetectSideways);
            p.RouteHoleLegality = S(o, "route_hole_legality", p.RouteHoleLegality);
            p.RouteHoleProbeTiles = I(o, "route_hole_probe_tiles", p.RouteHoleProbeTiles);
            p.RouteObstacleEnabled = B(o, "route_obstacle_enabled", p.RouteObstacleEnabled);
            p.RouteObstacleWeight = F(o, "route_obstacle_weight", p.RouteObstacleWeight);
            p.RouteObstacleExponent = F(o, "route_obstacle_exponent", p.RouteObstacleExponent);
            p.RouteObstacleSkipWhenWedged = B(o, "route_obstacle_skip_when_wedged", p.RouteObstacleSkipWhenWedged);
            p.RouteObstacleEscapeMuteTicks = I(o, "route_obstacle_escape_mute_ticks", p.RouteObstacleEscapeMuteTicks);
            p.PounceRouteEnabled = B(o, "pounce_route_enabled", p.PounceRouteEnabled);
            p.PounceRouteWhilePatrolling = B(o, "pounce_route_while_patrolling", p.PounceRouteWhilePatrolling);
            p.PounceRoutePeekSteps = I(o, "pounce_route_peek_steps", p.PounceRoutePeekSteps);
            p.PounceRouteMaxFinders = I(o, "pounce_route_max_finders", p.PounceRouteMaxFinders);
            p.PounceRouteItersPerTick = I(o, "pounce_route_iters_per_tick", p.PounceRouteItersPerTick);
            p.PounceRouteMinTilesOrigin = I(o, "pounce_route_min_tiles_origin", p.PounceRouteMinTilesOrigin);
            p.PounceRouteMinTilesDest = I(o, "pounce_route_min_tiles_dest", p.PounceRouteMinTilesDest);
            p.PounceRouteBlockNarrow = B(o, "pounce_route_block_narrow", p.PounceRouteBlockNarrow);
            p.PounceRouteReplanTicks = I(o, "pounce_route_replan_ticks", p.PounceRouteReplanTicks);
            p.MantleRouteEnabled = B(o, "mantle_route_enabled", p.MantleRouteEnabled);
            p.MantleRouteVeto = B(o, "mantle_route_veto", p.MantleRouteVeto);
            p.MantleRouteVetoOnSilence = B(o, "mantle_route_veto_on_silence", p.MantleRouteVetoOnSilence);
            p.MantleRouteVetoOnIllegalLanding = B(o, "mantle_route_veto_on_illegal_landing", p.MantleRouteVetoOnIllegalLanding);
            p.PounceRouteGoalSeedEnabled = B(o, "pounce_route_goal_seed_enabled", p.PounceRouteGoalSeedEnabled);
            p.PounceRouteGoalSeedSnapRows = I(o, "pounce_route_goal_seed_snap_rows", p.PounceRouteGoalSeedSnapRows);
            p.PounceRouteGoalSeedPathFloor = I(o, "pounce_route_goal_seed_path_floor", p.PounceRouteGoalSeedPathFloor);
            p.PounceRouteGoalSeedRadiusTiles = F(o, "pounce_route_goal_seed_radius_tiles", p.PounceRouteGoalSeedRadiusTiles);
            p.PounceRouteGoalSeedLateralTiles = F(o, "pounce_route_goal_seed_lateral_tiles", p.PounceRouteGoalSeedLateralTiles);
            p.PounceRouteGoalSeedMinGainTiles = F(o, "pounce_route_goal_seed_min_gain_tiles", p.PounceRouteGoalSeedMinGainTiles);
            p.PounceRouteGoalMinTilesOrigin = I(o, "pounce_route_goal_min_tiles_origin", p.PounceRouteGoalMinTilesOrigin);
            p.TurnRearAnchorEnabled = B(o, "turn_rear_anchor_enabled", p.TurnRearAnchorEnabled);
            p.TurnBlockedProbeLevel = B(o, "turn_blocked_probe_level", p.TurnBlockedProbeLevel);
            p.PounceRouteFlyLaunchVel = B(o, "pounce_route_fly_launch_vel", p.PounceRouteFlyLaunchVel);
            p.ZeroGravityInCorridors = B(o, "zero_gravity_in_corridors", p.ZeroGravityInCorridors);
            p.SqueezeArmWhenWedged = B(o, "squeeze_arm_when_wedged", p.SqueezeArmWhenWedged);
            p.SqueezeWedgeMaxTicks = I(o, "squeeze_wedge_max_ticks", p.SqueezeWedgeMaxTicks);
            p.SqueezeWedgeRearmInCorridor = B(o, "squeeze_wedge_rearm_in_corridor", p.SqueezeWedgeRearmInCorridor);
            p.SqueezeWedgeMaxRearms = I(o, "squeeze_wedge_max_rearms", p.SqueezeWedgeMaxRearms);
            p.SqueezeWedgeEscapeEnabled = B(o, "squeeze_wedge_escape_enabled", p.SqueezeWedgeEscapeEnabled);
            p.SqueezeWedgeEscapeTicks = I(o, "squeeze_wedge_escape_ticks", p.SqueezeWedgeEscapeTicks);
            p.SqueezeWedgeEscapeStuckTicks = I(o, "squeeze_wedge_escape_stuck_ticks", p.SqueezeWedgeEscapeStuckTicks);
            p.SqueezeReinflateTicks = I(o, "squeeze_reinflate_ticks", p.SqueezeReinflateTicks);
            p.CorridorTerrainMinChunks = I(o, "corridor_terrain_min_chunks", p.CorridorTerrainMinChunks);
            p.CorridorNarrowIgnoreDiagonals = B(o, "corridor_narrow_ignore_diagonals", p.CorridorNarrowIgnoreDiagonals);
            p.CorridorFootingGate = B(o, "corridor_footing_gate", p.CorridorFootingGate);
            p.CorridorEntryFootingFrames = I(o, "corridor_entry_footing_frames", p.CorridorEntryFootingFrames);
            p.CorridorCenterFac = F(o, "corridor_center_fac", p.CorridorCenterFac);
            p.CorridorCenterReachTiles = F(o, "corridor_center_reach_tiles", p.CorridorCenterReachTiles);
            p.CorridorCenterYieldToTravel = B(o, "corridor_center_yield_to_travel", p.CorridorCenterYieldToTravel);
            p.CorridorFoldFac = F(o, "corridor_fold_fac", p.CorridorFoldFac);
            p.SqueezeWedgeUtilityMin = F(o, "squeeze_wedge_utility_min", p.SqueezeWedgeUtilityMin);
            p.PounceRouteRevalidationsMax = I(o, "pounce_route_revalidations_max", p.PounceRouteRevalidationsMax);
            p.PounceRouteHoldMaxTicks = I(o, "pounce_route_hold_max_ticks", p.PounceRouteHoldMaxTicks);
            p.PounceRouteMinRise = F(o, "pounce_route_min_rise", p.PounceRouteMinRise);
            p.PounceRouteMaxRise = F(o, "pounce_route_max_rise", p.PounceRouteMaxRise);
            p.GaitWalkEnabled = B(o, "gait_walk_enabled", p.GaitWalkEnabled);
            p.GaitWalkTrotSpeed = F(o, "gait_walk_trot_speed", p.GaitWalkTrotSpeed);
            p.StepGaitGapShort = F(o, "step_gait_gap_short", p.StepGaitGapShort);
            p.StepGaitGapLong = F(o, "step_gait_gap_long", p.StepGaitGapLong);
            p.StepArcPeak = F(o, "step_arc_peak", p.StepArcPeak);
            p.StepTargetFreezeFrac = F(o, "step_target_freeze_frac", p.StepTargetFreezeFrac);
            p.StepSwingMinTicks = I(o, "step_swing_min_ticks", p.StepSwingMinTicks);
            p.PipeReentryDelayTicks = I(o, "pipe_reentry_delay_ticks", p.PipeReentryDelayTicks);
            p.PipeMouthShovePx = F(o, "pipe_mouth_shove_px", p.PipeMouthShovePx);
            p.PipeExitDelayTicks = I(o, "pipe_exit_delay_ticks", p.PipeExitDelayTicks);
            p.MantleRiseFromClimbEnds = B(o, "mantle_rise_from_climb_ends", p.MantleRiseFromClimbEnds);
            p.StepUpRampConnWalks = B(o, "step_up_ramp_conn_walks", p.StepUpRampConnWalks);
            p.PounceRouteSeedAttemptsPerTick = I(o, "pounce_route_seed_attempts_per_tick", p.PounceRouteSeedAttemptsPerTick);
            p.PartsSpineRevMinSpeed = F(o, "parts_spine_rev_min_speed", p.PartsSpineRevMinSpeed);
            p.PounceRouteMaxLegality = S(o, "pounce_route_max_legality", p.PounceRouteMaxLegality);
            p.MantleRouteScanTiles = I(o, "mantle_route_scan_tiles", p.MantleRouteScanTiles);
            p.MantleRouteReplanTicks = I(o, "mantle_route_replan_ticks", p.MantleRouteReplanTicks);
            p.MantleRoutePenalty = F(o, "mantle_route_penalty", p.MantleRoutePenalty);
            p.MantleRouteMaxRise = F(o, "mantle_route_max_rise", p.MantleRouteMaxRise);
            p.RememberCreatureFrames = I(o, "remember_creature_frames", p.RememberCreatureFrames);
            p.PreySureToGetDistance = F(o, "prey_sure_to_get_distance", p.PreySureToGetDistance);
            p.PreySureToLoseDistance = F(o, "prey_sure_to_lose_distance", p.PreySureToLoseDistance);
            p.PreyPersistenceBias = F(o, "prey_persistence_bias", p.PreyPersistenceBias);
            p.PreyGiveUpUnreachable = I(o, "prey_give_up_unreachable", p.PreyGiveUpUnreachable);
            p.PreyGiveUpGhostGeneration = I(o, "prey_give_up_ghost_generation", p.PreyGiveUpGhostGeneration);
            p.HuntPersistFrames = I(o, "hunt_persist_frames", p.HuntPersistFrames);
            p.HuntGhostPursue = B(o, "hunt_ghost_pursue", p.HuntGhostPursue);
            p.HearingSkill = F(o, "hearing_skill", p.HearingSkill);
            p.PhysGravity = F(o, "gravity", p.PhysGravity);
            p.PhysSurfaceFriction = F(o, "surface_friction", p.PhysSurfaceFriction);
            p.PhysAirFriction = F(o, "air_friction", p.PhysAirFriction);
            p.PhysBounce = F(o, "bounce", p.PhysBounce);
            p.PhysGoThroughFloors = B(o, "phys_go_through_floors", p.PhysGoThroughFloors);
            p.PhysWaterFriction = F(o, "water_friction", p.PhysWaterFriction);
            p.PhysBuoyancy = F(o, "buoyancy", p.PhysBuoyancy);

            p.SwimEnabled = B(o, "swim_enabled", p.SwimEnabled);
            p.SwimSpeed = F(o, "swim_speed", p.SwimSpeed);
            p.SwimSubmersionMin = F(o, "swim_submersion_min", p.SwimSubmersionMin);
            p.SwimSurfaceHold = B(o, "swim_surface_hold", p.SwimSurfaceHold);
            p.SwimSurfaceDamping = F(o, "swim_surface_damping", p.SwimSurfaceDamping);
            p.SwimSurfacePull = F(o, "swim_surface_pull", p.SwimSurfacePull);
            p.SwimSurfaceClamp = F(o, "swim_surface_clamp", p.SwimSurfaceClamp);
            p.SwimClimbOutFooting = B(o, "swim_climb_out_footing", p.SwimClimbOutFooting);
            p.SwimLogTicks = I(o, "swim_log_ticks", p.SwimLogTicks);
            p.SwimDiveEnabled = B(o, "swim_dive_enabled", p.SwimDiveEnabled);
            p.SwimDiveDepthTiles = F(o, "swim_dive_depth_tiles", p.SwimDiveDepthTiles);
            p.SwimDiveSpeed = F(o, "swim_dive_speed", p.SwimDiveSpeed);
            p.SwimSurfaceHoldReleaseOnDive = B(o, "swim_surface_hold_release_on_dive", p.SwimSurfaceHoldReleaseOnDive);
            p.StunFallEnabled = B(o, "stun_fall_enabled", p.StunFallEnabled);
            p.StunFallFrictionOnDead = B(o, "stun_fall_friction_on_dead", p.StunFallFrictionOnDead);
            p.DeathAssertPhysics = B(o, "death_assert_physics", p.DeathAssertPhysics);
            p.StunFallMinStun = I(o, "stun_fall_min_stun", p.StunFallMinStun);
            p.StunFallRightingOff = B(o, "stun_fall_righting_off", p.StunFallRightingOff);
            p.StunFallSurfaceFriction = F(o, "stun_fall_surface_friction", p.StunFallSurfaceFriction);
            p.StunFallRecoverTicks = I(o, "stun_fall_recover_ticks", p.StunFallRecoverTicks);
            p.BlindEnabled = B(o, "blind_enabled", p.BlindEnabled);
            p.BlindOmniFac = F(o, "blind_omni_fac", p.BlindOmniFac);
            p.BlindHeadScatterPx = F(o, "blind_head_scatter_px", p.BlindHeadScatterPx);
            p.BlindHeadScatterTicks = I(o, "blind_head_scatter_ticks", p.BlindHeadScatterTicks);
            p.BlindEyeFlicker = B(o, "blind_eye_flicker", p.BlindEyeFlicker);
            p.BlindStunEnabled = B(o, "blind_stun_enabled", p.BlindStunEnabled);
            p.BlindStunTicksNear = I(o, "blind_stun_ticks_near", p.BlindStunTicksNear);
            p.BlindStunTicksFar = I(o, "blind_stun_ticks_far", p.BlindStunTicksFar);
            p.DeathThroesEnabled = B(o, "death_throes_enabled", p.DeathThroesEnabled);
            p.DeathThroesDecay = F(o, "death_throes_decay", p.DeathThroesDecay);
            p.DeathThroesHeadForce = F(o, "death_throes_head_force", p.DeathThroesHeadForce);
            p.DeathThroesBodyForce = F(o, "death_throes_body_force", p.DeathThroesBodyForce);
            p.DeathThroesHeadshotFac = F(o, "death_throes_headshot_fac", p.DeathThroesHeadshotFac);
            p.DeathThroesBehindFac = F(o, "death_throes_behind_fac", p.DeathThroesBehindFac);
            p.DeathThroesGrabbedFac = F(o, "death_throes_grabbed_fac", p.DeathThroesGrabbedFac);
            p.DeathHeadPitchDeg = F(o, "death_head_pitch_deg", p.DeathHeadPitchDeg);
            p.DeathHeadPitchRate = F(o, "death_head_pitch_rate", p.DeathHeadPitchRate);
            p.DeathBehindFadeEnabled = B(o, "death_behind_fade_enabled", p.DeathBehindFadeEnabled);
            p.DeathBehindFadeTicks = I(o, "death_behind_fade_ticks", p.DeathBehindFadeTicks);
            p.DeathBehindFadeMinAlpha = F(o, "death_behind_fade_min_alpha", p.DeathBehindFadeMinAlpha);
            p.DeathBehindLayTicks = I(o, "death_behind_lay_ticks", p.DeathBehindLayTicks);
            p.DeathBehindLayLimbFac = F(o, "death_behind_lay_limb_fac", p.DeathBehindLayLimbFac);
            p.DeathLayAllDeaths = B(o, "death_lay_all_deaths", p.DeathLayAllDeaths);
            p.DeathBehindConeDeg = F(o, "death_behind_cone_deg", p.DeathBehindConeDeg);
            p.DeathLimpTicks = I(o, "death_limp_ticks", p.DeathLimpTicks);
            p.DeathBodyFlexMaxDeg = F(o, "death_body_flex_max_deg", p.DeathBodyFlexMaxDeg);
            p.DeathBodyBendFac = F(o, "death_body_bend_fac", p.DeathBodyBendFac);
            p.DeathBodyBendMax = F(o, "death_body_bend_max", p.DeathBodyBendMax);
            p.DeathTailLimpFac = F(o, "death_tail_limp_fac", p.DeathTailLimpFac);
            p.DeathTailRootDroop = F(o, "death_tail_root_droop", p.DeathTailRootDroop);
            p.DeathHeadGravity = F(o, "death_head_gravity", p.DeathHeadGravity);

            p.BaseSpeed = F(o, "base_speed", p.BaseSpeed);
            p.Acceleration = F(o, "acceleration", p.Acceleration);
            p.SpeedFloor = F(o, "speed_floor", p.SpeedFloor);
            p.SpeedCorridor = F(o, "speed_corridor", p.SpeedCorridor);
            p.SpeedClimb = F(o, "speed_climb", p.SpeedClimb);
            p.SpeedWall = F(o, "speed_wall", p.SpeedWall);
            p.SpeedCeiling = F(o, "speed_ceiling", p.SpeedCeiling);
            p.StepUpBoost = F(o, "step_up_boost", p.StepUpBoost);
            p.StepUpHeightThreshold = F(o, "step_up_height_threshold", p.StepUpHeightThreshold);
            p.StepUpCooldown = I(o, "step_up_cooldown", p.StepUpCooldown);
            p.StepUpVelYGate = F(o, "step_up_vel_y_gate", p.StepUpVelYGate);
            p.StepUpClearPx = F(o, "step_up_clear_px", p.StepUpClearPx);
            p.StepUpClearTolerance = F(o, "step_up_clear_tolerance", p.StepUpClearTolerance);
            p.LedgePounceEnabled = B(o, "ledge_pounce_enabled", p.LedgePounceEnabled);
            p.LedgePounceMinRise = F(o, "ledge_pounce_min_rise", p.LedgePounceMinRise);
            p.LedgePounceMaxRise = F(o, "ledge_pounce_max_rise", p.LedgePounceMaxRise);
            p.LedgePounceCooldown = I(o, "ledge_pounce_cooldown", p.LedgePounceCooldown);
            p.LedgePounceLeadFac = F(o, "ledge_pounce_lead_fac", p.LedgePounceLeadFac);
            p.LedgePounceHeadroomTiles = I(o, "ledge_pounce_headroom_tiles", p.LedgePounceHeadroomTiles);
            p.LedgePounceStandoffPx = F(o, "ledge_pounce_standoff_px", p.LedgePounceStandoffPx);
            p.LedgePounceLandMarginPx = F(o, "ledge_pounce_land_margin_px", p.LedgePounceLandMarginPx);
            p.LedgePounceAllowLevel = B(o, "ledge_pounce_allow_level", p.LedgePounceAllowLevel);
            p.LedgePounceMinGapPx = F(o, "ledge_pounce_min_gap_px", p.LedgePounceMinGapPx);
            p.LedgePounceAllowDescend = B(o, "ledge_pounce_allow_descend", p.LedgePounceAllowDescend);
            p.LedgePounceMaxDrop = F(o, "ledge_pounce_max_drop", p.LedgePounceMaxDrop);
            p.MantleEnabled = B(o, "mantle_enabled", p.MantleEnabled);
            p.MantleMinRise = F(o, "mantle_min_rise", p.MantleMinRise);
            p.MantleMaxRise = F(o, "mantle_max_rise", p.MantleMaxRise);
            p.MantleReachPx = F(o, "mantle_reach_px", p.MantleReachPx);
            p.MantleGripInsetPx = F(o, "mantle_grip_inset_px", p.MantleGripInsetPx);
            p.MantlePullGravities = F(o, "mantle_pull_gravities", p.MantlePullGravities);
            p.MantleForwardGravities = F(o, "mantle_forward_gravities", p.MantleForwardGravities);
            p.MantleRearPullFac = F(o, "mantle_rear_pull_fac", p.MantleRearPullFac);
            p.MantleMaxRiseSpeed = F(o, "mantle_max_rise_speed", p.MantleMaxRiseSpeed);
            p.MantleMaxTicks = I(o, "mantle_max_ticks", p.MantleMaxTicks);
            p.MantleCooldown = I(o, "mantle_cooldown", p.MantleCooldown);
            p.MantleHeadroomTiles = I(o, "mantle_headroom_tiles", p.MantleHeadroomTiles);
            p.MantleProbeTiles = I(o, "mantle_probe_tiles", p.MantleProbeTiles);
            p.MantleMaxScanTiles = I(o, "mantle_max_scan_tiles", p.MantleMaxScanTiles);
            p.MantleFacePlatforms = B(o, "mantle_face_platforms", p.MantleFacePlatforms);
            p.MantleClearMarginPx = F(o, "mantle_clear_margin_px", p.MantleClearMarginPx);
            p.MantleFootAboveFac = F(o, "mantle_foot_above_fac", p.MantleFootAboveFac);
            p.MantleLogTicks = I(o, "mantle_log_ticks", p.MantleLogTicks);
            p.ReachEnabled = B(o, "reach_enabled", p.ReachEnabled);
            p.ReachMaxTicks = I(o, "reach_max_ticks", p.ReachMaxTicks);
            p.ReachTicksPerTile = I(o, "reach_ticks_per_tile", p.ReachTicksPerTile);
            p.ReachSpeed = F(o, "reach_speed", p.ReachSpeed);
            p.ReachAccelFac = F(o, "reach_accel_fac", p.ReachAccelFac);
            p.ReachArrivePx = F(o, "reach_arrive_px", p.ReachArrivePx);
            p.ReachGravityOff = B(o, "reach_gravity_off", p.ReachGravityOff);
            p.ReachCooldown = I(o, "reach_cooldown", p.ReachCooldown);
            p.ReachLogTicks = I(o, "reach_log_ticks", p.ReachLogTicks);
            p.EnterEnabled = B(o, "enter_enabled", p.EnterEnabled);
            p.EnterGatherFrames = I(o, "enter_gather_frames", p.EnterGatherFrames);
            p.EnterGatherCompress = F(o, "enter_gather_compress", p.EnterGatherCompress);
            p.EnterHeadLead = F(o, "enter_head_lead", p.EnterHeadLead);
            p.EnterRearLag = F(o, "enter_rear_lag", p.EnterRearLag);
            p.EnterPull = F(o, "enter_pull", p.EnterPull);
            p.EnterSpinePull = B(o, "enter_spine_pull", p.EnterSpinePull);
            p.EnterMaxFrames = I(o, "enter_max_frames", p.EnterMaxFrames);
            p.EnterCooldown = I(o, "enter_cooldown", p.EnterCooldown);
            p.EnterReachPx = F(o, "enter_reach_px", p.EnterReachPx);
            p.EnterThroughFloors = B(o, "enter_through_floors", p.EnterThroughFloors);
            p.EnterShortcutEnabled = B(o, "enter_shortcut_enabled", p.EnterShortcutEnabled);
            p.EnterShortcutNeedsGround = B(o, "enter_shortcut_needs_ground", p.EnterShortcutNeedsGround);
            p.EnterShortcutScanTiles = I(o, "enter_shortcut_scan_tiles", p.EnterShortcutScanTiles);
            p.EnterShortcutNearTiles = I(o, "enter_shortcut_near_tiles", p.EnterShortcutNearTiles);
            p.MoveActionLogTicks = I(o, "move_action_log_ticks", p.MoveActionLogTicks);
            p.MoveSelectLogTicks = I(o, "move_select_log_ticks", p.MoveSelectLogTicks);
            p.PathOfferLogTicks = I(o, "path_offer_log_ticks", p.PathOfferLogTicks);
            p.LedgePounceLipScanTiles = I(o, "ledge_pounce_lip_scan_tiles", p.LedgePounceLipScanTiles);
            p.LedgeApproachLogBurst = I(o, "ledge_approach_log_burst", p.LedgeApproachLogBurst);
            p.LedgeApproachBurstGapTicks = I(o, "ledge_approach_burst_gap_ticks", p.LedgeApproachBurstGapTicks);
            p.LedgePounceLipSurfaces = S(o, "ledge_pounce_lip_surfaces", p.LedgePounceLipSurfaces);
            p.LedgePounceGatherSlackFac = F(o, "ledge_pounce_gather_slack_fac", p.LedgePounceGatherSlackFac);
            p.PounceMaxLaunchDeg = F(o, "pounce_max_launch_deg", p.PounceMaxLaunchDeg);
            p.SqueezeEnabled = B(o, "squeeze_enabled", p.SqueezeEnabled);
            p.SqueezeStartFrames = F(o, "squeeze_start_frames", p.SqueezeStartFrames);
            p.SqueezeFullFrames = F(o, "squeeze_full_frames", p.SqueezeFullFrames);
            p.SqueezeMinFac = F(o, "squeeze_min_fac", p.SqueezeMinFac);
            p.SqueezeLimbFac = F(o, "squeeze_limb_fac", p.SqueezeLimbFac);
            p.SqueezeBodyFac = F(o, "squeeze_body_fac", p.SqueezeBodyFac);
            p.SqueezeTailFac = F(o, "squeeze_tail_fac", p.SqueezeTailFac);
            p.SqueezeSpineLenFac = F(o, "squeeze_spine_len_fac", p.SqueezeSpineLenFac);
            p.SqueezeDrawFromTerrain = B(o, "squeeze_draw_from_terrain", p.SqueezeDrawFromTerrain);
            p.SqueezeDrawTerrainMax = F(o, "squeeze_draw_terrain_max", p.SqueezeDrawTerrainMax);
            p.SqueezeDrawTerrainLerp = F(o, "squeeze_draw_terrain_lerp", p.SqueezeDrawTerrainLerp);
            p.SqueezeSlopesOffFrames = I(o, "squeeze_slopes_off_frames", p.SqueezeSlopesOffFrames);
            p.SqueezeSpinePull = B(o, "squeeze_spine_pull", p.SqueezeSpinePull);
            p.SqueezeBacklogSteps = I(o, "squeeze_backlog_steps", p.SqueezeBacklogSteps);
            p.SqueezeProgressPx = F(o, "squeeze_progress_px", p.SqueezeProgressPx);
            p.SqueezeProgressWindow = I(o, "squeeze_progress_window", p.SqueezeProgressWindow);
            p.SqueezeSpanMinFac = F(o, "squeeze_span_min_fac", p.SqueezeSpanMinFac);
            p.SpanFloorCorrectPx = F(o, "span_floor_correct_px", p.SpanFloorCorrectPx);
            p.SpanCollapseWarnTicks = I(o, "span_collapse_warn_ticks", p.SpanCollapseWarnTicks);
            p.SpanLogTicks = I(o, "span_log_ticks", p.SpanLogTicks);
            p.SpanLogFloorTicks = I(o, "span_log_floor_ticks", p.SpanLogFloorTicks);
            p.SpanFloorGraceTicks = I(o, "span_floor_grace_ticks", p.SpanFloorGraceTicks);
            p.SqueezeSpinePullAlways = B(o, "squeeze_spine_pull_always", p.SqueezeSpinePullAlways);
            p.DriveHeadLed = B(o, "drive_head_led", p.DriveHeadLed);
            p.DriveGroundDescentClamp = B(o, "drive_ground_descent_clamp", p.DriveGroundDescentClamp);
            p.DriveSlopeHorizontal = B(o, "drive_slope_horizontal", p.DriveSlopeHorizontal);
            p.DriveTrailSlackPx = F(o, "drive_trail_slack_px", p.DriveTrailSlackPx);
            p.DriveTrailBackstepFac = F(o, "drive_trail_backstep_fac", p.DriveTrailBackstepFac);
            p.DriveMidSpeedFac = F(o, "drive_mid_speed_fac", p.DriveMidSpeedFac);
            p.DriveMidAccelFac = F(o, "drive_mid_accel_fac", p.DriveMidAccelFac);
            p.DriveRearSpeedFac = F(o, "drive_rear_speed_fac", p.DriveRearSpeedFac);
            p.DriveRearAccelFac = F(o, "drive_rear_accel_fac", p.DriveRearAccelFac);
            p.RunSpeedIdle = F(o, "run_speed_idle", p.RunSpeedIdle);
            p.RunSpeedHunt = F(o, "run_speed_hunt", p.RunSpeedHunt);
            p.RunSpeedFlee = F(o, "run_speed_flee", p.RunSpeedFlee);
            p.IdleWanderChance = F(o, "idle_wander_chance", p.IdleWanderChance);
            p.RunSpeedIdleStillFac = F(o, "run_speed_idle_still_fac", p.RunSpeedIdleStillFac);
            p.IdleUtilityThreshold = F(o, "idle_utility_threshold", p.IdleUtilityThreshold);
            p.IdleStayChance = F(o, "idle_stay_chance", p.IdleStayChance);
            p.IdleCommitWinStreak = I(o, "idle_commit_win_streak", p.IdleCommitWinStreak);
            p.IdleCommitLockMin = I(o, "idle_commit_lock_min", p.IdleCommitLockMin);
            p.IdleCommitLockMax = I(o, "idle_commit_lock_max", p.IdleCommitLockMax);
            p.IdleSwitchMinFrames = I(o, "idle_switch_min_frames", p.IdleSwitchMinFrames);
            p.IdleSwitchMaxFrames = I(o, "idle_switch_max_frames", p.IdleSwitchMaxFrames);
            p.PanicUtilityThreshold = F(o, "panic_utility_threshold", p.PanicUtilityThreshold);
            p.FleeReevaluations = I(o, "flee_reevaluations", p.FleeReevaluations);
            p.FleeMaxDistance = I(o, "flee_max_distance", p.FleeMaxDistance);
            p.BiteDamage = F(o, "bite_damage", p.BiteDamage);
            p.BiteStunBonus = F(o, "bite_stun_bonus", p.BiteStunBonus);
            p.BiteRadius = F(o, "bite_radius", p.BiteRadius);
            p.BiteCooldown = I(o, "bite_cooldown", p.BiteCooldown);
            p.BiteMomentumFac = F(o, "bite_momentum_fac", p.BiteMomentumFac);
            p.BiteThroughWalls = B(o, "bite_through_walls", p.BiteThroughWalls);
            p.BiteContactRadius = F(o, "bite_contact_radius", p.BiteContactRadius);
            p.BiteContactGap = F(o, "bite_contact_gap", p.BiteContactGap);
            p.BiteFacingDot = F(o, "bite_facing_dot", p.BiteFacingDot);
            p.BiteFacingPounceDot = F(o, "bite_facing_pounce_dot", p.BiteFacingPounceDot);
            p.BiteFacingChargeDot = F(o, "bite_facing_charge_dot", p.BiteFacingChargeDot);
            p.BiteSweptContact = B(o, "bite_swept_contact", p.BiteSweptContact);
            p.BiteSweptMinSpeed = F(o, "bite_swept_min_speed", p.BiteSweptMinSpeed);
            p.LungeSpeed = F(o, "lunge_speed", p.LungeSpeed);
            p.LungeMaxSpeed = F(o, "lunge_max_speed", p.LungeMaxSpeed);
            p.CarryEasePx = F(o, "carry_ease_px", p.CarryEasePx);
            p.PounceEnabled = B(o, "pounce_enabled", p.PounceEnabled);
            p.PounceDistance = F(o, "pounce_distance", p.PounceDistance);
            p.PounceMinDistance = F(o, "pounce_min_distance", p.PounceMinDistance);
            p.PounceCrouchFrames = I(o, "pounce_crouch_frames", p.PounceCrouchFrames);
            p.PouncePropulsionFrames = I(o, "pounce_propulsion_frames", p.PouncePropulsionFrames);
            p.PounceMaxFrames = I(o, "pounce_max_frames", p.PounceMaxFrames);
            p.PounceSpeed = F(o, "pounce_speed", p.PounceSpeed);
            p.PounceLift = F(o, "pounce_lift", p.PounceLift);
            p.PounceCooldown = I(o, "pounce_cooldown", p.PounceCooldown);
            p.PounceRecoveryStun = I(o, "pounce_recovery_stun", p.PounceRecoveryStun);
            p.PounceChance = F(o, "pounce_chance", p.PounceChance);
            p.PounceRetryDelay = I(o, "pounce_retry_delay", p.PounceRetryDelay);
            p.PounceCrouchPullback = F(o, "pounce_crouch_pullback", p.PounceCrouchPullback);
            p.PounceThrustFalloff = F(o, "pounce_thrust_falloff", p.PounceThrustFalloff);
            p.PounceGateRetryDelay = I(o, "pounce_gate_retry_delay", p.PounceGateRetryDelay);

            p.PounceGatherFrames = I(o, "pounce_gather_frames", p.PounceGatherFrames);
            p.PounceGatherPull = F(o, "pounce_gather_pull", p.PounceGatherPull);
            p.PounceGatherCompress = F(o, "pounce_gather_compress", p.PounceGatherCompress);
            p.PounceGatherArch = F(o, "pounce_gather_arch", p.PounceGatherArch);
            p.PounceGatherReadyRise = F(o, "pounce_gather_ready_rise", p.PounceGatherReadyRise);
            p.PounceGatherReleaseTicks = I(o, "pounce_gather_release_ticks", p.PounceGatherReleaseTicks);
            p.PounceLaunchSpeed = F(o, "pounce_launch_speed", p.PounceLaunchSpeed);
            p.PounceLaunchLift = F(o, "pounce_launch_lift", p.PounceLaunchLift);
            p.PounceLaunchPop = F(o, "pounce_launch_pop", p.PounceLaunchPop);
            p.PouncePitchCouple = F(o, "pounce_pitch_couple", p.PouncePitchCouple);
            p.PounceHeadLead = F(o, "pounce_head_lead", p.PounceHeadLead);
            p.PounceRearLag = F(o, "pounce_rear_lag", p.PounceRearLag);
            p.PounceBoost = F(o, "pounce_boost", p.PounceBoost);
            p.PounceBoostFrames = I(o, "pounce_boost_frames", p.PounceBoostFrames);
            p.PounceFlightMaxFrames = I(o, "pounce_flight_max_frames", p.PounceFlightMaxFrames);
            p.PounceFlightStretch = F(o, "pounce_flight_stretch", p.PounceFlightStretch);
            p.PounceLandFrames = I(o, "pounce_land_frames", p.PounceLandFrames);
            p.PounceLandDamp = F(o, "pounce_land_damp", p.PounceLandDamp);
            p.PounceLandCompress = F(o, "pounce_land_compress", p.PounceLandCompress);
            p.PounceRangeScaling = B(o, "pounce_range_scaling", p.PounceRangeScaling);
            p.PounceLaunchMinFrac = F(o, "pounce_launch_min_frac", p.PounceLaunchMinFrac);
            p.PounceRangeOvershootFac = F(o, "pounce_range_overshoot_fac", p.PounceRangeOvershootFac);
            p.PounceRiseMargin = F(o, "pounce_rise_margin", p.PounceRiseMargin);
            p.PounceGatherFramesMin = I(o, "pounce_gather_frames_min", p.PounceGatherFramesMin);
            p.PounceAttackEnabled = B(o, "pounce_attack_enabled", p.PounceAttackEnabled);
            p.PounceVariationDev = F(o, "pounce_variation_dev", p.PounceVariationDev);
            p.PounceVariationK = F(o, "pounce_variation_k", p.PounceVariationK);
            p.PounceVariationGatherFac = F(o, "pounce_variation_gather_fac", p.PounceVariationGatherFac);
            p.PounceRoomClamp = B(o, "pounce_room_clamp", p.PounceRoomClamp);
            p.PounceRoomMargin = F(o, "pounce_room_margin", p.PounceRoomMargin);
            p.PounceRequireLanding = B(o, "pounce_require_landing", p.PounceRequireLanding);
            p.PounceLandingScanTiles = I(o, "pounce_landing_scan_tiles", p.PounceLandingScanTiles);
            p.PounceLandingCheckObstruction = B(o, "pounce_landing_check_obstruction", p.PounceLandingCheckObstruction);
            p.PounceLandingRequireStandingRoom = B(o, "pounce_landing_require_standing_room", p.PounceLandingRequireStandingRoom);
            p.PounceAlignRate = F(o, "pounce_align_rate", p.PounceAlignRate);
            p.PounceAlignMaxDeg = F(o, "pounce_align_max_deg", p.PounceAlignMaxDeg);
            p.PounceAlignMinSpeed = F(o, "pounce_align_min_speed", p.PounceAlignMinSpeed);
            p.PounceAlignGroundFade = F(o, "pounce_align_ground_fade", p.PounceAlignGroundFade);
            p.PounceAlignMaxDownDeg = F(o, "pounce_align_max_down_deg", p.PounceAlignMaxDownDeg);
            p.PounceHindPlant = B(o, "pounce_hind_plant", p.PounceHindPlant);
            p.PounceHindReleaseTicks = I(o, "pounce_hind_release_ticks", p.PounceHindReleaseTicks);

            p.ChargeEnabled = B(o, "charge_enabled", p.ChargeEnabled);
            p.ChargeDistance = F(o, "charge_distance", p.ChargeDistance);
            p.ChargeMinDistance = F(o, "charge_min_distance", p.ChargeMinDistance);
            p.ChargeChance = F(o, "charge_chance", p.ChargeChance);
            p.ChargeFacingDot = F(o, "charge_facing_dot", p.ChargeFacingDot);
            p.ChargeWindupFrames = I(o, "charge_windup_frames", p.ChargeWindupFrames);
            p.ChargeWindupPull = F(o, "charge_windup_pull", p.ChargeWindupPull);
            p.ChargeThrust = F(o, "charge_thrust", p.ChargeThrust);
            p.ChargeThrustFrames = I(o, "charge_thrust_frames", p.ChargeThrustFrames);
            p.ChargeMaxFrames = I(o, "charge_max_frames", p.ChargeMaxFrames);
            p.ChargeMaxSpeed = F(o, "charge_max_speed", p.ChargeMaxSpeed);
            p.ChargeAimTrack = F(o, "charge_aim_track", p.ChargeAimTrack);
            p.ChargeRequireGrounded = B(o, "charge_require_grounded", p.ChargeRequireGrounded);
            p.ChargeRecoveryStun = I(o, "charge_recovery_stun", p.ChargeRecoveryStun);
            p.ChargeCooldown = I(o, "charge_cooldown", p.ChargeCooldown);
            p.AttackStateCeilingTicks = I(o, "attack_state_ceiling_ticks", p.AttackStateCeilingTicks);
            p.ChargeGateRetryDelay = I(o, "charge_gate_retry_delay", p.ChargeGateRetryDelay);
            p.ChargeRoomMargin = F(o, "charge_room_margin", p.ChargeRoomMargin);
            p.ChargeRequireLanding = B(o, "charge_require_landing", p.ChargeRequireLanding);
            p.ChargeLandingSamples = I(o, "charge_landing_samples", p.ChargeLandingSamples);
            p.ChargeStallSpeed = F(o, "charge_stall_speed", p.ChargeStallSpeed);
            p.ChargeStallGrace = I(o, "charge_stall_grace", p.ChargeStallGrace);

            MigrateRetiredPounceKey(o, "pounce_crouch_frames", "pounce_gather_frames",
                                    p.PounceCrouchFrames, ref p.PounceGatherFrames);
            MigrateRetiredPounceKeyF(o, "pounce_crouch_pullback", "pounce_gather_pull",
                                     p.PounceCrouchPullback, ref p.PounceGatherPull);
            MigrateRetiredPounceKeyF(o, "pounce_speed", "pounce_launch_speed",
                                     p.PounceSpeed, ref p.PounceLaunchSpeed);
            MigrateRetiredPounceKeyF(o, "pounce_lift", "pounce_launch_lift",
                                     p.PounceLift, ref p.PounceLaunchLift);

            p.MoveLogTicks = I(o, "move_log_ticks", p.MoveLogTicks);
            p.PathLatchTicks = I(o, "path_latch_ticks", p.PathLatchTicks);
            p.PathLatchArrive = F(o, "path_latch_arrive", p.PathLatchArrive);
            p.PathLatchMaxRejects = I(o, "path_latch_max_rejects", p.PathLatchMaxRejects);
            p.PathBodyTieBreak = B(o, "path_body_tie_break", p.PathBodyTieBreak);
            p.PathTieBreakMaxStreak = I(o, "path_tiebreak_max_streak", p.PathTieBreakMaxStreak);
            p.PathTiebreakCommitTicks = I(o, "path_tiebreak_commit_ticks", p.PathTiebreakCommitTicks);
            p.PathTiebreakNormaliseTurn = B(o, "path_tiebreak_normalise_turn", p.PathTiebreakNormaliseTurn);
            p.PathTiebreakTravelPreference = B(o, "path_tiebreak_travel_preference", p.PathTiebreakTravelPreference);
            p.TurnFrames = I(o, "turn_frames", p.TurnFrames);
            p.TurnTriggerDot = F(o, "turn_trigger_dot", p.TurnTriggerDot);
            p.TurnTriggerMinHorizontal = F(o, "turn_trigger_min_horizontal", p.TurnTriggerMinHorizontal);
            p.TurnRate = F(o, "turn_rate", p.TurnRate);
            p.TurnCooldown = I(o, "turn_cooldown", p.TurnCooldown);
            p.TurnTuck = F(o, "turn_tuck", p.TurnTuck);
            p.TurnSwap = B(o, "turn_swap", p.TurnSwap);
            p.TurnMode = S(o, "turn_mode", p.TurnMode);
            p.TurnMaxStepPx = F(o, "turn_max_step_px", p.TurnMaxStepPx);
            p.TurnPivotSpeed = F(o, "turn_pivot_speed", p.TurnPivotSpeed);
            p.TurnPivotFriction = F(o, "turn_pivot_friction", p.TurnPivotFriction);
            p.TurnPivotLead = F(o, "turn_pivot_lead", p.TurnPivotLead);
            p.TurnPivotMidFac = F(o, "turn_pivot_mid_fac", p.TurnPivotMidFac);
            p.TurnPivotBrake = F(o, "turn_pivot_brake", p.TurnPivotBrake);
            p.TurnPivotJitter = F(o, "turn_pivot_jitter", p.TurnPivotJitter);
            p.TurnPivotStretchPx = F(o, "turn_pivot_stretch_px", p.TurnPivotStretchPx);
            p.TurnPivotSpanClamp = B(o, "turn_pivot_span_clamp", p.TurnPivotSpanClamp);
            p.TurnFocusHold = B(o, "turn_focus_hold", p.TurnFocusHold);
            p.TurnFocusHoldDot = F(o, "turn_focus_hold_dot", p.TurnFocusHoldDot);
            p.TurnMinIntervalTicks = I(o, "turn_min_interval_ticks", p.TurnMinIntervalTicks);
            p.TurnPivotCrossFac = F(o, "turn_pivot_cross_fac", p.TurnPivotCrossFac);
            p.TurnPivotFinish = F(o, "turn_pivot_finish", p.TurnPivotFinish);
            p.TurnPivotReleaseDot = F(o, "turn_pivot_release_dot", p.TurnPivotReleaseDot);
            p.TurnPivotReleaseSpanFac = F(o, "turn_pivot_release_span_fac", p.TurnPivotReleaseSpanFac);
            p.TurnPivotSwingPx = F(o, "turn_pivot_swing_px", p.TurnPivotSwingPx);
            p.TurnPivotGoalDot = F(o, "turn_pivot_goal_dot", p.TurnPivotGoalDot);
            p.TurnPivotMaxTicks = I(o, "turn_pivot_max_ticks", p.TurnPivotMaxTicks);
            p.TurnPivotPushOff = B(o, "turn_pivot_push_off", p.TurnPivotPushOff);
            p.TurnPivotSpanFloorFac = F(o, "turn_pivot_span_floor_fac", p.TurnPivotSpanFloorFac);
            p.TurnLogTicks = I(o, "turn_log_ticks", p.TurnLogTicks);
            p.TurnBrake = F(o, "turn_brake", p.TurnBrake);
            p.TurnAbortAirTicks = I(o, "turn_abort_air_ticks", p.TurnAbortAirTicks);
            p.TurnInWater = B(o, "turn_in_water", p.TurnInWater);
            p.TurnCarryRotate = B(o, "turn_carry_rotate", p.TurnCarryRotate);
            p.TurnSpineRelax = F(o, "turn_spine_relax", p.TurnSpineRelax);
            p.TurnSpineRecoverTicks = I(o, "turn_spine_recover_ticks", p.TurnSpineRecoverTicks);
            p.TurnVerticalHold = F(o, "turn_vertical_hold", p.TurnVerticalHold);
            p.TurnHoldMaxTicks = I(o, "turn_hold_max_ticks", p.TurnHoldMaxTicks);
            if (o.TryGet("turn_swap").HasValue && !o.TryGet("turn_mode").HasValue)
            {
                p.TurnMode = p.TurnSwap ? "swap" : "pivot";
            }
            p.BodyTurnForeshorten = F(o, "body_turn_foreshorten", p.BodyTurnForeshorten);
            p.DepthVerticalHold = B(o, "depth_vertical_hold", p.DepthVerticalHold);
            p.DepthVerticalHoldBand = F(o, "depth_vertical_hold_band", p.DepthVerticalHoldBand);
            p.DrawUpSignLatch = B(o, "draw_up_sign_latch", p.DrawUpSignLatch);
            p.DrawUpSignDeadband = F(o, "draw_up_sign_deadband", p.DrawUpSignDeadband);
            p.PounceTailWhip = F(o, "pounce_tail_whip", p.PounceTailWhip);
            p.LimbSweepSpeedGate = F(o, "limb_sweep_speed_gate", p.LimbSweepSpeedGate);
            p.LimbSweepMinGrip = I(o, "limb_sweep_min_grip", p.LimbSweepMinGrip);
            p.LimbSweepBodySpeedGate = F(o, "limb_sweep_body_speed_gate", p.LimbSweepBodySpeedGate);
            p.EatInPlaceEnabled = B(o, "eat_in_place_enabled", p.EatInPlaceEnabled);
            p.DenFedEnabled = B(o, "den_fed_enabled", p.DenFedEnabled);
            p.ConspecificAwareness = B(o, "conspecific_awareness", p.ConspecificAwareness);
            p.RoamRadiusTiles = I(o, "roam_radius_tiles", p.RoamRadiusTiles);
            p.RoamAttemptsPerCandidate = I(o, "roam_attempts_per_candidate", p.RoamAttemptsPerCandidate);
            p.HuntForgetFrames = I(o, "hunt_forget_frames", p.HuntForgetFrames);
            p.DenRainUtilityThreshold = F(o, "den_rain_utility_threshold", p.DenRainUtilityThreshold);
            p.DenHealthThreshold = F(o, "den_health_threshold", p.DenHealthThreshold);
            p.DenGiveUpTicks = I(o, "den_give_up_ticks", p.DenGiveUpTicks);
            p.HuntForgetCooldownTicks = I(o, "hunt_forget_cooldown_ticks", p.HuntForgetCooldownTicks);

            p.VisionPerfectAngle = F(o, "vision_perfect_angle", p.VisionPerfectAngle);
            p.VisionPeripheralAngle = F(o, "vision_peripheral_angle", p.VisionPeripheralAngle);
            p.SenseRadius = F(o, "sense_radius", p.SenseRadius);
            p.SenseMaxScore = F(o, "sense_max_score", p.SenseMaxScore);
            p.SenseThroughWalls = B(o, "sense_through_walls", p.SenseThroughWalls);
            p.SenseWallRadiusFac = F(o, "sense_wall_radius_fac", p.SenseWallRadiusFac);
            p.SenseWallScoreFac = F(o, "sense_wall_score_fac", p.SenseWallScoreFac);
            p.SenseOmniIntervalTicks = I(o, "sense_omni_interval_ticks", p.SenseOmniIntervalTicks);
            p.SenseOmniInLosFull = B(o, "sense_omni_in_los_full", p.SenseOmniInLosFull);

            p.ScentEnabled = B(o, "scent_enabled", p.ScentEnabled);
            p.ScentRadiusTiles = F(o, "scent_radius_tiles", p.ScentRadiusTiles);
            p.ScentMaxScore = F(o, "scent_max_score", p.ScentMaxScore);
            p.ScentWallRadiusFac = F(o, "scent_wall_radius_fac", p.ScentWallRadiusFac);
            p.ScentWallScoreFac = F(o, "scent_wall_score_fac", p.ScentWallScoreFac);
            p.ScentIntervalTicks = I(o, "scent_interval_ticks", p.ScentIntervalTicks);
            p.ScentPassiveFac = F(o, "scent_passive_fac", p.ScentPassiveFac);
            p.ScentAppetiteScoreFac = F(o, "scent_appetite_score_fac", p.ScentAppetiteScoreFac);
            p.ScentAppetiteRangeFac = F(o, "scent_appetite_range_fac", p.ScentAppetiteRangeFac);
            p.ScentSniffPitchDeg = F(o, "scent_sniff_pitch_deg", p.ScentSniffPitchDeg);
            p.ScentSniffTicks = I(o, "scent_sniff_ticks", p.ScentSniffTicks);
            p.ScentSniffCooldownTicks = I(o, "scent_sniff_cooldown_ticks", p.ScentSniffCooldownTicks);

            p.CarryMaxMassRatio = F(o, "carry_max_mass_ratio", p.CarryMaxMassRatio);
            p.GraspDominance = F(o, "grasp_dominance", p.GraspDominance);
            p.CarryHoldDistance = F(o, "carry_hold_distance", p.CarryHoldDistance);
            p.CarryLagDropDistance = F(o, "carry_lag_drop_distance", p.CarryLagDropDistance);
            p.CarryLagDropMomentum = F(o, "carry_lag_drop_momentum", p.CarryLagDropMomentum);
            p.CarryWaterDropSubmersion = F(o, "carry_water_drop_submersion", p.CarryWaterDropSubmersion);
            p.DropPreyDamage = F(o, "drop_prey_damage", p.DropPreyDamage);
            p.EatFrames = I(o, "eat_frames", p.EatFrames);
            p.EatSafeThreat = F(o, "eat_safe_threat", p.EatSafeThreat);
            p.EatReach = F(o, "eat_reach", p.EatReach);
            p.EatPoseRate = F(o, "eat_pose_rate", p.EatPoseRate);
            p.EatCrouchDrop = F(o, "eat_crouch_drop", p.EatCrouchDrop);
            p.EatHeadFac = F(o, "eat_head_fac", p.EatHeadFac);
            p.EatMeatPerMeal = I(o, "eat_meat_per_meal", p.EatMeatPerMeal);
            p.EatsCarrion = B(o, "eats_carrion", p.EatsCarrion);
            p.CarrionIntensityFac = F(o, "carrion_intensity_fac", p.CarrionIntensityFac);
            p.CarrionFromAllPrey = B(o, "carrion_from_all_prey", p.CarrionFromAllPrey);
            p.ConspecificSpacing = F(o, "conspecific_spacing", p.ConspecificSpacing);
            p.RoamThreatWeight = F(o, "roam_threat_weight", p.RoamThreatWeight);
            p.RoamNoiseWeight = F(o, "roam_noise_weight", p.RoamNoiseWeight);
            p.RoamDistanceWeight = F(o, "roam_distance_weight", p.RoamDistanceWeight);
            p.RoamPickJitter = F(o, "roam_pick_jitter", p.RoamPickJitter);
            p.RoamCandidates = I(o, "roam_candidates", p.RoamCandidates);
            p.DenRestFrames = I(o, "den_rest_frames", p.DenRestFrames);
            p.DenFedPriority = F(o, "den_fed_priority", p.DenFedPriority);
            p.StalkEnabled = B(o, "stalk_enabled", p.StalkEnabled);
            p.StalkDistance = F(o, "stalk_distance", p.StalkDistance);
            p.RunSpeedStalk = F(o, "run_speed_stalk", p.RunSpeedStalk);
            p.StalkBreakDistance = F(o, "stalk_break_distance", p.StalkBreakDistance);
            p.StalkPreySpeedBreak = F(o, "stalk_prey_speed_break", p.StalkPreySpeedBreak);
            p.StalkCrouchFac = F(o, "stalk_crouch_fac", p.StalkCrouchFac);

            p.HungerPerCycle = F(o, "hunger_per_cycle", p.HungerPerCycle);
            p.StarveRegenFac = F(o, "starve_regen_fac", p.StarveRegenFac);
            p.StarveAggressionFac = F(o, "starve_aggression_fac", p.StarveAggressionFac);
            p.MealValue = F(o, "meal_value", p.MealValue);
            p.StarveLethal = B(o, "starve_lethal", p.StarveLethal);

            p.LandSafeSpeed = F(o, "land_safe_speed", p.LandSafeSpeed);
            p.LandDamageSpeed = F(o, "land_damage_speed", p.LandDamageSpeed);
            p.LandStunPerSpeed = F(o, "land_stun_per_speed", p.LandStunPerSpeed);
            p.LandDamagePerSpeed = F(o, "land_damage_per_speed", p.LandDamagePerSpeed);
            p.LandSoftFac = F(o, "land_soft_fac", p.LandSoftFac);
            p.LandMinAirTicks = I(o, "land_min_air_ticks", p.LandMinAirTicks);
            p.LandPounceGrace = I(o, "land_pounce_grace", p.LandPounceGrace);
            p.LandRightStrength = F(o, "land_right_strength", p.LandRightStrength);
            p.LandRightProximity = I(o, "land_right_proximity", p.LandRightProximity);
            p.LandRightMinFallSpeed = F(o, "land_right_min_fall_speed", p.LandRightMinFallSpeed);
            p.LandRightFallHysteresis = F(o, "land_right_fall_hysteresis", p.LandRightFallHysteresis);
            p.LandRightSuppressFac = F(o, "land_right_suppress_fac", p.LandRightSuppressFac);
            p.LandRightAxisSignLatch = B(o, "land_right_axis_sign_latch", p.LandRightAxisSignLatch);
            p.LandRightAxisSignBand = F(o, "land_right_axis_sign_band", p.LandRightAxisSignBand);
            p.LandRightAirAlways = B(o, "land_right_air_always", p.LandRightAirAlways);
            p.LandRightAirFac = F(o, "land_right_air_fac", p.LandRightAirFac);
            p.LandRightAirMinTicks = I(o, "land_right_air_min_ticks", p.LandRightAirMinTicks);
            p.LandRightSuppressInCorridor = B(o, "land_right_suppress_in_corridor", p.LandRightSuppressInCorridor);
            p.LandSound = S(o, "land_sound", p.LandSound);

            p.CamoEnabled = B(o, "camo_enabled", p.CamoEnabled);
            p.CamoVisibilityBonus = F(o, "camo_visibility_bonus", p.CamoVisibilityBonus);
            p.CamoSpeedHidden = F(o, "camo_speed_hidden", p.CamoSpeedHidden);
            p.CamoSpeedVisible = F(o, "camo_speed_visible", p.CamoSpeedVisible);
            p.CamoArmRate = F(o, "camo_arm_rate", p.CamoArmRate);
            p.CamoBreakRate = F(o, "camo_break_rate", p.CamoBreakRate);
            p.CamoBreakHoldTicks = I(o, "camo_break_hold_ticks", p.CamoBreakHoldTicks);
            p.CamoBreakHealth = F(o, "camo_break_health", p.CamoBreakHealth);
            p.CamoBreakOnStun = B(o, "camo_break_on_stun", p.CamoBreakOnStun);
            p.CamoBreakOnAttack = B(o, "camo_break_on_attack", p.CamoBreakOnAttack);
            p.CamoBreakOnHurt = B(o, "camo_break_on_hurt", p.CamoBreakOnHurt);
            p.CamoBlendMax = F(o, "camo_blend_max", p.CamoBlendMax);
            p.CamoColorSource = S(o, "camo_color_source", p.CamoColorSource);
            p.CamoColorDrift = F(o, "camo_color_drift", p.CamoColorDrift);
            p.CamoSampleDrawnBody = B(o, "camo_sample_drawn_body", p.CamoSampleDrawnBody);
            p.CamoColorFogMix = F(o, "camo_color_fog_mix", p.CamoColorFogMix);
            p.CamoHiddenThreshold = F(o, "camo_hidden_threshold", p.CamoHiddenThreshold);
            p.CamoLogTicks = I(o, "camo_log_ticks", p.CamoLogTicks);

            p.BaitItemTypes = S(o, "bait_item_types", p.BaitItemTypes);
            p.BaitPearlMiscOnly = B(o, "bait_pearl_misc_only", p.BaitPearlMiscOnly);
            p.BaitDominanceGate = B(o, "bait_dominance_gate", p.BaitDominanceGate);
            p.BaitTrackMaxItems = I(o, "bait_track_max_items", p.BaitTrackMaxItems);
            p.BaitTrackRememberTicks = I(o, "bait_track_remember_ticks", p.BaitTrackRememberTicks);
            p.BaitTrackForgetTiles = I(o, "bait_track_forget_tiles", p.BaitTrackForgetTiles);
            p.BaitGrabRangePx = F(o, "bait_grab_range_px", p.BaitGrabRangePx);
            p.BaitGraspDominance = F(o, "bait_grasp_dominance", p.BaitGraspDominance);
            p.BaitSeekEnabled = B(o, "bait_seek_enabled", p.BaitSeekEnabled);
            p.BaitPickVisibleFac = F(o, "bait_pick_visible_fac", p.BaitPickVisibleFac);
            p.BaitChooseIntervalTicks = I(o, "bait_choose_interval_ticks", p.BaitChooseIntervalTicks);
            p.BaitSeekMaxTicks = I(o, "bait_seek_max_ticks", p.BaitSeekMaxTicks);
            p.BaitRunSpeed = F(o, "bait_run_speed", p.BaitRunSpeed);
            p.BaitSpotRadiusTiles = I(o, "bait_spot_radius_tiles", p.BaitSpotRadiusTiles);
            p.BaitSpotCandidates = I(o, "bait_spot_candidates", p.BaitSpotCandidates);
            p.BaitPrepMinDistFac = F(o, "bait_prep_min_dist_fac", p.BaitPrepMinDistFac);
            p.BaitPrepMaxDistFac = F(o, "bait_prep_max_dist_fac", p.BaitPrepMaxDistFac);
            p.BaitPrepMaxRiseTiles = I(o, "bait_prep_max_rise_tiles", p.BaitPrepMaxRiseTiles);
            p.BaitPrepHeadroomTiles = I(o, "bait_prep_headroom_tiles", p.BaitPrepHeadroomTiles);
            p.BaitPrepDistWeight = F(o, "bait_prep_dist_weight", p.BaitPrepDistWeight);
            p.BaitPrepLevelWeight = F(o, "bait_prep_level_weight", p.BaitPrepLevelWeight);
            p.BaitPrepCoverWeight = F(o, "bait_prep_cover_weight", p.BaitPrepCoverWeight);
            p.BaitSpotTravelWeight = F(o, "bait_spot_travel_weight", p.BaitSpotTravelWeight);
            p.BaitPlanMaxAttempts = I(o, "bait_plan_max_attempts", p.BaitPlanMaxAttempts);
            p.BaitArrivePx = F(o, "bait_arrive_px", p.BaitArrivePx);
            p.BaitCarryMaxTicks = I(o, "bait_carry_max_ticks", p.BaitCarryMaxTicks);
            p.BaitSettleTicks = I(o, "bait_settle_ticks", p.BaitSettleTicks);
            p.BaitMovedPx = F(o, "bait_moved_px", p.BaitMovedPx);
            p.BaitWaitRunSpeed = F(o, "bait_wait_run_speed", p.BaitWaitRunSpeed);
            p.BaitWaitMaxTicks = I(o, "bait_wait_max_ticks", p.BaitWaitMaxTicks);
            p.BaitCooldownTicks = I(o, "bait_cooldown_ticks", p.BaitCooldownTicks);
            p.BaitHoldsAgainstHunt = B(o, "bait_holds_against_hunt", p.BaitHoldsAgainstHunt);
            p.BaitWaitHoldsAgainstHunt = B(o, "bait_wait_holds_against_hunt", p.BaitWaitHoldsAgainstHunt);
            p.BaitSpringRule = S(o, "bait_spring_rule", p.BaitSpringRule);
            p.BaitTriggerRangePx = F(o, "bait_trigger_range_px", p.BaitTriggerRangePx);
            p.BaitPounceRoll = B(o, "bait_pounce_roll", p.BaitPounceRoll);
            p.BaitFaceRunSpeed = F(o, "bait_face_run_speed", p.BaitFaceRunSpeed);
            p.BaitFaceMaxTicks = I(o, "bait_face_max_ticks", p.BaitFaceMaxTicks);
            p.BaitExitWeight = F(o, "bait_exit_weight", p.BaitExitWeight);
            p.BaitExitReachBonus = F(o, "bait_exit_reach_bonus", p.BaitExitReachBonus);
            p.BaitExitCentreBonus = F(o, "bait_exit_centre_bonus", p.BaitExitCentreBonus);
            p.BaitExitNearSteps = I(o, "bait_exit_near_steps", p.BaitExitNearSteps);
            p.BaitExitNearPenalty = F(o, "bait_exit_near_penalty", p.BaitExitNearPenalty);
            p.BaitExitPipeTiles = F(o, "bait_exit_pipe_tiles", p.BaitExitPipeTiles);
            p.BaitExitPipeExitPenalty = F(o, "bait_exit_pipe_exit_penalty", p.BaitExitPipeExitPenalty);
            p.BaitExitPipeOtherPenalty = F(o, "bait_exit_pipe_other_penalty", p.BaitExitPipeOtherPenalty);
            p.BaitExitPipeAtPrep = B(o, "bait_exit_pipe_at_prep", p.BaitExitPipeAtPrep);
            p.BaitExitNonePenalty = F(o, "bait_exit_none_penalty", p.BaitExitNonePenalty);
            p.BaitEnabled = B(o, "bait_enabled", p.BaitEnabled);

            p.RepEnabled = B(o, "rep_enabled", p.RepEnabled);
            p.RepCommunityWeight = F(o, "rep_community_weight", p.RepCommunityWeight);
            p.RepMemoryWeight = F(o, "rep_memory_weight", p.RepMemoryWeight);
            p.RepPersonalityAmount = F(o, "rep_personality_amount", p.RepPersonalityAmount);
            p.RepHostileThreshold = F(o, "rep_hostile_threshold", p.RepHostileThreshold);
            p.RepFriendlyThreshold = F(o, "rep_friendly_threshold", p.RepFriendlyThreshold);
            p.RepAggressionExponent = F(o, "rep_aggression_exponent", p.RepAggressionExponent);
            p.RepFriendlyStayOutOfWay = B(o, "rep_friendly_stay_out_of_way", p.RepFriendlyStayOutOfWay);
            p.RepDebugLike = F(o, "rep_debug_like", p.RepDebugLike);
            p.RepLogMinInterval = I(o, "rep_log_min_interval", p.RepLogMinInterval);
            p.RepMemoryFromAttacks = B(o, "rep_memory_from_attacks", p.RepMemoryFromAttacks);
            p.RepMemoryEventScale = F(o, "rep_memory_event_scale", p.RepMemoryEventScale);
            p.RepMemoryDecay = F(o, "rep_memory_decay", p.RepMemoryDecay);
            p.RepCommunityInfluence = F(o, "rep_community_influence", p.RepCommunityInfluence);

            p.ColorBody = C(o, "color_body", p.ColorBody);
            p.ColorTail = C(o, "color_tail", p.ColorTail);
            p.ColorTailRoot = C(o, "color_tail_root", p.ColorTailRoot);
            p.ColorLegs = C(o, "color_legs", p.ColorLegs);
            p.ColorPaws = C(o, "color_paws", p.ColorPaws);
            p.ColorLegLower = C(o, "color_leg_lower", p.ColorLegLower);
            p.ColorEye = C(o, "color_eye", p.ColorEye);
            p.EyeInkTintFloor = F(o, "eye_ink_tint_floor", p.EyeInkTintFloor);
            p.EyeHoleFloor = C(o, "eye_hole_floor", p.EyeHoleFloor);
            p.ColorHead = C(o, "color_head", p.ColorHead);

            p.SandboxGlowEnabled = B(o, "sandbox_glow_enabled", p.SandboxGlowEnabled);
            p.SandboxGlowColor = C(o, "sandbox_glow_color", p.SandboxGlowColor);
            p.SandboxHideArtCopy = B(o, "sandbox_hide_art_copy", p.SandboxHideArtCopy);
            p.IconColor = C(o, "icon_color", p.IconColor);
            p.SandboxGlowAlpha = F(o, "sandbox_glow_alpha", p.SandboxGlowAlpha);

            p.PoseLogTicks = I(o, "pose_log_ticks", p.PoseLogTicks);
            p.FeetLogTicks = I(o, "feet_log_ticks", p.FeetLogTicks);
            p.BodyCircles = I(o, "body_circles", p.BodyCircles);
            p.BodyMeshRadFac = F(o, "body_mesh_rad_fac", p.BodyMeshRadFac);
            p.BodyMeshPoints = I(o, "body_mesh_points", p.BodyMeshPoints);
            p.BodyProfileNeck = F(o, "body_profile_neck", p.BodyProfileNeck);
            p.BodyProfileChest = F(o, "body_profile_chest", p.BodyProfileChest);
            p.BodyProfileWaist = F(o, "body_profile_waist", p.BodyProfileWaist);
            p.BodyProfileHaunch = F(o, "body_profile_haunch", p.BodyProfileHaunch);
            p.BodyFrontCap = F(o, "body_front_cap", p.BodyFrontCap);
            p.BodyMeshOverlap = F(o, "body_mesh_overlap", p.BodyMeshOverlap);
            p.BodyRideHeight = F(o, "body_ride_height", p.BodyRideHeight);
            p.BodyRideCorridorFac = F(o, "body_ride_corridor_fac", p.BodyRideCorridorFac);
            p.BodyRideCorridorLerp = F(o, "body_ride_corridor_lerp", p.BodyRideCorridorLerp);
            p.BodyRideDeathFac = F(o, "body_ride_death_fac", p.BodyRideDeathFac);
            p.BodyRideDeathTicks = I(o, "body_ride_death_ticks", p.BodyRideDeathTicks);
            p.BodyRideStunCollapse = B(o, "body_ride_stun_collapse", p.BodyRideStunCollapse);
            p.BodyRideStunCollapseFac = F(o, "body_ride_stun_collapse_fac", p.BodyRideStunCollapseFac);
            p.DrawSmoothing = F(o, "draw_smoothing", p.DrawSmoothing);
            p.HeadSize = F(o, "head_size", p.HeadSize);
            p.HeadSprite = B(o, "head_sprite", p.HeadSprite);
            p.HeadSpriteElement = S(o, "head_sprite_element", p.HeadSpriteElement);
            p.HeadEyeElement = S(o, "head_eye_element", p.HeadEyeElement);
            p.HeadSpriteMask = B(o, "head_sprite_mask", p.HeadSpriteMask);
            p.HeadSpriteScale = F(o, "head_sprite_scale", p.HeadSpriteScale);
            p.HeadSpriteAnchorX = F(o, "head_sprite_anchor_x", p.HeadSpriteAnchorX);
            p.HeadSpriteAnchorY = F(o, "head_sprite_anchor_y", p.HeadSpriteAnchorY);
            p.HeadDrop = F(o, "head_drop", p.HeadDrop);
            p.HeadNeckSlack = F(o, "head_neck_slack", p.HeadNeckSlack);
            p.HeadGravity = F(o, "head_gravity", p.HeadGravity);
            p.HeadLookFac = F(o, "head_look_fac", p.HeadLookFac);
            p.HeadPitchMax = F(o, "head_pitch_max", p.HeadPitchMax);
            p.HeadMuzzleDroop = F(o, "head_muzzle_droop", p.HeadMuzzleDroop);
            p.HeadMuzzleLength = F(o, "head_muzzle_length", p.HeadMuzzleLength);
            p.HeadMuzzleWidth = F(o, "head_muzzle_width", p.HeadMuzzleWidth);
            p.HeadMuzzleTip = F(o, "head_muzzle_tip", p.HeadMuzzleTip);
            p.HeadForeshorten = F(o, "head_foreshorten", p.HeadForeshorten);
            p.EyeLength = F(o, "eye_length", p.EyeLength);
            p.EyeThickness = F(o, "eye_thickness", p.EyeThickness);
            p.EyeAlong = F(o, "eye_along", p.EyeAlong);
            p.EyeUp = F(o, "eye_up", p.EyeUp);
            p.NeckStiffness = F(o, "neck_stiffness", p.NeckStiffness);
            p.LimbLengthFac = F(o, "limb_length_fac", p.LimbLengthFac);
            p.LimbSize = F(o, "limb_size", p.LimbSize);
            p.LimbThickness = F(o, "limb_thickness", p.LimbThickness);
            p.LimbKneeBend = F(o, "limb_knee_bend", p.LimbKneeBend);
            p.LimbUpperFac = F(o, "limb_upper_fac", p.LimbUpperFac);
            p.LimbPawLength = F(o, "limb_paw_length", p.LimbPawLength);
            p.LimbPawWidth = F(o, "limb_paw_width", p.LimbPawWidth);
            p.LimbBendSmooth = F(o, "limb_bend_smooth", p.LimbBendSmooth);
            p.LimbBendFlipDeadband = F(o, "limb_bend_flip_deadband", p.LimbBendFlipDeadband);
            p.LegSprites = B(o, "leg_sprites", p.LegSprites);
            p.LegSpriteFront = S(o, "leg_sprite_front", p.LegSpriteFront);
            p.LegSpriteFrontFar = S(o, "leg_sprite_front_far", p.LegSpriteFrontFar);
            p.LegSpriteHind = S(o, "leg_sprite_hind", p.LegSpriteHind);
            p.LegSpriteHindFar = S(o, "leg_sprite_hind_far", p.LegSpriteHindFar);
            p.LegSpriteMask = B(o, "leg_sprite_mask", p.LegSpriteMask);
            p.TailSpriteElement = S(o, "tail_sprite_element", p.TailSpriteElement);
            p.BodySprite = B(o, "body_sprite", p.BodySprite);
            p.BodySpriteElement = S(o, "body_sprite_element", p.BodySpriteElement);
            p.BodySpriteMask = B(o, "body_sprite_mask", p.BodySpriteMask);
            p.BodySpriteScaleFac = F(o, "body_sprite_scale_fac", p.BodySpriteScaleFac);
            p.BodySpriteStretch = F(o, "body_sprite_stretch", p.BodySpriteStretch);
            p.LegSpriteWidthFac = F(o, "leg_sprite_width_fac", p.LegSpriteWidthFac);
            p.PartsRig = B(o, "parts_rig", p.PartsRig);
            p.PartsPrefix = S(o, "parts_prefix", p.PartsPrefix);
            p.PartsMask = B(o, "parts_mask", p.PartsMask);
            p.TobyDraw = I(o, "toby_draw", p.TobyDraw);
            p.TobyFrameTicks = I(o, "toby_frame_ticks", p.TobyFrameTicks);
            p.TobyFrameScale = F(o, "toby_frame_scale", p.TobyFrameScale);
            p.TobyFrameFeetPx = F(o, "toby_frame_feet_px", p.TobyFrameFeetPx);
            p.PartsScaleFac = F(o, "parts_scale_fac", p.PartsScaleFac);
            p.PartsRefSpine = F(o, "parts_ref_spine", p.PartsRefSpine);
            p.PartsBodyReach = F(o, "parts_body_reach", p.PartsBodyReach);
            p.PartsBodyStretch = F(o, "parts_body_stretch", p.PartsBodyStretch);
            p.PartsBodyMesh = B(o, "parts_body_mesh", p.PartsBodyMesh);
            p.PartsBodyQuads = I(o, "parts_body_quads", p.PartsBodyQuads);
            p.PartsChestQuads = I(o, "parts_chest_quads", p.PartsChestQuads);
            p.PartsBodyBendFac = F(o, "parts_body_bend_fac", p.PartsBodyBendFac);
            p.PartsBodyBendMax = F(o, "parts_body_bend_max", p.PartsBodyBendMax);
            p.PartsBodyLag = F(o, "parts_body_lag", p.PartsBodyLag);
            p.PartsBodyLagFac = F(o, "parts_body_lag_fac", p.PartsBodyLagFac);
            p.PartsBodyFlexSmooth = F(o, "parts_body_flex_smooth", p.PartsBodyFlexSmooth);
            p.PartsBodyFlexWave = F(o, "parts_body_flex_wave", p.PartsBodyFlexWave);
            p.PartsBodyFlexWavePhase = F(o, "parts_body_flex_wave_phase", p.PartsBodyFlexWavePhase);
            p.PartsBodyFlexAccel = F(o, "parts_body_flex_accel", p.PartsBodyFlexAccel);
            p.PartsBodyFlexLand = F(o, "parts_body_flex_land", p.PartsBodyFlexLand);
            p.PartsBodyFlexGather = F(o, "parts_body_flex_gather", p.PartsBodyFlexGather);
            p.PartsBodyFlexFlight = F(o, "parts_body_flex_flight", p.PartsBodyFlexFlight);
            p.PartsBodyFlexTurn = F(o, "parts_body_flex_turn", p.PartsBodyFlexTurn);
            p.PartsBodySquashSpeed = F(o, "parts_body_squash_speed", p.PartsBodySquashSpeed);
            p.PartsBodySquashAccel = F(o, "parts_body_squash_accel", p.PartsBodySquashAccel);
            p.PartsBodySquashWave = F(o, "parts_body_squash_wave", p.PartsBodySquashWave);
            p.PartsBodySquashTurn = F(o, "parts_body_squash_turn", p.PartsBodySquashTurn);
            p.PartsBodySquashAmp = F(o, "parts_body_squash_amp", p.PartsBodySquashAmp);
            p.PartsBodySquashLenGain = F(o, "parts_body_squash_len_gain", p.PartsBodySquashLenGain);
            p.PartsBodySquashGather = F(o, "parts_body_squash_gather", p.PartsBodySquashGather);
            p.PartsBodySquashFlight = F(o, "parts_body_squash_flight", p.PartsBodySquashFlight);
            p.PartsBodySquashLand = F(o, "parts_body_squash_land", p.PartsBodySquashLand);
            p.PartsBodySquashMax = F(o, "parts_body_squash_max", p.PartsBodySquashMax);
            p.PartsBodyFlexMaxDeg = F(o, "parts_body_flex_max_deg", p.PartsBodyFlexMaxDeg);
            p.PartsBodyWidthMax = F(o, "parts_body_width_max", p.PartsBodyWidthMax);
            p.PartsLegRootFollowBend = B(o, "parts_leg_root_follow_bend", p.PartsLegRootFollowBend);
            p.PartsBodyLandSpeed = F(o, "parts_body_land_speed", p.PartsBodyLandSpeed);
            p.PartsBodyLandDecay = F(o, "parts_body_land_decay", p.PartsBodyLandDecay);
            p.PartsChestAlong = F(o, "parts_chest_along", p.PartsChestAlong);
            p.PartsChestDrop = F(o, "parts_chest_drop", p.PartsChestDrop);
            p.CosmeticAnchorDrawnBody = B(o, "cosmetic_anchor_drawn_body", p.CosmeticAnchorDrawnBody);
            p.PartsTailRootOverlap = F(o, "parts_tail_root_overlap", p.PartsTailRootOverlap);
            p.PartsNeckFac = F(o, "parts_neck_fac", p.PartsNeckFac);
            p.PartsLegSlack = F(o, "parts_leg_slack", p.PartsLegSlack);
            p.PartsLegBendFront = F(o, "parts_leg_bend_front", p.PartsLegBendFront);
            p.PartsLegBendHind = F(o, "parts_leg_bend_hind", p.PartsLegBendHind);
            p.PartsLegThicknessFac = F(o, "parts_leg_thickness_fac", p.PartsLegThicknessFac);
            p.PartsLegMetaFacFront = F(o, "parts_leg_meta_fac_front", p.PartsLegMetaFacFront);
            p.PartsLegMetaFacHind = F(o, "parts_leg_meta_fac_hind", p.PartsLegMetaFacHind);
            p.PartsLegMetaRestDegFront = F(o, "parts_leg_meta_rest_deg_front", p.PartsLegMetaRestDegFront);
            p.PartsLegMetaRestDegHind = F(o, "parts_leg_meta_rest_deg_hind", p.PartsLegMetaRestDegHind);
            p.PartsLegMetaFoldDeg = F(o, "parts_leg_meta_fold_deg", p.PartsLegMetaFoldDeg);
            p.PartsLegPhaseLead = F(o, "parts_leg_phase_lead", p.PartsLegPhaseLead);
            p.PartsLegFoldPeak = F(o, "parts_leg_fold_peak", p.PartsLegFoldPeak);
            p.PartsLegTuckFac = F(o, "parts_leg_tuck_fac", p.PartsLegTuckFac);
            p.PartsLegKneeFoldFac = F(o, "parts_leg_knee_fold_fac", p.PartsLegKneeFoldFac);
            p.PartsLegProxLagFac = F(o, "parts_leg_prox_lag_fac", p.PartsLegProxLagFac);
            p.PartsPawAnchorFac = F(o, "parts_paw_anchor_fac", p.PartsPawAnchorFac);
            p.PartsKneeSmooth = F(o, "parts_knee_smooth", p.PartsKneeSmooth);
            p.PartsKneeMaxStep = F(o, "parts_knee_max_step", p.PartsKneeMaxStep);
            p.PartsKneeStepSpeedFac = F(o, "parts_knee_step_speed_fac", p.PartsKneeStepSpeedFac);
            p.PartsKneeReseedDist = F(o, "parts_knee_reseed_dist", p.PartsKneeReseedDist);
            p.PartsLegSingleSprite = B(o, "parts_leg_single_sprite", p.PartsLegSingleSprite);
            p.PartsLegForeOffset = F(o, "parts_leg_fore_offset", p.PartsLegForeOffset);
            p.PartsHaunchLean = F(o, "parts_haunch_lean", p.PartsHaunchLean);
            p.PartsHaunchInChain = B(o, "parts_haunch_in_chain", p.PartsHaunchInChain);
            p.PartsHeadTqGate = F(o, "parts_head_tq_gate", p.PartsHeadTqGate);
            p.PartsHeadAwayGate = F(o, "parts_head_away_gate", p.PartsHeadAwayGate);
            p.PartsHeadTqGateEnter = F(o, "parts_head_tq_gate_enter", p.PartsHeadTqGateEnter);
            p.PartsHeadTqGateExit = F(o, "parts_head_tq_gate_exit", p.PartsHeadTqGateExit);
            p.PartsHeadAwayGateEnter = F(o, "parts_head_away_gate_enter", p.PartsHeadAwayGateEnter);
            p.PartsHeadAwayGateExit = F(o, "parts_head_away_gate_exit", p.PartsHeadAwayGateExit);
            p.PartsHeadFrameDwellTicks = I(o, "parts_head_frame_dwell_ticks", p.PartsHeadFrameDwellTicks);
            p.PartsWaveAmp = F(o, "parts_wave_amp", p.PartsWaveAmp);
            p.PartsWaveLen = F(o, "parts_wave_len", p.PartsWaveLen);
            p.PartsWaveSpeedGate = F(o, "parts_wave_speed_gate", p.PartsWaveSpeedGate);
            p.PartsWaveSpeedFull = F(o, "parts_wave_speed_full", p.PartsWaveSpeedFull);
            p.PartsWaveGroundFade = F(o, "parts_wave_ground_fade", p.PartsWaveGroundFade);
            p.PartsLegMaxExtend = F(o, "parts_leg_max_extend", p.PartsLegMaxExtend);
            p.PartsTurnStep = B(o, "parts_turn_step", p.PartsTurnStep);
            p.PartsTurnStepFrontAt = F(o, "parts_turn_step_front_at", p.PartsTurnStepFrontAt);
            p.PartsTurnStepHindAt = F(o, "parts_turn_step_hind_at", p.PartsTurnStepHindAt);
            p.PartsTurnStepStagger = F(o, "parts_turn_step_stagger", p.PartsTurnStepStagger);
            p.PartsTurnLeanDeg = F(o, "parts_turn_lean_deg", p.PartsTurnLeanDeg);
            p.IdleBreathAmp = F(o, "idle_breath_amp", p.IdleBreathAmp);
            p.IdleBreathTicks = F(o, "idle_breath_ticks", p.IdleBreathTicks);
            p.IdleBreathBob = F(o, "idle_breath_bob", p.IdleBreathBob);
            p.IdleGlanceChance = F(o, "idle_glance_chance", p.IdleGlanceChance);
            p.IdleGlanceRange = F(o, "idle_glance_range", p.IdleGlanceRange);
            p.IdleGlanceTicks = I(o, "idle_glance_ticks", p.IdleGlanceTicks);
            p.IdleTailSwayAmp = F(o, "idle_tail_sway_amp", p.IdleTailSwayAmp);
            p.IdleTailSwayTicks = F(o, "idle_tail_sway_ticks", p.IdleTailSwayTicks);
            p.TailStreamFac = F(o, "tail_stream_fac", p.TailStreamFac);
            p.BodyLeanAccel = F(o, "body_lean_accel", p.BodyLeanAccel);
            p.BodyLeanMax = F(o, "body_lean_max", p.BodyLeanMax);
            p.HeadRestPitchDeg = F(o, "head_rest_pitch_deg", p.HeadRestPitchDeg);
            p.HeadCarryPx = F(o, "head_carry_px", p.HeadCarryPx);
            p.HeadPitchRate = F(o, "head_pitch_rate", p.HeadPitchRate);
            p.HeadLookPitchFac = F(o, "head_look_pitch_fac", p.HeadLookPitchFac);
            p.HeadIdleEnabled = B(o, "head_idle_enabled", p.HeadIdleEnabled);
            p.HeadIdleSpeed = F(o, "head_idle_speed", p.HeadIdleSpeed);
            p.HeadSniffChance = F(o, "head_sniff_chance", p.HeadSniffChance);
            p.HeadSniffTicks = I(o, "head_sniff_ticks", p.HeadSniffTicks);
            p.HeadSniffPitchDeg = F(o, "head_sniff_pitch_deg", p.HeadSniffPitchDeg);
            p.HeadScanChance = F(o, "head_scan_chance", p.HeadScanChance);
            p.HeadScanTicks = I(o, "head_scan_ticks", p.HeadScanTicks);
            p.HeadScanPitchDeg = F(o, "head_scan_pitch_deg", p.HeadScanPitchDeg);
            p.HeadScanYaw = F(o, "head_scan_yaw", p.HeadScanYaw);
            p.HeadScanSweepTicks = F(o, "head_scan_sweep_ticks", p.HeadScanSweepTicks);
            p.HeadLookCooldownTicks = I(o, "head_look_cooldown_ticks", p.HeadLookCooldownTicks);
            p.HeadYawRate = F(o, "head_yaw_rate", p.HeadYawRate);
            p.HeadYawDepth = F(o, "head_yaw_depth", p.HeadYawDepth);
            p.HeadNoiseEnabled = B(o, "head_noise_enabled", p.HeadNoiseEnabled);
            p.HeadNoiseMaxAge = I(o, "head_noise_max_age", p.HeadNoiseMaxAge);
            p.HeadNoiseHoldTicks = I(o, "head_noise_hold_ticks", p.HeadNoiseHoldTicks);
            p.HeadBehindYaw = F(o, "head_behind_yaw", p.HeadBehindYaw);
            p.TurnHeadLead = F(o, "turn_head_lead", p.TurnHeadLead);
            p.TurnHeadLeadRate = F(o, "turn_head_lead_rate", p.TurnHeadLeadRate);
            p.TurnHeadLeadPitchDeg = F(o, "turn_head_lead_pitch_deg", p.TurnHeadLeadPitchDeg);
            p.TurnTailSwing = F(o, "turn_tail_swing", p.TurnTailSwing);
            p.StepLeadFac = F(o, "step_lead_fac", p.StepLeadFac);
            p.StepLeadSpeedFac = F(o, "step_lead_speed_fac", p.StepLeadSpeedFac);
            p.StepBehindTrigger = F(o, "step_behind_trigger", p.StepBehindTrigger);
            p.StepSwingSpeedFac = F(o, "step_swing_speed_fac", p.StepSwingSpeedFac);
            p.StepArcLift = F(o, "step_arc_lift", p.StepArcLift);
            p.StepArcAsym = F(o, "step_arc_asym", p.StepArcAsym);
            p.StepSwingFold = F(o, "step_swing_fold", p.StepSwingFold);
            p.PawCurlDeg = F(o, "paw_curl_deg", p.PawCurlDeg);
            p.PawCurlPivotAnkle = B(o, "paw_curl_pivot_ankle", p.PawCurlPivotAnkle);
            p.GaitPairSpeed = F(o, "gait_pair_speed", p.GaitPairSpeed);
            p.StepPlantCommitTicks = I(o, "step_plant_commit_ticks", p.StepPlantCommitTicks);
            p.StepMinGrip = I(o, "step_min_grip", p.StepMinGrip);
            p.StepSwingMaxTicks = I(o, "step_swing_max_ticks", p.StepSwingMaxTicks);
            p.StepPlantWorldLock = B(o, "step_plant_world_lock", p.StepPlantWorldLock);
            p.StepTargetClamp = B(o, "step_target_clamp", p.StepTargetClamp);
            p.StepTargetMaxAboveFac = F(o, "step_target_max_above_fac", p.StepTargetMaxAboveFac);
            p.StepReachClampFac = F(o, "step_reach_clamp_fac", p.StepReachClampFac);
            p.StepReachFloorFollowsFold = B(o, "step_reach_floor_follows_fold", p.StepReachFloorFollowsFold);
            p.StepPlantAhead = B(o, "step_plant_ahead", p.StepPlantAhead);
            p.StepPlantAheadMinSpeed = F(o, "step_plant_ahead_min_speed", p.StepPlantAheadMinSpeed);
            p.StepPlantAheadFullSpeed = F(o, "step_plant_ahead_full_speed", p.StepPlantAheadFullSpeed);
            p.StepPlantAheadTravelFac = F(o, "step_plant_ahead_travel_fac", p.StepPlantAheadTravelFac);
            p.StepTrueStance = B(o, "step_true_stance", p.StepTrueStance);
            p.StepEventLog = B(o, "step_event_log", p.StepEventLog);
            p.PartsGaitChainAll = B(o, "parts_gait_chain_all", p.PartsGaitChainAll);
            p.StepHindAxisFlip = B(o, "step_hind_axis_flip", p.StepHindAxisFlip);
            p.RunPitchAmp = F(o, "run_pitch_amp", p.RunPitchAmp);
            p.RunHeightAmp = F(o, "run_height_amp", p.RunHeightAmp);
            p.RunTailAmp = F(o, "run_tail_amp", p.RunTailAmp);
            p.PounceBowPitch = F(o, "pounce_bow_pitch", p.PounceBowPitch);
            p.PounceBowTailUp = F(o, "pounce_bow_tail_up", p.PounceBowTailUp);
            p.PouncePropelPitch = F(o, "pounce_propel_pitch", p.PouncePropelPitch);
            p.PartsLegRootFront = F(o, "parts_leg_root_front", p.PartsLegRootFront);
            p.PartsLegRootHind = F(o, "parts_leg_root_hind", p.PartsLegRootHind);
            p.GaitDiagonal = B(o, "gait_diagonal", p.GaitDiagonal);
            p.TailNoTuck = B(o, "tail_no_tuck", p.TailNoTuck);
            p.TailRearBias = F(o, "tail_rear_bias", p.TailRearBias);
            p.TailUpBias = F(o, "tail_up_bias", p.TailUpBias);
            p.TailFloorClear = F(o, "tail_floor_clear", p.TailFloorClear);
            p.TurnDepthFac = F(o, "turn_depth_fac", p.TurnDepthFac);
            p.LimbSpeed = F(o, "limb_speed", p.LimbSpeed);
            p.LimbQuickness = F(o, "limb_quickness", p.LimbQuickness);
            p.LimbGripDelay = I(o, "limb_grip_delay", p.LimbGripDelay);
            p.StepLength = F(o, "step_length", p.StepLength);
            p.LiftFeet = F(o, "lift_feet", p.LiftFeet);
            p.FeetDown = F(o, "feet_down", p.FeetDown);
            p.LegPairDisplacement = F(o, "leg_pair_displacement", p.LegPairDisplacement);
            p.SmoothenLegMovement = B(o, "smoothen_leg_movement", p.SmoothenLegMovement);
            p.WalkBob = F(o, "walk_bob", p.WalkBob);
            p.LimbAttachFront = F(o, "limb_attach_front", p.LimbAttachFront);
            p.LimbAttachRear = F(o, "limb_attach_rear", p.LimbAttachRear);
            p.LimbAttachDrop = F(o, "limb_attach_drop", p.LimbAttachDrop);
            p.TailSegments = I(o, "tail_segments", p.TailSegments);
            p.TailLengthFac = F(o, "tail_length_fac", p.TailLengthFac);
            p.TailThicknessFac = F(o, "tail_thickness_fac", p.TailThicknessFac);
            p.TailLengthVsBody = F(o, "tail_length_vs_body", p.TailLengthVsBody);
            p.TailHeightVsBody = F(o, "tail_height_vs_body", p.TailHeightVsBody);
            p.TailTipTaper = F(o, "tail_tip_taper", p.TailTipTaper);
            p.TailRootThickness = F(o, "tail_root_thickness", p.TailRootThickness);
            p.TailTipThickness = F(o, "tail_tip_thickness", p.TailTipThickness);
            p.TailSwellPos = F(o, "tail_swell_pos", p.TailSwellPos);
            p.TailRootDrop = F(o, "tail_root_drop", p.TailRootDrop);
            p.TailRootDroop = F(o, "tail_root_droop", p.TailRootDroop);
            p.TailGravityCurve = F(o, "tail_gravity_curve", p.TailGravityCurve);
            p.TailMeshOverlap = F(o, "tail_mesh_overlap", p.TailMeshOverlap);
            p.TailStiffness = F(o, "tail_stiffness", p.TailStiffness);
            p.TailStiffnessDecline = F(o, "tail_stiffness_decline", p.TailStiffnessDecline);
            p.TailGravity = F(o, "tail_gravity", p.TailGravity);
            p.StalkCrouchDrop = F(o, "stalk_crouch_drop", p.StalkCrouchDrop);
            p.StalkCrouchLimbFac = F(o, "stalk_crouch_limb_fac", p.StalkCrouchLimbFac);
            p.LandingPoseReach = F(o, "landing_pose_reach", p.LandingPoseReach);
            p.LandingPoseLevel = F(o, "landing_pose_level", p.LandingPoseLevel);
            p.ClimbsWalls = B(o, "climbs_walls", p.ClimbsWalls);
            p.ClimbLegacyAnyTileFooting = B(o, "climb_legacy_any_tile_footing", p.ClimbLegacyAnyTileFooting);
            p.RegainFootingFrames = I(o, "regain_footing_frames", p.RegainFootingFrames);
            p.NoGripReleaseFrames = I(o, "no_grip_release_frames", p.NoGripReleaseFrames);
            p.ClimbLogTicks = I(o, "climb_log_ticks", p.ClimbLogTicks);
            p.FloorLeverage = F(o, "floor_leverage", p.FloorLeverage);
            p.ClimbTileCenterFac = F(o, "climb_tile_center_fac", p.ClimbTileCenterFac);
            p.ClimbsPoles = B(o, "climbs_poles", p.ClimbsPoles);
            p.PoleHorizontalAllowed = B(o, "pole_horizontal_allowed", p.PoleHorizontalAllowed);
            p.PoleGrabMaxSpeed = F(o, "pole_grab_max_speed", p.PoleGrabMaxSpeed);
            p.PoleRegrabTicks = I(o, "pole_regrab_ticks", p.PoleRegrabTicks);
            p.PoleReleaseOnHit = B(o, "pole_release_on_hit", p.PoleReleaseOnHit);
            p.PoleReleaseOnGrab = B(o, "pole_release_on_grab", p.PoleReleaseOnGrab);
            p.PoleReleaseOnBlind = B(o, "pole_release_on_blind", p.PoleReleaseOnBlind);
            p.PoleCenterHeadFac = F(o, "pole_center_head_fac", p.PoleCenterHeadFac);
            p.PoleCenterMidFac = F(o, "pole_center_mid_fac", p.PoleCenterMidFac);
            p.PoleCenterRearFac = F(o, "pole_center_rear_fac", p.PoleCenterRearFac);
            p.PoleCenterReachPx = F(o, "pole_center_reach_px", p.PoleCenterReachPx);
            p.PoleRideFac = F(o, "pole_ride_fac", p.PoleRideFac);
            p.PoleRideLerp = F(o, "pole_ride_lerp", p.PoleRideLerp);
            p.PoleCyclePx = F(o, "pole_cycle_px", p.PoleCyclePx);
            p.PoleReachFrac = F(o, "pole_reach_frac", p.PoleReachFrac);
            p.PolePawPhaseOffset = F(o, "pole_paw_phase_offset", p.PolePawPhaseOffset);
            p.PoleHindPhaseOffset = F(o, "pole_hind_phase_offset", p.PoleHindPhaseOffset);
            p.PoleFrontAmpPx = F(o, "pole_front_amp_px", p.PoleFrontAmpPx);
            p.PoleFrontLeadPx = F(o, "pole_front_lead_px", p.PoleFrontLeadPx);
            p.PoleHindAmpPx = F(o, "pole_hind_amp_px", p.PoleHindAmpPx);
            p.PoleHindLeadPx = F(o, "pole_hind_lead_px", p.PoleHindLeadPx);
            p.PolePawSidePx = F(o, "pole_paw_side_px", p.PolePawSidePx);
            p.PoleLegBendFront = F(o, "pole_leg_bend_front", p.PoleLegBendFront);
            p.PoleLegBendHind = F(o, "pole_leg_bend_hind", p.PoleLegBendHind);
            p.PoleLegMetaDegFront = F(o, "pole_leg_meta_deg_front", p.PoleLegMetaDegFront);
            p.PoleLegMetaDegHind = F(o, "pole_leg_meta_deg_hind", p.PoleLegMetaDegHind);
            p.PolePawSpeedFac = F(o, "pole_paw_speed_fac", p.PolePawSpeedFac);
            p.PoleBackDown = B(o, "pole_back_down", p.PoleBackDown);
            p.PolePullPulseFac = F(o, "pole_pull_pulse_fac", p.PolePullPulseFac);
            p.PoleBodySwayPx = F(o, "pole_body_sway_px", p.PoleBodySwayPx);
            p.PoleBodySwayCycles = I(o, "pole_body_sway_cycles", p.PoleBodySwayCycles);
            p.PoleBodySwayLag = F(o, "pole_body_sway_lag", p.PoleBodySwayLag);
            p.PoleStrokeJitter = F(o, "pole_stroke_jitter", p.PoleStrokeJitter);
            p.PoleStrokeJitterPx = F(o, "pole_stroke_jitter_px", p.PoleStrokeJitterPx);
            p.PoleLogTicks = I(o, "pole_log_ticks", p.PoleLogTicks);
            p.PoleMeetDetectTicks = I(o, "pole_meet_detect_ticks", p.PoleMeetDetectTicks);
            p.PoleMeetYieldRule = S(o, "pole_meet_yield_rule", p.PoleMeetYieldRule);
            p.PoleMeetProgressPx = F(o, "pole_meet_progress_px", p.PoleMeetProgressPx);
            p.PoleMeetAheadPx = F(o, "pole_meet_ahead_px", p.PoleMeetAheadPx);
            p.PoleMeetLanePx = F(o, "pole_meet_lane_px", p.PoleMeetLanePx);
            p.PoleMeetEscalateTicks = I(o, "pole_meet_escalate_ticks", p.PoleMeetEscalateTicks);
            p.PoleMeetWaitTicks = I(o, "pole_meet_wait_ticks", p.PoleMeetWaitTicks);
            p.PoleMeetAsideTiles = I(o, "pole_meet_aside_tiles", p.PoleMeetAsideTiles);
            p.PoleMeetGiveUpTicks = I(o, "pole_meet_give_up_ticks", p.PoleMeetGiveUpTicks);
            p.PoleAscentHeadFirst = B(o, "pole_ascent_head_first", p.PoleAscentHeadFirst);
            p.PoleAlignOrder = B(o, "pole_align_order", p.PoleAlignOrder);
            p.PoleAlignFarMaxPx = F(o, "pole_align_far_max_px", p.PoleAlignFarMaxPx);
            p.PoleAlignSwingPx = F(o, "pole_align_swing_px", p.PoleAlignSwingPx);
            p.PoleFloorRegrabTicks = I(o, "pole_floor_regrab_ticks", p.PoleFloorRegrabTicks);
            p.PoleDestFilter = B(o, "pole_dest_filter", p.PoleDestFilter);
            p.WallOverhangAllowed = B(o, "wall_overhang_allowed", p.WallOverhangAllowed);
            p.WallWanderDest = B(o, "wall_wander_dest", p.WallWanderDest);
            p.WallGrabMaxSpeed = F(o, "wall_grab_max_speed", p.WallGrabMaxSpeed);
            p.WallRegrabTicks = I(o, "wall_regrab_ticks", p.WallRegrabTicks);
            p.WallCenterFac = F(o, "wall_center_fac", p.WallCenterFac);
            p.WallStandoffPx = F(o, "wall_standoff_px", p.WallStandoffPx);
            p.WallPawFacePx = F(o, "wall_paw_face_px", p.WallPawFacePx);
            p.WallPoseLerp = F(o, "wall_pose_lerp", p.WallPoseLerp);
            p.WallLaunchAllowed = B(o, "wall_launch_allowed", p.WallLaunchAllowed);
            p.WallLogTicks = I(o, "wall_log_ticks", p.WallLogTicks);
            p.VoiceHunt = S(o, "voice_hunt", p.VoiceHunt);
            p.VoiceAlert = S(o, "voice_alert", p.VoiceAlert);
            p.VoiceAttack = S(o, "voice_attack", p.VoiceAttack);
            p.VoiceHurt = S(o, "voice_hurt", p.VoiceHurt);
            p.VoiceDeath = S(o, "voice_death", p.VoiceDeath);
            p.VoicePheromone = S(o, "voice_pheromone", p.VoicePheromone);
            p.PheromoneEnabled = B(o, "pheromone_enabled", p.PheromoneEnabled);
            p.PheromoneShare = S(o, "pheromone_share", p.PheromoneShare);
            p.PheromoneRangeTiles = F(o, "pheromone_range_tiles", p.PheromoneRangeTiles);
            p.PheromoneReceiverNeedsLos = B(o, "pheromone_receiver_needs_los", p.PheromoneReceiverNeedsLos);
            p.PheromoneReceiverResponse = S(o, "pheromone_receiver_response", p.PheromoneReceiverResponse);
            p.PheromoneCommMinTicks = I(o, "pheromone_comm_min_ticks", p.PheromoneCommMinTicks);
            p.PheromoneCommMaxTicks = I(o, "pheromone_comm_max_ticks", p.PheromoneCommMaxTicks);
            p.PheromoneFlickerFullTicks = I(o, "pheromone_flicker_full_ticks", p.PheromoneFlickerFullTicks);
            p.PheromoneFlickerRise = F(o, "pheromone_flicker_rise", p.PheromoneFlickerRise);
            p.PheromoneFlickerFall = F(o, "pheromone_flicker_fall", p.PheromoneFlickerFall);
            p.PheromoneLogTicks = I(o, "pheromone_log_ticks", p.PheromoneLogTicks);
            p.PheromoneStyle = I(o, "pheromone_style", p.PheromoneStyle);
            p.PheromoneWiggleDeg = F(o, "pheromone_wiggle_deg", p.PheromoneWiggleDeg);
            p.PheromoneWiggleHz = F(o, "pheromone_wiggle_hz", p.PheromoneWiggleHz);
            p.PheromoneWiggleLagDeg = F(o, "pheromone_wiggle_lag_deg", p.PheromoneWiggleLagDeg);
            p.PheromoneReleaseTicks = I(o, "pheromone_release_ticks", p.PheromoneReleaseTicks);
            p.PheromoneWiggleRiseTicks = I(o, "pheromone_wiggle_rise_ticks", p.PheromoneWiggleRiseTicks);
            p.PheromoneWiggleFallTicks = I(o, "pheromone_wiggle_fall_ticks", p.PheromoneWiggleFallTicks);
            p.PheromoneMistOpacity = F(o, "pheromone_mist_opacity", p.PheromoneMistOpacity);
            p.PheromoneMistHueDeg = F(o, "pheromone_mist_hue_deg", p.PheromoneMistHueDeg);
            p.PheromoneMistSaturation = F(o, "pheromone_mist_saturation", p.PheromoneMistSaturation);
            p.PheromoneMistLightness = F(o, "pheromone_mist_lightness", p.PheromoneMistLightness);
            p.PheromoneMistFog = F(o, "pheromone_mist_fog", p.PheromoneMistFog);
            p.PheromoneMistSize = F(o, "pheromone_mist_size", p.PheromoneMistSize);
            p.PheromoneMistCount = I(o, "pheromone_mist_count", p.PheromoneMistCount);
            p.PheromoneMistAim = F(o, "pheromone_mist_aim", p.PheromoneMistAim);
            p.PheromoneMistDrift = F(o, "pheromone_mist_drift", p.PheromoneMistDrift);
            p.PheromoneMistLifetimeTicks = I(o, "pheromone_mist_lifetime_ticks", p.PheromoneMistLifetimeTicks);
            p.PheromoneReceiverSniff = B(o, "pheromone_receiver_sniff", p.PheromoneReceiverSniff);
            p.PheromoneSniffReachPx = F(o, "pheromone_sniff_reach_px", p.PheromoneSniffReachPx);
            p.PheromoneSniffReachFac = F(o, "pheromone_sniff_reach_fac", p.PheromoneSniffReachFac);
            p.PheromoneSniffDelayTicks = I(o, "pheromone_sniff_delay_ticks", p.PheromoneSniffDelayTicks);
            p.EvasionEnabled = B(o, "evasion_enabled", p.EvasionEnabled);
            p.EvasionChance = F(o, "evasion_chance", p.EvasionChance);
            p.EvasionReactionMinTicks = I(o, "evasion_reaction_min_ticks", p.EvasionReactionMinTicks);
            p.EvasionReactionMaxTicks = I(o, "evasion_reaction_max_ticks", p.EvasionReactionMaxTicks);
            p.EvasionCooldownTicks = I(o, "evasion_cooldown_ticks", p.EvasionCooldownTicks);
            p.EvasionStaggerTicks = I(o, "evasion_stagger_ticks", p.EvasionStaggerTicks);
            p.VoiceVolume = F(o, "voice_volume", p.VoiceVolume);
            p.VoicePitch = F(o, "voice_pitch", p.VoicePitch);
            p.VoiceCooldownFrames = I(o, "voice_cooldown_frames", p.VoiceCooldownFrames);
            ReadCosmetics(o, p);

            p.PounceCrouchFrames = ClampI(o, "pounce_crouch_frames", p.PounceCrouchFrames,
                Math.Max(0, p.PounceCrouchFrames), "must_not_be_negative");
            p.PouncePropulsionFrames = ClampI(o, "pounce_propulsion_frames", p.PouncePropulsionFrames,
                Math.Max(1, p.PouncePropulsionFrames), "at_least_one_thrust_frame");
            p.EnterGatherFrames = ClampI(o, "enter_gather_frames", p.EnterGatherFrames,
                Math.Max(0, p.EnterGatherFrames), "must_not_be_negative");
            p.EnterMaxFrames = ClampI(o, "enter_max_frames", p.EnterMaxFrames,
                Math.Max(Math.Max(1, p.EnterGatherFrames + 1), p.EnterMaxFrames),
                "at_least_enter_gather_frames_plus_one");
            p.EnterCooldown = ClampI(o, "enter_cooldown", p.EnterCooldown,
                Math.Max(0, p.EnterCooldown), "must_not_be_negative");
            p.EnterPull = ClampF(o, "enter_pull", p.EnterPull,
                Mathf.Max(0f, p.EnterPull), "must_not_be_negative");
            p.EnterReachPx = ClampF(o, "enter_reach_px", p.EnterReachPx,
                Mathf.Max(0f, p.EnterReachPx), "must_not_be_negative");
            p.EnterGatherCompress = ClampF(o, "enter_gather_compress", p.EnterGatherCompress,
                Mathf.Clamp01(p.EnterGatherCompress), "range_0_to_1");
            p.EnterHeadLead = ClampF(o, "enter_head_lead", p.EnterHeadLead,
                Mathf.Max(0f, p.EnterHeadLead), "must_not_be_negative");
            p.EnterRearLag = ClampF(o, "enter_rear_lag", p.EnterRearLag,
                Mathf.Clamp01(p.EnterRearLag), "range_0_to_1");
            p.EnterShortcutScanTiles = ClampI(o, "enter_shortcut_scan_tiles", p.EnterShortcutScanTiles,
                Math.Max(1, p.EnterShortcutScanTiles), "at_least_one_tile");
            p.EnterShortcutNearTiles = ClampI(o, "enter_shortcut_near_tiles", p.EnterShortcutNearTiles,
                Math.Max(0, p.EnterShortcutNearTiles), "must_not_be_negative");
            p.PounceGatherFrames = ClampI(o, "pounce_gather_frames", p.PounceGatherFrames,
                Math.Max(0, p.PounceGatherFrames), "must_not_be_negative");
            p.PounceGatherFramesMin = ClampI(o, "pounce_gather_frames_min", p.PounceGatherFramesMin,
                Math.Min(Math.Max(0, p.PounceGatherFramesMin), Math.Max(0, p.PounceGatherFrames)),
                "between_zero_and_pounce_gather_frames");
            p.PounceLaunchMinFrac = ClampF(o, "pounce_launch_min_frac", p.PounceLaunchMinFrac,
                Mathf.Clamp(p.PounceLaunchMinFrac, 0.05f, 1f), "range_0.05_to_1");
            p.PounceLandFrames = ClampI(o, "pounce_land_frames", p.PounceLandFrames,
                Math.Max(0, p.PounceLandFrames), "must_not_be_negative");
            p.PounceBoostFrames = ClampI(o, "pounce_boost_frames", p.PounceBoostFrames,
                Math.Max(0, p.PounceBoostFrames), "must_not_be_negative");
            p.PounceFlightMaxFrames = ClampI(o, "pounce_flight_max_frames", p.PounceFlightMaxFrames,
                Math.Max(1, p.PounceFlightMaxFrames), "at_least_one_flight_frame");
            p.PounceLandDamp = ClampF(o, "pounce_land_damp", p.PounceLandDamp,
                Mathf.Clamp01(p.PounceLandDamp), "range_0_to_1");
            p.BodyTurnForeshorten = ClampF(o, "body_turn_foreshorten", p.BodyTurnForeshorten,
                Mathf.Clamp01(p.BodyTurnForeshorten), "range_0_to_1");
            p.DepthVerticalHoldBand = ClampF(o, "depth_vertical_hold_band", p.DepthVerticalHoldBand,
                Mathf.Clamp(p.DepthVerticalHoldBand, 0f, 0.49f), "range_0_to_0.49_zero_is_off");
            p.DrawUpSignDeadband = ClampF(o, "draw_up_sign_deadband", p.DrawUpSignDeadband,
                Mathf.Clamp(p.DrawUpSignDeadband, 0f, 0.5f), "range_0_to_0.5_zero_is_off");
            p.TurnRate = ClampF(o, "turn_rate", p.TurnRate,
                Mathf.Clamp(p.TurnRate, 0.001f, 1f), "range_0.001_to_1");
            p.TurnFrames = ClampI(o, "turn_frames", p.TurnFrames,
                Math.Max(0, p.TurnFrames), "must_not_be_negative");
            p.TurnMaxStepPx = ClampF(o, "turn_max_step_px", p.TurnMaxStepPx,
                Mathf.Clamp(p.TurnMaxStepPx, 0.5f, 32f), "range_0.5_to_32");
            p.TurnBrake = ClampF(o, "turn_brake", p.TurnBrake,
                Mathf.Clamp01(p.TurnBrake), "range_0_to_1");
            p.TurnAbortAirTicks = ClampI(o, "turn_abort_air_ticks", p.TurnAbortAirTicks,
                Math.Max(1, p.TurnAbortAirTicks), "at_least_one_tick");
            p.TurnLogTicks = ClampI(o, "turn_log_ticks", p.TurnLogTicks,
                Math.Max(0, p.TurnLogTicks), "must_not_be_negative");
            p.TurnSpineRelax = ClampF(o, "turn_spine_relax", p.TurnSpineRelax,
                Mathf.Clamp01(p.TurnSpineRelax), "range_0_to_1");
            p.TurnSpineRecoverTicks = ClampI(o, "turn_spine_recover_ticks", p.TurnSpineRecoverTicks,
                Math.Max(0, p.TurnSpineRecoverTicks), "must_not_be_negative");
            p.TurnVerticalHold = ClampF(o, "turn_vertical_hold", p.TurnVerticalHold,
                Mathf.Clamp01(p.TurnVerticalHold), "range_0_to_1");
            p.TurnHoldMaxTicks = ClampI(o, "turn_hold_max_ticks", p.TurnHoldMaxTicks,
                Math.Max(1, p.TurnHoldMaxTicks), "at_least_one_tick");
            p.DriveTrailBackstepFac = ClampF(o, "drive_trail_backstep_fac", p.DriveTrailBackstepFac,
                Mathf.Clamp01(p.DriveTrailBackstepFac), "range_0_to_1");
            p.PathLatchMaxRejects = ClampI(o, "path_latch_max_rejects", p.PathLatchMaxRejects,
                Math.Max(0, p.PathLatchMaxRejects), "must_not_be_negative");
            p.TurnPivotSpeed = ClampF(o, "turn_pivot_speed", p.TurnPivotSpeed,
                Mathf.Clamp(p.TurnPivotSpeed, 0.05f, 16f), "range_0.05_to_16_px_per_tick");
            p.TurnPivotFriction = ClampF(o, "turn_pivot_friction", p.TurnPivotFriction,
                Mathf.Clamp01(p.TurnPivotFriction), "range_0_to_1");
            p.TurnPivotLead = ClampF(o, "turn_pivot_lead", p.TurnPivotLead,
                Mathf.Clamp(p.TurnPivotLead, 0.01f, 2f), "range_0.01_to_2_body_lengths");
            p.TurnPivotMidFac = ClampF(o, "turn_pivot_mid_fac", p.TurnPivotMidFac,
                Mathf.Clamp(p.TurnPivotMidFac, 0f, 4f), "range_0_to_4");
            p.TurnPivotBrake = ClampF(o, "turn_pivot_brake", p.TurnPivotBrake,
                Mathf.Clamp01(p.TurnPivotBrake), "range_0_to_1");
            p.TurnPivotJitter = ClampF(o, "turn_pivot_jitter", p.TurnPivotJitter,
                Mathf.Clamp(p.TurnPivotJitter, 0f, 2f), "range_0_to_2");
            p.TurnPivotStretchPx = ClampF(o, "turn_pivot_stretch_px", p.TurnPivotStretchPx,
                Mathf.Max(0f, p.TurnPivotStretchPx), "must_not_be_negative");
            p.TurnFocusHoldDot = ClampF(o, "turn_focus_hold_dot", p.TurnFocusHoldDot,
                Mathf.Clamp(p.TurnFocusHoldDot, -1f, 1f), "range_-1_to_1");
            p.TurnMinIntervalTicks = ClampI(o, "turn_min_interval_ticks", p.TurnMinIntervalTicks,
                Math.Max(0, p.TurnMinIntervalTicks), "must_not_be_negative");
            p.TurnPivotCrossFac = ClampF(o, "turn_pivot_cross_fac", p.TurnPivotCrossFac,
                Mathf.Clamp(p.TurnPivotCrossFac, 0f, 0.9f), "range_0_to_0.9");
            p.TurnPivotFinish = ClampF(o, "turn_pivot_finish", p.TurnPivotFinish,
                Mathf.Clamp(p.TurnPivotFinish, 0.001f, 1f), "range_0.001_to_1");
            p.TurnPivotReleaseDot = ClampF(o, "turn_pivot_release_dot", p.TurnPivotReleaseDot,
                Mathf.Clamp(p.TurnPivotReleaseDot, -1f, 1f), "range_-1_to_1");
            p.TurnPivotGoalDot = ClampF(o, "turn_pivot_goal_dot", p.TurnPivotGoalDot,
                Mathf.Clamp(p.TurnPivotGoalDot, -1f, 1f), "range_-1_to_1");
            p.TurnPivotReleaseSpanFac = ClampF(o, "turn_pivot_release_span_fac", p.TurnPivotReleaseSpanFac,
                Mathf.Clamp01(p.TurnPivotReleaseSpanFac), "range_0_to_1");
            p.TurnPivotSwingPx = ClampF(o, "turn_pivot_swing_px", p.TurnPivotSwingPx,
                Mathf.Clamp(p.TurnPivotSwingPx, 0f, 32f), "range_0_to_32");
            p.TurnPivotMaxTicks = ClampI(o, "turn_pivot_max_ticks", p.TurnPivotMaxTicks,
                Math.Max(1, p.TurnPivotMaxTicks), "at_least_one_tick");
            if (p.TurnMode != "none" && p.TurnMode != "frontpivot")
            {
                p.TurnMode = "frontpivot";
            }
            {
                string bad;
                int mask = FoxLandingSurface.MaskOf(p.LedgePounceLipSurfaces, out bad);
                if (bad != null)
                {
                    p.LedgePounceLipSurfaces = FoxLandingSurface.Default;
                    mask = FoxLandingSurface.MaskOf(p.LedgePounceLipSurfaces, out bad);
                }
                p.LedgePounceLipSurfaceMask = mask;
            }
            p.LedgePounceGatherSlackFac = ClampF(o, "ledge_pounce_gather_slack_fac",
                p.LedgePounceGatherSlackFac, Mathf.Clamp01(p.LedgePounceGatherSlackFac), "must_be_0_to_1");

            p.PathLatchTicks = ClampI(o, "path_latch_ticks", p.PathLatchTicks,
                Math.Max(0, p.PathLatchTicks), "must_not_be_negative");
            p.PathTiebreakCommitTicks = ClampI(o, "path_tiebreak_commit_ticks", p.PathTiebreakCommitTicks,
                Mathf.Clamp(p.PathTiebreakCommitTicks, 0, 60), "range_0_to_60_ticks_zero_is_off");
            p.LimbSweepMinGrip = ClampI(o, "limb_sweep_min_grip", p.LimbSweepMinGrip,
                Math.Max(0, p.LimbSweepMinGrip), "must_not_be_negative");
            p.PounceMaxFrames = ClampI(o, "pounce_max_frames", p.PounceMaxFrames,
                Math.Max(1, p.PounceMaxFrames), "at_least_one_propulsion_frame");
            p.PathStepsPerFrame = ClampI(o, "path_steps_per_frame", p.PathStepsPerFrame,
                Mathf.Clamp(p.PathStepsPerFrame, 1, 100), "range_1_to_100");
            p.BodyMass = ClampF(o, "body_mass", p.BodyMass, Mathf.Max(0.1f, p.BodyMass), "min_0.1");
            p.BodySizeFac = ClampF(o, "body_size_fac", p.BodySizeFac, Mathf.Clamp(p.BodySizeFac, 0.05f, 10f),
                "range_0.05_to_10_it_becomes_a_per_draw_loop_bound");
            p.SpineElasticity = ClampF(o, "spine_elasticity", p.SpineElasticity,
                Mathf.Clamp01(p.SpineElasticity), "range_0_to_1_physics_stability");
            p.SpineWeightSymmetry = ClampF(o, "spine_weight_symmetry", p.SpineWeightSymmetry,
                Mathf.Clamp01(p.SpineWeightSymmetry), "range_0_to_1_physics_stability");
            p.LoadedPerformanceCost = ClampF(o, "loaded_performance_cost", p.LoadedPerformanceCost,
                Mathf.Max(10f, p.LoadedPerformanceCost), "min_10_fisobs_documented_minimum");
            p.Grasps = ClampI(o, "grasps", p.Grasps, Mathf.Clamp(p.Grasps, 0, 2), "range_0_to_2");
            p.IdleSwitchMaxFrames = ClampI(o, "idle_switch_max_frames", p.IdleSwitchMaxFrames,
                Math.Max(p.IdleSwitchMinFrames + 1, p.IdleSwitchMaxFrames),
                "must_exceed_idle_switch_min_frames");
            p.SenseOmniIntervalTicks = ClampI(o, "sense_omni_interval_ticks", p.SenseOmniIntervalTicks,
                Math.Max(1, p.SenseOmniIntervalTicks), "at_least_one_tick_between_sweeps");
            p.ScentRadiusTiles = ClampF(o, "scent_radius_tiles", p.ScentRadiusTiles,
                Mathf.Max(0f, p.ScentRadiusTiles), "min_0_tiles");
            p.ScentMaxScore = ClampF(o, "scent_max_score", p.ScentMaxScore,
                Mathf.Clamp01(p.ScentMaxScore), "range_0_to_1_estimated_chance_of_finding");
            p.ScentWallRadiusFac = ClampF(o, "scent_wall_radius_fac", p.ScentWallRadiusFac,
                Mathf.Clamp01(p.ScentWallRadiusFac), "range_0_to_1_a_wall_never_amplifies");
            p.ScentWallScoreFac = ClampF(o, "scent_wall_score_fac", p.ScentWallScoreFac,
                Mathf.Clamp01(p.ScentWallScoreFac), "range_0_to_1_a_wall_never_amplifies");
            p.ScentIntervalTicks = ClampI(o, "scent_interval_ticks", p.ScentIntervalTicks,
                Math.Max(1, p.ScentIntervalTicks), "at_least_one_tick_between_sweeps");
            p.ScentPassiveFac = ClampF(o, "scent_passive_fac", p.ScentPassiveFac,
                Mathf.Clamp01(p.ScentPassiveFac), "range_0_to_1_floor_never_above_cap");
            p.ScentAppetiteScoreFac = ClampF(o, "scent_appetite_score_fac", p.ScentAppetiteScoreFac,
                Mathf.Clamp01(p.ScentAppetiteScoreFac), "range_0_to_1_one_is_neutral");
            p.ScentAppetiteRangeFac = ClampF(o, "scent_appetite_range_fac", p.ScentAppetiteRangeFac,
                Mathf.Clamp01(p.ScentAppetiteRangeFac), "range_0_to_1_one_is_neutral");
            p.ScentSniffPitchDeg = ClampF(o, "scent_sniff_pitch_deg", p.ScentSniffPitchDeg,
                Mathf.Max(0f, p.ScentSniffPitchDeg), "min_0_the_scent_sniff_is_nose_up");
            p.ScentSniffTicks = ClampI(o, "scent_sniff_ticks", p.ScentSniffTicks,
                Math.Max(1, p.ScentSniffTicks), "at_least_one_tick_or_no_sniff_is_visible");
            p.ScentSniffCooldownTicks = ClampI(o, "scent_sniff_cooldown_ticks", p.ScentSniffCooldownTicks,
                Math.Max(0, p.ScentSniffCooldownTicks), "min_0_ticks");
            p.RoamCandidates = ClampI(o, "roam_candidates", p.RoamCandidates,
                Mathf.Clamp(p.RoamCandidates, 1, 64), "range_1_to_64");
            p.EatFrames = ClampI(o, "eat_frames", p.EatFrames,
                Math.Max(1, p.EatFrames), "at_least_one_feeding_frame");

            p.BodyCircles = ClampI(o, "body_circles", p.BodyCircles,
                Mathf.Clamp(p.BodyCircles, 1, 64), "range_1_to_64");
            p.TailSegments = ClampI(o, "tail_segments", p.TailSegments,
                Mathf.Clamp(p.TailSegments, 1, 32), "range_1_to_32");
            p.LimbGripDelay = ClampI(o, "limb_grip_delay", p.LimbGripDelay,
                Math.Max(0, p.LimbGripDelay), "must_not_be_negative");
            p.VoiceCooldownFrames = ClampI(o, "voice_cooldown_frames", p.VoiceCooldownFrames,
                Math.Max(0, p.VoiceCooldownFrames), "must_not_be_negative");
            p.RegainFootingFrames = ClampI(o, "regain_footing_frames", p.RegainFootingFrames,
                Math.Max(1, p.RegainFootingFrames), "at_least_one_tick");
            p.PoleGrabMaxSpeed = ClampF(o, "pole_grab_max_speed", p.PoleGrabMaxSpeed,
                Mathf.Max(0f, p.PoleGrabMaxSpeed), "must_not_be_negative");
            p.PoleRegrabTicks = ClampI(o, "pole_regrab_ticks", p.PoleRegrabTicks,
                Math.Max(0, p.PoleRegrabTicks), "must_not_be_negative");
            p.PoleCenterHeadFac = ClampF(o, "pole_center_head_fac", p.PoleCenterHeadFac,
                Mathf.Clamp01(p.PoleCenterHeadFac), "range_0_to_1_or_the_ease_pushes_away_or_overshoots");
            p.PoleCenterMidFac = ClampF(o, "pole_center_mid_fac", p.PoleCenterMidFac,
                Mathf.Clamp01(p.PoleCenterMidFac), "range_0_to_1_or_the_ease_pushes_away_or_overshoots");
            p.PoleCenterRearFac = ClampF(o, "pole_center_rear_fac", p.PoleCenterRearFac,
                Mathf.Clamp01(p.PoleCenterRearFac), "range_0_to_1_or_the_ease_pushes_away_or_overshoots");
            p.PoleCenterReachPx = ClampF(o, "pole_center_reach_px", p.PoleCenterReachPx,
                Mathf.Max(0f, p.PoleCenterReachPx), "must_not_be_negative");
            p.PoleRideFac = ClampF(o, "pole_ride_fac", p.PoleRideFac,
                Mathf.Clamp01(p.PoleRideFac), "range_0_to_1");
            p.PoleRideLerp = ClampF(o, "pole_ride_lerp", p.PoleRideLerp,
                Mathf.Clamp(p.PoleRideLerp, 0.01f, 1f), "range_0.01_to_1");
            p.PoleCyclePx = ClampF(o, "pole_cycle_px", p.PoleCyclePx,
                Mathf.Max(4f, p.PoleCyclePx), "at_least_4_px_or_the_paw_clock_divides_by_zero");
            p.PoleReachFrac = ClampF(o, "pole_reach_frac", p.PoleReachFrac,
                Mathf.Clamp(p.PoleReachFrac, 0.05f, 0.9f), "range_0.05_to_0.9");
            p.PolePawPhaseOffset = ClampF(o, "pole_paw_phase_offset", p.PolePawPhaseOffset,
                Mathf.Clamp01(p.PolePawPhaseOffset), "range_0_to_1");
            p.PoleHindPhaseOffset = ClampF(o, "pole_hind_phase_offset", p.PoleHindPhaseOffset,
                Mathf.Clamp01(p.PoleHindPhaseOffset), "range_0_to_1");
            p.PoleFrontAmpPx = ClampF(o, "pole_front_amp_px", p.PoleFrontAmpPx,
                Mathf.Max(0f, p.PoleFrontAmpPx), "must_not_be_negative");
            p.PoleHindAmpPx = ClampF(o, "pole_hind_amp_px", p.PoleHindAmpPx,
                Mathf.Max(0f, p.PoleHindAmpPx), "must_not_be_negative");
            p.PolePawSpeedFac = ClampF(o, "pole_paw_speed_fac", p.PolePawSpeedFac,
                Mathf.Max(0.1f, p.PolePawSpeedFac), "at_least_0.1_or_the_paws_never_arrive");
            p.PoleLegBendFront = ClampF(o, "pole_leg_bend_front", p.PoleLegBendFront,
                Mathf.Clamp(p.PoleLegBendFront, -1.5f, 1.5f), "range_-1.5_to_1.5_it_scales_a_solved_joint_angle");
            p.PoleLegBendHind = ClampF(o, "pole_leg_bend_hind", p.PoleLegBendHind,
                Mathf.Clamp(p.PoleLegBendHind, -1.5f, 1.5f), "range_-1.5_to_1.5_it_scales_a_solved_joint_angle");
            p.PoleLegMetaDegFront = ClampF(o, "pole_leg_meta_deg_front", p.PoleLegMetaDegFront,
                Mathf.Clamp(p.PoleLegMetaDegFront, 0f, 90f), "range_0_to_90_degrees_off_the_poles_outward_normal");
            p.PoleLegMetaDegHind = ClampF(o, "pole_leg_meta_deg_hind", p.PoleLegMetaDegHind,
                Mathf.Clamp(p.PoleLegMetaDegHind, 0f, 90f), "range_0_to_90_degrees_off_the_poles_outward_normal");
            p.PolePullPulseFac = ClampF(o, "pole_pull_pulse_fac", p.PolePullPulseFac,
                Mathf.Clamp(p.PolePullPulseFac, 0f, 0.9f), "range_0_to_0.9_or_the_drive_reverses_mid_cycle");
            p.PoleBodySwayPx = ClampF(o, "pole_body_sway_px", p.PoleBodySwayPx,
                Mathf.Max(0f, p.PoleBodySwayPx), "must_not_be_negative_0_is_off");
            p.PoleBodySwayCycles = ClampI(o, "pole_body_sway_cycles", p.PoleBodySwayCycles,
                Math.Max(1, p.PoleBodySwayCycles), "at_least_1_sway_per_paw_cycle");
            p.PoleBodySwayLag = ClampF(o, "pole_body_sway_lag", p.PoleBodySwayLag,
                Mathf.Clamp01(p.PoleBodySwayLag), "range_0_to_1_of_a_sway");
            p.PoleStrokeJitter = ClampF(o, "pole_stroke_jitter", p.PoleStrokeJitter,
                Mathf.Max(0f, p.PoleStrokeJitter), "must_not_be_negative_0_is_off");
            p.PoleStrokeJitterPx = ClampF(o, "pole_stroke_jitter_px", p.PoleStrokeJitterPx,
                Mathf.Max(0f, p.PoleStrokeJitterPx), "must_not_be_negative_0_is_off");
            p.PoleLogTicks = ClampI(o, "pole_log_ticks", p.PoleLogTicks,
                Math.Max(0, p.PoleLogTicks), "must_not_be_negative");
            p.PoleMeetDetectTicks = ClampI(o, "pole_meet_detect_ticks", p.PoleMeetDetectTicks,
                Math.Max(0, p.PoleMeetDetectTicks), "must_not_be_negative_zero_is_off");
            p.PoleMeetProgressPx = ClampF(o, "pole_meet_progress_px", p.PoleMeetProgressPx,
                Mathf.Max(0.5f, p.PoleMeetProgressPx), "at_least_0.5_px_or_any_jitter_counts_as_progress");
            p.PoleMeetAheadPx = ClampF(o, "pole_meet_ahead_px", p.PoleMeetAheadPx,
                Mathf.Max(0f, p.PoleMeetAheadPx), "must_not_be_negative");
            p.PoleMeetLanePx = ClampF(o, "pole_meet_lane_px", p.PoleMeetLanePx,
                Mathf.Max(0f, p.PoleMeetLanePx), "must_not_be_negative");
            p.PoleMeetEscalateTicks = ClampI(o, "pole_meet_escalate_ticks", p.PoleMeetEscalateTicks,
                Math.Max(0, p.PoleMeetEscalateTicks), "must_not_be_negative");
            p.PoleMeetWaitTicks = ClampI(o, "pole_meet_wait_ticks", p.PoleMeetWaitTicks,
                Math.Max(0, p.PoleMeetWaitTicks), "must_not_be_negative");
            p.PoleMeetAsideTiles = ClampI(o, "pole_meet_aside_tiles", p.PoleMeetAsideTiles,
                Mathf.Clamp(p.PoleMeetAsideTiles, 0, 6), "range_0_to_6_tiles");
            p.PoleMeetGiveUpTicks = ClampI(o, "pole_meet_give_up_ticks", p.PoleMeetGiveUpTicks,
                Math.Max(0, p.PoleMeetGiveUpTicks), "must_not_be_negative_zero_is_never");
            p.PoleAlignFarMaxPx = ClampF(o, "pole_align_far_max_px", p.PoleAlignFarMaxPx,
                Mathf.Max(0f, p.PoleAlignFarMaxPx), "must_not_be_negative_zero_is_off");
            p.PoleAlignSwingPx = ClampF(o, "pole_align_swing_px", p.PoleAlignSwingPx,
                Mathf.Clamp(p.PoleAlignSwingPx, 0f, 4f), "range_0_to_4_px_per_tick");
            p.PoleFloorRegrabTicks = ClampI(o, "pole_floor_regrab_ticks", p.PoleFloorRegrabTicks,
                Math.Max(0, p.PoleFloorRegrabTicks), "must_not_be_negative_zero_is_off");
            p.WallGrabMaxSpeed = ClampF(o, "wall_grab_max_speed", p.WallGrabMaxSpeed,
                Mathf.Max(0f, p.WallGrabMaxSpeed), "must_not_be_negative");
            p.WallRegrabTicks = ClampI(o, "wall_regrab_ticks", p.WallRegrabTicks,
                Math.Max(0, p.WallRegrabTicks), "must_not_be_negative");
            p.WallCenterFac = ClampF(o, "wall_center_fac", p.WallCenterFac,
                Mathf.Clamp01(p.WallCenterFac), "range_0_to_1_above_1_overshoots");
            p.WallStandoffPx = ClampF(o, "wall_standoff_px", p.WallStandoffPx,
                Mathf.Clamp(p.WallStandoffPx, 0f, 10f), "range_0_to_10_px");
            p.WallPawFacePx = ClampF(o, "wall_paw_face_px", p.WallPawFacePx,
                Mathf.Clamp(p.WallPawFacePx, -5f, 10f), "range_minus5_to_10_px");
            p.WallPoseLerp = ClampF(o, "wall_pose_lerp", p.WallPoseLerp,
                Mathf.Clamp(p.WallPoseLerp, 0.01f, 1f), "range_0.01_to_1");
            p.WallLogTicks = ClampI(o, "wall_log_ticks", p.WallLogTicks,
                Math.Max(0, p.WallLogTicks), "must_not_be_negative_zero_is_edges_only");
            p.CorridorEntryFootingFrames = ClampI(o, "corridor_entry_footing_frames", p.CorridorEntryFootingFrames,
                Math.Max(0, p.CorridorEntryFootingFrames), "must_not_be_negative_zero_is_off");
            p.CorridorCenterFac = ClampF(o, "corridor_center_fac", p.CorridorCenterFac,
                Mathf.Clamp(p.CorridorCenterFac, 0f, 1f), "range_0_to_1_above_1_overshoots_zero_is_off");
            p.CorridorCenterReachTiles = ClampF(o, "corridor_center_reach_tiles", p.CorridorCenterReachTiles,
                Mathf.Clamp(p.CorridorCenterReachTiles, 0f, 2f), "range_0_to_2_tiles_zero_is_off_two_is_two_spine_links");
            p.DrawSmoothing = ClampF(o, "draw_smoothing", p.DrawSmoothing,
                Mathf.Clamp(p.DrawSmoothing, 0f, 0.95f), "range_0_to_0.95_body_must_still_follow");
            p.TailSwellPos = ClampF(o, "tail_swell_pos", p.TailSwellPos,
                Mathf.Clamp(p.TailSwellPos, 0.01f, 0.99f), "range_0.01_to_0.99");
            p.LimbLengthFac = ClampF(o, "limb_length_fac", p.LimbLengthFac,
                Mathf.Clamp(p.LimbLengthFac, 0.05f, 10f),
                "range_0.05_to_10_it_becomes_a_per_draw_loop_bound");
            p.StalkCrouchLimbFac = ClampF(o, "stalk_crouch_limb_fac", p.StalkCrouchLimbFac,
                Mathf.Clamp(p.StalkCrouchLimbFac, 0f, 4f),
                "range_0_to_4_it_multiplies_a_per_draw_loop_bound");
            p.BodyRideHeight = ClampF(o, "body_ride_height", p.BodyRideHeight,
                Mathf.Max(0f, p.BodyRideHeight),
                "must_not_be_negative_it_is_the_leg_root_height");
            p.BodyRideCorridorFac = ClampF(o, "body_ride_corridor_fac", p.BodyRideCorridorFac,
                Mathf.Clamp01(p.BodyRideCorridorFac), "range_0_to_1_one_is_off");
            p.BodyRideCorridorLerp = ClampF(o, "body_ride_corridor_lerp", p.BodyRideCorridorLerp,
                Mathf.Clamp(p.BodyRideCorridorLerp, 0.01f, 1f), "range_0.01_to_1_one_is_a_snap");
            p.BodyRideDeathFac = ClampF(o, "body_ride_death_fac", p.BodyRideDeathFac,
                Mathf.Clamp01(p.BodyRideDeathFac), "range_0_to_1_one_is_off");
            p.BodyRideDeathTicks = ClampI(o, "body_ride_death_ticks", p.BodyRideDeathTicks,
                Math.Max(0, p.BodyRideDeathTicks), "must_not_be_negative_zero_is_instant");
            p.BodyRideStunCollapseFac = ClampF(o, "body_ride_stun_collapse_fac", p.BodyRideStunCollapseFac,
                Mathf.Clamp01(p.BodyRideStunCollapseFac), "range_0_to_1");
            p.DeathLimpTicks = ClampI(o, "death_limp_ticks", p.DeathLimpTicks,
                Math.Max(0, p.DeathLimpTicks), "must_not_be_negative_zero_is_instant");
            p.DeathBodyFlexMaxDeg = ClampF(o, "death_body_flex_max_deg", p.DeathBodyFlexMaxDeg,
                Mathf.Clamp(p.DeathBodyFlexMaxDeg, 0f, 170f), "range_0_to_170_zero_is_no_degree_cap");
            p.DeathBodyBendFac = ClampF(o, "death_body_bend_fac", p.DeathBodyBendFac,
                Mathf.Max(0f, p.DeathBodyBendFac), "must_not_be_negative");
            p.DeathBodyBendMax = ClampF(o, "death_body_bend_max", p.DeathBodyBendMax,
                Mathf.Max(0f, p.DeathBodyBendMax), "must_not_be_negative");
            p.DeathTailLimpFac = ClampF(o, "death_tail_limp_fac", p.DeathTailLimpFac,
                Mathf.Clamp01(p.DeathTailLimpFac), "range_0_to_1");
            p.DeathTailRootDroop = ClampF(o, "death_tail_root_droop", p.DeathTailRootDroop,
                Mathf.Clamp01(p.DeathTailRootDroop), "range_0_to_1");
            p.DeathHeadGravity = ClampF(o, "death_head_gravity", p.DeathHeadGravity,
                Mathf.Max(0f, p.DeathHeadGravity), "must_not_be_negative");
            p.DeathBehindConeDeg = ClampF(o, "death_behind_cone_deg", p.DeathBehindConeDeg,
                Mathf.Clamp(p.DeathBehindConeDeg, 0f, 360f), "range_0_to_360_180_is_a_half_plane");
            p.BlindStunTicksNear = ClampI(o, "blind_stun_ticks_near", p.BlindStunTicksNear,
                Math.Max(0, p.BlindStunTicksNear), "must_not_be_negative_zero_is_off");
            p.BlindStunTicksFar = ClampI(o, "blind_stun_ticks_far", p.BlindStunTicksFar,
                Math.Max(0, p.BlindStunTicksFar), "must_not_be_negative_zero_is_off");
            p.HeadSize = ClampF(o, "head_size", p.HeadSize,
                Mathf.Max(0.05f, p.HeadSize), "min_0.05");
            p.BodyMeshPoints = ClampI(o, "body_mesh_points", p.BodyMeshPoints,
                Mathf.Clamp(p.BodyMeshPoints, 3, 32), "range_3_to_32");
            p.LimbUpperFac = ClampF(o, "limb_upper_fac", p.LimbUpperFac,
                Mathf.Clamp(p.LimbUpperFac, 0.1f, 0.9f), "range_0.1_to_0.9_both_bones_need_length");
            p.LimbBendSmooth = ClampF(o, "limb_bend_smooth", p.LimbBendSmooth,
                Mathf.Clamp(p.LimbBendSmooth, 0.01f, 1f), "range_0.01_to_1");
            p.TailRootDroop = ClampF(o, "tail_root_droop", p.TailRootDroop,
                Mathf.Clamp01(p.TailRootDroop), "range_0_to_1");
            p.HeadNeckSlack = ClampF(o, "head_neck_slack", p.HeadNeckSlack,
                Mathf.Clamp(p.HeadNeckSlack, 0.05f, 2f), "range_0.05_to_2");
            p.StarveRegenFac = ClampF(o, "starve_regen_fac", p.StarveRegenFac,
                Mathf.Clamp01(p.StarveRegenFac), "range_0_to_1_multiplies_a_heal");
            p.BodySpriteScaleFac = ClampF(o, "body_sprite_scale_fac", p.BodySpriteScaleFac,
                Mathf.Max(0.05f, p.BodySpriteScaleFac), "min_0.05_a_zero_scale_draws_nothing");
            p.BodySpriteStretch = ClampF(o, "body_sprite_stretch", p.BodySpriteStretch,
                Mathf.Clamp01(p.BodySpriteStretch), "range_0_to_1");

            p.LedgePounceMaxRise = ClampF(o, "ledge_pounce_max_rise", p.LedgePounceMaxRise,
                Mathf.Clamp(p.LedgePounceMaxRise, 0f, Fox.MAX_SWEEP_RISE_PX), "range_0_to_2000_px_it_is_a_loop_bound");
            p.PounceFlightMaxFrames = ClampI(o, "pounce_flight_max_frames", p.PounceFlightMaxFrames,
                Mathf.Clamp(p.PounceFlightMaxFrames, 1, 600), "range_1_to_600_ticks_it_is_a_loop_bound");
            p.PounceBoostFrames = ClampI(o, "pounce_boost_frames", p.PounceBoostFrames,
                Mathf.Clamp(p.PounceBoostFrames, 0, 600), "range_0_to_600_ticks_it_is_a_loop_bound");
            p.PhysAirFriction = ClampF(o, "air_friction", p.PhysAirFriction,
                Mathf.Clamp(p.PhysAirFriction, 0.5f, 1f), "range_0.5_to_1_a_flight_must_terminate");

            p.LedgePounceLipScanTiles = ClampI(o, "ledge_pounce_lip_scan_tiles", p.LedgePounceLipScanTiles,
                Math.Min(p.LedgePounceLipScanTiles, Fox.MAX_LIP_SCAN_COLUMNS),
                "max_200_columns_it_is_a_per_tick_loop_bound");
            p.LedgePounceHeadroomTiles = ClampI(o, "ledge_pounce_headroom_tiles", p.LedgePounceHeadroomTiles,
                Math.Min(p.LedgePounceHeadroomTiles, 50), "max_50_tiles_it_is_a_per_tick_loop_bound");
            p.LedgePounceMaxDrop = ClampF(o, "ledge_pounce_max_drop", p.LedgePounceMaxDrop,
                Mathf.Clamp(p.LedgePounceMaxDrop, 0f, 1000f), "range_0_to_1000_px_it_is_a_per_tick_loop_bound");
            p.PounceLandingScanTiles = ClampI(o, "pounce_landing_scan_tiles", p.PounceLandingScanTiles,
                Mathf.Clamp(p.PounceLandingScanTiles, 0, 200), "range_0_to_200_tiles_it_is_a_loop_bound");
            p.MantleMaxScanTiles = ClampI(o, "mantle_max_scan_tiles", p.MantleMaxScanTiles,
                Math.Min(p.MantleMaxScanTiles, 50), "max_50_tiles_it_is_a_per_tick_loop_bound");
            p.RouteHoleProbeTiles = ClampI(o, "route_hole_probe_tiles", p.RouteHoleProbeTiles,
                Math.Min(p.RouteHoleProbeTiles, 50), "max_50_tiles_it_is_a_per_tick_loop_bound");
            p.PounceRoutePeekSteps = ClampI(o, "pounce_route_peek_steps", p.PounceRoutePeekSteps,
                Math.Min(p.PounceRoutePeekSteps, 200), "max_200_steps_it_is_a_per_tick_loop_bound");
            p.PounceRouteItersPerTick = ClampI(o, "pounce_route_iters_per_tick", p.PounceRouteItersPerTick,
                Math.Min(p.PounceRouteItersPerTick, 5000), "max_5000_it_is_a_per_tick_loop_bound");
            p.PounceRouteMaxFinders = ClampI(o, "pounce_route_max_finders", p.PounceRouteMaxFinders,
                Math.Min(p.PounceRouteMaxFinders, 32),
                "max_32_its_count_bounds_five_per_tick_loops");
            p.PounceRouteGoalSeedSnapRows = ClampI(o, "pounce_route_goal_seed_snap_rows",
                p.PounceRouteGoalSeedSnapRows, Math.Min(p.PounceRouteGoalSeedSnapRows, 50),
                "max_50_rows_it_is_a_per_tick_loop_bound");
            p.PounceRouteSeedAttemptsPerTick = ClampI(o, "pounce_route_seed_attempts_per_tick",
                p.PounceRouteSeedAttemptsPerTick, Math.Min(p.PounceRouteSeedAttemptsPerTick, 500),
                "max_500_it_is_a_per_tick_loop_bound");
            p.RoamAttemptsPerCandidate = ClampI(o, "roam_attempts_per_candidate", p.RoamAttemptsPerCandidate,
                Math.Min(p.RoamAttemptsPerCandidate, 200), "max_200_it_is_a_per_tick_loop_bound");
            p.LedgePounceStandoffPx = ClampF(o, "ledge_pounce_standoff_px", p.LedgePounceStandoffPx,
                Mathf.Clamp(p.LedgePounceStandoffPx, 0f, 4000f),
                "range_0_to_4000_px_it_becomes_a_per_tick_loop_bound");
            p.LandSoftFac = ClampF(o, "land_soft_fac", p.LandSoftFac,
                Mathf.Clamp(p.LandSoftFac, 0f, 8f), "range_0_to_8_it_scales_both_landing_thresholds");

            p.CamoVisibilityBonus = ClampF(o, "camo_visibility_bonus", p.CamoVisibilityBonus,
                Mathf.Clamp(p.CamoVisibilityBonus, 0f, 1f),
                "range_0_to_1_negative_would_make_a_hiding_fox_easier_to_see_and_above_1_is_clamped_by_BodyChunk");
            p.CamoArmRate = ClampF(o, "camo_arm_rate", p.CamoArmRate,
                Mathf.Clamp(p.CamoArmRate, 0f, 1f), "range_0_to_1_it_is_a_per_tick_step_of_a_0_to_1_ramp");
            p.CamoBreakRate = ClampF(o, "camo_break_rate", p.CamoBreakRate,
                Mathf.Clamp(p.CamoBreakRate, 0f, 1f), "range_0_to_1_it_is_a_per_tick_step_of_a_0_to_1_ramp");
            p.CamoBlendMax = ClampF(o, "camo_blend_max", p.CamoBlendMax,
                Mathf.Clamp01(p.CamoBlendMax), "range_0_to_1_it_is_a_colour_lerp_factor");
            p.CamoColorFogMix = ClampF(o, "camo_color_fog_mix", p.CamoColorFogMix,
                Mathf.Clamp01(p.CamoColorFogMix), "range_0_to_1_it_is_a_palette_lerp_factor");
            p.CamoColorDrift = ClampF(o, "camo_color_drift", p.CamoColorDrift,
                Mathf.Clamp01(p.CamoColorDrift), "range_0_to_1_it_is_a_per_tick_colour_lerp_factor");
            if (!IsCamoSource(p.CamoColorSource, CAMO_SOURCE_BACKGROUND)
                && !IsCamoSource(p.CamoColorSource, CAMO_SOURCE_PALETTE))
            {
                p.CamoColorSource = CAMO_SOURCE_BACKGROUND;
            }
            p.CamoHiddenThreshold = ClampF(o, "camo_hidden_threshold", p.CamoHiddenThreshold,
                Mathf.Clamp01(p.CamoHiddenThreshold), "range_0_to_1_it_is_compared_against_a_0_to_1_amount");
            p.CamoBreakHoldTicks = ClampI(o, "camo_break_hold_ticks", p.CamoBreakHoldTicks,
                Math.Max(0, p.CamoBreakHoldTicks), "min_0_a_negative_hold_would_never_expire");
            if (p.CamoSpeedVisible <= p.CamoSpeedHidden)
            {
                p.CamoSpeedVisible = ClampF(o, "camo_speed_visible", p.CamoSpeedVisible,
                    p.CamoSpeedHidden + 0.01f,
                    "must_exceed_camo_speed_hidden_or_the_ramp_inverts_and_camo_never_arms");
            }

            p.BaitSpotCandidates = ClampI(o, "bait_spot_candidates", p.BaitSpotCandidates,
                Mathf.Clamp(p.BaitSpotCandidates, 1, 64), "range_1_to_64_it_is_a_loop_bound_run_in_one_tick");
            p.BaitSpotRadiusTiles = ClampI(o, "bait_spot_radius_tiles", p.BaitSpotRadiusTiles,
                Mathf.Clamp(p.BaitSpotRadiusTiles, 1, 40), "range_1_to_40_it_is_a_loop_bound_run_in_one_tick");
            p.BaitPrepMaxRiseTiles = ClampI(o, "bait_prep_max_rise_tiles", p.BaitPrepMaxRiseTiles,
                Mathf.Clamp(p.BaitPrepMaxRiseTiles, 0, 8), "range_0_to_8_it_is_a_loop_bound_run_in_one_tick");
            p.BaitPrepHeadroomTiles = ClampI(o, "bait_prep_headroom_tiles", p.BaitPrepHeadroomTiles,
                Mathf.Clamp(p.BaitPrepHeadroomTiles, 0, 6), "range_0_to_6_it_is_a_loop_bound_run_in_one_tick");
            p.BaitTrackMaxItems = ClampI(o, "bait_track_max_items", p.BaitTrackMaxItems,
                Mathf.Clamp(p.BaitTrackMaxItems, 1, 40), "range_1_to_40_the_tracker_evicts_above_it_and_0_would_track_nothing");
            p.BaitPrepMaxDistFac = ClampF(o, "bait_prep_max_dist_fac", p.BaitPrepMaxDistFac,
                Mathf.Clamp01(p.BaitPrepMaxDistFac), "range_0_to_1_it_is_a_fraction_of_the_foxs_own_level_reach_and_beyond_that_no_line_reaches");
            p.BaitPrepMinDistFac = ClampF(o, "bait_prep_min_dist_fac", p.BaitPrepMinDistFac,
                Mathf.Clamp(p.BaitPrepMinDistFac, 0f, p.BaitPrepMaxDistFac),
                "range_0_to_bait_prep_max_dist_fac_an_inverted_band_contains_no_prep_spot");
            p.BaitArrivePx = ClampF(o, "bait_arrive_px", p.BaitArrivePx,
                Mathf.Max(5f, p.BaitArrivePx), "min_5_a_smaller_radius_is_never_reached_and_the_bait_is_never_put_down");
            p.BaitTriggerRangePx = ClampF(o, "bait_trigger_range_px", p.BaitTriggerRangePx,
                Mathf.Max(0f, p.BaitTriggerRangePx), "min_0_a_negative_radius_contains_nothing_and_only_a_grab_could_spring_the_trap");
            p.BaitFaceMaxTicks = ClampI(o, "bait_face_max_ticks", p.BaitFaceMaxTicks,
                Math.Max(0, p.BaitFaceMaxTicks), "min_0_which_means_never_turn");

            p.DeriveLeapEnvelope();

            return p;
        }

        private static void ReadRoomAttraction(JsonObject o, FoxBreedParams p)
        {
            var v = o.TryGet("room_attraction");
            if (!v.HasValue) return;

            var list = v.Value.TryList();
            if (list == null)
            {
                return;
            }

            var names = new List<string>();
            for (int i = 0; i < list.Value.Count; i++)
            {
                try { names.Add(list.Value[i].AsString()); }
                catch (JsonException e) { BadValue("room_attraction[" + i + "]", e, "skipped"); }
            }
            p.RoomAttraction.Clear();
            p.RoomAttraction.AddRange(names);
        }

        private static void ReadRelationships(JsonObject o, FoxBreedParams p)
        {
            var v = o.TryGet("relationships");
            if (!v.HasValue) return;

            var obj = v.Value.TryObject();
            if (obj == null)
            {
                return;
            }

            var parsed = new List<FoxRelationship>();
            foreach (var kv in obj.Value)
            {
                if (kv.Key.StartsWith("_")) continue;

                var entry = kv.Value.TryObject();
                if (entry == null)
                {
                    continue;
                }

                string type = S(entry.Value, "type", null);
                float intensity = F(entry.Value, "intensity", 0f);
                string revType = null;
                float revIntensity = 0f;

                var rev = entry.Value.TryGet("reverse");
                if (rev.HasValue)
                {
                    var revObj = rev.Value.TryObject();
                    if (revObj == null)
                    {
                    }
                    else
                    {
                        revType = S(revObj.Value, "type", null);
                        revIntensity = F(revObj.Value, "intensity", 0f);
                    }
                }

                parsed.Add(new FoxRelationship(kv.Key, type, intensity, revType, revIntensity));
            }

            p.Relationships.Clear();
            p.Relationships.AddRange(parsed);
        }

        private static void ReadTiles(JsonObject o, FoxBreedParams p)
        {
            var v = o.TryGet("tiles");
            if (!v.HasValue) return;
            var obj = v.Value.TryObject();
            if (obj == null) return;

            foreach (var kv in obj.Value)
            {
                if (kv.Key.StartsWith("_")) continue;
                if (!TryParseEnum(kv.Key, out AItile.Accessibility acc))
                {
                    continue;
                }
                if (ReadPathCost(kv.Value, out PathCost cost)) p.Tiles[acc] = cost;
                else p.Tiles.Remove(acc);
            }
        }

        private static void ReadCosmetics(JsonObject o, FoxBreedParams p)
        {
            var v = o.TryGet("cosmetics");
            if (!v.HasValue) return;

            var list = v.Value.TryList();
            if (list == null)
            {
                return;
            }

            var parsed = new List<FoxCosmeticSpec>();
            for (int i = 0; i < list.Value.Count; i++)
            {
                var entry = list.Value[i].TryObject();
                if (entry == null)
                {
                    continue;
                }
                string layer = S(entry.Value, "layer", null);
                if (string.IsNullOrEmpty(layer))
                {
                    continue;
                }
                parsed.Add(new FoxCosmeticSpec(layer, entry.Value));
            }

            p.Cosmetics.Clear();
            p.Cosmetics.AddRange(parsed);
        }

        private static void ReadConnections(JsonObject o, FoxBreedParams p)
        {
            var v = o.TryGet("connections");
            if (!v.HasValue) return;
            var obj = v.Value.TryObject();
            if (obj == null) return;

            foreach (var kv in obj.Value)
            {
                if (kv.Key.StartsWith("_")) continue;
                if (!TryParseEnum(kv.Key, out MovementConnection.MovementType mt))
                {
                    continue;
                }
                if (ReadPathCost(kv.Value, out PathCost cost)) p.Connections[mt] = cost;
                else p.Connections.Remove(mt);
            }
        }

        private static bool TryParseEnum<T>(string key, out T value) where T : struct
        {
            return Enum.TryParse(key.Replace("_", ""), true, out value) && Enum.IsDefined(typeof(T), value);
        }

        private static bool ReadPathCost(JsonAny any, out PathCost cost)
        {
            cost = default;
            var obj = any.TryObject();
            if (obj == null) return false;

            string legalityName = S(obj.Value, "legality", "Allowed");
            if (legalityName.Equals("inherit", StringComparison.OrdinalIgnoreCase)) return false;
            if (!Enum.TryParse(legalityName, true, out PathCost.Legality legality))
            {
                legality = PathCost.Legality.Allowed;
            }
            cost = new PathCost(F(obj.Value, "resistance", 1f), legality);

            return true;
        }

        private static void MigrateRetiredPounceKey(JsonObject o, string oldKey, string newKey,
                                                    int oldValue, ref int target)
        {
            if (!o.TryGet(oldKey).HasValue || o.TryGet(newKey).HasValue) return;
            target = oldValue;
        }

        private static void MigrateRetiredPounceKeyF(JsonObject o, string oldKey, string newKey,
                                                     float oldValue, ref float target)
        {
            if (!o.TryGet(oldKey).HasValue || o.TryGet(newKey).HasValue) return;
            target = oldValue;
        }

        private static void BadValue(string key, JsonException e, object fallback)
        {
            FoxFault.Warn($"bad_value key={key} path={(e.JsonPath ?? "unknown")} " +
                        $"msg=\"{e.Message}\" action=key_defaulted value={fallback}");
        }

        internal static float F(JsonObject o, string key, float fallback)
        {
            var v = o.TryGet(key);
            if (!v.HasValue) return fallback;
            float f;
            try { f = v.Value.AsFloat(); }
            catch (JsonException e) { BadValue(key, e, fallback); return fallback; }
            if (float.IsNaN(f) || float.IsInfinity(f))
            {
                return fallback;
            }
            return f;
        }

        internal static int I(JsonObject o, string key, int fallback)
        {
            var v = o.TryGet(key);
            if (!v.HasValue) return fallback;
            try { return v.Value.AsInt(); }
            catch (JsonException e) { BadValue(key, e, fallback); return fallback; }
        }

        internal static bool B(JsonObject o, string key, bool fallback)
        {
            var v = o.TryGet(key);
            if (!v.HasValue) return fallback;
            try { return v.Value.AsBool(); }
            catch (JsonException e) { BadValue(key, e, fallback); return fallback; }
        }

        internal static string S(JsonObject o, string key, string fallback)
        {
            var v = o.TryGet(key);
            if (!v.HasValue) return fallback;
            try { return v.Value.AsString(); }
            catch (JsonException e) { BadValue(key, e, fallback); return fallback; }
        }

        private static int ClampI(JsonObject o, string key, int requested, int applied, string reason)
        {
            return applied;
        }

        private static float ClampF(JsonObject o, string key, float requested, float applied, string reason)
        {
            if (float.IsNaN(applied) || float.IsInfinity(applied))
            {
                return 0f;
            }
            return applied;
        }

        internal static Color C(JsonObject o, string key, Color fallback)
        {
            var v = o.TryGet(key);
            if (!v.HasValue) return fallback;

            string hex;
            try { hex = v.Value.AsString().TrimStart('#'); }
            catch (JsonException e) { BadValue(key, e, fallback); return fallback; }

            if (hex.Length >= 6
                && int.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r)
                && int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g)
                && int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
            {
                return new Color(r / 255f, g / 255f, b / 255f);
            }
            return fallback;
        }
    }

    public sealed class FoxCosmeticSpec
    {
        public readonly string Layer;

        private readonly JsonObject _params;
        private readonly bool _hasParams;

        public FoxCosmeticSpec(string layer, JsonObject paramsObject)
        {
            Layer = layer;
            _params = paramsObject;
            _hasParams = true;
        }

        public FoxCosmeticSpec(string layer)
        {
            Layer = layer;
            _hasParams = false;
        }

        public float F(string key, float fallback)
        {
            return _hasParams ? FoxBreedParams.F(_params, key, fallback) : fallback;
        }

        public int I(string key, int fallback)
        {
            return _hasParams ? FoxBreedParams.I(_params, key, fallback) : fallback;
        }

        public bool B(string key, bool fallback)
        {
            return _hasParams ? FoxBreedParams.B(_params, key, fallback) : fallback;
        }

        public string S(string key, string fallback)
        {
            return _hasParams ? FoxBreedParams.S(_params, key, fallback) : fallback;
        }

        public Color C(string key, Color fallback)
        {
            return _hasParams ? FoxBreedParams.C(_params, key, fallback) : fallback;
        }

        public static List<FoxCosmeticSpec> Defaults()
        {
            return new List<FoxCosmeticSpec>
            {
                new FoxCosmeticSpec("Ears"),
                new FoxCosmeticSpec("Snout"),
                new FoxCosmeticSpec("BrushTail"),
            };
        }

        public override string ToString()
        {
            return Layer;
        }
    }

    public sealed class FoxRelationship
    {
        public readonly string Other;
        public readonly string Type;
        public readonly float Intensity;
        public readonly string ReverseType;
        public readonly float ReverseIntensity;

        public FoxRelationship(string other, string type, float intensity,
                               string reverseType = null, float reverseIntensity = 0f)
        {
            Other = other;
            Type = type;
            Intensity = intensity;
            ReverseType = reverseType;
            ReverseIntensity = reverseIntensity;
        }

        public override string ToString()
        {
            return $"{Other}:{Type ?? "none"}/{Intensity:0.00}" +
                   (ReverseType == null ? "" : $"<-{ReverseType}/{ReverseIntensity:0.00}");
        }

        public static List<FoxRelationship> Defaults()
        {
            return new List<FoxRelationship>
            {
                new FoxRelationship("Slugcat",        "Eats", 0.6f,  "Attacks", 0.4f),
                new FoxRelationship("LanternMouse",   "Eats", 0.7f),
                new FoxRelationship("CicadaA",        "Eats", 0.5f),
                new FoxRelationship("CicadaB",        "Eats", 0.5f),
                new FoxRelationship("EggBug",         "Eats", 0.45f),
                new FoxRelationship("Hazer",          "Eats", 0.4f),
                new FoxRelationship("Snail",          "Eats", 0.35f),
                new FoxRelationship("VultureGrub",    "Eats", 0.4f),
                new FoxRelationship("Centipede",      "Eats", 0.3f),
                new FoxRelationship("SmallCentipede", "Eats", 0.4f),

                new FoxRelationship("LizardTemplate", "AgressiveRival", 0.5f, "AgressiveRival", 0.5f),

                new FoxRelationship("RedLizard",  null, 0f, "Eats", 0.7f),
                new FoxRelationship("Vulture",    null, 0f, "Eats", 0.5f),
                new FoxRelationship("KingVulture",null, 0f, "Eats", 0.7f),
                new FoxRelationship("BigEel",     null, 0f, "Eats", 1f),
                new FoxRelationship("MirosBird",  null, 0f, "Eats", 0.8f),

                new FoxRelationship("DaddyLongLegs",   "Afraid", 1f),
                new FoxRelationship("BrotherLongLegs", "Afraid", 0.9f),
                new FoxRelationship("TempleGuard",     "Afraid", 1f),
                new FoxRelationship("RedCentipede",    "Afraid", 0.8f),
                new FoxRelationship("SpitterSpider",   "Afraid", 0.6f),
                new FoxRelationship("BigSpider",       "Afraid", 0.45f),
                new FoxRelationship("Deer",            "Afraid", 0.2f),
                new FoxRelationship("PoleMimic",       "Afraid", 0.5f),
                new FoxRelationship("TentaclePlant",   "Afraid", 0.8f),

                new FoxRelationship("Scavenger", "Afraid", 0.3f, "Attacks", 0.4f),
            };
        }
    }

    public static class FoxParams
    {
        public const string FILE = "fox/base.json";

        public const string VARIANT_DIR = "fox/variants";

        private const int RELOAD_POLL_TICKS = 80;

        private static readonly Dictionary<string, FoxBreedParams> Loaded =
            new Dictionary<string, FoxBreedParams>();

        private static readonly Dictionary<string, FoxBreedParams> Fallbacks =
            new Dictionary<string, FoxBreedParams>();

        private sealed class Watched
        {
            public string Rel;
            public string Path;
            public bool Present;
            public DateTime Stamp;
        }

        private static readonly List<Watched> _watched = new List<Watched>();

        private static int _lastPollTick = -1000000;

        public static bool ForceDebug { get; private set; }

        public static bool HotReload { get; private set; } = true;

        public static void Load()
        {
            Reload("init");
        }

        public static FoxBreedParams For(string variantId)
        {
            if (Loaded.TryGetValue(variantId, out var p)) return p;
            if (Fallbacks.TryGetValue(variantId, out var f)) return f;

            f = new FoxBreedParams();
            Fallbacks[variantId] = f;
            f.DeriveLeapEnvelope();
            return f;
        }

        public static void PollReload(int gameTick)
        {
            if (!HotReload) return;

            if (gameTick < _lastPollTick) _lastPollTick = gameTick;
            if (gameTick - _lastPollTick < RELOAD_POLL_TICKS) return;
            _lastPollTick = gameTick;

            bool changed = false;
            try
            {
                foreach (var w in _watched)
                {
                    if (w.Path == null || !File.Exists(w.Path))
                    {
                        string candidate = AssetManager.ResolveFilePath(w.Rel);
                        bool nowPresent = File.Exists(candidate);
                        if (nowPresent != w.Present)
                        {
                            changed = true;
                            break;
                        }
                        continue;
                    }
                    if (File.GetLastWriteTimeUtc(w.Path) != w.Stamp)
                    {
                        changed = true;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                FoxFault.Warn($"reload_poll_failed err={e.GetType().Name} msg=\"{e.Message}\" " +
                            $"action=disabling_hot_reload");
                HotReload = false;
                return;
            }

            if (changed) Reload("hot");
        }

        private static Watched Watch(string rel)
        {
            var w = new Watched { Rel = rel, Path = AssetManager.ResolveFilePath(rel) };
            w.Present = File.Exists(w.Path);
            if (w.Present) w.Stamp = File.GetLastWriteTimeUtc(w.Path);
            _watched.Add(w);
            return w;
        }

        private static void Reload(string reason)
        {
            try
            {
                _watched.Clear();
                Watched baseFile = Watch(FILE);
                List<string> ids = FoxCritob.RegisteredVariantIds();
                var variantFiles = new Dictionary<string, Watched>();
                foreach (var id in ids) variantFiles[id] = Watch(VARIANT_DIR + "/" + id + ".json");

                if (!baseFile.Present)
                {
                    ForceDebug = true;
                    Loaded.Clear();
                    return;
                }

                string baseText = File.ReadAllText(baseFile.Path);
                var root = JsonAny.Parse(baseText).TryObject();
                if (root == null)
                {
                    ForceDebug = true;
                    return;
                }

                ReadOptions(root.Value);
                List<FoxParamsMerge.Member> baseMembers = FoxParamsMerge.Members(baseText);

                var variantMembers = new Dictionary<string, List<FoxParamsMerge.Member>>();
                var notObject = new HashSet<string>();
                foreach (var id in ids)
                {
                    Watched vf = variantFiles[id];
                    if (!vf.Present) continue;
                    string vText = File.ReadAllText(vf.Path);
                    try
                    {
                        if (JsonAny.Parse(vText).TryObject() == null)
                        {
                            notObject.Add(id);
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        throw new FormatException($"{vf.Path}: {e.Message}", e);
                    }
                    variantMembers[id] = FoxParamsMerge.Members(vText);
                }

                var parsed = new Dictionary<string, FoxBreedParams>();
                foreach (var id in ids)
                {
                    if (notObject.Contains(id))
                    {
                        continue;
                    }

                    variantMembers.TryGetValue(id, out var vm);
                    var unknown = new List<string>();
                    string merged = FoxParamsMerge.Merge(baseMembers, vm, unknown, out int overrides);

                    try
                    {
                        parsed[id] = FoxBreedParams.FromJson(FoxParamsMerge.ToObject(id, merged));
                    }
                    catch (Exception e)
                    {
                        FoxFault.Warn($"params_entry_failed variant={id} err={e.GetType().Name} " +
                                    $"path={((e as JsonException)?.JsonPath ?? "unknown")} " +
                                    $"msg=\"{e.Message}\" action=defaults_used");
                    }
                }

                Loaded.Clear();
                foreach (var kv in parsed) Loaded[kv.Key] = kv.Value;
                foreach (var kv in parsed) Fallbacks.Remove(kv.Key);
            }
            catch (Exception e)
            {
                ForceDebug = true;
                FoxFault.Warn($"params_load_failed reason={reason} err={e.GetType().Name} " +
                            $"path={((e as JsonException)?.JsonPath ?? "unknown")} " +
                            $"msg=\"{e.Message}\" action=csharp_defaults debug_forced=true");
            }
        }

        private static void ReadOptions(JsonObject root)
        {
            var v = root.TryGet("_options");
            if (!v.HasValue) return;
            var obj = v.Value.TryObject();
            if (obj == null) return;

            ForceDebug = FoxBreedParams.B(obj.Value, "debug", ForceDebug);
            HotReload = FoxBreedParams.B(obj.Value, "hot_reload", HotReload);
        }
    }
}
