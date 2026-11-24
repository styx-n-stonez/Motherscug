using Menu;
using MoreSlugcats;
using System.Collections.Generic;
using UnityEngine;

namespace MotherMod
{
    public static class SlideShowManager
    {
        public static PlacedObject.Type CutsceneDen = new("mother_cutsceneden", true);

#warning remove this later
        public static SlideShow.SlideShowID test = new("Intro_Mother");

        public static void Register()
        {
            On.DevInterface.ObjectsPage.DevObjectGetCategoryFromPlacedType += ObjectsPage_DevObjectGetCategoryFromPlacedType;
            On.Room.Loaded += Room_Loaded;

#warning remove this
            On.Player.Update += Player_Update;
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            if (Input.GetKey(KeyCode.Slash) && self.room?.game?.session is StoryGameSession)
            {
                Plugin.LogSource.LogInfo("Cutscene test..");
                InitiateScene(self.room.game, test, "TK_TOP1");
            }
        }

        private static void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);
            if (self.game?.GetStorySession is not StoryGameSession) return;
            foreach (PlacedObject obj in self.roomSettings.placedObjects)
            {
                if (obj.type == CutsceneDen)
                {
                    self.AddObject(new PosCutsceneSpawn(self, obj));
                }
            }
        }

        private static DevInterface.ObjectsPage.DevObjectCategories ObjectsPage_DevObjectGetCategoryFromPlacedType(On.DevInterface.ObjectsPage.orig_DevObjectGetCategoryFromPlacedType orig, DevInterface.ObjectsPage self, PlacedObject.Type type)
        {
            if (type == CutsceneDen) return DevInterface.ObjectsPage.DevObjectCategories.Gameplay;
            else return orig(self, type);
        }

        public static void InitiateScene(RainWorldGame game, SlideShow.SlideShowID SlideShowId, string newDen)
        {
            RainWorldGame.ForceSaveNewDenLocation(game, newDen, false);
            game.manager.nextSlideshow = SlideShowId;
            game.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.SlideShow);
        }


        public class PosCutsceneSpawn : UpdatableAndDeletable
        {
            public PlacedObject po;

            public FadeOut fadeIn;

            private int counter = 0;

            public PosCutsceneSpawn(Room room, PlacedObject po) : base()
            {
                this.po = po;
                this.room = room;
            }

            public override void Update(bool eu)
            {
                base.Update(eu);
                if (room.game?.session is not StoryGameSession sess || sess.saveState.denPosition != room.abstractRoom.name)
                {
                    Destroy();
                }
                else
                {
                    bool CountUp = true;
                    for (int i = 0; i < room.game.Players.Count; i++)
                    {
                        if (room.game.Players[i].realizedCreature is Player player && player.room == room)
                        {
                            if (player.sceneFlag)
                            {
                                Destroy();
                                break;
                            }

                            player.sleepCurlUp = 1f;
                            player.sleepCounter = 99;
                            if (counter < 40)
                            {
                                player.SuperHardSetPosition(po.pos);
                            }
                            if (CountUp)
                            {
                                ++counter;
                                if (fadeIn is not null)
                                {
                                    if (counter < 80) fadeIn.fade = 1f;
                                    if (fadeIn.IsDoneFading())
                                    {
                                        MSCRoomSpecificScript.SetSceneFlagForAllPlayers(room.game);
                                        Destroy();
                                        break;
                                    }
                                }
                                else
                                {
                                    room.AddObject(fadeIn = new(room, Color.black, 80f, true));
                                }
                            }
                            CountUp = false;
                        }
                    }
                }
            }
        }
    }
}
