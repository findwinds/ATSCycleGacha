# 0.3.0

- Removed reputation tracking entirely; it was fragile and is no longer needed.
- Winning a settlement now grants 1 draw ticket (losses are configurable, default off).
- Added a WorldMap IMGUI gacha window (default F7).
- Each wheel has 15 options: 1 Legendary, 2 Epic, 4 Rare, 8 Uncommon.
- Drawing removes the option; after at least one draw the wheel can be reset for free.
- Rewards are still real world-event-style `EffectModel` buffs, applied at every new settlement start.
- Config file renamed to `cycle_gacha.json`.

# 0.2.0

- Changed rewards from one-time resources to real world-event-style buffs.
- Buffs are existing `EffectModel` effects (the `[WE]` world-event effects) applied with `EffectModel.Apply()`.
- Buffs are re-applied in `EffectsService.AddLateInitialEffects`, the same point where the game applies world-event and embark effects.
- Config now uses `effect` (an `EffectTypes` enum name or the effect's internal name) instead of `goods`/`villagers`.

# 0.1.0

- First version.
- Tracks reputation gained from orders, resolve and glade events independently.
- Every 18 points in a dimension grants one random buff from that dimension's pool.
