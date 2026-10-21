using System;
using System.Collections.Generic;
using System.Linq;
using ATS_API.Helpers;
using Eremite;
using Eremite.Model;

namespace Forwindz.CycleBlessings
{
    /// <summary>
    /// Applies the buffs won from the cycle gacha. The effects are real EffectModels, applied
    /// at the same point the game applies world-event rewards (EffectsService.AddLateInitialEffects).
    /// </summary>
    public static class BlessingService
    {
        private static readonly HashSet<string> s_loggedMissingEffects = new HashSet<string>();

        /// <summary>
        /// Called by a Harmony postfix on EffectsService.AddLateInitialEffects and by the
        /// OnStartGame fallback. The settlement flag makes it idempotent.
        /// </summary>
        public static void ApplyCycleBuffsAtSettlementStart()
        {
            if (CycleStateStore.IsSettlementApplied())
            {
                return;
            }

            CycleState state = CycleStateStore.State;
            List<string> effectNames = new List<string>(state.effectNames);
            int applied = 0;

            for (int i = 0; i < effectNames.Count; i++)
            {
                if (ApplyEffect(effectNames[i]))
                {
                    applied++;
                }
            }

            CycleStateStore.MarkSettlementApplied();
            string appliedNames = string.Join(", ", effectNames.Select(GetDisplayName).ToArray());
            Plugin.Log.LogInfo($"Applied {applied}/{effectNames.Count} cycle buff(s) to the new settlement: {appliedNames}");
        }

        public static void OnStartGameFallback(bool isNewGame)
        {
            if (!isNewGame)
            {
                return;
            }

            try
            {
                ApplyCycleBuffsAtSettlementStart();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to apply cycle buffs from the OnStartGame fallback");
                Plugin.Log.LogError(e);
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

        public static string GetDisplayName(string effectName)
        {
            if (string.IsNullOrEmpty(effectName))
            {
                return "?";
            }

            EffectModel effect = ResolveEffect(effectName);
            if (effect != null && effect.DisplayName != null)
            {
                string text = effect.DisplayName.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return StripTags(text);
                }
            }

            return effectName;
        }

        public static string GetDescription(string effectName)
        {
            EffectModel effect = ResolveEffect(effectName);
            if (effect != null && effect.Description != null)
            {
                string text = effect.Description.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return StripTags(text);
                }
            }

            return "";
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
    }
}
