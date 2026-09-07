#requires -Version 7.0

<#
.SYNOPSIS
Checks repository agent-context entry points and local Markdown file links.
.DESCRIPTION
Read-only and offline. Checks required frontmatter fields, skill names, and local
file targets outside code fences. Optional size targets are checked only when
CheckBudgets is supplied. Does not evaluate
instruction semantics, remote URLs, or Markdown anchor compatibility.
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$CheckBudgets
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$errors = [System.Collections.Generic.List[string]]::new()
$entries = [System.Collections.Generic.List[object]]::new()
$markdownFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

function Add-Entry
{
    param(
        [string]$Path,
        [string]$Kind,
        [int]$MaxLines = 100,
        [int]$MaxBytes = 6144
    )

    $entries.Add([pscustomobject]@{ Path = $Path; Kind = $Kind; MaxLines = $MaxLines; MaxBytes = $MaxBytes })
}

Add-Entry -Path (Join-Path $root 'AGENTS.md') -Kind 'root'
Add-Entry -Path (Join-Path $root '.github\copilot-instructions.md') -Kind 'pointer' -MaxLines 20 -MaxBytes 1024

if (Test-Path -LiteralPath (Join-Path $root '.git'))
{
    $pointerPath = Join-Path '.github' 'copilot-instructions.md'
    $trackedPointer = & git -C $root ls-files --stage -- $pointerPath
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Unable to inspect the tracked Copilot instruction file type.'
    }
    if ($trackedPointer -match '^120000 ')
    {
        $errors.Add('The Copilot pointer is still tracked as a symlink. Record its regular-file type; editing an emulated Windows symlink would otherwise commit its prose as a broken link target.')
    }
}

$skillsPath = Join-Path $root '.github\skills'
foreach ($directory in Get-ChildItem -LiteralPath $skillsPath -Directory)
{
    $entry = Join-Path $directory.FullName 'SKILL.md'
    if (!(Test-Path -LiteralPath $entry -PathType Leaf))
    {
        $errors.Add("Missing skill entry point: $entry")
        continue
    }

    Add-Entry -Path $entry -Kind 'skill'
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root '.github\agents') -Filter '*.agent.md' -File)
{
    Add-Entry -Path $file.FullName -Kind 'agent'
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root '.github\instructions') -Filter '*.instructions.md' -File)
{
    Add-Entry -Path $file.FullName -Kind 'instruction' -MaxLines 80 -MaxBytes 4096
}

$pluginsPath = Join-Path $root 'plugins'
if (Test-Path -LiteralPath $pluginsPath -PathType Container)
{
    foreach ($file in Get-ChildItem -LiteralPath $pluginsPath -Filter 'SKILL.md' -File -Recurse)
    {
        Add-Entry -Path $file.FullName -Kind 'skill'
    }
    foreach ($file in Get-ChildItem -LiteralPath $pluginsPath -Filter '*.agent.md' -File -Recurse)
    {
        Add-Entry -Path $file.FullName -Kind 'agent'
    }
}

$skillNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $entries)
{
    $relative = [System.IO.Path]::GetRelativePath($root, $entry.Path)
    if (!(Test-Path -LiteralPath $entry.Path -PathType Leaf))
    {
        $errors.Add("Missing entry point: $relative")
        continue
    }

    $lines = [System.IO.File]::ReadAllLines($entry.Path)
    $text = [System.IO.File]::ReadAllText($entry.Path)
    $bytes = (Get-Item -LiteralPath $entry.Path).Length
    if ($CheckBudgets -and ($lines.Count -gt $entry.MaxLines -or $bytes -gt $entry.MaxBytes))
    {
        $errors.Add("${relative}: $($lines.Count) lines / $bytes bytes exceeds $($entry.MaxLines) lines / $($entry.MaxBytes) bytes; move detail to focused references.")
    }

    if ($entry.Kind -in @('root', 'pointer'))
    {
        continue
    }

    $frontmatter = [regex]::Match($text, '\A---\r?\n(?<body>[\s\S]*?)\r?\n---(?:\r?\n|\z)')
    if (!$frontmatter.Success)
    {
        $errors.Add("${relative}: missing YAML frontmatter.")
        continue
    }

    $header = $frontmatter.Groups['body'].Value
    $requiredFields = if ($entry.Kind -eq 'instruction') { @('applyTo') } else { @('name', 'description') }
    foreach ($field in $requiredFields)
    {
        $fieldMatch = [regex]::Match($header, "(?m)^${field}:[ \t]*(?<value>[^\r\n]*)")
        $value = $fieldMatch.Groups['value'].Value.Trim().Trim('"', "'")
        $emptyBlock = $value -match '^[>|][-+]?$' -and
            ![regex]::IsMatch($header, "(?m)^${field}:[ \t]*[>|][-+]?[ \t]*\r?\n[ \t]+\S")
        if (!$fieldMatch.Success -or [string]::IsNullOrWhiteSpace($value) -or $value.StartsWith('#') -or $emptyBlock)
        {
            $errors.Add("${relative}: missing or empty '$field' metadata.")
        }
    }

    if ($entry.Kind -eq 'skill')
    {
        $nameMatch = [regex]::Match($header, '(?m)^name:[ \t]*(?<name>[^\r\n]+)')
        if ($nameMatch.Success)
        {
            $name = $nameMatch.Groups['name'].Value.Trim().Trim('"', "'")
            $directoryName = Split-Path (Split-Path $entry.Path -Parent) -Leaf
            if ($name -cne $directoryName -or $name -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$')
            {
                $errors.Add("${relative}: skill name '$name' must be lowercase kebab-case and match '$directoryName'.")
            }

            if (!$skillNames.Add($name))
            {
                $errors.Add("${relative}: duplicate skill name '$name'.")
            }
        }
    }
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root '.github') -Filter '*.md' -File -Recurse)
{
    [void]$markdownFiles.Add($file.FullName)
}

