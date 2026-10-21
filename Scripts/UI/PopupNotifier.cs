using System;
using System.Collections.Generic;
using System.Linq;
using ATS_API;
using ATS_API.Localization;
using Eremite;
using Eremite.Model;
using UnityEngine;

namespace Forwindz.CycleBlessings
{
    public static class PopupNotifier
    {
        private static LocaText s_statusHeader;
        private static LocaText s_statusBody;
        private static LocaText s_announceHeader;
        private static LocaText s_announceBody;

        /// <summary>
        /// Register every localized string once, at plugin load. The popup itself can then be shown
        /// immediately without waiting for ATS_API's localization tick.
        /// </summary>
        public static void Initialize()
        {
            if (s_statusHeader != null)
            {
                return;
            }

            s_statusHeader = Text("status_header", "循环祝福 / Cycle Blessings", "Cycle Blessings / 循环祝福");
            s_statusBody = Text(
                "status_body",
                "指令：{0} / {1}\n满意度：{2} / {1}\n森林事件：{3} / {1}\n\n本循环祝福（{4}）：\n{5}",
                "Orders: {0} / {1}\nResolve: {2} / {1}\nGlade events: {3} / {1}\n\nBlessings this cycle ({4}):\n{5}");
            s_announceHeader = Text("announce_header", "循环祝福 / Cycle Blessing", "Cycle Blessing / 循环祝福");
            s_announceBody = Text(
                "announce_body",
                "你的{0}声望累计达到 {1}，获得祝福：\n{2}\n\n{3}",
                "Your {0} reputation reached {1}. Blessing earned:\n{2}\n\n{3}");
        }

        public static void RegisterHotkey()
        {
            string configuredKey = ModConfig.Current.statusHotkey;
            if (string.IsNullOrEmpty(configuredKey))
            {
                return;
            }

            KeyCode keyCode;
            if (!Enum.TryParse(configuredKey, true, out keyCode))
            {
                Plugin.Log.LogWarning($"Unknown statusHotkey '{configuredKey}' in cycle_blessings.json; status popup hotkey disabled.");
                return;
            }

            try
            {
                Hotkeys.New(
                    PluginInfo.PLUGIN_GUID,
                    "cycleBlessingsStatus",
                    "Cycle Blessings status / 循环祝福状态",
                    new List<KeyCode> { keyCode },
                    ShowStatus);
                Plugin.Log.LogInfo($"Registered status hotkey {keyCode}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not register the status hotkey: " + e.Message);
            }
        }

        public static void ShowStatus()
        {
            try
            {
                if (!GameMB.IsGameActive)
                {
                    return;
                }

                CycleState state = CycleStateStore.State;
                string blessings = FormatBlessings(state);
                string threshold = ModConfig.Current.threshold.ToString("0.#");

                Initialize();

                GenericPopupTask.Show(
                        PluginInfo.PLUGIN_GUID,
                        s_statusHeader,
                        s_statusBody,
                        state.ordersReputation.ToString("0.#"),
                        threshold,
                        state.resolveReputation.ToString("0.#"),
                        state.gladeReputation.ToString("0.#"),
                        state.blessingIds.Count.ToString(),
                        blessings)
                    .WaitForDecision(ContinueButton());
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not show cycle blessings status popup");
                Plugin.Log.LogError(e);
            }
        }

        public static void AnnounceMany(BlessingDimension dimension, List<BlessingDef> defs, float total)
        {
            if (!ModConfig.Current.announceBlessings || defs == null || defs.Count == 0)
            {
                return;
            }

            try
            {
                string names = string.Join("、", defs.Select(a => a.name).ToArray());
                string descriptions = string.Join("\n", defs
                    .Select(a => a.description)
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToArray());

                Initialize();

                GenericPopupTask.Show(
                        PluginInfo.PLUGIN_GUID,
                        s_announceHeader,
                        s_announceBody,
                        BlessingService.GetDisplayName(dimension),
                        total.ToString("0.#"),
                        names,
                        descriptions)
                    .WaitForDecision(ContinueButton());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not show the blessing popup: " + e.Message);
            }
        }

        private static string FormatBlessings(CycleState state)
        {
            if (state.blessingIds.Count == 0)
            {
                return "（无）/ (none)";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < state.blessingIds.Count; i++)
            {
                BlessingDef def = BlessingService.FindDef(state.blessingIds[i]);
                names.Add(def != null ? def.name : state.blessingIds[i] + " (missing)");
            }

            return string.Join("\n", names.ToArray());
        }

        private static GenericPopupTask.ButtonInfo ContinueButton()
        {
            return new GenericPopupTask.ButtonInfo
            {
                Key = Keys.Continue_Key.ToLocaText(),
                Type = GenericPopupTask.ButtonTypes.Normal
            };
        }

        private static LocaText Text(string name, string chinese, string english)
        {
            string key = PluginInfo.PLUGIN_GUID + "_" + name;
            LocalizationManager.AddString(key, chinese, SystemLanguage.ChineseSimplified);
            LocalizationManager.AddString(key, english, SystemLanguage.English);
            return key.ToLocaText();
        }
    }
}
