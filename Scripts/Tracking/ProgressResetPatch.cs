using Eremite.Services;
using HarmonyLib;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// Clears the gacha cycle state when the player wipes a save.
    ///
    /// Two paths exist and both are needed:
    ///  - the save list in the main menu ("游戏资料") goes ProfileSlot.ClearProfile ->
    ///    ProfilesService.ClearProfile -> RemoveProfileSaves -> RemoveAllProfileFiles, which deletes
    ///    the whole profile folder including this mod's .moddedsave;
    ///  - clearing progress elsewhere goes through MetaStateService.ResetProgress, which ends up in
    ///    the same ClearProfile call.
    ///
    /// Deleting the file is not enough on its own: ATS_API keeps the ModSaveData in memory and writes
    /// it back on the next save, so the tickets, charge and draw history reappeared. Resetting the
    /// in-memory state is what actually clears them.
    /// </summary>
    internal static class ProgressResetPatch
    {
        /// <summary>Removing a profile's saves from the main menu save list.</summary>
        public static void ProfileSavesRemoved()
        {
            Reset("profile");
        }

        /// <summary>MetaStateService.ResetProgress, which clears progress through ClearProfile.</summary>
        public static void Postfix()
        {
            Reset("progress");
        }

        private static void Reset(string source)
        {
            try
            {
                CycleStateStore.ResetForWipedSave();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Failed to clear the gacha state after a save wipe (" + source + ")");
                Plugin.Log.LogError(e);
            }
        }
    }
}
