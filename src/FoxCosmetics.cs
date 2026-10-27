using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotherMod
{
    public abstract class FoxCosmetic
    {
        public enum ZOrder
        {
            Behind,
            BodySurface,
            BehindHead,
            InFront
        }

        public readonly FoxGraphics gfx;

        public readonly int startSprite;

        public int numberOfSprites;

        public ZOrder zOrder = ZOrder.BodySurface;

        public RoomPalette palette;

        protected FoxCosmetic(FoxGraphics gfx, int startSprite)
        {
            this.gfx = gfx;
            this.startSprite = startSprite;
        }

        public abstract string LayerName { get; }

        public virtual bool BakedIntoHeadSprite { get { return false; } }

        public virtual bool BakedIntoTailSprite { get { return false; } }

        public virtual void Update() { }

        public virtual void Reset() { }

        public virtual void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam) { }

        public virtual void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
                                        float timeStacker, Vector2 camPos) { }

        public virtual void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            this.palette = palette;
        }

        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            for (int i = startSprite; i < startSprite + numberOfSprites; i++)
            {
                if (i >= 0 && i < sLeaser.sprites.Length) newContatiner.AddChild(sLeaser.sprites[i]);
            }
        }

        protected static Vector2 PerpOf(Vector2 v)
        {
            float m = v.magnitude;
            if (m < 0.0001f) return new Vector2(1f, 0f);
            return new Vector2(-v.y / m, v.x / m);
        }

        protected static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        protected static float AimDeg(Vector2 v)
        {
            return Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;
        }
    }

    public static class FoxCosmetics
    {
        private static readonly Dictionary<string, Func<FoxGraphics, int, FoxCosmeticSpec, FoxCosmetic>> Registry =
            new Dictionary<string, Func<FoxGraphics, int, FoxCosmeticSpec, FoxCosmetic>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ears",      (g, i, s) => new FoxEars(g, i, s) },
                { "Snout",     (g, i, s) => new FoxSnout(g, i, s) },
                { "BrushTail", (g, i, s) => new FoxBrushTail(g, i, s) },
                { "Spines",    (g, i, s) => new FoxSpines(g, i, s) },
            };

        public static FoxCosmetic Create(FoxCosmeticSpec spec, FoxGraphics gfx, int startSprite)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Layer)) return null;
            if (!Registry.TryGetValue(spec.Layer, out var factory)) return null;
            try
            {
                return factory(gfx, startSprite, spec);
            }
            catch (Exception e)
            {
                FoxFault.Warn($"cosmetic_construction_failed layer={spec.Layer} " +
                            $"err={e.GetType().Name} msg=\"{e.Message}\" action=layer_dropped");
                return null;
            }
        }
    }

    public sealed class FoxEars : FoxCosmetic
    {
        private readonly float size;
        private readonly float length;
        private readonly float width;
        private readonly float spread;
        private readonly float tilt;
        private readonly float flatten;
        private readonly Color color;

        public override string LayerName { get { return "Ears"; } }

        public override bool BakedIntoHeadSprite { get { return true; } }

        public FoxEars(FoxGraphics gfx, int startSprite, FoxCosmeticSpec spec) : base(gfx, startSprite)
        {
            numberOfSprites = 2;
            zOrder = ZOrder.BehindHead;

            size = spec.F("size", 1f);
            length = spec.F("length", 11f);
            width = spec.F("width", 4.5f);
            spread = spec.F("spread", 0.8f);
            tilt = spec.F("tilt", 22f);
            flatten = spec.F("flatten", 35f);
            color = spec.C("color", gfx.fox.P.ColorTail);
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            for (int i = 0; i < 2; i++)
            {
                var tris = new TriangleMesh.Triangle[1] { new TriangleMesh.Triangle(0, 1, 2) };
                sLeaser.sprites[startSprite + i] = new TriangleMesh("Futile_White", tris, customColor: false);
            }
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
                                         float timeStacker, Vector2 camPos)
        {
            Vector2 headPos = gfx.DrawHeadPos;
            Vector2 axis = gfx.DrawHeadDir;
            Vector2 up = gfx.DrawHeadUp;

            float rad = gfx.HeadRad;
            float crouch = Mathf.Clamp01(gfx.fox.StalkCrouch);

            for (int i = 0; i < 2; i++)
            {
                float side = (i == 0) ? 1f : -1f;
                Vector2 root = headPos + up * (rad * 0.45f) + axis * (side * spread * rad);

                float fold = flatten * crouch + (gfx.fox.Consious ? 0f : flatten);
                float facing = (axis.x == 0f) ? 1f : Mathf.Sign(axis.x);
                Vector2 dir = Rotate(up, side * tilt + fold * facing);
                Vector2 tip = root + dir * (length * size);
                Vector2 half = PerpOf(dir) * (width * size * 0.5f);

                var mesh = sLeaser.sprites[startSprite + i] as TriangleMesh;
                if (mesh == null) continue;
                mesh.MoveVertice(0, root - half - camPos);
                mesh.MoveVertice(1, root + half - camPos);
                mesh.MoveVertice(2, tip - camPos);
                mesh.color = color;
            }
        }
    }

    public sealed class FoxSnout : FoxCosmetic
    {
        private readonly float length;
        private readonly float width;
        private readonly float tipWidth;
        private readonly float droop;
        private readonly Color color;
        private readonly float noseSize;
        private readonly Color noseColor;

        public override string LayerName { get { return "Snout"; } }

        public override bool BakedIntoHeadSprite { get { return true; } }

        public FoxSnout(FoxGraphics gfx, int startSprite, FoxCosmeticSpec spec) : base(gfx, startSprite)
        {
            numberOfSprites = 2;
            zOrder = ZOrder.InFront;

            length = spec.F("length", 9f);
            width = spec.F("width", 5.5f);
            tipWidth = spec.F("tip_width", 2.6f);
            droop = spec.F("droop", 0.12f);
            color = spec.C("color", gfx.fox.P.ColorTail);
            noseSize = spec.F("nose_size", 2.4f);
            noseColor = spec.C("nose_color", gfx.fox.P.ColorBody);
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            var tris = new TriangleMesh.Triangle[2]
            {
                new TriangleMesh.Triangle(0, 1, 2),
                new TriangleMesh.Triangle(1, 2, 3),
            };
            sLeaser.sprites[startSprite] = new TriangleMesh("Futile_White", tris, customColor: false);
            sLeaser.sprites[startSprite + 1] = new FSprite("Circle20");
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
                                         float timeStacker, Vector2 camPos)
        {
            Vector2 headPos = gfx.DrawHeadPos;
            Vector2 dir = gfx.DrawHeadDir;
            if (dir.sqrMagnitude < 0.01f) dir = gfx.BodyAxis;
            dir = (dir + new Vector2(0f, -droop)).normalized;

            Vector2 root = headPos + dir * (gfx.HeadRad * 0.2f);
            Vector2 tip = root + dir * length;
            Vector2 halfRoot = PerpOf(dir) * (width * 0.5f);
            Vector2 halfTip = PerpOf(dir) * (tipWidth * 0.5f);

            var mesh = sLeaser.sprites[startSprite] as TriangleMesh;
            if (mesh != null)
            {
                mesh.MoveVertice(0, root - halfRoot - camPos);
                mesh.MoveVertice(1, root + halfRoot - camPos);
                mesh.MoveVertice(2, tip - halfTip - camPos);
                mesh.MoveVertice(3, tip + halfTip - camPos);
                mesh.color = color;
            }

            FSprite nose = sLeaser.sprites[startSprite + 1];
            nose.x = tip.x - camPos.x;
            nose.y = tip.y - camPos.y;
            nose.scale = noseSize / 10f;
            nose.color = FoxGraphics.NoHole(gfx.fox.P, noseColor);
        }
    }

    public sealed class FoxBrushTail : FoxCosmetic
    {
        private readonly int tufts;
        private readonly float tuftSize;
        private readonly float spread;
        private readonly float droop;
        private readonly float sizeToward;
        private readonly Color color;

        public override string LayerName { get { return "BrushTail"; } }

        public override bool BakedIntoTailSprite { get { return true; } }

        public FoxBrushTail(FoxGraphics gfx, int startSprite, FoxCosmeticSpec spec) : base(gfx, startSprite)
        {
            tufts = Mathf.Clamp(spec.I("tufts", 9), 0, 32);
            numberOfSprites = tufts * 2;
            zOrder = ZOrder.Behind;

            tuftSize = spec.F("tuft_size", 2.2f);
            spread = spec.F("tuft_spread", 0.85f);
            droop = spec.F("tuft_droop", 0.3f);
            sizeToward = spec.F("tuft_follow_profile", 1f);
            color = spec.C("color", gfx.fox.P.ColorTail);
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            for (int i = 0; i < numberOfSprites; i++)
            {
                sLeaser.sprites[startSprite + i] = new FSprite("Circle20");
            }
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
                                         float timeStacker, Vector2 camPos)
        {
            if (tufts == 0) return;

            int segments = gfx.TailSegmentCount;
            Vector2 rear = Vector2.Lerp(gfx.drawPositions[2, 1], gfx.drawPositions[2, 0], timeStacker);

            for (int t = 0; t < tufts; t++)
            {
                float u = (tufts == 1) ? 0.5f : (float)t / (tufts - 1);

                float scaled = u * segments;
                int idx = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, segments - 1);
                float frac = Mathf.Clamp01(scaled - idx);
                Vector2 from = (idx == 0) ? rear
                    : Vector2.Lerp(gfx.tail[idx - 1].lastPos, gfx.tail[idx - 1].pos, timeStacker);
                Vector2 to = Vector2.Lerp(gfx.tail[idx].lastPos, gfx.tail[idx].pos, timeStacker);
                Vector2 at = Vector2.Lerp(from, to, frac);
                Vector2 perp = PerpOf(to - from);

                float rad = Mathf.Lerp(tuftSize, gfx.TailRad(idx) * (tuftSize / 5f), Mathf.Clamp01(sizeToward));

                float edge = gfx.TailRad(idx);

                for (int s = 0; s < 2; s++)
                {
                    float side = (s == 0) ? 1f : -1f;
                    Vector2 pos = at + perp * (side * edge * spread) + new Vector2(0f, -droop * rad);
                    FSprite sprite = sLeaser.sprites[startSprite + t * 2 + s];
                    sprite.x = pos.x - camPos.x;
                    sprite.y = pos.y - camPos.y;
                    sprite.scale = rad / 10f;
                    sprite.color = color;
                }
            }
        }
    }

    public sealed class FoxSpines : FoxCosmetic
    {
        private struct Quill
        {
            public Vector2 pos;
            public Vector2 lastPos;
            public Vector2 vel;
            public float u;
            public float len;
            public float angle;
            public float artLen;
        }

        private readonly Quill[] quills;
        private readonly string[] elements;
        private readonly int[] slots;
        private readonly float lift;
        private readonly float widthFac;
        private readonly float stiffness;
        private readonly float damping;
        private readonly float maxForce;
        private readonly float maxStretch;
        private readonly float artAimDeg;
        private readonly float anchorX;
        private readonly float anchorY;
        private readonly Color color;
        private bool seeded;

        public override string LayerName { get { return "Spines"; } }

        public FoxSpines(FoxGraphics gfx, int startSprite, FoxCosmeticSpec spec) : base(gfx, startSprite)
        {
            FoxBreedParams p = gfx.fox.P;

            int count = Mathf.Clamp(spec.I("count", 5), 0, 24);

            string baseName = spec.S("element", "Spine");
            int variants = Mathf.Clamp(spec.I("art_variants", 3), 1, 16);
            string suffix = p.PartsMask ? "Mask" : "";
            elements = new string[variants];
            int usable = 0;
            for (int i = 0; i < variants; i++)
            {
                string el = p.PartsPrefix + baseName + (i + 1) + suffix;
                elements[i] = FoxAtlas.Has(el) ? el : null;
                if (elements[i] != null) usable++;
            }
            if (usable == 0)
            {
                numberOfSprites = 0;
                quills = new Quill[0];
                slots = new int[0];
                return;
            }

            numberOfSprites = count;
            zOrder = spec.B("behind_body", true) ? ZOrder.Behind : ZOrder.BodySurface;

            float alongStart = spec.F("along_start", 0.06f);
            float alongEnd = spec.F("along_end", 0.42f);

            float length = spec.F("length", 43f);
            float size = spec.F("size", 1f);
            float taper = spec.F("taper", 33f / 43f);
            float taperExp = Mathf.Max(0.01f, spec.F("taper_exp", 1f));

            float angleStart = spec.F("angle_start", 55f);
            float angleEnd = spec.F("angle_end", -5f);

            lift = spec.F("lift", 0f);
            widthFac = spec.F("width_fac", 1f);

            stiffness = Mathf.Max(0.05f, spec.F("stiffness", 5f));
            damping = Mathf.Clamp01(spec.F("damping", 0.85f));
            maxForce = Mathf.Max(0f, spec.F("max_force", 10f));
            maxStretch = Mathf.Max(1f, spec.F("max_stretch", 1f));

            artAimDeg = spec.F("art_aim_deg", -90f);
            anchorX = spec.F("art_anchor_x", 1f);
            anchorY = spec.F("art_anchor_y", 0.5f);

            color = spec.C("color", gfx.ArtTint(p.ColorTail));

            float scale = gfx.PartsArtScale;
            quills = new Quill[count];
            slots = new int[count];
            for (int i = 0; i < count; i++)
            {
                float t = (count == 1) ? 0f : (float)i / (count - 1);
                float curve = Mathf.Pow(t, taperExp);
                int slot = Mathf.Clamp(Mathf.FloorToInt(t * variants), 0, variants - 1);
                if (elements[slot] == null)
                {
                    for (int j = 0; j < variants; j++) if (elements[j] != null) { slot = j; break; }
                }
                quills[i].u = Mathf.Lerp(alongStart, alongEnd, t);
                quills[i].len = Mathf.Max(0.5f, length * Mathf.Lerp(1f, taper, curve) * size * scale);
                quills[i].angle = Mathf.Lerp(angleStart, angleEnd, curve);
                quills[i].artLen = 1f;
                slots[i] = slot;
            }
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            for (int i = 0; i < numberOfSprites; i++)
            {
                string el = elements[slots[i]];
                var sprite = new FSprite(el);
                sprite.anchorX = anchorX;
                sprite.anchorY = anchorY;
                sLeaser.sprites[startSprite + i] = sprite;
                if (sprite.element != null && sprite.element.sourcePixelSize.x > 0.5f)
                {
                    quills[i].artLen = sprite.element.sourcePixelSize.x;
                }
            }
            Reset();
        }

        private Vector2 Rest(int i, float timeStacker, float extraDeg, out Vector2 root)
        {
            Vector2 outward;
            root = gfx.BackPoint(quills[i].u, timeStacker, out outward) + outward * lift;

            Vector2 side = new Vector2(outward.y, -outward.x);
            Vector2 rear = (Vector2.Dot(side, -gfx.BodyAxis) >= 0f) ? side : -side;

            float sign = (rear.x * outward.y - rear.y * outward.x >= 0f) ? 1f : -1f;
            float angle = quills[i].angle;
            if (extraDeg != 0f) angle += extraDeg;
            return root + Rotate(rear, sign * angle) * quills[i].len;
        }

        internal int QuillCount { get { return quills.Length; } }

        internal Vector2 QuillTip(int i)
        {
            return quills[i].pos;
        }

        public override void Reset()
        {
            for (int i = 0; i < quills.Length; i++)
            {
                Vector2 root;
                quills[i].pos = Rest(i, 1f, 0f, out root);
                quills[i].lastPos = quills[i].pos;
                quills[i].vel = Vector2.zero;
            }
            seeded = quills.Length > 0;
        }

        public override void Update()
        {
            if (!seeded) { Reset(); return; }

            for (int i = 0; i < quills.Length; i++)
            {
                Vector2 root;
                Vector2 ideal = Rest(i, 1f, gfx.PheromoneWiggleDeg(i), out root);

                quills[i].vel += Vector2.ClampMagnitude(ideal - quills[i].pos, maxForce) / stiffness;
                quills[i].vel *= damping;

                Vector2 d = quills[i].pos - root;
                float dist = d.magnitude;
                if (dist < 0.0001f)
                {
                    d = ideal - root;
                    dist = Mathf.Max(0.0001f, d.magnitude);
                }
                float want = Mathf.Clamp(dist, quills[i].len, quills[i].len * maxStretch);
                if (!Mathf.Approximately(want, dist))
                {
                    Vector2 fix = d / dist * (want - dist);
                    quills[i].pos += fix;
                    quills[i].vel += fix;
                }

                quills[i].lastPos = quills[i].pos;
                quills[i].pos += quills[i].vel;
            }
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
                                         float timeStacker, Vector2 camPos)
        {
            float lenFac = Mathf.Lerp(1f,
                                      Mathf.Clamp(gfx.fox.P.SqueezeSpineLenFac, 0.05f, 1f),
                                      gfx.fox.DrawSqueezeAmount);

            for (int i = 0; i < numberOfSprites; i++)
            {
                Vector2 outward;
                Vector2 root = gfx.BackPoint(quills[i].u, timeStacker, out outward) + outward * lift;
                Vector2 tip = Vector2.Lerp(quills[i].lastPos, quills[i].pos, timeStacker);
                Vector2 along = tip - root;
                float dist = along.magnitude;
                if (dist < 0.0001f) { along = outward; dist = quills[i].len; }

                FSprite sprite = sLeaser.sprites[startSprite + i];
                sprite.x = root.x - camPos.x;
                sprite.y = root.y - camPos.y;
                sprite.rotation = AimDeg(along) - artAimDeg;
                sprite.scaleX = dist * lenFac / Mathf.Max(0.5f, quills[i].artLen);
                sprite.scaleY = widthFac;
                sprite.color = color;
            }
        }
    }
}
