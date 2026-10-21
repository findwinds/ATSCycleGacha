# Cycle Blessings / 循环抽奖

一个《Against the Storm / 风暴之城》的 BepInEx 5 + ATS_API mod。

每赢下一局获得 **10 张抽奖券**。在大地图按 **F7** 打开抽奖界面，用券抽取**物资、全局加成效果或人口**。奖励在整个世界循环内生效，循环结束（封印完成）时清空。

> 0.4.0：奖励只保留物资 / 加成效果 / 人口，去掉蓝图、配方、主动能力等；新增 16 格 Common 档；每局 10 张券；抽奖累积 10 点免费抽进度。

---

## 1. 抽奖规则

### 抽奖券与免费抽

- 定居点**胜利**时 +**10 张抽奖券**（`ticketsPerSettlement` 可改）。
- 默认**失败不给券**；可以打开 `grantTicketOnLoss`。
- 每抽一次，按照抽到的稀有度累积 **免费抽进度**（10 点满）：
  - Legendary +10
  - Epic +5
  - Rare +3
  - Uncommon +2
  - Common +1
- 进度满 10 点 → 获得 1 次免费抽；免费抽同样会继续累积进度。
- 抽奖券、免费抽次数、进度都按循环持久化，循环结束清空。

### 转盘

每次打开界面时，如果还没有转盘，会生成 **31 个选项**：

| 稀有度 | 数量 | 默认内容 |
| --- | --- | --- |
| Legendary | 1 | 最强效果 / 超大物资 / +8 居民 |
| Epic | 2 | 强力效果 / 大物资 / +5 居民 |
| Rare | 4 | 较强效果 / 中物资 / +3 居民 |
| Uncommon | 8 | 中等效果 / 加工物资 / +2 居民 |
| Common | 16 | 小物资 / +1 居民 / 小型全局加成 |

- 抽一次消耗 1 张券（有免费抽时优先消耗免费抽），随机抽走一个选项并**从转盘剔除**。
- **至少抽过一次后**，可以免费重置转盘，重新生成 31 个选项；已抽到的奖励不会消失。
- 同稀有度优先不重复；池子不够时才会有放回重复。
- 抽到的奖励会在这个循环的**每一座新定居点开局时重新生效**。

### 默认奖池

**Legendary**

- 采矿自动机（每座矿井一个自动机岗位）。
- 长老的猫（复合）：全局士气 +2，工人双倍产出几率 +5%。
- 传奇物资：+250 木材、+150 根茎、+30 木板（每座新定居点）。
- 传奇移民：+8 名随机种族居民（每座新定居点）。

**Epic**

- 石化龙蛋：全局生产速度 +15%。
- 公会徽记：商人到达速度 +25%，基石和蓝图价格 -25%。
- 农场 +150%。
- 大型物资：+150 木材、+100 肉、+20 木板。
- 大型移民：+5 名随机种族居民。

**Rare**

- 长老的猫：全局士气 +2。
- 节日气氛：满足种族专属住房需求的居民额外 +2 士气。
- 感情用事：每季随机种族被偏爱。
- 伐木营地 +100%。
- 小农场 +150%。
- 稀有物资：+80 木材、+60 根茎、+10 木板。
- 稀有移民：+3 名随机种族居民。

**Uncommon**

- 农场 +100%。
- 金属冶炼生产速度 +33%。
- 草药园 +50%。
- 酿酒厂 +50%。
- 小农场 +100%。
- 常用物资：+40 木材、+30 根茎、+5 木板。
- 加工物资：+8 木板、+6 砖块、+5 织物。
- 新居民：+2 名随机种族居民。

**Common（16 格，灰白边框）**

- 木材/石材/黏土/根茎/肉/蔬菜/木板/砖块/织物 小补给。
- +1 名随机种族居民。
- 钓鱼小屋速度 +25%。
- 搬运工速度 +25%。
- 农场 +50%。
- 伐木营地 +50%。
- 采集者营地 +50%。
- 小农场 +50%。
- 房屋容量 +1。
- 全局士气 +1。

