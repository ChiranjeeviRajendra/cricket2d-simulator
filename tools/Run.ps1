param(
    [string]$GodotPath = $env:GODOT4,
    [switch]$BuildOnly,
    [switch]$Test,
    [switch]$Headless,
    [switch]$SmokeTest,
    [switch]$BattingPreview,
    [switch]$FourK,
    [switch]$PerformanceTest
)
$ErrorActionPreference = 'Stop'
if ($PerformanceTest -and ($Headless -or $BattingPreview -or $SmokeTest)) {
    throw 'PerformanceTest needs the rendered game; do not combine it with Headless, BattingPreview or SmokeTest.'
}
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet-home'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

if (-not $GodotPath) {
    $candidate = Get-Command godot, godot4 -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($candidate) { $GodotPath = $candidate.Source }
}
if (-not $GodotPath) {
    $downloads = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads'
    $candidate = Get-ChildItem -LiteralPath $downloads -Filter '*mono*console.exe' -Recurse -Depth 3 -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($candidate) { $GodotPath = $candidate.FullName }
}
if (-not $GodotPath -or -not (Test-Path -LiteralPath $GodotPath)) {
    throw 'Godot .NET was not found. Pass -GodotPath C:\path\to\Godot_mono_console.exe or set GODOT4.'
}
$packageSource = Join-Path (Split-Path -Parent $GodotPath) 'GodotSharp\Tools\nupkgs'
$localPackages = Join-Path $projectRoot '.local\godot-packages'
New-Item -ItemType Directory -Path $localPackages -Force | Out-Null
if (Test-Path -LiteralPath $packageSource) {
    foreach ($package in Get-ChildItem -LiteralPath $packageSource -Filter '*.nupkg') {
        $destination = Join-Path $localPackages $package.Name
        if (-not (Test-Path -LiteralPath $destination)) { Copy-Item -LiteralPath $package.FullName -Destination $destination }
    }
}
# The installed Godot .NET distribution supplies all four dependencies, so no app install is needed.
& dotnet build Cricket2D.csproj --nologo -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
if ($Test) {
    & dotnet run --project Tests/Cricket.Physics.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
    if ($LASTEXITCODE -ne 0) { throw 'Physics validation failed.' }
}
if ($BuildOnly) { exit 0 }
# Import the sky texture before runtime, including when launching a fresh checkout.
& $GodotPath --headless --editor --path $projectRoot --import
if ($LASTEXITCODE -ne 0) { throw 'Godot asset import failed.' }
$godotArguments = @('--path', $projectRoot)
if ($Headless) { $godotArguments += '--headless' }
if ($FourK -or $PerformanceTest) { $godotArguments += @('--resolution', '3840x2160', '--fullscreen') }
$userArguments = @()
if ($BattingPreview) { $userArguments += '--batting-preview' }
if ($SmokeTest) { $userArguments += '--smoke-test' }
if ($PerformanceTest) {
    $userArguments += '--performance-test'
}
if ($userArguments.Count -gt 0) { $godotArguments += @('--') + $userArguments }
& $GodotPath @godotArguments
exit $LASTEXITCODE
