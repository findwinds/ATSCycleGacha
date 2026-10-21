using System;
using System.Collections.Generic;
using System.Linq;
using ATS_API.Helpers;
using Eremite;
using Eremite.Model;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// Applies the rewards won from the cycle gacha. A reward can be an EffectModel buff,
    /// a resource bundle and/or villagers; all three are re-applied at every new settlement.
    /// </summary>
    public static class RewardService
    {
        private static readonly System.Random s_random = new System.Random(Environment.TickCount);
        private static readonly HashSet<string> s_loggedMissingEffects = new HashSet<string>();
        private static readonly HashSet<string> s_loggedMissingRewards = new HashSet<string>();

        /// <summary>
        /// Called by a Harmony postfix on EffectsService.AddLateInitialEffects and by the
        /// OnStartGame fallback. The settlement flag makes it idempotent.
        /// </summary>
        public static void ApplyCycleRewardsAtSettlementStart()
        {
            if (CycleStateStore.IsSettlementApplied())
            {
                return;
            }

            CycleState state = CycleStateStore.State;
            List<string> rewardIds = new List<string>(state.rewardIds);
            int applied = 0;

            for (int i = 0; i < rewardIds.Count; i++)
            {
                RewardDef def = FindDef(rewardIds[i]);
                if (def == null)
                {
                    LogMissingReward(rewardIds[i]);
                    continue;
                }

                if (ApplyReward(def))
                {
                    applied++;
                }
            }

            CycleStateStore.MarkSettlementApplied();
            string appliedNames = string.Join(", ", rewardIds.Select(GetDisplayName).ToArray());
            Plugin.Log.LogInfo($"Applied {applied}/{rewardIds.Count} cycle reward(s) to the new settlement: {appliedNames}");
        }

        public static void OnStartGameFallback(bool isNewGame)
        {
            if (!isNewGame)
            {
                return;
            }

            try
            {
                ApplyCycleRewardsAtSettlementStart();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to apply cycle rewards from the OnStartGame fallback");
                Plugin.Log.LogError(e);
            }
        }

        public static RewardDef FindDef(string rewardId)
        {
            RewardDef result;
            if (TryFindDef(ModConfig.Current.pools.legendary, rewardId, out result)) return result;
            if (TryFindDef(ModConfig.Current.pools.epic, rewardId, out result)) return result;
            if (TryFindDef(ModConfig.Current.pools.rare, rewardId, out result)) return result;
            if (TryFindDef(ModConfig.Current.pools.uncommon, rewardId, out result)) return result;
            if (TryFindDef(ModConfig.Current.pools.common, rewardId, out result)) return result;
            return null;
        }

        public static string GetDisplayName(string rewardId)
        {
            RewardDef def = FindDef(rewardId);
            if (def == null)
            {
                return string.IsNullOrEmpty(rewardId) ? "?" : rewardId;
            }

            if (!string.IsNullOrEmpty(def.name))
            {
                return def.name;
            }

            if (!string.IsNullOrEmpty(def.effect))
            {
                EffectModel effect = ResolveEffect(def.effect);
                if (effect != null && effect.DisplayName != null)
                {
                    string text = effect.DisplayName.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        return StripTags(text);
                    }
                }

                return def.effect;
            }

            return BuildRewardSummary(def);
        }

        public static string GetDescription(string rewardId)
        {
            RewardDef def = FindDef(rewardId);
            if (def == null)
            {
                return "";
            }

            if (!string.IsNullOrEmpty(def.description))
            {
                return def.description;
            }

            if (!string.IsNullOrEmpty(def.effect))
            {
                EffectModel effect = ResolveEffect(def.effect);
                if (effect != null && effect.Description != null)
                {
                    string text = effect.Description.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        return StripTags(text);
                    }
                }
            }

            string summary = BuildRewardSummary(def);
            bool isResourceOrPopulation =
                (def.goods != null && def.goods.Count > 0) || def.villagers > 0;
            return isResourceOrPopulation
                ? $"每座新定居点开局 {summary}。"
                : summary;
        }

        public static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(text.Length);
            bool insideTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    insideTag = true;
                }
                else if (c == '>')
                {
                    insideTag = false;
                }
                else if (!insideTag)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        public static EffectModel ResolveEffect(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            EffectTypes type;
            if (Enum.TryParse(key, true, out type) &&
                type != EffectTypes.Unknown &&
                type != EffectTypes.None)
            {
                string internalName = type.ToName();
                EffectModel effect = TryGetEffect(internalName);
                if (effect != null)
                {
                    return effect;
                }

                LogMissingEffect(key + " (" + internalName + ")");
                return null;
            }

            EffectModel rawEffect = TryGetEffect(key);
            if (rawEffect != null)
            {
                return rawEffect;
            }

            LogMissingEffect(key);
            return null;
        }

        private static bool ApplyReward(RewardDef def)
        {
            bool applied = false;

            if (!string.IsNullOrEmpty(def.effect))
            {
                applied |= ApplyEffect(def.effect);
            }

            if (def.goods != null && def.goods.Count > 0)
            {
                applied |= ApplyGoods(def);
            }

            if (def.villagers > 0)
            {
                applied |= ApplyVillagers(def);
            }

            return applied;
        }

        private static bool ApplyEffect(string effectName)
        {
            EffectModel effect = ResolveEffect(effectName);
            if (effect == null)
            {
                return false;
            }

            try
            {
                effect.Apply();
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to apply buff effect '{effectName}'");
                Plugin.Log.LogError(e);
                return false;
            }
        }

        private static bool ApplyGoods(RewardDef def)
        {
            try
            {
                if (Serviceable.StorageService == null ||
                    Serviceable.StorageService.Main == null ||
                    Serviceable.StorageService.Main.Goods == null)
                {
                    Plugin.Log.LogWarning($"Storage is not ready; goods from reward '{def.id}' were not granted this settlement.");
                    return false;
                }

                bool any = false;
                for (int i = 0; i < def.goods.Count; i++)
                {
                    GoodAmount good = def.goods[i];
                    if (good == null || string.IsNullOrEmpty(good.good) || good.amount <= 0)
                    {
                        continue;
                    }

                    string goodName = ResolveGoodName(good.good);
                    if (string.IsNullOrEmpty(goodName))
                    {
                        Plugin.Log.LogWarning($"Unknown good '{good.good}' in reward '{def.id}'.");
                        continue;
                    }

                    Serviceable.StorageService.Main.Goods.Add(goodName, good.amount);
                    any = true;
                }

                return any;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to grant goods for reward '{def.id}'");
                Plugin.Log.LogError(e);
                return false;
            }
        }

        private static bool ApplyVillagers(RewardDef def)
        {
            try
            {
                if (Serviceable.VillagersService == null ||
                    Serviceable.VillagersService.Races == null ||
                    Serviceable.VillagersService.Races.Count == 0)
                {
                    Plugin.Log.LogWarning($"Villagers service is not ready; settlers from reward '{def.id}' were not granted this settlement.");
                    return false;
                }

                List<string> raceNames = new List<string>(Serviceable.VillagersService.Races.Keys);
                for (int i = 0; i < def.villagers; i++)
                {
                    string raceName = string.IsNullOrEmpty(def.race)
                        ? raceNames[s_random.Next(raceNames.Count)]
                        : def.race;

                    RaceModel race = raceName.ToRaceModel();
                    if (race == null)
                    {
                        Plugin.Log.LogWarning($"Unknown race '{raceName}' in reward '{def.id}'.");
                        return false;
                    }

                    SO.VillagersService.SpawnNewVillager(race);
                }

                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to spawn settlers for reward '{def.id}'");
                Plugin.Log.LogError(e);
                return false;
            }
        }

        private static string ResolveGoodName(string key)
        {
            GoodsTypes type;
            if (Enum.TryParse(key, true, out type) &&
                type != GoodsTypes.Unknown &&
                type != GoodsTypes.None)
            {
                return type.ToName();
            }

            GoodModel model = key.ToGoodModel();
            return model != null ? model.name : null;
        }

        private static string GetGoodDisplayName(string key)
        {
            string internalName = ResolveGoodName(key);
            if (string.IsNullOrEmpty(internalName))
            {
                return key;
            }

            GoodModel model = internalName.ToGoodModel();
            if (model != null && model.displayName != null)
            {
                string text = model.displayName.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return StripTags(text);
                }
            }

            return internalName;
        }

        private static string BuildRewardSummary(RewardDef def)
        {
            List<string> parts = new List<string>();

            if (def.goods != null)
            {
                for (int i = 0; i < def.goods.Count; i++)
                {
                    GoodAmount good = def.goods[i];
                    if (good != null && !string.IsNullOrEmpty(good.good) && good.amount > 0)
                    {
                        parts.Add($"+{good.amount} {GetGoodDisplayName(good.good)}");
                    }
                }
            }

            if (def.villagers > 0)
            {
                parts.Add($"+{def.villagers} 居民 / Settlers");
            }

            if (!string.IsNullOrEmpty(def.effect) && parts.Count == 0)
            {
                parts.Add(def.effect);
            }

            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : def.id;
        }

        private static bool TryFindDef(List<RewardDef> pool, string rewardId, out RewardDef result)
        {
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].id == rewardId)
                    {
                        result = pool[i];
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        private static EffectModel TryGetEffect(string internalName)
        {
            try
            {
                return SO.Settings.GetEffect(internalName);
            }
            catch
            {
                return null;
            }
        }

        private static void LogMissingEffect(string key)
        {
            if (!s_loggedMissingEffects.Add(key))
            {
                return;
            }

            Plugin.Log.LogWarning(
                $"Could not find effect '{key}'. Check the EffectTypes enum name or the effect's internal name in cycle_gacha.json.");
        }

        private static void LogMissingReward(string rewardId)
        {
            if (!s_loggedMissingRewards.Add(rewardId))
            {
                return;
            }

            Plugin.Log.LogWarning($"Reward '{rewardId}' is owned by the cycle but missing from cycle_gacha.json; skipping it.");
        }
    }
}
