param(
    [ValidateSet("default", "manager", "player")]
    [string]$StartAppsProfile = "default",

    [string]$Configuration = "Release",

    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Join-Path $ScriptDir "StartApps.csproj"
$PublishRoot = Join-Path $ScriptDir "bin/publish"
$OutputDir = Join-Path $PublishRoot $StartAppsProfile

if (-not (Test-Path $ProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

$targetFileName = switch ($StartAppsProfile) {
    "manager" { "StartApps.Manager.exe" }
    "player" { "StartApps.Player.exe" }
    default { "StartApps.exe" }
}

Write-Host "[publish] project: $ProjectPath"
Write-Host "[publish] configuration: $Configuration"
Write-Host "[publish] runtime: $RuntimeIdentifier"
Write-Host "[publish] profile: $StartAppsProfile"
Write-Host "[publish] publish dir: $OutputDir"

Remove-Item -Recurse -Force $OutputDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

& dotnet publish $ProjectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -o $OutputDir `
    "/p:StartAppsProfile=$StartAppsProfile" `
    "/p:PublishSingleFile=true" `
    "/p:PublishReadyToRun=true" `
    "/p:IncludeNativeLibrariesForSelfExtract=true" `
    "/p:EnableCompressionInSingleFile=true" `
    "/p:DebugType=None" `
    "/p:DebugSymbols=false"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for profile '$StartAppsProfile'."
}

$publishedExecutablePath = Join-Path $OutputDir "StartApps.exe"
if (-not (Test-Path $publishedExecutablePath)) {
    throw "Published executable not found: $publishedExecutablePath"
}

$targetExecutablePath = Join-Path $OutputDir $targetFileName
if (-not [string]::Equals($publishedExecutablePath, $targetExecutablePath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Remove-Item -Force $targetExecutablePath -ErrorAction SilentlyContinue
    Move-Item -Path $publishedExecutablePath -Destination $targetExecutablePath
}

Write-Host ""
Write-Host "[publish] output executable:"
Write-Host $targetExecutablePath
Write-Host ""

Get-ChildItem $OutputDir | Format-Table Name, Length, LastWriteTime -AutoSize
