# Cycle Blessings / 循环抽奖

一个《Against the Storm / 风暴之城》的 BepInEx 5 + ATS_API mod。

每赢下一局获得 **10 张抽奖券**。在大地图按 **F7** 打开抽奖界面，抽取**单一物资、单一人口奖励或单一百分比加成**。奖励在整个世界循环内生效，循环结束（封印完成）时清空。

> 0.5.0：奖励收敛为单一类型（不允许复合奖励）；加成只保留百分比效果；物资/人口按稀有度用你指定的数值；进度条满 10 点改为 **+1 抽奖券**，溢出保留到下一轮；每局 10 张券。

---

## 1. 抽奖规则

### 抽奖券与进度条

- 定居点**胜利**时 +**10 张抽奖券**（`ticketsPerSettlement` 可改）。
- 默认**失败不给券**；可以打开 `grantTicketOnLoss`。
- 每抽一次，按照抽到的稀有度累积 **10 点进度条**：

| 稀有度 | 进度 |
| --- | --- |
| Legendary | +5 |
| Epic | +4 |
| Rare | +3 |
| Uncommon | +2 |
| Common | +1 |

- 进度达到 10 点 → **抽奖券 +1**；超出的部分保留到下一轮（例如 9/10 抽到 Rare +3 → 变成 2/10，并 +1 券）。
- 抽奖券、进度、转盘、已抽到的奖励都按循环持久化，循环结束清空。

### 转盘

每次打开界面时，如果还没有转盘，会生成 **31 个选项**：

| 稀有度 | 数量 | 默认内容 |
| --- | --- | --- |
| Legendary | 1 | +25% 生产速度 / 生食 +50 / 建材 +20 / 人口 +5 |
| Epic | 2 | +20% 生产速度 / 生食 +40 / 建材 +15 / 人口 +4 |
| Rare | 4 | +15% 生产速度 / 生食 +30 / 建材 +10 / 人口 +3 |
| Uncommon | 8 | +10% 生产速度 / 生食 +20 / 建材 +5 / 人口 +2 |
| Common | 16 | +5% 生产速度 / 生食 +10 / 建材 +1 / 人口 +1 / 加工品 +1 / 服务品 +1 / 铜锭 +1 / 工具 +1 |

- 抽一次消耗 1 张券，随机抽走一个选项并**从转盘剔除**。
- **至少抽过一次后**，可以免费重置转盘，重新生成 31 个选项；已抽到的奖励不会消失。
- 同稀有度优先不重复；每个奖励都是**单一内容**（一个效果 / 一种物资 / 一组人口），不会出现复合奖励。
- 抽到的奖励会在这个循环的**每一座新定居点开局时重新生效**。

### 默认奖池（按稀有度）

**Legendary（8 个候选，抽 1 个）**

- 全局生产速度 +25%。
- 生食 +50：根茎 / 肉 / 蔬菜（分别单独条目）。
- 建材 +20：木材 / 石材 / 黏土（分别单独条目）。
- 人口 +5（随机种族）。

**Epic（8 个候选，抽 2 个）**

- 全局生产速度 +20%。
- 生食 +40：根茎 / 肉 / 蔬菜。
- 建材 +15：木材 / 石材 / 黏土。
- 人口 +4。

**Rare（8 个候选，抽 4 个）**

- 全局生产速度 +15%。
- 生食 +30：根茎 / 肉 / 蔬菜。
- 建材 +10：木材 / 石材 / 黏土。
- 人口 +3。

**Uncommon（8 个候选，抽 8 个，全部出现）**

- 全局生产速度 +10%。
- 生食 +20：根茎 / 肉 / 蔬菜。
- 建材 +5：木材 / 石材 / 黏土。
- 人口 +2。

**Common（16 个候选，抽 16 个，全部出现）**

- 全局生产速度 +5%。
- 生食 +10：根茎 / 肉 / 蔬菜。
- 建材 +1：木材 / 石材 / 黏土。
- 人口 +1。
- 加工品 +1：木板 / 砖块 / 织物。
- 服务品 +1：茶 / 酒 / 熏香。
- 铜锭 +1。
- 简单工具 +1。

百分比加成的数值严格对应 `Global_Production_Faster_5/10/15/20/25`；物资/人口数值严格对应上面列出的每档数量。每个奖励只包含一种内容。

---

## 2. 界面

