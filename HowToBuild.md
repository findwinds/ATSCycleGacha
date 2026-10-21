# How to build

## Requirements

- Windows
- .NET SDK 6/7/8（能构建 `netstandard2.0` 即可；也可以用 Visual Studio 2022）
- 已安装《Against the Storm》
- 已安装 BepInEx 5.4.x（Thunderstore 的 `BepInExPack`）
- 已安装 ATS_API 3.7.0（Thunderstore: `ATS_API_Devs-API`）

## 1. 配置路径

把 `Directory.Build.props.example` 复制为 `Directory.Build.props`，填上本机路径：

```xml
<Project>
  <PropertyGroup>
    <!-- 包含 BepInEx\plugins 的目录，通常是 Thunderstore profile 根目录 -->
    <BepInExPath>C:/Users/你的用户名/AppData/Roaming/Thunderstore Mod Manager/DataFolder/AgainstTheStorm/profiles/Default</BepInExPath>
    <!-- 游戏安装目录，里面应有 Against the Storm_Data -->
    <StormPath>D:/Steam/steamapps/common/Against the Storm</StormPath>
  </PropertyGroup>
</Project>
```

也可以直接编辑 `.csproj` 里的 `$(StormPath)` / `$(BepInExPath)`，或者用环境变量传入。

## 2. 构建

```powershell
dotnet build -c Release
```

构建成功后，`Deploy` target 会把这些文件复制到：

```
$(BepInExPath)\BepInEx\plugins\Forwindz-CycleGacha\
```

包括 `CycleGacha.dll`、`manifest.json`、`icon.png`、`CHANGELOG.md`、`README.md`。

## 3. 运行前检查

1. 游戏目录下已安装 BepInEx，且 `BepInEx/plugins/ATS_API_Devs-API/API.dll` 存在。
2. `BepInEx/LogOutput.log` 中能看到：
   - `Cycle Gacha v0.5.0 loaded`
   - `Patched Eremite.Services.ReputationService.CheckForWin`
   - `Patched Eremite.Services.ReputationService.ForceGameWin`
   - `Patched Eremite.Services.ReputationService.GameLost`
   - `Patched Eremite.Services.EffectsService.AddLateInitialEffects`
3. 赢下一局，检查日志是否出现 `+10 draw ticket(s)`；回到大地图按 F7 打开抽奖窗口。
4. 抽一次后检查日志 `Gacha draw #...`，下一局开局检查 `Applied N/M cycle reward(s)`。

## 4. 常见构建问题

- **找不到 Assembly-CSharp.dll**：`StormPath` 没填对。注意路径里是 `Against the Storm_Data/Managed/Assembly-CSharp.dll`。
- **找不到 API.dll**：`BepInExPath` 应指向**包含 BepInEx 文件夹的 profile 根目录**，不是 `BepInEx` 本身。目标路径是 `<BepInExPath>\BepInEx\plugins\ATS_API_Devs-API\API.dll`。
- **BepInEx 包还原失败**：确认 `NuGet.Config` 里的 `https://nuget.bepinex.dev/v3/index.json` 可访问。
- **编译通过但游戏里没效果**：先看 `BepInEx/LogOutput.log` 里是否有 patch 失败或 `Could not find effect` 日志。
