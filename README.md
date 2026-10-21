# Cycle Blessings / 循环增益

一个《Against the Storm / 风暴之城》的 BepInEx 5 + ATS_API mod。

它把**指令（Orders）**、**满意度（Resolve）**、**森林事件（Glade Events）**三条声望线分别记账：每条线每累计 **18 点声望**，就获得一个**世界事件奖励式的全局 buff**。buff 会在当前循环内**每一座新定居点开局时重新生效**，循环结束（封印完成）时全部清空。

> 注意：0.2.0 起奖励不再是“开局资源”，而是真正的 `EffectModel` 全局增益——也就是完成 world event 后游戏会发给你的那类 buff。

---

## 1. 规则

### 三条独立的声望线

| 维度 | 统计来源 | 默认 buff 池主题 |
| --- | --- | --- |
| 指令 / Orders | 完成指令时直接获得的声望 | 经济、生产、贸易 |
| 满意度 / Resolve | 居民满意度突破阈值时产生的声望 | 全局士气、住房、种族偏好 |
| 森林事件 / Glade Events | 完成林间空地事件获得的声望 | 探索、空地、科技 |

- 仅统计**正数**声望；扣声望、失败惩罚不会被记录。
- 每条线独立累计。达到 18 点触发 1 次，达到 36 点再触发 1 次，以此类推。
- 同一个 buff 可以重复获得；每次获得都会再次 `Apply()`，是否叠加取决于该效果本身。
- 世界循环结束（古代封印完成）时，三条计数和所有已获得 buff 由 ATS_API 自动清空。

### buff 如何生效

获得 buff 时：

1. 如果当前定居点已经开局，会**立刻 `Apply()` 一次**（可关闭）。
2. 这个 buff 会记入当前循环的 buff 列表。
3. 之后每一座**新定居点**开局时，游戏在 `EffectsService.AddLateInitialEffects` 阶段会把列表里的每个 buff 再 `Apply()` 一次。

这和游戏自己处理 world event 奖励的方式一致：游戏把世界事件 buff 存在 `WorldStateService.Cycle.activeEventsEffects`，并在 `EffectsService.AddLateInitialEffects` 里逐条 `EffectModel.Apply()`。本 mod 在同一个时机应用自己的 buff 列表。

- 读档进入已有定居点不会重复应用（`CurrentSettlement` 里有“本局已应用”标记）。
- buff 通过游戏自己的 `EffectModel` 生效；多数世界事件效果会出现在定居点的增益/基石面板里，F8 面板也会列出本循环已获得的 buff。

### 默认 buff 池

默认池全部使用游戏自带的 `[WE]` 世界事件效果，因此文字、图标和数值都是原版的：

**指令线（随机 1 个）**

- **Petrified Egg / 石化龙蛋**：全局生产速度 +15%。
- **Guild's Sigil / 公会徽记**：商人到达速度 +25%，基石和蓝图价格 -25%。
- **The Eremite's Way / 隐士之道**：蓝图重掷费用 -10 琥珀。
- **Mining Automatons / 采矿自动机**：每座矿井永久占用一个自动机岗位。

**满意度线（随机 1 个）**

- **The Elder's Cat / 长老的猫（+2 士气）**：全局士气 +2。
- **The Elder's Cat / 长老的猫（复合）**：全局士气 +2，工人双倍产出几率 +5%。
- **Festive Mood / 节日气氛**：满足种族专属住房需求的居民额外 +2 士气。
- **Playing with Feelings / 感情用事**：每个季节随机一个种族被偏爱。

**森林事件线（随机 1 个）**

- **Guild's Logbook / 公会日志**：可以看到未探索空地的内容。
- **Blight Bomb / 枯萎炸弹**：解锁枯萎炸弹蓝图。
- **Field Engineering Station / 战地工程站**：解锁带燃料棒配方的临时工程站。
- **Syndicate Trader / 辛迪加商人**：获得召唤辛迪加商人的主动能力。

这些都可以在 `cycle_blessings.json` 里替换成任意 `EffectTypes` 效果（不限于 `[WE]`），比如原版基石、服务建筑加成、生产加成等。

---

## 2. 游戏内查看

- 默认按 **F8** 打开当前循环的进度面板：三条线的累计值、已获得 buff 列表。
- 每次获得 buff 会弹出一个提示框，显示游戏内的 buff 名称和描述（可关闭）。
- 热键和提示都可以在配置里关掉。

---

## 3. 安装

1. 安装 **BepInEx 5.4.x**（Thunderstore 的 BepInExPack 即可）。
2. 安装 **ATS_API 3.7.0**（Thunderstore: `ATS_API_Devs-API`）。
3. 把编译好的 `CycleBlessings.dll`、`manifest.json`、`icon.png` 放进：
   `BepInEx/plugins/Forwindz-CycleBlessings/`
4. 启动游戏一次，插件目录下会生成 `cycle_blessings.json`。

---

## 4. 配置说明

`cycle_blessings.json` 位于 `CycleBlessings.dll` 同目录。

```json
{
  "threshold": 18.0,
  "grantImmediatelyOnEarn": true,
  "applyBlessingsAtSettlementStart": true,
  "announceBlessings": true,
  "logUnknownReputationSources": true,
  "statusHotkey": "F8",
  "reputationSources": {
    "orders": ["Order"],
    "resolve": ["Resolve"],
    "glade": ["Relic", "Glade", "Event"]
  },
  "pools": {
    "orders": [
      {
        "id": "order_petrified_egg",
        "effect": "WE_Petrified_Egg",
        "name": null,
        "description": null,
        "weight": 1
      }
    ],
    "resolve": [],
    "glade": []
  }
}
```

