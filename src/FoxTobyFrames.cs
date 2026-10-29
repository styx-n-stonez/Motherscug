using UnityEngine;

namespace MotherMod
{
    public partial class FoxGraphics
    {
        internal const string TOBY_FRAME_PREFIX = "FoxToby_Frame";
        private const int TOBY_FRAME_COUNT = 4;
        private const float TOBY_FRAME_W = 22f;
        private const float TOBY_FRAME_H = 19f;
        private const float TOBY_FRAME_BODY_MID_PX = 10f;

        private bool tobyFramesActive;
        private FAtlasElement[] tobyFrameEl;
        private int tobyFrameClock;
        private bool tobyDrawLogged;
        private float tobyLoggedFace;
        private int tobyLoggedTicks = int.MinValue;
        private float tobyLoggedScale = float.NaN, tobyLoggedFeet = float.NaN;

        internal bool TobyFramesActive { get { return tobyFramesActive; } }

        private int SpriteTobyFrame { get { return StartOfExtraSprites + extraSprites; } }

        internal Vector2 TobyFrameNose { get { return drawPositions[0, 0]; } }

        private bool ResolveTobyFrames(FoxBreedParams p)
        {
            if (p.TobyDraw != 3) return false;
            var els = new FAtlasElement[TOBY_FRAME_COUNT];
            for (int i = 0; i < TOBY_FRAME_COUNT; i++)
            {
                string name = TOBY_FRAME_PREFIX + (i + 1);
                els[i] = FoxAtlas.Element(name);
                if (els[i] == null)
                {
                    return false;
                }
            }
            tobyFrameEl = els;
            tobyFrameClock = fox.abstractCreature.ID.RandomSeed & 0x7FFF;
            return true;
        }

        private void TickTobyFrames(FoxBreedParams p)
        {
            if (!tobyFramesActive) return;
            tobyFrameClock++;
            if (tobyFrameClock > 1000000000) tobyFrameClock = 0;
            if (p.TobyFrameTicks != tobyLoggedTicks || p.TobyFrameScale != tobyLoggedScale
                || p.TobyFrameFeetPx != tobyLoggedFeet)
            {
                tobyLoggedTicks = p.TobyFrameTicks;
                tobyLoggedScale = p.TobyFrameScale;
                tobyLoggedFeet = p.TobyFrameFeetPx;
            }
        }

        private int TobyFrameIndex(FoxBreedParams p)
        {
            int t = p.TobyFrameTicks;
            return t > 0 ? (tobyFrameClock / t) % TOBY_FRAME_COUNT : 0;
        }

        private void InitiateTobyFrameSprite(RoomCamera.SpriteLeaser sLeaser)
        {
            var s = new FSprite(tobyFrameEl[0]);
            s.anchorX = TOBY_FRAME_BODY_MID_PX / TOBY_FRAME_W;
            sLeaser.sprites[SpriteTobyFrame] = s;
        }

        private void AddTobyFrameToContainer(RoomCamera.SpriteLeaser sLeaser, FContainer c)
        {
            c.AddChild(sLeaser.sprites[SpriteTobyFrame]);
            if (sLeaser.containers != null && sLeaser.containers.Length > 0) c.AddChild(sLeaser.containers[0]);
        }

        private void DrawTobyFrame(RoomCamera.SpriteLeaser sLeaser, FoxBreedParams p, Vector2 headChunk,
                                   Vector2 rearChunk, float depth, Vector2 camPos)
        {
            FSprite s = sLeaser.sprites[SpriteTobyFrame];
            if (s == null) return;
            int idx = TobyFrameIndex(p);
            if (s.element != tobyFrameEl[idx]) s.element = tobyFrameEl[idx];

            float faceSign = (depth >= 0f) ? 1f : -1f;
            float sc = Mathf.Max(0.05f, p.TobyFrameScale);
            Vector2 fwd = DrawAxis(headChunk, rearChunk);
            Vector2 at = (headChunk + rearChunk) * 0.5f;
            float fore = TurnForeshorten(p, depth);

            s.x = at.x - camPos.x;
            s.y = at.y - camPos.y;
            s.rotation = AimDeg(fwd) - 90f * faceSign;
            s.scaleX = -sc * faceSign * fore;
            s.scaleY = sc;
            s.anchorX = TOBY_FRAME_BODY_MID_PX / TOBY_FRAME_W;
            s.anchorY = p.TobyFrameFeetPx / (sc * TOBY_FRAME_H);
            s.color = Color.white;

            if (!tobyDrawLogged || faceSign != tobyLoggedFace)
            {
                tobyDrawLogged = true;
                tobyLoggedFace = faceSign;
            }
        }
    }
}
