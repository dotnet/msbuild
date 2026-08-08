<#
.SYNOPSIS
    Files localization feedback in CEINTL for a validated OneLoc pull request.

.DESCRIPTION
    Uses the current Microsoft Entra identity to retrieve the CEINTL feedback
    work item template, find an existing item for the pull request, or create a
    new item. The GitHub workflow obtains the identity through OIDC; this script
    does not accept or persist a PAT or client secret.

.PARAMETER ContextPath
    Path to the trusted JSON context produced by the GitHub workflow.

.PARAMETER Team
    CEINTL team name or ID that owns the feedback work item template.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ContextPath,

    [Parameter(Mandatory = $true)]
    [string]$Team,

    [string]$Organization = 'ceapex',

    [string]$Project = 'CEINTL',

    [guid]$TemplateId = '9a48bdd1-98da-4592-8d8d-c52c0856301d'
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

$AzureDevOpsResource = '499b84ac-1321-427f-aa17-267ca6975798'

function Invoke-AzureDevOpsRequest
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Get', 'Post')]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Uri,

        [string]$Body,

        [string]$ContentType = 'application/json'
    )

    $parameters = @{
        Method = $Method
        Uri = $Uri
        Headers = @{ Authorization = "Bearer $script:AccessToken" }
        ContentType = $ContentType
    }
    if ($Body)
    {
        $parameters.Body = $Body
    }

    Invoke-RestMethod @parameters
}

function Set-WorkflowOutput
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ($env:GITHUB_OUTPUT)
    {
        Add-Content -Path $env:GITHUB_OUTPUT -Value "$Name=$Value"
    }
    else
    {
        Write-Host "$Name=$Value"
    }
}

if (-not (Test-Path -LiteralPath $ContextPath -PathType Leaf))
{
    throw "Context file '$ContextPath' does not exist."
}
if ([string]::IsNullOrWhiteSpace($Team))
{
    throw 'CEINTL_TEAM is not configured in the ceintl-ticketing GitHub environment.'
}
if (-not (Get-Command az -ErrorAction SilentlyContinue))
{
    throw "Azure CLI was not found. The workflow must run 'azure/login' before this script."
}

$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json
$prNumber = [int]$context.prNumber
$expectedPrUrl = "https://github.com/dotnet/msbuild/pull/$prNumber"
if ($prNumber -le 0 -or $context.prUrl -ne $expectedPrUrl)
{
    throw "The context does not identify a dotnet/msbuild pull request."
}
if ([string]::IsNullOrWhiteSpace($context.reviewBody))
{
    throw 'The context does not contain an expert-review finding.'
}

$script:AccessToken = (& az account get-access-token --resource $AzureDevOpsResource --query accessToken --output tsv --only-show-errors)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($script:AccessToken))
{
    throw 'Could not obtain a Microsoft Entra token for Azure DevOps.'
}

$encodedOrganization = [Uri]::EscapeDataString($Organization)
$encodedProject = [Uri]::EscapeDataString($Project)
$encodedTeam = [Uri]::EscapeDataString($Team)
$baseUri = "https://dev.azure.com/$encodedOrganization/$encodedProject"
$witApiBaseUri = "$baseUri/_apis/wit"

$template = Invoke-AzureDevOpsRequest -Method Get -Uri "$baseUri/$encodedTeam/_apis/wit/templates/$TemplateId`?api-version=7.1"
if ([string]::IsNullOrWhiteSpace($template.workItemTypeName))
{
    throw "CEINTL template '$TemplateId' did not specify a work item type."
}

