using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using CreatureType = CreatureTemplate.Type;

namespace MotherMod
{
    internal static class FoxSandboxIcon
    {
        private static bool attempted;

        internal static void Install()
        {
            if (attempted) return;
            attempted = true;

            try
            {
                On.ArenaBehaviors.SandboxEditor.CreatureOrItemIcon.GlowColor += GlowColor;
            }
            catch (System.Exception e)
            {
                FoxFault.Warn($"sandbox_glow_hook_failed target=CreatureOrItemIcon.GlowColor " +
                              $"err={e.GetType().Name} msg=\"{e.Message}\" action=vanilla_white_glow_kept");
            }

            try
            {
                On.ArenaBehaviors.SandboxEditor.CreatureOrItemIcon.GlowAlpha += GlowAlpha;
            }
            catch (System.Exception e)
            {
                FoxFault.Warn($"sandbox_glow_hook_failed target=CreatureOrItemIcon.GlowAlpha " +
                              $"err={e.GetType().Name} msg=\"{e.Message}\" action=vanilla_glow_alpha_kept");
            }

            try
            {
                On.ArenaBehaviors.SandboxEditor.CreatureOrItemIcon.DrawSprites += DrawSpritesProbe;
            }
            catch (System.Exception e)
            {
                FoxFault.Warn($"sandbox_glow_hook_failed target=CreatureOrItemIcon.DrawSprites " +
                              $"err={e.GetType().Name} msg=\"{e.Message}\" action=art_copy_not_suppressed");
            }
            try
            {
            }
            catch (System.Exception e)
            {
                FoxFault.Warn($"sandbox_glow_hook_failed target=CreatureOrItemIcon.GlowAlpha " +
                              $"err={e.GetType().Name} msg=\"{e.Message}\" action=vanilla_glow_alpha_kept");
            }
        }

        private static string VariantOf(ArenaBehaviors.SandboxEditor.CreatureOrItemIcon self)
        {
            if (self == null) return null;
            CreatureType type = self.iconData.critType;
            if (!FoxCritob.IsFoxType(type)) return null;
            return FoxCritob.VariantIdFor(type);
        }

        private static Color GlowColor(
            On.ArenaBehaviors.SandboxEditor.CreatureOrItemIcon.orig_GlowColor orig,
            ArenaBehaviors.SandboxEditor.CreatureOrItemIcon self, float timeStacker)
        {
            string variant = VariantOf(self);
            if (variant == null) return orig(self, timeStacker);

            FoxBreedParams p = FoxParams.For(variant);
            if (!p.SandboxGlowEnabled) return orig(self, timeStacker);

            Color c = p.SandboxGlowColor;
            return new Color(c.r, c.g, c.b, Mathf.Clamp01(p.SandboxGlowAlpha));
        }

        private static float GlowAlpha(
            On.ArenaBehaviors.SandboxEditor.CreatureOrItemIcon.orig_GlowAlpha orig,
            ArenaBehaviors.SandboxEditor.CreatureOrItemIcon self, float timeStacker)
        {
            string variant = VariantOf(self);
            if (variant == null) return orig(self, timeStacker);

            FoxBreedParams p = FoxParams.For(variant);
            if (!p.SandboxGlowEnabled) return orig(self, timeStacker);

            return p.SandboxGlowAlpha;
        }

        private static readonly Dictionary<string, int> ProbeFrames = new Dictionary<string, int>();

        private const int PROBE_AT_FRAME = 120;

        private static void DrawSpritesProbe(
            On.ArenaBehaviors.SandboxEditor.CreatureOrItemIcon.orig_DrawSprites orig,
            ArenaBehaviors.SandboxEditor.CreatureOrItemIcon self,
            RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, UnityEngine.Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            string variant = VariantOf(self);
            if (variant == null) return;

            FoxBreedParams pp = FoxParams.For(variant);
            if (pp.SandboxHideArtCopy && sLeaser?.sprites != null && sLeaser.sprites.Length > 2
                && sLeaser.sprites[2] != null)
            {
                sLeaser.sprites[2].isVisible = false;
            }

            return;
#pragma warning disable 0162

            try
            {
                return;
                int n = ProbeFrames.TryGetValue(variant, out int prev) ? prev + 1 : 1;
                ProbeFrames[variant] = n;
                if (n != PROBE_AT_FRAME) return;

                var sb = new System.Text.StringBuilder();
                sb.Append($"sandbox_leaser_probe variant={variant} frame={n} count={sLeaser?.sprites?.Length ?? -1}");
                if (sLeaser?.sprites != null)
                {
                    for (int i = 0; i < sLeaser.sprites.Length; i++)
                    {
                        var sp = sLeaser.sprites[i];
                        sb.Append($" [{i}] elem={sp?.element?.name ?? "<null>"}");
                        sb.Append($" col=#{(sp == null ? "??????" : ColorToHexUtility(sp.color))}");
                        sb.Append($" a={(sp == null ? -1f : sp.alpha):0.00}");
                        sb.Append($" sc={(sp == null ? -1f : sp.scale):0.00}");
                        sb.Append($" shdr={sp?.shader?.name ?? "<null>"}");
                        sb.Append($" vis={(sp != null && sp.isVisible)}");
                    }
                }
                var sym = self.symbol;
                var ss = sym?.symbolSprite;
                sb.Append($" || sym elem={ss?.element?.name ?? "<null>"}");
                sb.Append($" col=#{(ss == null ? "??????" : ColorToHexUtility(ss.color))}");
                sb.Append($" a={(ss == null ? -1f : ss.alpha):0.00}");
                sb.Append($" flash={(sym == null ? -1f : sym.showFlash):0.00}");
            }
            catch (System.Exception)
            {
            }
#pragma warning restore 0162
        }

        private static string ColorToHexUtility(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            int a = Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
            return $"{r:X2}{g:X2}{b:X2}{a:X2}";
        }
}
}