Common 池共有 18 个候选，每轮转盘从中抽 16 个不重复的。

默认池只使用物资、`EffectTypes` 全局效果和人口；**不包含蓝图、配方、主动能力、空地信息**等不适合作为抽奖奖励的内容。

---

## 2. 界面

- 大地图（WorldMap）场景按 **F7**，或点击左上角券数，打开/关闭抽奖窗口。
- 窗口顶部显示抽奖券数量、免费抽次数和每个稀有度的剩余/初始数量。
- 31 个选项以 5 列卡片网格显示（可滚动），按稀有度着色：
  - Legendary 橙、Epic 紫、Rare 蓝、Uncommon 绿、Common 灰白边框。
- 按钮：
  - **抽取 / Draw**：优先用免费抽，否则消耗 1 张券；抽中的选项剔除并显示结果。
  - **重置转盘 / Reset**：至少抽过一次后可用，免费重新生成 31 个选项。
  - **关闭 / Close**。
- 抽奖进度条：显示当前 `x/10` 充能；满 10 点自动转为 1 次免费抽。
- 窗口下方列出本循环已获得的奖励（重复的显示 `Nx`）。
- 界面是 IMGUI 卡片网格（不是带动画的转盘），抽取为即时随机；会尝试加载微软雅黑/黑体显示中文。

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
  "schemaVersion": 2,
  "gachaHotkey": "F7",
  "grantTicketOnLoss": false,
  "ticketsPerSettlement": 10,
  "wheel": {
    "legendary": 1,
    "epic": 2,
    "rare": 4,
    "uncommon": 8,
    "common": 16
  },
  "charge": {
    "legendary": 10,
    "epic": 5,
    "rare": 3,
    "uncommon": 2,
    "common": 1
  },
  "pools": {
    "legendary": [
      {
        "id": "legendary_supplies",
        "effect": null,
        "goods": [
          { "good": "Mat_Raw_Wood", "amount": 250 },
          { "good": "Food_Raw_Roots", "amount": 150 }
        ],
        "villagers": 0,
        "race": null,
        "name": "传奇物资 / Legendary Supplies",
        "description": "每座新定居点开局 +250 木材、+150 根茎。",
        "weight": 1
      }
    ]
  }
}
```

字段：

- `schemaVersion`：配置版本，不要手动改；旧版配置会自动备份为 `.old` 并重建默认配置。
- `gachaHotkey`：大地图打开抽奖窗口的热键，填 `UnityEngine.KeyCode` 的名字，如 `F7`。
- `grantTicketOnLoss`：失败是否也给券，默认 `false`。
- `ticketsPerSettlement`：每次胜利给多少张券，默认 `10`。
- `wheel`：每个稀有度在转盘里的数量，默认 `1/2/4/8/16`（共 31）。
- `charge`：每个稀有度抽到时给免费抽进度加多少点，默认 `10/5/3/2/1`（10 点 = 1 次免费抽）。
- `pools`：每个稀有度的奖池。每个奖励条目：
  - `effect`：可选，`EffectTypes` 枚举名（如 `WE_First_Elder_Bonus`）或效果内部名（如 `[WE] First Elder Bonus`）。
  - `goods`：可选，物资列表；`good` 填 `GoodsTypes` 枚举名（如 `Mat_Raw_Wood`）或内部名，`amount` 为数量。
  - `villagers` / `race`：可选，居民数量与种族；`race` 留空表示从当前定居点已有种族随机。
  - `name` / `description`：可选显示文本；留空时，效果类用游戏内文本，物资/人口类自动拼出摘要。
  - `weight`：抽取权重，默认 1；设为 0 可以让该条目永不抽中。

一个奖励条目可以同时有 `effect`、`goods`、`villagers`，但通常只填其中一种。如果某个稀有度池子的条目少于转盘需求数量，会有放回地重复抽取；池子为空时对应格子缺失并写日志。改完配置需要重启游戏。

---

## 5. 实现方式

- **发券**：Harmony postfix 挂在 `Eremite.Services.ReputationService` 的 `CheckForWin`（自然胜利/声望达到阈值）、`ForceGameWin`（胜利结算）和 `GameLost`（失败）上；`CurrentSettlement` 标记保证每局最多一次发券。每局发 `ticketsPerSettlement` 张（默认 10）。
- **循环持久化**：抽奖券、免费抽、进度、转盘、已抽到的奖励 ID 存在 ATS_API 的 `ModSaveData.CurrentCycle`；`WorldCalendarService.OnCycleEnded` 时 API 自动清空。
- **抽奖**：每次从剩余选项里均匀随机一个，剔除后按稀有度加充能；满 10 点转成 1 次免费抽。
- **应用奖励**：Harmony postfix 挂在 `Eremite.Services.EffectsService.AddLateInitialEffects` 上（游戏应用 world event / embark 效果的同一阶段）：
  - `effect` → `SO.Settings.GetEffect(name).Apply()`
  - `goods` → `Serviceable.StorageService.Main.Goods.Add(name, amount)`
  - `villagers` → `SO.VillagersService.SpawnNewVillager(race)`
  - 另有 `OnStartGame` 兜底，用 `CurrentSettlement` 标记防重复。
- **抽奖 UI**：插件 `OnGUI` 里判断当前场景是 `WorldMap`，用 Unity IMGUI 画窗口；热键通过 ATS_API 的 `Hotkeys` 注册。
- **不改原声望/原效果**：没有监听声望；只读对局结果并调用游戏自己的效果/存储/居民服务。

---

## 6. 已知限制 / 需要实机确认的点

这个仓库是在**没有安装游戏本体**的环境里写的，代码按 ATS_API 3.7.0（其枚举由游戏 1.9.3R 生成）和社区 mod 的实际用法对齐，但仍建议第一次实机测试时检查：

1. 日志里这 4 个 patch 是否都出现：
   - `Patched Eremite.Services.ReputationService.CheckForWin`
   - `Patched Eremite.Services.ReputationService.ForceGameWin`
   - `Patched Eremite.Services.ReputationService.GameLost`
   - `Patched Eremite.Services.EffectsService.AddLateInitialEffects`
2. 赢下一局后，日志是否出现 `+10 draw ticket(s)`，大地图左上角是否显示券数。
3. 按 F7 是否能打开窗口；抽奖后日志是否出现 `Gacha draw #... (charge +...)`。
4. 下一局开局后，日志是否出现 `Applied N/M cycle reward(s)`；物资/人口/效果是否到账。
5. 如果日志出现 `Could not find effect 'XXX'` 或 `Unknown good 'XXX'`，说明配置名字在当前游戏版本不存在；用 ATS_API 的 `EffectTypes` / `GoodsTypes` 枚举名替换。
6. 失败不发券是默认行为；需要的话把 `grantTicketOnLoss` 改成 `true`。
7. 界面是 31 格 IMGUI 卡片网格（可滚动），不是动画转盘；buff 不会写进大地图的 World Event 活动列表。

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
│  ├─ Config/ModConfig.cs          # 券数/31 格转盘/充能/奖池配置
│  ├─ State/CycleState.cs          # 抽奖券、免费抽、进度、转盘、已抽奖励
│  ├─ State/CycleStateStore.cs     # CurrentCycle 读写、循环重置、对局标记
│  ├─ Gacha/TicketService.cs       # 对局结束发券
│  ├─ Gacha/GachaService.cs        # 生成/抽取/重置转盘、充能与免费抽
│  ├─ Rewards/BlessingService.cs   # 开局应用效果/物资/居民、奖励显示
│  ├─ Tracking/SettlementEndPatch.cs # CheckForWin/ForceGameWin/GameLost postfix
│  ├─ Tracking/EffectsPatch.cs     # AddLateInitialEffects postfix
│  └─ UI/GachaWindow.cs            # 大地图 IMGUI 抽奖界面
└─ ModPackageInfo/
   ├─ manifest.json
   ├─ icon.png
   └─ CHANGELOG.md
```

编译方法见 [HowToBuild.md](HowToBuild.md)。
