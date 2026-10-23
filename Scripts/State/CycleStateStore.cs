using System;
using ATS_API.SaveLoading;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// Wraps ModSaveData.CurrentCycle. ATS_API replaces CurrentCycle with a fresh SaveData on
    /// WorldCalendarService.OnCycleEnded, which resets tickets, the wheel and all owned buffs.
    /// </summary>
    public static class CycleStateStore
    {
        private const string SaveKey = "CycleGacha_State_v1";
        private const string SettlementAppliedKey = "CycleGacha_SettlementApplied_v1";
        private const string SettlementTicketKey = "CycleGacha_TicketGranted_v1";

        private static ModSaveData s_saveData;
        private static bool s_grantedStartingTickets;
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
            Plugin.Log.LogInfo($"Cycle gacha save data loaded ({fileState}): {s_state.Describe()}");

            if (s_grantedStartingTickets)
            {
                // Persist the "already granted" flag right away, otherwise quitting before the next
                // save would hand the starting tickets out again on the following load.
                s_grantedStartingTickets = false;
                PersistImmediately();
            }

            // Restore rewards drawn by a build that applied them by hand instead of registering
            // them with the game's cycle bonuses. No-ops until the world state is loaded.
            RewardService.EnsureRegistered();
        }

        /// <summary>
        /// A fresh cycle starts with the configured number of draw tickets, so the board can be
        /// played before the first settlement is won.
        /// </summary>
        private static CycleState NewState()
        {
            CycleState state = new CycleState();
            int starting = ModConfig.Current != null ? ModConfig.Current.startingTickets : 0;
            if (starting > 0)
            {
                state.tickets = starting;
            }

            // The starting tickets are part of this state already, so the migration that tops up
            // older saves must not add them a second time.
            state.startingTicketsGranted = true;
            return state;
        }

        /// <summary>
        /// Wipes the cycle board after the player clears their save. The game keeps the world map in
        /// some modes (the Queen's Hand trial), so ATS_API's cycle-ended and new-game hooks never fire
        /// and the stale charge, tickets and draw history would otherwise be carried over.
        /// </summary>
        public static void ResetForWipedSave()
        {
            s_state = NewState();

            if (s_saveData != null)
            {
                s_saveData.CurrentCycle.SetValue(SaveKey, s_state);
            }

            PersistImmediately();
            Plugin.Log.LogInfo("The save was wiped, so the cycle gacha state was cleared: " +
                               s_state.Describe() + ".");
        }

        public static void OnCycleReset(ModSaveData data)
        {
            s_saveData = data;
            s_state = NewState();
            data.CurrentCycle.SetValue(SaveKey, s_state);
            Plugin.Log.LogInfo("Cycle gacha state reset for the new cycle (" + s_state.tickets + " ticket(s)).");

            try
            {
                ModdedSaveManager.SaveModdedData(PluginInfo.PLUGIN_GUID);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not save the cleared cycle state immediately: " + e.Message);
            }
        }

        /// <summary>
        /// Hands out the cycle's starting tickets. A save written by a build that predates them gets
        /// the bonus once, so the board is playable without waiting for a settlement win.
        /// </summary>
        private static void GrantStartingTicketsOnce(CycleState state)
        {
            if (state.startingTicketsGranted)
            {
                return;
            }

            state.startingTicketsGranted = true;

            int starting = ModConfig.Current != null ? ModConfig.Current.startingTickets : 0;
            if (starting > 0)
            {
                state.tickets += starting;
                Plugin.Log.LogInfo("Granted " + starting + " starting ticket(s) for this cycle.");
            }

            // Write the flag out right away, otherwise quitting before the next save would hand the
            // tickets out again on the following load.
            s_grantedStartingTickets = true;
        }

        public static void Persist()
        {
            if (s_saveData == null)
            {
                return;
            }

            s_saveData.CurrentCycle.SetValue(SaveKey, s_state);
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
                Plugin.Log.LogWarning("Could not save cycle gacha immediately: " + e.Message);
            }
        }

        /// <summary>
        /// Marks the settlement as ticket-resolved and optionally adds the ticket in the same
        /// save, so a crash cannot mark the settlement without awarding the ticket.
        /// </summary>
        public static bool GrantTicketIfNeeded(bool grant)
        {
            if (s_saveData == null)
            {
                Plugin.Log.LogWarning("Cannot grant a draw ticket because the mod save data is not loaded.");
                return false;
            }

            if (GetSettlementFlag(SettlementTicketKey))
            {
                return false;
            }

            s_saveData.CurrentSettlement.SetValue(SettlementTicketKey, true);
            if (grant)
            {
                State.tickets += ModConfig.Current.ticketsPerSettlement;
            }

            PersistImmediately();
            return grant;
        }

        /// <summary>
        /// CurrentSettlement is cleared by ATS_API when a new settlement starts, so this flag
        /// naturally resets for every new settlement and protects against double application.
        /// </summary>
        public static bool IsSettlementApplied()
        {
            return GetSettlementFlag(SettlementAppliedKey);
        }

        public static void MarkSettlementApplied()
        {
            SetSettlementFlag(SettlementAppliedKey);
        }

        /// <summary>
        /// Set once per settlement when the game reports a win or loss, so multiple end hooks
        /// cannot award more than one ticket for the same settlement.
        /// </summary>
        public static bool IsTicketGranted()
        {
            return GetSettlementFlag(SettlementTicketKey);
        }

        private static bool GetSettlementFlag(string key)
        {
            if (s_saveData == null)
            {
                return false;
            }

            try
            {
                return s_saveData.CurrentSettlement.GetValueAsObject(key, false);
            }
            catch
            {
                return false;
            }
        }

        private static void SetSettlementFlag(string key)
        {
            if (s_saveData == null)
            {
                return;
            }

            s_saveData.CurrentSettlement.SetValue(key, true);
            try
            {
                ModdedSaveManager.SaveModdedData(PluginInfo.PLUGIN_GUID);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not save settlement flag '{key}' immediately: " + e.Message);
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
                Plugin.Log.LogInfo($"Cycle gacha state initialized: {s_state.Describe()}");
            }
            catch (Exception e)
            {
                // Happens before ATS_API finished loading save data (for example during the main menu).
                s_state = NewState();
                Plugin.Log.LogDebug("Cycle gacha state is not available yet: " + e.Message);
            }
        }

        private static CycleState LoadState(ModSaveData data)
        {
            CycleState state = null;
            try
            {
                state = data.CurrentCycle.GetValueAsObject(SaveKey, NewState());
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to read cycle gacha from the save, starting a new state.");
                Plugin.Log.LogError(e);
            }

            if (state == null)
            {
                state = NewState();
            }

            state.Sanitize();
            GrantStartingTicketsOnce(state);
            return state;
        }
    }
}
