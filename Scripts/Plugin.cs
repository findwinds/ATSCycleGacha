using System;
using System.Collections.Generic;
using ATS_API;
using ATS_API.Helpers;
using ATS_API.SaveLoading;
using BepInEx;
using Eremite.Services;
using HarmonyLib;
using UnityEngine;

namespace Forwindz.CycleGacha
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "Forwindz.CycleGacha";
        public const string PLUGIN_NAME = "Cycle Gacha";
        public const string PLUGIN_VERSION = "0.13.0";
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
            RegisterGachaHotkey();

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded.");
        }

        private void OnGUI()
        {
            GachaWindow.Draw();
        }

        private bool m_gameDataPrepared;

        private void Update()
        {
            GachaWindow.Tick();

            if (m_gameDataPrepared)
            {
                return;
            }

            // Once the game's settings are readable: report what this build resolves, and give the
            // reward effects their texts. The settlement effect strip shows those texts even when the
            // session loads straight into a settlement and never visits the world map.
            if (RewardService.TryLogDataInventory())
            {
                RewardService.ApplyAllEffectTexts();
                m_gameDataPrepared = true;
            }
        }

        private void OnDestroy()
        {
            if (m_harmony != null)
            {
                m_harmony.UnpatchSelf();
            }
        }

        /// <summary>
        /// Rewards are not applied by the mod at all: each draw is registered with the game's cycle
        /// bonus state (SO.CycleEffectsService), which the game then folds into the next caravan and
        /// applies itself at settlement start. See RewardService for the full chain.
        /// </summary>
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
                    "cycleGacha",
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

            // The world map perk strip stores perk names and resolves them through
            // Settings.GetCycleEffect() while it builds its slots. Runtime perk models for drawn
            // buffs therefore have to exist before that, otherwise the lookup throws.
            Patch(
                "CyclePerksHUD.SetUp",
                typeof(Eremite.WorldMap.UI.Perks.CyclePerksHUD),
                "SetUp",
                null,
                typeof(CyclePerksHudPatch),
                nameof(CyclePerksHudPatch.Prefix),
                true);

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

        private void Patch(string label, Type type, string methodName, Type[] parameters, Type patchType, string postfixName, bool asPrefix = false)
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

                if (asPrefix)
                {
                    m_harmony.Patch(target, prefix: new HarmonyMethod(patchType, postfixName));
                }
                else
                {
                    m_harmony.Patch(target, postfix: new HarmonyMethod(patchType, postfixName));
                }

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
