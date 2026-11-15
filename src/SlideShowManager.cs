using Menu;
using UnityEngine;

namespace MotherMod
{
    public static class SlideShowManager
    {
        public static void Register()
        {
        }
        public static void InitiateScene(RainWorldGame game, SlideShow.SlideShowID SlideShowId, string newDen)
        {
            RainWorldGame.ForceSaveNewDenLocation(game, newDen, false);
            game.manager.nextSlideshow = SlideShowId;
            game.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.SlideShow);
        }
    }
}
