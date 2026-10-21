# Rebuild CycleGacha and redeploy it into the game.
# Uses the .NET SDK that was installed locally for this task (no system-wide install).
$ErrorActionPreference = 'Stop'

$root   = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = 'C:\game\ATS\_dotnet\dotnet.exe'
$game   = 'C:\Program Files (x86)\Steam\steamapps\common\Against the Storm'

if (-not (Test-Path $dotnet)) {
    throw "Local .NET SDK not found at $dotnet. Install one, or change `$dotnet here."
}

# Directory.Build.props in this folder points StormPath / BepInExPath at the game folder,
# so the build's Deploy target copies the DLL and metadata into
# <game>\BepInEx\plugins\Forwindz-CycleGacha\
& $dotnet build (Join-Path $root 'CycleGacha.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

Write-Host ""
Write-Host "Deployed to: $game\BepInEx\plugins\Forwindz-CycleGacha" -ForegroundColor Green
Get-ChildItem (Join-Path $game 'BepInEx\plugins\Forwindz-CycleGacha') |
    Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host "Launch the game and check BepInEx\LogOutput.log for 'Cycle Gacha v0.5.0 loaded'."
