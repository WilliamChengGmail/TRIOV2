param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$FrameworkDependent,
    [string]$Output = "publish"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\PerformanceAnalysis.App\PerformanceAnalysis.App.csproj"
$outputPath = Join-Path $PSScriptRoot $Output

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Recurse -Force
}

Write-Host "Publishing $project ..." -ForegroundColor Cyan

if ($FrameworkDependent) {
    dotnet publish $project `
        -c Release `
        -r $Runtime `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $outputPath
} else {
    dotnet publish $project `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $outputPath
}

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Published to: $outputPath" -ForegroundColor Green
Get-ChildItem $outputPath | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