- 大地图（WorldMap）场景按 **F7**，或点击左上角券数，打开/关闭抽奖窗口。
- 窗口顶部显示抽奖券数量和每个稀有度的剩余/初始数量。
- 31 个选项以 5 列卡片网格显示（可滚动），按稀有度着色：
  - Legendary 橙、Epic 紫、Rare 蓝、Uncommon 绿、Common 灰白边框。
  - 每张卡显示抽到后增加的进度（`+N`）。
- 按钮：
  - **抽取 / Draw**：消耗 1 张券；抽中的选项剔除并显示结果。
  - **重置转盘 / Reset**：至少抽过一次后可用，免费重新生成 31 个选项。
  - **关闭 / Close**。
- 进度条：显示当前 `x/10`；满 10 点自动 +1 张抽奖券，溢出保留。
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
  "schemaVersion": 3,
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
    "legendary": 5,
    "epic": 4,
    "rare": 3,
    "uncommon": 2,
    "common": 1
  },
  "pools": {
    "legendary": [
      {
        "id": "legendary_percent",
        "effect": "Global_Production_Faster_25",
        "goods": [],
        "villagers": 0,
        "race": null,
        "name": null,
        "description": null,
        "weight": 1
      }
    ]
  }
}
```

字段：

- `schemaVersion`：配置版本，不要手动改；旧版配置会自动备份为 `.old` 并重建默认配置（0.5.0 的奖励结构变了，需要重新填自定义池）。
- `gachaHotkey`：大地图打开抽奖窗口的热键，填 `UnityEngine.KeyCode` 的名字，如 `F7`。
- `grantTicketOnLoss`：失败是否也给券，默认 `false`。
- `ticketsPerSettlement`：每次胜利给多少张券，默认 `10`。
- `wheel`：每个稀有度在转盘里的数量，默认 `1/2/4/8/16`（共 31）。
- `charge`：每个稀有度抽到时给进度条加多少点，默认 `5/4/3/2/1`（满 10 点 +1 券）。
- `pools`：每个稀有度的奖池。每个奖励条目**只能填一种内容**：
  - `effect`：可选，`EffectTypes` 枚举名（如 `Global_Production_Faster_5`）或效果内部名。
  - `goods`：可选，**最多一个** `GoodAmount`；`good` 填 `GoodsTypes` 枚举名（如 `Food_Raw_Roots`）或内部名，`amount` 为数量。
  - `villagers` / `race`：可选，居民数量与种族；`race` 留空表示从当前定居点已有种族随机。
  - `name` / `description`：可选显示文本；留空时，效果类用游戏内文本，物资/人口类自动拼出摘要。
  - `weight`：抽取权重，默认 1；设为 0 可以让该条目永不抽中。

同时填 `effect`、`goods`、`villagers` 中两项或以上的条目会被视为复合奖励并跳过；`goods` 超过一个的条目也会被跳过。改完配置需要重启游戏。

---

## 5. 实现方式

- **发券**：Harmony postfix 挂在 `Eremite.Services.ReputationService` 的 `CheckForWin`（声望达到阈值）、`ForceGameWin`（胜利结算）和 `GameLost`（失败）上；`CurrentSettlement` 标记保证每局最多一次发券。每局发 `ticketsPerSettlement` 张（默认 10）。
- **循环持久化**：抽奖券、进度、转盘、已抽到的奖励 ID 存在 ATS_API 的 `ModSaveData.CurrentCycle`；`WorldCalendarService.OnCycleEnded` 时 API 自动清空（包括券、进度、转盘和已抽内容）。
- **抽奖**：每次从剩余选项里均匀随机一个，剔除后扣 1 券，按稀有度加进度；满 10 点 +1 券，溢出保留。
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
4. 下一局开局后，日志是否出现 `Applied N/M cycle reward(s)`；物资/人口/百分比加成是否到账。
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
│  ├─ Config/ModConfig.cs          # 券数/31 格转盘/进度/单类型奖池
│  ├─ State/CycleState.cs          # 抽奖券、进度、转盘、已抽奖励
│  ├─ State/CycleStateStore.cs     # CurrentCycle 读写、循环重置、对局标记
│  ├─ Gacha/TicketService.cs       # 对局结束发券
│  ├─ Gacha/GachaService.cs        # 生成/抽取/重置转盘、进度与 +1 券
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
