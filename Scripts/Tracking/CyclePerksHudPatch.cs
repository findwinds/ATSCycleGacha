using System;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// Runs right before the world map's cycle perk strip rebuilds its slots. Perk states are saved
    /// as model names and the strip resolves them with Settings.GetCycleEffect(), which throws for
    /// an unknown name, so the runtime perk models for drawn buffs must be in place by then.
    /// </summary>
    internal static class CyclePerksHudPatch
    {
        public static void Prefix()
        {
            try
            {
                RewardService.EnsureRegistered();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Failed to prepare the cycle perks for the world map HUD");
                Plugin.Log.LogError(e);
            }
        }
    }
}
