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
        public static void Postfix(object[] __args)
        {
            float amount = 0f;
            string reason = null;
            ReputationChangeSource type = default(ReputationChangeSource);
            bool foundSource = false;

            if (__args == null)
            {
                return;
            }

            // Read by type instead of by parameter name so a game update renaming the
            // parameters does not silently break tracking.
            for (int i = 0; i < __args.Length; i++)
            {
                object arg = __args[i];
                if (arg is float)
                {
                    amount = (float)arg;
                }
                else if (arg is int)
                {
                    amount = (int)arg;
                }
                else if (arg is double)
                {
                    amount = (float)(double)arg;
                }
                else if (arg is ReputationChangeSource)
                {
                    type = (ReputationChangeSource)arg;
                    foundSource = true;
                }
                else if (arg is string)
                {
                    reason = (string)arg;
                }
            }

            if (!foundSource || amount <= 0f)
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
