using Mono.Cecil.Cil;
using MonoMod.Cil;
using SlugBase.SaveData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotherMod.Mother;

public static class MotherHooks
{
    public static void Register()
    {
        On.Player.ObjectEaten += Player_ObjectEaten;
        IL.Player.Update += Player_Update;
        IL.Player.EatMeatUpdate += Player_EatMeatUpdate;
    }

    private static void Player_EatMeatUpdate(ILContext il)
    {
        try
        {
            ILCursor c = new(il);
            c.GotoNext(MoveType.AfterLabel, x => x.MatchLdfld<Player>("eatMeat"), x => x.MatchLdcI4(15), x => x.MatchRem(), x => x.MatchLdcI4(3));
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate(static (Player self, int graspIndex) =>
            {
                var grabbed = self.grasps[graspIndex].grabbed;
                if (self.room is not null && self.room.game.session is StoryGameSession sess && sess.saveStateNumber == MotherEnums.Mother && (grabbed is DaddyLongLegs || (grabbed is Lizard liz && liz.LizardState.rotType != LizardState.RotType.None)))
                {
                    MotherSaveStuff.AteRot = true;
                }
            });
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError("Mother Player Eat Meat hook failed miserably\n" + ex);
        }
    }

    private static void Player_Update(ILContext il)
    {
        try
        {
            ILCursor c = new(il);
            c.GotoNext(MoveType.AfterLabel, x => x.MatchLdfld<Player>("externalFoodSourceRotten"));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate(static (Player self) =>
            {
                if (self.room is not null && self.room.game.session is StoryGameSession sess && sess.saveStateNumber == MotherEnums.Mother)
                {
                    MotherSaveStuff.AteRot = true;
                }
            });
        }catch(Exception ex)
        {
            Plugin.LogSource.LogError("Mother Player Update hook failed miserably\n"+ex);
        }
    }

    private static void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
    {
        orig(self, edible);

        if(self.room is not null && self.room.game.session is StoryGameSession sess &&
            sess.saveStateNumber == MotherEnums.Mother && edible is DangleFruit { AbstrConsumable.rotted : true})
        {
            MotherSaveStuff.AteRot = true;
        }
    }
}
