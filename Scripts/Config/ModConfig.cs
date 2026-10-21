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
    public class BlessingDef
    {
        public string id;
        public string name;
        public string description;
        public List<GoodAmount> goods = new List<GoodAmount>();
        public int villagers;
        public string race;
        public int weight = 1;
    }

    [Serializable]
    public class SourceTokens
    {
        public List<string> orders = new List<string> { "Order" };
        public List<string> resolve = new List<string> { "Resolve" };
        public List<string> glade = new List<string> { "Relic", "Glade", "Event" };
    }

    [Serializable]
    public class BlessingPools
    {
        public List<BlessingDef> orders = new List<BlessingDef>();
        public List<BlessingDef> resolve = new List<BlessingDef>();
        public List<BlessingDef> glade = new List<BlessingDef>();
    }

    [Serializable]
    public class ModConfig
    {
        public float threshold = 18f;

        /// <summary>
        /// Apply the reward in the settlement where it was earned, in addition to future settlements.
        /// </summary>
        public bool grantImmediatelyOnEarn = true;

        /// <summary>
        /// Re-apply every owned blessing when a new settlement starts.
        /// </summary>
        public bool applyBlessingsAtSettlementStart = true;

        public bool announceBlessings = true;
        public bool logUnknownReputationSources = true;
        public string statusHotkey = "F8";

        public SourceTokens reputationSources = new SourceTokens();
        public BlessingPools pools = new BlessingPools();

        [JsonIgnore]
        public static ModConfig Current { get; private set; }

        public static string ConfigPath
        {
            get { return Path.Combine(Plugin.PluginDirectory, "cycle_blessings.json"); }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    Current = JsonConvert.DeserializeObject<ModConfig>(json);
                    if (Current == null)
                    {
                        Current = CreateDefault();
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
                Plugin.Log.LogError("Failed to load cycle_blessings.json, using built-in defaults.");
                Plugin.Log.LogError(e);
                Current = CreateDefault();
            }

            Current.Sanitize();
            Plugin.Log.LogInfo("Using cycle blessings config: " + ConfigPath);
        }

        public BlessingDimension? Classify(string sourceName, string reason)
        {
            if (MatchesAny(sourceName, reputationSources.orders))
            {
                return BlessingDimension.Orders;
            }

            if (MatchesAny(sourceName, reputationSources.resolve))
            {
                return BlessingDimension.Resolve;
            }

            if (MatchesAny(sourceName, reputationSources.glade))
            {
                return BlessingDimension.Glade;
            }

            // Some effects only carry the useful information in the reason string.
            if (MatchesAny(reason, reputationSources.orders))
            {
                return BlessingDimension.Orders;
            }

            if (MatchesAny(reason, reputationSources.resolve))
            {
                return BlessingDimension.Resolve;
            }

            if (MatchesAny(reason, reputationSources.glade))
            {
                return BlessingDimension.Glade;
            }

            return null;
        }

        public List<BlessingDef> GetPool(BlessingDimension dimension)
        {
            switch (dimension)
            {
                case BlessingDimension.Orders: return pools.orders;
                case BlessingDimension.Resolve: return pools.resolve;
                case BlessingDimension.Glade: return pools.glade;
                default: return new List<BlessingDef>();
            }
        }

        private static bool MatchesAny(string value, List<string> tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void Sanitize()
        {
            if (threshold < 1f)
            {
                threshold = 1f;
            }

            if (!grantImmediatelyOnEarn && !applyBlessingsAtSettlementStart)
            {
                Plugin.Log.LogWarning("Both grantImmediatelyOnEarn and applyBlessingsAtSettlementStart are false; blessings will be recorded but never delivered.");
            }

            if (reputationSources == null)
            {
                reputationSources = new SourceTokens();
            }

            if (pools == null)
            {
                pools = new BlessingPools();
            }

            if (pools.orders == null) pools.orders = new List<BlessingDef>();
            if (pools.resolve == null) pools.resolve = new List<BlessingDef>();
            if (pools.glade == null) pools.glade = new List<BlessingDef>();

            SanitizePool(pools.orders, "order");
            SanitizePool(pools.resolve, "resolve");
            SanitizePool(pools.glade, "glade");

            HashSet<string> ids = new HashSet<string>();
            CheckDuplicateIds(pools.orders, ids);
            CheckDuplicateIds(pools.resolve, ids);
            CheckDuplicateIds(pools.glade, ids);
        }

        private static void CheckDuplicateIds(List<BlessingDef> pool, HashSet<string> ids)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!ids.Add(pool[i].id))
                {
                    Plugin.Log.LogWarning($"Duplicate blessing id '{pool[i].id}' in cycle_blessings.json. Ids should be unique across all three pools.");
                }
            }
        }

        private static void SanitizePool(List<BlessingDef> pool, string prefix)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                BlessingDef def = pool[i];
                if (def == null)
                {
                    pool.RemoveAt(i--);
                    continue;
                }

                if (string.IsNullOrEmpty(def.id))
                {
                    def.id = prefix + "_" + i;
                }

                if (string.IsNullOrEmpty(def.name))
                {
                    def.name = def.id;
                }

                if (def.goods == null)
                {
                    def.goods = new List<GoodAmount>();
                }

                if (def.villagers < 0)
                {
                    def.villagers = 0;
                }

                if (def.weight < 0)
                {
                    def.weight = 0;
                }
            }
        }

        public static ModConfig CreateDefault()
        {
            ModConfig config = new ModConfig();

            config.pools.orders.Add(Goods("order_wood", "建材补给：木材 / Wood Cache", "每座新定居点开局额外获得 40 木材。", "Mat_Raw_Wood", 40));
            config.pools.orders.Add(Goods("order_stone", "建材补给：石材 / Stone Cache", "每座新定居点开局额外获得 25 石材。", "Mat_Raw_Stone", 25));
            config.pools.orders.Add(Goods("order_clay", "建材补给：黏土 / Clay Cache", "每座新定居点开局额外获得 25 黏土。", "Mat_Raw_Clay", 25));
            config.pools.orders.Add(Goods("order_roots", "生食补给：根茎 / Roots Cache", "每座新定居点开局额外获得 30 根茎。", "Food_Raw_Roots", 30));
            config.pools.orders.Add(Goods("order_meat", "生食补给：肉 / Meat Cache", "每座新定居点开局额外获得 20 肉。", "Food_Raw_Meat", 20));
            config.pools.orders.Add(Goods("order_vegetables", "生食补给：蔬菜 / Vegetable Cache", "每座新定居点开局额外获得 20 蔬菜。", "Food_Raw_Vegetables", 20));
            config.pools.orders.Add(Villagers("order_newcomers", "新居民 / New Settlers", "每座新定居点开局额外获得 2 名随机种族居民。", 2));

            config.pools.resolve.Add(Goods("resolve_planks", "制品补给：木板 / Plank Cache", "每座新定居点开局额外获得 15 木板。", "Mat_Processed_Planks", 15));
            config.pools.resolve.Add(Goods("resolve_bricks", "制品补给：砖块 / Brick Cache", "每座新定居点开局额外获得 12 砖块。", "Mat_Processed_Bricks", 12));
            config.pools.resolve.Add(Goods("resolve_fabric", "制品补给：织物 / Fabric Cache", "每座新定居点开局额外获得 10 织物。", "Mat_Processed_Fabric", 10));
            config.pools.resolve.Add(Goods("resolve_tea", "服务物品：茶 / Tea Cache", "每座新定居点开局额外获得 8 茶。", "Needs_Tea", 8));
            config.pools.resolve.Add(Goods("resolve_wine", "服务物品：酒 / Wine Cache", "每座新定居点开局额外获得 6 酒。", "Needs_Wine", 6));
            config.pools.resolve.Add(Goods("resolve_incense", "服务物品：熏香 / Incense Cache", "每座新定居点开局额外获得 5 熏香。", "Needs_Incense", 5));

            config.pools.glade.Add(Goods("glade_copper", "金属补给：铜锭 / Copper Bar Cache", "每座新定居点开局额外获得 8 铜锭。", "Metal_Copper_Bar", 8));
            config.pools.glade.Add(Goods("glade_parts", "零件补给 / Parts Cache", "每座新定居点开局额外获得 6 零件。", "Mat_Processed_Parts", 6));
            config.pools.glade.Add(Goods("glade_tools", "工具补给 / Tool Cache", "每座新定居点开局额外获得 10 简单工具。", "Tools_Simple_Tools", 10));

            return config;
        }

        private static BlessingDef Goods(string id, string name, string description, string good, int amount)
        {
            BlessingDef def = new BlessingDef();
            def.id = id;
            def.name = name;
            def.description = description;
            def.goods.Add(new GoodAmount { good = good, amount = amount });
            return def;
        }

        private static BlessingDef Villagers(string id, string name, string description, int count)
        {
            BlessingDef def = new BlessingDef();
            def.id = id;
            def.name = name;
            def.description = description;
            def.villagers = count;
            return def;
        }
    }
}
