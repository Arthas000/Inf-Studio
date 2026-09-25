[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [string]$UnityPath = "",
    [string]$OutputPath = ""
)
$ErrorActionPreference = "Stop"
$ProjectPath = (Resolve-Path $ProjectPath).Path
$versionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
if (!(Test-Path $versionFile)) { throw "Choose the actual Unity project root (Assets + Packages + ProjectSettings), not the source ZIP directory." }
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $line = Get-Content $versionFile | Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
    $version = ($line -replace '^m_EditorVersion: ', '').Trim()
    $candidates = @(
        "$env:ProgramFiles\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "${env:ProgramFiles(x86)}\Unity\Hub\Editor\$version\Editor\Unity.exe"
    )
    $UnityPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (!$UnityPath) { throw "Unity $version was not found at the usual Hub location. Supply -UnityPath with your real Editor\Unity.exe path." }
}
$UnityPath = (Resolve-Path $UnityPath).Path
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ProjectPath ("Builds\InFalsusStudio_Windows_0.9_" + (Get-Date -Format "yyyyMMdd_HHmmss"))
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if ((Test-Path $OutputPath) -and (Get-ChildItem -Force $OutputPath | Select-Object -First 1)) { throw "Output directory must be new or empty." }
$log = Join-Path $ProjectPath ("InFalsus-Build-"+(Get-Date -Format "yyyyMMdd_HHmmss")+".log")
$oldOutput = $env:INFALSUS_BUILD_OUTPUT
try {
    $env:INFALSUS_BUILD_OUTPUT = $OutputPath
    Write-Host "Close this Unity project in the Editor before continuing. The built-in audio modules and Windows Build Support must be enabled."
    & $UnityPath -batchmode -nographics -quit -projectPath $ProjectPath -buildTarget Win64 -executeMethod InFalsusStudio.Editor.StudioBuild08.BuildBatch -logFile $log
    $code = $LASTEXITCODE
    $exe = Join-Path $OutputPath "InFalsusStudio.exe"
    if ($code -ne 0 -or !(Test-Path $exe)) { throw "Unity build failed (exit $code). Read $log. No executable success is claimed." }
    Write-Host "Build succeeded: $exe"
    Write-Host "Keep the ENTIRE output directory. BuildReport.txt and CoreChecks.txt describe this local build."
    $archive = $OutputPath + ".zip"
    if (Test-Path $archive) { throw "Archive already exists: $archive" }
    Compress-Archive -Path $OutputPath -DestinationPath $archive
    Write-Host "Complete Windows directory archived: $archive"
}
finally { $env:INFALSUS_BUILD_OUTPUT = $oldOutput }
