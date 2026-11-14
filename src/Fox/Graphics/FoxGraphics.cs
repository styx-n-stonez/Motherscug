using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public partial class FoxGraphics : GraphicsModule
    {
        public readonly Fox fox;

        public FoxLimb[] limbs;
        public TailSegment[] tail;
        public GenericBodyPart head;

        public Vector2[,] drawPositions;

        private readonly List<FoxCosmetic> cosmetics = new List<FoxCosmetic>();
        private int extraSprites;
        private int tailSegmentCount;

        private float artRestSpine = 1f;

        private Vector2 artFwd, artUp;

        private float lastArtRot;
        private bool artRotSeeded;
        private float maxDrawRotDelta;
        private float maxDrawPosDelta;
        private Vector2 lastDrawMid;

        private Fox.PounceState lastPounceState = Fox.PounceState.None;

        internal float RideHeight(FoxBreedParams p)
        {
            float h = RideHeightOffPole(p);
            if (p == null || fox == null) return h;
            float poleAmt = fox.PoleAmount;
            if (poleAmt > 0f) h = Mathf.Lerp(h, PoleStandOff(p), poleAmt);
            return h;
        }

        internal float RideHeightOffPole(FoxBreedParams p)
        {
            if (p == null) return 0f;
            float h = p.BodyRideHeight;
            if (fox == null) return h;
            h *= Mathf.Lerp(1f, Mathf.Clamp01(p.BodyRideCorridorFac), fox.RideCorridorAmount);
            h *= Mathf.Lerp(1f, Mathf.Clamp01(p.BodyRideDeathFac), fox.DeathCollapseAmount);
            return h;
        }

        internal float PoleStandOff(FoxBreedParams p)
        {
            if (p == null || fox == null || fox.bodyChunks == null || fox.bodyChunks.Length < 2) return 0f;
            return (p.BodyRideHeight + fox.bodyChunks[1].rad) * Mathf.Clamp01(p.PoleRideFac);
        }

        internal Vector2 RideVector(FoxBreedParams p)
        {
            return BodyUp * RideHeight(p);
        }

        private static float TurnForeshorten(FoxBreedParams p, float depth)
        {
            float k = Mathf.Clamp01(p.BodyTurnForeshorten);
            if (k <= 0f) return 1f;
            return Mathf.Lerp(1f - k, 1f, Mathf.Abs(depth));
        }

        private float headDepth;
        private float lastHeadDepth;

        private float[] tailRad;

        private int frontLegsDown, hindLegsDown;
        private float frontBob;
        private float hindBob;

        public int noGripCounter;

        private Vector2 lookPos;
        private bool lookInitialized;
        private Vector2 bodyAxis = new Vector2(1f, 0f);

        private RoomPalette palette;
        private bool paletteSet;

        private Color[] camoBase;
        private bool[] camoBaseSet;
        private bool camoBlended;

        private Color camoSampledColor;
        private Color camoSampledPick;
        private bool camoSampleSeeded;
        private int camoSampleTick = int.MinValue;
        private int camoSampleCam = -1;
        private int camoSampleRoom = -1;

        private readonly float[] seededJitter = new float[8];

        private bool builtLogged;

        public int SpriteBodyMesh { get { return 0; } }
        public int SpriteLimbsStart { get { return 1; } }
        public int SpriteLimbsEnd { get { return SpriteLimbsStart + limbs.Length * PARTS_LEG_SPRITES; } }
        public int SpriteTail { get { return SpriteLimbsEnd; } }
        public int SpriteHead { get { return SpriteTail + 1; } }
        public int SpriteEye { get { return SpriteHead + 1; } }

        internal const float EYE_INK_TINT_MIN = 0.15f;

        internal static Color InkEyeTint(FoxBreedParams p, Color tint)
        {
            float f = Mathf.Clamp(p.EyeInkTintFloor, EYE_INK_TINT_MIN, 1f);
            return new Color(Mathf.Max(tint.r, f), Mathf.Max(tint.g, f), Mathf.Max(tint.b, f), tint.a);
        }

        private Color EyeAfterCamo(FoxBreedParams p, Color blended)
        {
            Color guarded = InkEyeTint(p, blended);
            return guarded;
        }

        public int SpriteChest { get { return SpriteEye + 1; } }
        public int StartOfExtraSprites { get { return SpriteChest + 1; } }
        public int TotalSprites { get { return StartOfExtraSprites + extraSprites + (tobyFramesActive ? 1 : 0); } }

        public FoxGraphics(Fox fox) : base(fox, true)
        {
            this.fox = fox;
            cullRange = 400f;

            FoxBreedParams p = fox.P;

            Random.State entryState = Random.state;
            Random.InitState(fox.abstractCreature.ID.RandomSeed);
            for (int i = 0; i < seededJitter.Length; i++) seededJitter[i] = Random.value;
            Random.state = entryState;

            drawPositions = new Vector2[fox.bodyChunks.Length, 2];
            for (int i = 0; i < fox.bodyChunks.Length; i++)
            {
                drawPositions[i, 0] = fox.bodyChunks[i].pos;
                drawPositions[i, 1] = fox.bodyChunks[i].pos;
            }

            var parts = new List<BodyPart>();

            limbs = new FoxLimb[4];
            for (int i = 0; i < 4; i++)
            {
                BodyChunk connection = fox.bodyChunks[(i < 2) ? 0 : 2];
                limbs[i] = new FoxLimb(this, connection, i, Mathf.Max(0.5f, p.LimbSize),
                                       0.7f, 0.99f, p.LimbSpeed, Mathf.Clamp01(p.LimbQuickness),
                                       (i % 2 == 1) ? limbs[i - 1] : null);
                parts.Add(limbs[i]);
            }

            artRestSpine = Mathf.Max(1f, 2f * 17f * p.BodyLengthFac * ((p.BodySizeFac + 1f) / 2f));

            FoxAtlas.EnsureLoaded();
            drawNothing = !ResolvePartsRig(p);
            BuildPartsRig(p);
            tobyFramesActive = !drawNothing && ResolveTobyFrames(p);

            tailSegmentCount = Mathf.Clamp(p.TailSegments, 1, 32);
            tail = new TailSegment[tailSegmentCount];
            tailRad = new float[tailSegmentCount];
            float artTail = (p.TailLengthVsBody > 0f) ? (P_BODY_W * partsScale * p.TailLengthVsBody)
                                                      : (P_TAIL_W * partsScale);
            float segLen = artTail * Mathf.Max(0.01f, p.TailLengthFac) / tailSegmentCount;
            for (int i = 0; i < tailSegmentCount; i++)
            {
                tailRad[i] = ((p.TailHeightVsBody > 0f) ? (P_BODY_H * p.TailHeightVsBody) : P_TAIL_H)
                             * 0.5f * partsScale * Mathf.Max(0.01f, p.TailThicknessFac)
                             * Mathf.Lerp(1f, Mathf.Clamp(p.TailTipTaper, 0.05f, 1f),
                                          tailSegmentCount == 1 ? 0f : (float)i / (tailSegmentCount - 1));
                tail[i] = new TailSegment(this, tailRad[i], segLen, (i > 0) ? tail[i - 1] : null,
                                          0.85f, 0.95f, 0.5f, pullInPreviousPosition: true);
                parts.Add(tail[i]);
            }

            head = new GenericBodyPart(this, 6f * p.HeadSize, 0.5f, 0.99f, fox.bodyChunks[0]);
            parts.Add(head);

            bodyParts = parts.ToArray();

            int cursor = StartOfExtraSprites;
            for (int i = 0; i < p.Cosmetics.Count && !drawNothing; i++)
            {
                FoxCosmeticSpec spec = p.Cosmetics[i];
                FoxCosmetic layer = FoxCosmetics.Create(spec, this, cursor);
                if (layer == null)
                {
                    continue;
                }
                cursor = AddCosmetic(cursor, layer);
            }

            WireGaitPartners(p);
        }

        public int AddCosmetic(int spriteIndex, FoxCosmetic cosmetic)
        {
            cosmetics.Add(cosmetic);
            spriteIndex += cosmetic.numberOfSprites;
            extraSprites += cosmetic.numberOfSprites;
            return spriteIndex;
        }

        private float SpineRadius(float t)
        {
            float a = fox.bodyChunks[0].rad;
            float b = fox.bodyChunks[1].rad;
            float c = fox.bodyChunks[2].rad;
            return (t < 0.5f) ? Mathf.Lerp(a, b, t * 2f) : Mathf.Lerp(b, c, (t - 0.5f) * 2f);
        }

        private static float WidthProfile(FoxBreedParams p, float u)
        {
            const float CHEST_AT = 0.28f;
            const float WAIST_AT = 0.62f;
            if (u <= CHEST_AT) return Mathf.Lerp(p.BodyProfileNeck, p.BodyProfileChest, u / CHEST_AT);
            if (u <= WAIST_AT)
            {
                return Mathf.Lerp(p.BodyProfileChest, p.BodyProfileWaist, (u - CHEST_AT) / (WAIST_AT - CHEST_AT));
            }
            return Mathf.Lerp(p.BodyProfileWaist, p.BodyProfileHaunch, (u - WAIST_AT) / (1f - WAIST_AT));
        }

        private float SpineHalfWidth(FoxBreedParams p, float u)
        {
            return SpineRadius(u) * Mathf.Max(0.01f, p.BodyMeshRadFac) * Mathf.Max(0f, WidthProfile(p, u))
                   * Mathf.Lerp(1f, Mathf.Clamp(p.SqueezeBodyFac, 0.05f, 1f), fox.DrawSqueezeAmount);
        }

        private float TailSqueezeFac(FoxBreedParams p)
        {
            return Mathf.Lerp(1f, Mathf.Clamp(p.SqueezeTailFac, 0.05f, 1f), fox.DrawSqueezeAmount);
        }

        private static void BuildArtFrame(Vector2 rear, Vector2 front, float depth,
                                          out Vector2 fwd, out Vector2 up)
        {
            Vector2 span = front - rear;
            float len = span.magnitude;
            fwd = (len > 0.01f) ? span / len : new Vector2(1f, 0f);
            float sign = (depth >= 0f) ? 1f : -1f;
            up = new Vector2(-fwd.y, fwd.x) * sign;
        }

        public Vector2 BodyAxis { get { return bodyAxis; } }

        public Vector2 BodyUp
        {
            get
            {
                Vector2 perp = new Vector2(-bodyAxis.y, bodyAxis.x);
                return perp * upSign;
            }
        }

        private void UpdateUpSign(FoxBreedParams p)
        {
            float x = bodyAxis.x;
            float raw = (x < 0f) ? -1f : 1f;
            bool onPole = fox != null && fox.PoleHeld;
            if (fox != null && fox.WallHeld)
            {
                float ay = bodyAxis.y * fox.WallFaceSide;
                if (Mathf.Abs(bodyAxis.y) >= WALL_SIGN_BAND)
                {
                    upSign = (ay < 0f) ? -1f : 1f;
                }
                return;
            }
            if (p == null || (!p.DrawUpSignLatch && !onPole))
            {
                upSign = raw;
                return;
            }
            float band = Mathf.Max(0f, p.DrawUpSignDeadband);
            if (onPole) band = Mathf.Max(band, POLE_VERTICAL_BAND);
            if (Mathf.Abs(x) >= band)
            {
                upSign = raw;
            }
        }

        private const float POLE_VERTICAL_BAND = 0.3f;

        private const float WALL_SIGN_BAND = 0.3f;

        private float upSign = 1f;

        private float depthSideSign;

        public Vector2 LimbsAimFor { get; private set; }

        public float TailRad(int i)
        {
            return (tailRad != null && i >= 0 && i < tailRad.Length) ? tailRad[i] : 1f;
        }

        public Vector2 BackPoint(float u, float timeStacker, out Vector2 outward)
        {
            outward = new Vector2(0f, 1f);
            if (fox.bodyChunks == null || fox.bodyChunks.Length < 3) return Vector2.zero;
            if (drawPositions == null || drawPositions.GetLength(0) < 3) return fox.bodyChunks[0].pos;

            Vector2 headChunk = Vector2.Lerp(drawPositions[0, 1], drawPositions[0, 0], timeStacker);
            Vector2 midChunk = Vector2.Lerp(drawPositions[1, 1], drawPositions[1, 0], timeStacker);
            Vector2 rearChunk = Vector2.Lerp(drawPositions[2, 1], drawPositions[2, 0], timeStacker);
            Vector2 bez = midChunk * 2f - (headChunk + rearChunk) * 0.5f;

            u = Mathf.Clamp01(u);
            Vector2 at = Bezier(headChunk, bez, rearChunk, u);

            Vector2 tangent = (bez - headChunk) * (2f * (1f - u)) + (rearChunk - bez) * (2f * u);
            if (tangent.sqrMagnitude < 0.0001f) tangent = DrawAxis(headChunk, rearChunk);

            Vector2 perp = new Vector2(-tangent.y, tangent.x).normalized;
            Vector2 up = DrawUp(headChunk, rearChunk);
            outward = (Vector2.Dot(perp, up) < 0f) ? -perp : perp;

            bool onChest;
            return at + outward * BackAnchorHalf(fox.P, u, timeStacker, headChunk, rearChunk, out onChest);
        }

        private float BackAnchorHalf(FoxBreedParams p, float u, float timeStacker,
                                     Vector2 headChunk, Vector2 rearChunk, out bool onChest)
        {
            onChest = false;
            if (!p.CosmeticAnchorDrawnBody) return SpineHalfWidth(p, u);
            return PartsDrawnHalfExtent(p, u, timeStacker, headChunk, rearChunk, out onChest);
        }

        public float PartsArtScale { get { return partsScale; } }

        public bool EveryLimbGrippedAtLeast(FoxLimb me, int minGrip)
        {
            for (int i = 0; i < limbs.Length; i++)
            {
                if (limbs[i].gripCounter > me.gripCounter) return false;
                if (limbs[i].gripCounter < Mathf.Max(1, minGrip)) return false;
            }
            return true;
        }

        public override void Update()
        {
            base.Update();

            if (fox.room == null) return;

            FoxBreedParams p = fox.P;

            bodyAxis = Fox.Dir(fox.bodyChunks[2].pos, fox.bodyChunks[0].pos);

            UpdateUpSign(p);

            UpdateAim(p);
            UpdateDrawPositions(p);
            UpdatePartsMotion(p);
            ApplyPartsPose(p);
            BuildArtFrame(drawPositions[2, 0], drawPositions[0, 0], headDepth, out artFwd, out artUp);
            UpdateLimbs(p);
            UpdateSpearLayer(p);
            UpdateTail(p);
            UpdateHead(p);
            UpdatePartsHeadFrame(p);
            TickTobyFrames(p);

            MeasureDrawMotion();

            if (p != null && p.CamoEnabled) DriftCamoColor(p);

            UpdatePheromoneLook(p);

            for (int i = 0; i < cosmetics.Count; i++) cosmetics[i].Update();

            lastPounceState = fox.pounce;
        }

        private void MeasureDrawMotion()
        {
            float rot = AimDeg(artFwd);
            if (artRotSeeded)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(lastArtRot, rot));
                if (d > maxDrawRotDelta) maxDrawRotDelta = d;
                float dp = (drawPositions[1, 0] - lastDrawMid).magnitude;
                if (dp > maxDrawPosDelta) maxDrawPosDelta = dp;
            }
            lastArtRot = rot;
            lastDrawMid = drawPositions[1, 0];
            artRotSeeded = true;
        }

        private void UpdateAim(FoxBreedParams p)
        {
            UpdateHeadLook(p);

            Vector2 target;
            Creature chase = fox.chaseTarget;
            if (chase != null && chase.room == fox.room && chase.mainBodyChunk != null)
            {
                target = chase.mainBodyChunk.pos;
            }
            else if (fox.lastFollowedConnection != default(MovementConnection))
            {
                IntVector2 dest = fox.lastFollowedConnection.DestTile;
                target = fox.room.MiddleOfTile(dest.x, dest.y);
            }
            else
            {
                target = fox.bodyChunks[0].pos + bodyAxis * 60f;
            }

            LimbsAimFor = target;

            target = ApplyHeadLook(p, target);
            if (fox.HopLook.LooksAtGoal) target = fox.HopLook.Goal;
            lookPos = lookInitialized ? Vector2.Lerp(lookPos, target, 0.2f) : target;
            lookInitialized = true;
        }

        private float poleSwayFront, poleSwayHind;

        private void ApplyPoleBodySway(FoxBreedParams p)
        {
            float amt = Mathf.Clamp01(fox.PoleAmount);
            if (amt <= 0f || p.PoleBodySwayPx <= 0f)
            {
                poleSwayFront = 0f;
                poleSwayHind = 0f;
                return;
            }
            int n = Mathf.Max(1, p.PoleBodySwayCycles);
            float r = Mathf.Clamp(p.PoleReachFrac, 0.05f, 0.9f);
            float a = 2f * Mathf.PI * n * (fox.PolePhase - r * 0.5f);
            float lag = 2f * Mathf.PI * Mathf.Clamp01(p.PoleBodySwayLag);
            float amp = p.PoleBodySwayPx * amt;
            poleSwayFront = amp * Mathf.Cos(a);
            poleSwayHind = amp * Mathf.Cos(a - lag);
            Vector2 up = BodyUp;
            drawPositions[0, 0] += up * poleSwayFront;
            drawPositions[2, 0] += up * poleSwayHind;
        }

        private void UpdateDrawPositions(FoxBreedParams p)
        {
            Vector2 ride = RideVector(p);
            for (int i = 0; i < fox.bodyChunks.Length; i++)
            {
                Vector2 seat = fox.bodyChunks[i].pos + ride;
                drawPositions[i, 1] = drawPositions[i, 0];
                drawPositions[i, 0] = (p.DrawSmoothing <= 0f)
                    ? seat
                    : Vector2.Lerp(seat, drawPositions[i, 0], p.DrawSmoothing);
            }

            float smoothing = (4f + 7f / Mathf.Max(0.01f, p.WalkBob)) / 2f;
            frontBob = (frontBob * smoothing + (frontLegsDown - 1)) / (smoothing + 1f);
            hindBob = (hindBob * smoothing + (hindLegsDown - 1)) / (smoothing + 1f);
            float bobFac = (1f - fox.PoleAmount) * (1f - fox.WallAmount);
            drawPositions[0, 0].y += frontBob * p.WalkBob * bobFac;
            drawPositions[1, 0].y += (frontBob + hindBob) * 0.5f * p.WalkBob * bobFac;
            drawPositions[2, 0].y += hindBob * p.WalkBob * bobFac;
            ApplyPoleBodySway(p);

            float crouch = Mathf.Clamp01(fox.StalkCrouch);
            if (crouch > 0f)
            {
                float drop = p.StalkCrouchDrop * crouch;
                for (int i = 0; i < fox.bodyChunks.Length; i++) drawPositions[i, 0].y -= drop;
            }

            float pounceGather = Mathf.Clamp01(fox.DrawPounceGather);
            float enterGather = Mathf.Clamp01(fox.EnterGather);
            bool enterOwnsPose = enterGather > pounceGather;
            float gather = Mathf.Max(pounceGather, enterGather);
            if (gather > 0f && p.PounceGatherArch != 0f)
            {
                Vector2 gUp = BodyUp;
                float arch = p.PounceGatherArch * gather
                             + p.PounceGatherReadyRise * Mathf.Clamp01(fox.PounceGatherReady);
                drawPositions[0, 0] -= gUp * (arch * 0.75f);
                drawPositions[1, 0] += gUp * arch;
                drawPositions[2, 0] -= gUp * (arch * 0.5f);

                float c = Mathf.Clamp01(enterOwnsPose ? p.EnterGatherCompress : p.PounceGatherCompress) * gather;
                if (c > 0f)
                {
                    drawPositions[0, 0] = Vector2.Lerp(drawPositions[0, 0], drawPositions[1, 0], c);
                    drawPositions[2, 0] = Vector2.Lerp(drawPositions[2, 0], drawPositions[1, 0], c);
                }
            }

            float flight = Mathf.Clamp01(fox.DrawPounceFlight);
            if (flight > 0f && p.PounceFlightStretch > 0f)
            {
                Vector2 v = fox.bodyChunks[1].vel;
                float sp = v.magnitude;
                if (sp > 0.01f)
                {
                    float stretch = Mathf.InverseLerp(2f, 12f, sp) * p.PounceFlightStretch * flight;
                    Vector2 vd = v / sp;
                    drawPositions[0, 0] += vd * stretch;
                    drawPositions[2, 0] -= vd * stretch;
                }
            }

            float pland = Mathf.Clamp01(fox.DrawPounceLand);
            if (pland > 0f && p.PounceLandCompress != 0f)
            {
                Vector2 lUp = BodyUp;
                float c = p.PounceLandCompress * pland;
                drawPositions[0, 0] -= lUp * c;
                drawPositions[1, 0] -= lUp * (c * 0.5f);
                drawPositions[2, 0] += lUp * (c * 0.5f);
            }

            float landing = Mathf.Clamp01(fox.DrawLandingPose);
            if (landing > 0f && p.LandingPoseLevel > 0f)
            {
                float level = Mathf.Clamp01(p.LandingPoseLevel) * landing;
                float mean = (drawPositions[0, 0].y + drawPositions[2, 0].y) * 0.5f;
                drawPositions[0, 0].y = Mathf.Lerp(drawPositions[0, 0].y, mean, level);
                drawPositions[2, 0].y = Mathf.Lerp(drawPositions[2, 0].y, mean, level);
            }

            ApplyHopLookPose(p);

            ApplySpearBodyPose(p);
        }

        private void UpdateLimbs(FoxBreedParams p)
        {
            frontLegsDown = 0;
            hindLegsDown = 0;
            int gripping = 0;

            Vector2 pChest, pFwd, pUp;
            float pSign;
            PartsFrameTick(p, out pChest, out pFwd, out pUp, out pSign);
            PublishPoleFrame(pFwd, pUp);

            for (int i = 0; i < limbs.Length; i++)
            {
                bool front = i < 2;
                limbs[i].attachPos = PartsLegRoot(p, i, pChest, pFwd, pUp);

                limbs[i].Update();

                if (limbs[i].gripCounter > 0) gripping++;

                if (limbs[i].ContactTicks(p) >= p.LimbGripDelay)
                {
                    if (front) frontLegsDown++;
                    else hindLegsDown++;
                }
            }

            noGripCounter = (gripping > 0) ? 0 : noGripCounter + 1;
        }

        private void UpdateTail(FoxBreedParams p)
        {
            Vector2 partsChest, partsFwd, partsUp;
            float partsSign;
            PartsFrameTick(p, out partsChest, out partsFwd, out partsUp, out partsSign);
            Vector2 tailRoot = PartsTailRoot(p, drawPositions[2, 0], partsFwd) - partsUp * p.TailRootDrop;
            tail[0].connectedPoint = tailRoot;

            if (lastPounceState == Fox.PounceState.Crouch && fox.pounce == Fox.PounceState.Propel
                && p.PounceTailWhip != 0f && !fox.HopLook.OwnsPose)
            {
                float n = p.PounceTailWhip;
                Vector2 back = Fox.Dir(drawPositions[0, 0], drawPositions[2, 0]);
                for (int i = 0; i < tailSegmentCount; i++)
                {
                    tail[i].vel += back * n;
                    n /= 1.7f;
                }
            }

            ApplyHopLookTail();

            for (int i = tailSegmentCount - 1; i >= 0; i--)
            {
                tail[i].Update();

                float t = (tailSegmentCount == 1) ? 1f : (float)i / (tailSegmentCount - 1);
                if (fox.room.PointSubmerged(tail[i].pos)) tail[i].vel *= 0.8f;
                else
                {
                    float droop = Mathf.Lerp(Mathf.Clamp01(p.TailRootDroop),
                                             Mathf.Clamp01(p.DeathTailRootDroop),
                                             Mathf.Clamp01(fox.DeathLimpAmount));
                    float ramp = Mathf.Lerp(droop, 1f,
                                            Mathf.Pow(t, Mathf.Max(0.1f, p.TailGravityCurve)));
                    tail[i].vel.y -= p.TailGravity * ramp;
                }

                float leash = tail[i].connectionRad * (i + 1) * 1.6f;
                if (!Custom.DistLess(tail[i].pos, tailRoot, leash))
                {
                    tail[i].pos = tailRoot + Fox.Dir(tailRoot, tail[i].pos) * leash;
                }

                Vector2 anchor = (i == 0) ? drawPositions[1, 0]
                               : (i == 1) ? tailRoot
                               : tail[i - 2].pos;
                anchor = Vector2.Lerp(anchor, drawPositions[1, 0], 0.2f);
                float dist = Vector2.Distance(anchor, tail[i].pos);
                if (dist > 0.01f)
                {
                    tail[i].vel += Fox.Dir(anchor, tail[i].pos)
                                   * (p.TailStiffness * Mathf.Pow(p.TailStiffnessDecline, i)) / dist;
                }
            }

            ApplyTailLife(p, tailRoot, -partsFwd, partsUp);
            ApplySpearTail(p, partsUp);
        }

        private void UpdateHead(FoxBreedParams p)
        {
            head.Update();

            if (fox.HeldPrey != null && fox.HeldPrey.mainBodyChunk != null)
            {
                head.pos = fox.HeldPrey.mainBodyChunk.pos;
            }
            else if (fox.Consious)
            {
                Vector2 dir = Fox.Dir(drawPositions[0, 0], lookPos);
                Vector2 spine = Fox.Dir(drawPositions[1, 0], drawPositions[0, 0]);
                float behind = Mathf.Clamp(-Vector2.Dot(dir, spine), 0f, 1f);
                float pull = 1f - Mathf.Pow(1f - behind, 3f);
                pull *= 1f - p.NeckStiffness;
                if (pull > 0f) dir = Vector3.Slerp(dir, spine, pull);
                head.vel += dir * Mathf.Lerp(6f, 2f, p.NeckStiffness);
            }

            if (fox.HeldPrey == null || fox.HeldPrey.mainBodyChunk == null)
            {
                head.vel.y -= fox.Consious ? p.HeadGravity : Mathf.Max(0f, p.DeathHeadGravity);
            }

            float lead = P_CHEST_W * partsScale * Mathf.Max(0.05f, p.PartsNeckFac);
            Vector2 hChest, hFwd, hUp; float hSign;
            PartsFrameTick(p, out hChest, out hFwd, out hUp, out hSign);
            Vector2 neck = PartsNeck(p, hChest, hFwd) - hUp * (p.HeadDrop - p.HeadCarryPx);
            head.ConnectToPoint(neck, lead * p.HeadNeckSlack,
                                push: false, 0.2f, fox.bodyChunks[0].vel, 0.5f, 0.1f);

            lastHeadDepth = headDepth;
            float wantDepth = Mathf.Clamp(bodyAxis.x / 0.5f, -1f, 1f);

            float depthBand = Mathf.Clamp(p.DepthVerticalHoldBand, 0f, 0.49f);
            bool poleHold = fox.PoleHeld;
            if (poleHold) depthBand = Mathf.Max(depthBand, POLE_VERTICAL_BAND);
            if (Mathf.Abs(bodyAxis.x) >= depthBand) depthSideSign = (bodyAxis.x < 0f) ? -1f : 1f;
            bool wallHold = fox.WallHeld;
            if (wallHold)
            {
                depthBand = Mathf.Max(depthBand, POLE_VERTICAL_BAND);
                if (Mathf.Abs(bodyAxis.y) >= WALL_SIGN_BAND)
                    depthSideSign = (bodyAxis.y * fox.WallFaceSide < 0f) ? -1f : 1f;
            }
            if (depthBand > 0f && !fox.TurnActive
                && Mathf.Abs(bodyAxis.x) < depthBand && depthSideSign != 0f)
            {
                float t = Mathf.Clamp01(Mathf.Abs(bodyAxis.x) / depthBand);
                wantDepth = Mathf.Lerp(depthSideSign, wantDepth, t);
            }
            float rate = 0.1f;
            float turnProg = fox.TurnProgress;
            if (Fox.TurnIsFrontPivot(p) && fox.TurnActive)
            {
                headDepth = Mathf.Lerp(headDepth, wantDepth, 0.25f);
                return;
            }
            if (turnProg > 0f && turnProg < 0.5f && p.TurnDepthFac > 0f)
            {
                wantDepth = Mathf.Lerp(wantDepth, 0f, Mathf.Clamp01(p.TurnDepthFac));
                rate = 0.25f;
            }
            headDepth = Mathf.Lerp(headDepth, wantDepth, rate);
        }

        public override void Reset()
        {
            bodyAxis = Fox.Dir(fox.bodyChunks[2].pos, fox.bodyChunks[0].pos);
            upSign = (bodyAxis.x < 0f) ? -1f : 1f;
            Vector2 ride = RideVector(fox.P);
            for (int i = 0; i < fox.bodyChunks.Length; i++)
            {
                drawPositions[i, 0] = fox.bodyChunks[i].pos + ride;
                drawPositions[i, 1] = drawPositions[i, 0];
            }
            for (int i = 0; i < tail.Length; i++) tail[i].Reset(fox.bodyChunks[2].pos + ride);
            for (int i = 0; i < limbs.Length; i++) limbs[i].Reset(fox.bodyChunks[1].pos);
            head.Reset(fox.bodyChunks[0].pos + ride);

            headDepth = Mathf.Clamp(bodyAxis.x / 0.5f, -1f, 1f);
            lastHeadDepth = headDepth;
            camoSampleSeeded = false;
            for (int i = 0; i < cosmetics.Count; i++) cosmetics[i].Reset();

            BuildArtFrame(drawPositions[2, 0], drawPositions[0, 0], headDepth, out artFwd, out artUp);

            lookPos = fox.bodyChunks[0].pos + bodyAxis * 60f;
            lookInitialized = true;
            LimbsAimFor = lookPos;
            frontBob = 0f;
            hindBob = 0f;
            noGripCounter = 0;

            speedSmooth = 0f;
            travelVel = Vector2.zero;
            travelSeeded = false;
            lastWavePitchDeg = 0f;
            wavePitchDeg = 0f;
            lastLeanDeg = 0f;
            leanDeg = 0f;
            lastVelAlong = Vector2.Dot(fox.bodyChunks[1].vel, bodyAxis);
            lastHeadDepthForTrend = headDepth;

            bodyBend = 0f;
            lastBodyBend = 0f;
            bodySquash = 0f;
            lastBodySquash = 0f;
            bodyLenRatio = 1f;
            landImpact = 0f;
            lastAirVy = 0f;
            lastGroundedForSquash = true;
            midLagSeeded = false;
            accelSmooth = 0f;
            flexGeom = 0f;
            flexMotion = 0f;
            squashGeom = 0f;
            squashMotion = 0f;
        }

        public override void SuckedIntoShortCut(Vector2 shortCutPosition)
        {
            for (int i = 0; i < tail.Length; i++)
            {
                tail[i].lastPos = tail[i].pos;
                tail[i].vel *= 0.5f;
                tail[i].pos = (tail[i].pos * 5f + shortCutPosition) / 6f;
            }
            for (int i = 0; i < limbs.Length; i++)
            {
                limbs[i].lastPos = limbs[i].pos;
                limbs[i].vel *= 0.5f;
                limbs[i].pos = (limbs[i].pos * 5f + shortCutPosition) / 6f;
            }
            head.lastPos = head.pos;
            head.vel *= 0.5f;
            head.pos = (head.pos * 5f + shortCutPosition) / 6f;

            travelSeeded = false;
            midLagSeeded = false;
            speedSmooth = 0f;
        }

        private void BindTailArt(TriangleMesh mesh, FoxBreedParams p)
        {
            if (mesh == null || mesh.UVvertices == null) return;
            FAtlasElement el = FoxAtlas.Element(PartsEl(p, "Tail"));
            if (el == null) return;
            mesh.element = el;
            MapTailUV(mesh, el, 1f);
        }

        private void MapTailUV(TriangleMesh mesh, FAtlasElement el, float side)
        {
            if (mesh == null || mesh.UVvertices == null || el == null) return;
            int n = mesh.UVvertices.Length;
            int pairs = (n - 1) / 2;
            if (pairs < 1) return;
            float lo = (side >= 0f) ? 0f : 1f;
            float hi = (side >= 0f) ? 1f : 0f;
            for (int v = 0; v < n; v++)
            {
                float u, w;
                if (v == n - 1) { u = 0f; w = 0.5f; }
                else { u = 1f - Mathf.Clamp01((v / 2) / (float)pairs); w = (v % 2 == 0) ? lo : hi; }
                mesh.UVvertices[v] = new Vector2(
                    Mathf.Lerp(el.uvBottomLeft.x, el.uvTopRight.x, u),
                    Mathf.Lerp(el.uvBottomLeft.y, el.uvTopRight.y, w));
            }
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            if (drawNothing)
            {
                sLeaser.sprites = new FSprite[1] { new FSprite("Futile_White") };
                sLeaser.containers = new FContainer[1] { new FContainer() };
                AddToContainer(sLeaser, rCam, null);
                base.InitiateSprites(sLeaser, rCam);
                return;
            }

            sLeaser.sprites = new FSprite[TotalSprites];
            FoxBreedParams bp = fox.P;

            InitiatePartsSprites(sLeaser, bp);

            sLeaser.sprites[SpriteTail] =
                TriangleMesh.MakeLongMesh(tail.Length, pointyTip: true, customColor: true);
            BindTailArt(sLeaser.sprites[SpriteTail] as TriangleMesh, bp);

            for (int i = 0; i < cosmetics.Count; i++) cosmetics[i].InitiateSprites(sLeaser, rCam);
            if (tobyFramesActive) InitiateTobyFrameSprite(sLeaser);

            sLeaser.containers = new FContainer[1] { new FContainer() };

            AddToContainer(sLeaser, rCam, null);
            base.InitiateSprites(sLeaser, rCam);

            if (!builtLogged)
            {
                builtLogged = true;
                var names = new string[cosmetics.Count];
                for (int i = 0; i < cosmetics.Count; i++) names[i] = cosmetics[i].LayerName;
            }
        }

        private int LimbSprite(int i) { return SpriteLimbsStart + i * PARTS_LEG_SPRITES; }

        public Vector2 DrawHeadPos { get; private set; }

        public Vector2 DrawHeadDir { get; private set; }

        private bool headDrawGuard;

        public Vector2 DrawNosePos { get; private set; }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
                                         float timeStacker, Vector2 camPos)
        {
            FoxBreedParams p = fox.P;

            if (drawNothing)
            {
                base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
                return;
            }

            RestoreCamoColors(sLeaser);

            Vector2 headChunk = Vector2.Lerp(drawPositions[0, 1], drawPositions[0, 0], timeStacker);
            Vector2 midChunk = Vector2.Lerp(drawPositions[1, 1], drawPositions[1, 0], timeStacker);
            Vector2 rearChunk = Vector2.Lerp(drawPositions[2, 1], drawPositions[2, 0], timeStacker);
            Vector2 drawAxis = DrawAxis(headChunk, rearChunk);
            float depth = Mathf.Lerp(lastHeadDepth, headDepth, timeStacker);

            Vector2 aFwd, aUp;
            BuildArtFrame(rearChunk, headChunk, depth, out aFwd, out aUp);

            ComputeHeadFrame(p, timeStacker, headChunk, midChunk, rearChunk);
            DrawPartsRig(sLeaser, p, timeStacker, headChunk, midChunk, rearChunk, depth, camPos);

            var tailMesh = sLeaser.sprites[SpriteTail] as TriangleMesh;
            if (tailMesh != null)
            {
                Vector2 tChest, tFwd, tUp; float tSign;
                BuildPartsFrame(p, rearChunk, midChunk, headChunk, depth,
                                out tChest, out tFwd, out tUp, out tSign);
                Vector2 tRear = midChunk + (rearChunk - midChunk) * TurnForeshorten(p, depth);
                Vector2 prev = PartsTailRoot(p, tRear, tFwd) - tUp * p.TailRootDrop;
                float tailSq = TailSqueezeFac(p);
                Vector2 first = Vector2.Lerp(tail[0].lastPos, tail[0].pos, timeStacker) - prev;
                float side = (first.sqrMagnitude > 0.01f)
                    ? Mathf.Sign(Vector2.Dot(new Vector2(-first.y, first.x), aUp)) : 1f;
                MapTailUV(tailMesh, tailMesh.element, side);
                for (int i = 0; i < tail.Length; i++)
                {
                    Vector2 seg = Vector2.Lerp(tail[i].lastPos, tail[i].pos, timeStacker);
                    Vector2 dir = seg - prev;
                    float dist = dir.magnitude;
                    dir = (dist > 0.01f) ? dir / dist : -drawAxis;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    Vector2 w = perp * (TailRad(i) * tailSq);
                    tailMesh.MoveVertice(i * 4, prev - w - camPos);
                    tailMesh.MoveVertice(i * 4 + 1, prev + w - camPos);
                    if (i < tail.Length - 1)
                    {
                        tailMesh.MoveVertice(i * 4 + 2, seg - w - camPos);
                        tailMesh.MoveVertice(i * 4 + 3, seg + w - camPos);
                    }
                    else
                    {
                        tailMesh.MoveVertice(i * 4 + 2, seg - camPos);
                    }
                    prev = seg;
                }
                TintTail(tailMesh);
            }

            for (int i = 0; i < cosmetics.Count; i++)
            {
                cosmetics[i].DrawSprites(sLeaser, rCam, timeStacker, camPos);
            }

            if (tobyFramesActive) DrawTobyFrame(sLeaser, p, headChunk, rearChunk, depth, camPos);

            ApplyCamoBlend(sLeaser, rCam, p, headChunk, midChunk, rearChunk);

            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
        }

        private Color CamoPaletteColor(FoxBreedParams p)
        {
            return Color.Lerp(palette.blackColor, palette.fogColor, Mathf.Clamp01(p.CamoColorFogMix));
        }

        private int CamoTick
        {
            get { return (fox.room != null && fox.room.game != null) ? fox.room.game.clock : 0; }
        }

        private static bool SameSampledColor(Color a, Color b)
        {
            const float tol = 0.5f / 255f;
            return Mathf.Abs(a.r - b.r) < tol && Mathf.Abs(a.g - b.g) < tol && Mathf.Abs(a.b - b.b) < tol;
        }

        private bool SampleCamoBackground(RoomCamera rCam, Vector2 seat0, Vector2 seat1, Vector2 seat2)
        {
            int tick = CamoTick;
            if (camoSampleSeeded && tick == camoSampleTick) return true;

            bool onScreen = false;
            int cam = -1, roomIndex = -1;
            bool v0 = false, v1 = false, v2 = false;
            Room camRoom = (rCam != null) ? rCam.room : null;
            Texture2D level = (rCam != null) ? rCam.levelTexture : null;
            if (camRoom == null || level == null || camRoom != fox.room
                || camRoom.cameraPositions == null || camRoom.abstractRoom == null)
            {
            }
            else
            {
                cam = rCam.currentCameraPosition;
                roomIndex = camRoom.abstractRoom.index;
                if (cam < 0 || cam >= camRoom.cameraPositions.Length)
                {
                }
                else
                {
                    Vector2 origin = camRoom.cameraPositions[cam];
                    v0 = OnLevelTexture(seat0 - origin, level);
                    v1 = OnLevelTexture(seat1 - origin, level);
                    v2 = OnLevelTexture(seat2 - origin, level);
                    onScreen = v0 && v1 && v2;
                }
            }

            if (camoSampleSeeded && cam >= 0 && (cam != camoSampleCam || roomIndex != camoSampleRoom))
            {
                camoSampleSeeded = false;
            }
            if (!onScreen) return camoSampleSeeded;

            Color s0 = rCam.PixelColorAtCoordinate(seat0);
            Color s1 = rCam.PixelColorAtCoordinate(seat1);
            Color s2 = rCam.PixelColorAtCoordinate(seat2);
            Color pick;
            if (SameSampledColor(s0, s1))
            {
                pick = s0;
            }
            else if (SameSampledColor(s1, s2))
            {
                pick = s1;
            }
            else if (SameSampledColor(s2, s0))
            {
                pick = s2;
            }
            else
            {
                pick = (s0 + s1 + s2) / 3f;
            }
            pick.a = 1f;

            camoSampledPick = pick;
            if (!camoSampleSeeded)
            {
                camoSampledColor = pick;
                camoSampleSeeded = true;
            }
            camoSampleTick = tick;
            camoSampleCam = cam;
            camoSampleRoom = roomIndex;
            return true;
        }

        private static bool OnLevelTexture(Vector2 rel, Texture2D level)
        {
            int x = Mathf.FloorToInt(rel.x);
            int y = Mathf.FloorToInt(rel.y);
            return x >= 0 && y >= 0 && x < level.width && y < level.height;
        }

        private void DriftCamoColor(FoxBreedParams p)
        {
            if (fox.CamoAmount <= 0f || !p.CamoSamplesBackground)
            {
                camoSampleSeeded = false;
                return;
            }
            if (!camoSampleSeeded) return;
            camoSampledColor = Color.Lerp(camoSampledColor, camoSampledPick, Mathf.Clamp01(p.CamoColorDrift));
        }

        private void RestoreCamoColors(RoomCamera.SpriteLeaser sLeaser)
        {
            if (!camoBlended || camoBase == null || camoBaseSet == null) return;
            camoBlended = false;
            if (sLeaser.sprites == null || sLeaser.sprites.Length != camoBase.Length)
            {
                camoBase = null;
                camoBaseSet = null;
                return;
            }
            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                if (!camoBaseSet[i]) continue;
                camoBaseSet[i] = false;
                FSprite s = sLeaser.sprites[i];
                if (s != null) s.color = camoBase[i];
            }
        }

        private void ApplyCamoBlend(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FoxBreedParams p,
                                    Vector2 headSeat, Vector2 midSeat, Vector2 rearSeat)
        {
            float amount = fox.CamoAmount;
            if (amount <= 0f || p == null || !p.CamoEnabled || !paletteSet)
            {
                return;
            }
            if (sLeaser.sprites == null) return;

            float t = Mathf.Clamp01(amount) * Mathf.Clamp01(p.CamoBlendMax);
            if (t <= 0f) return;

            bool wantBackground = p.CamoSamplesBackground;
            bool sampled = false;
            if (wantBackground)
            {
                bool drawn = p.CamoSampleDrawnBody || fox.bodyChunks == null || fox.bodyChunks.Length < 3;
                sampled = drawn
                    ? SampleCamoBackground(rCam, headSeat, midSeat, rearSeat)
                    : SampleCamoBackground(rCam, fox.bodyChunks[0].pos, fox.bodyChunks[1].pos,
                                           fox.bodyChunks[2].pos);
            }
            Color target = sampled ? camoSampledColor : CamoPaletteColor(p);

            if (camoBase == null || camoBase.Length != sLeaser.sprites.Length)
            {
                camoBase = new Color[sLeaser.sprites.Length];
                camoBaseSet = new bool[sLeaser.sprites.Length];
            }

            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                FSprite s = sLeaser.sprites[i];
                if (s == null) continue;

                if (i == SpriteTail)
                {
                    var tail = s as TriangleMesh;
                    if (tail != null && tail.verticeColors != null)
                    {
                        for (int v = 0; v < tail.verticeColors.Length; v++)
                        {
                            Color c = tail.verticeColors[v];
                            Color m = Color.Lerp(c, target, t);
                            m.a = c.a;
                            tail.verticeColors[v] = m;
                        }
                        continue;
                    }
                }

                Color baseColor = s.color;
                camoBase[i] = baseColor;
                camoBaseSet[i] = true;
                Color mixed = Color.Lerp(baseColor, target, t);
                mixed.a = baseColor.a;
                if (i == SpriteEye) mixed = EyeAfterCamo(p, mixed);
                s.color = mixed;
            }
            camoBlended = true;

        }

        private void ComputeHeadFrame(FoxBreedParams p, float timeStacker,
                                      Vector2 headChunk, Vector2 midChunk, Vector2 rearChunk)
        {
            Vector2 neckLine = Fox.Dir(midChunk, headChunk);
            headDrawGuard = Fox.NeckDot(headChunk, midChunk, rearChunk) < 0f;
            if (headDrawGuard) neckLine = Fox.Dir(rearChunk, headChunk);

            float deg = Mathf.Lerp(lastHeadPitchDeg, headPitchDeg, timeStacker);
            float pitchLim = SpearHeadPitchLimit(p);
            DrawHeadDir = PitchFromNeck(neckLine, Mathf.Clamp(deg, -pitchLim, pitchLim));
        }

        private static Vector2 PitchFromNeck(Vector2 neckLine, float deg)
        {
            if (neckLine.sqrMagnitude < 0.0001f) return new Vector2(1f, 0f);
            neckLine = neckLine.normalized;
            Vector2 up = new Vector2(-neckLine.y, neckLine.x);
            if (up.y < 0f) up = -up;
            float r = deg * Mathf.Deg2Rad;
            Vector2 v = neckLine * Mathf.Cos(r) + up * Mathf.Sin(r);
            return (v.sqrMagnitude > 0.0001f) ? v.normalized : neckLine;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 c, float u)
        {
            float iu = 1f - u;
            return a * (iu * iu) + control * (2f * iu * u) + c * (u * u);
        }

        private static void TintTail(TriangleMesh mesh)
        {
            if (mesh.verticeColors == null)
            {
                mesh.color = Color.white;
                return;
            }
            for (int v = 0; v < mesh.verticeColors.Length; v++) mesh.verticeColors[v] = Color.white;
        }

        private static float AimDeg(Vector2 v)
        {
            return Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;
        }

        private static Vector2 DrawAxis(Vector2 headChunk, Vector2 rearChunk)
        {
            return Fox.Dir(rearChunk, headChunk);
        }

        private Vector2 DrawUp(Vector2 headChunk, Vector2 rearChunk)
        {
            Vector2 axis = DrawAxis(headChunk, rearChunk);
            Vector2 perp = new Vector2(-axis.y, axis.x);
            return perp * upSign;
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            this.palette = palette;
            paletteSet = true;
            for (int i = 0; i < cosmetics.Count; i++) cosmetics[i].ApplyPalette(sLeaser, rCam, palette);
        }

        public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            if (newContatiner == null) newContatiner = rCam.ReturnFContainer("Midground");

            sLeaser.RemoveAllSpritesFromContainer();

            if (drawNothing)
            {
                if (sLeaser.containers != null && sLeaser.containers.Length > 0)
                {
                    newContatiner.AddChild(sLeaser.containers[0]);
                }
                return;
            }

            if (tobyFramesActive)
            {
                AddTobyFrameToContainer(sLeaser, newContatiner);
                return;
            }

            AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.Behind);
            AddPartsToContainer(sLeaser, newContatiner);
            AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.BodySurface);
            AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.BehindHead);
            if (sLeaser.containers != null && sLeaser.containers.Length > 0)
            {
                newContatiner.AddChild(sLeaser.containers[0]);
            }
            AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.InFront);
        }

        private void AddLayer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
                              FContainer container, FoxCosmetic.ZOrder pass)
        {
            for (int i = 0; i < cosmetics.Count; i++)
            {
                if (cosmetics[i].zOrder == pass) cosmetics[i].AddToContainer(sLeaser, rCam, container);
            }
        }
    }
}
