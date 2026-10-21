using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Forwindz.CycleBlessings
{
    /// <summary>
    /// Small IMGUI overlay for the WorldMap scene. It does not depend on the game's UI
    /// hierarchy, so it is much less fragile than injecting a panel into the world map UI.
    /// </summary>
    public static class GachaWindow
    {
        private const int WindowId = 0x0C7B;
        private const int Columns = 5;

        private static bool s_show;
        private static Rect s_windowRect = new Rect(240, 80, 940, 700);
        private static Vector2 s_ownedScroll;
        private static string s_lastResult = "";
        private static bool s_errorLogged;

        private static GUIStyle s_label;
        private static GUIStyle s_title;
        private static GUIStyle s_card;
        private static GUIStyle s_rarity;
        private static GUIStyle s_cardLegendary;
        private static GUIStyle s_cardEpic;
        private static GUIStyle s_cardRare;
        private static GUIStyle s_cardUncommon;
        private static GUIStyle s_button;

        public static void Toggle()
        {
            if (!IsWorldMap())
            {
                return;
            }

            s_show = !s_show;
        }

        public static void Draw()
        {
            try
            {
                DrawInternal();
            }
            catch (Exception e)
            {
                if (!s_errorLogged)
                {
                    s_errorLogged = true;
                    Plugin.Log.LogError("Gacha window failed to draw");
                    Plugin.Log.LogError(e);
                }
            }
        }

        private static void DrawInternal()
        {
            if (!IsWorldMap())
            {
                s_show = false;
                return;
            }

            EnsureStyles();
            DrawTicketBadge();

            if (!s_show)
            {
                return;
            }

            GachaService.EnsureWheel();
            s_windowRect = GUI.Window(WindowId, s_windowRect, DrawWindow, "Cycle Gacha");
        }

        private static void DrawTicketBadge()
        {
            CycleState state = CycleStateStore.State;
            if (state.tickets <= 0 && !s_show)
            {
                return;
            }

            Rect rect = new Rect(24, 24, 280, 60);
            string label = "抽奖券 / Tickets: " + state.tickets + "\n按 " + ModConfig.Current.gachaHotkey + " 或点击打开 / Open";
            if (GUI.Button(rect, label, s_button))
            {
                s_show = true;
            }
        }

        private static void DrawWindow(int id)
        {
            CycleState state = CycleStateStore.State;
            ModConfig config = ModConfig.Current;

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Tickets / 抽奖券: {state.tickets}", s_title, GUILayout.Width(220));
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"L {GachaService.CountRemaining("Legendary")}/{config.wheel.legendary}   " +
                $"E {GachaService.CountRemaining("Epic")}/{config.wheel.epic}   " +
                $"R {GachaService.CountRemaining("Rare")}/{config.wheel.rare}   " +
                $"U {GachaService.CountRemaining("Uncommon")}/{config.wheel.uncommon}",
                s_label);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            for (int i = 0; i < state.wheel.Count; i++)
            {
                if (i % Columns == 0)
                {
                    GUILayout.BeginHorizontal();
                }

                DrawCard(state.wheel[i]);

                if (i % Columns == Columns - 1 || i == state.wheel.Count - 1)
                {
                    GUILayout.EndHorizontal();
                }
            }

            if (state.wheel.Count == 0)
            {
                string message = state.drawsSinceWheelGenerated > 0
                    ? "转盘已抽完。点击“重置转盘”生成新的一轮。 / Wheel empty. Reset to generate a new one."
                    : "转盘为空，请检查 cycle_gacha.json 的奖池配置。 / Wheel is empty; check the pools.";
                GUILayout.Label(message, s_label);
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            bool canDraw = state.tickets > 0 && state.wheel.Count > 0;
            GUI.enabled = canDraw;
            if (GUILayout.Button("抽取 / Draw", s_button, GUILayout.Height(38)))
            {
                WheelOption option = GachaService.Draw();
                s_lastResult = option == null
                    ? "没有抽奖券或转盘为空。 / No ticket or the wheel is empty."
                    : FormatResult(option);
            }

            GUI.enabled = GachaService.CanReset();
            if (GUILayout.Button("重置转盘 / Reset", s_button, GUILayout.Height(38)))
            {
                if (GachaService.ResetWheel())
                {
                    s_lastResult = "转盘已重置。 / Wheel reset.";
                }
            }

            GUI.enabled = true;
            if (GUILayout.Button("关闭 / Close", s_button, GUILayout.Height(38)))
            {
                s_show = false;
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(s_lastResult))
            {
                GUILayout.Space(6);
                GUILayout.Label(s_lastResult, s_label);
            }

            GUILayout.Space(8);
            GUILayout.Label($"已获得增益 / Owned buffs: {state.effectNames.Count}", s_title);
            s_ownedScroll = GUILayout.BeginScrollView(s_ownedScroll, GUILayout.Height(110));
            if (state.effectNames.Count == 0)
            {
                GUILayout.Label("（无）/ (none)", s_label);
            }
            else
            {
                Dictionary<string, int> counts = new Dictionary<string, int>();
                List<string> order = new List<string>();
                for (int i = 0; i < state.effectNames.Count; i++)
                {
                    string effect = state.effectNames[i];
                    if (!counts.ContainsKey(effect))
                    {
                        counts[effect] = 0;
                        order.Add(effect);
                    }

                    counts[effect]++;
                }

                for (int i = 0; i < order.Count; i++)
                {
                    GUILayout.Label($"{counts[order[i]]}x  {BlessingService.GetDisplayName(order[i])}", s_label);
                }
            }

            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        private static void DrawCard(WheelOption option)
        {
            GUILayout.BeginVertical(CardStyle(option.rarity), GUILayout.Width(176), GUILayout.Height(104));
            GUILayout.Label(RarityLabel(option.rarity), s_rarity);
            GUILayout.Label(BlessingService.GetDisplayName(option.effect), s_card);
            GUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private static string RarityLabel(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return "传说 Legendary";
                case "Epic": return "史诗 Epic";
                case "Rare": return "稀有 Rare";
                default: return "罕见 Uncommon";
            }
        }

        private static GUIStyle CardStyle(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return s_cardLegendary;
                case "Epic": return s_cardEpic;
                case "Rare": return s_cardRare;
                default: return s_cardUncommon;
            }
        }

        private static GUIStyle CreateCardStyle(Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = MakeTexture(color);
            style.padding = new RectOffset(6, 6, 4, 4);
            return style;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static string FormatResult(WheelOption option)
        {
            string name = BlessingService.GetDisplayName(option.effect);
            string description = BlessingService.GetDescription(option.effect);
            if (description.Length > 300)
            {
                description = description.Substring(0, 300) + "...";
            }

            return string.IsNullOrEmpty(description)
                ? $"获得 / Won: [{option.rarity}] {name}"
                : $"获得 / Won: [{option.rarity}] {name}\n{description}";
        }

        private static bool IsWorldMap()
        {
            return SceneManager.GetActiveScene().name == "WorldMap";
        }

        private static void EnsureStyles()
        {
            if (s_label != null)
            {
                return;
            }

            Font font = LoadFont();
            s_label = new GUIStyle(GUI.skin.label);
            s_label.font = font;
            s_label.fontSize = 14;
            s_label.wordWrap = true;

            s_title = new GUIStyle(s_label);
            s_title.fontSize = 17;
            s_title.fontStyle = FontStyle.Bold;

            s_card = new GUIStyle(s_label);
            s_card.fontSize = 13;
            s_card.alignment = TextAnchor.UpperLeft;
            s_card.normal.textColor = Color.white;

            s_rarity = new GUIStyle(s_label);
            s_rarity.fontSize = 12;
            s_rarity.fontStyle = FontStyle.Bold;
            s_rarity.normal.textColor = Color.white;

            s_cardLegendary = CreateCardStyle(new Color(0.55f, 0.30f, 0.05f, 1f));
            s_cardEpic = CreateCardStyle(new Color(0.40f, 0.20f, 0.55f, 1f));
            s_cardRare = CreateCardStyle(new Color(0.15f, 0.30f, 0.55f, 1f));
            s_cardUncommon = CreateCardStyle(new Color(0.18f, 0.40f, 0.20f, 1f));

            s_button = new GUIStyle(GUI.skin.button);
            s_button.font = font;
            s_button.fontSize = 14;
        }

        private static Font LoadFont()
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei", "SimHei", "Arial Unicode MS", "Arial" }, 14);
                if (font != null)
                {
                    return font;
                }
            }
            catch
            {
                // Fall through to the default IMGUI font.
            }

            return GUI.skin.font;
        }
    }
}
