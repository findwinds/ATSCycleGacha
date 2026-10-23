using Eremite.Services;
using HarmonyLib;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// Clears the gacha cycle state when the player wipes their save.
    ///
    /// ATS_API only resets mod cycle data when the world calendar ends a cycle, and only resets
    /// settlement data on a new game. Wiping the save goes through MetaStateService.ResetProgress
    /// instead, which is not tied to a cycle end - in the Queen's Hand trial the world is kept and
    /// the cycle never formally ends, so charge and draw history survived the wipe.
    /// </summary>
    internal static class ProgressResetPatch
    {
        public static void Postfix()
        {
            try
            {
                CycleStateStore.ResetForWipedSave();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Failed to clear the gacha state after the save was wiped");
                Plugin.Log.LogError(e);
            }
        }
    }
}
