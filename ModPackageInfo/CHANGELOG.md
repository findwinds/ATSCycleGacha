# 0.9.0

- Reworked the gacha window so it reads as part of the game instead of a default IMGUI panel:
  - dark leather panel with a thin gold rule and an inset border, generated as 9-sliced textures and
    drawn with the parchment/gold palette the game's own UI uses;
  - real item icons taken from `GoodModel.icon` / `EffectModel.GetIcon()`, drawn from the sprite
    atlas, so cards show the actual stone / roots / villagers / buff art;
  - rarity accent colours on the card edges plus a small rarity caption (传说 / 史诗 / 稀有 / 罕见 / 普通);
  - a side panel with the charge meter, remaining-per-rarity counters, the draw / reset buttons and
    the owned-reward list with icons and stack counts;
  - the window is centred and sized from the current resolution, and the corner ticket badge is
    hidden while the window is open.
- Sectioned layout: reward grid on the left (5 columns, scrollable), information and actions on the
  right; the title bar carries the ticket count and a close button.
- No rule changes: draw, charge, reset, storage and reward application all still go through the same
  services.

# 0.8.4

- The reward texts are now applied to the effects at startup (`ApplyAllEffectTexts` from the
  plugin's `Update`), not only when a reward is registered. Continuing a cycle loads straight into a
  settlement, which never visits the world map, so 0.8.3's texts were never written in that session
  and the effect strip still showed `>MISSING KEY<`.
- The effects' category label is replaced too: it used to be inherited from an unrelated world event
  and read "森林空地事件 / Forest Glade Event". It now reads "抽奖奖励 / Cycle Gacha" via an
  `OverriddenLocaText`.
- Verified in game: hovering a drawn buff in the settlement's bottom-left effect strip shows the
  reward's name, the "抽奖奖励 / Cycle Gacha" label and its description.

# 0.8.3

- Fixed the effect details shown for the rewards *inside a settlement*. The three icons in the
  bottom-left effect strip were the drawn buffs already applying (they arrive through
  `embarkEffects`), but their name and description came from the shared
  `GlobalProductionRateEffectModel` assets, which ship without usable localisation, so the strip and
  its tooltip rendered `>MISSING KEY<`.
- `EffectSlot.SetUp` writes `displayName.text = model.DisplayName` and the description through
  `model.Description` / `FormatedDescription`, so the mod now assigns `OverriddenLocaText`
  (whose `GetText()` returns the key verbatim instead of looking it up) with the reward's own name
  and description from `cycle_gacha.json` to the resolved effect.
- No behaviour change: the buffs were already applied; only their text was wrong.

# 0.8.2

- Runtime cycle perks now derive from `GachaCycleEffectModel`, a subclass of
  `EmbarkEffectCycleEffectModel` that overrides `DisplayName` and `GetDescription()`. The shipped
  effect assets behind the gacha buffs carry no usable localisation, so the perk used to render
  `>MISSING KEY<` in the tooltip. The tooltip now shows the reward's name and description from
  `cycle_gacha.json`.
- The perk label is now bound to the game's generic `Label_CycleEffect_Generic` key instead of
  borrowing the wrapped effect's label, which belonged to an unrelated world event (it read
  "森林空地事件 / Forest Glade Event").
- Perk models are re-created on every world map visit (`EnsurePerkModels`), not only when a reward is
  registered. The strip stores perk *names* and resolves them through `Settings.GetCycleEffect()`,
  so a save whose rewards were already registered threw on a model that only existed in the previous
  session and the world map stayed on the loading screen.
- Verified in game (launched through Steam, so the DLC check passes): the bottom-left cycle perk
  strip shows the drawn buffs with their stack counts, and hovering one shows title, label and
  description.

# 0.8.1

- Cycle perks created at runtime now get a `label`, borrowed from the effect or from a shipped cycle
  perk, falling back to a new `LabelModel` bound to `Label_CycleEffect_Generic`.
  `CycleEffectSlot.ShowTooltip()` calls `model.label.ToString()` unconditionally, so with a null
  label the hover tooltip threw and never appeared for the generated perk icons.
