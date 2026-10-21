# 0.2.0

- Changed rewards from one-time resources to real world-event-style buffs.
- Buffs are existing `EffectModel` effects (the `[WE]` world-event effects) applied with `EffectModel.Apply()`.
- Buffs are re-applied in `EffectsService.AddLateInitialEffects`, the same point where the game applies world-event and embark effects.
- Default buff pools are themed: Orders -> economy/production, Resolve -> resolve/housing, Glade -> exploration/tech.
- Config now uses `effect` (an `EffectTypes` enum name or the effect's internal name) instead of `goods`/`villagers`.

# 0.1.0

- First version.
- Tracks reputation gained from orders, resolve and glade events independently.
- Every 18 points in a dimension grants one random blessing from that dimension's pool.
- Blessings are stored in `ModSaveData.CurrentCycle` and apply at the start of every new settlement in the cycle.
- Resets automatically when the world cycle ends.
- Configurable thresholds, source matching and blessing pools in `cycle_blessings.json`.
- Status popup hotkey (default F8).
