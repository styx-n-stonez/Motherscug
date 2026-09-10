using UnityEngine;

namespace MotherMod
{
    public class StaminaBar : HUD.HudPart
    {
        private readonly Player player;
        private MotherConfig config;
        private readonly HUD.HUDCircle[,] circles;
        private readonly int pipCount;

        private float fade;
        private float lastFade;
        private int visibleCounter;
        private float curStamina;
        private float lastStamina;
        private bool dangerous;
        private float heartPhase;

        public StaminaBar(HUD.HUD hud, FContainer container, Player player, MotherConfig config) : base(hud)
        {
            this.player = player;
            this.config = config;
            curStamina = config.StaminaMax;
            lastStamina = config.StaminaMax;

            pipCount = Mathf.Max(1, config.HudPipCount);
            circles = new HUD.HUDCircle[pipCount, 2];
            for (int i = 0; i < pipCount; i++)
            {
                circles[i, 0] = new HUD.HUDCircle(hud, HUD.HUDCircle.SnapToGraphic.FoodCircleA, container, 0);
                circles[i, 1] = new HUD.HUDCircle(hud, HUD.HUDCircle.SnapToGraphic.FoodCircleB, container, 0);
                circles[i, 1].forceColor = config.HudFillColor;
            }
        }

        private Vector2 GetBasePos()
        {
            if (hud.foodMeter != null)
            {
                return hud.foodMeter.pos + new Vector2(0f, config.HudYOffset);
            }

            Vector2 off = (hud.rainWorld != null && hud.rainWorld.options != null)
                ? hud.rainWorld.options.SafeScreenOffset
                : Vector2.zero;
            return new Vector2(
                Mathf.Max(50f, off.x + 5.5f),
                Mathf.Max(25f, off.y + 17.25f) + config.HudYOffset);
        }

        public override void Update()
        {
            lastFade = fade;
            lastStamina = curStamina;

            if (Plugin.Stamina.TryGet(player, out var freshConfig))
            {
                config = freshConfig;
            }

            if (StaminaSystem.TryGet(player, out var data))
            {
                curStamina = data.stamina;
                dangerous = data.DangerouslyExhausted;
                heartPhase = data.HeartPhase;
            }

            bool forceShow = hud.owner.RevealMap || hud.showKarmaFoodRain;
            if (curStamina < config.HudShowBelow)
            {
                visibleCounter = config.HudRevealFrames;
            }

            bool wantVisible = forceShow || visibleCounter > 0;
            if (!forceShow && visibleCounter > 0)
            {
                visibleCounter--;
            }

            fade = wantVisible
                ? Mathf.Min(1f, fade + config.HudFadeIn)
                : Mathf.Max(0f, fade - config.HudFadeOut);

            for (int i = 0; i < pipCount; i++)
            {
                circles[i, 0].Update();
                circles[i, 1].Update();
            }
        }

        public override void Draw(float timeStacker)
        {
            float drawFade = Mathf.Lerp(lastFade, fade, timeStacker);
            float max = Mathf.Max(1f, config.StaminaMax);
            float frac = Mathf.Clamp01(Mathf.Lerp(lastStamina, curStamina, timeStacker) / max);

            if (drawFade < 0.005f)
            {
                for (int i = 0; i < pipCount; i++)
                {
                    circles[i, 0].fade = 0f;
                    circles[i, 1].fade = 0f;
                    circles[i, 0].Draw(timeStacker);
                    circles[i, 1].Draw(timeStacker);
                }
                return;
            }

            int filled = Mathf.RoundToInt(frac * pipCount);

            int zonePips = Mathf.Clamp(Mathf.CeilToInt(config.OverexertEnter / max * pipCount), 0, pipCount);

            float pulse = dangerous && config.HudPulseScale > 0f
                ? 1f + config.HudPulseScale * config.HeartPulse01(heartPhase)
                : 1f;
            float pipScale = config.HudPipScale * pulse;

            Vector2 basePos = GetBasePos();
            for (int i = 0; i < pipCount; i++)
            {
                bool isFilled = i < filled;
                bool inZone = i < zonePips;
                Vector2 p = basePos + new Vector2(i * config.HudPipSpacing, 0f);

                circles[i, 0].pos = p;
                circles[i, 0].fade = isFilled ? drawFade : drawFade * config.HudEmptyDim;
                circles[i, 0].forceColor = (inZone || dangerous) ? config.HudZoneColor : (Color?)null;
                circles[i, 0].Draw(timeStacker);
                circles[i, 0].sprite.scale *= pipScale;

                bool redFill = dangerous && inZone;
                circles[i, 1].pos = p;
                circles[i, 1].fade = (redFill || isFilled) ? drawFade : 0f;
                circles[i, 1].forceColor = redFill ? config.HudZoneColor : config.HudFillColor;
                circles[i, 1].Draw(timeStacker);
                circles[i, 1].sprite.scale *= pipScale;
            }
        }

        public override void ClearSprites()
        {
        }
    }

    public static class StaminaHud
    {
        public static void Register()
        {
            On.HUD.HUD.ctor += HUD_ctor;
        }

        private static void HUD_ctor(On.HUD.HUD.orig_ctor orig, HUD.HUD self,
            FContainer[] fContainers, RainWorld rainWorld, HUD.IOwnAHUD owner)
        {
            orig(self, fContainers, rainWorld, owner);

            if (owner is Player player && Plugin.Stamina.TryGet(player, out var config))
            {
                self.parts.Add(new StaminaBar(self, fContainers[1], player, config));
                self.parts.Add(new ExhaustionOverlay(self, fContainers[1], player, config));
            }
        }
    }
}
