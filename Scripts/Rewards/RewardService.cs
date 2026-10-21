using System;
using System.Collections.Generic;
using System.Linq;
using ATS_API.Helpers;
using Eremite;
using Eremite.Model;
using Eremite.Model.Effects;
using Eremite.Model.Meta;
using Eremite.Services;
using Eremite.Services.World;
using UnityEngine;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// Hands the drawn rewards to the game's own cycle-bonus system, the same one a world event
    /// uses when it grants a cycle-wide reward:
    ///
    ///   CycleEffectModel.Apply()
    ///     -> SO.CycleEffectsService.AddEmbarkEffect / ChangeEmbarkGoods / ChangeEmbarkVillagers
    ///     -> WorldStateService.CycleEffects (bonusEmbarkEffects / bonusEmbarkGoods / bonusEmbarkVillagers)
    ///     -> CaravanGenerator copies them into the caravan shown on the world map
    ///     -> RegularGameConditionsCreator.SetUpEmbarkBonuses folds them into embarkEffects / embarkGoods
    ///     -> EffectsService.AddLateInitialEffects applies the buffs, StorageService.StoreInitialGoods
    ///        stores the goods, CaravanGenerator.GetVillagersAmount counts the settlers.
    ///
    /// CycleEffectsService.OnCycleEnded() rebuilds that state, so everything clears with the cycle.
    /// Nothing is applied by this mod itself.
    /// </summary>
    public static class RewardService
    {
        private static readonly HashSet<string> s_loggedMissingEffects = new HashSet<string>();
        private static readonly HashSet<string> s_loggedMissingRewards = new HashSet<string>();
        private static bool s_inventoryLogged;
        private static readonly Dictionary<string, CycleEffectModel> s_perkModels =
            new Dictionary<string, CycleEffectModel>();

        /// <summary>
        /// Registers a freshly drawn reward with the game's cycle bonus state.
        /// </summary>
        public static void OnRewardDrawn(string rewardId)
        {
            RewardDef def = FindDef(rewardId);
            if (def == null)
            {
                LogMissingReward(rewardId);
                return;
            }

            if (!RegisterReward(def))
            {
                // Not registered, so it stays unrecorded and EnsureRegistered retries later.
                return;
            }

            CycleStateStore.State.registeredRewardIds.Add(rewardId);
            CycleStateStore.PersistImmediately();
        }

        /// <summary>
        /// Re-creates the runtime cycle perk models for every buff reward this cycle owns, both the
        /// already registered ones and the ones about to be registered. Cheap and idempotent: the
        /// models are cached per effect name and the settings lookup is only touched when a model
        /// is missing.
        /// </summary>
        private static void EnsurePerkModels(CycleState state)
        {
            if (!ModConfig.Current.showWorldMapPerkIcons)
            {
                return;
            }

            EnsurePerkModelFor(state.registeredRewardIds);
            EnsurePerkModelFor(state.rewardIds);
        }

        private static void EnsurePerkModelFor(List<string> rewardIds)
        {
            if (rewardIds == null)
            {
                return;
            }

            for (int i = 0; i < rewardIds.Count; i++)
            {
                RewardDef def = FindDef(rewardIds[i]);
                if (def == null || string.IsNullOrEmpty(def.effect))
                {
                    continue;
                }

                string effectName = ResolveEffectName(def.effect);
                if (string.IsNullOrEmpty(effectName))
                {
                    continue;
                }

                EffectModel resolved = TryGetEffect(effectName);

                ApplyEffectText(resolved, def);

                GetOrCreatePerkModel(effectName, resolved, def);
            }
        }

        /// <summary>
        /// Rewards drawn by build 0.5.0/0.6.0 were applied by hand instead of being registered with
        /// the game, so a save carried over from them would never see them in a settlement. This
        /// registers whatever is still missing, once, and is a no-op afterwards.
        /// </summary>
        public static void EnsureRegistered()
        {
            CycleState state = CycleStateStore.State;
            if (state.rewardIds == null || state.rewardIds.Count == 0)
            {
                return;
            }

            if (!CycleEffectsReady())
            {
                Plugin.Log.LogDebug("Cycle bonus state is not available yet; will register the drawn rewards later.");
                return;
            }

            if (state.registeredRewardIds == null)
            {
                state.registeredRewardIds = new List<string>();
            }

            int registered = 0;
            LogCycleEffectInventory();

            // The world map perk strip stores perk *names* and resolves them through
            // Settings.GetCycleEffect() every session, but the perk models for buff rewards are
            // runtime objects. Re-create them before the strip is built, otherwise a save whose
            // rewards are already registered would throw on a model that does not exist yet.
            EnsurePerkModels(state);

            // Count-based diff: drawing the same reward twice has to register twice.
            Dictionary<string, int> wanted = new Dictionary<string, int>();
            for (int i = 0; i < state.rewardIds.Count; i++)
            {
                string id = state.rewardIds[i];
                wanted[id] = wanted.TryGetValue(id, out int c) ? c + 1 : 1;
            }

            for (int i = 0; i < state.registeredRewardIds.Count; i++)
            {
                string id = state.registeredRewardIds[i];
                int left;
                if (wanted.TryGetValue(id, out left))
                {
                    if (left <= 1)
                    {
                        wanted.Remove(id);
                    }
                    else
                    {
                        wanted[id] = left - 1;
                    }
                }
            }

            foreach (KeyValuePair<string, int> pair in wanted)
            {
                RewardDef def = FindDef(pair.Key);
                if (def == null)
                {
                    LogMissingReward(pair.Key);
                    continue;
                }

                for (int n = 0; n < pair.Value; n++)
                {
                    if (!RegisterReward(def))
                    {
                        break;
                    }

                    state.registeredRewardIds.Add(pair.Key);
                    registered++;
                }
            }

            if (registered > 0)
            {
                CycleStateStore.PersistImmediately();
                Plugin.Log.LogInfo(
                    $"Registered {registered} previously drawn reward(s) with the game's cycle bonuses " +
                    "(one-time upgrade of an existing save).");
            }
        }

        private static bool RegisterReward(RewardDef def)
        {
            if (!CycleEffectsReady())
            {
                Plugin.Log.LogWarning(
                    $"The world cycle state is not ready; reward '{def.id}' was not registered. " +
                    "It will be retried the next time the world map is open.");
                return false;
            }

            try
            {
                bool any = false;

                if (!string.IsNullOrEmpty(def.effect))
                {
                    string effectName = ResolveEffectName(def.effect);
                    if (string.IsNullOrEmpty(effectName))
                    {
                        return false;
                    }

                    EffectModel effect = TryGetEffect(effectName);

                    ApplyEffectText(effect, def);
                    CycleEffectModel perk = GetOrCreatePerkModel(effectName, effect, def);

                    if (perk != null)
                    {
                        // Same call a world event makes: registers the cycle perk (bottom-left icon
                        // strip, stacks on repeats) and its embark effect (applied every settlement).
                        perk.Apply();
                        Plugin.Log.LogInfo(
                            $"Reward '{def.id}': granted cycle perk '{perk.Name}' for buff '{effectName}'.");
                    }
                    else
                    {
                        SO.CycleEffectsService.AddEmbarkEffect(effectName);
                        Plugin.Log.LogInfo(
                            $"Reward '{def.id}': registered cycle buff '{effectName}' " +
                            "(no cycle-perk icon; see showWorldMapPerkIcons in cycle_gacha.json).");
                    }

                    any = true;
                }

                if (def.goods != null)
                {
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

                        GoodModel model = goodName.ToGoodModel();
                        CycleEffectModel perk = FindGoodPerk(model, good.amount);

                        if (perk != null)
                        {
                            perk.Apply();
                            Plugin.Log.LogInfo(
                                $"Reward '{def.id}': granted cycle perk '{perk.Name}' for {good.amount}x '{goodName}'.");
                        }
                        else
                        {
                            SO.CycleEffectsService.ChangeEmbarkGoods(new Good(goodName, good.amount));
                            Plugin.Log.LogInfo(
                                $"Reward '{def.id}': registered {good.amount}x '{goodName}' for the next caravan.");
                        }

                        any = true;
                    }
                }

                if (def.villagers > 0)
                {
                    CycleEffectModel perk = FindVillagersPerk(def.villagers);
                    if (perk != null)
                    {
                        perk.Apply();
                        Plugin.Log.LogInfo(
                            $"Reward '{def.id}': granted cycle perk '{perk.Name}' for +{def.villagers} settler(s).");
                    }
                    else
                    {
                        SO.CycleEffectsService.ChangeEmbarkVillagers(def.villagers);
                        Plugin.Log.LogInfo(
                            $"Reward '{def.id}': registered +{def.villagers} settler(s) for the next caravan.");
                    }

                    any = true;
                }

                return any;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to register reward '{def.id}' with the cycle bonus state");
                Plugin.Log.LogError(e);
                return false;
            }
        }

        /// <summary>
        /// Returns the cycle-perk model used to show a buff in the world map's perk strip.
        /// Prefers a perk asset the game already ships for that effect; otherwise creates one at
        /// runtime (the game has no cycle-perk asset for production percentages) and registers it in
        /// Settings' cycle effect lookup so CyclePerkSlot can resolve the name again after a reload.
        /// </summary>
        private static CycleEffectModel GetOrCreatePerkModel(string effectName, EffectModel effect, RewardDef def)
        {
            if (effect == null)
            {
                return null;
            }

            if (!ModConfig.Current.showWorldMapPerkIcons)
            {
                return null;
            }

            CycleEffectModel cached;
            if (s_perkModels.TryGetValue(effectName, out cached) && cached != null)
            {
                return cached;
            }

            CycleEffectModel shipped = FindEffectPerk(effect);
            if (shipped != null)
            {
                s_perkModels[effectName] = shipped;
                return shipped;
            }

            try
            {
                GachaCycleEffectModel model = ScriptableObject.CreateInstance<GachaCycleEffectModel>();
                model.name = "CycleGacha_" + effectName;
                model.effect = effect;
                model.showAsModifier = false;
                model.perkTitle = BuildPerkTitle(def, effectName);
                model.perkDescription = BuildPerkDescription(def, effectName);

                // Kept only as a safety net for the game's own text helpers; the tooltip text comes
                // from the overrides above, because these effects have no usable localisation keys.
                model.description = new LocaText { key = "Label_CycleEffect_Generic" };

                // CycleEffectSlot.ShowTooltip() calls model.label.ToString() unconditionally, and the
                // label is also the tooltip header, so it must not be null.
                model.label = ResolvePerkLabel(effect);

                if (!RegisterPerkModel(model.name, model))
                {
                    Plugin.Log.LogWarning(
                        $"Could not register the runtime cycle perk for '{effectName}'; " +
                        "the buff will still apply, just without a world map icon.");
                    return null;
                }

                s_perkModels[effectName] = model;
                Plugin.Log.LogInfo($"Created a cycle perk model for buff '{effectName}' ('{model.name}').");
                return model;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to create a cycle perk model for '{effectName}'");
                Plugin.Log.LogError(e);
                return null;
            }
        }

        /// <summary>
        /// Puts the model into the settings lookup only. It is deliberately NOT appended to
        /// Settings.cycleEffects, so the game's own cycle-perk generation never hands our perks out.
        /// </summary>
        private static bool RegisterPerkModel(string name, CycleEffectModel model)
        {
            try
            {
                Settings settings = SO.Settings;
                if (settings == null)
                {
                    return false;
                }

                ModelCache<CycleEffectModel> cache = settings.cycleEffectsCache;
                if (cache == null)
                {
                    return false;
                }

                // The cache builds itself once, from the shipped array, on first lookup. Force that
                // build first so the shipped entries are not lost when we touch the dictionary.
                CycleEffectModel[] shipped = settings.cycleEffects;
                if (shipped != null && shipped.Length > 0 && shipped[0] != null)
                {
                    try
                    {
                        settings.GetCycleEffect(shipped[0].Name);
                    }
                    catch
                    {
                        // Not present in the cache yet; handled below.
                    }
                }

                if (cache.cache == null)
                {
                    cache.cache = new Dictionary<string, CycleEffectModel>();
                }

                if (shipped != null)
                {
                    for (int i = 0; i < shipped.Length; i++)
                    {
                        if (shipped[i] != null && !cache.cache.ContainsKey(shipped[i].Name))
                        {
                            cache.cache[shipped[i].Name] = shipped[i];
                        }
                    }
                }

                cache.cache[name] = model;
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Registering a runtime cycle perk failed: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Gives the resolved effect readable texts.
        ///
        /// The global production-rate effects the gacha uses ship without usable localisation
        /// (their displayName/description keys are not in the text database), so every UI that
        /// renders the EffectModel - the in-settlement effect list and its tooltip in particular -
        /// shows ">MISSING KEY<". OverriddenLocaText returns its key verbatim instead of looking it
        /// up, which lets the reward's own name and description from cycle_gacha.json be shown.
        /// </summary>
        private static void ApplyEffectText(EffectModel effect, RewardDef def)
        {
            if (effect == null || def == null)
            {
                return;
            }

            string title = string.IsNullOrEmpty(def.name) ? null : StripTags(def.name);
            string description = string.IsNullOrEmpty(def.description) ? null : StripTags(def.description);
            if (title == null && description == null)
            {
                return;
            }

            try
            {
                if (title != null)
                {
                    effect.displayName = new OverriddenLocaText { key = title };
                }

                if (description != null)
                {
                    effect.description = new OverriddenLocaText { key = description };
                }

                LabelModel label = GachaLabel;
                if (label != null)
                {
                    effect.label = label;
                }

                if (s_effectTextApplied.Add(effect.Name))
                {
                    Plugin.Log.LogInfo(
                        "Applied reward text to effect '" + effect.Name + "': " + (title ?? "(no name)"));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not set the reward effect text: " + e.Message);
            }
        }

        private static LabelModel s_gachaLabel;
        private static readonly HashSet<string> s_effectTextApplied = new HashSet<string>();

        /// <summary>
        /// The category label shown in the effect tooltip. OverriddenLocaText returns its key as
        /// literal text, and the effects' own label belongs to an unrelated world event
        /// ("森林空地事件 / Forest Glade Event"), so it is replaced for the gacha's effects.
        /// </summary>
        private static LabelModel GachaLabel
        {
            get
            {
                if (s_gachaLabel == null)
                {
                    try
                    {
                        s_gachaLabel = ScriptableObject.CreateInstance<LabelModel>();
                        s_gachaLabel.name = "CycleGacha_EffectLabel";
                        s_gachaLabel.displayName = new OverriddenLocaText { key = "抽奖奖励 / Cycle Gacha" };
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning("Could not create the gacha effect label: " + e.Message);
                    }
                }

                return s_gachaLabel;
            }
        }

        /// <summary>
        /// Applies the configured texts to every effect the config references, so the settlement
        /// effect strip is correct no matter which screen loads first (world map, or straight into a
        /// settlement when continuing a cycle). Cheap and idempotent after the first pass.
        /// </summary>
        public static void ApplyAllEffectTexts()
        {
            ModConfig config = ModConfig.Current;
            if (config == null || config.pools == null)
            {
                return;
            }

            List<string> rarities = new List<string>(ModConfig.Rarities);
            for (int r = 0; r < rarities.Count; r++)
            {
                List<RewardDef> pool = config.GetPool(rarities[r]);
                for (int i = 0; i < pool.Count; i++)
                {
                    RewardDef def = pool[i];
                    if (def == null || string.IsNullOrEmpty(def.effect))
                    {
                        continue;
                    }

                    string effectName = ResolveEffectName(def.effect);
                    if (string.IsNullOrEmpty(effectName))
                    {
                        continue;
                    }

                    ApplyEffectText(TryGetEffect(effectName), def);
                }
            }
        }

        private static string BuildPerkTitle(RewardDef def, string effectName)
        {
            if (def != null && !string.IsNullOrEmpty(def.name))
            {
                return StripTags(def.name);
            }

            return effectName;
        }

        private static string BuildPerkDescription(RewardDef def, string effectName)
        {
            if (def != null && !string.IsNullOrEmpty(def.description))
            {
                return StripTags(def.description);
            }

            return "每座新定居点开局生效，持续整个循环。";
        }

        /// <summary>
        /// Supplies the label a cycle perk renders with. The generic "cycle effect" label is used
        /// instead of the wrapped effect's own label, because those effects carry labels that belong
        /// to unrelated world events and would show a misleading header in the tooltip.
        /// </summary>
        private static LabelModel ResolvePerkLabel(EffectModel effect)
        {
            try
            {
                LabelModel label = ScriptableObject.CreateInstance<LabelModel>();
                label.name = "CycleGacha_Label";
                label.displayName = new LocaText { key = "Label_CycleEffect_Generic" };
                return label;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not create the perk label: " + e.Message);
            }

            if (effect != null && effect.label != null)
            {
                return effect.label;
            }

            CycleEffectModel[] all = CycleEffects;
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    EmbarkEffectCycleEffectModel shipped = all[i] as EmbarkEffectCycleEffectModel;
                    if (shipped != null && shipped.label != null)
                    {
                        return shipped.label;
                    }
                }
            }

            return null;
        }

        private static CycleEffectModel[] CycleEffects
        {
            get
            {
                try
                {
                    return SO.Settings != null ? SO.Settings.cycleEffects : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Finds the shipped cycle-perk asset that wraps a given effect, which is the object a world
        /// event grants when it hands out "production +N% for the cycle".
        /// </summary>
        private static CycleEffectModel FindEffectPerk(EffectModel effect)
        {
            CycleEffectModel[] all = CycleEffects;
            if (effect == null || all == null)
            {
                return null;
            }

            CycleEffectModel withDuration = null;
            for (int i = 0; i < all.Length; i++)
            {
                EmbarkEffectCycleEffectModel perk = all[i] as EmbarkEffectCycleEffectModel;
                if (perk == null || perk.effect != effect)
                {
                    continue;
                }

                if (!perk.HasDuration)
                {
                    return perk;
                }

                if (withDuration == null)
                {
                    withDuration = perk;
                }
            }

            return withDuration;
        }

        private static CycleEffectModel FindGoodPerk(GoodModel good, int amount)
        {
            CycleEffectModel[] all = CycleEffects;
            if (good == null || all == null)
            {
                return null;
            }

            CycleEffectModel withDuration = null;
            for (int i = 0; i < all.Length; i++)
            {
                EmbarkGoodCycleEffectModel perk = all[i] as EmbarkGoodCycleEffectModel;
                if (perk == null || perk.good.good != good || perk.good.amount != amount)
                {
                    continue;
                }

                if (!perk.HasDuration)
                {
                    return perk;
                }

                if (withDuration == null)
                {
                    withDuration = perk;
                }
            }

            return withDuration;
        }

        private static CycleEffectModel FindVillagersPerk(int amount)
        {
            CycleEffectModel[] all = CycleEffects;
            if (amount <= 0 || all == null)
            {
                return null;
            }

            CycleEffectModel withDuration = null;
            for (int i = 0; i < all.Length; i++)
            {
                EmbarkVillagersCycleEffectModelModel perk = all[i] as EmbarkVillagersCycleEffectModelModel;
                if (perk == null || perk.amount != amount)
                {
                    continue;
                }

                if (!perk.HasDuration)
                {
                    return perk;
                }

                if (withDuration == null)
                {
                    withDuration = perk;
                }
            }

            return withDuration;
        }

        /// <summary>
        /// One-shot diagnostic: what this game build actually ships in Settings.cycleEffects, and
        /// whether the drawn buffs can be shown as cycle perks (the world map icon strip).
        /// </summary>
        private static void LogCycleEffectInventory()
        {
            if (s_inventoryLogged)
            {
                return;
            }

            s_inventoryLogged = true;

            try
            {
                CycleEffectModel[] all = CycleEffects;
                if (all == null)
                {
                    Plugin.Log.LogWarning("Cycle effect inventory: Settings.cycleEffects is null.");
                    return;
                }

                int embarkEffects = 0;
                int embarkGoods = 0;
                int embarkVillagers = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is EmbarkEffectCycleEffectModel) embarkEffects++;
                    else if (all[i] is EmbarkGoodCycleEffectModel) embarkGoods++;
                    else if (all[i] is EmbarkVillagersCycleEffectModelModel) embarkVillagers++;
                }

                Plugin.Log.LogInfo(
                    $"Cycle effect inventory: {all.Length} asset(s) — {embarkEffects} effect perk(s), " +
                    $"{embarkGoods} good perk(s), {embarkVillagers} population perk(s).");

                Settings settings = SO.Settings;
                int effectCount = settings != null && settings.effects != null ? settings.effects.Length : -1;
                int rateCount = 0;
                System.Text.StringBuilder rates = new System.Text.StringBuilder();
                if (effectCount > 0)
                {
                    for (int i = 0; i < settings.effects.Length; i++)
                    {
                        GlobalProductionRateEffectModel rate = settings.effects[i] as GlobalProductionRateEffectModel;
                        if (rate == null)
                        {
                            continue;
                        }

                        rateCount++;
                        if (rateCount <= 20)
                        {
                            rates.Append('[').Append(rate.Name).Append(" = ")
                                 .Append(Math.Round(rate.amount * 100f)).Append("%] ");
                        }
                    }
                }

                Plugin.Log.LogInfo(
                    $"Settings.effects: {effectCount} model(s); production-rate effects: {rateCount} " +
                    (rates.Length > 0 ? rates.ToString() : "(none)"));

                // Which of the drawn buffs have a perk asset wrapping their effect model?
                CycleState state = CycleStateStore.State;
                if (state.rewardIds == null)
                {
                    return;
                }

                for (int i = 0; i < state.rewardIds.Count; i++)
                {
                    RewardDef def = FindDef(state.rewardIds[i]);
                    if (def == null || string.IsNullOrEmpty(def.effect))
                    {
                        continue;
                    }

                    string effectName = ResolveEffectName(def.effect);
                    if (string.IsNullOrEmpty(effectName))
                    {
                        continue;
                    }

                    CycleEffectModel perk = FindEffectPerk(TryGetEffect(effectName));
                    Plugin.Log.LogInfo(
                        perk != null
                            ? $"Buff '{effectName}' -> cycle perk '{perk.Name}' (will show on the world map)."
                            : $"Buff '{effectName}' -> no cycle perk asset wraps it (applies, but without a world map icon).");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Cycle effect inventory failed: " + e.Message);
            }
        }

        private static bool s_dataInventoryLogged;

        /// <summary>
        /// Logs what the current game build actually exposes for the configured reward effects.
        /// Called from the plugin's Update, so it runs as soon as the game's settings exist.
        /// </summary>
        public static bool TryLogDataInventory()
        {
            if (s_dataInventoryLogged)
            {
                return true;
            }

            Settings settings;
            try
            {
                settings = SO.Settings;
            }
            catch
            {
                return false;
            }

            if (settings == null || settings.effects == null)
            {
                return false;
            }

            s_dataInventoryLogged = true;

            try
            {
                int rateCount = 0;
                System.Text.StringBuilder rates = new System.Text.StringBuilder();
                for (int i = 0; i < settings.effects.Length; i++)
                {
                    GlobalProductionRateEffectModel rate = settings.effects[i] as GlobalProductionRateEffectModel;
                    if (rate == null)
                    {
                        continue;
                    }

                    rateCount++;
                    if (rateCount <= 24)
                    {
                        rates.Append('[').Append(rate.Name).Append('=')
                             .Append(Math.Round(rate.amount * 100f)).Append("%] ");
                    }
                }

                Plugin.Log.LogInfo(
                    $"Game data: Settings.effects has {settings.effects.Length} model(s), " +
                    $"{rateCount} of them global production-rate effects: " +
                    (rates.Length > 0 ? rates.ToString() : "(none)"));

                // How do the buff rewards configured in cycle_gacha.json resolve here?
                ModConfig config = ModConfig.Current;
                List<string> poolNames = new List<string>(ModConfig.Rarities);
                Plugin.Log.LogInfo(
                    config == null
                        ? "Config resolution: ModConfig.Current is null"
                        : $"Config resolution: pools legendary={config.pools.legendary.Count} epic={config.pools.epic.Count} " +
                          $"rare={config.pools.rare.Count} uncommon={config.pools.uncommon.Count} common={config.pools.common.Count}");
                if (config == null)
                {
                    return true;
                }
                for (int p = 0; p < poolNames.Count; p++)
                {
                    List<RewardDef> pool = config.GetPool(poolNames[p]);
                    for (int i = 0; i < pool.Count; i++)
                    {
                        RewardDef def = pool[i];
                        if (def == null || string.IsNullOrEmpty(def.effect))
                        {
                            continue;
                        }

                        string resolved = ResolveEffectName(def.effect);
                        Plugin.Log.LogInfo(
                            resolved != null
                                ? "Reward effect " + def.effect + " resolves to " + resolved
                                : "Reward effect " + def.effect + " is NOT available in this game build");
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Game data inventory failed: " + e.Message);
            }

            return true;
        }

        private static bool CycleEffectsReady()        {
            try
            {
                return SO.CycleEffectsService != null &&
                       Serviceable.WorldStateService != null &&
                       Serviceable.WorldStateService.CycleEffects != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resolves a configured good key (GoodsTypes enum name or an internal name) to its model,
        /// so the UI can use the game's own icon for it.
        /// </summary>
        public static GoodModel ResolveGoodModel(string key)
        {
            string name = ResolveGoodName(key);
            return string.IsNullOrEmpty(name) ? null : name.ToGoodModel();
        }

        public static RewardDef FindDef(string rewardId)        {
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
                if (effect != null && !string.IsNullOrEmpty(effect.DisplayName))
                {
                    return StripTags(effect.DisplayName);
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
                if (effect != null && !string.IsNullOrEmpty(effect.Description))
                {
                    return StripTags(effect.Description);
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

        /// <summary>
        /// Resolves the effect's internal name, which is what the game's embark effect list stores.
        /// The name is only accepted when the game's own lookup finds it, because
        /// EffectsService.AddLateInitialEffects dereferences the result without a null check.
        /// </summary>
        public static string ResolveEffectName(string key)
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
                if (TryGetEffect(internalName) != null)
                {
                    return internalName;
                }

                // The EffectTypes enum in ATS_API is generated from an older game build, so a name
                // that still lists there can be missing (renamed or moved) in the current one.
                // Fall back to the effect with the same percentage, found by its amount value.
                string byAmount = FindProductionPercentEffectName(key);
                if (byAmount != null)
                {
                    Plugin.Log.LogInfo(
                        $"Effect '{internalName}' is not in this game build; using '{byAmount}' " +
                        "(same percentage) for the production buff.");
                    return byAmount;
                }

                LogMissingEffect(key + " (" + internalName + ")");
                return null;
            }

            if (TryGetEffect(key) != null)
            {
                return key;
            }

            string rawByAmount = FindProductionPercentEffectName(key);
            if (rawByAmount != null)
            {
                Plugin.Log.LogInfo(
                    $"Effect '{key}' is not in this game build; using '{rawByAmount}' (same percentage).");
                return rawByAmount;
            }

            LogMissingEffect(key);
            return null;
        }

        /// <summary>
        /// Finds a global production-rate effect by its amount instead of by name. The key is
        /// expected to end with the percentage (Global_Production_Faster_25, "Production +25%").
        /// </summary>
        private static string FindProductionPercentEffectName(string key)
        {
            try
            {
                System.Text.RegularExpressions.Match match =
                    System.Text.RegularExpressions.Regex.Match(key, @"(\d+)\s*%?$");
                if (!match.Success)
                {
                    return null;
                }

                int percent;
                if (!int.TryParse(match.Groups[1].Value, out percent) || percent <= 0)
                {
                    return null;
                }

                Settings settings = SO.Settings;
                if (settings == null || settings.effects == null)
                {
                    return null;
                }

                float wanted = percent / 100f;
                for (int i = 0; i < settings.effects.Length; i++)
                {
                    GlobalProductionRateEffectModel rate = settings.effects[i] as GlobalProductionRateEffectModel;
                    if (rate == null)
                    {
                        continue;
                    }

                    if (Math.Abs(rate.amount - wanted) < 0.0005f)
                    {
                        return rate.Name;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public static EffectModel ResolveEffect(string key)
        {
            string name = ResolveEffectName(key);
            return name == null ? null : TryGetEffect(name);
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
            return model != null ? model.Name : null;
        }

        private static string GetGoodDisplayName(string key)
        {
            string internalName = ResolveGoodName(key);
            if (string.IsNullOrEmpty(internalName))
            {
                return key;
            }

            GoodModel model = internalName.ToGoodModel();
            if (model != null && model.displayName != null && model.displayName.HasText)
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

        /// <summary>
        /// Resolves an effect by its internal name.
        /// Settings.GetEffect is tried first: it works as soon as the settings asset is loaded, and
        /// the world map is exactly where the mod needs it. GameModelService.GetEffect is only a
        /// fallback for modded/dynamic effects, because it is a per-settlement service whose
        /// dynamicEffects dictionary is null on the world map and throws a NullReferenceException
        /// there. The two lookups need separate try blocks: sharing one let that exception hide the
        /// working lookup, which is what kept every buff reward from ever resolving.
        /// </summary>
        private static EffectModel TryGetEffect(string internalName)
        {
            if (string.IsNullOrEmpty(internalName))
            {
                return null;
            }

            try
            {
                Settings settings = SO.Settings;
                if (settings != null)
                {
                    // ContainsEffect does a dictionary lookup that returns false instead of throwing.
                    if (settings.ContainsEffect(internalName))
                    {
                        EffectModel viaSettings = settings.GetEffect(internalName);
                        if (viaSettings != null)
                        {
                            return viaSettings;
                        }
                    }
                }
            }
            catch
            {
                // Fall through to the service lookup.
            }

            try
            {
                if (SO.GameModelService != null)
                {
                    return SO.GameModelService.GetEffect(internalName);
                }
            }
            catch
            {
                // Not a dynamic effect either.
            }

            return null;
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
