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
            elements = new string[variants];
            int usable = 0;
            for (int i = 0; i < variants; i++)
            {
                string el = p.PartsPrefix + baseName + (i + 1);
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

            color = spec.C("color", Color.white);

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
