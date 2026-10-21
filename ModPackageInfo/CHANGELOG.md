# 0.4.0

- Rewards now only use resources, global buff effects and population; blueprints, recipes and active abilities were removed.
- Added a Common tier: 16 cards with a gray-white frame, mostly small resource bundles, +1 settler or small global bonuses.
- The wheel now has 31 options: 1 Legendary, 2 Epic, 4 Rare, 8 Uncommon, 16 Common.
- Winning a settlement now grants 10 tickets (configurable via `ticketsPerSettlement`).
- Every draw charges a 10-point free-draw meter; charge depends on rarity (Legendary 10, Epic 5, Rare 3, Uncommon 2, Common 1).
- Rewards can now combine an effect, goods and villagers in `cycle_gacha.json`.
- Old `cycle_gacha.json` files are backed up as `.old` and replaced with the new default schema.

# 0.3.0

- Removed reputation tracking entirely; it was fragile and is no longer needed.
- Winning a settlement grants draw tickets (losses configurable).
- Added a WorldMap IMGUI gacha window (default F7).
- Drawing removes the option; after at least one draw the wheel can be reset for free.
- Rewards are real world-event-style `EffectModel` buffs, applied at every new settlement start.
- Config file renamed to `cycle_gacha.json`.

# 0.2.0

- Changed rewards from one-time resources to real world-event-style buffs.
- Buffs are existing `EffectModel` effects applied with `EffectModel.Apply()`.
- Buffs are re-applied in `EffectsService.AddLateInitialEffects`.

