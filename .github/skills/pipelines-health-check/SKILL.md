---
name: pipelines-health-check
description: Check health of MSBuild CI pipelines, VS repo PR insertion statuses, and VMR codeflow PRs. Use when asked about pipeline health, build failures, infrastructure issues, CI status, insertion PR status, VMR codeflow status, or for periodic health monitoring.
---

# Pipelines & PR Health Check

This skill checks the health of MSBuild's CI pipelines, the status of insertion PRs in the VS repository, and the health of VMR codeflow PRs that flow MSBuild source into the dotnet/dotnet unified repository.

## When to Use

- User asks about MSBuild pipeline health, CI status, or build failures
- User asks about VS insertion PR status or whether insertions are going through
- User asks about VMR codeflow PRs, dotnet/dotnet PR status, or dotnet-unified-build pipeline
- User asks to check if there are failing checks on PRs
- User asks for a health check or status overview
- Periodic monitoring requests

## Prerequisites

- `az` CLI must be installed and authenticated (`az login` with access to the DevDiv organization)
- Azure DevOps extension for `az` must be installed: `az extension add --name azure-devops`
- PowerShell 5.1+ or PowerShell Core

### Optional: WorkIQ (for infrastructure issue investigation)

[WorkIQ](https://www.npmjs.com/package/@microsoft/workiq) is an MCP server / CLI that can query Microsoft 365 data (people, emails, Teams, documents) to find **service ownership, contacts, and incident context** when pipeline failures are caused by infrastructure issues outside MSBuild's control.

**Check availability:**
```powershell
workiq version
# Expected: 0.2.x or later
```

**If not installed**, set it up:
```powershell
# Install globally (use --registry if your .npmrc redirects @microsoft scope to GitHub Packages)
npm install -g @microsoft/workiq --registry https://registry.npmjs.org

# Accept the EULA (required once)
workiq accept-eula
```

WorkIQ is not required for the core health check. If unavailable, the skill will still work — it will simply skip the ownership lookup and suggest manual investigation or offer to help install WorkIQ.

## Reference Information

### Pipelines

| Pipeline | Org/Project | ID | Purpose |
|----------|-------------|----|---------|
| MSBuild | devdiv/DevDiv | 9434 | Main CI pipeline — builds and tests on every commit to main |
| MSBuild-OptProf | devdiv/DevDiv | 17389 | Optimization/profiling pipeline — runs on schedule |
| dotnet-unified-build | dnceng-public/public | — | VMR build pipeline — runs on codeflow PRs in dotnet/dotnet |

### Key URLs

- MSBuild pipeline: `https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434`
- OptProf pipeline: `https://devdiv.visualstudio.com/DevDiv/_build?definitionId=17389`
- VS PRs assigned to MSBuild: `https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequests?_a=active&assignedTo=66cc9d27-aef7-4399-ba2c-3dccb4489098`
- VMR codeflow PRs: `https://github.com/dotnet/dotnet/pulls?q=is:pr+is:open+"Source+code+updates+from+dotnet/msbuild"`
- dotnet-unified-build (public): `https://dev.azure.com/dnceng-public/public/_build`

## Phase 1: Collect Data & Present Overview Table

### Step 1: Run all data collection scripts

Run these three scripts from the repository root **in parallel**. They output JSON to stdout. Each script may take 1–3 minutes depending on the number of PRs and pipeline runs to fetch, so use an `initial_wait` of **120 seconds** or higher.

```powershell
# Pipeline health (checks both MSBuild and MSBuild-OptProf)
$pipelineJson = & .\.github\skills\pipelines-health-check\check-pipeline-health.ps1

# VS PR status (checks active non-Experimental PRs and last merged PR)
$prJson = & .\.github\skills\pipelines-health-check\check-vs-pr-status.ps1

# VMR codeflow status (checks open codeflow PRs from msbuild and their dotnet-unified-build runs)
$vmrJson = & .\.github\skills\pipelines-health-check\check-vmr-codeflow.ps1
```

All scripts write progress messages to stderr (`Write-Host`) and the JSON result to stdout.

The scripts sanitize error messages (stripping control characters, truncating to 500 chars) so the JSON output can be parsed directly with `ConvertFrom-Json` without additional cleanup.

### Step 2: Present the overview table IMMEDIATELY

Parse the JSON outputs and render status overview tables to the user **before** doing any deeper investigation. This gives the user instant visibility.
Present ALL tables - for both pipelines and for the VS insertion PRs. Do not omit any of those unless explicitly asked by user just for some specific overview.

#### Pipeline Health Table

For each pipeline in the JSON output, render one row:

| Pipeline | Last Success | Age | Recent Runs | Status |
|----------|-------------|-----|-------------|--------|
| {pipelineName} ({pipelineId}) | {lastSuccessfulRun.finishTime} | {lastSuccessfulRun.ageHours}h | emoji sequence | status emoji + label |

**Recent Runs column:** Show an emoji for each run in `recentRuns` array (newest first):
- `✅` for `succeeded`
- `❌` for `failed`
- `⏳` for `inProgress`
- `⚪` for `canceled` or other

**Status column** — derive from `healthSummary` and `lastSuccessfulRun.ageHours`:
- `✅ HEALTHY` — healthSummary starts with "HEALTHY"
- `⚠️ FLAKY` — healthSummary starts with "FLAKY"
- `🔴 UNHEALTHY` — healthSummary starts with "UNHEALTHY"
- Add `⚠️` if ageHours > 24, `🔴` if ageHours > 48 (even if some runs succeed, stale success is a concern)

#### VS Insertion PRs Table (non-Experimental)

For each PR in the `prs` array:

| PR | Title | Checks ✅ | Checks ⏳ | Checks ❌ | Status |
|----|-------|-----------|-----------|-----------|--------|
| [{id}](url) | {title} (truncated) | {checks.succeeded} | {checks.pending} | {checks.failed} | status |

**Status column:**
- `🔴 Failing` — if `actionNeeded` is true (has failed required checks)
- `⏳ Running` — if `checks.pending > 0` and no failures
- `✅ Green` — if all checks succeeded or notApplicable

#### Last Merged Insertion Row

| Last Merged PR | Date | Age | Status |
|---------------|------|-----|--------|
| [{lastMergedPr.id}](lastMergedPr.url) | {lastMergedPr.closedDate} | {ageDays} days | status |

**Status:**
- `✅ Recent` — ageHours ≤ 48 (≤ 2 business days)
- `⚠️ Getting stale` — ageHours > 48 and ≤ 96
- `🔴 Stale insertion` — ageHours > 96 (> 4 business days)

**Note on weekends:** When computing business-day age, be aware that weekends inflate the hour count. If today is Monday and the last merge was Friday, that's ~72h but only 1 business day. Mention this nuance to the user if the age seems borderline.

#### VMR Codeflow PRs Table

For each codeflow PR in the `codeflowPRs` array from `$vmrJson`:

| Codeflow PR | Age | Pipeline Runs | Upstream PRs | Status |
|-------------|-----|---------------|--------------|--------|
| [#{prNumber}](prUrl) | {prAge}h | emoji sequence from pipelineRuns | count | status emoji + label |

**Pipeline Runs column:** Show an emoji for each run in `pipelineRuns` (newest first):
- `✅` for `result == "succeeded"`
- `❌` for `result == "failed"`
- `⏳` for `status == "inProgress"`
- `⚪` for other/no runs

**Status column** — derive from `healthSummary`:
- `✅ HEALTHY` — healthSummary starts with "HEALTHY"
- `🔄 IN PROGRESS` — healthSummary starts with "IN_PROGRESS" or "RETRYING"
- `🔴 FAILING` — healthSummary starts with "UNHEALTHY"
- `⚠️ MIXED` — healthSummary starts with "MIXED"
- `❓ UNKNOWN` — no pipeline runs found

**If a PR has failures**, also render a failure details sub-table:

| Failed Job | Failed Task | Error Category | Related Upstream PRs |
|------------|------------|----------------|---------------------|
| {job.name} | {task.name} | {category} | PR links from failureCorrelation |

The `failureCorrelation` array maps each failed build to error categories and potentially related upstream PRs (matched by title keywords). This helps quickly identify which msbuild change likely caused a VMR build failure.

**Upstream PRs list:** For each codeflow PR, list the included upstream msbuild PRs:

> **Included changes:** [#13175](url) Add App Host Support, [#13306](url) IBuildEngine callbacks, ...

### Step 3: Identify problems

After rendering the table, build a list of distinct problems. A "problem" is any of:

1. **Pipeline failure** — A pipeline whose latest run on main failed, especially if `lastSuccessfulRun.ageHours > 24`
2. **PR check failure** — An active non-Experimental PR that has `actionNeeded: true` (failed required checks)
3. **Stale insertion** — `lastMergedPr.ageHours > 48` (no successful insertion in >2 business days)
4. **All checks pending** — A PR where all checks are still pending/queued (may indicate a stuck pipeline or queue issue)
5. **VMR codeflow failure** — A codeflow PR whose `healthSummary` starts with "UNHEALTHY" (dotnet-unified-build failing)
6. **VMR codeflow stale** — A codeflow PR older than 48 hours with no successful pipeline run

If there are **no problems**, report `✅ ALL CLEAR — pipelines healthy, PRs on track, insertions flowing, VMR codeflow green` and stop. Do not proceed to Phase 2.

## Phase 2: Investigate Problems via Subagents

For **each distinct problem** identified in Step 3, launch a **separate subagent** to perform DEEP, DETAILED investigation (use `#tool:agent/runSubagent` to run the investigation tasks). Fire them in parallel when possible. Use the <agent> template below to seed them. 

<subagent>

### Subagent prompt templates

#### For pipeline failures

```
Investigate why the Azure DevOps pipeline "{pipelineName}" (ID: {pipelineId}) is failing.

Recent failed runs on branch {branch}:
{for each failed run, list: Run ID, start time, URL, and the failedTasks with their error messages}

Last successful run: {lastSuccessfulRun.finishTime} ({ageHours} hours ago)
URL: {lastSuccessfulRun.url}

Tasks:
1. Categorize each failure as one of:
   - BUILD ERROR: compilation failures, test failures, task execution errors in MSBuild code
   - CONFIG/PERMISSION: signing errors, NuGet authentication, certificate issues, feed access
   - INFRA/TRANSIENT: errors indicating unavailability or outage of services or resources
2. Check if all recent failures share the same root cause or if there are different issues
3. If infra/transient: suggest retrying the pipeline (provide the pipeline URL)
4. If build error: 
  - Check the `For build errors` section below on how to investigate build errors with binlogs
  - identify which component/task is failing and check recent commits to main to try to identify offending one.
5. If infrastructure issues:
  - Try to distill the exact reason for the issue, check if there are other failing pipelines with the same issue or any open bugs for the issue.
  - **Use WorkIQ** to find the owning team and contacts. Check if `workiq` CLI is available (`workiq version`).
    - If available, run: `workiq ask -q "Who owns the {failing service/task name} service in Microsoft DevDiv? Who should be contacted about {brief error description}?"`
    - Include the WorkIQ response in your findings — it typically returns team names, distribution lists, contact people, and escalation paths.
    - You can also ask WorkIQ about known outages: `workiq ask -q "Are there any known outages or incidents for {service name} in Azure DevOps?"`
    - If WorkIQ is NOT available, note this in your report and suggest the user install it:
      ```
      npm install -g @microsoft/workiq --registry https://registry.npmjs.org
      workiq accept-eula
      ```
  - Put together a concise overview of the issue, along with links to the failure messages, the owning team/contacts from WorkIQ, and suggested next steps.

Return: A comprehensive root cause analysis with category, explanation, links to failure messages, ownership info (from WorkIQ if available), and recommended action.
```

#### For PR check failures

```
Investigate failing checks on VS insertion PR #{prId}: "{prTitle}"
PR URL: {prUrl}

Failed checks:
{for each item in checks.failedChecks: genre, name, description, isRequired}

Pending checks (still running):
{for each item in checks.pendingChecks: genre, name, description, isRequired}

Pipeline health context:
{brief summary of pipeline health from Phase 1 — are pipelines also failing?}

Tasks:
1. Identify which failed checks are required vs optional
2. If required checks are failing, determine if this could be related to pipeline failures (same root cause)
3. If checks are just pending/queued, note that they may still be running and suggest waiting
4. Recommend specific actions: retry checks, investigate pipeline, or wait
5. If check is failing - try to traverse the chain of called pipelines to the actual error, then: 
  - Check the `For build errors` section below on how to investigate build errors with binlogs
  - identify which component/task is failing and check recent commits to msbuild main to try to identify offending one.

Return: Which checks need attention, likely cause, and recommended action.
```

#### For stale insertion

```
Investigate why MSBuild insertions into VS appear stale.

Last successfully merged non-Experimental PR: #{lastMergedPr.id} "{lastMergedPr.title}"
  Merged: {lastMergedPr.closedDate} ({ageDays} days ago)
  URL: {lastMergedPr.url}

Currently active non-Experimental PRs:
{for each PR: id, title, url, checks summary, actionNeeded}

Pipeline health:
{brief pipeline health summary}

Tasks:
1. Check if there are active non-Experimental PRs waiting — if none, the issue may be that no insertion was triggered
2. If there are active PRs with failing checks, identify if those failures are blocking the insertion
3. If there are active PRs with all checks pending, they may just need time
4. Correlate with pipeline health — if the CI pipeline is broken, insertions can't succeed
5. Recommend specific actions to unblock

Return: Explanation of why insertion appears stuck and what to do about it.
```

#### For VMR codeflow failures

```
Investigate failing dotnet-unified-build pipeline for VMR codeflow PR #{prNumber}: "{prTitle}"
PR URL: {prUrl}
PR Branch: {prBranch}

This is a codeflow PR that brings MSBuild source changes into the dotnet/dotnet VMR (Virtual Monolithic Repository).
The dotnet-unified-build pipeline runs in the dnceng-public/public Azure DevOps org.

Failed pipeline runs:
{for each failed run in pipelineRuns: buildId, buildNumber, URL, stages, failedJobs with their failedTasks and errors}

Failure categories detected: {failureCorrelation[].categories}

Upstream MSBuild PRs included in this codeflow:
{for each PR in upstreamPRs: number, title, url, merged status}

Failure-to-change correlation (from script):
{for each item in failureCorrelation: buildId, categories, relatedUpstreamPRs}

Tasks:
1. For each failed job, examine the error messages and categorize:
   - TASK_HOST: MSB4216 errors about MSBuild not finding the task host executable — typically caused by changes to MSBuild's app host, node launching, or SDK layout
   - SOURCE_BUILD_TASK_HOST: Same as TASK_HOST but in the source-only build (CentOS offline) — the previously-source-built SDK doesn't have the expected MSBuild executable
   - COMPILATION: CS/VB/FS compilation errors — a code change broke the build
   - BUILD_COMMAND: MSB3073 "exited with code N" — a build script or test failed
   - NUGET_AUTH/SIGNING/TIMEOUT/RESOURCE: Infrastructure issues
2. Use the failure-to-change correlation to identify which upstream MSBuild PR most likely caused each failure:
   - Check if the error is in an area touched by one of the upstream PRs
   - If TASK_HOST errors: look for PRs touching NodeLauncher, NodeProvider, task host, app host, or BuildEnvironmentHelper
   - If COMPILATION errors: check which files the upstream PRs modified and whether any could cause the compilation break
   - If test failures: identify which test is failing and which upstream PR likely affects that code path
3. Check if there is an in-progress retry build that might resolve the issue
4. If the failure appears to be an infrastructure issue (not caused by MSBuild changes), note that
5. For build errors, check the `For build errors` section on how to investigate with binlogs
6. Recommend specific actions:
   - If an upstream PR is clearly at fault, suggest reverting it or filing a fix
   - If a VMR-side fix is needed (e.g., SDK layout change), describe what needs to change
   - If a retry might help (transient infra), suggest re-running the pipeline
   - If the issue is already being retried (in-progress build), suggest waiting

Return: Root cause analysis mapping each failure to the likely upstream PR, with explanation, links, and recommended action.
```

#### For build errors

Tasks:
1. Try to find a .binlog file(s) in the build or step artifacts and fetch it
2. Ensure to acquire the [binlog-failure-analysis skill](https://github.com/ViktorHofer/dotnet-skills/blob/main/msbuild-skills/skills/binlog-failure-analysis/SKILL.md) together with the binlog-mcp (spawn via `dnx -y baronfel.binlog.mcp@0.0.13`)
3. Use the binlog analysis skill and mcp to analyse the binlog(s) you found and analyse problems from those

</subagent>

## Phase 3: Final Report

After all subagent results return, present the findings below the overview table under a **"🔍 Problems & Recommendations"** heading. For each problem:

```markdown
### Problem: {brief title}
**Category:** {INFRA | BUILD | CONFIG | PR_CHECKS | STALE_INSERTION | VMR_CODEFLOW}
**Details:** {subagent's explanation}
**Ownership:** {owning team, contacts, DL from WorkIQ — include only for INFRA/CONFIG issues}
**Recommended Action:** {subagent's recommendation}
```

If all problems are infra/transient, add a note: *"All current issues appear to be infrastructure-related. Consider retrying the pipelines and checking again in 30 minutes."*

## Troubleshooting

### "az: command not found" or "az account get-access-token" fails
The `az` CLI is not installed or not authenticated. Run `az login` first.

### Scripts return empty arrays
- Check that you have access to the DevDiv organization
- The branch filter defaults to `main` — if checking a different branch, pass `-Branch <name>` to the pipeline script

### PR statuses all show as "pending"
This is normal for newly created PRs. The checks take time to queue and run. If checks are pending for more than a few hours, this may indicate a stuck pipeline or queue issue.

### Timeout or rate limiting
If the scripts take a long time or fail with 429 errors, Azure DevOps may be rate-limiting. Wait a minute and retry.

### WorkIQ not found or EULA not accepted
If `workiq version` fails, install it:
```powershell
npm install -g @microsoft/workiq --registry https://registry.npmjs.org
workiq accept-eula
```
Note: If your `.npmrc` redirects the `@microsoft` scope to GitHub Packages, use `--registry https://registry.npmjs.org` to override, or pass `--userconfig` pointing to a clean `.npmrc`.

### WorkIQ returns empty or unhelpful results
WorkIQ queries Microsoft 365 data (Outlook, Teams, SharePoint). Results depend on your account's access and the data available in your tenant. Try rephrasing the question or being more specific about the service name. Example queries that work well:
- `workiq ask -q "Who owns the MicroBuild service in Microsoft?"`
- `workiq ask -q "Who owns the CloudBuild signing service in DevDiv?"`
- `workiq ask -q "Who should I contact about NuGet feed authentication failures in Azure DevOps?"`

### VMR codeflow script returns no PRs
- Verify there are actually open codeflow PRs: check `https://github.com/dotnet/dotnet/pulls?q=is:pr+is:open+"Source+code+updates+from+dotnet/msbuild"`
- Verify `gh` CLI is authenticated: run `gh auth status`

### VMR pipeline runs not found
- The dotnet-unified-build pipeline runs in `dnceng-public/public`, which is publicly accessible
- If no runs appear, the pipeline may not have been triggered yet for that PR
- Codeflow PRs may take a few minutes after creation before CI triggers

### JSON output too large or contains unexpected characters
- Error messages from Azure DevOps timelines can contain Windows paths, control characters, and multi-KB stack traces
- The scripts sanitize and truncate these to 500 characters — if you still see issues, check that you're running the latest version of the scripts
- Use `ConvertFrom-Json` in PowerShell or `json.loads()` in Python to parse the output; avoid manual string manipulation
