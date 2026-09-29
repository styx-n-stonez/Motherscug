using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MotherMod
{
    internal static class MotherSprites
    {
        internal const string ELEMENT_PREFIX = "Mother_";

        private static readonly string[] AtlasPaths =
        {
            "atlases/mother_body",
            "atlases/mother_hips",
            "atlases/mother_head",
            "atlases/mother_arm",
            "atlases/mother_tail",
        };

        private const int SPRITE_BODY = 0;
        private const int SPRITE_HIPS = 1;
        private const int SPRITE_TAIL = 2;
        private const int SPRITE_HEAD = 3;
        private const int SPRITE_ARM_A = 5;
        private const int SPRITE_ARM_B = 6;
        private const int SPRITE_HAND_A = 7;
        private const int SPRITE_HAND_B = 8;
        private const int SPRITE_FACE = 9;

        private static readonly int[] PerFrameSlots =
        {
            SPRITE_HEAD, SPRITE_ARM_A, SPRITE_ARM_B, SPRITE_HAND_A, SPRITE_HAND_B,
        };

        private static readonly int[] OwnedSlots =
        {
            SPRITE_BODY, SPRITE_HIPS, SPRITE_TAIL, SPRITE_HEAD,
            SPRITE_ARM_A, SPRITE_ARM_B, SPRITE_HAND_A, SPRITE_HAND_B,
        };

        private static readonly Dictionary<string, FAtlasElement> Elements = new Dictionary<string, FAtlasElement>();

        private static string ForeignPrefix;

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlayerGraphics, Color[]>
            PaletteSnapshot = new System.Runtime.CompilerServices.ConditionalWeakTable<PlayerGraphics, Color[]>();

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlayerGraphics, System.Runtime.CompilerServices.StrongBox<Color>>
            FaceColor = new System.Runtime.CompilerServices.ConditionalWeakTable<PlayerGraphics, System.Runtime.CompilerServices.StrongBox<Color>>();

        private static bool loggedFirstBind;
        private static bool loggedFirstLate;

        private static bool IsSet(Color c) => c.r >= 0f;

        private static void Recolor(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, int slot, Color color)
        {
            if (!IsSet(color)) return;
            if (sLeaser?.sprites == null || slot >= sLeaser.sprites.Length) return;

            var sprite = sLeaser.sprites[slot];
            if (sprite == null) return;

            if (self.malnourished > 0f)
            {
                float m = self.player.Malnourished ? self.malnourished : Mathf.Max(0f, self.malnourished - 0.005f);
                color = Color.Lerp(color, Color.gray, 0.4f * m);
            }

            sprite.color = self.HypothermiaColorBlend(color);
        }

        internal static void Register()
        {
            On.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
            On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
            On.PlayerGraphics.ApplyPalette += PlayerGraphics_ApplyPalette;

            On.RoomCamera.SpriteLeaser.Update += SpriteLeaser_Update;
        }

        internal static void LoadAtlases()
        {
            foreach (var path in AtlasPaths)
            {
                string png = AssetManager.ResolveFilePath(path + ".png");
                if (!File.Exists(png))
                {
                    Plugin.LogSource?.LogWarning($"[Mother][Sprites] atlas missing path={path}.png resolved={png}");
                    continue;
                }

                FAtlas atlas;
                try
                {
                    atlas = Futile.atlasManager.LoadAtlas(path);
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource?.LogError($"[Mother][Sprites] atlas load failed path={path} err={ex.Message}");
                    continue;
                }

                if (atlas?.elements == null) continue;
                foreach (var element in atlas.elements)
                {
                    Elements[element.name] = element;
                }
            }
        }

        private static bool IsMother(PlayerGraphics self, out MotherConfig config)
        {
            config = null;
            if (self?.player == null) return false;
            if (!Plugin.Stamina.TryGet(self.player, out config)) return false;

            if (ForeignPrefix == null) ForeignPrefix = self.player.slugcatStats?.name?.value;
            return true;
        }

        private static bool ShouldBind(PlayerGraphics self, out MotherConfig config)
        {
            if (!IsMother(self, out config)) return false;
            if (Elements.Count == 0) return false;
            return config.SpritesEnabled;
        }

        private static bool BindSlot(RoomCamera.SpriteLeaser sLeaser, int index)
        {
            if (sLeaser?.sprites == null || index >= sLeaser.sprites.Length) return false;

            var sprite = sLeaser.sprites[index];
            var name = sprite?.element?.name;
            if (name == null) return false;
            if (name.StartsWith(ELEMENT_PREFIX)) return false;

            if (!Resolve(name, out var element)) return false;

            sprite.element = element;
            return true;
        }

        private static bool Resolve(string name, out FAtlasElement element)
        {
            if (Elements.TryGetValue(ELEMENT_PREFIX + name, out element)) return true;

            if (ForeignPrefix != null && name.StartsWith(ForeignPrefix) && name.Length > ForeignPrefix.Length)
            {
                return Elements.TryGetValue(ELEMENT_PREFIX + name.Substring(ForeignPrefix.Length), out element);
            }

            element = null;
            return false;
        }

        private static void PlayerGraphics_InitiateSprites(On.PlayerGraphics.orig_InitiateSprites orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);

            if (!ShouldBind(self, out _)) return;

            bool body = BindSlot(sLeaser, SPRITE_BODY);
            bool hips = BindSlot(sLeaser, SPRITE_HIPS);
            bool tail = BindTail(sLeaser);
        }

        private static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
            float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            if (!IsMother(self, out var config)) return;

            string observed = SPRITE_HEAD < sLeaser.sprites.Length
                ? sLeaser.sprites[SPRITE_HEAD]?.element?.name ?? "null"
                : "no-slot";

            int bound = 0;
            bool active = Elements.Count > 0 && config.SpritesEnabled;
            if (active)
            {
                for (int i = 0; i < PerFrameSlots.Length; i++)
                {
                    if (BindSlot(sLeaser, PerFrameSlots[i])) bound++;
                }
            }

            if (sLeaser?.sprites != null && SPRITE_FACE < sLeaser.sprites.Length
                && sLeaser.sprites[SPRITE_FACE] != null)
            {
                var box = FaceColor.GetValue(self, _ => new System.Runtime.CompilerServices.StrongBox<Color>());
                box.Value = sLeaser.sprites[SPRITE_FACE].color;
            }

            if (!loggedFirstBind)
            {
                loggedFirstBind = true;
            }
        }

        private static bool BindTail(RoomCamera.SpriteLeaser sLeaser)
        {
            if (sLeaser?.sprites == null || SPRITE_TAIL >= sLeaser.sprites.Length) return false;
            if (!(sLeaser.sprites[SPRITE_TAIL] is TriangleMesh mesh)) return false;
            if (mesh.UVvertices == null) return false;
            if (!Elements.TryGetValue(ELEMENT_PREFIX + "TailTexture", out var element)) return false;

            mesh.element = element;

            int count = mesh.UVvertices.Length;

            int segments = (count + 1) / 4;
            if (segments < 1) return false;

            for (int i = 0; i < segments; i++)
            {
                float uBase = i / (float)segments;
                float uTip = (i + 1) / (float)segments;

                SetUV(mesh, i * 4, uBase, 0f, element);
                SetUV(mesh, i * 4 + 1, uBase, 1f, element);

                if (i * 4 + 3 < count)
                {
                    SetUV(mesh, i * 4 + 2, uTip, 0f, element);
                    SetUV(mesh, i * 4 + 3, uTip, 1f, element);
                }
                else
                {
                    SetUV(mesh, i * 4 + 2, 1f, 0.5f, element);
                }
            }

            return true;
        }

        private static void SetUV(TriangleMesh mesh, int vertex, float u, float v, FAtlasElement element)
        {
            if (vertex < 0 || vertex >= mesh.UVvertices.Length) return;

            mesh.UVvertices[vertex] = new Vector2(
                Mathf.Lerp(element.uvBottomLeft.x, element.uvTopRight.x, u),
                Mathf.Lerp(element.uvBottomLeft.y, element.uvTopRight.y, v));
        }

        private static void PlayerGraphics_ApplyPalette(On.PlayerGraphics.orig_ApplyPalette orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            orig(self, sLeaser, rCam, palette);

            if (IsMother(self, out _) && sLeaser?.sprites != null)
            {
                var snap = new Color[sLeaser.sprites.Length];
                for (int i = 0; i < sLeaser.sprites.Length; i++)
                {
                    snap[i] = sLeaser.sprites[i]?.color ?? Color.white;
                }
                PaletteSnapshot.Remove(self);
                PaletteSnapshot.Add(self, snap);
            }

            if (!ShouldBind(self, out var config)) return;

            if (config.SpritesRecolor)
            {
                Recolor(self, sLeaser, SPRITE_BODY, config.SpritesColorBody);
                Recolor(self, sLeaser, SPRITE_HIPS, config.SpritesColorHips);
                Recolor(self, sLeaser, SPRITE_TAIL, config.SpritesColorTail);
                Recolor(self, sLeaser, SPRITE_HEAD, config.SpritesColorHead);
                Recolor(self, sLeaser, SPRITE_ARM_A, config.SpritesColorArms);
                Recolor(self, sLeaser, SPRITE_ARM_B, config.SpritesColorArms);
                Recolor(self, sLeaser, SPRITE_HAND_A, config.SpritesColorHands);
                Recolor(self, sLeaser, SPRITE_HAND_B, config.SpritesColorHands);
                Recolor(self, sLeaser, SPRITE_FACE, config.SpritesColorFace);
                return;
            }

            if (config.SpritesKeepVanillaTint) return;

            for (int i = 0; i < OwnedSlots.Length; i++)
            {
                int slot = OwnedSlots[i];
                if (slot < sLeaser.sprites.Length && sLeaser.sprites[slot] != null)
                {
                    sLeaser.sprites[slot].color = Color.white;
                }
            }
        }

        private static void SpriteLeaser_Update(On.RoomCamera.SpriteLeaser.orig_Update orig,
            RoomCamera.SpriteLeaser self, float timeStacker, RoomCamera rCam, Vector2 camPos)
        {
            orig(self, timeStacker, rCam, camPos);

            if (!(self?.drawableObject is PlayerGraphics graphics)) return;
            if (!ShouldBind(graphics, out _)) return;
            if (self.sprites == null) return;

            int reclaimed = 0;
            if (BindSlot(self, SPRITE_BODY)) reclaimed++;
            if (BindSlot(self, SPRITE_HIPS)) reclaimed++;
            for (int i = 0; i < PerFrameSlots.Length; i++)
            {
                if (BindSlot(self, PerFrameSlots[i])) reclaimed++;
            }

            bool tail = false;
            if (SPRITE_TAIL < self.sprites.Length && self.sprites[SPRITE_TAIL] is TriangleMesh mesh
                && mesh.element != null && !mesh.element.name.StartsWith(ELEMENT_PREFIX))
            {
                tail = BindTail(self);
                if (tail) reclaimed++;
            }

            int repainted = 0;
            if (PaletteSnapshot.TryGetValue(graphics, out var snap))
            {
                int n = System.Math.Min(snap.Length, self.sprites.Length);
                for (int i = 0; i < n; i++)
                {
                    if (i == SPRITE_FACE) continue;

                    var sprite = self.sprites[i];
                    if (sprite == null || sprite.color == snap[i]) continue;
                    sprite.color = snap[i];
                    repainted++;
                }
            }

            if (FaceColor.TryGetValue(graphics, out var face)
                && SPRITE_FACE < self.sprites.Length && self.sprites[SPRITE_FACE] != null
                && self.sprites[SPRITE_FACE].color != face.Value)
            {
                self.sprites[SPRITE_FACE].color = face.Value;
                repainted++;
            }

            if (!loggedFirstLate && (reclaimed > 0 || repainted > 0))
            {
                loggedFirstLate = true;
            }
        }
    }
}
