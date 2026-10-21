using System;

namespace Forwindz.CycleBlessings
{
    /// <summary>
    /// One draw ticket per completed settlement. The CurrentSettlement flag prevents the
    /// multiple end hooks (CheckForWin, ForceGameWin, GameLost) from granting duplicates.
    /// </summary>
    public static class TicketService
    {
        public static void OnSettlementWon()
        {
            Grant(true);
        }

        public static void OnSettlementLost()
        {
            Grant(false);
        }

        private static void Grant(bool won)
        {
            try
            {
                if (CycleStateStore.IsTicketGranted())
                {
                    return;
                }

                bool shouldGrant = won || ModConfig.Current.grantTicketOnLoss;
                CycleStateStore.GrantTicketIfNeeded(shouldGrant);

                if (!shouldGrant)
                {
                    Plugin.Log.LogInfo("Settlement lost; no ticket because grantTicketOnLoss is false.");
                    return;
                }

                Plugin.Log.LogInfo($"Settlement {(won ? "won" : "lost")}: +1 draw ticket (total {CycleStateStore.State.tickets}).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to grant a draw ticket for the settlement");
                Plugin.Log.LogError(e);
            }
        }
    }
}
