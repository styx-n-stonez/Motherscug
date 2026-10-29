using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public partial class FoxGraphics : GraphicsModule
    {
        private const float REFERENCE_TAIL_LENGTH = 39f;

        private const float REFERENCE_TAIL_PEAK_RAD = 7.5f;

        private const float ART_BODY_W = 43f;
        private const float ART_BODY_H = 17f;
        private const float ART_SPINE_Y = 6.5f;
        private const float ART_TAIL_X = 2f;
        private const float ART_NECK_X = 36.44f;
        private const float ART_SPINE_LEN = ART_NECK_X - ART_TAIL_X;
        private static readonly float[] ART_LEG_X = { 31f, 37.5f, 12.5f, 17.5f };
        private const float ART_TAIL_LEN = 32f;
        private const float ART_TAIL_H = 15f;
        private const float ART_BODY_GREY = 0x2B / 255f;

        public readonly Fox fox;

        public FoxLimb[] limbs;
        public TailSegment[] tail;
        public GenericBodyPart head;

        public Vector2[,] drawPositions;

        private readonly List<FoxCosmetic> cosmetics = new List<FoxCosmetic>();
        private int extraSprites;
        private int bodyCircleCount;
        private int tailSegmentCount;

        private float[] bodyCircleRad;

        private float[] bodyCircleU;

        private Vector2[] spinePos;
        private float[] spineRad;
        private int bodyMeshPoints;

        private bool headSpriteActive;

        private bool headEyeActive;

        private bool legSpritesActive;

        private bool bodySpriteActive;

        private bool tailSpriteActive;

        private float artScale = 1f;

        private float artRestSpine = 1f;

        private Vector2 artOrigin, artFwd, artUp;
        private float artSx, artSy;

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
        public int SpriteBodyFillStart { get { return 1; } }
        public int SpriteBodyFillEnd { get { return SpriteBodyFillStart + bodyCircleCount; } }
        public int SpriteLimbsStart { get { return SpriteBodyFillEnd; } }
        public int SpriteLimbsEnd { get { return SpriteLimbsStart + limbs.Length * LimbSpriteCount; } }
        public int SpriteTail { get { return SpriteLimbsEnd; } }
        public int SpriteHead { get { return SpriteTail + 1; } }
        public int SpriteEye { get { return SpriteHead + 1; } }

        public Color EyeColor(FoxBreedParams p)
        {
            if (p.BlindEnabled && p.BlindEyeFlicker && fox != null && fox.Blinded)
            {
                return Custom.HSL2RGB(UnityEngine.Random.value, 1f,
                                      0.5f + 0.5f * UnityEngine.Random.value);
            }
            return p.ColorEye;
        }

        internal const float EYE_INK_TINT_MIN = 0.15f;

        internal static Color InkEyeTint(FoxBreedParams p, Color tint)
        {
            float f = Mathf.Clamp(p.EyeInkTintFloor, EYE_INK_TINT_MIN, 1f);
            return new Color(Mathf.Max(tint.r, f), Mathf.Max(tint.g, f), Mathf.Max(tint.b, f), tint.a);
        }

        internal static Color NoHole(FoxBreedParams p, Color c)
        {
            if (!IsHoleValue(c)) return c;
            Color floor = p != null ? p.EyeHoleFloor : default(Color);
            if (IsHoleValue(floor)) floor = new Color(0x0A / 255f, 0x0A / 255f, 0x0A / 255f);
            return new Color(floor.r, floor.g, floor.b, c.a);
        }

        internal static bool IsHoleValue(Color c)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f) == 0
                && Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f) == 0
                && Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f) == 0;
        }

        private Color EyeAfterCamo(FoxBreedParams p, Color blended)
        {
            Color guarded = (partsRigActive || headEyeActive) ? InkEyeTint(p, blended) : NoHole(p, blended);
            return guarded;
        }

        public int SpriteChest { get { return SpriteEye + 1; } }
        public int StartOfExtraSprites { get { return SpriteEye + 1 + (partsRigActive ? 1 : 0); } }
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
            artScale = (artRestSpine / ART_SPINE_LEN) * Mathf.Max(0.05f, p.BodySpriteScaleFac);

            FoxAtlas.EnsureLoaded();
            partsRigActive = ResolvePartsRig(p);
            if (partsRigActive) BuildPartsRig(p);
            tobyFramesActive = ResolveTobyFrames(p);

            bodySpriteActive = !partsRigActive && p.BodySprite && FoxAtlas.Has(p.BodySpriteElement);
            tailSpriteActive = partsRigActive || (bodySpriteActive && FoxAtlas.Has(p.TailSpriteElement));

            tailSegmentCount = Mathf.Clamp(p.TailSegments, 1, 32);
            tail = new TailSegment[tailSegmentCount];
            tailRad = new float[tailSegmentCount];
            float artTail = partsRigActive
                ? ((p.TailLengthVsBody > 0f) ? (P_BODY_W * partsScale * p.TailLengthVsBody)
                                             : (P_TAIL_W * partsScale))
                : (ART_TAIL_LEN * artScale);
            float segLen = ((bodySpriteActive || partsRigActive) ? artTail : REFERENCE_TAIL_LENGTH)
                           * Mathf.Max(0.01f, p.TailLengthFac) / tailSegmentCount;
            for (int i = 0; i < tailSegmentCount; i++)
            {
                tailRad[i] = partsRigActive
                    ? (((p.TailHeightVsBody > 0f) ? (P_BODY_H * p.TailHeightVsBody) : P_TAIL_H)
                       * 0.5f * partsScale * Mathf.Max(0.01f, p.TailThicknessFac)
                       * Mathf.Lerp(1f, Mathf.Clamp(p.TailTipTaper, 0.05f, 1f),
                                    tailSegmentCount == 1 ? 0f : (float)i / (tailSegmentCount - 1)))
                    : bodySpriteActive
                    ? (ART_TAIL_H * 0.5f * artScale * Mathf.Max(0.01f, p.TailThicknessFac))
                    : BrushProfile(p, tailSegmentCount == 1 ? 0.5f : (float)i / (tailSegmentCount - 1))
                      * REFERENCE_TAIL_PEAK_RAD * Mathf.Max(0.01f, p.TailThicknessFac);
                tail[i] = new TailSegment(this, tailRad[i], segLen, (i > 0) ? tail[i - 1] : null,
                                          0.85f, 0.95f, 0.5f, pullInPreviousPosition: true);
                parts.Add(tail[i]);
            }

            head = new GenericBodyPart(this, 6f * p.HeadSize, 0.5f, 0.99f, fox.bodyChunks[0]);
            parts.Add(head);

            bodyParts = parts.ToArray();

            bodyMeshPoints = Mathf.Clamp(p.BodyMeshPoints, 3, 32);
            spinePos = new Vector2[bodyMeshPoints];
            spineRad = new float[bodyMeshPoints];

            bodyCircleCount = (bodySpriteActive || partsRigActive) ? 0 : Mathf.Clamp(p.BodyCircles, 1, 64);
            bodyCircleRad = new float[bodyCircleCount];
            bodyCircleU = new float[bodyCircleCount];
            for (int i = 0; i < bodyCircleCount; i++)
            {
                float u = (i + 0.5f) / bodyCircleCount;
                bodyCircleU[i] = u;
                bodyCircleRad[i] = SpineHalfWidth(p, u);
            }

            headSpriteActive = !partsRigActive && p.HeadSprite && FoxAtlas.Has(p.HeadSpriteElement);
            headEyeActive = headSpriteActive
                            && !string.IsNullOrEmpty(p.HeadEyeElement)
                            && FoxAtlas.Has(p.HeadEyeElement);
            legSpritesActive = !partsRigActive && p.LegSprites
                               && FoxAtlas.Has(p.LegSpriteFront) && FoxAtlas.Has(p.LegSpriteFrontFar)
                               && FoxAtlas.Has(p.LegSpriteHind) && FoxAtlas.Has(p.LegSpriteHindFar);

            int cursor = StartOfExtraSprites;
            for (int i = 0; i < p.Cosmetics.Count; i++)
            {
                FoxCosmeticSpec spec = p.Cosmetics[i];
                FoxCosmetic layer = FoxCosmetics.Create(spec, this, cursor);
                if (layer == null)
                {
                    continue;
                }
                if ((headSpriteActive || partsRigActive) && layer.BakedIntoHeadSprite)
                {
                    continue;
                }
                if (tailSpriteActive && layer.BakedIntoTailSprite)
                {
                    continue;
                }
                cursor = AddCosmetic(cursor, layer);
            }

            WireGaitPartners(p);
            lastTurnSwapCount = fox.TurnSwapCount;
        }

        public int AddCosmetic(int spriteIndex, FoxCosmetic cosmetic)
        {
            cosmetics.Add(cosmetic);
            spriteIndex += cosmetic.numberOfSprites;
            extraSprites += cosmetic.numberOfSprites;
            return spriteIndex;
        }

        private static float BrushProfile(FoxBreedParams p, float t)
        {
            float swell = Mathf.Clamp(p.TailSwellPos, 0.01f, 0.99f);
            if (t <= swell) return Mathf.Lerp(p.TailRootThickness, 1f, t / swell);
            return Mathf.Lerp(1f, p.TailTipThickness, (t - swell) / (1f - swell));
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

        private void BuildArtFrame(FoxBreedParams p, Vector2 rear, Vector2 front, float depth,
                                   out Vector2 origin, out Vector2 fwd, out Vector2 up,
                                   out float sx, out float sy)
        {
            Vector2 span = front - rear;
            float len = span.magnitude;
            fwd = (len > 0.01f) ? span / len : new Vector2(1f, 0f);
            float sign = (depth >= 0f) ? 1f : -1f;
            up = new Vector2(-fwd.y, fwd.x) * sign;
            sy = artScale;
            float stretch = Mathf.Clamp01(p.BodySpriteStretch);
            sx = artScale * Mathf.Clamp(len / artRestSpine, 1f - stretch, 1f + stretch);
            origin = rear;
        }

        private static Vector2 ArtPoint(Vector2 origin, Vector2 fwd, Vector2 up,
                                        float sx, float sy, float ax, float ay)
        {
            return origin + fwd * (sx * (ax - ART_TAIL_X)) + up * (sy * (ay - ART_SPINE_Y));
        }

        private Vector2 ArtPointTick(float ax, float ay)
        {
            return ArtPoint(artOrigin, artFwd, artUp, artSx, artSy, ax, ay);
        }

        private static Color BodyArtTint(FoxBreedParams p, bool mask)
        {
            if (mask) return p.ColorBody;
            return new Color(Mathf.Clamp01(p.ColorBody.r / ART_BODY_GREY),
                             Mathf.Clamp01(p.ColorBody.g / ART_BODY_GREY),
                             Mathf.Clamp01(p.ColorBody.b / ART_BODY_GREY));
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

        public float HeadRad { get { return head.rad; } }

        public float TailRad(int i)
        {
            return (tailRad != null && i >= 0 && i < tailRad.Length) ? tailRad[i] : 1f;
        }

        public int TailSegmentCount { get { return tailSegmentCount; } }

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
            if (!partsRigActive || !p.CosmeticAnchorDrawnBody) return SpineHalfWidth(p, u);
            return PartsDrawnHalfExtent(p, u, timeStacker, headChunk, rearChunk, out onChest);
        }

        public float PartsArtScale { get { return partsRigActive ? partsScale : 1f; } }

        public Color ArtTint(Color variantColor) { return PartsTint(fox.P, variantColor); }

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

            CheckTurnSwap(p);

            UpdateAim(p);
            UpdateDrawPositions(p);
            if (partsRigActive)
            {
                UpdatePartsMotion(p);
                ApplyPartsPose(p);
            }
            BuildArtFrame(p, drawPositions[2, 0], drawPositions[0, 0], headDepth,
                          out artOrigin, out artFwd, out artUp, out artSx, out artSy);
            UpdateLimbs(p);
            UpdateTail(p);
            UpdateHead(p);
            if (partsRigActive) UpdatePartsHeadFrame(p);
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

            float eat = Mathf.Clamp01(fox.EatPose);
            if (eat > 0f)
            {
                target = Vector2.Lerp(target, fox.EatAimPos, Mathf.Clamp01(p.EatHeadFac) * eat);
            }
            if (eat <= 0f) target = ApplyHeadLook(p, target);
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

            float eat = Mathf.Clamp01(fox.EatPose);
            if (eat > 0f)
            {
                float drop = p.EatCrouchDrop * eat;
                for (int i = 0; i < fox.bodyChunks.Length; i++) drawPositions[i, 0].y -= drop;
            }

            float pounceGather = Mathf.Clamp01(fox.PounceGather);
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

            float flight = Mathf.Clamp01(fox.PounceFlight);
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

            float pland = Mathf.Clamp01(fox.PounceLand);
            if (pland > 0f && p.PounceLandCompress != 0f)
            {
                Vector2 lUp = BodyUp;
                float c = p.PounceLandCompress * pland;
                drawPositions[0, 0] -= lUp * c;
                drawPositions[1, 0] -= lUp * (c * 0.5f);
                drawPositions[2, 0] += lUp * (c * 0.5f);
            }

            float landing = Mathf.Clamp01(fox.LandingPose);
            if (landing > 0f && p.LandingPoseLevel > 0f)
            {
                float level = Mathf.Clamp01(p.LandingPoseLevel) * landing;
                float mean = (drawPositions[0, 0].y + drawPositions[2, 0].y) * 0.5f;
                drawPositions[0, 0].y = Mathf.Lerp(drawPositions[0, 0].y, mean, level);
                drawPositions[2, 0].y = Mathf.Lerp(drawPositions[2, 0].y, mean, level);
            }
        }

        private void UpdateLimbs(FoxBreedParams p)
        {
            frontLegsDown = 0;
            hindLegsDown = 0;
            int gripping = 0;

            Vector2 pChest = Vector2.zero, pFwd = Vector2.zero, pUp = Vector2.zero;
            float pSign = 1f;
            if (partsRigActive) PartsFrameTick(p, out pChest, out pFwd, out pUp, out pSign);
            PublishPoleFrame(pFwd, pUp, partsRigActive);

            for (int i = 0; i < limbs.Length; i++)
            {
                bool front = i < 2;
                if (partsRigActive)
                {
                    limbs[i].attachPos = PartsLegRoot(p, i, pChest, pFwd, pUp);
                }
                else if (bodySpriteActive)
                {
                    limbs[i].attachPos = ArtPointTick(ART_LEG_X[i], 0f) - artUp * p.LimbAttachDrop;
                }
                else
                {
                    Vector2 from = drawPositions[front ? 0 : 2, 0];
                    Vector2 toward = drawPositions[1, 0];
                    float along = front ? p.LimbAttachFront : p.LimbAttachRear;
                    limbs[i].attachPos = Vector2.Lerp(from, toward, Mathf.Clamp01(along))
                                         - BodyUp * p.LimbAttachDrop;
                }

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
            Vector2 partsChest = Vector2.zero, partsFwd = Vector2.zero, partsUp = Vector2.zero;
            float partsSign = 1f;
            if (partsRigActive) PartsFrameTick(p, out partsChest, out partsFwd, out partsUp, out partsSign);
            Vector2 tailRoot = partsRigActive
                ? PartsTailRoot(p, drawPositions[2, 0], partsFwd) - partsUp * p.TailRootDrop
                : bodySpriteActive
                ? ArtPointTick(ART_TAIL_X, ART_SPINE_Y) - artUp * p.TailRootDrop
                : drawPositions[2, 0] - BodyUp * p.TailRootDrop;
            tail[0].connectedPoint = tailRoot;

            if (lastPounceState == Fox.PounceState.Crouch && fox.pounce == Fox.PounceState.Propel
                && p.PounceTailWhip != 0f)
            {
                float n = p.PounceTailWhip;
                Vector2 back = Fox.Dir(drawPositions[0, 0], drawPositions[2, 0]);
                for (int i = 0; i < tailSegmentCount; i++)
                {
                    tail[i].vel += back * n;
                    n /= 1.7f;
                }
            }

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

            if (partsRigActive)
            {
                ApplyTailLife(p, tailRoot, -partsFwd, partsUp);
            }
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
                pull *= 1f - Mathf.Clamp01(p.NeckStiffness);
                if (pull > 0f) dir = Vector3.Slerp(dir, spine, pull);
                head.vel += dir * Mathf.Lerp(6f, 2f, Mathf.Clamp01(p.NeckStiffness));
            }

            if (fox.HeldPrey == null || fox.HeldPrey.mainBodyChunk == null)
            {
                head.vel.y -= fox.Consious ? p.HeadGravity : Mathf.Max(0f, p.DeathHeadGravity);
            }

            float lead = partsRigActive ? P_CHEST_W * partsScale * Mathf.Max(0.05f, p.PartsNeckFac)
                       : bodySpriteActive ? (ART_NECK_X - ART_TAIL_X) * artScale * 0.35f
                                          : 12f * p.HeadSize;
            Vector2 partsNeck = Vector2.zero;
            if (partsRigActive)
            {
                Vector2 hChest, hFwd, hUp; float hSign;
                PartsFrameTick(p, out hChest, out hFwd, out hUp, out hSign);
                partsNeck = PartsNeck(p, hChest, hFwd) - hUp * (p.HeadDrop - p.HeadCarryPx);
            }
            Vector2 neck = partsRigActive
                ? partsNeck
                : bodySpriteActive
                ? ArtPointTick(ART_NECK_X, ART_SPINE_Y) - artUp * p.HeadDrop
                : drawPositions[0, 0]
                  + Fox.Dir(drawPositions[1, 0], drawPositions[0, 0]) * lead
                  - BodyUp * p.HeadDrop;
            head.ConnectToPoint(neck, lead * Mathf.Max(0.05f, p.HeadNeckSlack),
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
            if ((p.DepthVerticalHold || poleHold || wallHold) && depthBand > 0f && !fox.TurnActive
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
            if (turnProg > 0f && turnProg < 0.5f && p.TurnDepthFac > 0f && !fox.TurnStalled)
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

            BuildArtFrame(fox.P, drawPositions[2, 0], drawPositions[0, 0], headDepth,
                          out artOrigin, out artFwd, out artUp, out artSx, out artSy);

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
            glanceTicks = 0;
            lastTurnSwapCount = fox.TurnSwapCount;

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

        private static string LegElement(FoxBreedParams p, int i)
        {
            switch (i)
            {
                case 0: return p.LegSpriteFrontFar;
                case 1: return p.LegSpriteFront;
                case 2: return p.LegSpriteHindFar;
                default: return p.LegSpriteHind;
            }
        }

        private void BindTailArt(TriangleMesh mesh, FoxBreedParams p)
        {
            if (mesh == null || mesh.UVvertices == null) return;
            FAtlasElement el = FoxAtlas.Element(partsRigActive ? PartsEl(p, "Tail") : p.TailSpriteElement);
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
            sLeaser.sprites = new FSprite[TotalSprites];
            FoxBreedParams bp = fox.P;

            var bodyTris = new TriangleMesh.Triangle[bodyMeshPoints * 2];
            for (int q = 0; q < bodyMeshPoints; q++)
            {
                int b = q * 4;
                bodyTris[q * 2] = new TriangleMesh.Triangle(b, b + 1, b + 2);
                bodyTris[q * 2 + 1] = new TriangleMesh.Triangle(b + 1, b + 2, b + 3);
            }
            if (partsRigActive)
            {
                InitiatePartsSprites(sLeaser, bp);
            }
            else if (bodySpriteActive)
            {
                var bodyArt = new FSprite(bp.BodySpriteElement);
                bodyArt.anchorX = ART_TAIL_X / ART_BODY_W;
                bodyArt.anchorY = ART_SPINE_Y / ART_BODY_H;
                sLeaser.sprites[SpriteBodyMesh] = bodyArt;
            }
            else
            {
                sLeaser.sprites[SpriteBodyMesh] = new TriangleMesh("Futile_White", bodyTris, customColor: false);
            }

            for (int i = SpriteBodyFillStart; i < SpriteBodyFillEnd; i++)
            {
                sLeaser.sprites[i] = new FSprite("Circle20");
            }

            for (int i = 0; i < limbs.Length && !partsRigActive; i++)
            {
                if (legSpritesActive)
                {
                    sLeaser.sprites[LimbSprite(i)] = new FSprite(LegElement(bp, i));
                    continue;
                }

                var tris = new TriangleMesh.Triangle[10]
                {
                    new TriangleMesh.Triangle(0, 1, 2),
                    new TriangleMesh.Triangle(1, 2, 3),
                    new TriangleMesh.Triangle(3, 4, 2),
                    new TriangleMesh.Triangle(4, 2, 5),
                    new TriangleMesh.Triangle(2, 5, 6),
                    new TriangleMesh.Triangle(5, 6, 7),
                    new TriangleMesh.Triangle(5, 7, 8),
                    new TriangleMesh.Triangle(8, 7, 9),
                    new TriangleMesh.Triangle(8, 9, 10),
                    new TriangleMesh.Triangle(8, 9, 11),
                };
                sLeaser.sprites[LimbSprite(i)] = new TriangleMesh("Futile_White", tris, customColor: false);
            }

            sLeaser.sprites[SpriteTail] =
                TriangleMesh.MakeLongMesh(tail.Length, pointyTip: true, customColor: true);
            if (tailSpriteActive) BindTailArt(sLeaser.sprites[SpriteTail] as TriangleMesh, bp);

            if (partsRigActive)
            {
            }
            else if (headSpriteActive)
            {
                var headSprite = new FSprite(bp.HeadSpriteElement);
                headSprite.anchorX = bp.HeadSpriteAnchorX;
                headSprite.anchorY = bp.HeadSpriteAnchorY;
                sLeaser.sprites[SpriteHead] = headSprite;
                if (headEyeActive)
                {
                    var eyeSprite = new FSprite(bp.HeadEyeElement);
                    eyeSprite.anchorX = bp.HeadSpriteAnchorX;
                    eyeSprite.anchorY = bp.HeadSpriteAnchorY;
                    sLeaser.sprites[SpriteEye] = eyeSprite;
                }
                else
                {
                    sLeaser.sprites[SpriteEye] = new FSprite("Futile_White");
                    sLeaser.sprites[SpriteEye].isVisible = false;
                }
            }
            else
            {
                var headTris = new TriangleMesh.Triangle[HEAD_QUADS * 2];
                for (int q = 0; q < HEAD_QUADS; q++)
                {
                    int b = q * 4;
                    headTris[q * 2] = new TriangleMesh.Triangle(b, b + 1, b + 2);
                    headTris[q * 2 + 1] = new TriangleMesh.Triangle(b + 1, b + 2, b + 3);
                }
                sLeaser.sprites[SpriteHead] = new TriangleMesh("Futile_White", headTris, customColor: false);

                var eyeTris = new TriangleMesh.Triangle[2]
                {
                    new TriangleMesh.Triangle(0, 1, 2),
                    new TriangleMesh.Triangle(1, 2, 3),
                };
                sLeaser.sprites[SpriteEye] = new TriangleMesh("Futile_White", eyeTris, customColor: false);
            }

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

        private int LimbSprite(int i) { return SpriteLimbsStart + i * LimbSpriteCount; }

        private const int HEAD_QUADS = 7;

        public Vector2 DrawHeadPos { get; private set; }

        public Vector2 DrawHeadDir { get; private set; }

        private bool headDrawGuard;

        public Vector2 DrawHeadUp { get; private set; }

        public Vector2 DrawNosePos { get; private set; }

        public Vector2 DrawNeckPos { get; private set; }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
                                         float timeStacker, Vector2 camPos)
        {
            FoxBreedParams p = fox.P;

            RestoreCamoColors(sLeaser);

            Vector2 headChunk = Vector2.Lerp(drawPositions[0, 1], drawPositions[0, 0], timeStacker);
            Vector2 midChunk = Vector2.Lerp(drawPositions[1, 1], drawPositions[1, 0], timeStacker);
            Vector2 rearChunk = Vector2.Lerp(drawPositions[2, 1], drawPositions[2, 0], timeStacker);
            Vector2 drawAxis = DrawAxis(headChunk, rearChunk);
            Vector2 drawUp = DrawUp(headChunk, rearChunk);
            float depth = Mathf.Lerp(lastHeadDepth, headDepth, timeStacker);

            Vector2 aOrigin, aFwd, aUp; float aSx, aSy;
            BuildArtFrame(p, rearChunk, headChunk, depth, out aOrigin, out aFwd, out aUp, out aSx, out aSy);
            float faceSign = (depth >= 0f) ? 1f : -1f;

            ComputeHeadFrame(p, timeStacker, headChunk, midChunk, rearChunk, drawUp, depth);
            if (partsRigActive)
            {
                DrawPartsRig(sLeaser, p, timeStacker, headChunk, midChunk, rearChunk, depth, camPos);
            }
            if (bodySpriteActive)
            {
                DrawHeadPos = ArtPoint(aOrigin, aFwd, aUp, aSx, aSy, ART_NECK_X, ART_SPINE_Y);
                DrawNeckPos = DrawHeadPos;
                DrawNosePos = DrawHeadPos + DrawHeadDir * (ART_BODY_W * 0.25f * aSy);
            }

            Vector2 bez = midChunk * 2f - (headChunk + rearChunk) * 0.5f;
            for (int i = 0; i < bodyMeshPoints; i++)
            {
                float u = (bodyMeshPoints == 1) ? 0.5f : (float)i / (bodyMeshPoints - 1);
                spinePos[i] = Bezier(headChunk, bez, rearChunk, u);
                spineRad[i] = SpineHalfWidth(p, u);
            }

            if (bodySpriteActive)
            {
                FSprite bodyArt = sLeaser.sprites[SpriteBodyMesh];
                bodyArt.x = aOrigin.x - camPos.x;
                bodyArt.y = aOrigin.y - camPos.y;
                bodyArt.rotation = AimDeg(aFwd) - 90f * faceSign;
                bodyArt.scaleX = aSx * faceSign * TurnForeshorten(p, depth);
                bodyArt.scaleY = aSy;
                bodyArt.color = BodyArtTint(p, p.BodySpriteMask);
            }

            var bodyMesh = partsRigActive ? null : sLeaser.sprites[SpriteBodyMesh] as TriangleMesh;
            if (bodyMesh != null)
            {
                Vector2 prev = DrawNeckPos;
                float prevW = p.BodyFrontCap;
                for (int i = 0; i < bodyMeshPoints; i++)
                {
                    Vector2 here = spinePos[i];
                    Vector2 seg = prev - here;
                    float dist = seg.magnitude;
                    Vector2 dir = (dist > 0.01f) ? seg / dist : drawAxis;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float w = spineRad[i];
                    Vector2 lead = here + dir * (dist * (1f + p.BodyMeshOverlap));

                    int b = i * 4;
                    bodyMesh.MoveVertice(b, lead - perp * prevW - camPos);
                    bodyMesh.MoveVertice(b + 1, lead + perp * prevW - camPos);
                    bodyMesh.MoveVertice(b + 2, here - perp * w - camPos);
                    bodyMesh.MoveVertice(b + 3, here + perp * w - camPos);

                    prevW = w;
                    prev = here;
                }
                bodyMesh.color = p.ColorBody;
            }

            for (int i = 0; i < bodyCircleCount; i++)
            {
                Vector2 pos = Bezier(headChunk, bez, rearChunk, bodyCircleU[i]);
                FSprite s = sLeaser.sprites[SpriteBodyFillStart + i];
                s.x = pos.x - camPos.x;
                s.y = pos.y - camPos.y;
                bodyCircleRad[i] = SpineHalfWidth(p, bodyCircleU[i]);
                s.scale = bodyCircleRad[i] / 10f;
                s.color = p.ColorBody;
            }

            for (int i = 0; i < limbs.Length && !partsRigActive; i++)
            {
                Vector2 paw = Vector2.Lerp(limbs[i].lastPos, limbs[i].pos, timeStacker);
                bool front = i < 2;
                Vector2 shoulder;
                if (bodySpriteActive)
                {
                    shoulder = ArtPoint(aOrigin, aFwd, aUp, aSx, aSy, ART_LEG_X[i], 0f)
                               - aUp * p.LimbAttachDrop;
                }
                else
                {
                    Vector2 from = front ? headChunk : rearChunk;
                    float along = front ? p.LimbAttachFront : p.LimbAttachRear;
                    shoulder = Vector2.Lerp(from, midChunk, Mathf.Clamp01(along)) - drawUp * p.LimbAttachDrop;
                }

                float bend = Mathf.Lerp(limbs[i].lastBend, limbs[i].bend, timeStacker);

                if (legSpritesActive)
                {
                    DrawSpriteLeg(sLeaser, p, i, shoulder, paw, depth, camPos);
                    continue;
                }
                DrawMeshLeg(sLeaser, p, i, shoulder, paw, bend, drawAxis, camPos);
            }

            var tailMesh = sLeaser.sprites[SpriteTail] as TriangleMesh;
            if (tailMesh != null)
            {
                Vector2 prev;
                if (partsRigActive)
                {
                    Vector2 tChest, tFwd, tUp; float tSign;
                    BuildPartsFrame(p, rearChunk, midChunk, headChunk, depth,
                                    out tChest, out tFwd, out tUp, out tSign);
                    Vector2 tRear = midChunk + (rearChunk - midChunk) * TurnForeshorten(p, depth);
                    prev = PartsTailRoot(p, tRear, tFwd) - tUp * p.TailRootDrop;
                }
                else
                {
                    prev = bodySpriteActive
                        ? ArtPoint(aOrigin, aFwd, aUp, aSx, aSy, ART_TAIL_X, ART_SPINE_Y) - aUp * p.TailRootDrop
                        : rearChunk - drawUp * p.TailRootDrop;
                }
                float tailSq = TailSqueezeFac(p);
                float prevRad = (bodySpriteActive || partsRigActive)
                    ? TailRad(0) * tailSq
                    : SpineHalfWidth(p, 1f);
                if (tailSpriteActive)
                {
                    Vector2 first = Vector2.Lerp(tail[0].lastPos, tail[0].pos, timeStacker) - prev;
                    float side = (first.sqrMagnitude > 0.01f)
                        ? Mathf.Sign(Vector2.Dot(new Vector2(-first.y, first.x), aUp)) : 1f;
                    MapTailUV(tailMesh, tailMesh.element, side);
                }
                for (int i = 0; i < tail.Length; i++)
                {
                    Vector2 seg = Vector2.Lerp(tail[i].lastPos, tail[i].pos, timeStacker);
                    Vector2 dir = seg - prev;
                    float dist = dir.magnitude;
                    dir = (dist > 0.01f) ? dir / dist : -drawAxis;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float overlap = (tailSpriteActive || i == 0) ? 0f : dist * p.TailMeshOverlap;
                    Vector2 wBase = tailSpriteActive
                        ? perp * (TailRad(i) * tailSq)
                        : perp * ((prevRad + tail[i].StretchedRad * tailSq) * 0.5f);
                    tailMesh.MoveVertice(i * 4, prev - wBase + dir * overlap - camPos);
                    tailMesh.MoveVertice(i * 4 + 1, prev + wBase + dir * overlap - camPos);
                    if (i < tail.Length - 1)
                    {
                        Vector2 wTip = perp * ((tailSpriteActive ? TailRad(i) : tail[i].StretchedRad) * tailSq);
                        tailMesh.MoveVertice(i * 4 + 2, seg - wTip - dir * overlap - camPos);
                        tailMesh.MoveVertice(i * 4 + 3, seg + wTip - dir * overlap - camPos);
                    }
                    else
                    {
                        tailMesh.MoveVertice(i * 4 + 2, seg - camPos);
                    }
                    prevRad = (tailSpriteActive ? TailRad(i) : tail[i].StretchedRad) * tailSq;
                    prev = seg;
                }
                TintTail(tailMesh, p);
            }

            if (partsRigActive) { }
            else if (headSpriteActive) DrawHeadSprite(sLeaser, p, depth, camPos);
            else DrawHeadMesh(sLeaser, p, depth, camPos);

            for (int i = 0; i < cosmetics.Count; i++)
            {
                cosmetics[i].DrawSprites(sLeaser, rCam, timeStacker, camPos);
            }

            if (tobyFramesActive) DrawTobyFrame(sLeaser, p, headChunk, rearChunk, depth, camPos);

            ApplyCamoBlend(sLeaser, rCam, p, headChunk, midChunk, rearChunk);

            float deathAlpha = fox.DeathFadeAlpha;
            if (deathAlpha < 1f)
            {
                for (int i = 0; i < sLeaser.sprites.Length; i++)
                {
                    FSprite s = sLeaser.sprites[i];
                    if (s != null) s.alpha = deathAlpha;
                }
            }

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
                                      Vector2 headChunk, Vector2 midChunk, Vector2 rearChunk,
                                      Vector2 drawUp, float depth)
        {
            Vector2 headPart = Vector2.Lerp(head.lastPos, head.pos, timeStacker);
            DrawHeadPos = Vector2.Lerp(headPart, headChunk, 0.2f);

            Vector2 neckLine = Fox.Dir(midChunk, headChunk);
            Vector2 bodyFwd = Fox.Dir(rearChunk, headChunk);
            headDrawGuard = Fox.NeckDot(headChunk, midChunk, rearChunk) < 0f;
            if (headDrawGuard) neckLine = bodyFwd;
            Vector2 lookLine = Fox.Dir(headChunk, headPart);
            if (lookLine.sqrMagnitude < 0.01f) lookLine = neckLine;

            if (partsRigActive)
            {
                float deg = Mathf.Lerp(lastHeadPitchDeg, headPitchDeg, timeStacker);
                DrawHeadDir = PitchFromNeck(neckLine, Mathf.Clamp(deg, -p.HeadPitchMax, p.HeadPitchMax));
            }
            else
            {
                Vector2 want = Vector3.Slerp(neckLine, lookLine, Mathf.Clamp01(p.HeadLookFac));
                DrawHeadDir = ClampPitch(neckLine, want, p.HeadPitchMax);

                Vector2 up = new Vector2(-DrawHeadDir.y, DrawHeadDir.x);
                if (up.y < 0f) up = -up;
                Vector2 drooped = DrawHeadDir - up * p.HeadMuzzleDroop;
                float m = drooped.magnitude;
                if (m > 0.01f) DrawHeadDir = drooped / m;
            }
            DrawHeadUp = new Vector2(-DrawHeadDir.y, DrawHeadDir.x);
            if (DrawHeadUp.y < 0f) DrawHeadUp = -DrawHeadUp;

            float fore = Mathf.Lerp(Mathf.Clamp01(p.HeadForeshorten), 1f, Mathf.Abs(depth));
            DrawNosePos = DrawHeadPos + DrawHeadDir * (p.HeadMuzzleLength * p.HeadSize * fore);

            DrawNeckPos = DrawHeadPos - DrawHeadDir * (head.rad * 0.9f);
            if (!Custom.DistLess(DrawNeckPos, headChunk, head.rad * 2f))
            {
                DrawNeckPos = headChunk + Fox.Dir(headChunk, DrawNeckPos) * (head.rad * 2f);
            }
            if (drawUp.sqrMagnitude < 0.01f) drawUp = new Vector2(0f, 1f);
        }

        private static Vector2 ClampPitch(Vector2 reference, Vector2 want, float maxDeg)
        {
            float cosMax = Mathf.Cos(Mathf.Clamp(maxDeg, 0f, 180f) * Mathf.Deg2Rad);
            float dot = Vector2.Dot(reference, want);
            if (dot >= cosMax) return want.normalized;
            float ang = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
            float frac = (ang > 0.0001f) ? Mathf.Clamp01((maxDeg * Mathf.Deg2Rad) / ang) : 1f;
            Vector3 v = Vector3.Slerp(reference, want, frac);
            Vector2 r = new Vector2(v.x, v.y);
            return (r.sqrMagnitude > 0.0001f) ? r.normalized : reference;
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

        private void DrawMeshLeg(RoomCamera.SpriteLeaser sLeaser, FoxBreedParams p, int i,
                                 Vector2 shoulder, Vector2 paw, float bend, Vector2 drawAxis, Vector2 camPos)
        {
            var mesh = sLeaser.sprites[LimbSprite(i)] as TriangleMesh;
            if (mesh == null) return;

            float reach = Mathf.Max(2f, limbs[i].jointDist);
            float upper = reach * Mathf.Clamp(p.LimbUpperFac, 0.1f, 0.9f);
            float lower = reach - upper;

            Vector2 pawDir = drawAxis;
            Vector2 legVec = paw - shoulder;
            float legLen = legVec.magnitude;
            Vector2 legDir = (legLen > 0.01f) ? legVec / legLen : new Vector2(0f, -1f);
            Vector2 ankle = paw - pawDir * (p.LimbPawLength * 0.5f);

            Vector2 knee = Custom.InverseKinematic(shoulder, ankle, upper, lower, bend);
            Vector2 kneePerp = PerpOf(ankle - shoulder);
            knee += kneePerp * (p.LimbKneeBend * Mathf.Clamp(bend, -1f, 1f) * reach * 0.15f);

            float thick = p.LimbThickness * 0.5f;
            Vector2 wUpper = PerpOf(knee - shoulder) * thick;
            Vector2 wLower = PerpOf(ankle - knee) * (thick * 0.8f);
            Vector2 alongLower = (ankle - knee);
            float lowLen = alongLower.magnitude;
            alongLower = (lowLen > 0.01f) ? alongLower / lowLen : legDir;

            Vector2 pawPerp = PerpOf(pawDir);
            float pawHalf = thick * Mathf.Max(0.2f, p.LimbPawWidth);
            Vector2 toe = ankle + pawDir * p.LimbPawLength;

            mesh.MoveVertice(0, shoulder - wUpper - camPos);
            mesh.MoveVertice(1, shoulder + wUpper - camPos);
            mesh.MoveVertice(2, Vector2.Lerp(shoulder, ankle, 0.5f) - camPos);
            mesh.MoveVertice(3, knee - wUpper * 0.8f - camPos);
            mesh.MoveVertice(4, knee + wUpper * 0.8f - camPos);
            mesh.MoveVertice(5, ankle - alongLower * (thick * 0.5f) - camPos);
            mesh.MoveVertice(6, ankle - wLower - camPos);
            mesh.MoveVertice(7, ankle + wLower - camPos);
            mesh.MoveVertice(8, ankle - pawPerp * pawHalf - camPos);
            mesh.MoveVertice(9, ankle + pawPerp * pawHalf - camPos);
            mesh.MoveVertice(10, toe - pawPerp * (pawHalf * 0.7f) - camPos);
            mesh.MoveVertice(11, toe + pawPerp * (pawHalf * 0.7f) - camPos);
            mesh.color = p.ColorBody;
        }

        private void DrawSpriteLeg(RoomCamera.SpriteLeaser sLeaser, FoxBreedParams p, int i,
                                   Vector2 shoulder, Vector2 paw, float depth, Vector2 camPos)
        {
            FSprite s = sLeaser.sprites[LimbSprite(i)];
            Vector2 leg = paw - shoulder;
            float len = leg.magnitude;
            s.x = shoulder.x - camPos.x;
            s.y = shoulder.y - camPos.y;
            s.anchorX = 0.5f;
            s.anchorY = 1f;
            s.rotation = AimDeg(leg) + 180f;
            s.scaleY = (s.element != null && s.element.sourcePixelSize.y > 0.01f)
                ? len / s.element.sourcePixelSize.y : 1f;
            s.scaleX = (bodySpriteActive ? artScale : (p.LimbThickness / 3f)) * p.LegSpriteWidthFac
                       * TurnForeshorten(p, depth);
            if (depth < 0f) s.scaleX = -s.scaleX;
            s.color = BodyArtTint(p, p.LegSpriteMask);
        }

        private void DrawHeadSprite(RoomCamera.SpriteLeaser sLeaser, FoxBreedParams p, float depth, Vector2 camPos)
        {
            FSprite s = sLeaser.sprites[SpriteHead];
            bool facingRight = depth >= 0f;
            s.x = DrawNeckPos.x - camPos.x;
            s.y = DrawNeckPos.y - camPos.y;
            s.anchorX = p.HeadSpriteAnchorX;
            s.anchorY = p.HeadSpriteAnchorY;
            s.rotation = AimDeg(DrawHeadDir) + (facingRight ? -90f : 90f);
            float scale = Mathf.Max(0.01f, p.HeadSpriteScale * p.HeadSize
                                           * (bodySpriteActive ? artScale : 1f));
            s.scaleX = (facingRight ? scale : -scale) * TurnForeshorten(p, depth);
            s.scaleY = scale;
            s.color = HeadTint(p);

            FSprite eye = sLeaser.sprites[SpriteEye];
            if (headEyeActive)
            {
                eye.x = s.x;
                eye.y = s.y;
                eye.anchorX = s.anchorX;
                eye.anchorY = s.anchorY;
                eye.rotation = s.rotation;
                eye.scaleX = s.scaleX;
                eye.scaleY = s.scaleY;
                eye.color = InkEyeTint(p, EyeColor(p));
            }
            else
            {
                eye.scaleX = 0f;
                eye.scaleY = 0f;
            }
        }

        private static Color HeadTint(FoxBreedParams p)
        {
            if (p.HeadSpriteMask) return p.ColorHead;
            const float ART_GREY = 0x97 / 255f;
            return new Color(Mathf.Clamp01(p.ColorHead.r / ART_GREY),
                             Mathf.Clamp01(p.ColorHead.g / ART_GREY),
                             Mathf.Clamp01(p.ColorHead.b / ART_GREY));
        }

        private void DrawHeadMesh(RoomCamera.SpriteLeaser sLeaser, FoxBreedParams p, float depth, Vector2 camPos)
        {
            var mesh = sLeaser.sprites[SpriteHead] as TriangleMesh;
            if (mesh == null) return;

            float skull = head.rad;
            float fore = Mathf.Lerp(Mathf.Clamp01(p.HeadForeshorten), 1f, Mathf.Abs(depth));
            float muzzle = p.HeadMuzzleLength * p.HeadSize * fore;
            Vector2 dir = DrawHeadDir;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            Vector2 prev = DrawHeadPos - dir * skull;
            float prevW = 0.05f * skull;
            for (int q = 0; q < HEAD_QUADS; q++)
            {
                float t = (float)(q + 1) / HEAD_QUADS;
                float x = Mathf.Lerp(-skull, muzzle, t);
                float w = HeadHalfWidth(p, x, skull, muzzle);
                Vector2 here = DrawHeadPos + dir * x;
                int b = q * 4;
                mesh.MoveVertice(b, prev - perp * prevW - camPos);
                mesh.MoveVertice(b + 1, prev + perp * prevW - camPos);
                mesh.MoveVertice(b + 2, here - perp * w - camPos);
                mesh.MoveVertice(b + 3, here + perp * w - camPos);
                prev = here;
                prevW = w;
            }
            mesh.color = p.ColorTail;

            var eye = sLeaser.sprites[SpriteEye] as TriangleMesh;
            if (eye == null) return;
            Vector2 centre = DrawHeadPos + dir * (p.EyeAlong * p.HeadSize) + DrawHeadUp * (p.EyeUp * p.HeadSize);
            Vector2 half = dir * (p.EyeLength * 0.5f * p.HeadSize);
            Vector2 thick = DrawHeadUp * (p.EyeThickness * 0.5f * p.HeadSize);
            eye.MoveVertice(0, centre - half - thick - camPos);
            eye.MoveVertice(1, centre - half + thick - camPos);
            eye.MoveVertice(2, centre + half - thick - camPos);
            eye.MoveVertice(3, centre + half + thick - camPos);
            eye.color = NoHole(p, EyeColor(p));
        }

        private static float HeadHalfWidth(FoxBreedParams p, float x, float skull, float muzzle)
        {
            float circle = (Mathf.Abs(x) < skull)
                ? skull * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / skull) * (x / skull)))
                : 0f;
            if (x <= 0f) return Mathf.Max(0.05f * skull, circle);
            float wedge = Mathf.Lerp(skull * p.HeadMuzzleWidth, skull * p.HeadMuzzleTip,
                                     Mathf.Clamp01(muzzle > 0.01f ? x / muzzle : 1f));
            return Mathf.Max(circle, wedge);
        }

        private void TintTail(TriangleMesh mesh, FoxBreedParams p)
        {
            if (mesh.verticeColors == null)
            {
                mesh.color = p.ColorTail;
                return;
            }
            int n = mesh.verticeColors.Length;
            if (partsRigActive && !p.PartsMask)
            {
                for (int v = 0; v < n; v++) mesh.verticeColors[v] = Color.white;
                return;
            }
            for (int v = 0; v < n; v++)
            {
                float t = (n > 1) ? (float)v / (n - 1) : 1f;
                mesh.verticeColors[v] = Color.Lerp(p.ColorTailRoot, p.ColorTail, t);
            }
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

        private static Vector2 PerpOf(Vector2 v)
        {
            float m = v.magnitude;
            if (m < 0.0001f) return new Vector2(1f, 0f);
            return new Vector2(-v.y / m, v.x / m);
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

            if (tobyFramesActive)
            {
                AddTobyFrameToContainer(sLeaser, newContatiner);
                return;
            }

            AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.Behind);

            if (partsRigActive)
            {
                AddPartsToContainer(sLeaser, newContatiner);
                AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.BodySurface);
                AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.BehindHead);
                if (sLeaser.containers != null && sLeaser.containers.Length > 0)
                {
                    newContatiner.AddChild(sLeaser.containers[0]);
                }
                AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.InFront);
                return;
            }

            for (int i = 0; i < limbs.Length; i += 2)
            {
                newContatiner.AddChild(sLeaser.sprites[LimbSprite(i)]);
            }

            newContatiner.AddChild(sLeaser.sprites[SpriteTail]);

            for (int i = SpriteBodyFillStart; i < SpriteBodyFillEnd; i++)
            {
                newContatiner.AddChild(sLeaser.sprites[i]);
            }
            newContatiner.AddChild(sLeaser.sprites[SpriteBodyMesh]);

            AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.BodySurface);

            for (int i = 1; i < limbs.Length; i += 2)
            {
                newContatiner.AddChild(sLeaser.sprites[LimbSprite(i)]);
            }

            AddLayer(sLeaser, rCam, newContatiner, FoxCosmetic.ZOrder.BehindHead);

            newContatiner.AddChild(sLeaser.sprites[SpriteHead]);
            newContatiner.AddChild(sLeaser.sprites[SpriteEye]);

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
