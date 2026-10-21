using System;
using System.Collections.Generic;

namespace Forwindz.CycleBlessings
{
    /// <summary>
    /// Owns the 15-option wheel: 1 Legendary, 2 Epic, 4 Rare and 8 Uncommon by default.
    /// Drawn options are removed; after at least one draw the player may regenerate the wheel.
    /// </summary>
    public static class GachaService
    {
        private static readonly System.Random s_random = new System.Random(Environment.TickCount);

        public static bool EnsureWheel()
        {
            CycleState state = CycleStateStore.State;
            if (!state.wheelGenerated)
            {
                GenerateWheel();
            }

            return state.wheel.Count > 0;
        }

        public static WheelOption Draw()
        {
            try
            {
                if (!EnsureWheel())
                {
                    return null;
                }

                CycleState state = CycleStateStore.State;
                if (state.tickets <= 0 || state.wheel.Count == 0)
                {
                    return null;
                }

                int index = s_random.Next(state.wheel.Count);
                WheelOption option = state.wheel[index];
                state.wheel.RemoveAt(index);
                state.tickets--;
                state.drawsSinceWheelGenerated++;
                state.totalDraws++;
                state.effectNames.Add(option.effect);

                CycleStateStore.PersistImmediately();
                Plugin.Log.LogInfo($"Gacha draw #{state.totalDraws}: {option.rarity} {option.effect}, {state.effectNames.Count} buff(s) owned.");
                return option;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to draw a gacha option");
                Plugin.Log.LogError(e);
                return null;
            }
        }

        public static bool CanReset()
        {
            return CycleStateStore.State.drawsSinceWheelGenerated > 0;
        }

        public static bool ResetWheel()
        {
            if (!CanReset())
            {
                return false;
            }

            GenerateWheel();
            return true;
        }

        public static void GenerateWheel()
        {
            CycleState state = CycleStateStore.State;
            state.wheel.Clear();
            state.drawsSinceWheelGenerated = 0;

            for (int r = 0; r < ModConfig.Rarities.Length; r++)
            {
                string rarity = ModConfig.Rarities[r];
                int count = ModConfig.Current.GetWheelCount(rarity);
                List<BlessingDef> pool = ModConfig.Current.GetPool(rarity);
                List<BlessingDef> available = BuildAvailable(pool);

                for (int i = 0; i < count; i++)
                {
                    // Prefer unique entries inside one tier. Fall back to with-replacement
                    // only when the configured pool is smaller than the requested count.
                    BlessingDef def = PickAndRemove(available);
                    if (def == null)
                    {
                        def = Pick(pool);
                    }

                    if (def == null)
                    {
                        Plugin.Log.LogWarning($"No usable {rarity} buffs; the wheel will have fewer than the configured 15 options.");
                        break;
                    }

                    WheelOption option = new WheelOption();
                    option.effect = def.effect;
                    option.rarity = rarity;
                    state.wheel.Add(option);
                }
            }

            state.wheelGenerated = true;
            CycleStateStore.PersistImmediately();
            Plugin.Log.LogInfo($"Generated a new gacha wheel with {state.wheel.Count} option(s).");
        }

        public static int CountRemaining(string rarity)
        {
            CycleState state = CycleStateStore.State;
            int count = 0;
            for (int i = 0; i < state.wheel.Count; i++)
            {
                if (state.wheel[i].rarity == rarity)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<BlessingDef> BuildAvailable(List<BlessingDef> pool)
        {
            List<BlessingDef> available = new List<BlessingDef>();
            if (pool == null)
            {
                return available;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                if (IsSelectable(pool[i]))
                {
                    available.Add(pool[i]);
                }
            }

            return available;
        }

        private static BlessingDef PickAndRemove(List<BlessingDef> available)
        {
            if (available == null || available.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            for (int i = 0; i < available.Count; i++)
            {
                totalWeight += available[i].weight;
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = s_random.Next(totalWeight);
            for (int i = 0; i < available.Count; i++)
            {
                roll -= available[i].weight;
                if (roll < 0)
                {
                    BlessingDef def = available[i];
                    available.RemoveAt(i);
                    return def;
                }
            }

            return null;
        }

        private static BlessingDef Pick(List<BlessingDef> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                BlessingDef def = pool[i];
                if (IsSelectable(def))
                {
                    totalWeight += def.weight;
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
    }
}
