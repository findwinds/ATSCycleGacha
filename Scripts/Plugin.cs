using System;
using System.Collections.Generic;
using ATS_API;
using ATS_API.Helpers;
using ATS_API.SaveLoading;
using BepInEx;
using Eremite.Services;
using HarmonyLib;
using UnityEngine;

namespace Forwindz.CycleBlessings
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "Forwindz.CycleBlessings";
        public const string PLUGIN_NAME = "Cycle Blessings";
        public const string PLUGIN_VERSION = "0.5.0";
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
            EventBus.OnStartGame.AddListener(BlessingService.OnStartGameFallback);
            EventBus.OnCycleEnded.AddListener(OnCycleEnded);

            PatchGameMethods();
            RegisterGachaHotkey();

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded.");
        }

        private void OnGUI()
        {
            GachaWindow.Draw();
        }

        private void OnDestroy()
        {
            if (m_harmony != null)
            {
                m_harmony.UnpatchAll(PluginInfo.PLUGIN_GUID);
            }
        }

        private void RegisterGachaHotkey()
        {
            string configuredKey = ModConfig.Current.gachaHotkey;
            if (string.IsNullOrEmpty(configuredKey))
            {
                return;
            }

            KeyCode keyCode;
            if (!Enum.TryParse(configuredKey, true, out keyCode))
            {
                Log.LogWarning($"Unknown gachaHotkey '{configuredKey}' in cycle_gacha.json; the gacha window has no hotkey.");
                return;
            }

            try
            {
                Hotkeys.New(
                    PluginInfo.PLUGIN_GUID,
                    "cycleBlessingsGacha",
                    "Cycle Gacha / 循环抽奖",
                    new List<KeyCode> { keyCode },
                    GachaWindow.Toggle);
                Log.LogInfo($"Registered gacha hotkey {keyCode}.");
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not register the gacha hotkey: " + e.Message);
            }
        }

        private void PatchGameMethods()
        {
            m_harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            Patch(
                "EffectsService.AddLateInitialEffects",
                typeof(EffectsService),
                nameof(EffectsService.AddLateInitialEffects),
                null,
                typeof(EffectsPatch),
                nameof(EffectsPatch.Postfix));

            Patch(
                "ReputationService.CheckForWin",
                typeof(ReputationService),
                nameof(ReputationService.CheckForWin),
                null,
                typeof(SettlementEndPatch),
                nameof(SettlementEndPatch.CheckForWinPostfix));

            Patch(
                "ReputationService.ForceGameWin",
                typeof(ReputationService),
                nameof(ReputationService.ForceGameWin),
                null,
                typeof(SettlementEndPatch),
                nameof(SettlementEndPatch.ForceGameWinPostfix));

            Patch(
                "ReputationService.GameLost",
                typeof(ReputationService),
                nameof(ReputationService.GameLost),
                null,
                typeof(SettlementEndPatch),
                nameof(SettlementEndPatch.GameLostPostfix));
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
            Log.LogInfo("Cycle ended: tickets, the wheel and the cycle buff list were reset by ATS_API.");
        }
    }
}
