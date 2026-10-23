# 0.15.2

- Clicks on the gacha window no longer reach the world map underneath. The window is drawn with
  IMGUI, which the game's EventSystem cannot see, so `InputService.IsOverUI()` reported "not over UI"
  and the click on the draw button also landed on the map (opening a field and jumping to another
  screen). A prefix on `InputService.IsOverUI()` now reports "over UI" while the cursor is inside the
  window, which covers every consumer at once (`WorldMapPointer.ReadClick` -> `IsWorldClicked` ->
  `IsOverUI`).

# 0.15.1

- Wiping the save now clears the gacha cycle state (tickets, charge, drawn wheel and the reward
  list). Clearing a save does not end a cycle: in the Queen's Hand trial the world map is kept and
  the calendar never ends, so ATS_API's cycle-ended and new-game hooks never fired and the charge
  and draw history survived the wipe. A postfix on `MetaStateService.ResetProgress` - the call behind
  the wipe button - resets the board and writes the cleared state out immediately.

# 0.15.0

- A cycle now starts with 10 draw tickets (`startingTickets` in cycle_gacha.json), so the board can
  be played before the first settlement is won. Saves from an older build get the bonus once, tracked
  by `startingTicketsGranted` in the cycle state and persisted immediately.
- Card names wrap onto a second line instead of being cut with an ellipsis; the cards always had the
  room for it and only the truncation was in the way. A name longer than 60 characters still falls
  back to truncation.
- Config schema is now 7 (adds `startingTickets`).

# 0.14.3

- Fixed the rarity-coloured world map perk frames, which had stopped working:
  - the refactor dropped the `frameColor` assignments, and goods perks were created as plain
    `EmbarkGoodCycleEffectModel` instead of the colour-carrying subclass, so both fell back to the
    game's generic palette;
  - `CycleEffectModel.GetColor()` turned out to be a dead end anyway: the perk slot prefab leaves
    `CycleEffectSlot.frame` null and the visible border is a child Image ("Frame", sprite
    FRAME_Square3_32x32) tinted cyan by the prefab, so the override was never consulted. A postfix on
    `CycleEffectSlot.SetUp` now tints that image with the reward's rarity colour.
  - Verified in game by sampling the rendered pixels: uncommon perks R102 G177 B111, common perks
    R188 G180 B160, matching `RarityPalette` exactly.

# 0.14.2

- `showWorldMapPerkIcons` is now hardcoded to `true` and removed from `cycle_gacha.json`; the world map cycle-perk strip is always populated.
- The config now only contains `schemaVersion`, `gachaHotkey`, `grantTicketOnLoss` and `ticketsPerSettlement`.
- Existing configs are backed up as `.old` and regenerated with schema 6.

# 0.14.1

- Reward card names are now tinted with the rarity colour (legendary orange, epic purple, rare blue, uncommon green, common grey-white) instead of plain white. Drawn cards keep their dimmed grey text.

# 0.14.0

- Removed the `wheel`, `charge` and `pools` sections from `cycle_gacha.json`. The 31-slot board shape, per-rarity charge values and the reward table are now fixed in code (`ModConfig` / `BuildPools`) because they are tuned to the 6x6 UI.
- The config now only holds runtime options: `schemaVersion`, `gachaHotkey`, `grantTicketOnLoss`, `ticketsPerSettlement`, `showWorldMapPerkIcons`.
- Existing configs are backed up as `.old` and regenerated with the small schema.

# 0.13.2

- Drawn (dimmed) cards now keep a dimmed rarity-coloured frame on all four sides, instead of losing the rarity ring.
- The wheel can now only be rerolled after the legendary reward has been drawn. If the config has no legendary option at all, the old any-draw fallback is kept so the board cannot become permanently stuck.

# 0.13.1

- Card frames now draw the rarity colour on all four sides, not just the top and bottom.
- The 6x6 board is completely filled: the legendary 2x2 card stays centred, the 30 normal cards fill the remaining cells, and the Draw/Reset buttons now sit in the two cells directly below the legendary card.
- Draw/Reset were moved out of the footer into those board cells; the footer now only shows the last result.

