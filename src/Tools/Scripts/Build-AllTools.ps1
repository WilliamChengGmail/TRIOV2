<#
.SYNOPSIS
    TRIOV2 工具套件一鍵自包含（Self-Contained）發佈腳本
.DESCRIPTION
    依據 .agents/rules/tools_directory.md 規範，將所有自研工具全數以
    -r win-x64 --self-contained true 模式編譯發佈至 Tools 目錄，
    確保在無安裝 .NET Runtime / .NET Desktop Runtime 的純淨機台上直接運行。
.AUTHOR
    Office of William
#>

[CmdletBinding()]
param (
    [switch]$SkipClean
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\..\.."

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  TRIOV2 Tools 自包含 (Self-Contained) 批次發佈工具" -ForegroundColor Cyan
Write-Host "  專案根目錄: $root" -ForegroundColor Gray
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

$tools = @(
    @{ Name = "AccountTool";        Proj = "src\Tools\AccountTool\AccountTool.csproj";                      Out = "Tools\AccountTool" },
    @{ Name = "DbInitializer";      Proj = "src\Tools\DbInitializer\DbInitializer.csproj";                  Out = "Tools\DbInitializer" },
    @{ Name = "DbMigrate";          Proj = "src\Tools\DbMigrate\DbMigrate.csproj";                          Out = "Tools\DbMigrate" },
    @{ Name = "QueryEvents";        Proj = "src\Tools\QueryEvents\QueryEvents.csproj";                      Out = "Tools\QueryEvents" },
    @{ Name = "UsbConfigTool";      Proj = "src\Tools\UsbConfigTool\UsbConfigTool.csproj";                  Out = "Tools\UsbConfigTool" },
    @{ Name = "UuidTool";           Proj = "src\Tools\UuidTool\UuidTool.csproj";                            Out = "Tools\UuidTool" },
    @{ Name = "ServiceTestTool";    Proj = "src\Tools\ServiceTestTool\ServiceTestTool.csproj";              Out = "Tools\ServiceTestTool" },
    @{ Name = "DevLauncher";        Proj = "src\Tools\DevLauncher\DevLauncher.csproj";                      Out = "Tools\DevLauncher_bin" },
    @{ Name = "Simulator";          Proj = "src\Tools\TRIOV2.Simulator\TRIOV2.Simulator.csproj";            Out = "Tools\Simulator_bin" },
    @{ Name = "PrivilegedService";  Proj = "src\TRIOV2.PrivilegedService\TRIOV2.PrivilegedService.csproj";  Out = "Tools\PrivilegedService" },
    @{ Name = "VisionSimulator";    Proj = "src\Tools\TRIOV2.VisionSimulator\TRIOV2.VisionSimulator.csproj";Out = "Tools\VisionSimulator" },
    @{ Name = "ResourceSimulator";  Proj = "src\Tools\TRIOV2.ResourceSimulator\TRIOV2.ResourceSimulator.csproj"; Out = "Tools\ResourceSimulator" }
)

$count = 0
$total = $tools.Count

foreach ($t in $tools) {
    $count++
    $name = $t.Name
    $projPath = Join-Path $root $t.Proj
    $outPath = Join-Path $root $t.Out

    Write-Host "[$count/$total] 正在發佈 $name (Self-Contained)..." -ForegroundColor Yellow
    Write-Host "       專案: $($t.Proj)" -ForegroundColor Gray
    Write-Host "       目標: $($t.Out)" -ForegroundColor Gray

    if (-not (Test-Path $projPath)) {
        Write-Error "找不到專案檔: $projPath"
        continue
    }

    # 預防性處理：若現有執行檔或 DLL 正被背景行程鎖定，先重新命名為 .old 避免 MSB3027 鎖定衝突
    if (Test-Path $outPath) {
        Get-ChildItem -Path $outPath -Filter "*.exe" -File -ErrorAction SilentlyContinue | ForEach-Object {
            $oldName = "$($_.FullName).old"
            Remove-Item $oldName -Force -ErrorAction SilentlyContinue
            Rename-Item -Path $_.FullName -NewName "$($_.Name).old" -Force -ErrorAction SilentlyContinue
        }
        Get-ChildItem -Path $outPath -Filter "*.dll" -File -ErrorAction SilentlyContinue | ForEach-Object {
            $oldName = "$($_.FullName).old"
            Remove-Item $oldName -Force -ErrorAction SilentlyContinue
            Rename-Item -Path $_.FullName -NewName "$($_.Name).old" -Force -ErrorAction SilentlyContinue
        }
    }

    # 執行 dotnet publish
    $cmd = "dotnet publish `"$projPath`" -c Release -r win-x64 --self-contained true -o `"$outPath`""
    Invoke-Expression $cmd
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$name 發佈失敗！ExitCode: $LASTEXITCODE"
        continue
    }

    # 嘗試清理 .old 檔案
    if (Test-Path $outPath) {
        Get-ChildItem -Path $outPath -Filter "*.old" -File -ErrorAction SilentlyContinue | ForEach-Object {
            Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
        }
    }

    # 瘦身清理: 移除非 Windows runtimes
    $runtimesDir = Join-Path $outPath "runtimes"
    if (Test-Path $runtimesDir) {
        Get-ChildItem -Path $runtimesDir -Directory | Where-Object { $_.Name -notlike "win*" } | ForEach-Object {
            Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    # 排除 pdb 符號檔以節省機台體積
    Get-ChildItem -Path $outPath -Filter "*.pdb" -Recurse -File | ForEach-Object {
        Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
    }

    # 計算發佈體積
    $sizeMb = [Math]::Round(((Get-ChildItem -Path $outPath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB), 2)
    Write-Host "       ✅ 發佈完成，體積: $sizeMb MB" -ForegroundColor Green
    Write-Host ""
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  所有 Tools 自包含發佈作業全數完成！" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Cyan