- The icon's frame colour comes from `CycleEffectModel.GetColor()`, which only distinguishes
  positive from negative (`Settings.RewardColorCommonPositive` / `RewardColorCommonNegative`) and
  does not vary by rarity, so generated perks use the same colour as any other positive cycle perk
  granted by a world event. The tooltip's icon frame is a fixed part of the tooltip prefab, which is
  why it looks grey there.

# 0.8.0

- Fixed the actual reason buff rewards never worked: `RewardService.TryGetEffect` called
  `GameModelService.GetEffect` first and swallowed everything with one try/catch.
  `GameModelService` is a per-settlement service whose `dynamicEffects` dictionary is null on the
  world map, so it threw `NullReferenceException` there, the catch turned that into "no effect",
  and the working `Settings.GetEffect` call was never reached. Every buff reward was therefore
  dropped before it could be registered, applied or shown.
- `Settings.GetEffect` is now tried first (guarded by `Settings.ContainsEffect`, which returns
  false instead of throwing), with `GameModelService.GetEffect` only as a fallback for dynamic
  effects, each in its own try block.
- Also kept the amount-based fallback: if an `EffectTypes` name from a newer/older ATS_API build is
  missing, the global production-rate effect with the same percentage is used instead.
- Diagnostics were added to find this (`Settings.effects` inventory and a per-reward resolution
  report at startup); they stay in the log and are harmless.

# 0.7.2

- Buff rewards now really do appear in the world map's bottom-left cycle perk strip. 0.7.1 tried to
  reuse a shipped perk asset, but this game build ships no cycle-effect asset for production
  percentages (its localisation only has `CycleEffect_EmbarkGoodsAdd_Generic`,
  `CycleEffect_Villagers_GenericGain*`, `CycleEffect_EmbarkRange`, `CycleEffect_SealFragments`,
  `CycleEffect_PreparationPoints`, `CycleEffect_Years_*`, `CycleEffect_Status_*`,
  `CycleEffect_WeCornerstonesRerrolls`, `CycleEffect_GhostTownForceTraderPriceReduction`), so that
  lookup could never match.
- The mod now creates an `EmbarkEffectCycleEffectModel` per drawn buff at runtime and registers it
  in `Settings.cycleEffectsCache` (the lookup `Settings.GetCycleEffect` uses). Its `Apply()` does
  both halves: `CyclePerksService.Add` (perk slot with stacks) and `AddEmbarkEffect` (applied in
  every settlement). Repeated draws of the same buff stack, as usual.
- The runtime model is written to the lookup dictionary only, never to `Settings.cycleEffects`, so
  the game's own cycle perk generation cannot hand out gacha perks.
- New config option `showWorldMapPerkIcons` (default true) turns the runtime perk models off; buffs
  then still apply, just without a world map icon.
- Goods and population use the same cycle-bonus channel as before; they are listed in the caravan /
  embark screen, which is where the game shows embark bonuses.

# 0.7.1

- Rewards now also show up on the world map, next to the effects granted by world events. The
  bottom-left icon strip is `CyclePerksHUD`, which renders `CycleEffectModel` cycle perks, so a raw
  `EffectModel` never appeared there. The mod now looks up the shipped cycle-perk asset that wraps
  the drawn effect (`EmbarkEffectCycleEffectModel.effect == drawn effect`) and calls its `Apply()`,
  which is exactly what a world event does: `Apply()` registers the perk (HUD icon, stacks on
  repeated draws) and its embark effect (re-applied in every settlement).
- The same idea is used for goods and population when this game build ships a matching
  `EmbarkGoodCycleEffectModel` / `EmbarkVillagersCycleEffectModelModel` asset (same good and amount);
  otherwise it falls back to the plain `ChangeEmbarkGoods` / `ChangeEmbarkVillagers` calls, which
  apply correctly but have no world-map icon.
