# Read-only collection and output contracts

Use only the collector needed for the request. Commands below run from the
repository root in PowerShell. Scripts support PowerShell 5.1 or later and use
existing authentication; they do not install tools/extensions or authenticate.

## Choose a supported surface

Prefer a current-session service tool when its discovered schema expresses the
requested organization/project and filters. Do not infer a capability from an
installed plugin name. Some `hlx` build-listing schemas accept only `public` and
`internal` projects, even with a `devdiv` organization; do not invent a `DevDiv`
argument. A supported build/timeline URL operation or these native read-only
collectors can be the fallback.

For GitHub, prefer `gh` and inspect `gh api --help` before relying on
`--paginate --slurp`. For private Azure DevOps reads, the collectors use built-in
`az rest` with the Azure DevOps resource audience and the existing account. They
do not require installation of the Azure DevOps CLI extension.

## Pipeline outcomes

```powershell
$pipelineJson = & .\.github\skills\pipelines-health-check\check-pipeline-health.ps1 -Branch main -Top 5
if (!$?) { throw "Pipeline collection failed; no all-clear conclusion is available." }
$pipelines = $pipelineJson | ConvertFrom-Json
```

Defaults are MSBuild (historically definition 9434) and MSBuild-OptProf
(historically 17389), in DevDiv. The script resolves each definition and rejects a
default ID that no longer matches its expected name in that organization/project.
Those numeric IDs do not impose name expectations in other organizations or projects. For another selected
pipeline, supply verified `-PipelineIds`, `-Organization`, and `-Project`.
`-Branch` accepts either a short name or a full ref.

The output is always a JSON array, including a single pipeline. Each entry has:

| Field | Meaning |
|---|---|
| `schemaVersion`, `collectionStatus`, `observedAt` | Contract version 2, successful requested collection, and UTC observation time |
| `pipelineId`, `pipelineName`, `organization`, `project`, `branch` | Resolved identity and scope |
| `sampleLimit`, `recentRuns` | Bounded recent sample in newest queue-time order, not all history |
| `lastSuccessfulRun` | Most recently finished successful run, or null when that query found none |
| `healthState`, `healthSummary` | Outcome of the latest sampled run; historical success does not make a currently failed run healthy |

Use `-IncludeFailureDetails` only for requested deeper detail. `failureDetails`
contains failed/error-bearing timeline records, including job/stage-level issues
that have no failed child task. Records retain parent IDs and log IDs for a
targeted follow-up.

## VS insertion PRs

```powershell
$prJson = & .\.github\skills\pipelines-health-check\check-vs-pr-status.ps1 -TargetBranch main -CompletedWithinDays 30
if (!$?) { throw "Insertion collection failed; its status is unavailable." }
$insertions = $prJson | ConvertFrom-Json
```

Repository `VS` and the historical MSBuild reviewer ID are defaults. The script
resolves repository metadata and the current reviewer identity, checking the
expected reviewer name. If the team or repository changed, discover the intended
identity before supplying `-RepositoryName`, `-RepositoryId`, `-ReviewerId`, or
`-ExpectedReviewerName`; do not bypass an unexpected mismatch just to get results.

The script paginates active PRs for the selected reviewer/target branch.
Experimental PRs are excluded unless `-IncludeExperimental` is supplied.
For each PR it queries iterations, PR statuses, and
[policy evaluations](https://learn.microsoft.com/en-us/rest/api/azure/devops/policy/evaluations/list?view=azure-devops-rest-7.1).
Only current-iteration and PR-wide statuses are eligible. For each context, the
newest eligible timestamp wins, with status ID breaking ties; iteration-specific
success must not hide a newer PR-wide failure. Status context names are not used
to guess which policies block merging.

`prs` contains per-PR `checks`, `blockingPolicies`, `unknownPolicyCount`,
`currentIteration`, `healthState`, and `actionNeeded`. An observed optional check
failure still deserves attention, but does not itself prove a blocking policy.
Missing iteration/check evidence is unknown, not green. This is not a complete
merge-eligibility decision.

`lastMergedPr` is the newest nonexperimental completed PR within the explicitly
reported `lastMergedSearchSince` window, sorted by close time. A null value means
none in that window, not "no insertion has ever merged". `-MaxPages` bounds PR
and policy enumeration; exceeding it terminates as incomplete, not as success.

## MSBuild-to-VMR PR checks

```powershell
$vmrJson = & .\.github\skills\pipelines-health-check\check-vmr-codeflow.ps1 -Top 3
if (!$?) { throw "VMR collection failed; its status is unavailable." }
$vmr = $vmrJson | ConvertFrom-Json
```

GitHub PR search is paginated and checked against `total_count`,
`incomplete_results`, duplicates, and the search result ceiling. A search failure
does not become "no open PRs".

The script resolves `dotnet-unified-build` by name unless a verified
`-DefinitionId` is supplied. It confirms the definition's repository, and filters
builds by definition, repository, and PR ref. The default VMR organization/project
is public and needs no new Azure authentication.

Every sampled build records `sourceVersion` and `matchesCurrentRevision`.
`healthState` is based only on builds matching the PR's current head or a
test-merge SHA whose mergeability has been computed successfully. If no sampled build matches, report unknown even if an older
build succeeded. Query the PR's actual checks or broaden the bounded sample when
needed; a test-merge SHA alone is not current evidence while GitHub recomputes
mergeability.

Optional `-IncludeFailureDetails` retrieves failure records only for matching
current-revision failed/partially successful runs.
Optional `-IncludeUpstreamReferences` retrieves PR-body/comment references, with
details for up to 30 referenced PRs and the remaining numbers listed explicitly.
`upstreamReferences` is **not** a flowed commit range or causal attribution.
Use flow analysis/tracing to establish actual membership.

## Interpreting results

`HEALTHY` requires affirmative success evidence. `UNHEALTHY`, `UNKNOWN`,
`PENDING`, `IN_PROGRESS`, and `CANCELED` are not interchangeable. A partially
successful build is not healthy. All ages are elapsed hours, not business days;
choose schedule-aware staleness thresholds for the requested pipeline rather
than applying a universal 48-hour rule.

Use only relevant rows/columns; placeholders below are a fenced template, not
real links:

```text
Surface | Current result | Last success/merge | Scope/coverage | Evidence URL
<pipeline or PR> | <state> | <time or unknown> | <branch, revision, sample> | <actual URL>
```

Required query failures throw and produce no success-shaped JSON. Separate
collectors can succeed independently: report each result and each unavailable
surface instead of making a global all-clear statement. JSON goes to the success
output stream; the scripts do not emit `Write-Host` progress into native stdout.

Version 2 replaces guessed required-check genres and title-based failure
correlations with policy/revision evidence. Consumers must not expect the old
`failureCorrelation` or treat comment references as `upstreamPRs` membership.

Error excerpts are bounded and have basic credential-pattern redaction. This is
not sufficient for public sharing: inspect source logs, paths, and task inputs
before publishing any diagnostic material.

## Offline collector regression checks

[Collector fixtures](../tests/Collectors.Tests.ps1) intercept `az`, `gh`, and
`Invoke-RestMethod`; unexpected operations fail rather than reaching a service.
Run them in a fresh process, never by dot-sourcing:

```powershell
pwsh -NoProfile -File .\.github\skills\pipelines-health-check\tests\Collectors.Tests.ps1
```

On Windows, use `powershell.exe -NoProfile -File` with the same file to cover the
Windows PowerShell 5.1 contract as well. No product build or SDK installation is
needed for these checks.
