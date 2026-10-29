using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public partial class FoxGraphics
    {
        private const float P_CHEST_W = 14f, P_CHEST_H = 13f;
        private const float P_BODY_W = 23f, P_BODY_H = 12f;
        private const float P_TAIL_W = 38f, P_TAIL_H = 15f;
        private const float P_HEAD_W = 24f;
        private const float P_LEGA_UPPER_W = 13f, P_LEGA_UPPER_H = 5f;
        private const float P_LEGA_JOINT_W = 4f;
        private const float P_LEGA_LOWER_W = 10f, P_LEGA_LOWER_H = 4f;
        private const float P_LEGA_PAW_W = 7f, P_LEGA_PAW_H = 4f;
        private const float P_LEGB_HAUNCH_W = 6f, P_LEGB_HAUNCH_H = 8f;
        private const float P_LEGB_UPPER_W = 7f, P_LEGB_UPPER_H = 4f;
        private const float P_LEGB_JOINT_W = 3f;
        private const float P_LEGB_LOWER_W = 6f, P_LEGB_LOWER_H = 3f;
        private const float P_LEGB_PAW_W = 7f, P_LEGB_PAW_H = 4f;

        private const int PARTS_LEG_SPRITES = 7;
        private const int LEG_HAUNCH = 0, LEG_UPPER = 1, LEG_JOINT = 2, LEG_LOWER = 3,
                          LEG_JOINT2 = 4, LEG_META = 5, LEG_PAW = 6;

        private const int HEAD_SIDE = 0, HEAD_TQ = 1, HEAD_AWAY = 2;
        private static readonly string[] HEAD_PART_NAMES = { "HeadSide", "HeadThreeQuarter", "HeadAway" };

        internal bool partsRigActive;

        private int LimbSpriteCount { get { return partsRigActive ? PARTS_LEG_SPRITES : 1; } }

        private float partsScale = 1f;

        private FAtlasElement[] partsHeadEl, partsEyeEl;

        private Vector2[] partsKneeDrawn;
        private bool[] partsKneeSeeded;
        private int[] partsKneeClamps;

        private int partsHeadFrame = HEAD_SIDE;
        private bool headTurnTowardCamera = true;
        private float lastHeadDepthForTrend;
        private bool headFrameDeep;
        private int headFrameDwell;
        private int headFrameHeld;
        private int headFrameSuppressed;
        private int headFrameDwellLeftAtFirst;

        private float travelDist;
        private Vector2 lastTravelPos;

        public float GaitOdometer { get { return travelDist; } }

        public float GaitSpeed
        {
            get { return partsRigActive ? speedSmooth : fox.bodyChunks[1].vel.magnitude; }
        }

        public Vector2 GaitVelocity
        {
            get { return partsRigActive ? travelVel : fox.bodyChunks[1].vel; }
        }
        private Vector2 travelVel;

        private bool gaitWalk4;

        private const float GAIT_WALK_HYSTERESIS = 1.15f;

        private bool travelSeeded;
        private float speedSmooth;
        private float waveGround = 1f;
        private float breathTicks, swayTicks;
        private float leanDeg, lastLeanDeg, lastVelAlong;
        private float wavePitchDeg, lastWavePitchDeg;
        private float pairBlend;
        private bool gaitPaired;
        private int glanceTicks;
        private float glanceDir;
        private uint glanceRng;

        private const int HL_NEUTRAL = 0, HL_SNIFF = 1, HL_SCAN = 2, HL_NOISE = 3, HL_TARGET = 4, HL_TURN = 5,
                          HL_SCENT = 6;
        private static readonly string[] HEAD_LOOK_NAMES =
            { "neutral", "sniff", "scan", "noise", "target", "turn", "scent" };

        private int scentSniffServing;
        private int scentSniffLeft;
        private float scentSniffPeakPitch;
        private int headMode = HL_NEUTRAL;
        private int headModeTicks;
        private int headLookCooldown;
        private float headPitchDeg, lastHeadPitchDeg;
        private float headYaw;
        private float headScanPhase;

        private Vector2 blindScatterOffset;
        private int blindScatterHold;
        private bool blindScatterSeeded;
        private Vector2 headNoisePos;
        private int headNoiseHold;
        private Vector2 headLookPos;
        private bool headLookHasPos;
        private string headLookWhat = "none";
        private bool headPitchSeeded;
        private const int HEAD_LOOK_LOG_TICKS = 40;
        private int headLookLogCounter;
        private int lastTurnSwapCount;

        private bool partsBodyMeshActive;
        private int partsBodyQuads = 4, partsChestQuads = 3;
        private float bodyBend, lastBodyBend;
        private Vector2 midLagPos;
        private bool midLagSeeded;
        private float bodySquash, lastBodySquash;
        private float bodyLenRatio = 1f;
        private float landImpact;
        private float lastAirVy;
        private bool lastGroundedForSquash = true;
        private float flexGeom, flexMotion, squashGeom, squashMotion;
        private float accelSmooth;

        private bool ResolvePartsRig(FoxBreedParams p)
        {
            if (!p.PartsRig) return false;
            bool ok = true;
            for (int i = 0; i < 3; i++) ok &= PartsRequire(p, HEAD_PART_NAMES[i]);
            ok &= PartsRequire(p, "Chest") & PartsRequire(p, "Body") & PartsRequire(p, "Tail");
            ok &= PartsRequire(p, "LegA_Upper") & PartsRequire(p, "LegA_Joint")
                  & PartsRequire(p, "LegA_Lower") & PartsRequire(p, "LegA_Paw");
            ok &= PartsRequire(p, "LegB_Haunch") & PartsRequire(p, "LegB_Upper") & PartsRequire(p, "LegB_Joint")
                  & PartsRequire(p, "LegB_Lower") & PartsRequire(p, "LegB_Paw");
            return ok;
        }

        private bool PartsRequire(FoxBreedParams p, string part)
        {
            string el = PartsEl(p, part);
            if (FoxAtlas.Has(el)) return true;
            return false;
        }

        private static string PartsEl(FoxBreedParams p, string part)
        {
            return p.PartsPrefix + part + (p.PartsMask ? "Mask" : "");
        }

        private static Color PartsTint(FoxBreedParams p, Color variantColor)
        {
            return p.PartsMask ? variantColor : Color.white;
        }

        private void BuildPartsRig(FoxBreedParams p)
        {
            partsScale = (artRestSpine / Mathf.Max(1f, p.PartsRefSpine)) * Mathf.Max(0.05f, p.PartsScaleFac);
            partsBodyMeshActive = p.PartsBodyMesh;
            partsBodyQuads = Mathf.Clamp(p.PartsBodyQuads, 1, 24);
            partsChestQuads = Mathf.Clamp(p.PartsChestQuads, 1, 24);
            partsHeadEl = new FAtlasElement[3];
            partsEyeEl = new FAtlasElement[3];
            for (int i = 0; i < 3; i++)
            {
                partsHeadEl[i] = FoxAtlas.Element(PartsEl(p, HEAD_PART_NAMES[i]));
                partsEyeEl[i] = FoxAtlas.Element(p.PartsPrefix + HEAD_PART_NAMES[i] + "Eye");
            }
            glanceRng = (uint)(fox.abstractCreature.ID.RandomSeed ^ 0x5F3A7C1);
            if (glanceRng == 0u) glanceRng = 1u;
        }

        private float NextGlanceRandom()
        {
            glanceRng ^= glanceRng << 13;
            glanceRng ^= glanceRng >> 17;
            glanceRng ^= glanceRng << 5;
            return (glanceRng & 0xFFFFFF) / 16777216f;
        }

        private void BuildPartsFrame(FoxBreedParams p, Vector2 rear, Vector2 mid, Vector2 front, float depth,
                                     out Vector2 chest, out Vector2 fwd, out Vector2 up, out float faceSign)
        {
            chest = Vector2.Lerp(front, mid, Mathf.Clamp01(p.PartsChestAlong));
            Vector2 span = chest - rear;
            float len = span.magnitude;
            fwd = (len > 0.01f) ? span / len : new Vector2(1f, 0f);
            faceSign = (depth >= 0f) ? 1f : -1f;
            up = new Vector2(-fwd.y, fwd.x) * faceSign;
            chest -= up * p.PartsChestDrop;
        }

        private void PartsFrameTick(FoxBreedParams p, out Vector2 chest, out Vector2 fwd,
                                    out Vector2 up, out float faceSign)
        {
            BuildPartsFrame(p, drawPositions[2, 0], drawPositions[1, 0], drawPositions[0, 0], headDepth,
                            out chest, out fwd, out up, out faceSign);
        }

        private Vector2 PartsLegRoot(FoxBreedParams p, int i, Vector2 chest, Vector2 fwd, Vector2 up)
        {
            Vector2 rear = drawPositions[2, 0];
            return PartsLegRootFrom(p, i, rear, chest, fwd, up);
        }

        private Vector2 PartsLegRootFrom(FoxBreedParams p, int i, Vector2 rear, Vector2 chest,
                                         Vector2 fwd, Vector2 up)
        {
            float span = Vector2.Distance(rear, chest);
            float fore = ((i % 2 == 0) ? -1f : 1f) * p.PartsLegForeOffset;
            if (i < 2)
            {
                return rear + fwd * (span * p.PartsLegRootFront + fore)
                       - up * (P_CHEST_H * 0.30f * partsScale);
            }
            Vector2 hip = rear + fwd * (span * p.PartsLegRootHind + fore)
                          - up * (P_BODY_H * 0.25f * partsScale);
            return p.PartsHaunchInChain
                ? hip + PartsHaunchDir(p, fwd, up) * (P_LEGB_HAUNCH_H * partsScale)
                : hip;
        }

        private static Vector2 PartsHaunchDir(FoxBreedParams p, Vector2 fwd, Vector2 up)
        {
            float a = p.PartsHaunchLean * Mathf.Deg2Rad;
            Vector2 d = -up * Mathf.Cos(a) + fwd * Mathf.Sin(a);
            return (d.sqrMagnitude > 0.0001f) ? d.normalized : -up;
        }

        private float PartsLegRestChain(FoxBreedParams p, int i)
        {
            return LegRootHeight(p, i) * Mathf.Max(0.2f, p.PartsLegSlack);
        }

        private float LegRootHeight(FoxBreedParams p, int i)
        {
            float stand = RideHeightOffPole(p) + fox.bodyChunks[1].rad;
            float rootH;
            if (i < 2)
            {
                rootH = stand - P_CHEST_H * 0.30f * partsScale;
            }
            else
            {
                rootH = stand - P_BODY_H * 0.25f * partsScale;
                if (p.PartsHaunchInChain)
                {
                    rootH -= P_LEGB_HAUNCH_H * partsScale
                             * Mathf.Cos(p.PartsHaunchLean * Mathf.Deg2Rad);
                }
            }
            return Mathf.Max(4f, rootH);
        }

        public float LegGroundReach(int i)
        {
            if (!partsRigActive || fox == null || limbs == null || i < 0 || i >= limbs.Length) return 0f;
            return LegRootHeight(fox.P, i);
        }

        private Vector2 poleFrameFwd = new Vector2(0f, 1f), poleFrameUp = new Vector2(1f, 0f);
        private bool poleFrameOk;

        internal float PoleBodySide { get { return poleBodySide; } }
        private float poleBodySide = 1f;

        private const float POLE_SIDE_BAND = 0.3f;

        private const float POLE_REACH_MARGIN_PX = 0.5f;

        private void PublishPoleFrame(Vector2 fwd, Vector2 up, bool valid)
        {
            poleFrameFwd = fwd;
            poleFrameUp = up;
            poleFrameOk = valid;
            if (fox == null || !fox.PoleHeld) return;
            float x = BodyUp.x;
            if (Mathf.Abs(x) >= POLE_SIDE_BAND) poleBodySide = (x < 0f) ? -1f : 1f;
        }

        internal bool PoleStrokeWindow(int i, Vector2 root, Vector2 origin, Vector2 along,
                                       out float lo, out float hi)
        {
            lo = 0f;
            hi = 0f;
            if (!partsRigActive || !poleFrameOk || fox == null || limbs == null
                || i < 0 || i >= limbs.Length) return false;
            FoxBreedParams p = fox.P;
            if (p == null) return false;

            LegPose q = SolveLegPose(p, i, root, origin, poleFrameFwd, poleFrameUp, 0f, 0f, 0f, false);
            Vector2 jointOffPaw = q.distal - q.paw;
            float limit = q.reachLimit - POLE_REACH_MARGIN_PX;
            if (limit <= 0f) return false;

            Vector2 w = root - origin - jointOffPaw;
            float centre = Vector2.Dot(w, along);
            float acrossSq = w.sqrMagnitude - centre * centre;
            float disc = limit * limit - acrossSq;
            if (disc <= 0f) return false;
            float half = Mathf.Sqrt(disc);
            lo = centre - half;
            hi = centre + half;
            return true;
        }

        private float PartsLegChain(FoxBreedParams p, int i, float swing)
        {
            return PartsLegRestChain(p, i) * Mathf.Lerp(1f, Mathf.Max(1f, p.StepSwingFold), swing);
        }

        private static float PartsLegReachLimit(FoxBreedParams p, float chain)
        {
            return chain * Mathf.Clamp(p.PartsLegMaxExtend, 0.3f, 0.99f);
        }

        private Vector2 PartsTailRoot(FoxBreedParams p, Vector2 rear, Vector2 fwd)
        {
            return rear + fwd * p.PartsTailRootOverlap;
        }

        private Vector2 PartsNeck(FoxBreedParams p, Vector2 chest, Vector2 fwd)
        {
            return chest + fwd * (P_CHEST_W * p.PartsNeckFac * partsScale);
        }

        private void InitiatePartsSprites(RoomCamera.SpriteLeaser sLeaser, FoxBreedParams p)
        {
            if (partsBodyMeshActive)
            {
                sLeaser.sprites[SpriteBodyMesh] = MakeStripMesh(PartsEl(p, "Body"), partsBodyQuads);
                sLeaser.sprites[SpriteChest] = MakeStripMesh(PartsEl(p, "Chest"), partsChestQuads);
            }
            else
            {
                var body = new FSprite(PartsEl(p, "Body"));
                body.anchorX = 0f;
                body.anchorY = 0.5f;
                sLeaser.sprites[SpriteBodyMesh] = body;

                var chest = new FSprite(PartsEl(p, "Chest"));
                chest.anchorX = 0.5f;
                chest.anchorY = 0.5f;
                sLeaser.sprites[SpriteChest] = chest;
            }

            for (int i = 0; i < limbs.Length; i++)
            {
                bool front = i < 2;
                int b = LimbSprite(i);
                if (front)
                {
                    var park = new FSprite("Futile_White");
                    park.scaleX = 0f;
                    park.scaleY = 0f;
                    sLeaser.sprites[b + LEG_HAUNCH] = park;
                    sLeaser.sprites[b + LEG_UPPER] = new FSprite(PartsEl(p, "LegA_Upper"));
                    sLeaser.sprites[b + LEG_JOINT] = new FSprite(PartsEl(p, "LegA_Joint"));
                    sLeaser.sprites[b + LEG_LOWER] = new FSprite(PartsEl(p, "LegA_Lower"));
                    sLeaser.sprites[b + LEG_JOINT2] = new FSprite(PartsEl(p, "LegA_Joint"));
                    sLeaser.sprites[b + LEG_META] = new FSprite(PartsEl(p, "LegA_Lower"));
                    sLeaser.sprites[b + LEG_PAW] = new FSprite(PartsEl(p, "LegA_Paw"));
                }
                else
                {
                    sLeaser.sprites[b + LEG_HAUNCH] = new FSprite(PartsEl(p, "LegB_Haunch"));
                    sLeaser.sprites[b + LEG_UPPER] = new FSprite(PartsEl(p, "LegB_Upper"));
                    sLeaser.sprites[b + LEG_JOINT] = new FSprite(PartsEl(p, "LegB_Joint"));
                    sLeaser.sprites[b + LEG_LOWER] = new FSprite(PartsEl(p, "LegB_Lower"));
                    sLeaser.sprites[b + LEG_JOINT2] = new FSprite(PartsEl(p, "LegB_Joint"));
                    sLeaser.sprites[b + LEG_META] = new FSprite(PartsEl(p, "LegB_Lower"));
                    sLeaser.sprites[b + LEG_PAW] = new FSprite(PartsEl(p, "LegB_Paw"));
                }
            }

            var headSprite = new FSprite(PartsEl(p, HEAD_PART_NAMES[partsHeadFrame]));
            headSprite.anchorX = p.HeadSpriteAnchorX;
            headSprite.anchorY = p.HeadSpriteAnchorY;
            sLeaser.sprites[SpriteHead] = headSprite;

            FAtlasElement eyeEl = partsEyeEl[partsHeadFrame];
            var eye = new FSprite(eyeEl != null ? eyeEl.name : "Futile_White");
            eye.anchorX = p.HeadSpriteAnchorX;
            eye.anchorY = p.HeadSpriteAnchorY;
            if (eyeEl == null) { eye.scaleX = 0f; eye.scaleY = 0f; }
            sLeaser.sprites[SpriteEye] = eye;
        }

        private static TriangleMesh MakeStripMesh(string element, int quads)
        {
            var tris = new TriangleMesh.Triangle[quads * 2];
            for (int q = 0; q < quads; q++)
            {
                int b = q * 4;
                tris[q * 2] = new TriangleMesh.Triangle(b, b + 1, b + 2);
                tris[q * 2 + 1] = new TriangleMesh.Triangle(b + 1, b + 2, b + 3);
            }
            var mesh = new TriangleMesh(FoxAtlas.Has(element) ? element : "Futile_White",
                                        tris, customColor: false);
            MapStripUV(mesh, quads);
            return mesh;
        }

        private static void MapStripUV(TriangleMesh mesh, int quads)
        {
            if (mesh == null || mesh.UVvertices == null || mesh.element == null) return;
            FAtlasElement el = mesh.element;
            for (int q = 0; q < quads && (q * 4 + 3) < mesh.UVvertices.Length; q++)
            {
                float u0 = (float)q / quads;
                float u1 = (float)(q + 1) / quads;
                float x0 = Mathf.Lerp(el.uvBottomLeft.x, el.uvTopRight.x, u0);
                float x1 = Mathf.Lerp(el.uvBottomLeft.x, el.uvTopRight.x, u1);
                int b = q * 4;
                mesh.UVvertices[b] = new Vector2(x0, el.uvBottomLeft.y);
                mesh.UVvertices[b + 1] = new Vector2(x0, el.uvTopRight.y);
                mesh.UVvertices[b + 2] = new Vector2(x1, el.uvBottomLeft.y);
                mesh.UVvertices[b + 3] = new Vector2(x1, el.uvTopRight.y);
            }
        }

        private void AddPartsToContainer(RoomCamera.SpriteLeaser sLeaser, FContainer c)
        {
            for (int i = 0; i < limbs.Length; i += 2) AddLegSprites(sLeaser, c, i);
            c.AddChild(sLeaser.sprites[SpriteTail]);
            c.AddChild(sLeaser.sprites[SpriteBodyMesh]);
            c.AddChild(sLeaser.sprites[SpriteChest]);
            for (int i = 1; i < limbs.Length; i += 2) AddLegSprites(sLeaser, c, i);
            c.AddChild(sLeaser.sprites[SpriteHead]);
            c.AddChild(sLeaser.sprites[SpriteEye]);
        }

        private void AddLegSprites(RoomCamera.SpriteLeaser sLeaser, FContainer c, int i)
        {
            int b = LimbSprite(i);
            for (int s = 0; s < PARTS_LEG_SPRITES; s++) c.AddChild(sLeaser.sprites[b + s]);
        }

        private void DrawPartsRig(RoomCamera.SpriteLeaser sLeaser, FoxBreedParams p, float timeStacker,
                                  Vector2 headChunk, Vector2 midChunk, Vector2 rearChunk,
                                  float depth, Vector2 camPos)
        {
            Vector2 chest, fwd, up; float faceSign;
            BuildPartsFrame(p, rearChunk, midChunk, headChunk, depth, out chest, out fwd, out up, out faceSign);
            float s = partsScale;
            float fore = TurnForeshorten(p, depth);
            Vector2 rearDraw = midChunk + (rearChunk - midChunk) * fore;
            chest = midChunk + (chest - midChunk) * fore;
            Color bodyTint = PartsTint(p, p.ColorBody);

            float breath = PartsBreath(p, timeStacker);
            float lean = Mathf.Lerp(lastLeanDeg, leanDeg, timeStacker) * faceSign;
            float turnProgress = fox.TurnProgress;
            if (turnProgress > 0f && p.PartsTurnLeanDeg != 0f)
            {
                lean += p.PartsTurnLeanDeg * Mathf.Sin(Mathf.Clamp01(turnProgress) * Mathf.PI) * faceSign;
            }

            float natural = P_BODY_W * s;
            float chordLen = Mathf.Max(1f, Vector2.Distance(rearDraw, chest));
            float want = chordLen * Mathf.Max(0.1f, p.PartsBodyReach);
            float stretch = Mathf.Clamp01(p.PartsBodyStretch);
            float drawnLen = Mathf.Clamp(want, natural * (1f - stretch), natural * (1f + stretch));
            Vector2 chestDraw = chest + up * (p.IdleBreathBob * breath);

            Vector2 fwdLean = (lean != 0f) ? Custom.DegToVec(AimDeg(fwd) + lean) : fwd;
            if (fwdLean.sqrMagnitude < 0.0001f) fwdLean = fwd;
            Vector2 nLean = new Vector2(-fwdLean.y, fwdLean.x);

            float bendDraw = Mathf.Lerp(lastBodyBend, bodyBend, timeStacker);
            float squashDraw = Mathf.Lerp(lastBodySquash, bodySquash, timeStacker);
            float squeezeDraw = fox.DrawSqueezeAmount;
            float widthFac = BodyWidthFac(p, squashDraw, squeezeDraw);

            var bodyStrip = sLeaser.sprites[SpriteBodyMesh] as TriangleMesh;
            var chestStrip = sLeaser.sprites[SpriteChest] as TriangleMesh;
            if (bodyStrip != null && chestStrip != null)
            {
                Vector2 bEnd = rearDraw + fwdLean * chordLen;
                Vector2 ctrl = (rearDraw + bEnd) * 0.5f + nLean * (2f * bendDraw);
                float tEnd = Mathf.Clamp(drawnLen / chordLen, 0.2f, 2f);

                float bodyHalf = PartsBodyHalf(p, s, breath, widthFac);
                WriteStrip(bodyStrip, rearDraw, ctrl, bEnd, 0f, tEnd, Vector2.zero,
                           bodyHalf, faceSign, camPos);
                bodyStrip.color = bodyTint;

                float h = (P_CHEST_W * s * 0.5f) / chordLen;
                Vector2 chestOffset = chestDraw - Bezier(rearDraw, ctrl, bEnd, 1f);
                float chestHalf = PartsChestHalf(p, s, breath, widthFac);
                WriteStrip(chestStrip, rearDraw, ctrl, bEnd, 1f - h, 1f + h, chestOffset,
                           chestHalf, faceSign, camPos);
                chestStrip.color = bodyTint;
            }
            else
            {
                FSprite body = sLeaser.sprites[SpriteBodyMesh];
                body.x = rearDraw.x - camPos.x;
                body.y = rearDraw.y - camPos.y;
                body.anchorX = 0f;
                body.anchorY = 0.5f;
                body.rotation = AimDeg(fwd) - 90f * faceSign + lean;
                body.scaleX = (drawnLen / P_BODY_W) * faceSign;
                body.scaleY = s * (1f + p.IdleBreathAmp * breath * 0.6f) * SqueezeDrawFac(p, squeezeDraw);
                body.color = bodyTint;

                FSprite chestSprite = sLeaser.sprites[SpriteChest];
                chestSprite.x = chestDraw.x - camPos.x;
                chestSprite.y = chestDraw.y - camPos.y;
                chestSprite.anchorX = 0.5f;
                chestSprite.anchorY = 0.5f;
                chestSprite.rotation = AimDeg(fwd) - 90f * faceSign + lean;
                chestSprite.scaleX = s * faceSign;
                chestSprite.scaleY = s * (1f + p.IdleBreathAmp * breath) * SqueezeDrawFac(p, squeezeDraw);
                chestSprite.color = bodyTint;
            }

            for (int i = 0; i < limbs.Length; i++)
            {
                Vector2 paw = ForeshortenAlong(
                    Vector2.Lerp(limbs[i].lastPos, limbs[i].pos, timeStacker), midChunk, fwd, up, fore);
                float swing = Mathf.Clamp01(Mathf.Lerp(limbs[i].lastSwing, limbs[i].swing, timeStacker));
                float bendSmooth = Mathf.Lerp(limbs[i].lastBend, limbs[i].bend, timeStacker);
                Vector2 root = PartsLegRootFrom(p, i, rearDraw, chest, fwd, up);
                if (p.PartsLegRootFollowBend && bendDraw != 0f)
                {
                    float tRoot = Mathf.Clamp01((i < 2) ? p.PartsLegRootFront : p.PartsLegRootHind);
                    root += nLean * (4f * bendDraw * tRoot * (1f - tRoot));
                }
                bool foldInterp = fox.PoleAmount <= 0f && !limbs[i].OnPole;
                float phaseLast = foldInterp ? limbs[i].LastSwingPhase : limbs[i].SwingPhase;
                DrawPartsLeg(sLeaser, p, i, root, paw, fwd, up, faceSign, swing, bendSmooth, camPos,
                             phaseLast, foldInterp ? timeStacker : 1f);
            }

            DrawPartsHead(sLeaser, p, chest, fwd, faceSign, fore, timeStacker, camPos);
        }

        private static float BodyWidthFac(FoxBreedParams p, float squash, float squeeze)
        {
            float rail = Mathf.Clamp01(p.PartsBodyWidthMax);
            float w = Mathf.Clamp(1f - squash * p.PartsBodySquashAmp, 1f - rail, 1f + rail);
            w = Mathf.Clamp(w, 0.3f, 2.5f);
            return w * SqueezeDrawFac(p, squeeze);
        }

        private static float SqueezeDrawFac(FoxBreedParams p, float squeeze)
        {
            return Mathf.Lerp(1f, Mathf.Clamp(p.SqueezeBodyFac, 0.05f, 1f), Mathf.Clamp01(squeeze));
        }

        private float PartsBreath(FoxBreedParams p, float timeStacker)
        {
            float calm = 1f - Mathf.Clamp01(speedSmooth / Mathf.Max(0.01f, p.PartsWaveSpeedFull));
            return (p.IdleBreathTicks > 1f)
                ? Mathf.Sin((breathTicks + timeStacker) * (Mathf.PI * 2f / p.IdleBreathTicks)) * calm
                : 0f;
        }

        private static float PartsBodyHalf(FoxBreedParams p, float s, float breath, float widthFac)
        {
            return P_BODY_H * s * 0.5f * (1f + p.IdleBreathAmp * breath * 0.6f) * widthFac;
        }

        private static float PartsChestHalf(FoxBreedParams p, float s, float breath, float widthFac)
        {
            return P_CHEST_H * s * 0.5f * (1f + p.IdleBreathAmp * breath) * widthFac;
        }

        private float PartsDrawnHalfExtent(FoxBreedParams p, float u, float timeStacker,
                                           Vector2 headChunk, Vector2 rearChunk, out bool onChest)
        {
            float s = partsScale;
            float breath = PartsBreath(p, timeStacker);
            float widthFac = partsBodyMeshActive
                ? BodyWidthFac(p, Mathf.Lerp(lastBodySquash, bodySquash, timeStacker), fox.DrawSqueezeAmount)
                : SqueezeDrawFac(p, fox.DrawSqueezeAmount);

            float bodyHalf = PartsBodyHalf(p, s, breath, widthFac);
            float chestHalf = PartsChestHalf(p, s, breath, widthFac);

            float spineLen = Mathf.Max(1f, Vector2.Distance(headChunk, rearChunk));
            float uChest = 0.5f * Mathf.Clamp01(p.PartsChestAlong);
            float uChestBack = uChest + (P_CHEST_W * s * 0.5f) / spineLen;

            onChest = u <= uChestBack;
            return onChest ? Mathf.Max(chestHalf, bodyHalf) : bodyHalf;
        }

        private static void SpineSample(Vector2 a, Vector2 ctrl, Vector2 b, float t,
                                        out Vector2 pos, out Vector2 dir)
        {
            float tc = Mathf.Clamp01(t);
            dir = (ctrl - a) * (2f * (1f - tc)) + (b - ctrl) * (2f * tc);
            pos = Bezier(a, ctrl, b, tc);
            if (t != tc) pos += dir * (t - tc);
        }

        private static void WriteStrip(TriangleMesh mesh, Vector2 a, Vector2 ctrl, Vector2 b,
                                       float t0, float t1, Vector2 offset, float halfWidth,
                                       float faceSign, Vector2 camPos)
        {
            if (mesh == null || mesh.vertices == null) return;
            int quads = mesh.vertices.Length / 4;
            if (quads < 1) return;
            Vector2 prevPos = Vector2.zero, prevNormal = Vector2.zero;
            for (int q = 0; q <= quads; q++)
            {
                float t = Mathf.Lerp(t0, t1, (float)q / quads);
                Vector2 pos, d;
                SpineSample(a, ctrl, b, t, out pos, out d);
                pos += offset;
                Vector2 n = (d.sqrMagnitude > 0.0001f)
                    ? new Vector2(-d.y, d.x).normalized * faceSign
                    : new Vector2(0f, faceSign);
                if (q > 0)
                {
                    int v = (q - 1) * 4;
                    mesh.MoveVertice(v, prevPos - prevNormal * halfWidth - camPos);
                    mesh.MoveVertice(v + 1, prevPos + prevNormal * halfWidth - camPos);
                    mesh.MoveVertice(v + 2, pos - n * halfWidth - camPos);
                    mesh.MoveVertice(v + 3, pos + n * halfWidth - camPos);
                }
                prevPos = pos;
                prevNormal = n;
            }
        }

        private struct LegPose
        {
            public Vector2 root;
            public Vector2 mid;
            public Vector2 distal;
            public Vector2 mcp;
            public Vector2 paw;
            public float upperLen, lowerLen, metaLen, chain;
            public float phase, foldDistal, foldMid;
            public float metaDeg;
            public float reachLimit;
        }

        private const float POLE_BEND_BAND_PX = 2f;

        private static float Bump(float t, float peak)
        {
            t = Mathf.Clamp01(t);
            peak = Mathf.Clamp(peak, 0.02f, 0.98f);
            return (t < peak)
                ? 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t / peak)
                : 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (t - peak) / (1f - peak));
        }

        private LegPose SolveLegPose(FoxBreedParams p, int i, Vector2 root, Vector2 paw, Vector2 fwd,
                                     Vector2 up, float swing, float phase, float bendSmooth,
                                     bool stabilise, float phaseLast = 0f, float phaseLerp = 1f)
        {
            bool front = i < 2;
            float s = partsScale;
            LegPose q = new LegPose();
            q.root = root;
            q.phase = Mathf.Clamp01(phase);

            q.chain = PartsLegChain(p, i, swing);
            float metaFac = Mathf.Clamp(front ? p.PartsLegMetaFacFront : p.PartsLegMetaFacHind,
                                        0f, 0.45f);
            q.metaLen = q.chain * metaFac;
            float proximal = Mathf.Max(1f, q.chain - q.metaLen);
            q.upperLen = proximal * Mathf.Clamp(p.LimbUpperFac, 0.15f, 0.85f);
            q.lowerLen = proximal - q.upperLen;

            float lead = Mathf.Clamp(p.PartsLegPhaseLead, 0f, 0.45f);
            float peakD = Mathf.Clamp01(p.PartsLegFoldPeak);
            float air = Mathf.Clamp01(swing);
            float driveD = Bump(q.phase, peakD);
            float driveM = Bump(q.phase, Mathf.Clamp01(peakD + lead));
            float driveP = Bump(q.phase, Mathf.Clamp01(peakD + 2f * lead));
            if (phaseLerp < 1f)
            {
                float t = Mathf.Clamp01(phaseLerp);
                float last = Mathf.Clamp01(phaseLast);
                driveD = Mathf.Lerp(Bump(last, peakD), driveD, t);
                driveM = Mathf.Lerp(Bump(last, Mathf.Clamp01(peakD + lead)), driveM, t);
                driveP = Mathf.Lerp(Bump(last, Mathf.Clamp01(peakD + 2f * lead)), driveP, t);
            }
            q.foldDistal = driveD * air;
            q.foldMid = driveM * air;

            Vector2 toRoot = root - paw;
            float toRootLen = toRoot.magnitude;
            Vector2 upLeg = (toRootLen > 0.01f) ? toRoot / toRootLen : up;
            float tuck = q.chain * (Mathf.Max(0f, p.PartsLegTuckFac) * q.foldDistal
                                    + Mathf.Max(0f, p.PartsLegKneeFoldFac) * q.foldMid);
            float lagPx = q.chain * Mathf.Max(0f, p.PartsLegProxLagFac) * q.foldDistal;
            tuck = Mathf.Min(tuck, toRootLen * 0.5f);
            q.paw = paw + upLeg * tuck - fwd * lagPx;

            float pawW = (front ? P_LEGA_PAW_W : P_LEGB_PAW_W) * s;
            float pawH = (front ? P_LEGA_PAW_H : P_LEGB_PAW_H) * s;
            q.mcp = q.paw - fwd * (pawW * Mathf.Clamp01(p.PartsPawAnchorFac)) + up * (pawH * 0.5f);

            float restDeg = front ? p.PartsLegMetaRestDegFront : p.PartsLegMetaRestDegHind;
            float poleAmt = (fox != null) ? fox.PoleAmount : 0f;
            if (poleAmt > 0f)
            {
                restDeg = Mathf.Lerp(restDeg, front ? p.PoleLegMetaDegFront : p.PoleLegMetaDegHind,
                                     poleAmt);
            }
            q.metaDeg = restDeg + p.PartsLegMetaFoldDeg * q.foldDistal;
            float a = q.metaDeg * Mathf.Deg2Rad;
            Vector2 metaDir = (up * Mathf.Cos(a) - fwd * Mathf.Sin(a)).normalized;
            q.distal = q.mcp + metaDir * q.metaLen;

            q.reachLimit = PartsLegReachLimit(p, q.upperLen + q.lowerLen);
            float d = Vector2.Distance(root, q.distal);
            if (d > q.reachLimit && d > 0.01f)
            {
                Vector2 pull = (q.distal - root) * (q.reachLimit / d - 1f);
                q.distal += pull;
                q.mcp += pull;
                q.paw += pull;
                d = q.reachLimit;
            }

            float bendKey = front ? p.PartsLegBendFront : p.PartsLegBendHind;
            float flip = bendKey * bendSmooth;
            if (poleAmt > 0f)
            {
                float frameSign = (up.y * fwd.x - up.x * fwd.y) >= 0f ? 1f : -1f;
                float sep = Vector2.Dot(root - q.distal, up);
                float poleKey = front ? p.PoleLegBendFront : p.PoleLegBendHind;
                float poleFlip = poleKey * frameSign * Mathf.Clamp(sep / POLE_BEND_BAND_PX, -1f, 1f);
                flip = Mathf.Lerp(flip, poleFlip, poleAmt);
            }
            q.mid = Custom.InverseKinematic(root, q.distal, q.upperLen, q.lowerLen, flip);
            if (stabilise) q.mid = StabiliseKnee(p, i, q.mid);

            return q;
        }

        private void DrawPartsLeg(RoomCamera.SpriteLeaser sLeaser, FoxBreedParams p, int i,
                                  Vector2 root, Vector2 paw, Vector2 fwd, Vector2 up, float faceSign,
                                  float swing, float bendSmooth, Vector2 camPos,
                                  float phaseLast, float phaseLerp)
        {
            bool front = i < 2;
            int b = LimbSprite(i);
            float s = partsScale;
            float thick = s * Mathf.Max(0.05f, p.PartsLegThicknessFac);
            Color tint = PartsTint(p, p.ColorLegs);
            Color pawTint = PartsTint(p, p.ColorPaws);
            Color lowerTint = PartsTint(p, p.ColorLegLower);

            LegPose q = SolveLegPose(p, i, root, paw, fwd, up, swing,
                                     limbs[i].SwingPhase, bendSmooth, true, phaseLast, phaseLerp);
            Vector2 knee = q.mid;
            Vector2 hock = q.distal;
            Vector2 ankle = q.mcp;
            paw = q.paw;

            if (p.PartsLegSingleSprite)
            {
                sLeaser.sprites[b + LEG_HAUNCH].scaleX = 0f;
                sLeaser.sprites[b + LEG_HAUNCH].scaleY = 0f;
                sLeaser.sprites[b + LEG_JOINT].scaleX = 0f;
                sLeaser.sprites[b + LEG_JOINT].scaleY = 0f;
                sLeaser.sprites[b + LEG_LOWER].scaleX = 0f;
                sLeaser.sprites[b + LEG_LOWER].scaleY = 0f;
                sLeaser.sprites[b + LEG_JOINT2].scaleX = 0f;
                sLeaser.sprites[b + LEG_JOINT2].scaleY = 0f;
                sLeaser.sprites[b + LEG_META].scaleX = 0f;
                sLeaser.sprites[b + LEG_META].scaleY = 0f;
                PlaceSegment(sLeaser.sprites[b + LEG_UPPER], root, ankle,
                             front ? P_LEGA_UPPER_W : P_LEGB_UPPER_W,
                             thick * (front ? P_LEGA_UPPER_H : P_LEGB_UPPER_H), faceSign, camPos);
                FSprite singlePaw = sLeaser.sprites[b + LEG_PAW];
                PlaceCentred(singlePaw, paw, fwd, thick * (front ? P_LEGA_PAW_W : P_LEGB_PAW_W),
                             thick * (front ? P_LEGA_PAW_H : P_LEGB_PAW_H), faceSign, camPos);
                CurlPaw(singlePaw, paw, ankle, p.PawCurlDeg * swing * faceSign, p.PawCurlPivotAnkle);
                for (int k = 0; k < PARTS_LEG_SPRITES; k++) sLeaser.sprites[b + k].color = tint;
                singlePaw.color = pawTint;
                return;
            }

            if (!front)
            {
                Vector2 hDir = PartsHaunchDir(p, fwd, up);
                float hLen = P_LEGB_HAUNCH_H * s;
                Vector2 hTop = p.PartsHaunchInChain ? root - hDir * hLen : root;
                Vector2 hBot = hTop + hDir * hLen;
                PlaceUpright(sLeaser.sprites[b + LEG_HAUNCH], Vector2.Lerp(hTop, hBot, 0.5f), hTop - hBot,
                             P_LEGB_HAUNCH_W * thick, hLen, faceSign, camPos);
            }
            else
            {
                sLeaser.sprites[b + LEG_HAUNCH].scaleX = 0f;
                sLeaser.sprites[b + LEG_HAUNCH].scaleY = 0f;
            }

            float upperW = front ? P_LEGA_UPPER_W : P_LEGB_UPPER_W;
            float upperH = front ? P_LEGA_UPPER_H : P_LEGB_UPPER_H;
            float lowerW = front ? P_LEGA_LOWER_W : P_LEGB_LOWER_W;
            float lowerH = front ? P_LEGA_LOWER_H : P_LEGB_LOWER_H;
            float jointW = front ? P_LEGA_JOINT_W : P_LEGB_JOINT_W;
            float pawH = front ? P_LEGA_PAW_H : P_LEGB_PAW_H;

            PlaceSegment(sLeaser.sprites[b + LEG_UPPER], root, knee, upperW, thick * upperH, faceSign, camPos);
            PlaceSegment(sLeaser.sprites[b + LEG_LOWER], knee, hock, lowerW, thick * lowerH, faceSign, camPos);
            PlaceSegment(sLeaser.sprites[b + LEG_META], hock, ankle, lowerW, thick * lowerH * 0.8f,
                         faceSign, camPos);
            PlaceCentred(sLeaser.sprites[b + LEG_JOINT], knee, hock - knee, thick * jointW, thick * jointW,
                         faceSign, camPos);
            PlaceCentred(sLeaser.sprites[b + LEG_JOINT2], hock, ankle - hock,
                         thick * jointW * 0.85f, thick * jointW * 0.85f, faceSign, camPos);
            FSprite pawSprite = sLeaser.sprites[b + LEG_PAW];
            PlaceCentred(pawSprite, paw, fwd, thick * (front ? P_LEGA_PAW_W : P_LEGB_PAW_W), thick * pawH,
                         faceSign, camPos);
            CurlPaw(pawSprite, paw, ankle, p.PawCurlDeg * swing * faceSign, p.PawCurlPivotAnkle);

            for (int k = 0; k < PARTS_LEG_SPRITES; k++) sLeaser.sprites[b + k].color = tint;
            sLeaser.sprites[b + LEG_LOWER].color = lowerTint;
            sLeaser.sprites[b + LEG_META].color = lowerTint;
            sLeaser.sprites[b + LEG_JOINT].color = lowerTint;
            sLeaser.sprites[b + LEG_JOINT2].color = lowerTint;
            pawSprite.color = pawTint;
        }

        private Vector2 StabiliseKnee(FoxBreedParams p, int i, Vector2 raw)
        {
            if (partsKneeDrawn == null || partsKneeDrawn.Length != limbs.Length)
            {
                partsKneeDrawn = new Vector2[limbs.Length];
                partsKneeSeeded = new bool[limbs.Length];
                partsKneeClamps = new int[limbs.Length];
            }
            if (i < 0 || i >= partsKneeDrawn.Length) return raw;

            float smooth = Mathf.Clamp01(p.PartsKneeSmooth);
            float budget = Mathf.Max(0f, p.PartsKneeMaxStep)
                           + Mathf.Max(0f, p.PartsKneeStepSpeedFac) * fox.mainBodyChunk.vel.magnitude;

            if (!partsKneeSeeded[i] || (smooth <= 0f && budget <= 0f)
                || Vector2.Distance(partsKneeDrawn[i], raw) > Mathf.Max(1f, p.PartsKneeReseedDist))
            {
                partsKneeSeeded[i] = true;
                partsKneeDrawn[i] = raw;
                return raw;
            }

            Vector2 cur = partsKneeDrawn[i];
            Vector2 want = Vector2.Lerp(raw, cur, smooth);
            Vector2 delta = want - cur;
            float mag = delta.magnitude;
            if (budget > 0f && mag > budget)
            {
                delta *= budget / mag;
                partsKneeClamps[i]++;
            }
            partsKneeDrawn[i] = cur + delta;
            return partsKneeDrawn[i];
        }

        private static void PlaceSegment(FSprite s, Vector2 from, Vector2 to, float elemW, float thickPx,
                                         float faceSign, Vector2 camPos)
        {
            Vector2 v = to - from;
            float len = v.magnitude;
            Vector2 dir = (len > 0.01f) ? v / len : new Vector2(faceSign, 0f);
            s.x = from.x - camPos.x;
            s.y = from.y - camPos.y;
            s.anchorX = 0f;
            s.anchorY = 0.5f;
            s.rotation = AimDeg(dir) - 90f * faceSign;
            float ew = (s.element != null && s.element.sourcePixelSize.x > 0.01f) ? s.element.sourcePixelSize.x : elemW;
            s.scaleX = (len / Mathf.Max(0.5f, ew)) * faceSign;
            s.scaleY = (s.element != null && s.element.sourcePixelSize.y > 0.01f)
                ? thickPx / s.element.sourcePixelSize.y : 1f;
        }

        private static Vector2 ForeshortenAlong(Vector2 pt, Vector2 mid, Vector2 fwd, Vector2 up, float fore)
        {
            Vector2 rel = pt - mid;
            return mid + fwd * (Vector2.Dot(rel, fwd) * fore) + up * Vector2.Dot(rel, up);
        }

        private static void PlaceUpright(FSprite s, Vector2 at, Vector2 upDir, float widthPx, float heightPx,
                                         float faceSign, Vector2 camPos)
        {
            Vector2 d = (upDir.sqrMagnitude > 0.0001f) ? upDir.normalized : new Vector2(0f, 1f);
            s.x = at.x - camPos.x;
            s.y = at.y - camPos.y;
            s.anchorX = 0.5f;
            s.anchorY = 0.5f;
            s.rotation = AimDeg(d);
            float ew = (s.element != null && s.element.sourcePixelSize.x > 0.01f) ? s.element.sourcePixelSize.x : 1f;
            float eh = (s.element != null && s.element.sourcePixelSize.y > 0.01f) ? s.element.sourcePixelSize.y : 1f;
            s.scaleX = (widthPx / ew) * faceSign;
            s.scaleY = heightPx / eh;
        }

        private static void PlaceCentred(FSprite s, Vector2 at, Vector2 dir, float widthPx, float heightPx,
                                         float faceSign, Vector2 camPos)
        {
            Vector2 d = (dir.sqrMagnitude > 0.0001f) ? dir.normalized : new Vector2(faceSign, 0f);
            s.x = at.x - camPos.x;
            s.y = at.y - camPos.y;
            s.anchorX = 0.5f;
            s.anchorY = 0.5f;
            s.rotation = AimDeg(d) - 90f * faceSign;
            float ew = (s.element != null && s.element.sourcePixelSize.x > 0.01f) ? s.element.sourcePixelSize.x : 1f;
            float eh = (s.element != null && s.element.sourcePixelSize.y > 0.01f) ? s.element.sourcePixelSize.y : 1f;
            s.scaleX = (widthPx / ew) * faceSign;
            s.scaleY = heightPx / eh;
        }

        private static void CurlPaw(FSprite s, Vector2 paw, Vector2 ankle, float curlDeg, bool pivotAnkle)
        {
            if (Mathf.Abs(curlDeg) < 0.01f) return;
            s.rotation += curlDeg;
            if (!pivotAnkle) return;
            Vector2 off = paw - ankle;
            float len = off.magnitude;
            if (len < 0.01f) return;
            Vector2 turned = Custom.DegToVec(Custom.VecToDeg(off) + curlDeg) * len;
            s.x += turned.x - off.x;
            s.y += turned.y - off.y;
        }

        private void DrawPartsHead(RoomCamera.SpriteLeaser sLeaser, FoxBreedParams p, Vector2 chest,
                                   Vector2 fwd, float faceSign, float fore, float timeStacker, Vector2 camPos)
        {
            FSprite s = sLeaser.sprites[SpriteHead];
            FSprite eye = sLeaser.sprites[SpriteEye];
            FAtlasElement wantHead = partsHeadEl[partsHeadFrame];
            if (wantHead != null && s.element != wantHead) s.element = wantHead;
            FAtlasElement wantEye = partsEyeEl[partsHeadFrame];
            if (wantEye != null && eye.element != wantEye) eye.element = wantEye;

            Vector2 neck = PartsNeck(p, chest, fwd);
            DrawNeckPos = neck;
            DrawHeadPos = neck;
            float rock = Mathf.Lerp(lastWavePitchDeg, wavePitchDeg, timeStacker);
            Vector2 dir = Custom.DegToVec(AimDeg(DrawHeadDir) - rock * faceSign);
            if (dir.sqrMagnitude > 0.0001f) DrawHeadDir = dir.normalized;
            DrawHeadUp = new Vector2(-DrawHeadDir.y, DrawHeadDir.x);
            if (DrawHeadUp.y < 0f) DrawHeadUp = -DrawHeadUp;
            DrawNosePos = DrawHeadPos + DrawHeadDir * (P_HEAD_W * 0.7f * partsScale);

            float scale = Mathf.Max(0.01f, p.HeadSpriteScale * p.HeadSize * partsScale);
            s.x = neck.x - camPos.x;
            s.y = neck.y - camPos.y;
            s.anchorX = p.HeadSpriteAnchorX;
            s.anchorY = p.HeadSpriteAnchorY;
            s.rotation = AimDeg(DrawHeadDir) - 90f * faceSign;
            s.scaleX = scale * faceSign * fore;
            s.scaleY = scale;
            s.color = PartsTint(p, p.ColorHead);

            eye.x = s.x;
            eye.y = s.y;
            eye.anchorX = s.anchorX;
            eye.anchorY = s.anchorY;
            eye.rotation = s.rotation;
            if (wantEye == null)
            {
                eye.scaleX = 0f;
                eye.scaleY = 0f;
            }
            else
            {
                eye.scaleX = s.scaleX;
                eye.scaleY = s.scaleY;
                eye.color = InkEyeTint(p, PartsTint(p, EyeColor(p)));
            }
        }

        private void UpdatePartsHeadFrame(FoxBreedParams p)
        {
            float yawPull = Mathf.Abs(headYaw) * Mathf.Clamp01(p.HeadYawDepth);
            float effDepth = headDepth * (1f - yawPull);
            float a = Mathf.Abs(effDepth);

            float tqEnter = Mathf.Clamp01(p.PartsHeadTqGateEnter);
            float tqExit = Mathf.Clamp01(p.PartsHeadTqGateExit);
            if (tqExit < tqEnter) { float swap = tqEnter; tqEnter = tqExit; tqExit = swap; }
            float awayEnter = Mathf.Clamp(p.PartsHeadAwayGateEnter, 0f, tqEnter);
            float awayExit = Mathf.Clamp(p.PartsHeadAwayGateExit, 0f, tqExit);
            if (awayExit < awayEnter) { float swap = awayEnter; awayEnter = awayExit; awayExit = swap; }
            int dwellTicks = Mathf.Max(0, p.PartsHeadFrameDwellTicks);

            float sideGate = (partsHeadFrame == HEAD_SIDE) ? tqEnter : tqExit;
            float deepGate = headFrameDeep ? awayExit : awayEnter;
            if (deepGate > sideGate) deepGate = sideGate;

            bool side = a >= sideGate;
            if (side)
            {
                headTurnTowardCamera = headDepth < lastHeadDepthForTrend;
            }
            lastHeadDepthForTrend = headDepth;
            bool yawDriven = yawPull > 0.05f;
            if (yawDriven) headTurnTowardCamera = headYaw >= 0f;

            bool deep = !side && a < deepGate;
            int want = side ? HEAD_SIDE
                     : !deep ? HEAD_TQ
                     : (headTurnTowardCamera ? HEAD_TQ : HEAD_AWAY);

            if (headFrameDwell > 0) headFrameDwell--;
            headFrameHeld++;

            if (want == partsHeadFrame)
            {
                headFrameDeep = deep;
                return;
            }
            if (headFrameDwell > 0)
            {
                if (headFrameSuppressed == 0) headFrameDwellLeftAtFirst = headFrameDwell;
                headFrameSuppressed++;
                return;
            }

            partsHeadFrame = want;
            headFrameDeep = deep;
            int held = headFrameHeld, suppressed = headFrameSuppressed, dwellLeft = headFrameDwellLeftAtFirst;
            headFrameHeld = 0;
            headFrameSuppressed = 0;
            headFrameDwellLeftAtFirst = 0;
            headFrameDwell = dwellTicks;
        }

        private void UpdatePartsMotion(FoxBreedParams p)
        {
            Vector2 now = fox.bodyChunks[1].pos;
            Vector2 step = travelSeeded ? (now - lastTravelPos) : Vector2.zero;
            float dp = travelSeeded ? step.magnitude : 0f;
            if (!artRotSeeded) { dp = 0f; step = Vector2.zero; }
            lastTravelPos = now;
            travelSeeded = true;
            travelVel = Vector2.Lerp(travelVel, step, 0.2f);

            bool limp = fox.dead;
            waveGround = Mathf.Lerp(waveGround, (fox.Grounded && !limp) ? 1f : 0f,
                                    Mathf.Clamp01(p.PartsWaveGroundFade));

            travelDist += dp * waveGround;
            speedSmooth = Mathf.Lerp(speedSmooth, dp, 0.2f);

            float gate = Mathf.Max(0.1f, p.GaitPairSpeed);
            pairBlend = Mathf.Clamp01((speedSmooth - gate) / (gate * 0.5f));
            bool paired = p.GaitDiagonal && pairBlend >= 0.5f;

            bool walk4 = p.GaitWalkEnabled && p.GaitDiagonal && !paired
                         && speedSmooth < p.GaitWalkTrotSpeed * (gaitWalk4 ? GAIT_WALK_HYSTERESIS : 1f);
            if (paired != gaitPaired || walk4 != gaitWalk4)
            {
                gaitPaired = paired;
                gaitWalk4 = walk4;
                WireGaitPartners(p);
            }

            float velAlong = Vector2.Dot(fox.bodyChunks[1].vel, bodyAxis);
            float accel = velAlong - lastVelAlong;
            lastVelAlong = velAlong;
            accelSmooth = Mathf.Lerp(accelSmooth, accel, 0.2f);
            lastLeanDeg = leanDeg;
            leanDeg = Mathf.Lerp(leanDeg, Mathf.Clamp(accel * p.BodyLeanAccel, -p.BodyLeanMax, p.BodyLeanMax), 0.15f);

            UpdatePartsFlex(p);

            if (limp) return;

            breathTicks += 1f;
            swayTicks += 1f;
            if (breathTicks > 100000f) breathTicks = 0f;
            if (swayTicks > 100000f) swayTicks = 0f;
        }

        private void UpdatePartsFlex(FoxBreedParams p)
        {
            lastBodyBend = bodyBend;
            lastBodySquash = bodySquash;
            if (!partsBodyMeshActive)
            {
                bodyBend = 0f; bodySquash = 0f; bodyLenRatio = 1f; landImpact = 0f;
                flexGeom = 0f; flexMotion = 0f; squashGeom = 0f; squashMotion = 0f;
                return;
            }

            float waveGain = Mathf.Max(0f, p.PartsWaveAmp);
            float waveAmp = waveGain * Mathf.Clamp01(Mathf.InverseLerp(p.PartsWaveSpeedGate, p.PartsWaveSpeedFull,
                                                            speedSmooth)) * Mathf.Clamp01(waveGround);
            float wavePh = PartsWavePhase(p, travelDist);
            float speedNorm = Mathf.Clamp01(speedSmooth / Mathf.Max(0.01f, p.PartsWaveSpeedFull));
            float turnArc = Mathf.Sin(Mathf.Clamp01(fox.TurnProgress) * Mathf.PI);

            Vector2 front = drawPositions[0, 0], mid = drawPositions[1, 0], rear = drawPositions[2, 0];
            Vector2 chordMid = (front + rear) * 0.5f;
            Vector2 perp = new Vector2(-bodyAxis.y, bodyAxis.x);

            if (!midLagSeeded) { midLagPos = mid; midLagSeeded = true; }
            midLagPos = Vector2.Lerp(midLagPos, mid, Mathf.Clamp(p.PartsBodyLag, 0.01f, 1f));

            float limp = Mathf.Clamp01(fox.DeathLimpAmount);
            float bendFac = Mathf.Lerp(p.PartsBodyBendFac, p.DeathBodyBendFac, limp);
            float real = Vector2.Dot(mid - chordMid, perp) * bendFac;
            float whip = Vector2.Dot(midLagPos - mid, perp) * p.PartsBodyLagFac;
            flexGeom = real + whip;

            float upSign = Mathf.Clamp(perp.y * 4f, -1f, 1f);
            flexMotion = upSign * (
                  p.PartsBodyFlexWave * waveAmp * Mathf.Sin(wavePh + p.PartsBodyFlexWavePhase)
                + p.PartsBodyFlexAccel * accelSmooth
                + p.PartsBodyFlexLand * landImpact
                + p.PartsBodyFlexGather * Mathf.Clamp01(fox.PounceGather)
                + p.PartsBodyFlexFlight * Mathf.Clamp01(fox.PounceFlight)
                + p.PartsBodyFlexTurn * turnArc);

            float spineLen = Mathf.Max(1f, Vector2.Distance(front, rear) * TurnForeshorten(p, headDepth));
            float maxBow = Mathf.Max(0f, Mathf.Lerp(p.PartsBodyBendMax, p.DeathBodyBendMax, limp));
            float flexMaxDeg = Mathf.Lerp(p.PartsBodyFlexMaxDeg, p.DeathBodyFlexMaxDeg, limp);
            if (flexMaxDeg > 0f)
            {
                float degBow = spineLen * 0.25f
                             * Mathf.Tan(Mathf.Clamp(flexMaxDeg, 0f, 170f) * 0.5f * Mathf.Deg2Rad);
                maxBow = Mathf.Min(maxBow, degBow);
            }
            float wantBend = Mathf.Clamp(flexGeom + flexMotion, -maxBow, maxBow);
            bodyBend = Mathf.Lerp(bodyBend, wantBend, Mathf.Clamp01(p.PartsBodyFlexSmooth));
            bodyBend = Mathf.Clamp(bodyBend, -maxBow, maxBow);

            bool grounded = fox.Grounded;
            if (grounded && !lastGroundedForSquash)
            {
                float hit = Mathf.Clamp01(Mathf.Abs(lastAirVy) / Mathf.Max(0.5f, p.PartsBodyLandSpeed));
                if (hit > landImpact) landImpact = hit;
            }
            lastGroundedForSquash = grounded;
            lastAirVy = grounded ? 0f : fox.bodyChunks[1].vel.y;
            landImpact = Mathf.Max(0f, landImpact - Mathf.Max(0.001f, p.PartsBodyLandDecay));

            bodyLenRatio = Vector2.Distance(fox.bodyChunks[0].pos, fox.bodyChunks[2].pos)
                           / Mathf.Max(1f, artRestSpine);
            squashGeom = (bodyLenRatio - 1f) * p.PartsBodySquashLenGain
                         + p.PartsBodySquashFlight * Mathf.Clamp01(fox.PounceFlight)
                         - p.PartsBodySquashGather * Mathf.Clamp01(fox.PounceGather)
                         - p.PartsBodySquashLand * landImpact;
            squashMotion = p.PartsBodySquashSpeed * speedNorm
                           + p.PartsBodySquashAccel * accelSmooth
                           + p.PartsBodySquashWave * waveAmp * Mathf.Sin(wavePh * 2f)
                           - p.PartsBodySquashTurn * turnArc;
            float e = squashGeom + squashMotion;
            float maxE = Mathf.Max(0f, p.PartsBodySquashMax);
            bodySquash = Mathf.Lerp(bodySquash, Mathf.Clamp(e, -maxE, maxE),
                                    Mathf.Clamp01(p.PartsBodyFlexSmooth));
        }

        private void WireGaitPartners(FoxBreedParams p)
        {
            for (int i = 0; i < limbs.Length; i++)
            {
                limbs[i].gaitPartner = null;
                limbs[i].gaitSuccessor = null;
                limbs[i].gaitGapFrac = 0f;
                limbs[i].ClearGaitHold();
            }
            if (!p.GaitDiagonal)
            {
                for (int i = 0; i < limbs.Length; i++) limbs[i].gaitPartner = limbs[i].otherLimbInPair;
                return;
            }
            if (gaitWalk4 && !gaitPaired && limbs.Length == 4)
            {
                float shortGap = Mathf.Max(0f, p.StepGaitGapShort);
                float longGap = Mathf.Max(0f, p.StepGaitGapLong);
                limbs[2].gaitSuccessor = limbs[0]; limbs[2].gaitGapFrac = shortGap;
                limbs[0].gaitSuccessor = limbs[3]; limbs[0].gaitGapFrac = longGap;
                limbs[3].gaitSuccessor = limbs[1]; limbs[3].gaitGapFrac = shortGap;
                limbs[1].gaitSuccessor = limbs[2]; limbs[1].gaitGapFrac = longGap;
                return;
            }
            if (gaitPaired)
            {
                limbs[0].gaitPartner = limbs[2];
                limbs[1].gaitPartner = limbs[3];
            }
            else
            {
                limbs[1].gaitPartner = limbs[0];
                limbs[3].gaitPartner = limbs[1];
                if (p.PartsGaitChainAll) limbs[2].gaitPartner = limbs[3];
            }
        }

        private void ApplyPartsPose(FoxBreedParams p)
        {
            Vector2 up = BodyUp;
            float spineLen = Mathf.Max(1f, Vector2.Distance(drawPositions[0, 0], drawPositions[2, 0]));

            float amp = Mathf.Clamp01(Mathf.InverseLerp(p.PartsWaveSpeedGate, p.PartsWaveSpeedFull, speedSmooth))
                        * Mathf.Clamp01(waveGround);
            float phase = PartsWavePhase(p, travelDist);
            lastWavePitchDeg = wavePitchDeg;
            wavePitchDeg = 0f;

            if (amp > 0f && p.RunPitchAmp != 0f)
            {
                float pitch = p.RunPitchAmp * amp * Mathf.Sin(phase);
                wavePitchDeg = pitch;
                float lever = Mathf.Tan(Mathf.Clamp(pitch, -60f, 60f) * Mathf.Deg2Rad) * spineLen * 0.5f;
                drawPositions[0, 0] -= up * lever;
                drawPositions[2, 0] += up * lever;
            }
            if (amp > 0f && p.RunHeightAmp != 0f)
            {
                float h = p.RunHeightAmp * amp * Mathf.Sin(phase * 2f);
                for (int i = 0; i < drawPositions.GetLength(0); i++) drawPositions[i, 0] += up * h;
            }

            float gather = Mathf.Clamp01(fox.PounceGather);
            if (gather > 0f && p.PounceBowPitch != 0f)
            {
                float lever = Mathf.Tan(Mathf.Clamp(p.PounceBowPitch * gather, -60f, 60f) * Mathf.Deg2Rad)
                              * spineLen * 0.5f;
                drawPositions[0, 0] -= up * lever;
                drawPositions[2, 0] += up * lever;
                wavePitchDeg += p.PounceBowPitch * gather;
            }
            float flight = Mathf.Clamp01(fox.PounceFlight);
            if (flight > 0f && p.PouncePropelPitch != 0f)
            {
                float lever = Mathf.Tan(Mathf.Clamp(p.PouncePropelPitch * flight, -60f, 60f) * Mathf.Deg2Rad)
                              * spineLen * 0.5f;
                drawPositions[0, 0] += up * lever;
                drawPositions[2, 0] -= up * lever;
                wavePitchDeg -= p.PouncePropelPitch * flight;
            }
        }

        private static float PartsWavePhase(FoxBreedParams p, float travel)
        {
            return travel / Mathf.Max(1f, p.PartsWaveLen) * Mathf.PI * 2f;
        }

        private void ApplyTailLife(FoxBreedParams p, Vector2 tailRoot, Vector2 back, Vector2 up)
        {
            float amp = Mathf.Clamp01(Mathf.InverseLerp(p.PartsWaveSpeedGate, p.PartsWaveSpeedFull, speedSmooth));
            float phase = PartsWavePhase(p, travelDist);
            float calm = 1f - amp;
            float sway = (p.IdleTailSwayTicks > 1f)
                ? Mathf.Sin(swayTicks * (Mathf.PI * 2f / p.IdleTailSwayTicks)) : 0f;
            float gather = Mathf.Clamp01(fox.PounceGather);
            float turnSweep = (p.TurnTailSwing != 0f)
                ? p.TurnTailSwing * Mathf.Sin(Mathf.Clamp01(fox.TurnProgress) * Mathf.PI) : 0f;

            float live = 1f - Mathf.Clamp01(fox.DeathLimpAmount) * Mathf.Clamp01(p.DeathTailLimpFac);
            float rearBias = p.TailRearBias * live;
            float upBias = p.TailUpBias * live;
            float floorClear = p.TailFloorClear * live;
            sway *= live;

            for (int i = 0; i < tail.Length; i++)
            {
                float t = (tail.Length == 1) ? 1f : (float)i / (tail.Length - 1);
                if (p.RunTailAmp != 0f) tail[i].vel += up * (-Mathf.Sin(phase) * p.RunTailAmp * amp * (0.4f + t));
                tail[i].vel += up * (sway * p.IdleTailSwayAmp * calm * t * 0.5f);
                tail[i].vel += back * (rearBias * (1f - t * 0.5f));
                tail[i].vel += up * (upBias * calm * (1f - t * 0.5f));
                if (p.TailStreamFac != 0f) tail[i].vel += back * (p.TailStreamFac * speedSmooth * t * 0.15f);
                if (gather > 0f) tail[i].vel += up * (p.PounceBowTailUp * gather * (0.3f + t));
                if (turnSweep != 0f) tail[i].vel += up * (turnSweep * (0.3f + t));

                if (p.TailNoTuck && live > 0f)
                {
                    Vector2 off = tail[i].pos - tailRoot;
                    float f = -Vector2.Dot(off, back);
                    if (f > 0f)
                    {
                        tail[i].pos += back * (f * live);
                        float vf = -Vector2.Dot(tail[i].vel, back);
                        if (vf > 0f) tail[i].vel += back * (vf * live);
                    }
                }
                if (floorClear > 0f && fox.room != null)
                {
                    IntVector2 tp = fox.room.GetTilePosition(tail[i].pos);
                    if (fox.room.GetTile(tp).Solid)
                    {
                        float top = fox.room.MiddleOfTile(tp).y + 10f + floorClear;
                        if (tail[i].pos.y < top)
                        {
                            tail[i].pos.y = top;
                            if (tail[i].vel.y < 0f) tail[i].vel.y = 0f;
                        }
                    }
                }
            }
        }

        private void UpdateHeadLook(FoxBreedParams p)
        {
            if (!partsRigActive)
            {
                ServiceScentSniffWithoutParts(p);
                return;
            }
            if (!headPitchSeeded)
            {
                headPitchDeg = lastHeadPitchDeg = p.HeadRestPitchDeg;
                headPitchSeeded = true;
            }

            if (fox.dead)
            {
                float deathTarget = Mathf.Clamp(p.DeathHeadPitchDeg, -p.HeadPitchMax, p.HeadPitchMax);
                lastHeadPitchDeg = headPitchDeg;
                headPitchDeg = Mathf.Lerp(headPitchDeg, deathTarget,
                                          Mathf.Clamp01(p.DeathHeadPitchRate));
                headYaw = Mathf.Lerp(headYaw, 0f, Mathf.Clamp01(p.DeathHeadPitchRate));

                if (ScentSniffOpen) CloseScentSniff(false, "dead");
                if (PheromoneSniffWanted) CancelPheromoneSniff("dead");

                if (headMode != HL_NEUTRAL || headLookHasPos)
                {
                    headMode = HL_NEUTRAL;
                    headModeTicks = 0;
                    headNoiseHold = 0;
                    headLookCooldown = 0;
                    headLookHasPos = false;
                    headLookWhat = "dead";
                }
                return;
            }

            if (headLookCooldown > 0) headLookCooldown--;
            if (headModeTicks > 0) headModeTicks--;
            if (headNoiseHold > 0) headNoiseHold--;

            int was = headMode;
            int want = HL_NEUTRAL;
            Vector2 lookAt = Vector2.zero;
            headLookHasPos = false;
            headLookWhat = "none";
            bool freshNoise = false;
            bool pheromoneSniffNow = false;

            Creature chase = fox.chaseTarget;
            bool eating = fox.HeldPrey != null || fox.EatPose > 0f;

            if (chase != null && chase.room == fox.room && chase.mainBodyChunk != null)
            {
                want = HL_TARGET; lookAt = chase.mainBodyChunk.pos; headLookHasPos = true;
                headLookWhat = "chase";
                headModeTicks = 0; headNoiseHold = 0;
                if (ScentSniffOpen) CloseScentSniff(false, "target");
            }
            else if (eating && TryMealPoint(out lookAt))
            {
                want = HL_TARGET; headLookHasPos = true;
                headLookWhat = "meal";
                headModeTicks = 0; headNoiseHold = 0;
                if (ScentSniffOpen) CloseScentSniff(false, "meal");
            }
            else
            {
                if (p.HeadNoiseEnabled && fox.AI != null)
                {
                    Vector2 np; int nage;
                    float selfRad = fox.bodyChunks[0].rad * 5f;
                    if (fox.AI.TryGetHeardNoise(Mathf.Max(0, p.HeadNoiseMaxAge), out np, out nage)
                        && !Custom.DistLess(np, fox.mainBodyChunk.pos, selfRad)
                        && (headNoiseHold <= 0 || !Custom.DistLess(np, headNoisePos, 20f)))
                    {
                        freshNoise = true;
                        headNoisePos = np;
                        headNoiseHold = Mathf.Max(1, p.HeadNoiseHoldTicks);
                    }
                }

                if (headNoiseHold > 0)
                {
                    want = HL_NOISE; lookAt = headNoisePos; headLookHasPos = true;
                    headLookWhat = "noise";
                    headModeTicks = 0;
                    if (ScentSniffOpen) CloseScentSniff(false, "noise");
                }
                else if (ScentSniffOpen)
                {
                    StartOrContinueScentSniff(p, "parts");
                    want = HL_SCENT; headLookWhat = "scent";
                    headModeTicks = 0;
                }
                else if (PheromoneSniffWanted)
                {
                    want = HL_SCENT; headLookWhat = "pheromone";
                    headModeTicks = 0;
                    pheromoneSniffNow = true;
                }
                else if (fox.TurnActive && p.TurnHeadLead > 0f)
                {
                    want = HL_TURN; headLookWhat = "turn";
                    headModeTicks = 0;
                }
                else
                {
                    bool idle = speedSmooth <= p.HeadIdleSpeed;
                    bool inIdleMode = (headMode == HL_SNIFF || headMode == HL_SCAN);
                    if (inIdleMode && !idle)
                    {
                        want = HL_NEUTRAL; headModeTicks = 0;
                        headLookCooldown = JitterTicks(p.HeadLookCooldownTicks);
                    }
                    else if (inIdleMode && headModeTicks > 0)
                    {
                        want = headMode;
                    }
                    else if (inIdleMode)
                    {
                        want = HL_NEUTRAL;
                        headLookCooldown = JitterTicks(p.HeadLookCooldownTicks);
                    }
                    else if (p.HeadIdleEnabled && idle && headLookCooldown <= 0)
                    {
                        float r = NextGlanceRandom();
                        if (r < p.HeadSniffChance)
                        {
                            want = HL_SNIFF;
                            headModeTicks = JitterTicks(p.HeadSniffTicks);
                        }
                        else if (r < p.HeadSniffChance + p.HeadScanChance)
                        {
                            want = HL_SCAN;
                            headModeTicks = JitterTicks(p.HeadScanTicks);
                            headScanPhase = NextGlanceRandom() * Mathf.PI * 2f;
                        }
                    }
                    if (want == HL_SNIFF || want == HL_SCAN || want == HL_NEUTRAL)
                    {
                        headLookWhat = HEAD_LOOK_NAMES[want];
                    }
                }
            }

            if (!pheromoneSniffNow && PheromoneSniffWanted) CancelPheromoneSniff(headLookWhat);

            if (headLookHasPos && p.BlindEnabled && fox.Blinded && p.BlindHeadScatterPx > 0f)
            {
                int hold = Mathf.Max(1, p.BlindHeadScatterTicks);
                if (blindScatterHold <= 0 || !blindScatterSeeded)
                {
                    blindScatterSeeded = true;
                    blindScatterHold = hold;
                    blindScatterOffset = Custom.RNV() * (UnityEngine.Random.value * p.BlindHeadScatterPx);
                }
                blindScatterHold--;
                lookAt = fox.mainBodyChunk.pos + blindScatterOffset;
                headLookWhat = "blind";
            }
            else
            {
                blindScatterSeeded = false;
                blindScatterHold = 0;
            }

            float pitchTarget = p.HeadRestPitchDeg;
            float yawTarget = 0f;
            float yawRate = Mathf.Clamp01(p.HeadYawRate);
            float turnArc = Mathf.Sin(Mathf.Clamp01(fox.TurnProgress) * Mathf.PI);
            switch (want)
            {
                case HL_TARGET:
                case HL_NOISE:
                {
                    float behind;
                    pitchTarget = LookPitchDeg(p, lookAt, out behind);
                    yawTarget = behind * Mathf.Clamp01(p.HeadBehindYaw);
                    break;
                }
                case HL_SNIFF:
                    pitchTarget = p.HeadSniffPitchDeg;
                    break;
                case HL_SCAN:
                    pitchTarget = p.HeadScanPitchDeg;
                    if (p.HeadScanSweepTicks > 1f) headScanPhase += Mathf.PI * 2f / p.HeadScanSweepTicks;
                    if (headScanPhase > 100000f) headScanPhase = 0f;
                    yawTarget = Mathf.Sin(headScanPhase) * Mathf.Clamp01(p.HeadScanYaw);
                    break;
                case HL_TURN:
                    pitchTarget = p.HeadRestPitchDeg + p.TurnHeadLeadPitchDeg * turnArc;
                    yawTarget = Mathf.Clamp01(p.TurnHeadLead) * turnArc;
                    yawRate = Mathf.Clamp01(p.TurnHeadLeadRate);
                    break;
                case HL_SCENT:
                    pitchTarget = p.ScentSniffPitchDeg;
                    break;
            }
            pitchTarget = Mathf.Clamp(pitchTarget, -p.HeadPitchMax, p.HeadPitchMax);

            lastHeadPitchDeg = headPitchDeg;
            headPitchDeg = Mathf.Lerp(headPitchDeg, pitchTarget, Mathf.Clamp01(p.HeadPitchRate));
            headYaw = Mathf.Lerp(headYaw, yawTarget, yawRate);
            headMode = want;
            if (headLookHasPos) headLookPos = lookAt;

            if (want == HL_SCENT)
            {
                if (pheromoneSniffNow) TickPheromoneSniff();
                else TickScentSniff(pitchTarget);
            }

            headLookLogCounter++;
            bool modeChanged = want != was;
            if (modeChanged || (freshNoise && headLookLogCounter >= HEAD_LOOK_LOG_TICKS))
            {
                headLookLogCounter = 0;
            }
        }

        private int RigClock
        {
            get { return (fox.room != null && fox.room.game != null) ? fox.room.game.clock : 0; }
        }

        private bool ScentSniffOpen
        {
            get { return fox.ScentSniff.Open; }
        }

        private void StartOrContinueScentSniff(FoxBreedParams p, string rig)
        {
            Fox.ScentSniffSeam s = fox.ScentSniff;
            if (scentSniffServing == s.RequestSerial) return;

            scentSniffServing = s.RequestSerial;
            s.StartedSerial = s.RequestSerial;
            scentSniffLeft = Mathf.Max(1, p.ScentSniffTicks);
            scentSniffPeakPitch = headPitchDeg;
        }

        private void TickScentSniff(float pitchTarget)
        {
            if (headPitchDeg > scentSniffPeakPitch) scentSniffPeakPitch = headPitchDeg;
            scentSniffLeft--;
            if (scentSniffLeft <= 0) CloseScentSniff(true, "played", pitchTarget);
        }

        private void CloseScentSniff(bool completed, string reason, float pitchTarget = float.NaN)
        {
            Fox.ScentSniffSeam s = fox.ScentSniff;
            int serial = s.RequestSerial;
            s.ClosedSerial = serial;
            s.ClosedCompleted = completed;

            scentSniffServing = 0;
            scentSniffLeft = 0;
        }

        private void ServiceScentSniffWithoutParts(FoxBreedParams p)
        {
            if (!ScentSniffOpen) return;
            if (fox.dead)
            {
                CloseScentSniff(false, "dead");
                return;
            }
            StartOrContinueScentSniff(p, "fallback");
            scentSniffLeft--;
            if (scentSniffLeft <= 0) CloseScentSniff(true, "no_parts_rig");
        }

        private bool TryMealPoint(out Vector2 at)
        {
            if (fox.HeldPrey != null && fox.HeldPrey.mainBodyChunk != null)
            {
                at = fox.HeldPrey.mainBodyChunk.pos;
                return true;
            }
            if (fox.EatPose > 0f)
            {
                at = fox.EatAimPos;
                return true;
            }
            at = Vector2.zero;
            return false;
        }

        private float LookPitchDeg(FoxBreedParams p, Vector2 at, out float behind)
        {
            Vector2 fwd = bodyAxis;
            Vector2 up = new Vector2(-fwd.y, fwd.x);
            if (up.y < 0f) up = -up;
            Vector2 d = at - fox.bodyChunks[0].pos;
            if (d.sqrMagnitude < 1f) { behind = 0f; return p.HeadRestPitchDeg; }
            d = d.normalized;
            behind = Mathf.Clamp01(-Vector2.Dot(d, fwd));
            float deg = Mathf.Atan2(Vector2.Dot(d, up), Vector2.Dot(d, fwd)) * Mathf.Rad2Deg;
            if (deg > 90f) deg = 180f - deg;
            else if (deg < -90f) deg = -180f - deg;
            return deg * Mathf.Clamp01(p.HeadLookPitchFac);
        }

        private int JitterTicks(int ticks)
        {
            return Mathf.Max(1, Mathf.RoundToInt(ticks * (0.75f + 0.5f * NextGlanceRandom())));
        }

        private Vector2 ApplyHeadLook(FoxBreedParams p, Vector2 target)
        {
            if (!partsRigActive) return ApplyIdleGlance(p, target);
            return headLookHasPos ? headLookPos : target;
        }

        private Vector2 ApplyIdleGlance(FoxBreedParams p, Vector2 target)
        {
            if (p.IdleGlanceChance <= 0f || fox.chaseTarget != null)
            {
                glanceTicks = 0;
                return target;
            }
            if (glanceTicks > 0)
            {
                glanceTicks--;
            }
            else if (NextGlanceRandom() < p.IdleGlanceChance)
            {
                glanceTicks = Mathf.Max(1, p.IdleGlanceTicks);
                glanceDir = (NextGlanceRandom() < 0.5f) ? -1f : 1f;
            }
            if (glanceTicks <= 0) return target;
            Vector2 perp = new Vector2(-bodyAxis.y, bodyAxis.x);
            return target + perp * (glanceDir * p.IdleGlanceRange);
        }

        private void CheckTurnSwap(FoxBreedParams p)
        {
            if (fox.TurnSwapCount == lastTurnSwapCount) return;
            lastTurnSwapCount = fox.TurnSwapCount;

            for (int slot = 0; slot < 2; slot++)
            {
                Vector2 t = drawPositions[0, slot];
                drawPositions[0, slot] = drawPositions[2, slot];
                drawPositions[2, slot] = t;
            }
            Vector2 ride = RideVector(p);
            Vector2 newRear = fox.bodyChunks[2].pos + ride;
            for (int i = 0; i < tail.Length; i++) tail[i].Reset(newRear);

            for (int i = 0; i < limbs.Length / 2; i++) limbs[i].SwapFootWith(limbs[i + 2]);
            for (int i = 0; i < limbs.Length; i++) limbs[i].ReseedBend();
            head.Reset(fox.bodyChunks[0].pos + ride);

            headDepth = 0f;
            lastHeadDepth = 0f;
            artRotSeeded = false;
            bodyBend = 0f;
            lastBodyBend = 0f;
            midLagSeeded = false;
        }
    }
}
