<#
.SYNOPSIS
    Demo: touch a source file that this project does NOT depend on and run
    CTS apply.
    Expectation: 0 tests selected. The edit is reverted on exit.

.PARAMETER Project
    Project key from projects.json. Default StringTools.
#>
[CmdletBinding()]
param([string]$Project = 'StringTools')

. (Join-Path $PSScriptRoot '_DemoCommon.ps1')

$proj = Resolve-DemoProject -Key $Project
$file = Get-DemoFile -Project $proj -Kind Unrelated

Invoke-DemoApply -Project $proj `
    -Title "[$($proj.Key)] Apply after touching unrelated file: $($file.Relative)" `
    -TouchFile $file.Full `
    -ExpectMessage 'impacted = 0 (unrelated file does not affect this project)'
