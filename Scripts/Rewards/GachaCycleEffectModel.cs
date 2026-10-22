using Eremite.Model.Meta;
using UnityEngine;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// Cycle perk created at runtime for a drawn buff.
    ///
    /// The shipped <see cref="EmbarkEffectCycleEffectModel"/> builds its texts from the wrapped
    /// EffectModel: DisplayName is effect.DisplayName and GetDescription formats the effect's
    /// description key. The global production-rate effects carry no usable localisation, so a plain
    /// EmbarkEffectCycleEffectModel renders ">MISSING KEY<" in the world map tooltip. Overriding the
    /// two text members lets the tooltip show the name and description from cycle_gacha.json, while
    /// application logic (AddPerk + AddEmbarkEffect) stays exactly the game's own.
    ///
    /// GetColor is overridden as well: the base palette only knows positive/negative, so every perk
    /// looked alike; the frame now carries the reward's rarity colour.
    /// </summary>
    public class GachaCycleEffectModel : EmbarkEffectCycleEffectModel
    {
        public string perkTitle;
        public string perkDescription;
        public Color frameColor;

        public override string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(perkTitle))
                {
                    return perkTitle;
                }

                return base.DisplayName;
            }
        }

        // Declared public because the reference assembly is publicized (the real member is
        // protected); the signature is what the runtime matches for the override.
        public override string GetDescription()
        {
            if (!string.IsNullOrEmpty(perkDescription))
            {
                return perkDescription;
            }

            return base.GetDescription();
        }

        public override Color GetColor()
        {
            return frameColor;
        }
    }

    /// <summary>
    /// Cycle perk created at runtime for a goods reward, with the same rarity-coloured frame.
    /// </summary>
    public class GachaGoodPerkModel : EmbarkGoodCycleEffectModel
    {
        public Color frameColor;

        public override Color GetColor()
        {
            return frameColor;
        }
    }
}
