# CTS pipeline telemetry — `cts-metrics.json`

Both `azure-pipelines/cts-collect.yml` and `azure-pipelines/cts-apply.yml`
emit a single `cts-metrics.json` file per OS per run and publish it as a
pipeline artifact. They also echo it inside a `##[group]CTS apply metrics`
or `##[group]CTS collect metrics` log block (depending on phase) so the
data is searchable in pipeline logs.

The schema is intentionally flat so it can be ingested later into Kusto /
App Insights without remodelling. Unknown fields should be ignored by
consumers; new fields will be added at the end of this list.

## Common fields (always present)

| Field | Type | Notes |
| --- | --- | --- |
| `schemaVersion` | int | Bump on any breaking change. Current: `1`. |
| `phase` | string | `"collect"` or `"apply"`. |
| `os` | string | `"windows"` or `"linux"` (matches pool image family). |
| `pipelineRunId` | string | `$(Build.BuildId)`. |
| `pipelineRunUrl` | string | Link to the run for human triage. |
| `repoBranch` | string | `$(Build.SourceBranch)`. |
| `wallTimeMs` | int \| null | Wall-clock time spent inside the `cts` invocation. |
| `ctsToolVersion` | string | Output of `cts --version`. |
| `timestampUtc` | string | ISO-8601, run start. |

## `phase=collect`

| Field | Type | Notes |
| --- | --- | --- |
| `sha` | string | HEAD SHA of `main` at job start. The baseline tag passed to `cts collect --tag`. |
| `artifactName` | string | Pipeline artifact name (`cts-baseline-<os>`). |

## `phase=apply`

| Field | Type | Notes |
| --- | --- | --- |
| `prHeadSha` | string | HEAD SHA of the PR being validated. |
| `baselineSha` | string \| null | SHA of the snapshot we resolved (null on fallback). |
| `baselineAgeCommits` | int \| null | `git rev-list --count <baselineSha>..<prHeadSha>`. |
| `baselineAgeMinutes` | int \| null | Wall-clock age of the baseline. |
| `fallbackReason` | string \| null | `null` on happy path. Known values: `"collect-pipeline-not-configured"` (apply yaml's `collectPipelineId == 0`), `"baseline-download-failed"`, `"baseline-metadata-missing"`, `"cts-apply-error"`. |
| `selectedTestCount` | int \| null | Tests CTS selected as impacted (headline number). `null` on fallback. |
| `totalCandidateTestCount` | int \| null | Total candidate tests considered (headline number). `null` on fallback. |
| `incrementalityPercent` | number \| null | Percentage reduction achieved by the selection. `null` on fallback. |
| `executedTestCount` | int \| null | Tests actually executed by the apply run. `null` on fallback. |
| `passedTestCount` | int \| null | Executed tests that passed. `null` on fallback. |
| `failedTestCount` | int \| null | Executed tests that failed. `null` on fallback. |
| `skippedTestCount` | int \| null | Executed tests that were skipped. `null` on fallback. |

## Coverage note

Tests are net10.0 only — the wrappers cannot host the net472 leg (xunit.v3
requires `OutputType=Exe`, CTS requires `OutputType=Library`). The regular
PR pipeline continues to provide net472 signal. See `scripts/cts/README.md`
→ "Coverage gap" for the details.

## Not yet populated

The following fields are intentionally absent from v1 of the schema; they
require parsing the `cts` log/JSON output and will be added in a follow-up:

* `testCount`, `moduleCount`, `coverageBytes` (collect)

## Conventions

* When a value cannot be computed it is emitted as JSON `null` (never omitted).
* Times are in UTC, milliseconds.
* Per-OS files keep the field set identical so an aggregator can append rows.
