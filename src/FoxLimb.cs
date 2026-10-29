using System;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public class FoxLimb : Limb
    {
        private readonly FoxGraphics gfx;

        public FoxLimb otherLimbInPair;

        public FoxLimb gaitPartner;

        public FoxLimb gaitSuccessor;

        public float gaitGapFrac;

        private float gaitHoldOdo = -1f;

        private float swingTravel;
        private bool swingTravelSeeded;
        private float swingStartOdo;

        private float lastStrideRef;

        public void ClearGaitHold()
        {
            gaitHoldOdo = -1f;
        }

        public float swing, lastSwing;

        public bool reachingForTerrain;

        public int gripCounter;

        public float jointDist;

        private const float REACH_GROUND_MARGIN = 0.5f;

        private const float PROBE_MAX_PX = 512f;

        private const float MIN_REACH_FAC = 0.6f;

        private const float PAW_MAX_STEP_FRAC = 0.25f;

        private float groundReach;
        private float standDrop;
        private float halfSweep;

        public float flip;

        public float bend;
        public float lastBend;

        public Vector2 attachPos;

        private bool extraLongStep;

        private Vector2 plantTarget;
        private bool plantCommitted;

        private bool stepStance;
        private int swingTicks;
        private int commitTicks;

        private int plantedTicks;

        private float swingStartBehind;
        private bool swingSeeded;

        private float swingProgress;

        private float lastSwingPhase;
        public float LastSwingPhase { get { return lastSwingPhase; } }

        private bool trulyPlanted;
        private int trueStanceTicks;

        public int ContactTicks(FoxBreedParams p)
        {
            if (OnPole || OnWall || p == null || !p.StepTrueStance) return gripCounter;
            return trueStanceTicks;
        }

        private const float GROUND_CONTACT_SLACK_PX = 1.5f;

        private const float PLANT_LEVEL_TOL_PX = 3f;
        private const float PLANT_CHORD_MARGIN_PX = 0.5f;

        internal const int AHEAD_KEY_OFF = 2, AHEAD_NO_GEOMETRY = 3,
                           AHEAD_ACTION = 4, AHEAD_TURN_STEP = 5, AHEAD_POSE = 6, AHEAD_AIRBORNE = 7,
                           AHEAD_CORRIDOR = 8, AHEAD_CLIMBER = 9, AHEAD_WATER = 10, AHEAD_SLOW = 11,
                           AHEAD_NOT_PLAIN = 12, AHEAD_NO_GROUND_ROOT = 13, AHEAD_NO_GROUND_AHEAD = 14,
                           AHEAD_STEP = 15, AHEAD_BLOCKED = 16, AHEAD_OUT_OF_REACH = 17
                           ;

        private int pendingReleaseWhy;
        internal const int REL_NONE = 0, REL_BEAT = 1, REL_OVER_EXTENDED = 2, REL_OUT_OF_REACH = 3,
                           REL_SWEEP = 4, REL_TURN = 5, REL_DANGLE = 6, REL_POLE = 7, REL_LOST = 8,
                           REL_WALL = 9,
                           REL_REASONS = 10;
        private readonly int[] releaseCounts = new int[REL_REASONS];

        private bool stepAirborne;
        private int stepLiftTick;
        private float stepLiftY, stepMaxY;

        private float swingPhase;
        public float SwingPhase { get { return swingPhase; } }

        private bool swingTargetFrozen;
        private Vector2 frozenTarget;

        private Vector2 gripAnchor;
        private float slipPath, slipDrift, lastSlipPath, lastSlipDrift;

        private float lastTargetAbove;

        private bool MantleReaching
        {
            get
            {
                if (gfx == null || gfx.fox == null || !gfx.fox.MantleActive) return false;
                return (limbNumber < 2) != gfx.fox.MantleFrontUp;
            }
        }

        private bool turnStepArmed, turnStepping;

        private bool hindDriving;
        private bool gatherHolding;
        private float heldBendSign = 1f;

        public FoxLimb(FoxGraphics owner, BodyChunk connectionChunk, int num, float rad,
                       float sfFric, float aFric, float huntSpeed, float quickness,
                       FoxLimb otherLimbInPair)
            : base(owner, connectionChunk, num, rad, sfFric, aFric, huntSpeed, quickness)
        {
            gfx = owner;
            this.otherLimbInPair = otherLimbInPair;
            mode = Mode.Retracted;
            attachPos = connectionChunk.pos;
            jointDist = 1f;
        }

        public override void Reset(Vector2 resetPoint)
        {
            retract = true;
            reachingForTerrain = false;
            gripCounter = 0;
            bend = BackwardSign();
            lastBend = bend;
            swing = 0f;
            lastSwing = 0f;
            plantCommitted = false;
            commitTicks = 0;
            plantedTicks = 0;
            swingSeeded = false;
            swingProgress = 0f;
            stepStance = false;
            swingTicks = 0;
            swingPhase = 0f;
            lastSwingPhase = 0f;
            trulyPlanted = false;
            trueStanceTicks = 0;
            pendingReleaseWhy = REL_NONE;
            for (int r = 0; r < releaseCounts.Length; r++) releaseCounts[r] = 0;
            stepAirborne = false;
            stepLiftTick = 0;
            stepLiftY = resetPoint.y;
            stepMaxY = resetPoint.y;
            gaitHoldOdo = -1f;
            swingTravel = 0f;
            swingTravelSeeded = false;
            swingStartOdo = (gfx != null) ? gfx.GaitOdometer : 0f;
            lastStrideRef = 0f;
            swingTargetFrozen = false;
            frozenTarget = resetPoint;
            slipPath = 0f;
            slipDrift = 0f;
            lastSlipPath = 0f;
            lastSlipDrift = 0f;
            gripAnchor = resetPoint;
            lastTargetAbove = 0f;
            groundReach = 0f;
            standDrop = 0f;
            halfSweep = 0f;
            base.Reset(resetPoint);
        }

        public override void Update()
        {
            FoxBreedParams p = gfx.fox.P;

            poleWasOn = OnPole;
            OnPole = false;
            OnWall = false;

            lastSwingPhase = swingPhase;
            pendingReleaseWhy = REL_NONE;

            huntSpeed = p.LimbSpeed;
            quickness = Mathf.Clamp01(p.LimbQuickness);

            float crouch = Mathf.Clamp01(gfx.fox.StalkCrouch);
            float crouchFac = Mathf.Lerp(1f, p.StalkCrouchLimbFac, crouch);

            float squeezeFac = Mathf.Lerp(1f, Mathf.Clamp(p.SqueezeLimbFac, 0.05f, 1f),
                                          gfx.fox.DrawSqueezeAmount);

            float layFac = Mathf.Lerp(1f, Mathf.Clamp(p.DeathBehindLayLimbFac, 0.05f, 1f),
                                      gfx.fox.DeathLayAmount);

            jointDist = 25f * ((p.BodySizeFac + 1f) / 2f) * Mathf.Max(0.05f, p.LimbLengthFac)
                        * crouchFac * squeezeFac * layFac;

            float floorFold = p.StepReachFloorFollowsFold ? (squeezeFac * layFac) : 1f;
            float clampFac = Mathf.Clamp(p.StepReachClampFac, 0.1f, 2f);
            groundReach = (gfx != null) ? gfx.LegGroundReach(limbNumber) * crouchFac * floorFold : 0f;
            if (groundReach > 0f)
            {
                float need = (groundReach + REACH_GROUND_MARGIN) / clampFac + 1f;
                if (jointDist < need)
                {
                    jointDist = need;
                }
            }

            float maxReachNow = Mathf.Max(2f, (jointDist - 1f) * clampFac);
            standDrop = (groundReach > 0f)
                ? Mathf.Clamp(groundReach - rad, 1f, maxReachNow - 1f)
                : 0f;
            halfSweep = (groundReach > 0f)
                ? Mathf.Sqrt(Mathf.Max(4f, maxReachNow * maxReachNow - standDrop * standDrop))
                : jointDist;

            bool hindDrive = p.PounceHindPlant && limbNumber >= 2
                             && gfx.fox.PounceFlight > 0f
                             && gfx.fox.PounceTicks <= Math.Max(0, p.PounceHindReleaseTicks);
            if (hindDrive != hindDriving)
            {
                hindDriving = hindDrive;
            }

            if (gfx.fox.PounceFlight > 0f && !gfx.fox.Grounded && !hindDrive)
            {
                mode = Mode.Dangle;
                vel.y -= 0.9f;
                retract = false;
                gripCounter = 0;
                reachingForTerrain = false;
                pendingReleaseWhy = REL_DANGLE;
                ClearPlant();
                DangleToward();
                FinishUpdate();
                return;
            }

            if (!gfx.fox.Consious || gfx.fox.EngineOwnsBody)
            {
                mode = Mode.Dangle;
                vel.y -= 0.9f;
                retract = false;
                gripCounter = 0;
                reachingForTerrain = false;
                pendingReleaseWhy = REL_DANGLE;
                ClearPlant();
                DangleToward();
                FinishUpdate();
                return;
            }

            if (gfx.fox.PoleHeld)
            {
                pendingReleaseWhy = REL_POLE;
                UpdateOnPole(p);
                return;
            }

            if (gfx.fox.WallHeld)
            {
                pendingReleaseWhy = REL_WALL;
                UpdateOnWall(p);
                return;
            }

            float turnProg = gfx.fox.TurnProgress;
            if (turnProg <= 0f)
            {
                turnStepArmed = false;
            }
            else if (p.PartsTurnStep && !turnStepArmed && turnProg >= TurnStepAt(p))
            {
                turnStepArmed = true;
                turnStepping = true;
                reachingForTerrain = false;
                retract = false;
                extraLongStep = false;
                mode = Mode.HuntAbsolutePosition;
                gripCounter = 0;
                pendingReleaseWhy = REL_TURN;
                ClearPlant();
            }
            if (turnStepping && ContactTicks(p) >= 1)
            {
                turnStepping = false;
            }

            if (mode == Mode.HuntAbsolutePosition)
            {
                huntSpeed = PawSpeed(p, 1f);
            }

            Vector2 axis = gfx.BodyAxis;
            if (connection.index == 2 && p.StepHindAxisFlip) axis *= -1f;
            axis = Vector2.Lerp(axis, Fox.Dir(attachPos, gfx.LimbsAimFor), 0.4f).normalized;

            float behind = DistanceToPerpendicular(pos, attachPos, axis);

            float stepBand = Mathf.Lerp(-0.5f, 0.5f, Mathf.Clamp01(p.StepLength));

            float triggerBehind = halfSweep * -stepBand;

            float stanceSweep = halfSweep * Mathf.Max(0f, p.StepBehindTrigger + stepBand);
            lastStrideRef = stanceSweep + Mathf.Max(0f, swingTravel);

            if (stepStance)
            {
                mode = Mode.HuntAbsolutePosition;
                reachingForTerrain = true;
                swingTicks = 0;
                swingPhase = 0f;
                if (p.StepPlantWorldLock) absoluteHuntPos = plantTarget;
                huntSpeed = PawSpeed(p, 1f);
                plantedTicks++;
                commitTicks = 0;

                bool mayRelease = plantedTicks > Mathf.Max(0, p.StepMinGrip);
                bool fellBehind = p.StepBehindTrigger > 0f && behind > halfSweep * p.StepBehindTrigger;
                bool overExtended = !Custom.DistLess(pos, attachPos, jointDist - 1f)
                                    && behind > jointDist * -0.5f * (Mathf.Clamp01(p.StepLength) + 0.1f);
                bool outOfReach = !Custom.DistLess(plantTarget, attachPos, jointDist);
                bool travelling = p.LimbSweepBodySpeedGate <= 0f
                                  || gfx.GaitSpeed > p.LimbSweepBodySpeedGate;
                bool sweep = mayRelease && gfx.fox.runSpeed > p.LimbSweepSpeedGate
                             && travelling && p.SmoothenLegMovement
                             && gfx.EveryLimbGrippedAtLeast(this, p.LimbSweepMinGrip);

                bool ringHeld = gaitHoldOdo >= 0f && gfx.GaitOdometer < gaitHoldOdo;
                bool beat = fellBehind && !ringHeld;

                bool gatherHold = p.PounceHindPlant && limbNumber >= 2
                                  && gfx.fox.PounceGather > 0f && gfx.fox.PounceFlight <= 0f;
                if (gatherHold && (sweep || fellBehind || overExtended) && !outOfReach)
                {
                    if (!gatherHolding)
                    {
                        gatherHolding = true;
                    }
                }
                else if (outOfReach || sweep || (mayRelease && (beat || overExtended)))
                {
                    gatherHolding = false;
                    pendingReleaseWhy = (mayRelease && beat) ? REL_BEAT
                                      : (mayRelease && overExtended) ? REL_OVER_EXTENDED
                                      : sweep ? REL_SWEEP : REL_OUT_OF_REACH;
                    releaseCounts[pendingReleaseWhy]++;
                    extraLongStep = gaitPartner != null && p.SmoothenLegMovement
                                    && gaitPartner.ContactTicks(p) < 1;
                    ArmSuccessorHold();
                    EnterSwing();
                }
            }
            else if (!reachingForTerrain || !plantCommitted)
            {
                mode = Mode.HuntAbsolutePosition;
                plantCommitted = false;
                commitTicks = 0;
                plantedTicks = 0;
                swingTicks++;
                if (!swingSeeded) { swingStartBehind = behind; swingSeeded = true; swingProgress = 0f; }
                float spd = Mathf.Clamp01(connection.vel.magnitude / 6f);
                float lead = Mathf.Min(halfSweep,
                                       halfSweep * Mathf.Max(0f, p.StepLeadFac + p.StepLeadSpeedFac * spd));
                Vector2 anchor = (groundReach > 0f)
                    ? attachPos - gfx.BodyUp * (standDrop * (1f - Mathf.Clamp01(p.LiftFeet)))
                    : Vector2.Lerp(pos, attachPos, Mathf.Clamp01(p.LiftFeet));
                absoluteHuntPos = anchor + axis * lead;
                float sweepLen = swingStartBehind + lead;
                float minSpan = jointDist * 0.25f;
                float span = (sweepLen >= 0f) ? Mathf.Max(minSpan, sweepLen) : Mathf.Min(-minSpan, sweepLen);
                float frac = Mathf.Max(swingProgress, Mathf.Clamp01((swingStartBehind - behind) / span));
                swingProgress = frac;
                if (swingTargetFrozen) swingPhase = Mathf.Lerp(swingPhase, 1f, 0.35f);
                else swingPhase = frac;
                float asym = Mathf.Clamp01(p.StepArcAsym);
                float r = Mathf.Clamp(p.StepArcPeak, 0.05f, 0.95f);
                float u = Mathf.Clamp01((frac < r) ? (frac / r) : ((1f - frac) / (1f - r)));
                float shaped = u * u * (3f - 2f * u);
                float arc = Mathf.Lerp(Mathf.Sin(frac * Mathf.PI), shaped, asym);
                absoluteHuntPos += gfx.BodyUp * (p.StepArcLift * arc);
                absoluteHuntPos = ClampFootTarget(p, absoluteHuntPos, "swing");

                float reachNow = Vector2.Distance(pos, attachPos);
                bool atReachLimit = reachNow > (jointDist - 1f) * 0.85f;
                int minTicks = Mathf.Clamp(p.StepSwingMinTicks, 0,
                                           Mathf.Max(0, Mathf.Max(1, p.StepSwingMaxTicks) - 2));
                bool minHeld = swingTicks < minTicks && !atReachLimit;

                float freezeAt = p.StepTargetFreezeFrac;
                if (swingTargetFrozen)
                {
                    absoluteHuntPos = frozenTarget;
                }
                else if (freezeAt > 0f && freezeAt < 1f && frac >= freezeAt && !minHeld && !extraLongStep)
                {
                    swingTargetFrozen = true;
                    frozenTarget = absoluteHuntPos;
                    reachingForTerrain = true;
                }
                huntSpeed = PawSpeed(p, p.StepSwingSpeedFac);

                if (extraLongStep)
                {
                    if (gaitPartner == null || gaitPartner.ContactTicks(p) > p.LimbGripDelay)
                    {
                        extraLongStep = false;
                    }
                }

                if (!extraLongStep && behind < triggerBehind)
                {
                    reachingForTerrain = true;
                }

                if (reachingForTerrain && !plantCommitted)
                {
                    Vector2 aheadPlant;
                    if (TryPlantAhead(p, axis, lead, behind, out aheadPlant))
                    {
                        CommitPlant(aheadPlant, "ahead");
                    }
                }

                if (reachingForTerrain && !plantCommitted)
                {
                    Vector2 search = axis;
                    Vector2 perp = new Vector2(-axis.y, axis.x);
                    search += perp * ((6f - 12f * (limbNumber % 2)) * 0.2f);
                    search.y -= 0.3f * p.FeetDown;
                    search.x += ((limbNumber % 2 == 0) ? -1f : 1f) * p.LegPairDisplacement * flip;

                    float landing = Mathf.Clamp01(gfx.fox.LandingPose);
                    if (landing > 0f)
                    {
                        search.y -= 0.6f * landing;
                        search = search.normalized;
                    }

                    Vector2 gripGoal = attachPos + search * 50f
                                       + new Vector2(0f, -p.LandingPoseReach * landing);
                    Vector2 gripFrom = attachPos;
                    if (MantleReaching)
                    {
                        gripGoal = gfx.fox.MantleGrip;
                        gripFrom = gfx.fox.MantleGrip;
                    }
                    FindGrip(gfx.fox.room, attachPos, gripFrom, jointDist - 1f,
                             gripGoal, 2, 2, gfx.fox.IsWallClimber);
                }

                if (!plantCommitted && p.StepSwingMaxTicks > 0
                    && swingTicks >= Mathf.Max(1, p.StepSwingMaxTicks))
                {
                    Vector2 deadlinePlant;
                    bool deadlineAhead = TryPlantAhead(p, axis, lead, behind, out deadlinePlant);
                    if (deadlineAhead)
                    {
                        CommitPlant(deadlinePlant, "ahead_deadline");
                    }
                    else CommitPlant(FallbackPlantPoint(p), "fallback");
                }
            }
            else
            {
                mode = Mode.HuntAbsolutePosition;
                absoluteHuntPos = plantTarget;
                huntSpeed = PawSpeed(p, p.StepSwingSpeedFac);
                commitTicks++;
                plantedTicks = 0;
                swingPhase = Mathf.Lerp(swingPhase, 1f, 0.35f);

                if (!Custom.DistLess(plantTarget, attachPos, jointDist))
                {
                    EnterSwing();
                }
                else if (reachedSnapPosition || PlantedNow(p)
                         || commitTicks >= Mathf.Max(1, p.StepPlantCommitTicks))
                {
                    stepStance = true;
                    plantedTicks = 0;
                    swingTicks = 0;
                    OnPlanted();
                }
            }

            if (retract && reachedSnapPosition)
            {
                mode = Mode.HuntAbsolutePosition;
                retract = false;
                EnterSwing();
            }

            if (otherLimbInPair != null && Custom.DistLess(pos, otherLimbInPair.pos, rad * 3f))
            {
                float d = Vector2.Distance(pos, otherLimbInPair.pos);
                Vector2 push = Fox.Dir(pos, otherLimbInPair.pos) * ((rad * 3f - d) * 0.5f);
                vel -= push;
                otherLimbInPair.vel += push;
            }

            flip = Mathf.Lerp(flip, (behind < 0f) ? 1f : -1f, 0.3f);

            lastBend = bend;
            bend = Mathf.Lerp(bend, BackwardSign(), Mathf.Clamp(p.LimbBendSmooth, 0.01f, 1f));

            FinishUpdate();
        }

        internal bool OnPole { get; private set; }
        private bool poleWasOn;
        internal bool PoleReaching { get; private set; }
        internal bool PoleTargetClamped { get; private set; }

        private float poleLeadEff, poleAmpEff;
        private float polePlanLeadKey, polePlanAmpKey;
        private bool polePlanSeeded;
        internal bool PoleWindowValid { get; private set; }

        private const float POLE_PLAN_MARGIN_PX = 1f;
        private const float POLE_LIMB_REACH_SLACK_PX = 0.5f;

        internal int PoleStrokeNo { get; private set; }
        internal float PoleStrokeAmpMul { get; private set; } = 1f;
        internal float PoleStrokeLeadJitter { get; private set; }
        private float poleStrokeU1, poleStrokeU2;

        private float PoleStrokeReachFrac(FoxBreedParams p, float r)
        {
            return Mathf.Clamp(r * (1f + 0.5f * Mathf.Max(0f, p.PoleStrokeJitter) * poleStrokeU1), 0.05f, 0.9f);
        }

        private static float PoleStrokeUnit(int seed, int leg, int strokeNo, int salt)
        {
            int x = unchecked(seed * 31 + leg * 977 + salt * 7919);
            int y = unchecked(strokeNo * 131 + salt);
            return (float)(LabHash2(x, y) * 2.0 - 1.0);
        }

        private static double LabHash2(int x, int y)
        {
            int h = JsToInt32((double)x * 374761393.0 + (double)y * 668265263.0);
            h ^= (int)((uint)h >> 13);
            h = JsToInt32((double)h * 1274126177.0);
            h ^= (int)((uint)h >> 16);
            return (uint)h / 4294967296.0;
        }

        private static int JsToInt32(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return 0;
            double t = Math.Truncate(d);
            double m = t - 4294967296.0 * Math.Floor(t / 4294967296.0);
            return unchecked((int)(uint)m);
        }

        private void UpdateOnPole(FoxBreedParams p)
        {
            Fox fox = gfx.fox;
            bool front = limbNumber < 2;
            bool wasReaching = PoleReaching;
            OnPole = true;

            float ph = fox.PolePhase
                       + ((limbNumber % 2 == 1) ? Mathf.Clamp01(p.PolePawPhaseOffset) : 0f)
                       + (front ? 0f : Mathf.Clamp01(p.PoleHindPhaseOffset));
            ph -= Mathf.Floor(ph);

            float r = Mathf.Clamp(p.PoleReachFrac, 0.05f, 0.9f);
            float rk = PoleStrokeReachFrac(p, r);
            bool reaching = ph < rk;
            if (!poleWasOn || (reaching && !wasReaching))
            {
                PoleStrokeNo++;
                int seed = fox.abstractCreature.ID.number;
                poleStrokeU1 = PoleStrokeUnit(seed, limbNumber, PoleStrokeNo, 1);
                poleStrokeU2 = PoleStrokeUnit(seed, limbNumber, PoleStrokeNo, 2);
                rk = PoleStrokeReachFrac(p, r);
                reaching = ph < rk;
            }
            float stroke;
            float reachU = 0f;
            if (reaching)
            {
                reachU = ph / rk;
                float eased = reachU * reachU * (3f - 2f * reachU);
                stroke = -1f + 2f * eased;
            }
            else
            {
                stroke = 1f - 2f * ((ph - rk) / (1f - rk));
            }
            PoleReaching = reaching;

            PoleStrokeAmpMul = (1f - rk) / (1f - r);
            PoleStrokeLeadJitter = Mathf.Max(0f, p.PoleStrokeJitterPx) * poleStrokeU2;
            float amp = Mathf.Max(0f, front ? p.PoleFrontAmpPx : p.PoleHindAmpPx) * PoleStrokeAmpMul;
            float lead = (front ? p.PoleFrontLeadPx : p.PoleHindLeadPx) + PoleStrokeLeadJitter;

            float bodySide = gfx.PoleBodySide;
            float sideSign = ((limbNumber % 2 == 0) ? -1f : 1f) * bodySide;
            Vector2 origin = new Vector2(fox.PoleAxisX + sideSign * p.PolePawSidePx, attachPos.y);
            Vector2 along = new Vector2(0f, fox.PoleHeadSign);

            float lo, hi;
            PoleWindowValid = gfx.PoleStrokeWindow(limbNumber, attachPos, origin, along, out lo, out hi);
            bool replan = !poleWasOn || !polePlanSeeded || (reaching && !wasReaching)
                          || lead != polePlanLeadKey || amp != polePlanAmpKey;
            if (PoleWindowValid)
            {
                float half = (hi - lo) * 0.5f;
                if (replan)
                {
                    poleAmpEff = Mathf.Min(amp, half);
                    float margin = Mathf.Min(POLE_PLAN_MARGIN_PX, half - poleAmpEff);
                    poleLeadEff = Mathf.Clamp(lead, lo + poleAmpEff + margin, hi - poleAmpEff - margin);
                }
                else
                {
                    poleAmpEff = Mathf.Min(poleAmpEff, half);
                    poleLeadEff = Mathf.Clamp(poleLeadEff, lo + poleAmpEff, hi - poleAmpEff);
                }
                polePlanSeeded = true;
                polePlanLeadKey = lead;
                polePlanAmpKey = amp;
            }
            else
            {
                poleLeadEff = lead;
                poleAmpEff = amp;
                polePlanSeeded = false;
            }
            Vector2 target = origin + along * (poleLeadEff + poleAmpEff * stroke);

            float chase = PawSpeed(p, p.PolePawSpeedFac);

            float clampFac = Mathf.Clamp(p.StepReachClampFac, 0.1f, 2f);
            if (PoleWindowValid)
            {
                float far = Mathf.Max(
                    Vector2.Distance(attachPos, origin + along * (poleLeadEff - poleAmpEff)),
                    Vector2.Distance(attachPos, origin + along * (poleLeadEff + poleAmpEff)));
                float need = (far + POLE_LIMB_REACH_SLACK_PX) / clampFac + 1f;
                if (jointDist < need) jointDist = need;
            }

            float maxReach = Mathf.Max(2f, (jointDist - 1f) * clampFac);
            Vector2 rel = target - attachPos;
            float d = rel.magnitude;
            PoleTargetClamped = d > maxReach && d > 0.01f;
            if (PoleTargetClamped) target = attachPos + rel * (maxReach / d);

            mode = Mode.HuntAbsolutePosition;
            absoluteHuntPos = target;
            huntSpeed = chase;
            retract = false;
            extraLongStep = false;
            turnStepping = false;
            turnStepArmed = false;
            ClearPlant();
            gaitHoldOdo = -1f;
            reachingForTerrain = !reaching;
            swingPhase = reaching ? reachU : 0f;

            Vector2 axis = gfx.BodyAxis;
            float behind = DistanceToPerpendicular(pos, attachPos, axis);

            flip = Mathf.Lerp(flip, (behind < 0f) ? 1f : -1f, 0.3f);
            lastBend = bend;
            bend = Mathf.Lerp(bend, BackwardSign(), Mathf.Clamp(p.LimbBendSmooth, 0.01f, 1f));

            FinishUpdate();
        }

        internal bool OnWall { get; private set; }
        internal bool WallTargetClamped { get; private set; }

        private void UpdateOnWall(FoxBreedParams p)
        {
            Fox fox = gfx.fox;
            bool front = limbNumber < 2;
            OnWall = true;

            float ph = fox.WallPhase
                       + ((limbNumber % 2 == 1) ? Mathf.Clamp01(p.PolePawPhaseOffset) : 0f)
                       + (front ? 0f : Mathf.Clamp01(p.PoleHindPhaseOffset));
            ph -= Mathf.Floor(ph);

            float r = Mathf.Clamp(p.PoleReachFrac, 0.05f, 0.9f);
            float stroke;
            float reachU = 0f;
            bool reaching = ph < r;
            if (reaching)
            {
                reachU = ph / r;
                float eased = reachU * reachU * (3f - 2f * reachU);
                stroke = -1f + 2f * eased;
            }
            else
            {
                stroke = 1f - 2f * ((ph - r) / (1f - r));
            }

            float amp = Mathf.Max(0f, front ? p.PoleFrontAmpPx : p.PoleHindAmpPx);
            float lead = front ? p.PoleFrontLeadPx : p.PoleHindLeadPx;
            float side = fox.WallFaceSide < 0f ? -1f : 1f;
            Vector2 target = new Vector2(fox.WallFaceX - side * p.WallPawFacePx,
                                         attachPos.y + fox.WallHeadSign * (lead + amp * stroke));

            float chase = PawSpeed(p, p.PolePawSpeedFac);

            float clampFac = Mathf.Clamp(p.StepReachClampFac, 0.1f, 2f);
            float maxReach = Mathf.Max(2f, (jointDist - 1f) * clampFac);
            Vector2 rel = target - attachPos;
            float d = rel.magnitude;
            WallTargetClamped = d > maxReach && d > 0.01f;
            if (WallTargetClamped) target = attachPos + rel * (maxReach / d);

            mode = Mode.HuntAbsolutePosition;
            absoluteHuntPos = target;
            huntSpeed = chase;
            retract = false;
            extraLongStep = false;
            turnStepping = false;
            turnStepArmed = false;
            ClearPlant();
            gaitHoldOdo = -1f;
            reachingForTerrain = !reaching;
            swingPhase = reaching ? reachU : 0f;

            Vector2 axis = gfx.BodyAxis;
            float behind = DistanceToPerpendicular(pos, attachPos, axis);

            flip = Mathf.Lerp(flip, (behind < 0f) ? 1f : -1f, 0.3f);
            lastBend = bend;
            bend = Mathf.Lerp(bend, BackwardSign(), Mathf.Clamp(p.LimbBendSmooth, 0.01f, 1f));

            FinishUpdate();
        }

        private float PawSpeed(FoxBreedParams p, float fac)
        {
            bool capped;
            float own = PawOwnSpeed(p, fac, out capped);
            return own + connection.vel.magnitude;
        }

        private float PawOwnSpeed(FoxBreedParams p, float fac, out bool capped)
        {
            float own = Mathf.Max(0.1f, p.LimbSpeed) * Mathf.Max(0.1f, fac);
            float cap = Mathf.Max(2f, (jointDist - 1f) * Mathf.Clamp(p.StepReachClampFac, 0.1f, 2f)
                                      * PAW_MAX_STEP_FRAC);
            capped = own > cap;
            return capped ? cap : own;
        }

        private float TurnStepAt(FoxBreedParams p)
        {
            float at = (limbNumber < 2) ? p.PartsTurnStepFrontAt : p.PartsTurnStepHindAt;
            if (limbNumber % 2 == 1) at += p.PartsTurnStepStagger;
            return Mathf.Clamp01(at);
        }

        public void SwapFootWith(FoxLimb other)
        {
            if (other == null || other == this) return;
            Vector2 v;
            v = pos; pos = other.pos; other.pos = v;
            v = lastPos; lastPos = other.lastPos; other.lastPos = v;
            v = vel; vel = other.vel; other.vel = v;
            v = absoluteHuntPos; absoluteHuntPos = other.absoluteHuntPos; other.absoluteHuntPos = v;
            Mode m = mode; mode = other.mode; other.mode = m;
            bool b;
            b = reachingForTerrain; reachingForTerrain = other.reachingForTerrain; other.reachingForTerrain = b;
            b = retract; retract = other.retract; other.retract = b;
            b = extraLongStep; extraLongStep = other.extraLongStep; other.extraLongStep = b;
            b = turnStepping; turnStepping = other.turnStepping; other.turnStepping = b;
            b = plantCommitted; plantCommitted = other.plantCommitted; other.plantCommitted = b;
            b = swingSeeded; swingSeeded = other.swingSeeded; other.swingSeeded = b;
            b = stepStance; stepStance = other.stepStance; other.stepStance = b;
            b = swingTargetFrozen; swingTargetFrozen = other.swingTargetFrozen; other.swingTargetFrozen = b;
            int st = swingTicks; swingTicks = other.swingTicks; other.swingTicks = st;
            Vector2 vp;
            vp = frozenTarget; frozenTarget = other.frozenTarget; other.frozenTarget = vp;
            vp = plantTarget; plantTarget = other.plantTarget; other.plantTarget = vp;
            vp = gripAnchor; gripAnchor = other.gripAnchor; other.gripAnchor = vp;
            int n = gripCounter; gripCounter = other.gripCounter; other.gripCounter = n;
            n = plantedTicks; plantedTicks = other.plantedTicks; other.plantedTicks = n;
            float f;
            f = slipPath; slipPath = other.slipPath; other.slipPath = f;
            f = slipDrift; slipDrift = other.slipDrift; other.slipDrift = f;
            f = lastSlipPath; lastSlipPath = other.lastSlipPath; other.lastSlipPath = f;
            f = lastSlipDrift; lastSlipDrift = other.lastSlipDrift; other.lastSlipDrift = f;
            f = swingStartBehind; swingStartBehind = other.swingStartBehind; other.swingStartBehind = f;
            f = swingStartOdo; swingStartOdo = other.swingStartOdo; other.swingStartOdo = f;
            f = swingPhase; swingPhase = other.swingPhase; other.swingPhase = f;
            f = lastSwingPhase; lastSwingPhase = other.lastSwingPhase; other.lastSwingPhase = f;
            f = swingProgress; swingProgress = other.swingProgress; other.swingProgress = f;
            f = swing; swing = other.swing; other.swing = f;
            f = lastSwing; lastSwing = other.lastSwing; other.lastSwing = f;
            f = flip; flip = other.flip; other.flip = f;
            b = trulyPlanted; trulyPlanted = other.trulyPlanted; other.trulyPlanted = b;
            n = trueStanceTicks; trueStanceTicks = other.trueStanceTicks; other.trueStanceTicks = n;
            b = stepAirborne; stepAirborne = other.stepAirborne; other.stepAirborne = b;
            n = stepLiftTick; stepLiftTick = other.stepLiftTick; other.stepLiftTick = n;
            f = stepLiftY; stepLiftY = other.stepLiftY; other.stepLiftY = f;
            f = stepMaxY; stepMaxY = other.stepMaxY; other.stepMaxY = f;
        }

        public void ReseedBend()
        {
            bend = BackwardSign();
            lastBend = bend;
        }

        private float BackwardSign()
        {
            if (gfx == null) return 1f;
            Vector2 axis = gfx.BodyAxis;

            FoxBreedParams bp = gfx.fox != null ? gfx.fox.P : null;
            float band = (bp != null) ? Mathf.Max(0f, bp.LimbBendFlipDeadband) : 0f;
            if (axis.x > band) heldBendSign = 1f;
            else if (axis.x < -band) heldBendSign = -1f;
            return heldBendSign;
        }

        private void DangleToward()
        {
            Vector2 axis = gfx.BodyAxis;
            Vector2 perp = new Vector2(-axis.y, axis.x);
            Vector2 target = attachPos + perp * (30f - 60f * (limbNumber % 2)) * 0.2f + new Vector2(0f, -10f);
            vel += Fox.Dir(pos, target) * 0.6f;
        }

        private void FinishUpdate()
        {
            base.Update();

            ConnectToPoint(attachPos, jointDist, push: false, 0f, connection.vel, 0f, 0f);

            bool holding = mode == Mode.HuntAbsolutePosition && reachingForTerrain
                           && (reachedSnapPosition || (OverLappingHuntPos && terrainContact));
            if (holding)
            {
                if (gripCounter == 0)
                {
                    gripAnchor = pos;
                    slipPath = 0f;
                    slipDrift = 0f;
                }
                else
                {
                    slipPath += Vector2.Distance(pos, lastPos);
                    slipDrift = Vector2.Distance(pos, gripAnchor);
                }
                gripCounter++;
            }
            else
            {
                if (gripCounter > 0)
                {
                    lastSlipPath = slipPath;
                    lastSlipDrift = slipDrift;
                }
                gripCounter = 0;
            }

            bool wasDown = trulyPlanted;
            int downFor = trueStanceTicks;
            bool locked = stepStance || (plantCommitted && reachingForTerrain);
            trulyPlanted = !OnPole && !OnWall && mode == Mode.HuntAbsolutePosition && locked && OverLappingHuntPos
                           && (terrainContact || PawGroundGap() <= GROUND_CONTACT_SLACK_PX);
            trueStanceTicks = trulyPlanted ? trueStanceTicks + 1 : 0;

            if (trulyPlanted && !wasDown) NoteTouchdown();
            else if (!trulyPlanted && wasDown) NoteLift(downFor);
            else if (stepAirborne && pos.y > stepMaxY) stepMaxY = pos.y;

            lastSwing = swing;
            swing = Mathf.Lerp(swing, holding ? 0f : 1f, 0.25f);
        }

        public override void GrabbedTerrain()
        {
            CommitPlant(ClampFootTarget(gfx.fox.P, absoluteHuntPos, "grip"), "grip");
        }

        private void CommitPlant(Vector2 target, string site)
        {
            reachingForTerrain = true;
            absoluteHuntPos = target;
            plantTarget = target;
            plantCommitted = true;
            commitTicks = 0;
        }

        private Vector2 FallbackPlantPoint(FoxBreedParams p)
        {
            Vector2 up = gfx.BodyUp;
            float reach = ProbeReach();
            Room rm = gfx.fox.room;
            if (rm != null)
            {
                bool plainUnused;
                float d = FirstSolidBelow(rm, attachPos, up, reach, out plainUnused);
                if (d > 2f)
                {
                    return ClampFootTarget(p, attachPos - up * Mathf.Clamp(d - 1f - rad, 2f, reach),
                                           "fallback");
                }
            }
            float rest = (standDrop > 1f) ? standDrop : reach * 0.8f;
            return ClampFootTarget(p, attachPos - up * Mathf.Clamp(rest, 2f, reach), "fallback");
        }

        private float ProbeReach()
        {
            float jd = jointDist;
            if (float.IsNaN(jd)) jd = PROBE_MAX_PX;
            return Mathf.Min(Mathf.Max(2f, jd - 1f), PROBE_MAX_PX);
        }

        private static float FirstSolidBelow(Room rm, Vector2 from, Vector2 up, float reach, out bool plain)
        {
            plain = true;
            for (float d = 1f; d <= reach; d += 1f)
            {
                Room.Tile t = rm.GetTile(from - up * d);
                if (t.Solid) return d;
                if (t.Terrain != Room.Tile.TerrainType.Air) plain = false;
            }
            return -1f;
        }

        private bool TryPlantAhead(FoxBreedParams p, Vector2 axis, float lead, float behind, out Vector2 plant)
        {
            plant = attachPos;
            Fox fox = gfx.fox;

            if (!p.StepPlantAhead) return RefuseAhead(AHEAD_KEY_OFF);
            if (groundReach <= 0f || standDrop <= 1f) return RefuseAhead(AHEAD_NO_GEOMETRY);
            if (fox.Move != null && fox.Move.AnyRunning) return RefuseAhead(AHEAD_ACTION);
            if (turnStepping || fox.TurnProgress > 0f) return RefuseAhead(AHEAD_TURN_STEP);
            if (fox.PounceGather > 0f || fox.PounceFlight > 0f || fox.PounceLand > 0f
                || fox.LandingPose > 0f || fox.EnterGather > 0f) return RefuseAhead(AHEAD_POSE);
            if (fox.AirTicks >= Math.Max(1, p.LandMinAirTicks)) return RefuseAhead(AHEAD_AIRBORNE);
            if (fox.CorridorChunkCount() > 0 || fox.DrawSqueezeAmount > 0f || fox.RideCorridorAmount > 0f)
                return RefuseAhead(AHEAD_CORRIDOR);
            if (fox.IsWallClimber) return RefuseAhead(AHEAD_CLIMBER);
            if (fox.Swimming) return RefuseAhead(AHEAD_WATER);

            Vector2 up = gfx.BodyUp;
            Vector2 fwd = axis - up * Vector2.Dot(axis, up);
            fwd = (fwd.sqrMagnitude > 0.0001f) ? fwd.normalized : gfx.BodyAxis;
            float v = Vector2.Dot(gfx.GaitVelocity, fwd);
            float minV = Mathf.Max(0f, p.StepPlantAheadMinSpeed);
            if (v <= 0f || v < minV) return RefuseAhead(AHEAD_SLOW);
            float fullV = Mathf.Max(minV, p.StepPlantAheadFullSpeed);
            float ramp = (fullV > minV) ? Mathf.Clamp01((v - minV) / (fullV - minV)) : 1f;

            float leadGround = lead * Mathf.Max(0f, Vector2.Dot(axis, fwd));
            bool cappedUnused;
            float own = PawOwnSpeed(p, p.StepSwingSpeedFac, out cappedUnused);
            float travel = Mathf.Clamp01(p.StepPlantAheadTravelFac) * v
                           * Mathf.Max(0f, leadGround + behind) / Mathf.Max(0.5f, own);
            float x = ramp * (leadGround + travel);

            Room rm = fox.room;
            if (rm == null) return RefuseAhead(AHEAD_NO_GROUND_ROOT);
            if (rm.terrain != null) return RefuseAhead(AHEAD_NOT_PLAIN);
            float reach = ProbeReach();
            bool plain;
            float dRoot = FirstSolidBelow(rm, attachPos, up, reach, out plain);
            if (!plain) return RefuseAhead(AHEAD_NOT_PLAIN);
            if (dRoot < 0f) return RefuseAhead(AHEAD_NO_GROUND_ROOT);
            if (dRoot <= 2f) return RefuseAhead(AHEAD_BLOCKED);

            float maxReach = Mathf.Max(2f, (jointDist - 1f) * Mathf.Clamp(p.StepReachClampFac, 0.1f, 2f));
            float drop = Mathf.Clamp(dRoot - 1f - rad, 2f, reach);
            x = Mathf.Clamp(x, 0f, AheadChord(maxReach, drop));

            float dAhead = FirstSolidBelow(rm, attachPos + fwd * x, up, reach, out plain);
            if (!plain) return RefuseAhead(AHEAD_NOT_PLAIN);
            if (dAhead < 0f) return RefuseAhead(AHEAD_NO_GROUND_AHEAD);
            if (dAhead <= 2f) return RefuseAhead(AHEAD_BLOCKED);
            if (Mathf.Abs(dAhead - dRoot) > PLANT_LEVEL_TOL_PX) return RefuseAhead(AHEAD_STEP);

            float dropAhead = Mathf.Clamp(dAhead - 1f - rad, 2f, reach);
            x = Mathf.Min(x, AheadChord(maxReach, dropAhead));
            Vector2 candidate = ClampFootTarget(p, attachPos + fwd * x - up * dropAhead, "ahead");
            if (!Custom.DistLess(candidate, attachPos, jointDist)) return RefuseAhead(AHEAD_OUT_OF_REACH);

            plant = candidate;
            return true;
        }

        private static float AheadChord(float maxReach, float drop)
        {
            return Mathf.Max(0f, Mathf.Sqrt(Mathf.Max(0f, maxReach * maxReach - drop * drop))
                                 - PLANT_CHORD_MARGIN_PX);
        }

        private bool RefuseAhead(int why)
        {
            return false;
        }

        private float PawGroundGap()
        {
            Room rm = (gfx != null && gfx.fox != null) ? gfx.fox.room : null;
            if (rm == null) return float.PositiveInfinity;
            IntVector2 at = rm.GetTilePosition(pos);
            float best = float.PositiveInfinity;
            for (int dy = 0; dy >= -1; dy--)
            {
                IntVector2 t = new IntVector2(at.x, at.y + dy);
                Room.Tile.TerrainType kind = rm.GetTile(t.x, t.y).Terrain;
                if (kind == Room.Tile.TerrainType.Air || kind == Room.Tile.TerrainType.ShortcutEntrance) continue;
                Vector2 mid = rm.MiddleOfTile(t);
                float gap;
                if (kind == Room.Tile.TerrainType.Solid) gap = (pos.y - rad) - (mid.y + 10f);
                else if (kind == Room.Tile.TerrainType.Floor) gap = pos.y - (mid.y + 10f);
                else
                {
                    Room.SlopeDirection dir = rm.IdentifySlope(t);
                    if (dir == Room.SlopeDirection.UpLeft) gap = (pos.y - rad) - (mid.y - (mid.x - pos.x));
                    else if (dir == Room.SlopeDirection.UpRight) gap = (pos.y - rad) - (mid.y + (mid.x - pos.x));
                    else continue;
                }
                if (gap < best) best = gap;
            }
            return best;
        }

        private void NoteTouchdown()
        {
            stepAirborne = false;
        }

        private void NoteLift(int downFor)
        {
            int why = (pendingReleaseWhy != REL_NONE) ? pendingReleaseWhy : REL_LOST;
            if (why >= REL_TURN) releaseCounts[why]++;
            int tick = gfx.fox.MoveTick;
            stepAirborne = true;
            stepLiftTick = tick;
            stepLiftY = lastPos.y;
            stepMaxY = Mathf.Max(lastPos.y, pos.y);
        }

        private void OnPlanted()
        {
            if (gfx == null) return;
            float odo = gfx.GaitOdometer;
            float w = odo - swingStartOdo;
            if (w > 0.01f)
            {
                swingTravel = swingTravelSeeded ? Mathf.Lerp(swingTravel, w, 0.34f) : w;
                swingTravelSeeded = true;
            }
        }

        private void ArmSuccessorHold()
        {
            if (gfx == null || gaitSuccessor == null || gaitGapFrac <= 0f) return;
            float stride = gaitSuccessor.lastStrideRef;
            if (stride <= 0f) return;
            gaitSuccessor.gaitHoldOdo = gfx.GaitOdometer + gaitGapFrac * stride;
        }

        private void EnterSwing()
        {
            if (gfx != null) swingStartOdo = gfx.GaitOdometer;
            swingTargetFrozen = false;
            stepStance = false;
            reachingForTerrain = false;
            plantCommitted = false;
            commitTicks = 0;
            plantedTicks = 0;
            swingTicks = 0;
            swingSeeded = false;
            swingProgress = 0f;
            swingPhase = 0f;
        }

        private bool PlantedNow(FoxBreedParams p)
        {
            if (OverLappingHuntPos) return true;
            return p.StepPlantWorldLock && plantCommitted && gripCounter > 0
                   && Custom.DistLess(plantTarget, attachPos, jointDist - 1f);
        }

        private Vector2 ClampFootTarget(FoxBreedParams p, Vector2 target, string site)
        {
            Vector2 rel = target - attachPos;
            lastTargetAbove = Vector2.Dot(rel, gfx.BodyUp);
            if (!p.StepTargetClamp) return target;

            bool climber = gfx.fox.IsWallClimber;

            float aboveFac = Mathf.Max(0f, p.StepTargetMaxAboveFac);
            if (MantleReaching) aboveFac = Mathf.Max(aboveFac, Mathf.Max(0f, p.MantleFootAboveFac));
            float maxAbove = aboveFac * jointDist;
            if (!climber && lastTargetAbove > maxAbove)
            {
                target -= gfx.BodyUp * (lastTargetAbove - maxAbove);
                rel = target - attachPos;
            }

            float maxReach = Mathf.Max(2f, (jointDist - 1f) * Mathf.Clamp(p.StepReachClampFac, 0.1f, 2f));
            float d = rel.magnitude;
            if (d > maxReach && d > 0.01f)
            {
                target = attachPos + rel * (maxReach / d);
                rel = target - attachPos;
                d = maxReach;
            }

            if (!MantleReaching && !climber && standDrop > 1f)
            {
                float minReach = Mathf.Min(maxReach - 1f, Mathf.Max(rad + 1f, standDrop * MIN_REACH_FAC));
                if (d < minReach)
                {
                    Vector2 dir = (d > 0.01f) ? (rel / d) : -gfx.BodyUp;
                    float rise = Vector2.Dot(dir, gfx.BodyUp);
                    if (rise > 0f)
                    {
                        dir -= gfx.BodyUp * rise;
                        dir = (dir.sqrMagnitude > 0.0001f) ? dir.normalized : -gfx.BodyUp;
                    }
                    target = attachPos + dir * minReach;
                }
            }

            return target;
        }

        private void ClearPlant()
        {
            plantCommitted = false;
            commitTicks = 0;
            plantedTicks = 0;
            swingSeeded = false;
            swingProgress = 0f;
            stepStance = false;
            swingTicks = 0;
            swingPhase = 0f;
            swingTargetFrozen = false;
            if (gfx != null) swingStartOdo = gfx.GaitOdometer;
        }

        private static float DistanceToPerpendicular(Vector2 point, Vector2 origin, Vector2 axis)
        {
            Vector2 d = point - origin;
            return -(d.x * axis.x + d.y * axis.y);
        }
    }
}
