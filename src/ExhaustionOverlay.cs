using UnityEngine;

namespace MotherMod
{
    public class ExhaustionOverlay : HUD.HudPart
    {
        private readonly Player player;
        private MotherConfig config;
        private FSprite vignette;
        private readonly bool active;

        private float phase;
        private float lastPhase;
        private float intensity;
        private float lastIntensity;

        public ExhaustionOverlay(HUD.HUD hud, FContainer container, Player player, MotherConfig config) : base(hud)
        {
            this.player = player;
            this.config = config;

            active = Futile.atlasManager.DoesContainElementWithName("MotherVignette");
            Plugin.Log("Overlay", $"init active={active}");
            if (!active) return;

            vignette = new FSprite("MotherVignette")
            {
                anchorX = 0.5f,
                anchorY = 0.5f,
                alpha = 0f,
            };
            container.AddChild(vignette);
        }

        public override void Update()
        {
            if (!active) return;

            lastIntensity = intensity;

            if (Plugin.Stamina.TryGet(player, out var freshConfig)) config = freshConfig;

            intensity = 0f;
            if (StaminaSystem.TryGet(player, out var data))
            {
                if (data.DangerouslyExhausted) intensity = data.ExhaustionIntensity;

                lastPhase = phase;
                phase = data.HeartPhase;
            }
        }

        public override void Draw(float timeStacker)
        {
            if (!active) return;

            float t = Mathf.Lerp(lastIntensity, intensity, timeStacker);
            if (t <= 0.001f)
            {
                vignette.alpha = 0f;
                vignette.isVisible = false;
                return;
            }
            vignette.isVisible = true;

            float target = phase < lastPhase ? phase + 1f : phase;
            float renderPhase = Mathf.Repeat(Mathf.Lerp(lastPhase, target, timeStacker), 1f);

            float pulse = config.PulseBase + config.PulseDepth * config.HeartPulse01(renderPhase);

            Vector2 screen = hud.rainWorld.options.ScreenSize;
            vignette.x = screen.x * 0.5f;
            vignette.y = screen.y * 0.5f;
            vignette.scaleX = screen.x / vignette.element.sourcePixelSize.x;
            vignette.scaleY = screen.y / vignette.element.sourcePixelSize.y;
            vignette.color = config.VignetteColor;
            vignette.alpha = Mathf.Clamp01(t * config.VignetteMaxAlpha * pulse);
        }

        public override void ClearSprites()
        {
            if (vignette != null) vignette.RemoveFromContainer();
        }
    }
}
