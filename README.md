# Cycle Blessings / 循环抽奖

一个《Against the Storm / 风暴之城》的 BepInEx 5 + ATS_API mod。

每完成一局（胜利），获得 **1 张抽奖券**。在大地图按 **F7** 打开抽奖界面，用券抽一个**世界事件奖励式全局 buff**。buff 在整个世界循环内生效，循环结束（封印完成）时清空。

> 0.3.0 起不再监听单局声望，而是按“对局结束发券”的简单模型工作；抽奖券和转盘都按循环持久化。

---

## 1. 抽奖规则

### 抽奖券

- 定居点**胜利**时 +1 张抽奖券。
- 默认**失败不给券**；可以在配置里打开 `grantTicketOnLoss`。
- 读档/重复触发的对局结束钩子不会重复发券（`CurrentSettlement` 里有“本局已发券”标记）。
- 抽奖券存在当前循环里，循环结束清空。

### 转盘

每次打开界面时，如果还没有转盘，会生成 **15 个选项**：

| 稀有度 | 数量 | 默认奖池 |
| --- | --- | --- |
| Legendary | 1 | 3 个世界事件级强力 buff |
| Epic | 2 | 4 个高级 buff |
| Rare | 4 | 6 个实用 buff |
| Uncommon | 8 | 8 个较小/辅助 buff |

- 抽一次消耗 1 张券，随机抽走一个选项，并**从转盘里剔除**。
- **至少抽过一次后**，可以点“重置转盘”免费重新生成 15 个选项；已经抽到的 buff 不会消失。
- 转盘、已抽到的 buff、抽奖券都保存在当前循环里。
- 抽到的 buff 会在这个循环的**每一座新定居点开局时重新生效**（通过游戏的 `EffectModel.Apply()`，和 world event 奖励同一时机）。

### 默认奖池

**Legendary**

- Mining Automatons / 采矿自动机：每座矿井一个自动机岗位。
- The Elder's Cat（复合）：全局士气 +2，工人双倍产出几率 +5%。
- Guild's Sigil / 公会徽记：商人到达速度 +25%，基石和蓝图价格 -25%。

**Epic**

- Petrified Egg / 石化龙蛋：全局生产速度 +15%。
- Field Engineering Station / 战地工程站（富集燃料配方）。
- The Elder's Cat（+2 士气）。
- Festive Mood / 节日气氛：满足种族专属住房需求的居民额外 +2 士气。

**Rare**

- Guild's Logbook / 公会日志：可以看到未探索空地的内容。
- Blight Bomb / 枯萎炸弹蓝图。
- Field Engineering Station / 战地工程站（燃料棒配方）。
- Syndicate Trader / 辛迪加商人主动能力。
- The Eremite's Way / 隐士之道：蓝图重掷费用 -10 琥珀。
- Playing with Feelings / 感情用事：每季随机种族被偏爱。

**Uncommon**

- 农场产量 +100%。
- 钓鱼小屋速度 +25%。
- 搬运工速度 +25%。
- 金属冶炼生产速度 +33%。
- 房屋容量 +1。
- 采集者营地产量 +50%。
- 草药园产量 +50%。
- 酿酒厂产量 +50%。

默认池全部使用游戏自带的 `EffectTypes` 效果；名字、图标、数值都走原版数据。可以在 `cycle_gacha.json` 里换成任意效果。

---

## 2. 界面

- 大地图（WorldMap）场景按 **F7** 打开/关闭抽奖窗口。
- 窗口顶部显示抽奖券数量和每个稀有度的剩余/初始数量。
- 15 个选项以 5×3 卡片显示，按稀有度着色（橙/紫/蓝/绿）。
- 按钮：
  - **抽取 / Draw**：消耗 1 张券，随机抽走一个选项并显示结果。
  - **重置转盘 / Reset**：至少抽过一次后可用，免费重新生成 15 个选项。
  - **关闭 / Close**。
- 窗口下方会列出本循环已抽到的 buff（重复的显示 `Nx`）。
- 大地图左上角会显示当前抽奖券数量（有券或窗口打开时），点击它也可以打开窗口。
- 界面是 IMGUI 卡片网格（不是带动画的转盘），抽取为即时随机；不依赖游戏原生 UI 层级，会尝试加载微软雅黑/黑体来显示中文。

---

## 3. 安装

1. 安装 **BepInEx 5.4.x**（Thunderstore 的 BepInExPack 即可）。
2. 安装 **ATS_API 3.7.0**（Thunderstore: `ATS_API_Devs-API`）。
3. 把编译好的 `CycleBlessings.dll`、`manifest.json`、`icon.png` 放进：
   `BepInEx/plugins/Forwindz-CycleBlessings/`
4. 启动游戏一次，插件目录下会生成 `cycle_gacha.json`。

---

## 4. 配置说明

`cycle_gacha.json` 位于 `CycleBlessings.dll` 同目录。

```json
{
  "gachaHotkey": "F7",
  "grantTicketOnLoss": false,
  "wheel": {
    "legendary": 1,
    "epic": 2,
    "rare": 4,
    "uncommon": 8
  },
  "pools": {
    "legendary": [
      {
        "id": "legendary_automatons",
        "effect": "WE_Automaton_In_All_Mines",
        "name": null,
        "description": null,
        "weight": 1
      }
    ],
    "epic": [],
    "rare": [],
    "uncommon": []
  }
}
```

