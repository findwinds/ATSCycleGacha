# Cycle Gacha / 循环抽奖

一个《Against the Storm / 风暴之城》的 BepInEx 5 + ATS_API mod。

每赢下一局获得 **10 张抽奖券**。在大地图按 **F7** 打开抽奖界面，抽取**单一物资、单一人口奖励或单一百分比加成**。奖励在整个世界循环内生效，循环结束（封印完成）时清空。

> 当前规则：奖励为单一类型；加成只保留百分比效果；每局 10 张券；进度满 10 点 +1 券并保留溢出；6×6 转盘中央是 2×2 传说卡，抽奖/重置按钮在它正下方；抽走传说卡后才能重置转盘。

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
- **只有抽走传说级奖励后**，才能免费重置转盘，重新生成 31 个选项；已抽到的奖励不会消失。
  如果配置里没有传说级选项，则退回“至少抽过一次后可重置”的旧规则，避免转盘卡死。
- 同稀有度优先不重复；每个奖励都是**单一内容**（一个效果 / 一种物资 / 一组人口），不会出现复合奖励。
- 抽到的奖励会在这个循环的**每一座新定居点开局时重新生效**。

### 默认奖池

每档奖池都是同一组 **21 个候选**：2 种百分比加成 + 19 种物资；物资数量随稀有度变化。
转盘仍按稀有度抽 `1 / 2 / 4 / 8 / 16` 个（共 31 格）。

| 物资 | 普通 | 罕见 | 稀有 | 史诗 | 传说 |
| --- | --- | --- | --- | --- | --- |
| 管道 | 5 | 10 | 20 | 35 | 55 |
| 晶露 | 5 | 10 | 20 | 35 | 55 |
| 琥珀 | 5 | 10 | 20 | 35 | 55 |
| 零部件 | 1 | 3 | 6 | 10 | 15 |
| 砖头 | 5 | 10 | 20 | 35 | 55 |
| 面粉 | 20 | 30 | 50 | 80 | 120 |
| 野火精华 | 1 | 3 | 6 | 10 | 15 |
| 油 | 20 | 30 | 50 | 80 | 120 |
| 稻子 | 20 | 30 | 50 | 80 | 120 |
| 成包的供给品 | 10 | 12 | 15 | 19 | 24 |
| 蘑菇 | 10 | 25 | 45 | 70 | 100 |
| 陶器 | 20 | 30 | 50 | 80 | 120 |
| 靴子 | 10 | 20 | 35 | 55 | 80 |
| 蔬菜 | 10 | 20 | 40 | 80 | 160 |
| 昆虫 | 10 | 20 | 40 | 80 | 160 |
| 木板 | 5 | 10 | 20 | 35 | 55 |
| 工具 | 5 | 10 | 20 | 35 | 55 |
| 石头 | 10 | 20 | 40 | 80 | 160 |
| 蛋 | 20 | 30 | 50 | 80 | 120 |

百分比加成：

| 稀有度 | 生产速度 | 生产翻倍几率 |
| --- | --- | --- |
| 普通 | +1% | +1% |
| 罕见 | +3% | +2% |
| 稀有 | +6% | +3% |
| 史诗 | +10% | +4% |
| 传说 | +15% | +5% |

游戏自带的百分比效果资源只覆盖 5/8/10/15/20/25…（生产速度）和 10/15/20/25/30（翻倍几率），
所以其它数值由 mod 在运行时克隆同类型模板并改写 `amount` 生成，名字形如
`CycleGacha_production_speed_6`、`CycleGacha_double_yield_3`。
---

## 2. 界面

- 大地图（WorldMap）场景按 **F7**，或点击左上角的抽奖券徽标，打开/关闭抽奖窗口。
- 外观沿用游戏原版风格：深色皮革底 + 细金边（程序生成九宫格贴图）、羊皮纸色文字、金色标题，
  卡片显示真实物品图标（`GoodModel.icon` / `EffectModel.GetIcon()`）。
- 转盘是 **6×6 紧凑卡片组**：
  - 传说奖励是一张 **2×2 的大卡**，固定在正中间；
  - 其余 30 张普通卡片填满剩下的 30 个格子；
  - **抽奖**和**重置转盘**按钮放在传说大卡正下方的两个格子里，所以 6×6 的 36 格全部填满。
- 每张卡片的上、下、左、右四条边以及奖励名称文字都按稀有度上色（传说金橙、史诗紫、稀有蓝、罕见绿、普通灰白）；已抽走的卡片文字保持暗灰。
- 抽取有动画：一道高亮光圈沿卡片依次扫过，跳过已抽走的卡位，速度逐渐变慢，最后停在中奖卡上；
  结果显示在窗口下方。
- 被抽中的卡片不会消失，而是变暗灰留在原位，并保留一圈暗化的稀有度颜色边框，这样 6×6
  布局保持完整、动画也有位置可停；这个状态随存档保存。
- 鼠标悬停卡片会显示名称、稀有度、进度值与描述；右侧栏是进度条、各稀有度剩余/总数和已获得奖励列表
  （带图标，重复的显示 `×N`）。

## 3. 安装

