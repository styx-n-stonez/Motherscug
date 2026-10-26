using MonoMod.RuntimeDetour.HookGen;
using SlugBase.SaveData;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MotherMod
{
    public static class CravingsSystem
    {
        public const string LASTMOTHERCRAVINGS = "LASTMOTHERCRAVINGS";
        public const string MOTHERPREVCYCLE = "MOTHERPREVCYCLE";

        public delegate float orig_GetMeatMultiplier(SlugBase.DataTypes.Diet diet, Player player, Creature crit);
        public delegate float hook_GetMeatMultiplier(orig_GetMeatMultiplier orig, SlugBase.DataTypes.Diet diet, Player player, Creature crit);

        public static List<CreatureTemplate.Type> PossibleMeatCravings = new();
        public static Dictionary<CreatureTemplate.Type, float> MeatCravingsMultipliers = new();

        public static List<AbstractPhysicalObject.AbstractObjectType> PossibleCravings = new();
        public static Dictionary<AbstractPhysicalObject.AbstractObjectType, float> CravingsMultipliers = new();

        private static CreatureTemplate.Type _activeMeatCraving;
        public static CreatureTemplate.Type ActiveMeatCraving
        {
            get
            {
                return _activeMeatCraving;
            }
            set
            {
                _activeMeatCraving = value;
                CravingValue = value.value;
            }
        }
        private static AbstractPhysicalObject.AbstractObjectType _ActiveCraving;
        public static AbstractPhysicalObject.AbstractObjectType ActiveCraving
        {
            get
            {
                return _ActiveCraving;
            }
            set
            {
                _ActiveCraving = value;
                CravingValue = value.value;
            }
        }
        public static string CravingValue = string.Empty;

        public static bool IsMeat = false;

        public static float Multiplier = 1.5f;
        public static void Register()
        {
            On.SlugcatStats.NourishmentOfObjectEaten += SlugcatStats_NourishmentOfObjectEaten;
            HookEndpointManager.Add(typeof(SlugBase.DataTypes.Diet).GetMethod("GetMeatMultiplier", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic),
                (hook_GetMeatMultiplier)MeatMultiplierHK);
            On.StoryGameSession.ctor += StoryGameSession_ctor;
            On.GameSession.ctor += GameSession_ctor;
        }

        public static void LateRegister()
        {
            RegisterObjects();
            RegisterMeat();
        }

        public static void RegisterObjects()
        {
            string path = AssetManager.ResolveFilePath("MOTHER_OBJCRAVINGS.txt");
            if (File.Exists(path))
            {
                foreach(string line in File.ReadLines(path))
                {
                    string[] op = line.Split(',');
                    AbstractPhysicalObject.AbstractObjectType type = new(op[0].Trim());
                    if (type.index == -1) continue;
                    PossibleCravings.Add(type);
                    if (op.Length > 1 && int.TryParse(op[1].Trim(),out int cMul))
                    {
                        CravingsMultipliers[type] = cMul;
                    }
                    Plugin.LogSource.LogDebug("Registered object craving: {type.value}");
                }
            }
            else
            {
                Plugin.LogSource.LogDebug("object craving list not found");
            }
        }
        public static void RegisterMeat()
        {
            string path = AssetManager.ResolveFilePath("MOTHER_MEATCRAVINGS.txt");
            if (File.Exists(path))
            {
                foreach (string line in File.ReadLines(path))
                {
                    string[] op = line.Split(',');
                    CreatureTemplate.Type type = new(op[0].Trim());
                    if (type.index == -1) continue;
                    PossibleMeatCravings.Add(type);
                    if (op.Length > 1 && int.TryParse(op[1].Trim(), out int cMul))
                    {
                        MeatCravingsMultipliers[type] = cMul;
                    }
                    Plugin.LogSource.LogDebug($"Registered meat craving: {type.value}");
                }
            }
            else
            {
                Plugin.LogSource.LogDebug($"meat craving list not found");
            }
        }

        private static int SlugcatStats_NourishmentOfObjectEaten(On.SlugcatStats.orig_NourishmentOfObjectEaten orig, SlugcatStats.Name slugcatIndex, IPlayerEdible eatenobject)
        {
            if (slugcatIndex == MotherEnums.Mother &&
                ((IsMeat && (eatenobject as Creature).Template.type == ActiveMeatCraving)
                || (eatenobject as PhysicalObject).abstractPhysicalObject.type == ActiveCraving))
            {
                return Mathf.RoundToInt(orig(slugcatIndex, eatenobject) * Multiplier);
            }
            else
            {
                return orig(slugcatIndex, eatenobject);
            }
        }

        public static float MeatMultiplierHK(orig_GetMeatMultiplier orig, SlugBase.DataTypes.Diet diet, Player player, Creature crit)
        {
            if (IsMeat && player.slugcatStats.name == MotherEnums.Mother && crit.Template.type == ActiveMeatCraving)
            {
                return orig(diet, player, crit) * Multiplier;
            }
            else
            {
                return orig(diet, player, crit);
            }
        }

        private static void GameSession_ctor(On.GameSession.orig_ctor orig, GameSession self, RainWorldGame game)
        {
            orig(self, game);
            if (self is not StoryGameSession)
            {
                ActiveCraving = null;
                ActiveMeatCraving = null;
                IsMeat = false;
            }
        }

        private static void StoryGameSession_ctor(On.StoryGameSession.orig_ctor orig, StoryGameSession self, SlugcatStats.Name saveStateNumber, RainWorldGame game)
        {
            orig(self, saveStateNumber, game);
            int sum = PossibleCravings.Count + PossibleMeatCravings.Count;
            if (self.characterStats.name == MotherEnums.Mother && sum > 0)
            {

                DeathPersistentSaveData dpsd = self.saveState.deathPersistentSaveData;
                SlugBaseSaveData slugbasedata = dpsd.GetSlugBaseData();
                slugbasedata.TryGet(MOTHERPREVCYCLE, out int previousCycle);

                if (slugbasedata.TryGet(LASTMOTHERCRAVINGS, out List<string> lastcravings))
                {
                    while (lastcravings.Count > Mathf.Min(sum, 4))
                    {
                        lastcravings.RemoveAt(4);
                    }
                }
                else
                {
                    lastcravings = new List<string>();
                }

                if (dpsd.survives == previousCycle && lastcravings.Count > 0)
                {
                    string lastcraving = lastcravings[0];
                    AbstractPhysicalObject.AbstractObjectType obj = new(lastcraving);
                    if (obj.index == -1)
                    {
                        CreatureTemplate.Type tpl = new(lastcraving);
                        if (tpl.Index != -1)
                        {
                            IsMeat = true;
                            ActiveMeatCraving = tpl;

                            if (MeatCravingsMultipliers.TryGetValue(tpl, out float mul))
                            {
                                Multiplier = mul;
                            }
                            else
                            {
                                Multiplier = 1.5f;
                            }
                            Plugin.LogSource.LogDebug($"Current food craving: {(ActiveMeatCraving.value)}");
                            return;
                        }
                    }
                    else
                    {
                        IsMeat = false;
                        ActiveCraving = obj;

                        if (CravingsMultipliers.TryGetValue(obj, out var mul))
                        {
                            Multiplier = mul;
                        }
                        else
                        {
                            Multiplier = 1.5f;
                        }
                        Plugin.LogSource.LogDebug($"Current food craving: {(ActiveCraving.value)}");
                        return;
                    }
                }

                int index = -1;

                do
                {
                    index = Random.Range(0, PossibleCravings.Count + PossibleMeatCravings.Count + 1);

                    if (index >= PossibleCravings.Count)
                    {
                        IsMeat = true;
                        ActiveMeatCraving = PossibleMeatCravings[index - PossibleCravings.Count];

                        if (MeatCravingsMultipliers.TryGetValue(ActiveMeatCraving, out float mul))
                        {
                            Multiplier = mul;
                        }
                        else
                        {
                            Multiplier = 1.5f;
                        }
                    }
                    else
                    {
                        IsMeat = false;
                        ActiveCraving = PossibleCravings[index - 1];

                        if (CravingsMultipliers.TryGetValue(ActiveCraving, out var mul))
                        {
                            Multiplier = mul;
                        }
                        else
                        {
                            Multiplier = 1.5f;
                        }
                    }
                } while (lastcravings.Contains(CravingValue));

                lastcravings.Insert(0, IsMeat ? ActiveMeatCraving.value : ActiveCraving.value);

                slugbasedata.Set(LASTMOTHERCRAVINGS, lastcravings);
                slugbasedata.Set(MOTHERPREVCYCLE, dpsd.survives);

                self.saveState.progression.SaveDeathPersistentDataOfCurrentState(false, false);

                Plugin.LogSource.LogDebug($"Current food craving: {(IsMeat ? ActiveMeatCraving.value : ActiveCraving.value)} - {index}");
            }
        }
    }
}