字段：

- `gachaHotkey`：大地图打开抽奖窗口的热键，填 `UnityEngine.KeyCode` 的名字，如 `F7`。
- `grantTicketOnLoss`：失败是否也给券，默认 `false`。
- `wheel`：每个稀有度在转盘里的数量，默认 `1/2/4/8`。
- `pools`：每个稀有度的奖池。每个条目：
  - `effect`：**必填**。优先按 `ATS_API.Helpers.EffectTypes` 的枚举名解析（如 `WE_Petrified_Egg`），解析不到时再当作效果内部名（如 `[WE] First Elder Bonus`）。
  - `name` / `description`：可选，留空则直接使用游戏内该效果的名称和描述（推荐留空，自动跟随游戏语言）。
  - `weight`：抽取权重，默认 1；设为 0 可以让该条目永不抽中。

如果某个稀有度池子的条目少于转盘需求数量，会**有放回地重复抽取**来填满；如果池子为空，对应格子会缺失并写日志。改完配置需要重启游戏。

---

## 5. 实现方式

- **发券**：Harmony postfix 挂在 `Eremite.Services.ReputationService` 的 `CheckForWin`（自然胜利）、`ForceGameWin`（胜利结算）和 `GameLost`（失败）上；用 `CurrentSettlement` 标记保证每局最多一张券。
- **循环持久化**：抽奖券、转盘、已抽到的 buff 列表存在 ATS_API 的 `ModSaveData.CurrentCycle`；`WorldCalendarService.OnCycleEnded` 时 API 自动清空。
- **应用 buff**：Harmony postfix 挂在 `Eremite.Services.EffectsService.AddLateInitialEffects` 上（游戏应用 world event / embark 效果的同一阶段），对每个已抽到的效果调 `SO.Settings.GetEffect(name).Apply()`；另有 `OnStartGame` 兜底，用 `CurrentSettlement` 标记防重复。
- **抽奖 UI**：插件 `OnGUI` 里判断当前场景是 `WorldMap`，用 Unity IMGUI 画窗口；热键通过 ATS_API 的 `Hotkeys` 注册。
- **不改原声望/原效果**：没有监听声望；只读对局结果并调用游戏自己的 `EffectModel.Apply()`。

---

## 6. 已知限制 / 需要实机确认的点

这个仓库是在**没有安装游戏本体**的环境里写的，代码按 ATS_API 3.7.0（其枚举由游戏 1.9.3R 生成）和社区 mod 的实际用法对齐，但仍建议第一次实机测试时检查：

1. 日志里这 4 个 patch 是否都出现：
   - `Patched Eremite.Services.ReputationService.CheckForWin`
   - `Patched Eremite.Services.ReputationService.ForceGameWin`
   - `Patched Eremite.Services.ReputationService.GameLost`
   - `Patched Eremite.Services.EffectsService.AddLateInitialEffects`
2. 赢下一局后，日志是否出现 `+1 draw ticket`，大地图左上角是否显示券数。
3. 按 F7 是否能打开窗口；抽奖后日志是否出现 `Gacha draw #...`。
4. 下一局开局后，日志是否出现 `Applied N/M cycle buff(s)`。
5. 如果日志出现 `Could not find effect 'XXX'`，说明配置里的 `effect` 名字在当前游戏版本不存在；用 ATS_API 的 `EffectTypes` 枚举名替换。
6. 失败不发券是默认行为；需要的话把 `grantTicketOnLoss` 改成 `true`。
7. buff 通过 `EffectModel.Apply()` 应用到定居点，不会写进大地图的 World Event 活动列表。

兼容性注意：如果同时使用了会**完全替换** `ReputationService.CheckForWin/ForceGameWin/GameLost` 或 `EffectsService.AddLateInitialEffects` 的作弊类 mod（prefix 返回 `false`、不执行原方法），对应的 postfix 可能不会被执行。

---

## 7. 目录结构

```
ATSCycleBlessings/
├─ CycleBlessings.csproj
├─ Directory.Build.props.example   # 复制为 Directory.Build.props 并填路径
├─ NuGet.Config
├─ Scripts/
│  ├─ Plugin.cs                    # BepInEx 入口、Harmony、热键、OnGUI
│  ├─ Config/ModConfig.cs          # 抽奖券/轮盘/奖池配置，默认 EffectTypes 池
│  ├─ State/CycleState.cs          # 抽奖券、15 格转盘、已抽 buff
│  ├─ State/CycleStateStore.cs     # CurrentCycle 读写、循环重置、对局标记
│  ├─ Gacha/TicketService.cs       # 对局结束发券
│  ├─ Gacha/GachaService.cs        # 生成/抽取/重置转盘
│  ├─ Rewards/BlessingService.cs   # 开局应用 buff、效果名/描述
│  ├─ Tracking/SettlementEndPatch.cs # CheckForWin/ForceGameWin/GameLost postfix
│  ├─ Tracking/EffectsPatch.cs     # AddLateInitialEffects postfix
│  └─ UI/GachaWindow.cs            # 大地图 IMGUI 抽奖界面
└─ ModPackageInfo/
   ├─ manifest.json
   ├─ icon.png
   └─ CHANGELOG.md
```

编译方法见 [HowToBuild.md](HowToBuild.md)。
