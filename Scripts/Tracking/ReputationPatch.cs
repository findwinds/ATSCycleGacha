using System;
using Eremite;
using Eremite.Services;

namespace Forwindz.CycleBlessings
{
    /// <summary>
    /// ReputationService.AddReputationPoints is the single funnel the game uses for every
    /// reputation source (orders, resolve, glade events, cornerstones, ...). Reading the source
    /// argument lets us attribute gains without touching the settlement's reputation itself.
    /// </summary>
    internal static class ReputationPatch
    {
        public static void Postfix(float amount, ReputationChangeSource type, string reason)
        {
            if (amount <= 0f)
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
                // GameMB is not ready yet; a reputation gain cannot happen before the game starts anyway.
                return;
            }

            try
            {
                BlessingService.OnReputationGained(amount, type, reason);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to track reputation gain");
                Plugin.Log.LogError(e);
            }
        }
    }
}