字段：

- `threshold`：每条线触发一次 buff 所需的声望，默认 18。
- `grantImmediatelyOnEarn`：抽到 buff 时是否在当前定居点立刻应用一次。
- `applyBlessingsAtSettlementStart`：新定居点开局是否重新应用全部 buff。
- `announceBlessings`：是否弹获得提示。
- `logUnknownReputationSources`：遇到没归类的声望来源时，是否在日志里提示一次（方便你补 token）。
- `statusHotkey`：状态面板热键，填 `UnityEngine.KeyCode` 的名字，如 `F8`；留空关闭。
- `reputationSources`：把游戏内部来源名映射到三个维度。匹配是**不区分大小写的子串匹配**，例如 `"Order"` 可以匹配 `Order`、`OrderReward` 等。
- `pools`：三个维度的奖池。每个条目：
  - `effect`：**必填**。优先按 `ATS_API.Helpers.EffectTypes` 的枚举名解析（如 `WE_Petrified_Egg`、`WE_First_Elder_Cat_Resolve`）；解析不到时再当作效果内部名（如 `[WE] First Elder Bonus`）。`EffectTypes` 的名字可以在 ATS_API 的 `Scripts/Helpers/Enums/EffectTypes.cs` 里查，或者用调试 mod 的 Perks 面板查看。
  - `name` / `description`：可选，留空则直接使用游戏内该效果的名称和描述（推荐留空，自动跟随游戏语言）。
  - `weight`：抽取权重，默认 1；设为 0 可以让该条目永不抽中（不用删除它）。

改完配置需要重启游戏（或回到主菜单重新进入）生效。

---

## 5. 实现方式

- **监听声望**：Harmony postfix 挂在 `Eremite.Services.ReputationService.AddReputationPoints(float amount, ReputationChangeSource type, string reason)`。这是游戏所有声望来源的统一入口；通过 `ReputationChangeSource` 的名字（必要时再看 `reason`）归到三条线。
- **循环持久化**：使用 ATS_API 的 `ModdedSaveManager`。计数和 buff 列表存在 `ModSaveData.CurrentCycle` 里；ATS_API 在 `WorldCalendarService.OnCycleEnded` 时会把 `CurrentCycle` 换成新的 `SaveData`，正好对应“循环结束清空”。
- **应用 buff**：Harmony postfix 挂在 `Eremite.Services.EffectsService.AddLateInitialEffects` 上，这是游戏应用 world event / embark 效果的同一个阶段。对列表里的每个效果调用 `Serviceable.Settings.GetEffect(name).Apply()`。
- **立即生效**：抽到 buff 时如果已经在局内，直接 `EffectModel.Apply()`。
- **不修改游戏原声望/原效果**：只读声望和来源，不改写原方法；buff 通过游戏自己的 `EffectModel.Apply()` 生效。
- **兼容 DLC**：奖池和来源都用名字匹配，不写死枚举数值；DLC 新增的指令/事件只要来源名能匹配 token 就会计入。

---

## 6. 已知限制 / 需要实机确认的点

这个仓库是在**没有安装游戏本体**的环境里写的，代码按 ATS_API 3.7.0（其枚举由游戏 1.9.3R 生成）和社区 mod 的实际用法对齐，但仍建议第一次实机测试时检查：

1. 日志里 `Patched Eremite.Services.ReputationService.AddReputationPoints` 和 `Patched Eremite.Services.EffectsService.AddLateInitialEffects` 是否都出现。
2. 完成一个指令后，日志是否出现 `Earned 1 Orders buff(s)`。
3. 新定居点开局后，日志是否出现 `Applied N/M cycle buff(s)`。
4. 如果日志出现 `Unclassified reputation source 'XXX'`，把 `XXX` 加进 `reputationSources` 对应维度。
5. 如果日志出现 `Could not find effect 'XXX'`，说明配置里的 `effect` 名字在当前游戏版本不存在；用 ATS_API 的 `EffectTypes` 枚举名替换。
6. 少数 world event 效果可能只适合在特定事件/地图阶段使用；默认池已经挑了通用、正面的效果。如果某个效果应用后没有明显变化，换一个即可。

兼容性注意：如果同时使用了会**完全替换** `ReputationService.AddReputationPoints` 或 `EffectsService.AddLateInitialEffects` 的作弊/倍率类 mod（即 prefix 返回 `false`、不执行原方法），本 mod 对应的 postfix 可能不会被执行。普通数值修改、基石、事件类 mod 不受影响。

---

## 7. 目录结构

```
ATSCycleBlessings/
├─ CycleBlessings.csproj
├─ Directory.Build.props.example   # 复制为 Directory.Build.props 并填路径
├─ NuGet.Config
├─ Scripts/
│  ├─ Plugin.cs                    # BepInEx 入口、Harmony、事件订阅
│  ├─ Config/ModConfig.cs          # 配置模型、默认 [WE] buff 池、来源分类
│  ├─ State/CycleState.cs          # 三维计数 + 已获得 effect 列表
│  ├─ State/CycleStateStore.cs     # CurrentCycle 读写、循环重置
│  ├─ Tracking/ReputationPatch.cs  # AddReputationPoints postfix
│  ├─ Tracking/EffectsPatch.cs     # AddLateInitialEffects postfix
│  ├─ Rewards/BlessingService.cs   # 里程碑判定、抽 buff、Apply
│  └─ UI/PopupNotifier.cs          # F8 状态面板、获得提示
└─ ModPackageInfo/
   ├─ manifest.json
   ├─ icon.png
   └─ CHANGELOG.md
```

编译方法见 [HowToBuild.md](HowToBuild.md)。
