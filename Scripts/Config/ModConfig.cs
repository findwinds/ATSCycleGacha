using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Forwindz.CycleBlessings
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
        public int legendary = 10;
        public int epic = 5;
        public int rare = 3;
        public int uncommon = 2;
        public int common = 1;
    }

    [Serializable]
    public class BlessingPools
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

        public WheelCounts wheel = new WheelCounts();
        public ChargeValues charge = new ChargeValues();
        public BlessingPools pools = new BlessingPools();

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
                    if (Current == null || Current.schemaVersion < 2)
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
            if (wheel == null) wheel = new WheelCounts();
            if (charge == null) charge = new ChargeValues();
            if (pools == null) pools = new BlessingPools();

            if (pools.legendary == null) pools.legendary = new List<RewardDef>();
            if (pools.epic == null) pools.epic = new List<RewardDef>();
            if (pools.rare == null) pools.rare = new List<RewardDef>();
            if (pools.uncommon == null) pools.uncommon = new List<RewardDef>();
            if (pools.common == null) pools.common = new List<RewardDef>();

            if (wheel.legendary < 0) wheel.legendary = 0;
            if (wheel.epic < 0) wheel.epic = 0;
            if (wheel.rare < 0) wheel.rare = 0;
            if (wheel.uncommon < 0) wheel.uncommon = 0;
            if (wheel.common < 0) wheel.common = 0;

            if (charge.legendary < 0) charge.legendary = 0;
            if (charge.epic < 0) charge.epic = 0;
            if (charge.rare < 0) charge.rare = 0;
            if (charge.uncommon < 0) charge.uncommon = 0;
            if (charge.common < 0) charge.common = 0;

            if (ticketsPerSettlement < 1) ticketsPerSettlement = 1;

            SanitizePool(pools.legendary, "legendary", wheel.legendary);
            SanitizePool(pools.epic, "epic", wheel.epic);
            SanitizePool(pools.rare, "rare", wheel.rare);
            SanitizePool(pools.uncommon, "uncommon", wheel.uncommon);
            SanitizePool(pools.common, "common", wheel.common);

            HashSet<string> ids = new HashSet<string>();
            CheckDuplicateIds(pools.legendary, ids);
            CheckDuplicateIds(pools.epic, ids);
            CheckDuplicateIds(pools.rare, ids);
            CheckDuplicateIds(pools.uncommon, ids);
            CheckDuplicateIds(pools.common, ids);
        }

        private static void CheckDuplicateIds(List<RewardDef> pool, HashSet<string> ids)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!ids.Add(pool[i].id))
                {
                    Plugin.Log.LogWarning($"Duplicate reward id '{pool[i].id}' in cycle_gacha.json. Ids should be unique across all pools.");
                }
            }
        }

        private static void SanitizePool(List<RewardDef> pool, string rarity, int wheelCount)
        {
            int usable = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                RewardDef def = pool[i];
                if (def == null)
                {
                    pool.RemoveAt(i--);
                    continue;
                }

                if (string.IsNullOrEmpty(def.id))
                {
                    def.id = rarity + "_" + i;
                }

                if (def.goods == null)
                {
                    def.goods = new List<GoodAmount>();
                }

                bool hasEffect = !string.IsNullOrEmpty(def.effect);
                bool hasGoods = def.goods.Count > 0;
                bool hasVillagers = def.villagers > 0;
                if (!hasEffect && !hasGoods && !hasVillagers)
                {
                    Plugin.Log.LogWarning($"Reward '{def.id}' has no effect, goods or villagers; it will be skipped.");
                    continue;
                }

                if (def.villagers < 0) def.villagers = 0;
                if (def.weight < 0) def.weight = 0;
                if (def.weight > 0) usable++;
            }

            if (wheelCount > 0 && usable == 0)
            {
                Plugin.Log.LogWarning($"The {rarity} pool has no usable rewards, but the wheel asks for {wheelCount} option(s). Those slots will be empty.");
            }
        }

        public static ModConfig CreateDefault()
        {
            ModConfig config = new ModConfig();
            config.schemaVersion = 2;

            // Legendary: strongest effects, huge supplies or a large settler group.
            config.pools.legendary.Add(Buff("legendary_automatons", "WE_Automaton_In_All_Mines"));
            config.pools.legendary.Add(Buff("legendary_elder_cat", "WE_First_Elder_Bonus"));
            config.pools.legendary.Add(Goods("legendary_supplies", "传奇物资 / Legendary Supplies",
                "每座新定居点开局 +250 木材、+150 根茎、+30 木板。",
                G("Mat_Raw_Wood", 250), G("Food_Raw_Roots", 150), G("Mat_Processed_Planks", 30)));
            config.pools.legendary.Add(Settlers("legendary_settlers", "传奇移民 / Legendary Settlers",
                "每座新定居点开局 +8 名随机种族居民。", 8));

            // Epic: strong economy/production effects, large supplies, +5 settlers.
            config.pools.epic.Add(Buff("epic_petrified_egg", "WE_Petrified_Egg"));
            config.pools.epic.Add(Buff("epic_guild_sigil", "WE_Crashed_Airship_Bonus"));
            config.pools.epic.Add(Buff("epic_farm", "Farm_Plus150"));
            config.pools.epic.Add(Goods("epic_supplies", "大型物资 / Large Supplies",
                "每座新定居点开局 +150 木材、+100 肉、+20 木板。",
                G("Mat_Raw_Wood", 150), G("Food_Raw_Meat", 100), G("Mat_Processed_Planks", 20)));
            config.pools.epic.Add(Settlers("epic_settlers", "大型移民 / Large Settler Group",
                "每座新定居点开局 +5 名随机种族居民。", 5));

            // Rare: strong resolve / production effects, medium supplies, +3 settlers.
            config.pools.rare.Add(Buff("rare_elder_cat_resolve", "WE_First_Elder_Cat_Resolve"));
            config.pools.rare.Add(Buff("rare_festive_mood", "WE_Fallen_Viceroy_Commemoration_Composite"));
            config.pools.rare.Add(Buff("rare_favored_species", "WE_Favor_Random_Race"));
            config.pools.rare.Add(Buff("rare_woodcutters", "Woodcutters_Camp_Plus100"));
            config.pools.rare.Add(Buff("rare_smallfarm", "SmallFarm_Plus150"));
            config.pools.rare.Add(Goods("rare_supplies", "稀有物资 / Rare Supplies",
                "每座新定居点开局 +80 木材、+60 根茎、+10 木板。",
                G("Mat_Raw_Wood", 80), G("Food_Raw_Roots", 60), G("Mat_Processed_Planks", 10)));
            config.pools.rare.Add(Settlers("rare_settlers", "稀有移民 / Rare Settler Group",
                "每座新定居点开局 +3 名随机种族居民。", 3));

            // Uncommon: medium global bonuses, processed goods or +2 settlers.
            config.pools.uncommon.Add(Buff("uncommon_farm", "Farm_Plus100"));
            config.pools.uncommon.Add(Buff("uncommon_metal", "Metalurgic_Proficiency_33"));
            config.pools.uncommon.Add(Buff("uncommon_herb", "Herb_Garden_Plus50_Composite"));
            config.pools.uncommon.Add(Buff("uncommon_brewery", "Brewery_Plus50_Composite"));
            config.pools.uncommon.Add(Buff("uncommon_smallfarm", "SmallFarm_Plus100"));
            config.pools.uncommon.Add(Goods("uncommon_supplies", "常用物资 / Supplies",
                "每座新定居点开局 +40 木材、+30 根茎、+5 木板。",
                G("Mat_Raw_Wood", 40), G("Food_Raw_Roots", 30), G("Mat_Processed_Planks", 5)));
            config.pools.uncommon.Add(Goods("uncommon_processed", "加工物资 / Processed Supplies",
                "每座新定居点开局 +8 木板、+6 砖块、+5 织物。",
                G("Mat_Processed_Planks", 8), G("Mat_Processed_Bricks", 6), G("Mat_Processed_Fabric", 5)));
            config.pools.uncommon.Add(Settlers("uncommon_settlers", "新居民 / Settlers",
                "每座新定居点开局 +2 名随机种族居民。", 2));

            // Common: small amounts of resources, +1 settler or a small global bonus.
            config.pools.common.Add(Goods("common_wood", "木材补给 / Wood Cache", "每座新定居点开局 +20 木材。", G("Mat_Raw_Wood", 20)));
            config.pools.common.Add(Goods("common_stone", "石材补给 / Stone Cache", "每座新定居点开局 +15 石材。", G("Mat_Raw_Stone", 15)));
            config.pools.common.Add(Goods("common_clay", "黏土补给 / Clay Cache", "每座新定居点开局 +15 黏土。", G("Mat_Raw_Clay", 15)));
            config.pools.common.Add(Goods("common_roots", "根茎补给 / Roots Cache", "每座新定居点开局 +15 根茎。", G("Food_Raw_Roots", 15)));
            config.pools.common.Add(Goods("common_meat", "肉食补给 / Meat Cache", "每座新定居点开局 +10 肉。", G("Food_Raw_Meat", 10)));
            config.pools.common.Add(Goods("common_vegetables", "蔬菜补给 / Vegetable Cache", "每座新定居点开局 +10 蔬菜。", G("Food_Raw_Vegetables", 10)));
            config.pools.common.Add(Goods("common_planks", "木板补给 / Plank Cache", "每座新定居点开局 +5 木板。", G("Mat_Processed_Planks", 5)));
            config.pools.common.Add(Goods("common_bricks", "砖块补给 / Brick Cache", "每座新定居点开局 +4 砖块。", G("Mat_Processed_Bricks", 4)));
            config.pools.common.Add(Goods("common_fabric", "织物补给 / Fabric Cache", "每座新定居点开局 +3 织物。", G("Mat_Processed_Fabric", 3)));
            config.pools.common.Add(Settlers("common_settlers", "新居民 / New Settler", "每座新定居点开局 +1 名随机种族居民。", 1));
            config.pools.common.Add(Buff("common_fishing", "Fishing_Huts_Speed"));
            config.pools.common.Add(Buff("common_hauler", "Hauler_Speed"));
            config.pools.common.Add(Buff("common_farm", "Farm_Plus50"));
            config.pools.common.Add(Buff("common_woodcutters", "Woodcutters_Camp_Plus50"));
            config.pools.common.Add(Buff("common_foragers", "Foragers_Camp_Plus50_Composite"));
            config.pools.common.Add(Buff("common_smallfarm", "SmallFarm_Plus50"));
            config.pools.common.Add(Buff("common_houses", "Houses_Plus1_For_Break_Time"));
            config.pools.common.Add(Buff("common_resolve", "Hub_Resolve_Composite"));

            return config;
        }

        private static GoodAmount G(string good, int amount)
        {
            return new GoodAmount { good = good, amount = amount };
        }

        private static RewardDef Buff(string id, string effect)
        {
            RewardDef def = new RewardDef();
            def.id = id;
            def.effect = effect;
            return def;
        }

        private static RewardDef Goods(string id, string name, string description, params GoodAmount[] goods)
        {
            RewardDef def = new RewardDef();
            def.id = id;
            def.name = name;
            def.description = description;
            def.goods.AddRange(goods);
            return def;
        }

        private static RewardDef Settlers(string id, string name, string description, int count)
        {
            RewardDef def = new RewardDef();
            def.id = id;
            def.name = name;
            def.description = description;
            def.villagers = count;
            return def;
        }
    }
}
