# CTS pipeline telemetry — `cts-metrics.json`

Both `azure-pipelines/cts-collect.yml` and `azure-pipelines/cts-apply.yml`
emit a single `cts-metrics.json` file per OS per run and publish it as a
pipeline artifact. They also echo it inside an `##[group]CTS metrics`
log block so the data is searchable in pipeline logs.

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
| `wallTimeMs` | int | Wall-clock time spent inside the `cts` invocation. |
| `ctsToolVersion` | string | Output of `cts --version`. |
| `timestampUtc` | string | ISO-8601, run start. |

## `phase=collect`

| Field | Type | Notes |
| --- | --- | --- |
| `sha` | string | HEAD SHA of `main` at job start. Used as snapshot tag. |
| `testCount` | int | Total tests executed during collect. |
| `moduleCount` | int | Distinct test modules instrumented. |
| `coverageBytes` | int | Size of the coverage payload. |
| `artifactName` | string | Pipeline artifact name (`cts-baseline-<sha>-<os>`). |

## `phase=apply`

| Field | Type | Notes |
| --- | --- | --- |
| `prHeadSha` | string | HEAD SHA of the PR being validated. |
| `baselineSha` | string \| null | SHA of the snapshot we resolved (null on fallback). |
| `baselineAgeCommits` | int \| null | Commit distance from `baselineSha` to PR's merge-base. |
| `baselineAgeMinutes` | int \| null | Wall-clock age of the baseline. |
| `selectedTestCount` | int | Tests CTS chose to run. |
| `totalCandidateTestCount` | int | Tests CTS considered (denominator for ratio). |
| `selectionRatio` | float | `selectedTestCount / totalCandidateTestCount`. |
| `fallbackReason` | string \| null | `null` on happy path. Known values: `"no-ancestor-snapshot"`, `"snapshot-download-failed"`, `"cts-apply-error"`. |

## Coverage note

`testCount` / `selectedTestCount` / `totalCandidateTestCount` reflect
**net10.0 only**. The net472 leg of the multi-TFM test projects is not
exercised by the CTS pipeline (see `scripts/cts/README.md` → "Coverage
gap"). When comparing CTS incrementality against the regular PR pipeline
remember to scale denominators accordingly.

## Conventions

* When a value cannot be computed it is emitted as JSON `null` (never omitted).
* Times are in UTC, milliseconds.
* Per-OS files keep the field set identical so an aggregator can append rows.