- Only perks without a duration are preferred, so a reward lasts the whole cycle instead of expiring
  after N won games.

# 0.7.0

- Rewards now go through the game's own cycle-bonus system, the same one a world event uses when it
  grants a cycle reward. On draw:
  - buff → `SO.CycleEffectsService.AddEmbarkEffect(name)`
  - goods → `SO.CycleEffectsService.ChangeEmbarkGoods(new Good(name, amount))`
  - population → `SO.CycleEffectsService.ChangeEmbarkVillagers(n)`

  The game then copies them into the caravan shown on the world map
  (`CaravanPickSlot` / `MetaPerksSummaryPanel`), folds them into `embarkEffects` / `embarkGoods`
  when the settlement is created, and applies them itself: buffs in
  `EffectsService.AddLateInitialEffects`, goods in `StorageService.StoreInitialGoods`
  (`StorageOperationType.InitialCaravan`), population via `CaravanGenerator.GetVillagersAmount`.
  `CycleEffectsService.OnCycleEnded()` rebuilds that state, so rewards clear with the cycle.
- The mod no longer applies anything itself: removed the readiness coroutine, the
  `StorageService.Main.Goods.Add` path (which was overwritten by the async storage load), the
  settler spawn (which threw in `ResolveService`), and the `activeEventsEffects` writing from 0.6.0.
- Rewards drawn by 0.5.0/0.6.0 are registered once with the game when the save is loaded, tracked
  per reward in `registeredRewardIds` so repeated draws still stack.
- Effects are only registered when the game's own `GameModelService.GetEffect` finds them, because
  `AddLateInitialEffects` dereferences the lookup result without a null check.

# 0.6.0

- Rewards now actually reach the player. 0.5.0 applied everything from a postfix on
  `EffectsService.AddLateInitialEffects`, which runs too early: `StorageService.OnLoading` is async
  and fills the main storage afterwards, so granted goods were overwritten, and `ResolveService`
  had no per-race data yet, so spawning settlers threw `KeyNotFoundException`.
- Buff rewards are no longer applied by the mod. Their effect names are registered in the game's
  own cycle effect list (`WorldStateService.Cycle.activeEventsEffects`), exactly like a world-event
  reward, so the game applies them at every settlement start and lists them on the world map.
  Duplicates stack, and the list is synced on draw, on the world map and after a save is loaded.
- Goods and population are granted from a coroutine after the settlement's services are ready
  (main storage exists and storage buildings are registered), with the storage amount logged before
  and after each grant.
- Population rewards pick from active `Settings.Races` instead of the villager service's race keys.
- Removed the `EffectsService.AddLateInitialEffects` patch and `SettlementStartPatch`.
- Requires the same ATS_API 3.7.0.

# 0.5.0

- Rewards are now strictly single-type: one effect, one good, or one population group. Composite rewards are skipped.
- Bonus rewards are percentage-only, using `Global_Production_Faster_5/10/15/20/25`.
- Reworked resource/population values by rarity:
  - Raw food: 10/20/30/40/50.
  - Building materials: 1/5/10/15/20.
  - Population: 1/2/3/4/5.
- Charge values are now Common 1, Uncommon 2, Rare 3, Epic 4, Legendary 5.
- A full 10-point charge now grants +1 draw ticket; overflow carries into the next charge cycle.
- Cycle reset clears tickets, charge, wheel and drawn rewards (via ATS_API's CurrentCycle reset).
- Renamed the project, assembly, plugin GUID and namespace from CycleBlessings to CycleGacha.

# 0.4.0

- Rewards now only use resources, global buff effects and population; blueprints, recipes and active abilities were removed.
- Added a Common tier: 16 cards with a gray-white frame.
- The wheel now has 31 options: 1 Legendary, 2 Epic, 4 Rare, 8 Uncommon, 16 Common.
- Winning a settlement grants 10 tickets.
- Every draw charges a 10-point meter based on rarity.

