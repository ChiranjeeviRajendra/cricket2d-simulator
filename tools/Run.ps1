param(
    [string]$GodotPath = $env:GODOT4,
    [switch]$BuildOnly,
    [switch]$Test,
    [switch]$Headless,
    [switch]$SmokeTest,
    [switch]$BattingPreview
)
$ErrorActionPreference = 'Stop'
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
    Get-ChildItem -LiteralPath $packageSource -Filter '*.nupkg' | Copy-Item -Destination $localPackages -Force
}
# The installed Godot .NET distribution supplies all four dependencies, so no app install is needed.
& dotnet build Cricket2D.csproj --nologo -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
if ($Test) {
    & dotnet run --project Tests/Cricket.Physics.Tests.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
    if ($LASTEXITCODE -ne 0) { throw 'Physics validation failed.' }
}
if ($BuildOnly) { exit 0 }
$godotArguments = @('--path', $projectRoot)
if ($Headless) { $godotArguments += '--headless' }
$userArguments = @()
if ($BattingPreview) { $userArguments += '--batting-preview' }
if ($SmokeTest) { $userArguments += '--smoke-test' }
if ($userArguments.Count -gt 0) { $godotArguments += @('--') + $userArguments }
& $GodotPath @godotArguments
exit $LASTEXITCODE
