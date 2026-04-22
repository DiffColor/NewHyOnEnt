param(
    [string]$ReleaseTag = "runtime-assets",
    [string]$AssetName = "mpv-2.dll.zip",
    [string]$TargetPath = "Player/Windows/NewHyOn_Player/lib/mpv-2.dll"
)

$ErrorActionPreference = "Stop"

$repo = $env:GITHUB_REPOSITORY
if ([string]::IsNullOrWhiteSpace($repo)) {
    $remoteUrl = git config --get remote.origin.url
    if ($remoteUrl -match "github\.com[:/](?<owner>[^/]+)/(?<name>[^/.]+)(\.git)?$") {
        $repo = "$($Matches.owner)/$($Matches.name)"
    }
}

if ([string]::IsNullOrWhiteSpace($repo)) {
    throw "GitHub repository could not be resolved."
}

$tempRoot = $env:RUNNER_TEMP
if ([string]::IsNullOrWhiteSpace($tempRoot)) {
    $tempRoot = [System.IO.Path]::GetTempPath()
}

$downloadDir = Join-Path $tempRoot "newhyon-runtime-assets"
$targetDir = Split-Path -Parent $TargetPath
$assetPath = Join-Path $downloadDir $AssetName

Remove-Item -Recurse -Force $downloadDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $downloadDir | Out-Null
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

gh release download $ReleaseTag --repo $repo --pattern $AssetName --dir $downloadDir --clobber

if (!(Test-Path $assetPath)) {
    throw "Runtime asset was not downloaded: $assetPath"
}

Expand-Archive -Path $assetPath -DestinationPath $targetDir -Force

if (!(Test-Path $TargetPath)) {
    throw "Runtime file was not restored: $TargetPath"
}

$runtimeFile = Get-Item $TargetPath
if ($runtimeFile.Length -le 0) {
    throw "Runtime file is empty: $TargetPath"
}

Write-Host "Restored $TargetPath ($($runtimeFile.Length) bytes) from $ReleaseTag/$AssetName."