1. 安装 **BepInEx 5.4.x**（Thunderstore 的 BepInExPack 即可）。
2. 安装 **ATS_API 3.7.0**（Thunderstore: `ATS_API_Devs-API`）。
3. 把编译好的 `CycleGacha.dll`、`manifest.json`、`icon.png` 放进：
   `BepInEx/plugins/Forwindz-CycleGacha/`
4. 启动游戏一次，插件目录下会生成 `cycle_gacha.json`。

---

## 4. 配置说明

`cycle_gacha.json` 位于 `CycleGacha.dll` 同目录。

```json
{
  "schemaVersion": 6,
  "gachaHotkey": "F7",
  "grantTicketOnLoss": false,
  "ticketsPerSettlement": 10
}
```

字段：

- `schemaVersion`：配置版本，不要手动改；旧版配置会自动备份为 `.old` 并重建。
- `gachaHotkey`：大地图打开抽奖窗口的热键，填 `UnityEngine.KeyCode` 的名字，如 `F7`。
- `grantTicketOnLoss`：失败是否也给券，默认 `false`。
- `ticketsPerSettlement`：每次胜利给多少张券，默认 `10`。

---

## 5. 实现方式

- **发券**：Harmony postfix 挂在 `Eremite.Services.ReputationService` 的 `CheckForWin`（声望达到阈值）、`ForceGameWin`（胜利结算）和 `GameLost`（失败）上；`CurrentSettlement` 标记保证每局最多一次发券。每局发 `ticketsPerSettlement` 张（默认 10）。
- **循环持久化**：抽奖券、进度、转盘、已抽到的奖励 ID 存在 ATS_API 的 `ModSaveData.CurrentCycle`；`WorldCalendarService.OnCycleEnded` 时 API 自动清空（包括券、进度、转盘和已抽内容）。
- **抽奖**：每次从剩余选项里均匀随机一个，标记为已抽走并扣 1 券，按稀有度加进度；满 10 点 +1 券，溢出保留。
- **应用奖励**：不自己 apply，而是把奖励交给游戏自己的循环加成系统（世界事件给循环奖励走的就是这条路）：
  - `effect` → `SO.CycleEffectsService.AddEmbarkEffect(效果内部名)`，写进 `WorldStateService.CycleEffects.bonusEmbarkEffects`
  - `goods` → `SO.CycleEffectsService.ChangeEmbarkGoods(new Good(物资名, 数量))`，写进 `bonusEmbarkGoods`
  - `villagers` → `SO.CycleEffectsService.ChangeEmbarkVillagers(n)`，写进 `bonusEmbarkVillagers`
  - 之后游戏自己完成剩下的：`CaravanGenerator` 把三项抄进车队 → `RegularGameConditionsCreator.SetUpEmbarkBonuses` 拼成 `embarkEffects` / `embarkGoods` → 开局时 `EffectsService.AddLateInitialEffects` 应用加成、`StorageService.StoreInitialGoods` 以 `InitialCaravan` 入库物资、`CaravanGenerator.GetVillagersAmount` 决定初始人口；大地图的车队界面（`CaravanPickSlot`）会列出这些加成。
  - 循环结束时 `CycleEffectsService.OnCycleEnded()` 重建这份状态，所以奖励随循环清空，和本 mod 自己的存档数据同步。
  - 旧版本（0.5.0/0.6.0）自己动手应用的奖励，会在读档后按 `registeredRewardIds` 的差集补登记一次，不会重复。
- **抽奖 UI**：插件 `OnGUI` 里判断当前场景是 `WorldMap`，用 Unity IMGUI 画窗口；热键通过 ATS_API 的 `Hotkeys` 注册。
- **不改原声望/原效果**：没有监听声望；只读对局结果并调用游戏自己的效果/存储/居民服务。

---

## 6. 目录结构

```
ATSCycleGacha/
├─ CycleGacha.csproj
├─ Directory.Build.props.example   # 复制为 Directory.Build.props 并填路径
├─ NuGet.Config
├─ Scripts/
│  ├─ Plugin.cs                    # BepInEx 入口、Harmony、热键、OnGUI
│  ├─ Config/ModConfig.cs          # 运行选项 + 内置 31 格/进度/奖励表
│  ├─ State/CycleState.cs          # 抽奖券、进度、转盘、已抽奖励
│  ├─ State/CycleStateStore.cs     # CurrentCycle 读写、循环重置、对局标记
│  ├─ Gacha/TicketService.cs       # 对局结束发券
│  ├─ Gacha/GachaService.cs        # 生成/抽取/重置转盘、进度与 +1 券
│  ├─ Rewards/RewardService.cs    # 开局应用效果/物资/居民、奖励显示
│  ├─ Tracking/SettlementEndPatch.cs # CheckForWin/ForceGameWin/GameLost postfix
│  ├─ Tracking/SettlementStartPatch.cs # AddLateInitialEffects postfix
│  └─ UI/GachaWindow.cs            # 大地图 IMGUI 抽奖界面
└─ ModPackageInfo/
   ├─ manifest.json
   ├─ icon.png
   └─ CHANGELOG.md
```

编译方法见 [HowToBuild.md](HowToBuild.md)。
