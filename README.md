# Cycle Blessings / 循环祝福

一个《Against the Storm / 风暴之城》的 BepInEx 5 + ATS_API mod。

它把**指令（Orders）**、**满意度（Resolve）**、**森林事件（Glade Events）**三条声望线分别记账：每条线每累计 **18 点声望**，就从对应奖池里随机获得一个**在整个世界循环内持续生效**的祝福。祝福会在这个循环内每一座新定居点开局时自动发放；循环结束（封印完成）时全部清空。

> 设计目的：让大地图长循环里反复开局的流程有一个持续积累、越来越顺的成长感。

---

## 1. 规则

### 三条独立的声望线

| 维度 | 统计来源 | 奖池 |
| --- | --- | --- |
| 指令 / Orders | 完成指令时直接获得的声望 | 建材、生食、人口 |
| 满意度 / Resolve | 居民满意度突破阈值时产生的声望 | 制品资源、服务物品 |
| 森林事件 / Glade Events | 完成林间空地事件获得的声望 | 金属、零件、工具 |

- 仅统计**正数**声望；扣声望、失败惩罚不会被记录。
- 每条线独立累计。达到 18 点触发 1 次，达到 36 点再触发 1 次，以此类推。
- 同一个祝福可以重复获得并叠加（例如连续两次抽到 +40 木材，就是每局开局 +80）。

### 祝福如何生效

祝福本身是“开局补给包”：

- 抽到后，如果当前定居点已经开局，会**立刻发放一次**（可关闭）。
- 之后这个循环内每一座**新定居点**开局时，都会重新发放一次。
- 读档进入已有定居点不会重复发放，避免刷资源。
- 世界循环结束（古代封印完成）时，三条计数和所有祝福由 ATS_API 自动清空。

### 默认奖池

**指令线（随机 1 个）**

- +40 木材 / +25 石材 / +25 黏土
- +30 根茎 / +20 肉 / +20 蔬菜
- +2 名随机种族居民

**满意度线（随机 1 个）**

- +15 木板 / +12 砖块 / +10 织物
- +8 茶 / +6 酒 / +5 熏香

**森林事件线（随机 1 个）**

- +8 铜锭
- +6 零件
- +10 简单工具

全部数值、条目、权重、阈值都在 `cycle_blessings.json` 里，可以自行改。

---

## 2. 游戏内查看

