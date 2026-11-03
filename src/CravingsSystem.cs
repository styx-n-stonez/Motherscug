using HUD;
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

        const int MaximumLastCravings = 5;
        const float DefaultMultiplier = 1.5f;

        public delegate float orig_GetMeatMultiplier(SlugBase.DataTypes.Diet diet, Player player, Creature crit);
        public delegate float hook_GetMeatMultiplier(orig_GetMeatMultiplier orig, SlugBase.DataTypes.Diet diet, Player player, Creature crit);

        public static readonly List<ExtEnumBase> PossibleCravings = new();
        public static readonly Dictionary<ExtEnumBase, float> CravingsMultipliers = new();
        public static ExtEnumBase Craving;

        public static float Multiplier = DefaultMultiplier;

        public static void Register()
        {
            On.SlugcatStats.NourishmentOfObjectEaten += SlugcatStats_NourishmentOfObjectEaten;
            HookEndpointManager.Add(typeof(SlugBase.DataTypes.Diet).GetMethod("GetMeatMultiplier", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic),
                (hook_GetMeatMultiplier)MeatMultiplierHK);
            On.StoryGameSession.ctor += StoryGameSession_ctor;
            On.GameSession.ctor += GameSession_ctor;

            On.HUD.HUD.ctor += HUD_ctor;
        }

        private static void HUD_ctor(On.HUD.HUD.orig_ctor orig, HUD.HUD self, FContainer[] fContainers, RainWorld rainWorld, IOwnAHUD owner)
        {
            orig(self, fContainers, rainWorld, owner);

            if (owner is Player player && player.abstractCreature.world.game.session is StoryGameSession session && session.characterStats.name == MotherEnums.Mother)
            {
                self.parts.Add(new CravingsUI(self, fContainers[1]));
            }
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
                foreach (string line in File.ReadLines(path))
                {
                    string[] op = line.Split(',');
                    AbstractPhysicalObject.AbstractObjectType type = new(op[0].Trim());
                    if (type.index == -1)
                    {
                        Plugin.LogSource.LogDebug($"Food object craving does not exists (missing mod or dlc?): {op[0].Trim()}");
                        continue;
                    }
                    PossibleCravings.Add(type);
                    if (op.Length > 1 && float.TryParse(op[1].Trim(), out float cMul))
                    {
                        CravingsMultipliers[type] = cMul;
                    }
                    Plugin.LogSource.LogDebug($"Registered object craving: {type.value}");
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
                    if (type.index == -1)
                    {
                        Plugin.LogSource.LogDebug($"Meat craving does not exists (missing mod or dlc?): {op[0].Trim()}");
                        continue;
                    }
                    PossibleCravings.Add(type);
                    if (op.Length > 1 && float.TryParse(op[1].Trim(), out float cMul))
                    {
                        CravingsMultipliers[type] = cMul;
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
                ((eatenobject is Creature crit && crit.Template.type == Craving as CreatureTemplate.Type)
                || (eatenobject as PhysicalObject).abstractPhysicalObject.type == Craving as AbstractPhysicalObject.AbstractObjectType))
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
            if (player.slugcatStats.name == MotherEnums.Mother && crit.Template.type == Craving as CreatureTemplate.Type)
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
                Craving = null;
            }
        }

        private static void StoryGameSession_ctor(On.StoryGameSession.orig_ctor orig, StoryGameSession self, SlugcatStats.Name saveStateNumber, RainWorldGame game)
        {
            orig(self, saveStateNumber, game);
            int cravingcount = PossibleCravings.Count;
            if (self.characterStats.name == MotherEnums.Mother && cravingcount > 0)
            {

                DeathPersistentSaveData dpsd = self.saveState.deathPersistentSaveData;
                SlugBaseSaveData slugbasedata = dpsd.GetSlugBaseData();
                slugbasedata.TryGet(MOTHERPREVCYCLE, out int previousCycle);

                if (slugbasedata.TryGet(LASTMOTHERCRAVINGS, out List<string> lastcravings))
                {
                    int minCravings = Mathf.Min(cravingcount - 1, MaximumLastCravings - 1);
                    while (lastcravings.Count > minCravings)
                    {
                        lastcravings.RemoveAt(lastcravings.Count - 1);
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
                            Craving = tpl;

                            if (!CravingsMultipliers.TryGetValue(tpl, out Multiplier))
                            {
                                Multiplier = DefaultMultiplier;
                            }
                            Plugin.LogSource.LogDebug($"Current food craving: {Craving.value}");
                            return;
                        }
                    }
                    else
                    {
                        Craving = obj;

                        if (!CravingsMultipliers.TryGetValue(obj, out Multiplier))
                        {
                            Multiplier = DefaultMultiplier;
                        }
                        Plugin.LogSource.LogDebug($"Current food craving: {Craving.value}");
                        return;
                    }
                }

                do
                {
                    Craving = PossibleCravings[Random.Range(0, cravingcount)];
                    if (!CravingsMultipliers.TryGetValue(Craving, out Multiplier))
                    {
                        Multiplier = DefaultMultiplier;
                    }
                } while (Craving is not null && lastcravings.Contains(Craving.value));

                lastcravings.Insert(0, Craving.value);

                slugbasedata.Set(LASTMOTHERCRAVINGS, lastcravings);
                slugbasedata.Set(MOTHERPREVCYCLE, dpsd.survives);

                Plugin.LogSource.LogDebug($"Current food craving: {Craving.value}");
            }
        }

        public class CravingsUI : HudPart
        {
            public readonly FContainer HUDfContainer;

            public IconSymbol symbol;
            public Color storedColor;

            private int _internalUpdateTimer = DefaultUpdateTimer;
            public const int DefaultUpdateTimer = 50;
            public bool cravingsIconSet;

            public float fade;
            public float lastFade;
            public float firstFade;

            public Vector2 Pos
            {
                get
                {
                    return hud.foodMeter?.pos + new Vector2(0f,32f) ?? Vector2.zero;
                }
            }
            public bool Show
            {
                get
                {
                    return hud.showKarmaFoodRain || hud.owner.RevealMap;
                }
            }

            public override void Draw(float timeStacker)
            {
                base.Draw(timeStacker);

                if(symbol is not null)
                {
                    symbol.Draw(timeStacker, Pos);
                    symbol.symbolSprite.alpha = fade;

                    if (firstFade < 1f)
                    {
                        symbol.showFlash = Mathf.Lerp(symbol.showFlash, 0.5f, 0.1f);
                    }
                    else
                    {
                        symbol.showFlash = Mathf.Lerp(symbol.showFlash, 0f, 0.1f);
                    }

                    symbol.shadowSprite1.alpha = symbol.symbolSprite.alpha * 0.5f;
                    symbol.shadowSprite2.alpha = symbol.symbolSprite.alpha * 0.5f;
                }
            }

            public CravingsUI(HUD.HUD hud, FContainer container) : base(hud)
            {
                HUDfContainer = container;
            }

            public void RemoveSprites()
            {
                symbol?.RemoveSprites();
            }

            public override void Update()
            {
                base.Update();
                symbol?.Update();

                if (firstFade < 0.995f)
                {
                    firstFade = Mathf.Lerp(firstFade, 1f, 0.05f);
                }
                else
                {
                    firstFade = 1f;
                }

                if (hud.foodMeter is not null)
                {
                    fade = Mathf.Lerp(fade, Show ? hud.foodMeter.fade : 0f, (fade < hud.foodMeter.fade) ? 0.15f : 0.25f);
                }

                lastFade = fade;

                if (cravingsIconSet) return;

                _internalUpdateTimer--;
                if(_internalUpdateTimer <= 0)
                {
                    UpdateSymbol();
                    _internalUpdateTimer = DefaultUpdateTimer;
                    cravingsIconSet = true;
                }
            }

            public void UpdateSymbol()
            {
                if (Craving is null)
                {
                    return;
                }
                if (Craving is CreatureTemplate.Type critType)
                {
                    IconSymbol.IconSymbolData data = new(critType, AbstractPhysicalObject.AbstractObjectType.Creature, 0);
                    symbol?.RemoveSprites();
                    symbol = new CreatureSymbol(data, HUDfContainer);
                    storedColor = CreatureSymbol.ColorOfCreature(data);
                    symbol.myColor = storedColor;
                    symbol.Show(true);
                }
                else if (Craving is AbstractPhysicalObject.AbstractObjectType objType)
                {
                    IconSymbol.IconSymbolData data = new(CreatureTemplate.Type.StandardGroundCreature, objType, 0);
                    symbol?.RemoveSprites();
                    symbol = new ItemSymbol(data, HUDfContainer);
                    storedColor = ItemSymbol.ColorForItem(objType, 0);
                    symbol.myColor = storedColor;
                    symbol.Show(true);
                }
            }
        }
    }
}