if (Test-Path -LiteralPath $pluginsPath -PathType Container)
{
    foreach ($file in Get-ChildItem -LiteralPath $pluginsPath -Filter '*.md' -File -Recurse)
    {
        [void]$markdownFiles.Add($file.FullName)
    }
}

foreach ($relative in @('AGENTS.md', 'eng\common\AGENTS.md', 'src\MSBuildTaskHost\AGENTS.md', 'documentation\agent-context.md'))
{
    $path = Join-Path $root $relative
    if (Test-Path -LiteralPath $path -PathType Leaf)
    {
        [void]$markdownFiles.Add($path)
    }
}

$contextDocs = Join-Path $root 'documentation\agent-context'
if (Test-Path -LiteralPath $contextDocs -PathType Container)
{
    foreach ($file in Get-ChildItem -LiteralPath $contextDocs -Filter '*.md' -File -Recurse)
    {
        [void]$markdownFiles.Add($file.FullName)
    }
}

foreach ($path in $markdownFiles)
{
    $relative = [System.IO.Path]::GetRelativePath($root, $path)
    $directory = Split-Path $path -Parent
    $fenceCharacter = ''
    $fenceLength = 0
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($path))
    {
        $lineNumber++
        $fence = [regex]::Match($line, '^[ \t]*(?<fence>`{3,}|~{3,})')
        if ($fence.Success)
        {
            $marker = $fence.Groups['fence'].Value
            if ($fenceLength -eq 0)
            {
                $fenceCharacter = $marker.Substring(0, 1)
                $fenceLength = $marker.Length
            }
            elseif ($marker.StartsWith($fenceCharacter) -and $marker.Length -ge $fenceLength)
            {
                $fenceLength = 0
            }

            continue
        }

        if ($fenceLength -ne 0)
        {
            continue
        }

        foreach ($link in [regex]::Matches($line, '!?\[[^\]\r\n]*\]\((?<target><[^>\r\n]+>|[^\s)]+)(?:[ \t]+["''][^)]*["''])?\)'))
        {
            $target = $link.Groups['target'].Value.Trim('<', '>')
            if ($target -match '^(?:[a-z][a-z0-9+.-]*:|//|#)' -or $target -match '\{\{|\$\(')
            {
                continue
            }

            $fileTarget = [Uri]::UnescapeDataString(($target -split '[#?]', 2)[0])
            if ([string]::IsNullOrEmpty($fileTarget))
            {
                continue
            }

            $fileTarget = $fileTarget.Replace('\', [System.IO.Path]::DirectorySeparatorChar).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            $resolvedTarget = if ($fileTarget.StartsWith([System.IO.Path]::DirectorySeparatorChar))
            {
                Join-Path $root $fileTarget.TrimStart([System.IO.Path]::DirectorySeparatorChar)
            }
            else
            {
                Join-Path $directory $fileTarget
            }

            if (!(Test-Path -LiteralPath $resolvedTarget))
            {
                $errors.Add("${relative}:${lineNumber}: missing local link target '$target'.")
            }
        }
    }
}

if ($errors.Count -gt 0)
{
    foreach ($message in $errors)
    {
        [Console]::Error.WriteLine($message)
    }

    [Console]::Error.WriteLine("Agent context validation failed: $($errors.Count) issue(s).")
    exit 1
}

Write-Host "Agent context validation passed: $($entries.Count) entry points and $($markdownFiles.Count) Markdown files."
