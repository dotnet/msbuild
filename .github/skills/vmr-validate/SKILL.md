---
name: vmr-validate
description: Queue the VMR source-build validation pipeline for the current branch. Use when the user says /vmr-validate or asks to run VMR validation.
---

# VMR Source-Build Validation

This skill queues the `msbuild-vmr-validation` Azure DevOps pipeline on the current Git branch.

## What it does

Runs the VMR (Virtual Mono Repo) source-build validation against your current branch to verify that MSBuild changes don't break the .NET source-build. The pipeline builds the VMR in source-build mode (stage 1 and stage 2).

## Steps

1. Detect the current Git branch name.
2. Check if the branch exists on `dotnet/msbuild` (the `upstream` remote). The Azure DevOps pipeline can only access branches on `dotnet/msbuild`, **not forks**. If the branch only exists on a fork, tell the user to push it to `dotnet/msbuild` first: `git push upstream <branch>`.
3. Queue the `msbuild-vmr-validation` pipeline (ID 334) in the `dnceng-public` Azure DevOps organization, `public` project, on that branch.
4. Report the pipeline run URL back to the user.

## How to run

```powershell
# 1. Get the current branch
$branch = git rev-parse --abbrev-ref HEAD

# 2. Check branch exists on upstream (dotnet/msbuild)
$upstreamUrl = git remote get-url upstream 2>$null
if ($upstreamUrl -and $upstreamUrl -notmatch 'dotnet/msbuild') {
    # upstream is not dotnet/msbuild -- warn user
}
$refCheck = git ls-remote --heads upstream $branch 2>$null
if (-not $refCheck) {
    Write-Host "Branch '$branch' not found on dotnet/msbuild. Push it first: git push upstream $branch"
    # Stop here -- the pipeline will fail if the branch doesn't exist on dotnet/msbuild
}

# 3. Queue the pipeline
$result = az pipelines run --org https://dev.azure.com/dnceng-public --project public --id 334 --branch $branch --query "{id:id}" -o json 2>&1
```

## Output

After queuing, display:
- The **run ID**
- The **build URL**: `https://dev.azure.com/dnceng-public/public/_build/results?buildId=<id>`
- The **status**

## Prerequisites

- Azure CLI (`az`) must be installed and authenticated
- User must have permissions to queue builds in `dnceng-public/public`
