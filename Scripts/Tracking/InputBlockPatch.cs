using Eremite.Services;
using HarmonyLib;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// Stops clicks on the gacha window from reaching the game underneath it.
    ///
    /// The window is drawn with IMGUI, which Unity's EventSystem knows nothing about, so
    /// InputService.IsOverUI() - the gate every world click goes through
    /// (WorldMapPointer.ReadClick -> InputService.IsWorldClicked -> IsOverUI) - reported "not over
    /// UI" and a click on the draw button also hit the world map, jumping to another screen.
    /// Reporting "over UI" while the cursor sits on the window fixes every consumer at once.
    /// </summary>
    internal static class InputBlockPatch
    {
        public static bool Prefix(ref bool __result)
        {
            if (!GachaWindow.IsPointerInsideWindow())
            {
                return true;
            }

            __result = true;
            return false;
        }
    }
}
