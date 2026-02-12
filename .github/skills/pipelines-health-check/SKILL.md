---
name: pipelines-health-check
description: Check health of MSBuild CI pipelines and VS repo PR insertion statuses. Use when asked about pipeline health, build failures, infrastructure issues, CI status, insertion PR status, or for periodic health monitoring.
---

# Pipelines & PR Health Check

This skill checks the health of MSBuild's CI pipelines and the status of insertion PRs in the VS repository.

## When to Use

- User asks about MSBuild pipeline health, CI status, or build failures
- User asks about VS insertion PR status or whether insertions are going through
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

| Pipeline | ID | Purpose |
|----------|----|---------|
| MSBuild | 9434 | Main CI pipeline — builds and tests on every commit to main |
| MSBuild-OptProf | 17389 | Optimization/profiling pipeline — runs on schedule |

### Key URLs

- MSBuild pipeline: `https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434`
- OptProf pipeline: `https://devdiv.visualstudio.com/DevDiv/_build?definitionId=17389`
- VS PRs assigned to MSBuild: `https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequests?_a=active&assignedTo=66cc9d27-aef7-4399-ba2c-3dccb4489098`

## Phase 1: Collect Data & Present Overview Table

### Step 1: Run both data collection scripts

Run these two scripts from the repository root. They output JSON to stdout.

```powershell
# Pipeline health (checks both MSBuild and MSBuild-OptProf)
$pipelineJson = & .\.github\skills\pipelines-health-check\check-pipeline-health.ps1

# VS PR status (checks active non-Experimental PRs and last merged PR)
$prJson = & .\.github\skills\pipelines-health-check\check-vs-pr-status.ps1
```

Both scripts use `az account get-access-token` internally — no token management needed.

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

### Step 3: Identify problems

After rendering the table, build a list of distinct problems. A "problem" is any of:

1. **Pipeline failure** — A pipeline whose latest run on main failed, especially if `lastSuccessfulRun.ageHours > 24`
2. **PR check failure** — An active non-Experimental PR that has `actionNeeded: true` (failed required checks)
3. **Stale insertion** — `lastMergedPr.ageHours > 48` (no successful insertion in >2 business days)
4. **All checks pending** — A PR where all checks are still pending/queued (may indicate a stuck pipeline or queue issue)

If there are **no problems**, report `✅ ALL CLEAR — pipelines healthy, PRs on track, insertions flowing` and stop. Do not proceed to Phase 2.

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
**Category:** {INFRA | BUILD | CONFIG | PR_CHECKS | STALE_INSERTION}
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
