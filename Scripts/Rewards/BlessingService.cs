using System;
using System.Collections.Generic;
using System.Linq;
using ATS_API.Helpers;
using Eremite;
using Eremite.Model;
using Eremite.Services;
using UnityEngine;

namespace Forwindz.CycleBlessings
{
    public static class BlessingService
    {
        private static readonly System.Random s_random = new System.Random(Environment.TickCount);
        private static readonly HashSet<string> s_loggedUnknownSources = new HashSet<string>();

        private static bool s_pendingSettlementApply;
        private static bool s_applyDeadlineStarted;
        private static float s_applyDeadline;

        public static void OnReputationGained(float amount, ReputationChangeSource type, string reason)
        {
            string sourceName = type.ToString();
            BlessingDimension? dimension = ModConfig.Current.Classify(sourceName, reason);
            if (!dimension.HasValue)
            {
                LogUnknown(sourceName, reason);
                return;
            }

            CycleState state = CycleStateStore.State;
            float total = state.AddReputation(dimension.Value, amount);
            CycleStateStore.Persist();

            float threshold = ModConfig.Current.threshold;
            int reachedMilestones = Mathf.FloorToInt((total + 0.0001f) / threshold);
            int granted = state.GetBlessingCount(dimension.Value);
            if (reachedMilestones <= granted)
            {
                return;
            }

            List<BlessingDef> earned = new List<BlessingDef>();
            while (granted < reachedMilestones)
            {
                BlessingDef def = Roll(dimension.Value);
                if (def == null)
                {
                    Plugin.Log.LogWarning($"The {dimension.Value} blessing pool is empty; the milestone will be retried on the next reputation gain.");
                    break;
                }

                state.IncrementBlessing(dimension.Value);
                state.blessingIds.Add(def.id);
                earned.Add(def);
                granted++;

                if (ModConfig.Current.grantImmediatelyOnEarn)
                {
                    TryGrantNow(def);
                }
            }

            if (earned.Count == 0)
            {
                return;
            }

            CycleStateStore.PersistImmediately();
            PopupNotifier.AnnounceMany(dimension.Value, earned, total);

            string ids = string.Join(", ", earned.Select(a => a.id).ToArray());
            Plugin.Log.LogInfo($"Earned {earned.Count} {dimension.Value} blessing(s) at {total:0.##} reputation: {ids}");
        }

        public static void OnStartGame(bool isNewGame)
        {
            if (!isNewGame || !ModConfig.Current.applyBlessingsAtSettlementStart)
            {
                return;
            }

            if (CycleStateStore.IsSettlementApplied())
            {
                Plugin.Log.LogWarning("Cycle blessings were already applied to this settlement; skipping a duplicate application.");
                return;
            }

            s_pendingSettlementApply = true;
            s_applyDeadlineStarted = false;
            Plugin.Log.LogInfo("New settlement detected; cycle blessings will be applied once the settlement services are ready.");
        }

        public static void Tick()
        {
            if (!s_pendingSettlementApply)
            {
                return;
            }

            try
            {
                if (!GameMB.IsGameActive)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            if (!s_applyDeadlineStarted)
            {
                s_applyDeadlineStarted = true;
                s_applyDeadline = Time.realtimeSinceStartup + 30f;
            }

            if (Time.realtimeSinceStartup > s_applyDeadline)
            {
                Plugin.Log.LogWarning("Gave up waiting for settlement services; cycle blessings were not applied to this settlement.");
                s_pendingSettlementApply = false;
                return;
            }

            try
            {
                if (!ServicesReady(AnyBlessingNeedsVillagers()))
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            ApplyAllAtSettlementStart();
            CycleStateStore.MarkSettlementApplied();
            s_pendingSettlementApply = false;
        }

        public static BlessingDef FindDef(string id)
        {
            BlessingDef result;
            if (TryFindDef(ModConfig.Current.pools.orders, id, out result)) return result;
            if (TryFindDef(ModConfig.Current.pools.resolve, id, out result)) return result;
            if (TryFindDef(ModConfig.Current.pools.glade, id, out result)) return result;
            return null;
        }

        public static string GetDisplayName(BlessingDimension dimension)
        {
            switch (dimension)
            {
                case BlessingDimension.Orders: return "指令 / Orders";
                case BlessingDimension.Resolve: return "满意度 / Resolve";
                case BlessingDimension.Glade: return "森林事件 / Glade Events";
                default: return dimension.ToString();
            }
        }

        private static void ApplyAllAtSettlementStart()
        {
            CycleState state = CycleStateStore.State;
            List<string> ids = new List<string>(state.blessingIds);
            int applied = 0;

            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                BlessingDef def = FindDef(id);
                if (def == null)
                {
                    Plugin.Log.LogWarning($"Blessing '{id}' is saved in the cycle but missing from cycle_blessings.json; skipping it.");
                    continue;
                }

                ApplyBlessing(def, false);
                applied++;
            }

            Plugin.Log.LogInfo($"Applied {applied}/{ids.Count} cycle blessing(s) to the new settlement.");
        }

        private static bool TryGrantNow(BlessingDef def)
        {
            try
            {
                if (!GameMB.IsGameActive || !ServicesReady(def.villagers > 0))
                {
                    return false;
                }

                ApplyBlessing(def, true);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not grant blessing '{def.id}' immediately; it will still apply to future settlements.");
                Plugin.Log.LogWarning(e.Message);
                return false;
            }
        }

        private static void ApplyBlessing(BlessingDef def, bool immediate)
        {
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
                        Plugin.Log.LogWarning($"Unknown good '{good.good}' in blessing '{def.id}'; skipping it.");
                        continue;
                    }

                    Serviceable.StorageService.Main.Goods.Add(goodName, good.amount);

                    if (immediate)
                    {
                        Plugin.Log.LogInfo($"Granted +{good.amount} {goodName} from blessing '{def.id}'.");
                    }
                }
            }

