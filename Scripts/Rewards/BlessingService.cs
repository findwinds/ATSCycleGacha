using System;
using System.Collections.Generic;
using System.Linq;
using ATS_API.Helpers;
using Eremite;
using Eremite.Model;
using Eremite.Services;
using UnityEngine;

namespace Forwindz.CycleBlessings
{
    /// <summary>
    /// Turns reputation milestones into real EffectModel buffs. The effects are the same
    /// world-event reward effects the game stores in WorldStateService.Cycle.activeEventsEffects
    /// and applies in EffectsService.AddLateInitialEffects, so they behave like world-event buffs
    /// for the rest of the cycle.
    /// </summary>
    public static class BlessingService
    {
        private static readonly System.Random s_random = new System.Random(Environment.TickCount);
        private static readonly HashSet<string> s_loggedUnknownSources = new HashSet<string>();
        private static readonly HashSet<string> s_loggedMissingEffects = new HashSet<string>();

        public static void OnReputationGained(float amount, ReputationChangeSource type, string reason)
        {
            string sourceName = type.ToString();
            BlessingDimension? dimension = ModConfig.Current.Classify(sourceName, reason);
            if (!dimension.HasValue)
            {
                LogUnknown(sourceName, reason);
                return;
            }

            CycleState state = CycleStateStore.State;
            float total = state.AddReputation(dimension.Value, amount);
            CycleStateStore.Persist();

            float threshold = ModConfig.Current.threshold;
            List<BlessingDef> earned = new List<BlessingDef>();
            int guard = 0;

            // Re-read state on every iteration: an applied buff may itself trigger a
            // reputation gain, and the nested call can consume a milestone first.
            while (state.GetGrantCount(dimension.Value) <
                   Mathf.FloorToInt((state.GetReputation(dimension.Value) + 0.0001f) / threshold))
            {
                if (++guard > 100)
                {
                    Plugin.Log.LogWarning("Stopped granting buffs after 100 milestones in one reputation gain; check for a buff that grants reputation recursively.");
                    break;
                }

                BlessingDef def = Roll(dimension.Value);
                if (def == null)
                {
                    Plugin.Log.LogWarning($"The {dimension.Value} buff pool has no usable entries; the milestone will be retried on the next reputation gain.");
                    break;
                }

                state.IncrementGrant(dimension.Value);
                state.effectNames.Add(def.effect);
                earned.Add(def);

                if (ModConfig.Current.grantImmediatelyOnEarn)
                {
                    TryApplyNow(def);
                }
            }

            if (earned.Count == 0)
            {
                return;
            }

            CycleStateStore.PersistImmediately();
            PopupNotifier.AnnounceMany(dimension.Value, earned, total);

            string names = string.Join(", ", earned.Select(GetDisplayName).ToArray());
            Plugin.Log.LogInfo($"Earned {earned.Count} {dimension.Value} buff(s) at {total:0.##} reputation: {names}");
        }

        /// <summary>
        /// Called by a Harmony postfix on EffectsService.AddLateInitialEffects. This is the
        /// same point where the game applies world-event and embark effects, so the settlement
        /// state and Settings are ready.
        /// </summary>
        public static void ApplyCycleBuffsAtSettlementStart()
        {
            if (!ModConfig.Current.applyBlessingsAtSettlementStart)
            {
                return;
            }

            if (CycleStateStore.IsSettlementApplied())
            {
                return;
            }

            CycleState state = CycleStateStore.State;
            List<string> effectNames = new List<string>(state.effectNames);
            int applied = 0;

            for (int i = 0; i < effectNames.Count; i++)
            {
                if (ApplyEffect(effectNames[i], false))
                {
                    applied++;
                }
            }

            CycleStateStore.MarkSettlementApplied();
            Plugin.Log.LogInfo($"Applied {applied}/{effectNames.Count} cycle buff(s) to the new settlement.");
        }

        public static BlessingDef FindDef(string effectName)
        {
            BlessingDef result;
            if (TryFindDef(ModConfig.Current.pools.orders, effectName, out result)) return result;
            if (TryFindDef(ModConfig.Current.pools.resolve, effectName, out result)) return result;
            if (TryFindDef(ModConfig.Current.pools.glade, effectName, out result)) return result;
            return null;
        }

