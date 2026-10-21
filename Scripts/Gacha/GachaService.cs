using System;
using System.Collections.Generic;

namespace Forwindz.CycleBlessings
{
    /// <summary>
    /// Owns the wheel: by default 1 Legendary, 2 Epic, 4 Rare, 8 Uncommon and 16 Common.
    /// Drawn options are removed; after at least one draw the player may regenerate the wheel.
    /// Each draw also charges a 10-point free-draw meter based on the drawn rarity.
    /// </summary>
    public static class GachaService
    {
        public const int MaxCharge = 10;

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

        public static bool CanDraw()
        {
            CycleState state = CycleStateStore.State;
            return state.wheel.Count > 0 && state.tickets > 0;
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
                if (state.wheel.Count == 0 || state.tickets <= 0)
                {
                    return null;
                }

                int index = s_random.Next(state.wheel.Count);
                WheelOption option = state.wheel[index];
                state.wheel.RemoveAt(index);
                state.tickets--;

                state.drawsSinceWheelGenerated++;
                state.totalDraws++;
                state.rewardIds.Add(option.rewardId);

                int gainedCharge = ModConfig.Current.GetCharge(option.rarity);
                state.drawCharge += gainedCharge;
                while (state.drawCharge >= MaxCharge)
                {
                    state.drawCharge -= MaxCharge;
                    state.tickets++;
                }

                CycleStateStore.PersistImmediately();
                Plugin.Log.LogInfo(
                    $"Gacha draw #{state.totalDraws}: {option.rarity} {option.rewardId} " +
                    $"(charge +{gainedCharge}, {state.drawCharge}/{MaxCharge}, tickets={state.tickets}).");
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
                List<RewardDef> pool = ModConfig.Current.GetPool(rarity);
                List<RewardDef> available = BuildAvailable(pool);

                for (int i = 0; i < count; i++)
                {
                    // Prefer unique entries inside one tier. Fall back to with-replacement
                    // only when the configured pool is smaller than the requested count.
                    RewardDef def = PickAndRemove(available);
                    if (def == null)
                    {
                        def = Pick(pool);
                    }

                    if (def == null)
                    {
                        Plugin.Log.LogWarning($"No usable {rarity} rewards; the wheel will have fewer than the configured options.");
                        break;
                    }

                    WheelOption option = new WheelOption();
                    option.rewardId = def.id;
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

        private static List<RewardDef> BuildAvailable(List<RewardDef> pool)
        {
            List<RewardDef> available = new List<RewardDef>();
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

        private static RewardDef PickAndRemove(List<RewardDef> available)
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
                    RewardDef def = available[i];
                    available.RemoveAt(i);
                    return def;
                }
            }

            return null;
        }

        private static RewardDef Pick(List<RewardDef> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                RewardDef def = pool[i];
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
                RewardDef def = pool[i];
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

        private static bool IsSelectable(RewardDef def)
        {
            if (def == null || def.weight <= 0)
            {
                return false;
            }

            return !string.IsNullOrEmpty(def.effect) ||
                   (def.goods != null && def.goods.Count > 0) ||
                   def.villagers > 0;
        }
    }
}
