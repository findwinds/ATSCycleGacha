using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Forwindz.CycleGacha
{
    [Serializable]
    public class GoodAmount
    {
        /// <summary>
        /// Either a GoodsTypes enum member (Mat_Raw_Wood) or the internal good name ([Mat Raw] Wood).
        /// </summary>
        public string good;
        public int amount;
    }

    [Serializable]
    public class RewardDef
    {
        public string id;

        /// <summary>
        /// Optional EffectModel buff. Use an EffectTypes enum member (WE_First_Elder_Bonus)
        /// or the effect's internal name ("[WE] First Elder Bonus").
        /// </summary>
        public string effect;

        /// <summary>Resources granted at the start of every new settlement in the cycle.</summary>
        public List<GoodAmount> goods = new List<GoodAmount>();

        /// <summary>Villagers granted at the start of every new settlement in the cycle.</summary>
        public int villagers;
        public string race;

        public string name;
        public string description;
        public int weight = 1;
    }

    [Serializable]
    public class WheelCounts
    {
        public int legendary = 1;
        public int epic = 2;
        public int rare = 4;
        public int uncommon = 8;
        public int common = 16;
    }

    [Serializable]
    public class ChargeValues
    {
        public int legendary = 5;
        public int epic = 4;
        public int rare = 3;
        public int uncommon = 2;
        public int common = 1;
    }

    [Serializable]
    public class RewardPools
    {
        public List<RewardDef> legendary = new List<RewardDef>();
        public List<RewardDef> epic = new List<RewardDef>();
        public List<RewardDef> rare = new List<RewardDef>();
        public List<RewardDef> uncommon = new List<RewardDef>();
        public List<RewardDef> common = new List<RewardDef>();
    }

    [Serializable]
    public class ModConfig
    {
        public static readonly string[] Rarities = { "Legendary", "Epic", "Rare", "Uncommon", "Common" };

        /// <summary>Config schema version. 0/1 are legacy configs that are replaced on load.</summary>
        public int schemaVersion;

        /// <summary>Hotkey that opens the gacha window while the WorldMap scene is active.</summary>
        public string gachaHotkey = "F7";

        /// <summary>Grant tickets when a settlement is lost as well as when it is won.</summary>
        public bool grantTicketOnLoss = false;

        /// <summary>Tickets granted for one completed settlement.</summary>
        public int ticketsPerSettlement = 10;

        /// <summary>Tickets a fresh cycle starts with, before any settlement is won.</summary>
        public int startingTickets = 10;

        private static readonly WheelCounts s_wheelCounts = new WheelCounts();
        private static readonly ChargeValues s_chargeValues = new ChargeValues();
        private static readonly RewardPools s_pools;

        static ModConfig()
        {
            s_pools = BuildPools();
        }

        /// <summary>The board shape is fixed and tuned to the 6x6 UI.</summary>
        [JsonIgnore]
        public WheelCounts wheel { get { return s_wheelCounts; } }

        /// <summary>Charge per rarity is fixed and tuned to the 10-point ticket meter.</summary>
        [JsonIgnore]
        public ChargeValues charge { get { return s_chargeValues; } }

        /// <summary>The reward table lives in code, so the JSON config stays minimal.</summary>
        [JsonIgnore]
        public RewardPools pools { get { return s_pools; } }

        [JsonIgnore]
        public static ModConfig Current { get; private set; }

        public static string ConfigPath
        {
            get { return Path.Combine(Plugin.PluginDirectory, "cycle_gacha.json"); }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    Current = JsonConvert.DeserializeObject<ModConfig>(json);
                    if (Current == null || Current.schemaVersion < 7)
                    {
                        if (Current != null)
                        {
                            try
                            {
                                File.Copy(ConfigPath, ConfigPath + ".old", true);
                            }
                            catch
                            {
                                // A failed backup should not block the upgrade.
                            }
                        }

                        Current = CreateDefault();
                        File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(Current, Formatting.Indented));
                        Plugin.Log.LogInfo("Upgraded cycle_gacha.json to the current schema (old file kept as .old).");
                    }
                }
                else
                {
                    Current = CreateDefault();
                    File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(Current, Formatting.Indented));
                    Plugin.Log.LogInfo("Created default config: " + ConfigPath);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to load cycle_gacha.json, using built-in defaults.");
                Plugin.Log.LogError(e);
                Current = CreateDefault();
            }

            Current.Sanitize();
            Plugin.Log.LogInfo("Using cycle gacha config: " + ConfigPath);
        }

        public List<RewardDef> GetPool(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return pools.legendary;
                case "Epic": return pools.epic;
                case "Rare": return pools.rare;
                case "Uncommon": return pools.uncommon;
                case "Common": return pools.common;
                default: return new List<RewardDef>();
            }
        }

        public int GetWheelCount(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return wheel.legendary;
                case "Epic": return wheel.epic;
                case "Rare": return wheel.rare;
                case "Uncommon": return wheel.uncommon;
                case "Common": return wheel.common;
                default: return 0;
            }
        }

        public int GetCharge(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return charge.legendary;
                case "Epic": return charge.epic;
                case "Rare": return charge.rare;
                case "Uncommon": return charge.uncommon;
                case "Common": return charge.common;
                default: return 0;
            }
        }

        private void Sanitize()
        {
            if (ticketsPerSettlement < 1) ticketsPerSettlement = 1;
            if (string.IsNullOrEmpty(gachaHotkey)) gachaHotkey = "F7";
        }

        public static ModConfig CreateDefault()
        {
            ModConfig config = new ModConfig();
            config.schemaVersion = 7;
            return config;
        }

        /// <summary>
        /// The fixed reward table. Every tier offers the same 21 options: two percentage buffs plus
        /// 19 single goods, with the amounts taken from s_goodAmounts for that tier.
        /// </summary>
        private static RewardPools BuildPools()
        {
            RewardPools pools = new RewardPools();
            AddTier(pools.legendary, "legendary", 4, 15, 5);
            AddTier(pools.epic, "epic", 3, 10, 4);
            AddTier(pools.rare, "rare", 2, 6, 3);
            AddTier(pools.uncommon, "uncommon", 1, 3, 2);
            AddTier(pools.common, "common", 0, 1, 1);
            return pools;
        }

        /// <summary>
        /// Every tier offers the same 21 options: the two percentage buffs plus 19 goods, with the
        /// amounts taken from the table below for that tier.
        /// </summary>
        private static void AddTier(List<RewardDef> pool, string prefix, int tier, int speedPercent, int doublePercent)
        {
            pool.Add(Buff(
                prefix + "_speed",
                "cycle:production_speed:" + speedPercent,
                "生产速度 +" + speedPercent + "%",
                "全局生产速度 +" + speedPercent + "%。"));

            pool.Add(Buff(
                prefix + "_double",
                "cycle:double_yield:" + doublePercent,
                "生产翻倍几率 +" + doublePercent + "%",
                "村民进行生产时有 " + doublePercent + "% 几率获得双倍产出。"));

            for (int i = 0; i < s_goodKeys.Length; i++)
            {
                pool.Add(Resource(prefix + "_" + s_goodIds[i], s_goodKeys[i], s_goodAmounts[i][tier]));
            }
        }

        /// <summary>GoodsTypes member names, in the order the table below lists its amounts.</summary>
        private static readonly string[] s_goodKeys =
        {
            "Mat_Processed_Pipe", "Metal_Crystalized_Dew", "Valuable_Amber", "Mat_Processed_Parts",
            "Mat_Processed_Bricks", "Crafting_Flour", "Hearth_Parts", "Crafting_Oil",
            "Food_Raw_Grain", "Packs_Pack_Of_Provisions", "Food_Raw_Mushrooms", "Vessel_Pottery",
            "Needs_Boots", "Food_Raw_Vegetables", "Food_Raw_Insects", "Mat_Processed_Planks",
            "Tools_Simple_Tools", "Mat_Raw_Stone", "Food_Raw_Eggs"
        };

        /// <summary>Id fragments, so the config stays readable.</summary>
        private static readonly string[] s_goodIds =
        {
            "pipe", "dew", "amber", "parts", "bricks", "flour", "wildfire", "oil", "grain",
            "provisions", "mushrooms", "pottery", "boots", "vegetables", "insects", "planks",
            "tools", "stone", "eggs"
        };

        /// <summary>Amounts per tier: common, uncommon, rare, epic, legendary.</summary>
        private static readonly int[][] s_goodAmounts =
        {
            new[] { 5, 10, 20, 35, 55 },
            new[] { 5, 10, 20, 35, 55 },
            new[] { 5, 10, 20, 35, 55 },
            new[] { 1, 3, 6, 10, 15 },
            new[] { 5, 10, 20, 35, 55 },
            new[] { 20, 30, 50, 80, 120 },
            new[] { 1, 3, 6, 10, 15 },
            new[] { 20, 30, 50, 80, 120 },
            new[] { 20, 30, 50, 80, 120 },
            new[] { 10, 12, 15, 19, 24 },
            new[] { 10, 25, 45, 70, 100 },
            new[] { 20, 30, 50, 80, 120 },
            new[] { 10, 20, 35, 55, 80 },
            new[] { 10, 20, 40, 80, 160 },
            new[] { 10, 20, 40, 80, 160 },
            new[] { 5, 10, 20, 35, 55 },
            new[] { 5, 10, 20, 35, 55 },
            new[] { 10, 20, 40, 80, 160 },
            new[] { 20, 30, 50, 80, 120 }
        };

        private static GoodAmount G(string good, int amount)
        {
            return new GoodAmount { good = good, amount = amount };
        }

        private static RewardDef Buff(string id, string effect, string name, string description)
        {
            RewardDef def = new RewardDef();
            def.id = id;
            def.effect = effect;
            def.name = name;
            def.description = description;
            return def;
        }

        private static RewardDef Resource(string id, string good, int amount)
        {
            RewardDef def = new RewardDef();
            def.id = id;
            def.goods.Add(G(good, amount));
            return def;
        }
    }
}