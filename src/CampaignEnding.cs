namespace MotherMod
{
    public static class CampaignEnding
    {
        public static bool End(RainWorldGame game, string mode)
        {
            if (game == null)
            {
                return false;
            }
            if (!game.IsStorySession)
            {
                return false;
            }

            if (game.manager?.upcomingProcess != null)
            {
                return false;
            }

            game.GoToRedsGameOver();

            bool switching = game.manager?.upcomingProcess != null;
            bool sceneApplied = HasSelectMenuScene(game);

            if (!switching)
            {
                Plugin.LogSource?.LogWarning(
                    "[Mother][End] WARNING campaign end did NOT start — no process switch is pending " +
                    "(a dialog may have queued it, or another mod's finalization dropped it). " +
                    "Falling back to an ordinary death; the campaign is NOT over.");
                return false;
            }

            if (!sceneApplied)
            {
                Plugin.LogSource?.LogWarning(
                    "[Mother][End] WARNING ending scene NOT applied — the campaign is ending but the " +
                    "permadeath scene was not written. Check that the_mother.json still declares " +
                    "limited_cycles (with death_menu_scene), and that no mod was toggled in Remix " +
                    "mid-session (progression refuses to save when the mod set changed).");
            }
            return true;
        }

        private static bool HasSelectMenuScene(RainWorldGame game)
        {
            var strings = game.GetStorySession?.saveState?.deathPersistentSaveData?.unrecognizedSaveStrings;
            if (strings == null) return false;
            for (int i = 0; i < strings.Count; i++)
            {
                if (strings[i] != null && strings[i].StartsWith(SELECT_SCENE_SAVE_PREFIX)) return true;
            }
            return false;
        }

        private const string SELECT_SCENE_SAVE_PREFIX = "SELECTSCENE_SlugBaseInternal_";
    }
}
