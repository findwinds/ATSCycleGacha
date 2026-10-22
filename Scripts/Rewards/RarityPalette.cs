using UnityEngine;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// The rarity palette used by the gacha UI and by the world map perk frames, so a reward looks
    /// the same wherever it is shown.
    /// </summary>
    public static class RarityPalette
    {
        public static readonly Color Legendary = new Color(0.84f, 0.55f, 0.18f);
        public static readonly Color Epic = new Color(0.62f, 0.40f, 0.80f);
        public static readonly Color Rare = new Color(0.36f, 0.58f, 0.85f);
        public static readonly Color Uncommon = new Color(0.40f, 0.70f, 0.44f);
        public static readonly Color Common = new Color(0.74f, 0.71f, 0.63f);

        public static readonly Color[] All = { Legendary, Epic, Rare, Uncommon, Common };

        public static int Index(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return 0;
                case "Epic": return 1;
                case "Rare": return 2;
                case "Uncommon": return 3;
                default: return 4;
            }
        }

        public static Color Get(string rarity)
        {
            return All[Index(rarity)];
        }

        public static string Label(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return "传说";
                case "Epic": return "史诗";
                case "Rare": return "稀有";
                case "Uncommon": return "罕见";
                default: return "普通";
            }
        }
    }
}