- 默认按 **F8** 打开当前循环的进度面板：三条线的累计值、已获得祝福列表。
- 每次获得祝福会弹出一个提示框（可关闭）。
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
  "pools": { ... }
}
```

字段：

- `threshold`：每条线触发一次祝福所需的声望，默认 18。
- `grantImmediatelyOnEarn`：抽到祝福时是否在当前定居点立刻发一次。
- `applyBlessingsAtSettlementStart`：新定居点开局是否重新发放全部祝福。
- `announceBlessings`：是否弹祝福提示框。
- `logUnknownReputationSources`：遇到没归类的声望来源时，是否在日志里提示一次（方便你补 token）。
- `statusHotkey`：状态面板热键，填 `UnityEngine.KeyCode` 的名字，如 `F8`；留空关闭。
- `reputationSources`：把游戏内部来源名映射到三个维度。匹配是**不区分大小写的子串匹配**，例如 `"Order"` 可以匹配 `Order`、`OrderReward` 等。
- `pools`：三个维度的奖池。每个条目：
  ```json
  {
    "id": "order_wood",
    "name": "建材补给：木材 / Wood Cache",
    "description": "每座新定居点开局额外获得 40 木材。",
    "goods": [{ "good": "Mat_Raw_Wood", "amount": 40 }],
    "villagers": 0,
    "race": "",
    "weight": 1
  }
  ```
  - `good` 优先按 `ATS_API.Helpers.GoodsTypes` 的枚举名解析（如 `Mat_Raw_Wood`、`Tools_Simple_Tools`）；解析不到时再当作游戏内部物品名（如 `[Mat Raw] Wood`）。
  - `villagers` 大于 0 时发放居民；`race` 留空表示从当前定居点已有的种族里随机。
  - `weight` 是抽取权重，默认 1；设为 0 可以让该条目永不抽中（不用删除它）。

改完配置需要重启游戏（或回到主菜单重新进入）生效。

---

## 5. 实现方式

- **监听声望**：Harmony postfix 挂在 `Eremite.Services.ReputationService.AddReputationPoints(float amount, ReputationChangeSource type, string reason)`。这是游戏所有声望来源的统一入口；通过 `ReputationChangeSource` 的名字（必要时再看 `reason`）归到三条线。
- **循环持久化**：使用 ATS_API 的 `ModdedSaveManager`。状态存在 `ModSaveData.CurrentCycle` 里；ATS_API 在 `WorldCalendarService.OnCycleEnded` 时会把 `CurrentCycle` 换成新的 `SaveData`，正好对应“循环结束清空”。
- **开局发放**：监听 `EventBus.OnStartGame(bool isNewGame)`，只在 `isNewGame == true` 时把该循环已有的祝福发放到新定居点。发放用 `Serviceable.StorageService.Main.Goods.Add(name, amount)` 和 `SO.VillagersService.SpawnNewVillager(race)`。
- **不修改游戏原声望**：只读 `amount` 和来源，不拦截、不改写原方法的执行。
- **兼容 DLC**：奖池和来源都用名字匹配，不写死枚举数值；DLC 新增的指令/事件只要来源名能匹配 token 就会计入。

---

## 6. 已知限制 / 需要实机确认的点

这个仓库是在**没有安装游戏本体**的环境里写的，代码按 ATS_API 现有接口和社区 mod 的用法对齐，但仍建议第一次实机测试时检查：

1. 日志里 `Patched Eremite.Services.ReputationService.AddReputationPoints` 是否出现。
2. 完成一个指令后，日志是否出现 `Earned 1 Orders blessing(s)`。
3. 如果日志出现 `Unclassified reputation source 'XXX'`，说明该来源名不在 `reputationSources` 里；把 `XXX` 加进对应的 token 列表即可。
4. 如果森林事件的来源名不是 `Relic` / `Glade` / `Event` 之一，把日志里的实际名字补进 `reputationSources.glade`。
5. 祝福在开局发放时，确认 `Serviceable.StorageService.Main.Goods` 已就绪；插件会等待最多 30 秒并在日志里报告结果。

兼容性注意：如果同时使用了会**完全替换** `ReputationService.AddReputationPoints` 的作弊/倍率类 mod（即 prefix 返回 `false`、不执行原方法），本 mod 的 postfix 不会被执行，声望不会被统计。普通的数值修改、基石、事件类 mod 不受影响。

当前版本只做“开局资源/人口”类祝福，没有接入游戏的 `EffectModel` 全局修饰符系统。像“全局士气 +1.5”“服务建筑效率 +20%”这类真正改变数值的祝福，建议作为下一版用 ATS_API 的 `HookedEffectBuilder` 单独实现，避免和原版基石/事件修饰符冲突。

---

## 7. 目录结构

```
ATSCycleBlessings/
├─ CycleBlessings.csproj
├─ Directory.Build.props.example   # 复制为 Directory.Build.props 并填路径
├─ NuGet.Config
├─ Scripts/
│  ├─ Plugin.cs                    # BepInEx 入口、Harmony、事件订阅
│  ├─ Config/ModConfig.cs          # 配置模型、默认奖池、来源分类
│  ├─ State/CycleState.cs          # 三维计数 + 已获得祝福
│  ├─ State/CycleStateStore.cs     # CurrentCycle 读写、循环重置
│  ├─ Tracking/ReputationPatch.cs  # AddReputationPoints postfix
│  ├─ Rewards/BlessingService.cs   # 里程碑判定、抽奖、发放
│  └─ UI/PopupNotifier.cs          # F8 状态面板、获得提示
└─ ModPackageInfo/
   ├─ manifest.json
   ├─ icon.png
   └─ CHANGELOG.md
```

编译方法见 [HowToBuild.md](HowToBuild.md)。
