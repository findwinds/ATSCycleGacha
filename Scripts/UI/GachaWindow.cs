using System;
using System.Collections.Generic;
using ATS_API.Helpers;
using Eremite;
using Eremite.Model;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Forwindz.CycleGacha
{
    /// <summary>
    /// The gacha window, drawn in the game's own visual language: a dark leather panel with a thin
    /// gold rule, parchment-toned text, the game's reward colours for the rarity accents and the
    /// real item icons (GoodModel.icon / EffectModel.GetIcon), instead of the default IMGUI skin.
    /// </summary>
    public static class GachaWindow
    {
        private const int WindowId = 0x0C7B;
        private const int Columns = 5;

        // ---- palette (matches the game's parchment / gold UI) ----
        private static readonly Color ColPanelFill = new Color(0.055f, 0.043f, 0.031f, 0.97f);
        private static readonly Color ColPanelEdge = new Color(0.72f, 0.60f, 0.34f, 1f);
        private static readonly Color ColPanelInner = new Color(0.25f, 0.21f, 0.14f, 1f);
        private static readonly Color ColCardFill = new Color(0.10f, 0.083f, 0.061f, 1f);
        private static readonly Color ColCardInner = new Color(0.16f, 0.13f, 0.09f, 1f);
        private static readonly Color ColText = new Color(0.91f, 0.87f, 0.76f, 1f);
        private static readonly Color ColTextGold = new Color(0.85f, 0.71f, 0.41f, 1f);
        private static readonly Color ColTextDim = new Color(0.58f, 0.54f, 0.46f, 1f);
        private static readonly Color ColButtonFill = new Color(0.13f, 0.105f, 0.075f, 1f);
        private static readonly Color ColButtonHover = new Color(0.20f, 0.16f, 0.11f, 1f);
        private static readonly Color ColButtonActive = new Color(0.26f, 0.20f, 0.12f, 1f);

        private static readonly Color[] RarityColors =
        {
            new Color(0.84f, 0.55f, 0.18f), // Legendary
            new Color(0.62f, 0.40f, 0.80f), // Epic
            new Color(0.36f, 0.58f, 0.85f), // Rare
            new Color(0.40f, 0.70f, 0.44f), // Uncommon
            new Color(0.74f, 0.71f, 0.63f)  // Common
        };

        private static bool s_show;
        private static Rect s_windowRect = new Rect(0, 0, 1220, 800);
        private static Vector2 s_wheelScroll;
        private static Vector2 s_ownedScroll;
        private static string s_lastResult = "";
        private static bool s_errorLogged;
        private static bool s_positioned;
        private static float s_nextRegistrationCheck;

        private static GUIStyle s_window, s_label, s_labelDim, s_title, s_cardName, s_cardCharge,


            s_button, s_chip, s_bigNumber, s_section, s_cardStyle, s_cardStyleHover;

        private static Texture2D s_panelTex, s_cardTex, s_cardTexHover, s_buttonTex, s_buttonHoverTex,
            s_buttonActiveTex, s_ruleTex, s_barBackTex, s_barFillTex, s_chipTex;

        public static void Toggle()
        {
            if (!IsWorldMap())
            {
                return;
            }

            s_show = !s_show;
        }

        private static float Scale
        {
            get { return Mathf.Clamp(Screen.height / 1080f, 0.8f, 2.2f); }
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
            TryEnsureRewardsRegistered();

            if (!s_show)
            {
                return;
            }

            GachaService.EnsureWheel();

            float s = Scale;
            if (!s_positioned)
            {
                s_positioned = true;
                float w = Mathf.Min(1220f * s, Screen.width * 0.94f);
                float h = Mathf.Min(800f * s, Screen.height * 0.92f);
                s_windowRect = new Rect(
                    Mathf.Max(0f, (Screen.width - w) * 0.5f),
                    Mathf.Max(0f, (Screen.height - h) * 0.5f),
                    w,
                    h);
            }

            s_windowRect = GUI.Window(WindowId, s_windowRect, DrawWindow, GUIContent.none, s_window);
        }

        // ---------------------------------------------------------------- badge

        private static void DrawTicketBadge()
        {
            CycleState state = CycleStateStore.State;
            if (s_show || state.tickets <= 0)
            {
                return;
            }

            float s = Scale;
            Rect rect = new Rect(18f * s, 18f * s, 300f * s, 54f * s);

            GUI.Box(rect, GUIContent.none, s_chip);
            GUI.Label(
                new Rect(rect.x + 12f * s, rect.y + 6f * s, rect.width - 20f * s, 24f * s),
                "抽奖券  " + state.tickets,
                s_title);
            GUI.Label(
                new Rect(rect.x + 12f * s, rect.y + 28f * s, rect.width - 20f * s, 20f * s),
                "按 " + ModConfig.Current.gachaHotkey + " 打开 · " + state.drawCharge + "/" + GachaService.MaxCharge,
                s_labelDim);

            if (GUI.Button(rect, GUIContent.none, s_button))
            {
                s_show = true;
            }

            // Re-draw the labels above the invisible button hit area.
            GUI.Label(
                new Rect(rect.x + 12f * s, rect.y + 6f * s, rect.width - 20f * s, 24f * s),
                "抽奖券  " + state.tickets,
                s_title);
            GUI.Label(
                new Rect(rect.x + 12f * s, rect.y + 28f * s, rect.width - 20f * s, 20f * s),
                "按 " + ModConfig.Current.gachaHotkey + " 打开 · " + state.drawCharge + "/" + GachaService.MaxCharge,
                s_labelDim);
        }

        // ---------------------------------------------------------------- window

        private static void DrawWindow(int id)
        {
            float s = Scale;
            CycleState state = CycleStateStore.State;
            ModConfig config = ModConfig.Current;

            const float pad = 20f;
            float W = s_windowRect.width;
            float H = s_windowRect.height;
            float titleH = 58f * s;
            float leftX = pad * s;
            float leftY = pad * s + titleH;
            float rightW = Mathf.Min(310f * s, W * 0.32f);
            float leftW = W - pad * 2f * s - rightW - 16f * s;
            float rightX = leftX + leftW + 16f * s;
            float bodyH = H - leftY - pad * s;

            DrawHeader(state, pad * s, pad * s, W - pad * 2f * s, titleH);
            DrawRule(new Rect(leftX, leftY - 10f * s, leftW, 2f * s));
            DrawRule(new Rect(rightX - 9f * s, leftY - 10f * s, 1f * s, bodyH + 10f * s));

            DrawWheel(state, leftX, leftY, leftW, bodyH);
            DrawSidePanel(state, config, rightX, leftY, rightW, bodyH);

            GUI.DragWindow(new Rect(0, 0, W, titleH));
        }

        private static void DrawHeader(CycleState state, float x, float y, float w, float h)
        {
            float s = Scale;

            GUI.Label(new Rect(x, y + 6f * s, w * 0.6f, 30f * s), "循环抽奖", s_bigNumber);
            GUI.Label(new Rect(x, y + 34f * s, w * 0.6f, 20f * s), "CYCLE GACHA  ·  WORLD MAP", s_labelDim);

            string tickets = "抽奖券  " + state.tickets;
            Vector2 size = s_title.CalcSize(new GUIContent(tickets));

            Rect close = new Rect(x + w - 28f * s, y + 10f * s, 28f * s, 28f * s);
            float chipRight = close.x - 10f * s;
            Rect ticketRect = new Rect(chipRight - (size.x + 24f * s), y + 12f * s, size.x + 24f * s, 32f * s);

            GUI.Box(ticketRect, GUIContent.none, s_chip);
            GUI.Label(new Rect(ticketRect.x + 12f * s, ticketRect.y + 5f * s, size.x, 24f * s), tickets, s_title);

            if (GUI.Button(close, "×", s_button))
            {
                s_show = false;
            }
        }

        private static void DrawWheel(CycleState state, float x, float y, float w, float h)
        {
            float s = Scale;
            float cardW = (w - (Columns - 1) * 10f * s) / Columns;
            float cardH = 108f * s;
            float rowGap = 10f * s;

            int rows = Mathf.CeilToInt(state.wheel.Count / (float)Columns);
            float contentH = Mathf.Max(h, rows * (cardH + rowGap));
            Rect viewRect = new Rect(x, y, w, h);

            float scrollbar = 12f * s;
            s_wheelScroll = GUI.BeginScrollView(
                viewRect,
                s_wheelScroll,
                new Rect(0, 0, w - scrollbar, contentH),
                false,
                true);

            for (int i = 0; i < state.wheel.Count; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                Rect card = new Rect(
                    col * (cardW + 10f * s),
                    row * (cardH + rowGap),
                    cardW,
                    cardH);

                DrawCard(state.wheel[i], card);
            }

            GUI.EndScrollView();

            if (state.wheel.Count == 0)
            {
                string message = state.drawsSinceWheelGenerated > 0
                    ? "转盘已抽完。点击「重置转盘」开始新一轮。"
                    : "转盘为空，请检查 cycle_gacha.json 的奖池配置。";
                GUI.Label(new Rect(0, 0, viewRect.width - 20f * s, 40f * s), message, s_labelDim);
            }
        }

        private static void DrawCard(WheelOption option, Rect card)
        {
            float s = Scale;
            Color rarity = RarityColor(option.rarity);

            bool hover = card.Contains(Event.current.mousePosition);
            GUI.Box(card, GUIContent.none, hover ? s_cardStyleHover : s_cardStyle);

            // rarity edge
            Fill(new Rect(card.x, card.y, card.width, 2f * s), rarity);
            Fill(new Rect(card.x, card.y + card.height - 2f * s, card.width, 2f * s), rarity);

            Rect iconRect = new Rect(card.x + 8f * s, card.y + 10f * s, 40f * s, 40f * s);
            DrawIcon(iconRect, GetRewardIcon(option.rewardId), Color.white);

            GUI.Label(
                new Rect(card.x + 54f * s, card.y + 8f * s, card.width - 62f * s, 34f * s),
                Truncate(RewardService.GetDisplayName(option.rewardId), 22),
                s_cardName);

            GUI.Label(
                new Rect(card.x + 54f * s, card.y + 40f * s, card.width - 62f * s, 18f * s),
                RarityLabel(option.rarity),
                Colored(s_cardCharge, rarity));

            string charge = "+" + ModConfig.Current.GetCharge(option.rarity);
            Vector2 size = s_cardCharge.CalcSize(new GUIContent(charge));
            GUI.Label(
                new Rect(card.x + card.width - size.x - 8f * s, card.y + card.height - 26f * s, size.x + 2f * s, 20f * s),
                charge,
                Colored(s_cardCharge, ColTextGold));
        }

        private static void DrawSidePanel(CycleState state, ModConfig config, float x, float y, float w, float h)
        {
            float s = Scale;
            float cursor = y;

            GUI.Label(new Rect(x, cursor, w, 20f * s), "抽奖进度", s_section);
            cursor += 22f * s;
            DrawChargeBar(state, x, cursor, w, 14f * s);
            cursor += 20f * s;
            GUI.Label(
                new Rect(x, cursor, w, 18f * s),
                state.drawCharge + " / " + GachaService.MaxCharge + "   满 " + GachaService.MaxCharge + " 点 +1 券",
                s_labelDim);
            cursor += 26f * s;

            GUI.Label(new Rect(x, cursor, w, 20f * s), "转盘剩余", s_section);
            cursor += 22f * s;
            for (int i = 0; i < ModConfig.Rarities.Length; i++)
            {
                string rarity = ModConfig.Rarities[i];
                int total = config.GetWheelCount(rarity);
                if (total <= 0)
                {
                    continue;
                }

                int left = GachaService.CountRemaining(rarity);
                Color color = RarityColor(rarity);

                Fill(new Rect(x, cursor + 3f * s, 8f * s, 8f * s), color);
                GUI.Label(
                    new Rect(x + 14f * s, cursor, w - 60f * s, 18f * s),
                    RarityShortName(rarity),
                    Colored(s_label, color));
                GUI.Label(
                    new Rect(x + w - 48f * s, cursor, 48f * s, 18f * s),
                    left + " / " + total,
                    Colored(s_labelDim, ColTextDim));
                cursor += 20f * s;
            }

            cursor += 8f * s;
            DrawRule(new Rect(x, cursor, w, 1f * s));
            cursor += 10f * s;

            if (GachaService.CanDraw())
            {
                if (GUI.Button(new Rect(x, cursor, w, 42f * s), "抽取", s_button))
                {
                    WheelOption option = GachaService.Draw();
                    s_lastResult = option == null
                        ? "没有抽奖券，或转盘为空。"
                        : FormatResult(option);
                }
            }
            else
            {
                GUI.enabled = false;
                GUI.Button(new Rect(x, cursor, w, 42f * s), "抽取", s_button);
                GUI.enabled = true;
            }

            cursor += 50f * s;

            bool canReset = GachaService.CanReset();
            GUI.enabled = canReset;
            if (GUI.Button(new Rect(x, cursor, w, 34f * s), "重置转盘", s_button))
            {
                if (GachaService.ResetWheel())
                {
                    s_lastResult = "转盘已重置。";
                }
            }

            GUI.enabled = true;
            cursor += 44f * s;

            if (!string.IsNullOrEmpty(s_lastResult))
            {
                GUI.Label(new Rect(x, cursor, w, 54f * s), s_lastResult, s_label);
                cursor += 58f * s;
            }

            float listH = Mathf.Max(60f * s, h - (cursor - y) - 24f * s);
            GUI.Label(new Rect(x, cursor, w, 20f * s), "已获得的循环奖励  " + state.rewardIds.Count, s_section);
            cursor += 22f * s;

            Rect listRect = new Rect(x, cursor, w, Mathf.Max(40f * s, listH - 22f * s));
            s_ownedScroll = GUI.BeginScrollView(
                listRect, s_ownedScroll, new Rect(0, 0, w - 12f * s, Mathf.Max(listRect.height, CountOwnedRows(state) * 24f * s)), false, true);

            if (state.rewardIds.Count == 0)
            {
                GUI.Label(new Rect(0, 0, w - 20f * s, 20f * s), "（空）", s_labelDim);
            }
            else
            {
                Dictionary<string, int> counts = new Dictionary<string, int>();
                List<string> order = new List<string>();
                for (int i = 0; i < state.rewardIds.Count; i++)
                {
                    string rewardId = state.rewardIds[i];
                    if (!counts.ContainsKey(rewardId))
                    {
                        counts[rewardId] = 0;
                        order.Add(rewardId);
                    }

                    counts[rewardId]++;
                }

                float rowY = 0f;
                for (int i = 0; i < order.Count; i++)
                {
                    Rect row = new Rect(0, rowY, w - 12f * s, 22f * s);
                    if (row.Contains(Event.current.mousePosition))
                    {
                        Fill(row, new Color(1f, 1f, 1f, 0.04f));
                    }

                    DrawIcon(new Rect(row.x, row.y + 2f * s, 18f * s, 18f * s), GetRewardIcon(order[i]), Color.white);
                    GUI.Label(
                        new Rect(row.x + 24f * s, row.y + 1f * s, row.width - 46f * s, 20f * s),
                        Truncate(RewardService.GetDisplayName(order[i]), 18),
                        s_label);
                    GUI.Label(
                        new Rect(row.x + row.width - 22f * s, row.y + 1f * s, 22f * s, 20f * s),
                        "×" + counts[order[i]],
                        Colored(s_labelDim, ColTextGold));
                    rowY += 24f * s;
                }
            }

            GUI.EndScrollView();
        }

        private static int CountOwnedRows(CycleState state)
        {
            HashSet<string> unique = new HashSet<string>();
            if (state.rewardIds != null)
            {
                for (int i = 0; i < state.rewardIds.Count; i++)
                {
                    unique.Add(state.rewardIds[i]);
                }
            }

            return Mathf.Max(1, unique.Count);
        }

        private static void DrawChargeBar(CycleState state, float x, float y, float w, float h)
        {
            GUI.DrawTexture(new Rect(x, y, w, h), s_barBackTex);
            float fill = Mathf.Clamp01(state.drawCharge / (float)GachaService.MaxCharge);
            if (fill > 0f)
            {
                GUI.DrawTexture(new Rect(x + 1f, y + 1f, (w - 2f) * fill, h - 2f), s_barFillTex);
            }

            Fill(new Rect(x, y, w, 1f), ColPanelEdge);
            Fill(new Rect(x, y + h - 1f, w, 1f), ColPanelEdge);
        }

        // ---------------------------------------------------------------- helpers

        private static void DrawRule(Rect rect)
        {
            GUI.DrawTexture(rect, s_ruleTex);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static string FormatResult(WheelOption option)
        {
            string name = RewardService.GetDisplayName(option.rewardId);
            return "获得  [" + RarityLabel(option.rarity) + "]  " + name;
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            text = text.Replace("\n", " ");
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }

        private static Color RarityColor(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return RarityColors[0];
                case "Epic": return RarityColors[1];
                case "Rare": return RarityColors[2];
                case "Uncommon": return RarityColors[3];
                default: return RarityColors[4];
            }
        }

        private static string RarityLabel(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return "传说";
                case "Epic": return "史诗";
                case "Rare": return "稀有";
                case "Uncommon": return "罕见";
                default: return "普通";
            }
        }

        private static string RarityShortName(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return "传说 Legendary";
                case "Epic": return "史诗 Epic";
                case "Rare": return "稀有 Rare";
                case "Uncommon": return "罕见 Uncommon";
                default: return "普通 Common";
            }
        }

        private static GUIStyle Colored(GUIStyle baseStyle, Color color)
        {
            GUIStyle style = new GUIStyle(baseStyle);
            style.normal.textColor = color;
            return style;
        }

        private static void DrawIcon(Rect rect, Sprite sprite, Color tint)
        {
            if (sprite == null || sprite.texture == null)
            {
                Fill(rect, new Color(1f, 1f, 1f, 0.06f));
                return;
            }

            if (rect.width / rect.height > 1f)
            {
                float w = rect.height;
                rect = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y, w, rect.height);
            }
            else if (rect.width / rect.height < 1f)
            {
                float h = rect.width;
                rect = new Rect(rect.x, rect.y + (rect.height - h) * 0.5f, rect.width, h);
            }

            Texture2D texture = sprite.texture;
            Rect tr = sprite.textureRect;
            Rect coords = new Rect(
                tr.x / texture.width,
                tr.y / texture.height,
                tr.width / texture.width,
                tr.height / texture.height);

            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, texture, coords, true);
            GUI.color = previous;
        }

        private static Sprite GetRewardIcon(string rewardId)
        {
            RewardDef def = RewardService.FindDef(rewardId);
            if (def == null)
            {
                return null;
            }

            try
            {
                if (!string.IsNullOrEmpty(def.effect))
                {
                    EffectModel effect = RewardService.ResolveEffect(def.effect);
                    if (effect != null)
                    {
                        Sprite icon = effect.GetIcon();
                        if (icon != null)
                        {
                            return icon;
                        }

                        if (effect.overrideIcon != null)
                        {
                            return effect.overrideIcon;
                        }
                    }

                    return null;
                }

                if (def.goods != null && def.goods.Count > 0 && def.goods[0] != null)
                {
                    GoodModel good = RewardService.ResolveGoodModel(def.goods[0].good);
                    if (good != null)
                    {
                        return good.icon;
                    }
                }

                if (def.villagers > 0)
                {
                    RaceModel[] races = SO.Settings != null ? SO.Settings.Races : null;
                    if (races != null && races.Length > 0 && races[0] != null)
                    {
                        return races[0].icon;
                    }
                }
            }
            catch
            {
                // Icons are cosmetic.
            }

            return null;
        }

        private static bool IsWorldMap()
        {
            return SceneManager.GetActiveScene().name == "WorldMap";
        }

        private static void TryEnsureRewardsRegistered()
        {
            if (Time.realtimeSinceStartup < s_nextRegistrationCheck)
            {
                return;
            }

            s_nextRegistrationCheck = Time.realtimeSinceStartup + 2f;

            try
            {
                RewardService.EnsureRegistered();
            }
            catch (Exception e)
            {
                if (!s_errorLogged)
                {
                    s_errorLogged = true;
                    Plugin.Log.LogError("Failed to register previously drawn rewards");
                    Plugin.Log.LogError(e);
                }
            }
        }

        // ---------------------------------------------------------------- skin

        private static void EnsureStyles()
        {
            if (s_window != null)
            {
                return;
            }

            float s = Scale;
            Font font = LoadFont();

            s_panelTex = MakeSliced(ColPanelFill, ColPanelEdge, ColPanelInner, 36);
            s_cardTex = MakeSliced(ColCardFill, new Color(0.36f, 0.31f, 0.21f, 1f), ColCardInner, 24);
            s_cardTexHover = MakeSliced(new Color(0.14f, 0.115f, 0.08f, 1f), new Color(0.55f, 0.47f, 0.30f, 1f), ColCardInner, 24);
            s_buttonTex = MakeSliced(ColButtonFill, new Color(0.46f, 0.39f, 0.24f, 1f), new Color(0.30f, 0.25f, 0.15f, 1f), 20);
            s_buttonHoverTex = MakeSliced(ColButtonHover, new Color(0.70f, 0.58f, 0.33f, 1f), new Color(0.42f, 0.34f, 0.20f, 1f), 20);
            s_buttonActiveTex = MakeSliced(ColButtonActive, new Color(0.82f, 0.68f, 0.39f, 1f), new Color(0.50f, 0.41f, 0.24f, 1f), 20);
            s_chipTex = MakeSliced(new Color(0.10f, 0.083f, 0.061f, 0.92f), new Color(0.42f, 0.36f, 0.22f, 1f), ColCardInner, 16);
            s_ruleTex = MakeSolid(new Color(0.62f, 0.52f, 0.30f, 0.55f));
            s_barBackTex = MakeSolid(new Color(0.08f, 0.07f, 0.05f, 1f));
            s_barFillTex = MakeSolid(new Color(0.76f, 0.62f, 0.31f, 1f));

            s_window = new GUIStyle(GUI.skin.window);
            s_window.border = new RectOffset(18, 18, 18, 18);
            s_window.normal.background = s_panelTex;
            s_window.onNormal.background = s_panelTex;
            s_window.normal.textColor = ColText;
            s_window.padding = new RectOffset(18, 18, 18, 18);
            s_window.font = font;

            s_label = new GUIStyle(GUI.skin.label);
            s_label.font = font;
            s_label.fontSize = Mathf.RoundToInt(15f * s);
            s_label.normal.textColor = ColText;
            s_label.wordWrap = true;

            s_labelDim = new GUIStyle(s_label);
            s_labelDim.fontSize = Mathf.RoundToInt(13f * s);
            s_labelDim.normal.textColor = ColTextDim;
            s_labelDim.wordWrap = false;

            s_section = new GUIStyle(s_label);
            s_section.fontSize = Mathf.RoundToInt(13f * s);
            s_section.fontStyle = FontStyle.Bold;
            s_section.normal.textColor = ColTextGold;
            s_section.wordWrap = false;

            s_title = new GUIStyle(s_label);
            s_title.fontSize = Mathf.RoundToInt(17f * s);
            s_title.fontStyle = FontStyle.Bold;
            s_title.normal.textColor = ColTextGold;
            s_title.wordWrap = false;

            s_bigNumber = new GUIStyle(s_label);
            s_bigNumber.fontSize = Mathf.RoundToInt(28f * s);
            s_bigNumber.fontStyle = FontStyle.Bold;
            s_bigNumber.normal.textColor = ColTextGold;
            s_bigNumber.wordWrap = false;

            s_cardName = new GUIStyle(s_label);
            s_cardName.fontSize = Mathf.RoundToInt(13f * s);
            s_cardName.wordWrap = true;
            s_cardName.normal.textColor = ColText;

            s_cardCharge = new GUIStyle(s_label);
            s_cardCharge.fontSize = Mathf.RoundToInt(12f * s);
            s_cardCharge.fontStyle = FontStyle.Bold;
            s_cardCharge.wordWrap = false;

            s_button = new GUIStyle(GUI.skin.button);
            s_button.font = font;
            s_button.fontSize = Mathf.RoundToInt(15f * s);
            s_button.border = new RectOffset(10, 10, 10, 10);
            s_button.normal.background = s_buttonTex;
            s_button.hover.background = s_buttonHoverTex;
            s_button.active.background = s_buttonActiveTex;
            s_button.focused.background = s_buttonHoverTex;
            s_button.normal.textColor = ColTextGold;
            s_button.hover.textColor = new Color(0.98f, 0.90f, 0.70f);
            s_button.active.textColor = Color.white;
            s_button.alignment = TextAnchor.MiddleCenter;

            s_chip = new GUIStyle(s_button);
            s_chip.padding = new RectOffset(8, 8, 6, 6);

            s_cardStyle = new GUIStyle(GUI.skin.box);
            s_cardStyle.border = new RectOffset(8, 8, 8, 8);
            s_cardStyle.normal.background = s_cardTex;
            s_cardStyle.padding = new RectOffset(6, 6, 6, 6);

            s_cardStyleHover = new GUIStyle(s_cardStyle);
            s_cardStyleHover.normal.background = s_cardTexHover;
            s_cardStyleHover.hover.background = s_cardTexHover;
        }

        private static Texture2D MakeSliced(Color fill, Color border, Color inner, int size)
        {
            int b = Mathf.Max(3, size / 6);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < b || y < b || x >= size - b || y >= size - b;
                    bool isInnerRule = !isBorder && (x == b || y == b || x == size - b - 1 || y == size - b - 1);
                    Color color = isBorder ? border : (isInnerRule ? inner : fill);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D MakeSolid(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
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
