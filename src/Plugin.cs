using System.IO;
using BepInEx;
using UnityEngine;
using SlugBase.Features;
using static SlugBase.Features.FeatureTypes;

namespace MotherMod
{
    [BepInPlugin(MOD_ID, "The Mother", "0.2.0")]
    [BepInDependency("slime-cubed.slugbase")]
    class Plugin : BaseUnityPlugin
    {
        private const string MOD_ID = "styxnstonez.motherscug";

        public const string BREATH_SOUND = "Mother_Breath_LOOP";

        public static readonly PlayerFeature<MotherConfig> Stamina =
            new PlayerFeature<MotherConfig>("mother/core", MotherConfig.FromJson);

        public static readonly GameFeature<float> MeanLizards = GameFloat("motherscug/mean_lizards");

        public static bool DebugLogging;

        internal static BepInEx.Logging.ManualLogSource LogSource;

        public static void Log(string tag, string msg)
        {
            if (!DebugLogging) return;
            LogSource?.LogDebug($"[Mother][{tag}] {msg}");
        }

        public void OnEnable()
        {
            LogSource = base.Logger;
            LogSource.LogInfo("The Mother 0.2.0 loaded");

            if (!ExtEnumBase.TryParse(typeof(SoundID), BREATH_SOUND, false, out _))
            {
                new SoundID(BREATH_SOUND, register: true);
            }

            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            StaminaSystem.Register();
            StaminaHud.Register();
            PredatorAI.Register();

            On.Lizard.ctor += Lizard_ctor;
        }

        private const int   VIGNETTE_SIZE          = 256;
        private const float VIGNETTE_INNER_CLEAR   = 0.45f;
        private const float VIGNETTE_OUTER_OPAQUE  = 1.00f;

        private void LoadResources(RainWorld rainWorld)
        {
            if (Futile.atlasManager.GetAtlasWithName("MotherVignette") != null) return;

            string path = AssetManager.ResolveFilePath("illustrations/mother_vignette.png");
            if (File.Exists(path))
            {
                var fileTex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                AssetManager.SafeWWWLoadTexture(ref fileTex, path, clampWrapMode: true, crispPixels: false);
                Futile.atlasManager.LoadAtlasFromTexture("MotherVignette", fileTex, false);
                Log("Overlay", $"vignette source=file path={path} size={fileTex.width}x{fileTex.height}");
                return;
            }

            var tex = GenerateVignetteTexture();

            Futile.atlasManager.LoadAtlasFromTexture("MotherVignette", tex, false);
            Log("Overlay", $"vignette source=generated size={VIGNETTE_SIZE}x{VIGNETTE_SIZE} " +
                           $"innerClear={VIGNETTE_INNER_CLEAR} outerOpaque={VIGNETTE_OUTER_OPAQUE}");
        }

        private static Texture2D GenerateVignetteTexture()
        {
            int n = VIGNETTE_SIZE;
            var tex = new Texture2D(n, n, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };

            float half = (n - 1) * 0.5f;
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(VIGNETTE_INNER_CLEAR, VIGNETTE_OUTER_OPAQUE, r));
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }

        private void Lizard_ctor(On.Lizard.orig_ctor orig, Lizard self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);

            if (MeanLizards.TryGet(world.game, out float meanness))
            {
                self.spawnDataEvil = Mathf.Min(self.spawnDataEvil, meanness);
            }
        }
    }
}
