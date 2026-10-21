using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Forwindz.CycleBlessings
{
    [Serializable]
    public class BlessingDef
    {
        public string id;

        /// <summary>
        /// An EffectTypes enum member (for example WE_First_Elder_Bonus) or the effect's
        /// internal name ("[WE] First Elder Bonus"). The effect is applied with EffectModel.Apply(),
        /// the same way the game applies world-event rewards.
        /// </summary>
        public string effect;

        /// <summary>
        /// Optional display override for the popup. When empty, the effect's in-game
        /// DisplayName is used.
        /// </summary>
        public string name;

        /// <summary>
        /// Optional description override for the popup. When empty, the effect's in-game
        /// Description is used.
        /// </summary>
        public string description;

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
        /// Apply the buff immediately in the settlement where it was earned, in addition to
        /// every future settlement of the cycle.
        /// </summary>
        public bool grantImmediatelyOnEarn = true;

        /// <summary>
        /// Re-apply every owned buff when a new settlement starts.
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
                Plugin.Log.LogWarning("Both grantImmediatelyOnEarn and applyBlessingsAtSettlementStart are false; buffs will be recorded but never applied.");
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
                    Plugin.Log.LogWarning($"Duplicate buff id '{pool[i].id}' in cycle_blessings.json. Ids should be unique across all three pools.");
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

                if (string.IsNullOrEmpty(def.effect))
                {
                    Plugin.Log.LogWarning($"Buff '{def.id}' has no effect set; it will be skipped.");
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

            // The default pools reuse the game's own world-event reward effects. The game
            // applies these through EffectsService.AddLateInitialEffects at settlement start.
            config.pools.orders.Add(Buff("order_petrified_egg", "WE_Petrified_Egg"));
            config.pools.orders.Add(Buff("order_airship_sigil", "WE_Crashed_Airship_Bonus"));
            config.pools.orders.Add(Buff("order_cheap_rerolls", "WE_Lower_Blueprints_Reroll_Cost"));
            config.pools.orders.Add(Buff("order_mining_automata", "WE_Automaton_In_All_Mines"));

            config.pools.resolve.Add(Buff("resolve_elder_cat", "WE_First_Elder_Cat_Resolve"));
            config.pools.resolve.Add(Buff("resolve_elder_cat_bonus", "WE_First_Elder_Bonus"));
            config.pools.resolve.Add(Buff("resolve_festive_mood", "WE_Fallen_Viceroy_Commemoration_Composite"));
            config.pools.resolve.Add(Buff("resolve_favored_species", "WE_Favor_Random_Race"));

            config.pools.glade.Add(Buff("glade_guild_logbook", "WE_Glade_Info"));
            config.pools.glade.Add(Buff("glade_blight_bomb", "WE_Blight_Bomb_Blueprint"));
            config.pools.glade.Add(Buff("glade_field_station", "WE_Temporary_Engineering_Station_With_Fuel_Rod_Recipe"));
            config.pools.glade.Add(Buff("glade_syndicate_trader", "WE_Call_Syndicate_Trader_Ability"));

            return config;
        }

        private static BlessingDef Buff(string id, string effect)
        {
            BlessingDef def = new BlessingDef();
            def.id = id;
            def.effect = effect;
            return def;
        }
    }
}
