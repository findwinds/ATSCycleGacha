using System;
using Eremite.Services;

namespace Forwindz.CycleBlessings
{
    /// <summary>
    /// The game marks a settlement win through ReputationService.CheckForWin (natural win)
    /// and ForceGameWin (debug/final flow); a loss goes through GameLost.
    /// </summary>
    internal static class SettlementEndPatch
    {
        public static void CheckForWinPostfix(ReputationService __instance)
        {
            try
            {
                if (__instance.State.finished ||
                    __instance.State.reputation >= __instance.GetReputationToWin())
                {
                    TicketService.OnSettlementWon();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to check for a settlement win while granting a ticket");
                Plugin.Log.LogError(e);
            }
        }

        public static void ForceGameWinPostfix()
        {
            TicketService.OnSettlementWon();
        }

        public static void GameLostPostfix()
        {
            TicketService.OnSettlementLost();
        }
    }
}