$dedupTag = "GitHubPR-dotnet-msbuild-$prNumber"
$escapedDedupTag = $dedupTag.Replace("'", "''")
$escapedWorkItemType = ([string]$template.workItemTypeName).Replace("'", "''")
$wiql = @{
    query = @"
SELECT [System.Id]
FROM WorkItems
WHERE [System.TeamProject] = @project
  AND [System.WorkItemType] = '$escapedWorkItemType'
  AND [System.Tags] CONTAINS '$escapedDedupTag'
ORDER BY [System.CreatedDate] DESC
"@
} | ConvertTo-Json
$queryResult = Invoke-AzureDevOpsRequest -Method Post -Uri "$witApiBaseUri/wiql?api-version=7.1" -Body $wiql
$existing = $null
foreach ($existingReference in $queryResult.workItems)
{
    $candidate = Invoke-AzureDevOpsRequest -Method Get -Uri "$witApiBaseUri/workitems/$($existingReference.id)?api-version=7.1&`$expand=fields"
    $candidateTags = [string]$candidate.fields.'System.Tags' -split ';' |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ }
    if ($candidateTags -contains $dedupTag)
    {
        $existing = $candidate
        break
    }
}
if ($existing)
{
    Set-WorkflowOutput -Name 'work-item-id' -Value ([string]$existing.id)
    Set-WorkflowOutput -Name 'work-item-url' -Value ([string]$existing._links.html.href)
    Set-WorkflowOutput -Name 'created' -Value 'false'
    return
}

$fieldResponse = Invoke-AzureDevOpsRequest -Method Get -Uri "$witApiBaseUri/fields?api-version=7.1"
$writableFields = @{}
foreach ($field in $fieldResponse.value)
{
    if (-not $field.readOnly -or $field.referenceName -in @('System.AreaPath', 'System.IterationPath'))
    {
        $writableFields[$field.referenceName] = $true
    }
}

$fields = @{}
foreach ($property in $template.fields.PSObject.Properties)
{
    if ($writableFields.ContainsKey($property.Name))
    {
        $fields[$property.Name] = $property.Value
    }
}

$encodedReview = [System.Net.WebUtility]::HtmlEncode([string]$context.reviewBody)
$encodedFiles = $context.files |
    ForEach-Object { "<li><code>$([System.Net.WebUtility]::HtmlEncode([string]$_))</code></li>" }
$description = @"
<p>An automated MSBuild review found a possible localization problem in
<a href="$expectedPrUrl">dotnet/msbuild#$prNumber</a>.</p>
<p><strong>Base:</strong> $([System.Net.WebUtility]::HtmlEncode([string]$context.baseRef))<br/>
<strong>Head:</strong> $([System.Net.WebUtility]::HtmlEncode([string]$context.headRef))</p>
<p><strong>Changed localization files:</strong></p>
<ul>$($encodedFiles -join '')</ul>
<p><strong>Expert review:</strong> <a href="$([System.Net.WebUtility]::HtmlEncode([string]$context.reviewUrl))">GitHub review</a></p>
<pre>$encodedReview</pre>
"@

$templateTags = @()
if ($fields.ContainsKey('System.Tags') -and $fields['System.Tags'])
{
    $templateTags = [string]$fields['System.Tags'] -split ';'
}
$fields['System.Title'] = "[dotnet/msbuild#$prNumber] OneLocBuild localization feedback"
$fields['System.Description'] = $description
$fields['System.Tags'] = (@($templateTags) + @($dedupTag, 'OneLocBuild', 'GitHub automation') |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ } |
        Select-Object -Unique) -join '; '

$patch = [System.Collections.Generic.List[object]]::new()
foreach ($fieldName in ($fields.Keys | Sort-Object))
{
    $patch.Add(@{
            op = 'add'
            path = "/fields/$fieldName"
            value = $fields[$fieldName]
        })
}

$workItemType = [Uri]::EscapeDataString([string]$template.workItemTypeName)
$createUri = "$witApiBaseUri/workitems/`$${workItemType}?api-version=7.1"
$created = Invoke-AzureDevOpsRequest `
    -Method Post `
    -Uri $createUri `
    -Body ($patch | ConvertTo-Json -Depth 20) `
    -ContentType 'application/json-patch+json'

Set-WorkflowOutput -Name 'work-item-id' -Value ([string]$created.id)
Set-WorkflowOutput -Name 'work-item-url' -Value ([string]$created._links.html.href)
Set-WorkflowOutput -Name 'created' -Value 'true'
