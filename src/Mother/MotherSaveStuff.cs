using RWCustom;
using SlugBase.SaveData;

namespace MotherMod.Mother
{
    public static class MotherSaveStuff
    {
        const string ATEROT = "MOTHER_AteRot";
        const string CHILDDEAD = "MOTHER_ChildDead";
        const string BOTHCHILDRENDEAD = "MOTHER_ChildrenDead";

        public static bool AteRot;
        public static bool OneChildDead;
        public static bool BothChildrenDead;

        public static void Register()
        {
            //those are used to avoid direct read/write to the slugbase data to avoid performance issues
            On.StoryGameSession.ctor += StoryGameSession_ctor;
            On.RainWorldGame.Win += RainWorldGame_Win;
        }

        private static void RainWorldGame_Win(On.RainWorldGame.orig_Win orig, RainWorldGame self, bool malnourished, bool fromWarpPoint)
        {
            if(self.session is StoryGameSession sess && sess.saveStateNumber == MotherEnums.Mother)
            {
                SlugBaseSaveData CustomData = sess.saveState.miscWorldSaveData.GetSlugBaseData();

                CustomData.Set(ATEROT, AteRot);
                CustomData.Set(CHILDDEAD, OneChildDead);
                CustomData.Set(BOTHCHILDRENDEAD, BothChildrenDead);
            }

            orig(self,malnourished, fromWarpPoint);
        }

        private static void StoryGameSession_ctor(On.StoryGameSession.orig_ctor orig, StoryGameSession self, SlugcatStats.Name saveStateNumber, RainWorldGame game)
        {
            orig(self, saveStateNumber, game);
            if (saveStateNumber != MotherEnums.Mother)
            {
                return;
            }

            SlugBaseSaveData MotherData = self.saveState.miscWorldSaveData.GetSlugBaseData();

            Plugin.LogSource.LogInfo("Mother session loaded\n AteRot: " +
            (AteRot = MotherData.GetData(ATEROT)) + "\n One Child Dead: " +
            (OneChildDead = MotherData.GetData(CHILDDEAD)) + "\n Both Children Dead: " +
            (BothChildrenDead = MotherData.GetData(BOTHCHILDRENDEAD)));

        }

        private static bool GetData(this SlugBaseSaveData CustomData, string key)
        {
            if(CustomData.TryGet(key, out bool value))
            {
                return value;
            }
            return false; // false by default
        }
    }
}
