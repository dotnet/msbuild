<#
.SYNOPSIS
    Demo: run CTS apply with NO working-tree changes.
    Expectation: 0 tests selected, 0 executed.

.PARAMETER Project
    Project key from projects.json. Default StringTools.
#>
[CmdletBinding()]
param([string]$Project = 'StringTools')

. (Join-Path $PSScriptRoot '_DemoCommon.ps1')

$proj = Resolve-DemoProject -Key $Project

Invoke-DemoApply -Project $proj `
    -Title "[$($proj.Key)] Apply with NO working-tree changes" `
    -ExpectMessage 'impacted = 0, executed = 0'
