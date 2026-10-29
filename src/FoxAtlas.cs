using System.Collections.Generic;
using System.IO;

namespace MotherMod
{
    internal static class FoxAtlas
    {
        private static readonly string[] ATLAS_PATHS =
        {
            "atlases/fox_ref", "atlases/fox_tofu", "atlases/fox_milk", "atlases/fox_popcorn", "atlases/fox_corn",
            "atlases/fox_cherry", "atlases/fox_mushroom", "atlases/fox_potato", "atlases/fox_toby",
        };

        private static readonly HashSet<string> Names = new HashSet<string>();

        internal static bool Loaded { get; private set; }

        internal static int Count { get { return Names.Count; } }

        internal static bool Has(string element)
        {
            return !string.IsNullOrEmpty(element) && Names.Contains(element);
        }

        internal static FAtlasElement Element(string element)
        {
            if (!Has(element)) return null;
            try { return Futile.atlasManager.GetElementWithName(element); }
            catch { return null; }
        }

        internal static void Load()
        {
            Loaded = true;
            missingThisAttempt = 0;
            for (int i = 0; i < ATLAS_PATHS.Length; i++) LoadOne(ATLAS_PATHS[i]);
            if (missingThisAttempt > 0) Loaded = false;
        }

        private static int missingThisAttempt;

        private static int retries;

        private const int MAX_RETRIES = 4;

        internal static void EnsureLoaded()
        {
            if (Loaded || retries >= MAX_RETRIES) return;
            retries++;
            Load();
        }

        private static void LoadOne(string ATLAS_PATH)
        {
            string png = AssetManager.ResolveFilePath(ATLAS_PATH + ".png");
            if (!File.Exists(png))
            {
                missingThisAttempt++;
                return;
            }

            FAtlas atlas;
            try
            {
                atlas = Futile.atlasManager.LoadAtlas(ATLAS_PATH);
            }
            catch (System.Exception ex)
            {
                FoxFault.Warn($"graphics_atlas elements=0 path={ATLAS_PATH} err={ex.GetType().Name} " +
                            $"msg=\"{ex.Message}\" action=mesh_head_and_mesh_legs");
                return;
            }

            if (atlas == null || atlas.elements == null)
            {
                return;
            }

            var mine = new List<string>();
            for (int i = 0; i < atlas.elements.Count; i++)
            {
                if (atlas.elements[i] == null) continue;
                Names.Add(atlas.elements[i].name);
                mine.Add(atlas.elements[i].name);
            }

            mine.Sort(System.StringComparer.Ordinal);
        }
    }
}