            if (def.villagers > 0)
            {
                ApplyVillagers(def, immediate);
            }
        }

        private static void ApplyVillagers(BlessingDef def, bool immediate)
        {
            if (Serviceable.VillagersService == null)
            {
                Plugin.Log.LogWarning($"Villagers service is not ready; blessing '{def.id}' could not add villagers.");
                return;
            }

            var races = Serviceable.VillagersService.Races;
            if (races == null || races.Count == 0)
            {
                Plugin.Log.LogWarning($"No races are available; blessing '{def.id}' could not add villagers.");
                return;
            }

            List<string> raceNames = new List<string>(races.Keys);
            for (int i = 0; i < def.villagers; i++)
            {
                string raceName = string.IsNullOrEmpty(def.race)
                    ? raceNames[s_random.Next(raceNames.Count)]
                    : def.race;

                RaceModel race = raceName.ToRaceModel();
                if (race == null)
                {
                    Plugin.Log.LogWarning($"Unknown race '{raceName}' in blessing '{def.id}'; stopping villager grant.");
                    return;
                }

                SO.VillagersService.SpawnNewVillager(race);

                if (immediate)
                {
                    Plugin.Log.LogInfo($"Spawned 1 {raceName} from blessing '{def.id}'.");
                }
            }
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
            return model != null ? model.name : null;
        }

        private static bool AnyBlessingNeedsVillagers()
        {
            CycleState state = CycleStateStore.State;
            for (int i = 0; i < state.blessingIds.Count; i++)
            {
                BlessingDef def = FindDef(state.blessingIds[i]);
                if (def != null && def.villagers > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ServicesReady(bool needVillagers)
        {
            if (Serviceable.StorageService == null ||
                Serviceable.StorageService.Main == null ||
                Serviceable.StorageService.Main.Goods == null)
            {
                return false;
            }

            if (needVillagers)
            {
                if (Serviceable.VillagersService == null || Serviceable.VillagersService.Races == null || Serviceable.VillagersService.Races.Count == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static BlessingDef Roll(BlessingDimension dimension)
        {
            List<BlessingDef> pool = ModConfig.Current.GetPool(dimension);
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                totalWeight += Math.Max(0, pool[i].weight);
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = s_random.Next(totalWeight);
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= Math.Max(0, pool[i].weight);
                if (roll < 0)
                {
                    return pool[i];
                }
            }

            return pool[pool.Count - 1];
        }

        private static bool TryFindDef(List<BlessingDef> pool, string id, out BlessingDef result)
        {
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].id == id)
                    {
                        result = pool[i];
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        private static void LogUnknown(string source, string reason)
        {
            if (!ModConfig.Current.logUnknownReputationSources)
            {
                return;
            }

            string key = source + "|" + reason;
            if (!s_loggedUnknownSources.Add(key))
            {
                return;
            }

            Plugin.Log.LogWarning(
                $"Unclassified reputation source '{source}' (reason='{reason}'). " +
                "Add a matching token under reputationSources in cycle_blessings.json if this source should count.");
        }
    }
}
