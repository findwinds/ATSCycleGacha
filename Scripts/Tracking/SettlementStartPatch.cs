using System;
using Eremite.Services;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// EffectsService.AddLateInitialEffects is where the game applies world-event rewards
    /// (WorldStateService.Cycle.activeEventsEffects) at settlement start. Applying our buffs
    /// in the postfix gives them the same timing and semantics.
    /// </summary>
    internal static class SettlementStartPatch
    {
        public static void Postfix()
        {
            try
            {
                RewardService.ApplyCycleRewardsAtSettlementStart();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to apply cycle buffs at settlement start");
                Plugin.Log.LogError(e);
            }
        }
    }
}
