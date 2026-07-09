<#
.SYNOPSIS
    Demo: touch a *broad* source file (used by most tests) and run CTS apply.
    Expectation: most or all tests are selected. The edit is reverted on exit.

.PARAMETER Project
    Project key from projects.json. Default StringTools.
#>
[CmdletBinding()]
param([string]$Project = 'StringTools')

. (Join-Path $PSScriptRoot '_DemoCommon.ps1')

$proj = Resolve-DemoProject -Key $Project
$file = Get-DemoFile -Project $proj -Kind Broad

Invoke-DemoApply -Project $proj `
    -Title "[$($proj.Key)] Apply after touching broad file: $($file.Relative)" `
    -TouchFile $file.Full `
    -ExpectMessage 'impacted ~= total (most/all selected)'
