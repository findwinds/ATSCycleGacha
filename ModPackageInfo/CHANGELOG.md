# 0.1.0

- First version.
- Tracks reputation gained from orders, resolve and glade events independently.
- Every 18 points in a dimension grants one random blessing from that dimension's pool.
- Blessings are stored in `ModSaveData.CurrentCycle` and apply at the start of every new settlement in the cycle.
- Resets automatically when the world cycle ends.
- Configurable thresholds, source matching and blessing pools in `cycle_blessings.json`.
- Status popup hotkey (default F8).
