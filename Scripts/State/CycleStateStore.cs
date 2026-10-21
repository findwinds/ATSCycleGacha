using System;
using ATS_API.SaveLoading;

namespace Forwindz.CycleBlessings
{
    /// <summary>
    /// Wraps ModSaveData.CurrentCycle. ATS_API replaces CurrentCycle with a fresh SaveData on
    /// WorldCalendarService.OnCycleEnded, which is exactly the reset point this mod needs.
    /// </summary>
    public static class CycleStateStore
    {
        private const string SaveKey = "CycleBlessings_State_v2";
        private const string SettlementAppliedKey = "CycleBlessings_Applied_v1";

        private static ModSaveData s_saveData;
        private static CycleState s_state;

        public static CycleState State
        {
            get
            {
                EnsureLoaded();
                return s_state;
            }
        }

        public static void Initialize()
        {
            EnsureLoaded();
        }

        public static void OnSaveDataLoaded(ModSaveData data, SaveFileState fileState)
        {
            s_saveData = data;
            s_state = LoadState(data);
            Plugin.Log.LogInfo($"Cycle blessings save data loaded ({fileState}): {s_state.Describe()}");
        }

        public static void OnCycleReset(ModSaveData data)
        {
            s_saveData = data;
            s_state = new CycleState();
            data.CurrentCycle.SetValue(SaveKey, s_state);
            Plugin.Log.LogInfo("Cycle blessings state reset for the new cycle.");

            try
            {
                ModdedSaveManager.SaveModdedData(PluginInfo.PLUGIN_GUID);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not save the cleared cycle state immediately: " + e.Message);
            }
        }

        public static void Persist()
        {
            if (s_saveData == null)
            {
                return;
            }

            s_saveData.CurrentCycle.SetValue(SaveKey, s_state);
        }

        /// <summary>
        /// CurrentSettlement is cleared by ATS_API when a new settlement starts, so this flag
        /// naturally resets for every new settlement and protects against double application.
        /// </summary>
        public static bool IsSettlementApplied()
        {
            if (s_saveData == null)
            {
                return false;
            }

            try
            {
                return s_saveData.CurrentSettlement.GetValueAsObject(SettlementAppliedKey, false);
            }
            catch
            {
                return false;
            }
        }

        public static void MarkSettlementApplied()
        {
            if (s_saveData == null)
            {
                return;
            }

            s_saveData.CurrentSettlement.SetValue(SettlementAppliedKey, true);
            try
            {
                ModdedSaveManager.SaveModdedData(PluginInfo.PLUGIN_GUID);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not save the settlement-applied flag immediately: " + e.Message);
            }
        }

        public static void PersistImmediately()
        {
            Persist();

            try
            {
                ModdedSaveManager.SaveModdedData(PluginInfo.PLUGIN_GUID);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not save cycle blessings immediately: " + e.Message);
            }
        }

        private static void EnsureLoaded()
        {
            if (s_state != null)
            {
                return;
            }

            try
            {
                s_saveData = ModdedSaveManager.GetSaveData(PluginInfo.PLUGIN_GUID);
                s_state = LoadState(s_saveData);
                Plugin.Log.LogInfo($"Cycle blessings state initialized: {s_state.Describe()}");
            }
            catch (Exception e)
            {
                // Happens before ATS_API finished loading save data (for example during the main menu).
                s_state = new CycleState();
                Plugin.Log.LogDebug("Cycle blessings state is not available yet: " + e.Message);
            }
        }

        private static CycleState LoadState(ModSaveData data)
        {
            CycleState state = null;
            try
            {
                state = data.CurrentCycle.GetValueAsObject(SaveKey, new CycleState());
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to read cycle blessings from the save, starting a new state.");
                Plugin.Log.LogError(e);
            }

            if (state == null)
            {
                state = new CycleState();
            }

            state.Sanitize();
            return state;
        }
    }
}
