# scripts/cts/demos/_DemoCommon.ps1
# Shared helpers for the demo scripts. Keep tiny.

. (Join-Path $PSScriptRoot '../_Common.ps1')

function Resolve-DemoProject {
    param([Parameter(Mandatory)] [string]$Key)
    $proj = (Get-Projects -Filter $Key)[0]
    if (-not (Test-Path $script:BaselineDir) -or
        -not (Get-ChildItem -Path $script:BaselineDir -Recurse -ErrorAction SilentlyContinue)) {
        throw "No baseline at $script:BaselineDir. Run .\Collect-Local.ps1 -Project $Key first."
    }
    return $proj
}

function Get-DemoFile {
    param(
        [Parameter(Mandatory)] $Project,
        [Parameter(Mandatory)] [ValidateSet('Broad','Narrow','Unrelated')] [string]$Kind
    )
    if (-not $Project.DemoFiles) {
        throw "Project '$($Project.Key)' has no DemoFiles defined in projects.json."
    }
    $rel = $Project.DemoFiles.$Kind
    if (-not $rel) {
        throw "Project '$($Project.Key)' has no DemoFiles.$Kind defined in projects.json."
    }
    $full = Join-Path $script:RepoRoot $rel
    if (-not (Test-Path $full)) {
        throw "DemoFile '$rel' does not exist in the repo."
    }
    return [pscustomobject]@{ Relative = $rel; Full = $full }
}

function Invoke-DemoApply {
    param(
        [Parameter(Mandatory)] $Project,
        [Parameter(Mandatory)] [string]$Title,
        [string]$TouchFile,
        [string]$ExpectMessage
    )

    Write-Host ''
    Write-Host ('-' * 72) -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host ('-' * 72) -ForegroundColor Cyan
    if ($ExpectMessage) { Write-Host "  expected: $ExpectMessage" -ForegroundColor DarkGray }

    if ($TouchFile) {
        Add-Content -Path $TouchFile -Value ''
        Write-Host "  + appended one blank line to $([IO.Path]::GetRelativePath($script:RepoRoot, $TouchFile))" -ForegroundColor Yellow
    }

    try {
        & (Join-Path $PSScriptRoot '../Run-Local.ps1') -Project $Project.Key -SkipBuild -TimeoutMinutes 5 | Out-Null
        $log = Join-Path $script:LogsDir "apply-$($Project.Key).log"
        Get-Content $log -Tail 30 |
            Where-Object { $_ -match 'impacted test\(s\):|executed:|succeeded:|failed:|reason ' } |
            ForEach-Object { Write-Host "  $_" -ForegroundColor Green }
    }
    finally {
        if ($TouchFile) {
            Push-Location $script:RepoRoot
            try { git checkout -- $TouchFile | Out-Null }
            finally { Pop-Location }
        }
    }
}
