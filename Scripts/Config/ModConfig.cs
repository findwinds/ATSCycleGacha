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
        /// internal name ("[WE] First Elder Bonus"). The effect is applied with EffectModel.Apply().
        /// </summary>
        public string effect;

        /// <summary>
        /// Optional display override for the gacha UI. When empty, the effect's in-game
        /// DisplayName is used.
        /// </summary>
        public string name;

        /// <summary>
        /// Optional description override for the gacha UI. When empty, the effect's in-game
        /// Description is used.
        /// </summary>
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
    }

    [Serializable]
    public class BlessingPools
    {
        public List<BlessingDef> legendary = new List<BlessingDef>();
        public List<BlessingDef> epic = new List<BlessingDef>();
        public List<BlessingDef> rare = new List<BlessingDef>();
        public List<BlessingDef> uncommon = new List<BlessingDef>();
    }

    [Serializable]
    public class ModConfig
    {
        public static readonly string[] Rarities = { "Legendary", "Epic", "Rare", "Uncommon" };

        /// <summary>
        /// Hotkey that opens the gacha window while the WorldMap scene is active.
        /// </summary>
        public string gachaHotkey = "F7";

        /// <summary>
        /// Grant a ticket when a settlement is lost as well as when it is won.
        /// </summary>
        public bool grantTicketOnLoss = false;

        public WheelCounts wheel = new WheelCounts();
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
                Plugin.Log.LogError("Failed to load cycle_gacha.json, using built-in defaults.");
                Plugin.Log.LogError(e);
                Current = CreateDefault();
            }

            Current.Sanitize();
            Plugin.Log.LogInfo("Using cycle blessings config: " + ConfigPath);
        }

        public List<BlessingDef> GetPool(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return pools.legendary;
                case "Epic": return pools.epic;
                case "Rare": return pools.rare;
                case "Uncommon": return pools.uncommon;
                default: return new List<BlessingDef>();
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
                default: return 0;
            }
        }

        private void Sanitize()
        {
            if (wheel == null)
            {
                wheel = new WheelCounts();
            }

            if (pools == null)
            {
                pools = new BlessingPools();
            }

            if (pools.legendary == null) pools.legendary = new List<BlessingDef>();
            if (pools.epic == null) pools.epic = new List<BlessingDef>();
            if (pools.rare == null) pools.rare = new List<BlessingDef>();
            if (pools.uncommon == null) pools.uncommon = new List<BlessingDef>();

            if (wheel.legendary < 0) wheel.legendary = 0;
            if (wheel.epic < 0) wheel.epic = 0;
            if (wheel.rare < 0) wheel.rare = 0;
            if (wheel.uncommon < 0) wheel.uncommon = 0;

            SanitizePool(pools.legendary, "legendary", wheel.legendary);
            SanitizePool(pools.epic, "epic", wheel.epic);
            SanitizePool(pools.rare, "rare", wheel.rare);
            SanitizePool(pools.uncommon, "uncommon", wheel.uncommon);

            HashSet<string> ids = new HashSet<string>();
            CheckDuplicateIds(pools.legendary, ids);
            CheckDuplicateIds(pools.epic, ids);
            CheckDuplicateIds(pools.rare, ids);
            CheckDuplicateIds(pools.uncommon, ids);
        }

        private static void CheckDuplicateIds(List<BlessingDef> pool, HashSet<string> ids)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!ids.Add(pool[i].id))
                {
                    Plugin.Log.LogWarning($"Duplicate buff id '{pool[i].id}' in cycle_gacha.json. Ids should be unique across all pools.");
                }
            }
        }

        private static void SanitizePool(List<BlessingDef> pool, string rarity, int wheelCount)
        {
            int usable = 0;
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
                    def.id = rarity + "_" + i;
                }

                if (string.IsNullOrEmpty(def.effect))
                {
                    Plugin.Log.LogWarning($"Buff '{def.id}' has no effect set; it will be skipped.");
                    continue;
                }

                if (def.weight < 0)
                {
                    def.weight = 0;
                }

                if (def.weight > 0)
                {
                    usable++;
                }
            }

            if (wheelCount > 0 && usable == 0)
            {
                Plugin.Log.LogWarning($"The {rarity} pool has no usable buffs, but the wheel asks for {wheelCount} option(s). Those slots will be empty.");
            }
        }

        public static ModConfig CreateDefault()
        {
            ModConfig config = new ModConfig();

            // Legendary: the strongest persistent world-event rewards.
            config.pools.legendary.Add(Buff("legendary_automatons", "WE_Automaton_In_All_Mines"));
            config.pools.legendary.Add(Buff("legendary_elder_cat", "WE_First_Elder_Bonus"));
            config.pools.legendary.Add(Buff("legendary_guild_sigil", "WE_Crashed_Airship_Bonus"));

            // Epic: strong economy / production / resolve rewards.
            config.pools.epic.Add(Buff("epic_petrified_egg", "WE_Petrified_Egg"));
            config.pools.epic.Add(Buff("epic_field_station_enriched", "WE_Temporary_Engineering_Station_With_Enriched_Fuel_Recipe"));
            config.pools.epic.Add(Buff("epic_elder_cat_resolve", "WE_First_Elder_Cat_Resolve"));
            config.pools.epic.Add(Buff("epic_festive_mood", "WE_Fallen_Viceroy_Commemoration_Composite"));

            // Rare: useful utility and exploration rewards.
            config.pools.rare.Add(Buff("rare_guild_logbook", "WE_Glade_Info"));
            config.pools.rare.Add(Buff("rare_blight_bomb", "WE_Blight_Bomb_Blueprint"));
            config.pools.rare.Add(Buff("rare_field_station_fuel_rod", "WE_Temporary_Engineering_Station_With_Fuel_Rod_Recipe"));
            config.pools.rare.Add(Buff("rare_syndicate_trader", "WE_Call_Syndicate_Trader_Ability"));
            config.pools.rare.Add(Buff("rare_cheap_rerolls", "WE_Lower_Blueprints_Reroll_Cost"));
            config.pools.rare.Add(Buff("rare_favored_species", "WE_Favor_Random_Race"));

            // Uncommon: smaller global bonuses, plus two minor world-event rewards.
            config.pools.uncommon.Add(Buff("uncommon_farm_production", "Farm_Plus100"));
            config.pools.uncommon.Add(Buff("uncommon_fishing_speed", "Fishing_Huts_Speed"));
            config.pools.uncommon.Add(Buff("uncommon_hauler_speed", "Hauler_Speed"));
            config.pools.uncommon.Add(Buff("uncommon_metal_speed", "Metalurgic_Proficiency_33"));
            config.pools.uncommon.Add(Buff("uncommon_house_capacity", "Houses_Plus1_For_Break_Time"));
            config.pools.uncommon.Add(Buff("uncommon_foragers", "Foragers_Camp_Plus50_Composite"));
            config.pools.uncommon.Add(Buff("uncommon_herb_garden", "Herb_Garden_Plus50_Composite"));
            config.pools.uncommon.Add(Buff("uncommon_brewery", "Brewery_Plus50_Composite"));

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
