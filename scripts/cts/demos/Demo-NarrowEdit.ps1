<#
.SYNOPSIS
    Demo: touch a *narrow* source file (only some tests cover it) and run CTS
    apply.
    Expectation: partial selection (impacted < total). The edit is reverted
    on exit.

.PARAMETER Project
    Project key from projects.json. Default StringTools.
#>
[CmdletBinding()]
param([string]$Project = 'StringTools')

. (Join-Path $PSScriptRoot '_DemoCommon.ps1')

$proj = Resolve-DemoProject -Key $Project
$file = Get-DemoFile -Project $proj -Kind Narrow

Invoke-DemoApply -Project $proj `
    -Title "[$($proj.Key)] Apply after touching narrow file: $($file.Relative)" `
    -TouchFile $file.Full `
    -ExpectMessage 'impacted > 0 but < total (partial selection)'
