using Eremite.Model.Meta;
using Eremite.View.HUD;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// Colours the world map cycle perk frames by rarity.
    ///
    /// CycleEffectSlot.SetUpFrame tints frame.color, but the perk slot prefab leaves that field null:
    /// the visible border is a child Image ("Frame", sprite FRAME_Square3_32x32) whose colour is set
    /// to the generic cyan reward tint, and CycleEffectModel.GetColor() is never consulted. Verified
    /// in game: overriding GetColor changed nothing and the slot's frame field is null. This postfix
    /// tints the actual frame image for the mod's own perks.
    /// </summary>
    internal static class PerkFramePatch
    {
        private static readonly System.Collections.Generic.Dictionary<int, Image> s_frameImages =
            new System.Collections.Generic.Dictionary<int, Image>();

        public static void Postfix(CycleEffectSlot __instance)
        {
            try
            {
                int rarity = RarityOf(__instance.model);
                if (rarity < 0)
                {
                    return;
                }

                Image frame = __instance.frame;
                if (frame == null)
                {
                    frame = FindFrameImage(__instance);
                }

                if (frame != null)
                {
                    frame.color = RarityPalette.All[rarity];
                }
            }
            catch
            {
                // Cosmetic only.
            }
        }

        /// <summary>
        /// The slot's own frame field is empty in this prefab, so the child image that draws the
        /// border is located by name, falling back to any child using a FRAME_ sprite.
        /// </summary>
        private static Image FindFrameImage(CycleEffectSlot slot)
        {
            int key = slot.GetInstanceID();
            Image cached;
            if (s_frameImages.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            Transform direct = slot.transform.Find("Frame");
            Image image = direct != null ? direct.GetComponent<Image>() : null;

            if (image == null)
            {
                Image[] children = slot.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    Sprite sprite = children[i].sprite;
                    if (sprite != null && sprite.name.StartsWith("FRAME_"))
                    {
                        image = children[i];
                        break;
                    }
                }
            }

            if (image != null)
            {
                s_frameImages[key] = image;
            }

            return image;
        }

        private static int RarityOf(CycleEffectModel model)
        {
            GachaCycleEffectModel buff = model as GachaCycleEffectModel;
            if (buff != null)
            {
                return buff.rarityIndex;
            }

            GachaGoodPerkModel goods = model as GachaGoodPerkModel;
            if (goods != null)
            {
                return goods.rarityIndex;
            }

            return -1;
        }
    }
}