        public static string GetDisplayName(BlessingDef def)
        {
            if (def == null)
            {
                return "?";
            }

            if (!string.IsNullOrEmpty(def.name))
            {
                return def.name;
            }

            EffectModel effect = ResolveEffect(def.effect);
            if (effect != null && effect.DisplayName != null)
            {
                string text = effect.DisplayName.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return def.effect;
        }

        public static string GetDescription(BlessingDef def)
        {
            if (def == null)
            {
                return "";
            }

            if (!string.IsNullOrEmpty(def.description))
            {
                return def.description;
            }

            EffectModel effect = ResolveEffect(def.effect);
            if (effect != null && effect.Description != null)
            {
                string text = effect.Description.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return "";
        }

        public static string GetDisplayName(string effectName)
        {
            BlessingDef def = FindDef(effectName);
            if (def != null)
            {
                return GetDisplayName(def);
            }

            EffectModel effect = ResolveEffect(effectName);
            if (effect != null && effect.DisplayName != null)
            {
                string text = effect.DisplayName.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return effectName;
        }

        public static string GetDimensionDisplayName(BlessingDimension dimension)
        {
            switch (dimension)
            {
                case BlessingDimension.Orders: return "指令 / Orders";
                case BlessingDimension.Resolve: return "满意度 / Resolve";
                case BlessingDimension.Glade: return "森林事件 / Glade Events";
                default: return dimension.ToString();
            }
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
                if (SO.Settings.ContainsEffect(internalName))
                {
                    return SO.Settings.GetEffect(internalName);
                }

                LogMissingEffect(key + " (" + internalName + ")");
                return null;
            }

            if (SO.Settings.ContainsEffect(key))
            {
                return SO.Settings.GetEffect(key);
            }

            LogMissingEffect(key);
            return null;
        }

        private static bool TryApplyNow(BlessingDef def)
        {
            bool applied = ApplyEffect(def.effect, true);
            if (!applied)
            {
                Plugin.Log.LogWarning($"Could not apply buff '{def.effect}' immediately; it will still be applied to future settlements.");
            }

            return applied;
        }

        private static bool ApplyEffect(string effectName, bool requireActiveGame)
        {
            if (requireActiveGame)
            {
                try
                {
                    if (!GameMB.IsGameActive)
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }

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

        private static BlessingDef Roll(BlessingDimension dimension)
        {
            List<BlessingDef> pool = ModConfig.Current.GetPool(dimension);
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (IsSelectable(pool[i]))
                {
                    totalWeight += pool[i].weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = s_random.Next(totalWeight);
            for (int i = 0; i < pool.Count; i++)
            {
                BlessingDef def = pool[i];
                if (!IsSelectable(def))
                {
                    continue;
                }

                roll -= def.weight;
                if (roll < 0)
                {
                    return def;
                }
            }

            return null;
        }

        private static bool IsSelectable(BlessingDef def)
        {
            return def != null && !string.IsNullOrEmpty(def.effect) && def.weight > 0;
        }

        private static bool TryFindDef(List<BlessingDef> pool, string effectName, out BlessingDef result)
        {
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].effect == effectName)
                    {
                        result = pool[i];
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        private static void LogUnknown(string source, string reason)
        {
            if (!ModConfig.Current.logUnknownReputationSources)
            {
                return;
            }

            string key = source + "|" + reason;
            if (!s_loggedUnknownSources.Add(key))
            {
                return;
            }

            Plugin.Log.LogWarning(
                $"Unclassified reputation source '{source}' (reason='{reason}'). " +
                "Add a matching token under reputationSources in cycle_blessings.json if this source should count.");
        }

        private static void LogMissingEffect(string key)
        {
            if (!s_loggedMissingEffects.Add(key))
            {
                return;
            }

            Plugin.Log.LogWarning(
                $"Could not find effect '{key}'. Check the EffectTypes enum name or the effect's internal name in cycle_blessings.json.");
        }
    }
}
