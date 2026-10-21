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
        public int legendary = 5;
        public int epic = 4;
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
                    if (Current == null || Current.schemaVersion < 3)
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
                int kinds = (hasEffect ? 1 : 0) + (hasGoods ? 1 : 0) + (hasVillagers ? 1 : 0);
                if (kinds == 0)
                {
                    Plugin.Log.LogWarning($"Reward '{def.id}' has no effect, goods or villagers; it will be skipped.");
                    continue;
                }

                if (kinds > 1)
                {
                    Plugin.Log.LogWarning($"Reward '{def.id}' combines multiple reward types (effect/goods/villagers); composite rewards are not allowed and will be skipped.");
                    continue;
                }

                if (hasGoods && def.goods.Count > 1)
                {
                    Plugin.Log.LogWarning($"Reward '{def.id}' contains multiple goods; single-good rewards only, skipping it.");
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
            config.schemaVersion = 3;

            AddTier(config.pools.legendary, "legendary", 50, 20, 5, "Global_Production_Faster_25", 25);
            AddTier(config.pools.epic, "epic", 40, 15, 4, "Global_Production_Faster_20", 20);
            AddTier(config.pools.rare, "rare", 30, 10, 3, "Global_Production_Faster_15", 15);
            AddTier(config.pools.uncommon, "uncommon", 20, 5, 2, "Global_Production_Faster_10", 10);

            // Common: exactly 16 single-type rewards.
            config.pools.common.Add(Percent("common_percent", "Global_Production_Faster_5", 5));
            config.pools.common.Add(Resource("common_food_roots", "Food_Raw_Roots", 10));
            config.pools.common.Add(Resource("common_food_meat", "Food_Raw_Meat", 10));
            config.pools.common.Add(Resource("common_food_vegetables", "Food_Raw_Vegetables", 10));
            config.pools.common.Add(Resource("common_mat_wood", "Mat_Raw_Wood", 1));
            config.pools.common.Add(Resource("common_mat_stone", "Mat_Raw_Stone", 1));
            config.pools.common.Add(Resource("common_mat_clay", "Mat_Raw_Clay", 1));
            config.pools.common.Add(Settlers("common_settlers", 1));
            config.pools.common.Add(Resource("common_processed_planks", "Mat_Processed_Planks", 1));
            config.pools.common.Add(Resource("common_processed_bricks", "Mat_Processed_Bricks", 1));
            config.pools.common.Add(Resource("common_processed_fabric", "Mat_Processed_Fabric", 1));
            config.pools.common.Add(Resource("common_service_tea", "Needs_Tea", 1));
            config.pools.common.Add(Resource("common_service_wine", "Needs_Wine", 1));
            config.pools.common.Add(Resource("common_service_incense", "Needs_Incense", 1));
            config.pools.common.Add(Resource("common_metal_copper", "Metal_Copper_Bar", 1));
            config.pools.common.Add(Resource("common_tools", "Tools_Simple_Tools", 1));

            return config;
        }

        private static void AddTier(List<RewardDef> pool, string prefix, int food, int material, int settlers, string percentEffect, int percent)
        {
            pool.Add(Percent(prefix + "_percent", percentEffect, percent));
            pool.Add(Resource(prefix + "_food_roots", "Food_Raw_Roots", food));
            pool.Add(Resource(prefix + "_food_meat", "Food_Raw_Meat", food));
            pool.Add(Resource(prefix + "_food_vegetables", "Food_Raw_Vegetables", food));
            pool.Add(Resource(prefix + "_mat_wood", "Mat_Raw_Wood", material));
            pool.Add(Resource(prefix + "_mat_stone", "Mat_Raw_Stone", material));
            pool.Add(Resource(prefix + "_mat_clay", "Mat_Raw_Clay", material));
            pool.Add(Settlers(prefix + "_settlers", settlers));
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

        private static RewardDef Percent(string id, string effect, int percent)
        {
            RewardDef def = new RewardDef();
            def.id = id;
            def.effect = effect;
            def.name = $"全局生产速度 +{percent}% / Production +{percent}%";
            def.description = $"全局生产速度 +{percent}%。";
            return def;
        }

        private static RewardDef Resource(string id, string good, int amount)
        {
            RewardDef def = new RewardDef();
            def.id = id;
            def.goods.Add(G(good, amount));
            return def;
        }

        private static RewardDef Settlers(string id, int count)
        {
            RewardDef def = new RewardDef();
            def.id = id;
            def.villagers = count;
            return def;
        }
    }
}
