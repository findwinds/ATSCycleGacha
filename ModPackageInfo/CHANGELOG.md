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

