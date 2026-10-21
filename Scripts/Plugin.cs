using System;
using ATS_API.Helpers;
using ATS_API.SaveLoading;
using BepInEx;
using Eremite;
using Eremite.Services;
using HarmonyLib;

namespace Forwindz.CycleBlessings
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "Forwindz.CycleBlessings";
        public const string PLUGIN_NAME = "Cycle Blessings";
        public const string PLUGIN_VERSION = "0.1.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("API", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static BepInEx.Logging.ManualLogSource Log { get; private set; }
        public static string PluginDirectory { get; private set; }

        private Harmony m_harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            PluginDirectory = System.IO.Path.GetDirectoryName(Info.Location);

            ModConfig.Load();
            CycleStateStore.Initialize();

            ModdedSaveManager.ListenForLoadedSaveData(PluginInfo.PLUGIN_GUID, CycleStateStore.OnSaveDataLoaded);
            ModdedSaveManager.ListenForResetCycleSaveData(PluginInfo.PLUGIN_GUID, CycleStateStore.OnCycleReset);
            EventBus.OnStartGame.AddListener(BlessingService.OnStartGame);
            EventBus.OnCycleEnded.AddListener(OnCycleEnded);

            PatchReputationService();
            PopupNotifier.Initialize();
            PopupNotifier.RegisterHotkey();

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded. Threshold={ModConfig.Current.threshold}");
        }

        private void Update()
        {
            BlessingService.Tick();
        }

        private void OnDestroy()
        {
            if (m_harmony != null)
            {
                m_harmony.UnpatchAll(PluginInfo.PLUGIN_GUID);
            }
        }

        private void PatchReputationService()
        {
            m_harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            try
            {
                var target = AccessTools.Method(
                    typeof(ReputationService),
                    nameof(ReputationService.AddReputationPoints),
                    new[] { typeof(float), typeof(ReputationChangeSource), typeof(string) });
                if (target == null)
                {
                    Log.LogError("Could not find ReputationService.AddReputationPoints. Reputation will not be tracked.");
                    return;
                }

                m_harmony.Patch(target, postfix: new HarmonyMethod(typeof(ReputationPatch), nameof(ReputationPatch.Postfix)));
                Log.LogInfo($"Patched {target.DeclaringType.FullName}.{target.Name}");
            }
            catch (Exception e)
            {
                Log.LogError("Failed to patch ReputationService.AddReputationPoints");
                Log.LogError(e);
            }
        }

        private static void OnCycleEnded()
        {
            Log.LogInfo("Cycle ended: cycle blessing counters were reset by ATS_API.");
        }
    }
}
