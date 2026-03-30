param(
    [Parameter(Mandatory = $true)]
    [string]$SolutionPath
)

$solutionFullPath = Join-Path (Get-Location) $SolutionPath
if (-not (Test-Path $solutionFullPath)) {
    throw "Solution file not found: $SolutionPath"
}

$content = Get-Content -Path $solutionFullPath -Raw

$replacements = @{
    'C:\\Mac\\Home\\Documents\\Workspaces\\Products\\NewHyOn\\Manager\\NewHyOn_Manager\\NewHyOn Manager\.csproj' = 'NewHyOn_Manager\NewHyOn Manager.csproj'
    'C:\\Mac\\Home\\Documents\\Workspaces\\Products\\NewHyOn\\Manager\\NewHyOn_Settings\\NewHyOn Settings\.csproj' = 'NewHyOn_Settings\NewHyOn Settings.csproj'
    'C:\\Mac\\Home\\Documents\\Workspaces\\Products\\NewHyOn\\Player\\Windows\\NewHyOn_Player\\NewHyOn Player\.csproj' = 'NewHyOn_Player\NewHyOn Player.csproj'
}

foreach ($pattern in $replacements.Keys) {
    $content = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        $pattern,
        $replacements[$pattern]
    )
}

[System.IO.File]::WriteAllText($solutionFullPath, $content, [System.Text.UTF8Encoding]::new($true))
