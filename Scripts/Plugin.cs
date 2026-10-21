using System;
using ATS_API.Helpers;
using ATS_API.SaveLoading;
using BepInEx;
using Eremite.Services;
using HarmonyLib;

namespace Forwindz.CycleBlessings
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "Forwindz.CycleBlessings";
        public const string PLUGIN_NAME = "Cycle Blessings";
        public const string PLUGIN_VERSION = "0.2.0";
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
            EventBus.OnCycleEnded.AddListener(OnCycleEnded);

            PatchGameMethods();
            PopupNotifier.Initialize();
            PopupNotifier.RegisterHotkey();

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded. Threshold={ModConfig.Current.threshold}");
        }

        private void OnDestroy()
        {
            if (m_harmony != null)
            {
                m_harmony.UnpatchAll(PluginInfo.PLUGIN_GUID);
            }
        }

        private void PatchGameMethods()
        {
            m_harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            Patch(
                "ReputationService.AddReputationPoints",
                typeof(ReputationService),
                nameof(ReputationService.AddReputationPoints),
                new[] { typeof(float), typeof(ReputationChangeSource), typeof(string) },
                typeof(ReputationPatch),
                nameof(ReputationPatch.Postfix));

            Patch(
                "EffectsService.AddLateInitialEffects",
                typeof(EffectsService),
                nameof(EffectsService.AddLateInitialEffects),
                null,
                typeof(EffectsPatch),
                nameof(EffectsPatch.Postfix));
        }

        private void Patch(string label, Type type, string methodName, Type[] parameters, Type patchType, string postfixName)
        {
            try
            {
                var target = parameters != null
                    ? AccessTools.Method(type, methodName, parameters)
                    : AccessTools.Method(type, methodName, Type.EmptyTypes);

                if (target == null && parameters == null)
                {
                    target = AccessTools.Method(type, methodName);
                }

                if (target == null)
                {
                    Log.LogError($"Could not find {label}. The related feature will not work.");
                    return;
                }

                m_harmony.Patch(target, postfix: new HarmonyMethod(patchType, postfixName));
                Log.LogInfo($"Patched {target.DeclaringType.FullName}.{target.Name}");
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to patch {label}");
                Log.LogError(e);
            }
        }

        private static void OnCycleEnded()
        {
            Log.LogInfo("Cycle ended: cycle buff counters and buff list were reset by ATS_API.");
        }
    }
}
