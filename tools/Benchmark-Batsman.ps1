param(
    [ValidateRange(2,120)][int]$Seconds = 6,
    [ValidateRange(1,10)][int]$Cycles = 2,
    [switch]$Uncapped,
    [string]$GodotPath = $env:GODOT4
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $GodotPath) {
    $downloads = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads'
    $candidate = Get-ChildItem -LiteralPath $downloads -Filter '*mono*console.exe' -Recurse -Depth 3 |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($candidate) { $GodotPath = $candidate.FullName }
}
if (-not $GodotPath) { throw 'Pass -GodotPath or set GODOT4 to the Godot .NET runner.' }
Push-Location -LiteralPath $projectRoot
$debugRoot = Join-Path $projectRoot '.godot/mono/temp/bin/Debug'
$releaseRoot = Join-Path $projectRoot '.godot/mono/temp/bin/ExportRelease'
$backups = @{}
try {
    & dotnet build Cricket2D.csproj --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Debug build failed.' }
    & dotnet build Cricket2D.csproj -c ExportRelease --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    # The installed local runner loads Debug. Supply release assemblies for the timed run,
    # then restore the exact original bytes even if the runner fails.
    foreach ($name in @('Cricket2D.dll','Cricket.Physics.dll')) {
        $destination = Join-Path $debugRoot $name
        $backups[$destination] = [IO.File]::ReadAllBytes($destination)
        Copy-Item -LiteralPath (Join-Path $releaseRoot $name) -Destination $destination
    }
    $suffix = if ($Uncapped) { 'uncapped' } else { 'vsync' }
    $reportRelative = "artifacts/performance-batsman-$suffix.json"
    $runnerArguments = @('--path',$projectRoot,'--resolution','3840x2160','--fullscreen',
        '--log-file',(Join-Path $projectRoot "artifacts/performance-batsman-$suffix.log"),'--',
        '--performance-test',"--performance-seconds=$Seconds","--performance-cycles=$Cycles","--performance-output=$reportRelative")
    if ($Uncapped) { $runnerArguments += '--performance-uncapped' }
    $consolePath = Join-Path $projectRoot "artifacts/performance-batsman-$suffix-console.log"
    & $GodotPath @runnerArguments 2>&1 | Tee-Object -FilePath $consolePath
    if ($LASTEXITCODE -ne 0) { throw 'Rendered benchmark failed.' }
    if (Select-String -LiteralPath $consolePath -Pattern 'Fatal error|0xC0000005|^ERROR:' -Quiet) {
        throw 'Rendered benchmark logged a native/runtime error; inspect the console log.'
    }
    $reportPath = Join-Path $projectRoot $reportRelative
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $report | Add-Member -NotePropertyName BuildConfiguration -NotePropertyValue 'ExportRelease'
    $report | Add-Member -NotePropertyName Host -NotePropertyValue 'Local Godot .NET runner; not a packaged export'
    $report | Add-Member -NotePropertyName GameAssemblySha256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $debugRoot 'Cricket2D.dll')).Hash
    $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath
} finally {
    foreach ($destination in $backups.Keys) { [IO.File]::WriteAllBytes($destination,$backups[$destination]) }
    Pop-Location
}
