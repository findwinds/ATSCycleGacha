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
    /// The gacha window, drawn in the game's own visual language.
    ///
    /// The board is radial: the legendary option sits in the middle and each lower rarity forms a
    /// ring around it with smaller cards, so the layout itself shows how rare an option is. Drawing
    /// runs a highlight around the ring, skipping taken slots, slowing down until it lands.
    /// </summary>
    public static class GachaWindow
    {
        private const int WindowId = 0x0C7B;

        // ---- palette (the game's parchment / gold UI) ----
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
            new Color(0.84f, 0.55f, 0.18f), // 传说
            new Color(0.62f, 0.40f, 0.80f), // 史诗
            new Color(0.36f, 0.58f, 0.85f), // 稀有
            new Color(0.40f, 0.70f, 0.44f), // 罕见
            new Color(0.74f, 0.71f, 0.63f)  // 普通
        };

        // Board geometry in board units; scaled to whatever space the window offers.
        private static readonly float[] RingRadii = { 0f, 186f, 330f, 466f, 592f };
        private static readonly Vector2[] CardSizes =
        {
            new Vector2(224f, 132f),
            new Vector2(172f, 104f),
            new Vector2(150f, 90f),
            new Vector2(132f, 78f),
            new Vector2(116f, 68f)
        };
        private static readonly int[] CardFontSizes = { 19, 17, 15, 13, 12 };
        private const float BoardUnit = 1320f;

        // ---- animation ----
        private enum AnimPhase
        {
            None,
            Sweep,
            Celebrate
        }

        private const float SweepSeconds = 2.3f;
        private const float CelebrateSeconds = 5f;

        private static AnimPhase s_phase = AnimPhase.None;
        private static float s_phaseTime;
        private static int s_animStep;
        private static readonly List<int> s_animOrder = new List<int>();
        private static float[] s_animTimes;
        private static WheelOption s_animWinner;
        private static bool s_committed;

        private static bool s_show;
        private static Rect s_windowRect = new Rect(0, 0, 1340f, 1000f);
        private static Vector2 s_ownedScroll;
        private static string s_lastResult = "";
        private static bool s_errorLogged;
        private static bool s_positioned;
        private static float s_nextRegistrationCheck;
        private static int s_hoveredIndex = -1;

        private static GUIStyle s_window, s_label, s_labelDim, s_title, s_bigNumber, s_section, s_button, s_chip, s_tooltip;

        private static Texture2D s_panelTex, s_buttonTex, s_buttonHoverTex, s_buttonActiveTex,
            s_ruleTex, s_barBackTex, s_barFillTex, s_glowTex, s_chipTex;

        private static readonly Dictionary<int, GUIStyle> s_textStyles = new Dictionary<int, GUIStyle>();
        private static Font s_font;

        private static GUIStyle[] s_cardBg;
        private static GUIStyle[] s_cardBgDrawn;
        private static GUIStyle[] s_cardName;
        private static GUIStyle[] s_cardNameDrawn;
        private static GUIStyle[] s_cardCaption;
        private static float s_styleScale = -1f;
        private static float s_boardScale;

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

        /// <summary>Board units to pixels, derived from the space the window leaves for the board.</summary>
        private static void UpdateBoardScale()
        {
            float s = Scale;
            float pad = 18f * s;
            float sideW = Mathf.Min(258f * s, Mathf.Max(1f, s_windowRect.width) * 0.26f);
            float w = Mathf.Max(1f, s_windowRect.width) - pad * 2f - sideW - 14f * s;
            float h = Mathf.Max(1f, s_windowRect.height) - pad - (pad + 54f * s) - 72f * s;
            s_boardScale = Mathf.Clamp(Mathf.Min(w, h) / BoardUnit, 0.3f, 2.2f);
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

        /// <summary>
        /// Advances the draw animation. The sweep eases out, then the winning card blinks for a
        /// while; the reward is only committed (and therefore shown in the list) when the sweep
        /// lands, so the result cannot be read off the side panel before the animation decides.
        /// </summary>
        public static void Tick()
        {
            if (s_phase == AnimPhase.None)
            {
                return;
            }

            s_phaseTime += Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            if (s_phase == AnimPhase.Sweep)
            {
                while (s_animStep + 1 < s_animOrder.Count &&
                       s_animTimes != null && s_phaseTime >= s_animTimes[s_animStep + 1])
                {
                    s_animStep++;
                }

                bool landed = s_animTimes == null || s_animOrder.Count == 0 ||
                              (s_animStep >= s_animOrder.Count - 1 && s_phaseTime >= SweepSeconds);

                if (landed || s_phaseTime >= SweepSeconds + 0.6f)
                {
                    LandOnWinner();
                }

                return;
            }

            if (s_phase == AnimPhase.Celebrate && s_phaseTime >= CelebrateSeconds)
            {
                s_phase = AnimPhase.None;
                s_animWinner = null;
            }
        }

        private static void StartDraw()
        {
            WheelOption won = GachaService.Draw();
            if (won == null)
            {
                s_lastResult = "没有抽奖券，或转盘已抽完。";
                return;
            }

            CycleState state = CycleStateStore.State;
            List<int> candidates = new List<int>();
            int winnerIndex = -1;

            for (int i = 0; i < state.wheel.Count; i++)
            {
                WheelOption option = state.wheel[i];
                if (option == null)
                {
                    continue;
                }

                if (option == won)
                {
                    winnerIndex = i;
                    continue;
                }

                // Skip everything drawn earlier; the winner is appended last so the sweep lands on it.
                if (!option.drawn)
                {
                    candidates.Add(i);
                }
            }

            if (winnerIndex < 0 || candidates.Count + 1 < 2)
            {
                s_lastResult = "转盘已抽完。";
                LandOnWinner();
                return;
            }

            // Rotate the order so the sweep does not always start at the same card.
            int start = UnityEngine.Random.Range(0, candidates.Count);
            s_animOrder.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                s_animOrder.Add(candidates[(start + i) % candidates.Count]);
            }

            s_animOrder.Add(winnerIndex);

            // Cumulative start times: fast at the beginning, clearly slowing down before the result.
            int n = s_animOrder.Count;
            s_animTimes = new float[n];
            float total = 0f;
            for (int k = 0; k < n; k++)
            {
                float t = n <= 1 ? 1f : k / (float)(n - 1);
                s_animTimes[k] = total;
                total += Mathf.Lerp(0.035f, 0.34f, Mathf.Pow(t, 3f));
            }

            float scale = total > 0.01f ? SweepSeconds / total : 1f;
            for (int k = 0; k < n; k++)
            {
                s_animTimes[k] *= scale;
            }

            s_animWinner = won;
            s_animStep = 0;
            s_phaseTime = 0f;
            s_committed = false;
            s_phase = AnimPhase.Sweep;
            s_lastResult = "抽取中……";
        }

        private static void LandOnWinner()
        {
            if (!s_committed)
            {
                s_committed = true;

                // Now the result is real: record the reward and register it with the game.
                GachaService.CommitPending();

                if (s_animWinner != null)
                {
                    s_lastResult = "获得  [" + RarityLabel(s_animWinner.rarity) + "]  " +
                                   RewardService.GetDisplayName(s_animWinner.rewardId);
                }
            }

            s_phase = AnimPhase.Celebrate;
            s_phaseTime = 0f;
        }

        private static void DrawInternal()
        {
            if (!IsWorldMap())
            {
                s_show = false;
                s_phase = AnimPhase.None;
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
            UpdateBoardScale();

            float s = Scale;
            if (!s_positioned)
            {
                s_positioned = true;
                float w = Mathf.Min(1340f * s, Screen.width * 0.94f);
                float h = Mathf.Min(1000f * s, Screen.height * 0.92f);
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

            if (GUI.Button(rect, GUIContent.none, s_button))
            {
                s_show = true;
            }

            GUI.Label(new Rect(rect.x + 12f * s, rect.y + 6f * s, rect.width - 20f * s, 24f * s),
                "抽奖券  " + state.tickets, s_title);
            GUI.Label(new Rect(rect.x + 12f * s, rect.y + 28f * s, rect.width - 20f * s, 20f * s),
                "按 " + ModConfig.Current.gachaHotkey + " 打开 · " + state.drawCharge + "/" + GachaService.MaxCharge,
                s_labelDim);
        }

        // ---------------------------------------------------------------- window

        private static void DrawWindow(int id)
        {
            float s = Scale;
            CycleState state = CycleStateStore.State;
            float W = s_windowRect.width;
            float H = s_windowRect.height;
            float pad = 18f * s;
            float headerH = 54f * s;
            float footerH = 72f * s;
            float sideW = Mathf.Min(258f * s, W * 0.26f);

            DrawHeader(state, pad, pad, W - pad * 2f, headerH);

            float boardX = pad;
            float boardY = pad + headerH;
            float boardW = W - pad * 2f - sideW - 14f * s;
            float boardH = H - boardY - pad - footerH;
            Rect board = new Rect(boardX, boardY, boardW, boardH);

            DrawBoard(state, board);

            float sideX = boardX + boardW + 14f * s;
            DrawSidePanel(state, sideX, boardY, sideW, boardH);

            DrawFooter(state, pad, H - pad - footerH, W - pad * 2f, footerH);

            if (s_hoveredIndex >= 0 && s_hoveredIndex < state.wheel.Count)
            {
                DrawHoverTooltip(state.wheel[s_hoveredIndex]);
            }

            GUI.DragWindow(new Rect(0, 0, W, headerH));
        }

        private static void DrawHeader(CycleState state, float x, float y, float w, float h)
        {
            float s = Scale;

            GUI.Label(new Rect(x, y, w * 0.6f, 28f * s), "循环抽奖", s_bigNumber);
            GUI.Label(new Rect(x, y + 28f * s, w * 0.6f, 18f * s), "大地图 · 命运转盘", s_labelDim);

            string tickets = "抽奖券  " + state.tickets;
            Vector2 size = s_title.CalcSize(new GUIContent(tickets));

            Rect close = new Rect(x + w - 28f * s, y + 10f * s, 28f * s, 28f * s);
            float chipRight = close.x - 10f * s;
            Rect ticketRect = new Rect(chipRight - (size.x + 24f * s), y + 10f * s, size.x + 24f * s, 30f * s);

            GUI.Box(ticketRect, GUIContent.none, s_chip);
            GUI.Label(new Rect(ticketRect.x + 12f * s, ticketRect.y + 4f * s, size.x, 24f * s), tickets, s_title);

            if (GUI.Button(close, "×", s_button))
            {
                s_show = false;
                if (s_phase == AnimPhase.Sweep)
                {
                    LandOnWinner();
                }
            }
        }

        // ---------------------------------------------------------------- board

        private struct Slot
        {
            public int index;
            public Rect rect;
            public int rarity;
        }

        private static readonly List<Slot> s_layout = new List<Slot>();
        private static Rect s_actionDrawRect;
        private static Rect s_actionResetRect;

        private const int GridCols = 6;
        private const int GridRows = 6;
        private const int ActionRow = 4;
        private const int ActionColLeft = 2;

        /// <summary>
        /// Compact card block: the legendary option is one big card in the middle (2x2 cells) and
        /// every other option fills the surrounding cells in a stable pseudo-random order, so the
        /// board does not reshuffle between frames or reloads.
        /// </summary>
        private static void ComputeLayout(CycleState state, Rect board)
        {
            s_layout.Clear();

            float cellW = board.width / GridCols;
            float cellH = board.height / GridRows;
            float margin = Mathf.Min(cellW, cellH) * 0.045f;

            int legendary = -1;
            List<int> others = new List<int>();

            for (int i = 0; i < state.wheel.Count; i++)
            {
                WheelOption option = state.wheel[i];
                if (option == null)
                {
                    continue;
                }

                if (option.rarity == "Legendary")
                {
                    if (legendary < 0)
                    {
                        legendary = i;
                    }

                    continue;
                }

                others.Add(i);
            }

            others.Sort(delegate (int a, int b)
            {
                int ha = StableHash(state.wheel[a].rewardId);
                int hb = StableHash(state.wheel[b].rewardId);
                return ha != hb ? ha.CompareTo(hb) : a.CompareTo(b);
            });

            int cursor = 0;
            for (int row = 0; row < GridRows && cursor < others.Count; row++)
            {
                for (int col = 0; col < GridCols && cursor < others.Count; col++)
                {
                    bool center = col >= 2 && col <= 3 && row >= 2 && row <= 3;
                    bool action = row == ActionRow && col >= ActionColLeft && col < ActionColLeft + 2;
                    if (center || action)
                    {
                        continue;
                    }

                    int index = others[cursor++];
                    Rect cell = CellRect(board, row, col, cellW, cellH, margin);

                    Slot slot = new Slot();
                    slot.index = index;
                    slot.rect = cell;
                    slot.rarity = RarityPalette.Index(state.wheel[index].rarity);
                    s_layout.Add(slot);
                }
            }

            if (legendary >= 0)
            {
                Rect cell = new Rect(
                    board.x + 2f * cellW + margin,
                    board.y + 2f * cellH + margin,
                    2f * cellW - margin * 2f,
                    2f * cellH - margin * 2f);

                Slot slot = new Slot();
                slot.index = legendary;
                slot.rect = cell;
                slot.rarity = 0;
                s_layout.Add(slot);
            }

            s_actionDrawRect = CellRect(board, ActionRow, ActionColLeft, cellW, cellH, margin);
            s_actionResetRect = CellRect(board, ActionRow, ActionColLeft + 1, cellW, cellH, margin);
        }

        private static Rect CellRect(Rect board, int row, int col, float cellW, float cellH, float margin)
        {
            return new Rect(
                board.x + col * cellW + margin,
                board.y + row * cellH + margin,
                cellW - margin * 2f,
                cellH - margin * 2f);
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = 17;
                if (text != null)
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        hash = hash * 31 + text[i];
                    }
                }

                return hash & 0x7FFFFFFF;
            }
        }

        private static void DrawBoard(CycleState state, Rect board)
        {
            ComputeLayout(state, board);

            int highlight = -1;
            if (s_phase == AnimPhase.Sweep && s_animOrder.Count > 0)
            {
                highlight = s_animOrder[Mathf.Clamp(s_animStep, 0, s_animOrder.Count - 1)];
            }
            else if (s_phase == AnimPhase.Celebrate && s_animWinner != null)
            {
                highlight = IndexOf(s_animWinner);
            }

            s_hoveredIndex = -1;

            for (int i = 0; i < s_layout.Count; i++)
            {
                Slot slot = s_layout[i];
                DrawCard(state.wheel[slot.index], slot.rect, slot.rarity, slot.index == highlight);
            }

            DrawBoardActions();
        }

        private static void DrawBoardActions()
        {
            float s = Scale;
            bool busy = s_phase != AnimPhase.None;

            GUI.enabled = !busy && GachaService.CanDraw();
            string drawLabel = s_phase == AnimPhase.Sweep ? "抽取中…" : "抽奖";
            if (GUI.Button(s_actionDrawRect, drawLabel, s_button))
            {
                StartDraw();
            }

            GUI.enabled = !busy && GachaService.CanReset();
            if (GUI.Button(s_actionResetRect, "重置转盘", s_button))
            {
                if (GachaService.ResetWheel())
                {
                    s_lastResult = "转盘已重置。";
                }
            }

            GUI.enabled = true;
        }

        private static void DrawCard(WheelOption option, Rect rect, int rarity, bool highlighted)
        {
            float s = Scale;
            Color rarityColor = RarityColors[rarity];
            bool drawn = option != null && option.drawn;
            bool hover = !drawn && rect.Contains(Event.current.mousePosition);

            if (highlighted)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.realtimeSinceStartup * 16f);
                Rect glow = new Rect(
                    rect.x - rect.height * 0.30f,
                    rect.y - rect.height * 0.30f,
                    rect.width + rect.height * 0.60f,
                    rect.height + rect.height * 0.60f);

                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.94f, 0.68f, 0.55f + 0.35f * pulse);
                GUI.DrawTexture(glow, s_glowTex);
                GUI.color = previous;
            }

            GUI.Box(rect, GUIContent.none, drawn ? s_cardBgDrawn[rarity] : s_cardBg[rarity]);

            if (highlighted)
            {
                Color edge = new Color(1f, 0.95f, 0.72f, 1f);
                Fill(new Rect(rect.x, rect.y, rect.width, 2f * s), edge);
                Fill(new Rect(rect.x, rect.y + rect.height - 2f * s, rect.width, 2f * s), edge);
                Fill(new Rect(rect.x, rect.y, 2f * s, rect.height), edge);
                Fill(new Rect(rect.x + rect.width - 2f * s, rect.y, 2f * s, rect.height), edge);
            }
            else if (!drawn)
            {
                Fill(new Rect(rect.x, rect.y, rect.width, 2f * s), rarityColor);
                Fill(new Rect(rect.x, rect.y + rect.height - 2f * s, rect.width, 2f * s), rarityColor);
                Fill(new Rect(rect.x, rect.y, 2f * s, rect.height), rarityColor);
                Fill(new Rect(rect.x + rect.width - 2f * s, rect.y, 2f * s, rect.height), rarityColor);
            }

            float pad = Mathf.Max(3f, rect.height * 0.06f);
            float iconSize = Mathf.Min(rect.height - pad * 2f, rect.width * 0.34f);
            Rect icon = new Rect(rect.x + pad, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
            DrawIcon(icon, GetRewardIcon(option != null ? option.rewardId : null), drawn ? 0.32f : 1f);

            int fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.height * 0.20f), 9, 26);
            GUIStyle nameStyle = TextStyle(fontSize, drawn ? 1 : -1, rarity, false);
            GUIStyle captionStyle = TextStyle(Mathf.Max(9, Mathf.RoundToInt(fontSize * 0.85f)), drawn ? 1 : 0, rarity, true);

            string charge = "+" + (option != null ? ModConfig.Current.GetCharge(option.rarity) : "0");
            Vector2 chargeSize = captionStyle.CalcSize(new GUIContent(charge));
            float chargeH = chargeSize.y;

            float textX = icon.xMax + pad * 1.4f;
            float textW = rect.xMax - pad - textX;
            if (textW > 14f)
            {
                string name = option != null ? RewardService.GetDisplayName(option.rewardId) : "";
                int maxChars = Mathf.Max(4, Mathf.FloorToInt(textW / Mathf.Max(6f, fontSize * 0.62f)));

                GUI.Label(
                    new Rect(textX, rect.y + pad * 0.8f, textW, rect.height - pad * 1.6f - chargeH),
                    Truncate(name, maxChars),
                    nameStyle);
            }

            GUI.Label(
                new Rect(rect.xMax - chargeSize.x - pad * 1.2f, rect.yMax - chargeH - pad * 0.4f,
                    chargeSize.x + 2f, chargeH),
                charge,
                captionStyle);

            if (drawn)
            {
                Fill(rect, new Color(0f, 0f, 0f, 0.28f));
            }

            if (hover)
            {
                s_hoveredIndex = IndexOf(option);
            }
        }

        private static int IndexOf(WheelOption option)
        {
            CycleState state = CycleStateStore.State;
            for (int i = 0; i < state.wheel.Count; i++)
            {
                if (ReferenceEquals(state.wheel[i], option))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void DrawHoverTooltip(WheelOption option)
        {
            if (option == null)
            {
                return;
            }

            float s = Scale;
            string newline = "\n";
            string body = RewardService.GetDisplayName(option.rewardId) + newline +
                          RarityLabel(option.rarity) + "   进度 +" +
                          ModConfig.Current.GetCharge(option.rarity);

            string description = RewardService.GetDescription(option.rewardId);
            if (!string.IsNullOrEmpty(description))
            {
                body += newline + newline + description;
            }

            Vector2 mouse = Event.current.mousePosition;
            float w = 300f * s;
            Vector2 size = s_tooltip.CalcSize(new GUIContent(body));
            float h = Mathf.Min(size.y + 18f * s, 260f * s);

            Rect rect = new Rect(mouse.x + 18f * s, mouse.y + 12f * s, w, h);
            if (rect.xMax > s_windowRect.width - 6f)
            {
                rect.x = Mathf.Max(6f, mouse.x - w - 18f * s);
            }

            if (rect.yMax > s_windowRect.height - 6f)
            {
                rect.y = Mathf.Max(6f, s_windowRect.height - h - 6f);
            }

            GUI.Box(rect, GUIContent.none, s_chip);
            GUI.Label(new Rect(rect.x + 10f * s, rect.y + 8f * s, rect.width - 20f * s, rect.height - 16f * s),
                body, s_tooltip);
        }

        private static void DrawFooter(CycleState state, float x, float y, float w, float h)
        {
            float s = Scale;
            GUI.Label(new Rect(x, y + 10f * s, w, h - 20f * s), s_lastResult, s_label);
        }

        private static void DrawSidePanel(CycleState state, float x, float y, float w, float h)
        {
            float s = Scale;
            float cursor = y;

            GUI.Label(new Rect(x, cursor, w, 18f * s), "抽奖进度", s_section);
            cursor += 20f * s;
            DrawChargeBar(state, x, cursor, w, 12f * s);
            cursor += 17f * s;
            GUI.Label(new Rect(x, cursor, w, 16f * s),
                state.drawCharge + " / " + GachaService.MaxCharge + "   满 " + GachaService.MaxCharge + " 点 +1 券",
                s_labelDim);
            cursor += 24f * s;

            GUI.Label(new Rect(x, cursor, w, 18f * s), "转盘剩余", s_section);
            cursor += 19f * s;
            for (int i = 0; i < ModConfig.Rarities.Length; i++)
            {
                string rarity = ModConfig.Rarities[i];
                int total = ModConfig.Current.GetWheelCount(rarity);
                if (total <= 0)
                {
                    continue;
                }

                Color color = RarityColors[i];
                Fill(new Rect(x, cursor + 4f * s, 8f * s, 8f * s), color);
                GUI.Label(new Rect(x + 14f * s, cursor, w - 60f * s, 17f * s), RarityLabel(rarity),
                    Colored(s_label, color));
                GUI.Label(new Rect(x + w - 52f * s, cursor, 52f * s, 17f * s),
                    GachaService.CountRemaining(rarity) + " / " + total, Colored(s_labelDim, ColTextDim));
                cursor += 18f * s;
            }

            cursor += 8f * s;
            DrawRule(new Rect(x, cursor, w, 1f * s));
            cursor += 10f * s;

            GUI.Label(new Rect(x, cursor, w, 18f * s), "已获得奖励  " + state.rewardIds.Count, s_section);
            cursor += 20f * s;

            float listH = Mathf.Max(40f * s, y + h - cursor);
            Rect listRect = new Rect(x, cursor, w, listH);
            s_ownedScroll = GUI.BeginScrollView(
                listRect, s_ownedScroll,
                new Rect(0, 0, w - 12f * s, Mathf.Max(listRect.height, CountOwnedRows(state) * 22f * s)),
                false, true);

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
                    Rect row = new Rect(0, rowY, w - 12f * s, 20f * s);
                    if (row.Contains(Event.current.mousePosition))
                    {
                        Fill(row, new Color(1f, 1f, 1f, 0.04f));
                    }

                    DrawIcon(new Rect(row.x, row.y + 2f * s, 16f * s, 16f * s), GetRewardIcon(order[i]), 1f);
                    GUI.Label(new Rect(row.x + 22f * s, row.y, row.width - 44f * s, 20f * s),
                        Truncate(RewardService.GetDisplayName(order[i]), 12), s_labelDim);
                    GUI.Label(new Rect(row.x + row.width - 20f * s, row.y, 20f * s, 20f * s),
                        "×" + counts[order[i]], Colored(s_labelDim, ColTextGold));
                    rowY += 22f * s;
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

        private static int RarityIndex(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return 0;
                case "Epic": return 1;
                case "Rare": return 2;
                case "Uncommon": return 3;
                default: return 4;
            }
        }

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

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            text = text.Replace("\n", " ");
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
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

        private static GUIStyle Colored(GUIStyle baseStyle, Color color)
        {
            GUIStyle style = new GUIStyle(baseStyle);
            style.normal.textColor = color;
            return style;
        }

        private static void DrawIcon(Rect rect, Sprite sprite, float alpha)
        {
            if (sprite == null || sprite.texture == null)
            {
                Fill(rect, new Color(1f, 1f, 1f, 0.06f * alpha));
                return;
            }

            if (rect.width > rect.height)
            {
                float w = rect.height;
                rect = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y, w, rect.height);
            }
            else if (rect.width < rect.height)
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
            GUI.color = new Color(1f, 1f, 1f, alpha);
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
            float scale = Scale;
            if (s_window != null && Mathf.Abs(scale - s_styleScale) < 0.02f)
            {
                return;
            }

            s_styleScale = scale;
            float s = scale;
            Font font = LoadFont();

            if (s_window == null)
            {
                s_panelTex = MakeSliced(ColPanelFill, ColPanelEdge, ColPanelInner, 36);
                s_buttonTex = MakeSliced(ColButtonFill, new Color(0.46f, 0.39f, 0.24f, 1f), new Color(0.30f, 0.25f, 0.15f, 1f), 20);
                s_buttonHoverTex = MakeSliced(ColButtonHover, new Color(0.70f, 0.58f, 0.33f, 1f), new Color(0.42f, 0.34f, 0.20f, 1f), 20);
                s_buttonActiveTex = MakeSliced(ColButtonActive, new Color(0.82f, 0.68f, 0.39f, 1f), new Color(0.50f, 0.41f, 0.24f, 1f), 20);
                s_chipTex = MakeSliced(new Color(0.10f, 0.083f, 0.061f, 0.94f), new Color(0.42f, 0.36f, 0.22f, 1f), ColCardInner, 16);
                s_ruleTex = MakeSolid(new Color(0.62f, 0.52f, 0.30f, 0.55f));
                s_barBackTex = MakeSolid(new Color(0.08f, 0.07f, 0.05f, 1f));
                s_barFillTex = MakeSolid(new Color(0.76f, 0.62f, 0.31f, 1f));
                s_glowTex = MakeGlow();
            }

            s_window = new GUIStyle(GUI.skin.window);
            s_window.border = new RectOffset(18, 18, 18, 18);
            s_window.normal.background = s_panelTex;
            s_window.onNormal.background = s_panelTex;
            s_window.padding = new RectOffset(18, 18, 18, 18);
            s_window.font = font;

            s_label = new GUIStyle(GUI.skin.label);
            s_label.font = font;
            s_label.fontSize = Mathf.RoundToInt(14f * s);
            s_label.normal.textColor = ColText;
            s_label.wordWrap = true;

            s_labelDim = new GUIStyle(s_label);
            s_labelDim.fontSize = Mathf.RoundToInt(12f * s);
            s_labelDim.normal.textColor = ColTextDim;
            s_labelDim.wordWrap = false;

            s_section = new GUIStyle(s_label);
            s_section.fontSize = Mathf.RoundToInt(12f * s);
            s_section.fontStyle = FontStyle.Bold;
            s_section.normal.textColor = ColTextGold;
            s_section.wordWrap = false;

            s_title = new GUIStyle(s_label);
            s_title.fontSize = Mathf.RoundToInt(16f * s);
            s_title.fontStyle = FontStyle.Bold;
            s_title.normal.textColor = ColTextGold;
            s_title.wordWrap = false;

            s_bigNumber = new GUIStyle(s_label);
            s_bigNumber.fontSize = Mathf.RoundToInt(26f * s);
            s_bigNumber.fontStyle = FontStyle.Bold;
            s_bigNumber.normal.textColor = ColTextGold;
            s_bigNumber.wordWrap = false;

            s_tooltip = new GUIStyle(s_label);
            s_tooltip.fontSize = Mathf.RoundToInt(13f * s);
            s_tooltip.wordWrap = true;
            s_tooltip.normal.textColor = ColText;

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

            BuildCardStyles(s_boardScale > 0f ? s_boardScale : scale, font);
        }

        /// <summary>
        /// Card text style keyed by font size. The board sizes cards by grid cell, so the text has
        /// to follow the card size instead of the rarity; drawn = 1 greys it out, and tintColor
        /// paints the caption in the rarity colour.
        /// </summary>
        private static GUIStyle TextStyle(int fontSize, int drawn, int rarity, bool tintColor)
        {
            int key = fontSize * 32 + drawn * 8 + rarity + (tintColor ? 4 : 0);
            GUIStyle style;
            if (s_textStyles.TryGetValue(key, out style) && style != null)
            {
                return style;
            }

            if (s_font == null)
            {
                s_font = LoadFont();
            }

            style = new GUIStyle(GUI.skin.label);
            style.font = s_font;
            style.fontSize = fontSize;
            style.wordWrap = true;
            style.alignment = TextAnchor.UpperLeft;

            if (drawn == 1)
            {
                style.normal.textColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            }
            else if (tintColor && rarity >= 0 && rarity < RarityPalette.All.Length)
            {
                style.normal.textColor = RarityPalette.All[rarity];
            }
            else
            {
                style.normal.textColor = ColText;
            }

            s_textStyles[key] = style;
            return style;
        }

        private static void BuildCardStyles(float scale, Font font)
        {
            s_cardBg = new GUIStyle[RarityColors.Length];
            s_cardBgDrawn = new GUIStyle[RarityColors.Length];
            s_cardName = new GUIStyle[RarityColors.Length];
            s_cardNameDrawn = new GUIStyle[RarityColors.Length];
            s_cardCaption = new GUIStyle[RarityColors.Length];

            for (int i = 0; i < RarityColors.Length; i++)
            {
                Color rarity = RarityColors[i];
                Texture2D normal = MakeSliced(new Color(0.135f, 0.110f, 0.078f, 0.98f), rarity, new Color(0.22f, 0.18f, 0.12f, 1f), 64);
                Texture2D dim = MakeSliced(
                    new Color(0.085f, 0.085f, 0.085f, 1f),
                    new Color(0.28f, 0.28f, 0.28f, 1f),
                    new Color(0.12f, 0.12f, 0.12f, 1f),
                    64);

                GUIStyle bg = new GUIStyle(GUI.skin.box);
                bg.border = new RectOffset(14, 14, 14, 14);
                bg.normal.background = normal;
                s_cardBg[i] = bg;

                GUIStyle bgDrawn = new GUIStyle(bg);
                bgDrawn.normal.background = dim;
                s_cardBgDrawn[i] = bgDrawn;

                int fs = Mathf.RoundToInt(CardFontSizes[i] * scale);

                GUIStyle name = new GUIStyle(GUI.skin.label);
                name.font = font;
                name.fontSize = fs;
                name.wordWrap = true;
                name.alignment = TextAnchor.UpperLeft;
                name.normal.textColor = ColText;
                s_cardName[i] = name;

                GUIStyle nameDrawn = new GUIStyle(name);
                nameDrawn.normal.textColor = new Color(0.45f, 0.45f, 0.45f, 1f);
                s_cardNameDrawn[i] = nameDrawn;

                GUIStyle caption = new GUIStyle(GUI.skin.label);
                caption.font = font;
                caption.fontSize = Mathf.Max(9, Mathf.RoundToInt(fs * 0.82f));
                caption.wordWrap = false;
                caption.normal.textColor = rarity;
                s_cardCaption[i] = caption;
            }
        }

        private static Texture2D MakeSliced(Color fill, Color border, Color inner, int size)
        {
            int b = Mathf.Max(4, size / 8);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < b || y < b || x >= size - b || y >= size - b;
                    bool isInner = !isBorder && (x == b || y == b || x == size - b - 1 || y == size - b - 1);
                    texture.SetPixel(x, y, isBorder ? border : (isInner ? inner : fill));
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

        private static Texture2D MakeGlow()
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    texture.SetPixel(x, y, new Color(1f, 0.95f, 0.72f, a));
                }
            }

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