# 0.13.0

- The perk icons the mod adds to the world map's cycle strip now use the reward's rarity colour
  (grey/white, green, blue, purple, orange) instead of the game's single positive/negative colour.
  `CycleEffectModel.GetColor()` is virtual, so the generated models (`GachaCycleEffectModel` for
  buffs, `GachaGoodPerkModel` for goods) override it and return the rarity colour of the reward they
  came from; `RarityPalette` is the single source for those colours, used by both the strip and the
  gacha window.
- Board layout is no longer radial. The legendary option is one large card in the middle (2x2 grid
  cells) and the other 30 options fill the surrounding cells in a compact card block, ordered by a
  stable hash of the reward id so the arrangement looks random but never reshuffles between frames
  or reloads.
- Card text follows the card size (grid cell) rather than the rarity, cards have a visible panel
  fill, and the icon column is narrower so names get room.

# 0.12.0

- Goods rewards now appear in the world map's cycle perk strip like the buffs. The game only ships
  embark-good perks for its own bundles, so each goods reward gets a generated
  `EmbarkGoodCycleEffectModel` (named `CycleGacha_good_<reward id>`) with the right good and amount,
  its own "+N <good>" title and description, registered in the cycle effect lookup. `Apply()`
  registers both the perk icon and the caravan goods.
- Draw animation is now two phases: the sweep eases out (slowest right before deciding) and lands on
  the winner, which then keeps blinking for five seconds before the buttons unlock.
- The result is only committed when the sweep lands: the side panel's owned-reward list and count no
  longer reveal the prize while the highlight is still running. The pending reward is stored in the
  save (`pendingRewardId`) and committed on the next session if the game is closed mid-animation,
  so a draw is never lost.

# 0.11.0

- The board is now radial instead of a grid: the legendary option sits in the middle and each lower
  rarity forms a ring around it (2 epic, 4 rare, 8 uncommon, 16 common), with the card size and text
  size stepping down per rarity, so the layout itself shows how rare an option is.
- Drawing animates: a highlight runs around the board, skips options that were already taken, slows
  down and stops on the winning option. The result is reported under the board.
- Drawn options stay on the board, greyed out, instead of disappearing - that keeps the rings in
  shape and gives the sweep something to skip. `WheelOption.drawn` is persisted, so a reload keeps
  them greyed.
- Hovering a card shows its name, rarity, charge and description in a tooltip.

# 0.10.0

- New default reward table, Chinese text only:
  - two percentage buffs per tier - 生产速度 (+1/+3/+6/+10/+15%) and 生产翻倍几率
    (+1/+2/+3/+4/+5%);
  - 19 goods with per-tier amounts (管道, 晶露, 琥珀, 零部件, 砖头, 面粉, 野火精华, 油, 稻子,
    成包的供给品, 蘑菇, 陶器, 靴子, 蔬菜, 昆虫, 木板, 工具, 石头, 蛋), so every tier offers the
    same 21 options and the wheel keeps drawing 1/2/4/8/16 of them;
  - reward text no longer carries the English half, and goods use the game's own localised names;
  - population rewards are no longer part of the default pool.
- Percentages the game does not ship as effects are generated at runtime: the mod clones a shipped
  `GlobalProductionRateEffectModel` / `GlobalExtraProductionChanceEffectModel`, sets its `amount` and
  registers it in Settings' effect lookup (`CycleGacha_production_speed_6`, ...). Config keys use the
  `cycle:production_speed:<percent>` / `cycle:double_yield:<percent>` form. Verified in the log that
  the generated effects report the intended amount (+1% .. +15%).
- Config schema is now 4; an old `cycle_gacha.json` is backed up to `.old` and rebuilt.
- The wheel is rebuilt when it references reward ids the current table no longer has, so a save
  opened after a table change shows the new cards instead of raw ids.
- The cycle perk list is reconciled against the current table: perks whose effect no longer exists
  are dropped (and removed from `bonusEmbarkEffects`) instead of making the world map stall on a
  `Settings.GetCycleEffect` miss, and the models for the remaining ones are re-created.

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

