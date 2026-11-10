using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public partial class FoxGraphics
    {
        internal bool SpearHoldValid;
        internal Vector2 SpearHoldPos, SpearHoldDir;

        private float spearPitch, lastSpearPitch;
        private Vector2 spHand, spHandVel;
        private bool spHandOn;
        private Vector2 spHang, spHangVel;
        private bool spHangOn;
        private readonly int[] spLegMode = new int[2];
        private readonly Vector2[] spLegPos = new Vector2[2], spLegLast = new Vector2[2], spRetFrom = new Vector2[2];
        private readonly int[] spRetAge = new int[2];
        private readonly float[] spRetSwing = new float[2], spRetSwingLast = new float[2], spRetPhase = new float[2];
        private Vector2 spLastPawHold, spLastPawDir;
        private float spHeadPitch, spHeadVel;
        private int spHeadStart = int.MinValue, spWhipStart = int.MinValue, spPoseLogStart = int.MinValue;
        private float spearLen = -1f;
        private float spLoadBrake;
        private int spLoadStart = int.MinValue;

        private FoxSpear SpearState { get { return fox != null && fox.AI != null ? fox.AI.spear : null; } }

        private int SpearNow { get { return (fox.room != null && fox.room.game != null) ? fox.room.game.clock : 0; } }

        private bool SpearLayerOn(FoxBreedParams p)
        {
            return partsRigActive && FoxSpear.IsOn(p) && SpearState != null;
        }

        private static float SpEase(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private static Vector2 SpRot(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        private static Vector2 SpTipUp(Vector2 v, float deg, float face) { return SpRot(v, face * deg); }

        private static Vector2 SpSlerp2(Vector2 a, Vector2 b, float t)
        {
            float aa = Mathf.Atan2(a.y, a.x), d = Mathf.Atan2(b.y, b.x) - aa;
            while (d > Mathf.PI) d -= 2f * Mathf.PI;
            while (d < -Mathf.PI) d += 2f * Mathf.PI;
            float r = aa + d * t;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        private static float SpSpringStep(float s, float rt, float overshoot)
        {
            if (s <= 0f) return 0f;
            float os = Mathf.Clamp(overshoot, 0.001f, 0.6f), ln = Mathf.Log(os);
            float z = -ln / Mathf.Sqrt(Mathf.PI * Mathf.PI + ln * ln), rz = Mathf.Sqrt(1f - z * z);
            float wd = (Mathf.PI - Mathf.Acos(z)) / Mathf.Max(1f, rt), wn = wd / rz;
            return 1f - Mathf.Exp(-z * wn * s) * (Mathf.Cos(wd * s) + z / rz * Mathf.Sin(wd * s));
        }

        private float SpearLength()
        {
            if (spearLen > 0f) return spearLen;
            spearLen = 53f;
            if (Futile.atlasManager != null && Futile.atlasManager.DoesContainElementWithName("SmallSpear"))
            {
                float h = Futile.atlasManager.GetElementWithName("SmallSpear").sourcePixelSize.y;
                if (h > 1f) spearLen = h;
            }
            return spearLen;
        }

        private void ApplySpearBodyPose(FoxBreedParams p)
        {
            lastSpearPitch = spearPitch;
            spearPitch = 0f;
            if (!SpearLayerOn(p)) return;
            FoxSpear S = SpearState;
            int t = SpearNow;
            if (!S.LayerActive(t)) return;
            bool on = p.SpearBodyMotion >= 1;
            float a = p.SpearRearDeg, RT = Mathf.Max(1, p.SpearRearTicks), LT = Mathf.Max(1, p.SpearRearLoadTicks);
            float pitch = 0f, load = 0f, land = 0f, shift = 0f;
            if (S.StopTick == int.MaxValue)
            {
                if (on && S.BrakeRemainTicks < LT) load = p.SpearRearLoadPx * SpEase(1f - S.BrakeRemainTicks / LT);
                if (spLoadStart != S.StartTick) { spLoadStart = S.StartTick; spLoadBrake = 0f; }
                spLoadBrake = load;
            }
            else
            {
                int t0 = S.StopTick, tRel = S.RelTick, tRelEnd = S.RelEndTick, tRecEnd = S.RecEndTick;
                if (on)
                {
                    float dip = 0f;
                    if (S.Released && t >= tRel)
                    {
                        int age = t - tRel, Sn = Mathf.Max(1, p.SpearSnapTicks), back = Mathf.Max(1, tRelEnd - tRel);
                        dip = age < Sn ? SpEase((float)age / Sn) : 1f - SpEase((float)(age - Sn) / Mathf.Max(1, back - Sn));
                    }
                    pitch = a * SpSpringStep(t - t0, RT, p.SpearRearOvershoot)
                          - a * SpSpringStep(t - tRelEnd, RT, p.SpearRearOvershoot)
                          - p.SpearReleaseDipDeg * dip;
                    if (spLoadStart != S.StartTick) { spLoadStart = S.StartTick; spLoadBrake = 0f; }
                    if (t >= t0 && t < t0 + LT) load = spLoadBrake * (1f - SpEase((t - t0) / LT));
                    int tl = tRelEnd + (int)RT;
                    float u = (t - tl) / Mathf.Max(1f, p.SpearLandSettleTicks);
                    if (tRelEnd - t0 >= RT && t >= tl && u < 1f) land = p.SpearLandCompressPx * (1f - u);
                    float lean = p.SpearWindupLeanPx;
                    if (t >= t0 && t < tRel) shift = -lean * SpEase((float)(t - t0) / Mathf.Max(1, tRel - t0));
                    else if (t >= tRel)
                    {
                        int age = t - tRel, Sn = Mathf.Max(1, p.SpearSnapTicks);
                        if (S.Released)
                        {
                            shift = age < Sn ? Mathf.Lerp(-lean, p.SpearReleaseLungePx, SpEase((float)age / Sn))
                                  : p.SpearReleaseLungePx * (1f - SpEase((float)(age - Sn) / Mathf.Max(1, tRecEnd - tRel - Sn)));
                        }
                        else
                        {
                            float planned = Mathf.Max(p.SpearWindupTicks, p.SpearRearTicks);
                            float had = -lean * SpEase((tRel - t0) / Mathf.Max(1f, planned));
                            shift = had * (1f - SpEase(age / RT));
                        }
                    }
                }
                else
                {
                    if (t >= t0) pitch = t < tRelEnd ? a * SpEase((t - t0) / RT) : a * (1f - SpEase((t - tRelEnd) / RT));
                }
            }
            spearPitch = pitch;
            float F = S.Face;
            if (load > 0f)
            {
                drawPositions[2, 0].y -= load;
                drawPositions[1, 0].y -= load * 0.75f;
                drawPositions[0, 0].y -= load * 0.5f;
            }
            if (Mathf.Abs(pitch) > 0.001f)
            {
                Vector2 rear = drawPositions[2, 0];
                for (int i = 0; i < 2; i++) drawPositions[i, 0] = rear + SpTipUp(drawPositions[i, 0] - rear, pitch, F);
            }
            if (land > 0f)
            {
                drawPositions[0, 0].y -= land;
                drawPositions[1, 0].y -= land * 0.5f;
                drawPositions[2, 0].y += land * 0.5f;
            }
            if (shift != 0f) for (int i = 0; i < 3; i++) drawPositions[i, 0].x += F * shift;
        }

        private float SpearMaxReach(FoxBreedParams p, int i)
        {
            float chain = PartsLegChain(p, i, 0f);
            float meta = chain * Mathf.Clamp(i < 2 ? p.PartsLegMetaFacFront : p.PartsLegMetaFacHind, 0f, 0.45f);
            float prox = Mathf.Max(1f, chain - meta);
            float upper = prox * Mathf.Clamp(p.LimbUpperFac, 0.15f, 0.85f);
            float pawOff = (i < 2 ? P_LEGA_PAW_W : P_LEGB_PAW_W) * partsScale * Mathf.Clamp01(p.PartsPawAnchorFac);
            return PartsLegReachLimit(p, upper + (prox - upper)) + meta + pawOff;
        }

        private LegPose SolveSpearReach(FoxBreedParams p, int i, Vector2 root, Vector2 target, float bendSmooth)
        {
            LegPose q = new LegPose();
            bool front = i < 2;
            q.root = root;
            q.chain = PartsLegChain(p, i, 0f);
            q.metaLen = q.chain * Mathf.Clamp(front ? p.PartsLegMetaFacFront : p.PartsLegMetaFacHind, 0f, 0.45f);
            float prox = Mathf.Max(1f, q.chain - q.metaLen);
            q.upperLen = prox * Mathf.Clamp(p.LimbUpperFac, 0.15f, 0.85f);
            q.lowerLen = prox - q.upperLen;
            q.reachLimit = PartsLegReachLimit(p, q.upperLen + q.lowerLen);
            float pawOff = (front ? P_LEGA_PAW_W : P_LEGB_PAW_W) * partsScale * Mathf.Clamp01(p.PartsPawAnchorFac);
            float maxReach = q.reachLimit + q.metaLen + pawOff;
            Vector2 v = target - root;
            float d = v.magnitude;
            q.reachClamped = d > maxReach + 0.01f;
            q.paw = q.reachClamped ? root + v * (maxReach / d) : target;
            Vector2 toRoot = root - q.paw;
            toRoot = toRoot.sqrMagnitude > 0.0001f ? toRoot.normalized : new Vector2(0f, 1f);
            q.mcp = q.paw + toRoot * pawOff;
            q.distal = q.mcp + toRoot * q.metaLen;
            float flip = (front ? p.PartsLegBendFront : p.PartsLegBendHind) * bendSmooth;
            q.mid = Custom.InverseKinematic(root, q.distal, q.upperLen, q.lowerLen, flip);
            return q;
        }

        private static void SpHunt(ref Vector2 pos, ref Vector2 vel, Vector2 target)
        {
            Vector2 d = target - pos;
            if (d.magnitude < 24f) vel = d;
            else vel = Vector2.Lerp(vel, d.normalized * 24f, 0.95f);
            pos += vel;
        }

        private Vector2 SpearAimPoint(FoxSpear S)
        {
            Creature t = S.Target ?? fox.chaseTarget;
            if (t == null || t.mainBodyChunk == null) return fox.mainBodyChunk.pos + new Vector2(S.Face * 100f, 0f);
            Vector2 a = t.mainBodyChunk.pos;
            float d = Vector2.Distance(a, fox.mainBodyChunk.pos);
            if (d > 200f) a.y += Mathf.Pow(d, 1.5f) * 0.05f * 0.13f;
            return a;
        }

        private Vector2 SpWindTarget(FoxBreedParams p, FoxSpear S, Vector2 pivot, Vector2 up, int t)
        {
            Vector2 aim = (SpearAimPoint(S) - pivot).normalized;
            Vector2 v = SpSlerp2(-aim, up, p.SpearWindupLift);
            float wob = Mathf.Sin(Mathf.PI / 20f * (t - S.StopTick)) * p.SpearWindupWobblePx;
            return pivot + v * p.SpearWindupBackPx + up * wob;
        }

        private Vector2 SpWeaponDir(FoxSpear S, Vector2 pivot, Vector2 hand, Vector2 up)
        {
            Vector2 n = pivot - hand;
            n = n.sqrMagnitude > 0.0001f ? n.normalized : new Vector2(0f, 1f);
            Vector2 perp = new Vector2(-n.y, n.x);
            Vector2 v = perp * -S.Face + up + (SpearAimPoint(S) - hand).normalized * 2f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : new Vector2(S.Face, 0f);
        }

        private void UpdateSpearLayer(FoxBreedParams p)
        {
            SpearHoldValid = false;
            for (int i = 0; i < 2; i++) { spLegLast[i] = spLegPos[i]; spRetSwingLast[i] = spRetSwing[i]; }
            if (!SpearLayerOn(p) || limbs == null || limbs.Length < 2)
            {
                spLegMode[0] = spLegMode[1] = 0;
                spHandOn = spHangOn = false;
                return;
            }
            FoxSpear S = SpearState;
            int t = SpearNow;
            bool on = p.SpearBodyMotion >= 1;
            Vector2 chest, fwd, up; float sign;
            PartsFrameTick(p, out chest, out fwd, out up, out sign);
            Vector2 rear = drawPositions[2, 0];
            bool seq = S.LayerActive(t) && S.StopTick != int.MaxValue;
            bool pawWindow = seq && t >= S.StopTick && t < S.RelEndTick;
            bool winding = seq && t >= S.StopTick && t < S.RelTick;
            bool releasing = seq && S.Released && t >= S.RelTick && t < S.RelEndTick;
            Spear flying = S.Flying;
            int age = t - S.RelTick;

            Vector2 pivot = PartsLegRootFrom(p, 1, rear, chest, fwd, up);
            if (pawWindow)
            {
                if (!spHandOn) { spHand = limbs[1].pos; spHandVel = Vector2.zero; spHandOn = true; spLegLast[1] = spHand; }
                Vector2 tgt = (releasing && flying != null) ? flying.firstChunk.pos : SpWindTarget(p, S, pivot, up, t);
                if (releasing && flying != null && age < p.SpearSnapTicks) { spHand = flying.firstChunk.pos; spHandVel = Vector2.zero; }
                else SpHunt(ref spHand, ref spHandVel, tgt);
                float mr = SpearMaxReach(p, 1);
                Vector2 off = spHand - pivot;
                if (off.magnitude > mr) spHand = pivot + off.normalized * mr;
                spLegMode[1] = 1;
                spLegPos[1] = spHand;
            }
            else if (spHandOn) { spHandOn = false; BeginSpearReturn(1); }

            if (pawWindow)
            {
                Vector2 r0 = PartsLegRootFrom(p, 0, rear, chest, fwd, up);
                Vector2 ht = r0 + new Vector2(0f, -(on ? p.SpearTuckDrop : 0.72f) * SpearMaxReach(p, 0)) + fwd * 3f;
                if (!spHangOn) { spHang = limbs[0].pos; spHangVel = Vector2.zero; spHangOn = true; spLegLast[0] = spHang; }
                SpHunt(ref spHang, ref spHangVel, ht);
                spLegMode[0] = on ? 2 : 1;
                spLegPos[0] = spHang;
            }
            else if (spHangOn) { spHangOn = false; BeginSpearReturn(0); }

            for (int i = 0; i < 2; i++)
            {
                if (spLegMode[i] != 3) continue;
                spRetAge[i]++;
                float s = Mathf.Clamp01((float)spRetAge[i] / FoxSpear.SWING_TICKS);
                Vector2 q = Vector2.Lerp(spRetFrom[i], limbs[i].pos, SpEase(s));
                q.y += p.StepArcLift * Mathf.Sin(Mathf.PI * s);
                spLegPos[i] = q;
                spRetSwing[i] = Mathf.Sin(Mathf.PI * s);
                spRetPhase[i] = s;
                if (s >= 1f) spLegMode[i] = 0;
            }

            Spear held = fox.HeldItem as Spear;
            if (held == null) return;
            float face = sign;
            Vector2 neck = PartsNeck(p, chest, fwd);
            Vector2 neckLine = Fox.Dir(drawPositions[1, 0], drawPositions[0, 0]);
            float lim = SpearHeadPitchLimit(p);
            Vector2 hd = PitchFromNeck(neckLine, Mathf.Clamp(headPitchDeg, -lim, lim));
            hd = Custom.DegToVec(AimDeg(hd) - wavePitchDeg * face);
            Vector2 mouth = p.SpearCarryAt >= 1 ? neck + hd * (P_HEAD_W * 0.7f * partsScale) : fox.CarryPointNow(p);
            Vector2 mouthDir = SpTipUp(hd, p.SpearMouthAngleDeg, face);
            Vector2 pos = mouth, dir = mouthDir;
            float L = SpearLength();
            if (pawWindow && spHandOn)
            {
                LegPose q = SolveSpearReach(p, 1, pivot, spHand, limbs[1].bend);
                Vector2 grip = q.paw;
                dir = winding ? SpWeaponDir(S, pivot, grip, up) : SpTipUp(fwd, p.SpearPawAngleDeg, face);
                pos = grip - dir * (L * (0.5f - Mathf.Clamp01(p.SpearGripFrac)));
                float RT = Mathf.Max(1, p.SpearRearTicks);
                if (t < S.StopTick + RT)
                {
                    float k2 = SpEase((t - S.StopTick) / RT);
                    if (on)
                    {
                        Vector2 ctrl = Vector2.Lerp(mouth, pos, 0.5f) + up * p.SpearTransferArcPx;
                        float u = 1f - k2;
                        pos = u * u * mouth + 2f * u * k2 * ctrl + k2 * k2 * pos;
                    }
                    else pos = Vector2.Lerp(mouth, pos, k2);
                    Vector2 bl = Vector2.Lerp(mouthDir, dir, k2);
                    if (bl.sqrMagnitude > 0.0001f) dir = bl.normalized;
                }
                spLastPawHold = pos;
                spLastPawDir = dir;
            }
            else if (spLegMode[1] == 3)
            {
                float k = SpEase((float)spRetAge[1] / FoxSpear.SWING_TICKS);
                pos = Vector2.Lerp(spLastPawHold, mouth, k);
                Vector2 bl = Vector2.Lerp(spLastPawDir, mouthDir, k);
                if (bl.sqrMagnitude > 0.0001f) dir = bl.normalized;
            }
            SpearHoldPos = pos;
            SpearHoldDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : new Vector2(face, 0f);
            SpearHoldValid = true;

            if (S.LayerActive(t) && t == S.RelTick - 1 && spPoseLogStart != S.StartTick)
            {
                spPoseLogStart = S.StartTick;
            }
        }

        private void BeginSpearReturn(int i)
        {
            spRetFrom[i] = spLegPos[i];
            spRetAge[i] = 0;
            spRetSwing[i] = 0f;
            spRetPhase[i] = 0f;
            spLegMode[i] = 3;
        }

        private bool SpearLegPose(FoxBreedParams p, int i, Vector2 root, Vector2 fwd, Vector2 up, float timeStacker,
                                  float bendSmooth, ref LegPose q, ref float swing, ref Vector2 pawDir)
        {
            if (i > 1 || spLegMode[i] == 0 || !SpearLayerOn(p)) return false;
            Vector2 at = Vector2.Lerp(spLegLast[i], spLegPos[i], timeStacker);
            int mode = spLegMode[i];
            if (mode == 1)
            {
                q = SolveSpearReach(p, i, root, at, bendSmooth);
                swing = 0f;
                Vector2 o = q.paw - root;
                pawDir = o.sqrMagnitude > 0.0001f ? o.normalized : fwd;
            }
            else if (mode == 2)
            {
                float fold = Mathf.Clamp01(p.SpearTuckFold);
                q = SolveLegPose(p, i, root, at, fwd, up, fold, Mathf.Clamp01(p.PartsLegFoldPeak), bendSmooth, false);
                swing = fold;
            }
            else
            {
                float s = Mathf.Lerp(spRetSwingLast[i], spRetSwing[i], timeStacker);
                q = SolveLegPose(p, i, root, at, fwd, up, s, spRetPhase[i], bendSmooth, false);
                swing = s;
            }
            return true;
        }

        private float SpearHeadPitchLimit(FoxBreedParams p)
        {
            if (!SpearLayerOn(p)) return p.HeadPitchMax;
            return Mathf.Max(p.HeadPitchMax, Mathf.Abs(headPitchDeg), Mathf.Abs(lastHeadPitchDeg));
        }

        private void ApplySpearHead(FoxBreedParams p)
        {
            if (!SpearLayerOn(p) || fox.dead) return;
            FoxSpear S = SpearState;
            int t = SpearNow;
            if (!S.LayerActive(t)) return;
            Spear flying = S.Flying;
            Vector2 lookAt;
            if (S.Released && flying != null && t - S.RelTick < p.SpearHeadPushTicks) lookAt = flying.firstChunk.pos;
            else
            {
                Creature c = S.Target ?? fox.chaseTarget;
                if (c == null || c.mainBodyChunk == null || c.room != fox.room) return;
                lookAt = c.mainBodyChunk.pos;
            }
            if (spHeadStart != S.StartTick) { spHeadStart = S.StartTick; spHeadPitch = lastHeadPitchDeg; spHeadVel = 0f; }
            bool on = p.SpearBodyMotion >= 1;
            Vector2 chest, fwd, up; float sign;
            PartsFrameTick(p, out chest, out fwd, out up, out sign);
            Vector2 neck = PartsNeck(p, chest, fwd);
            Vector2 nl = Fox.Dir(drawPositions[1, 0], drawPositions[0, 0]);
            Vector2 nu = new Vector2(-nl.y, nl.x);
            if (nu.y < 0f) nu = -nu;
            Vector2 want = Fox.Dir(neck, lookAt);
            float deg = Mathf.Atan2(Vector2.Dot(want, nu), Vector2.Dot(want, nl)) * Mathf.Rad2Deg;
            if (deg > 90f) deg = 180f - deg;
            else if (deg < -90f) deg = -180f - deg;
            deg = Mathf.Clamp(deg, -p.HeadPitchMax, p.HeadPitchMax);
            float RT = Mathf.Max(1, p.SpearRearTicks);
            if (on && S.StopTick != int.MaxValue && t >= S.StopTick && t < S.StopTick + RT)
                deg -= p.SpearTransferHeadDeg * Mathf.Sin(Mathf.PI * (t - S.StopTick) / RT);
            if (on)
            {
                spHeadVel = spHeadVel * p.SpearHeadDamp + (deg - spHeadPitch) * p.SpearHeadSpring;
                spHeadPitch += spHeadVel;
            }
            else spHeadPitch = deg;
            headPitchDeg = spHeadPitch;
        }

        private void ApplySpearTail(FoxBreedParams p, Vector2 up)
        {
            if (!SpearLayerOn(p) || tail == null || tailSegmentCount <= 0) return;
            FoxSpear S = SpearState;
            int t = SpearNow;
            if (!S.LayerActive(t) || p.SpearBodyMotion < 1) return;
            float cnt = -(spearPitch - lastSpearPitch) * p.SpearTailCounter;
            int n = tailSegmentCount;
            if (cnt != 0f)
            {
                for (int i = 0; i < n; i++)
                {
                    float tt = n == 1 ? 1f : (float)i / (n - 1);
                    tail[i].vel += up * (cnt * (0.3f + tt));
                }
            }
            if (S.Released && t >= S.RelTick && spWhipStart != S.StartTick && p.SpearTailWhip != 0f)
            {
                spWhipStart = S.StartTick;
                float w = p.SpearTailWhip;
                Vector2 back = Fox.Dir(drawPositions[0, 0], drawPositions[2, 0]);
                for (int i = 0; i < n; i++) { tail[i].vel += back * w; w /= 1.7f; }
            }
        }
    }
}
