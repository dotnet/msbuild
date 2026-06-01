---
name: flaky-test-detector
description: Detect flaky tests in dotnet/msbuild by scanning recent Azure DevOps CI builds (approved PR validation + rolling builds on main) for test failures that recur across multiple independent evidence sources. Use when asked to find flaky tests, investigate intermittent CI failures, triage the flaky-test label, or produce a flaky-test report.
---

# Flaky Test Detector

This skill identifies **flaky tests** in dotnet/msbuild: tests that fail intermittently rather
than because of a real product regression. It works entirely against **anonymously accessible**
Azure DevOps APIs on the public CI (`dnceng-public` / `public`, pipeline `msbuild-pr`,
definition **75**), so it needs no Azure DevOps credentials.

## When to Use

- User asks to find or triage flaky tests, or to investigate intermittent CI failures.
- Periodic (scheduled) flaky-test sweeps.
- Before deciding whether to quarantine a test or open/refresh a `flaky-test` tracking issue.
- To gather evidence (which PRs / rolling builds, which legs/TFMs, error signatures) for a
  specific suspected-flaky test.

## Core Idea: Evidence Sources

A test is flagged as flaky when it fails across several **independent evidence sources**. A
source is one of:

1. **A Pull Request** — all failed PR-validation builds for one PR collapse into a single
   source. Only **non-draft, approved (or merged) PRs targeting `main`** count, because a
   failure in code reviewers signed off on is unlikely to be caused by that PR's own changes.
2. **A single failed rolling/CI build on `main`** (reason `individualCI` / `batchedCI` /
   `schedule`). `main` is expected to be green, so a test failing there is strong, independent
   evidence of flakiness (or a regression — see below).

A test failing across many *unrelated* approved PRs **and/or** multiple rolling `main` builds
cannot be explained by any one change, which is the signature of flakiness.

> **Scope (current):** `main` branch only. Builds on `vs18.x` / `exp/*` branches are ignored.

## Data Path (all anonymous on dnceng-public)

The Azure DevOps **Test Management API is NOT anonymously accessible**, so this skill uses
published pipeline **artifacts** instead:

1. **Build list** — `/_apis/build/builds?definitions=75&resultFilter=failed&minTime=...` returns
   failed builds with `reason`, `sourceBranch`, and `triggerInfo['pr.number']`.
2. **Timeline** — `/_apis/build/builds/{id}/timeline` exposes which *legs* (jobs) failed, used
   to download only the relevant artifacts.
3. **Artifacts list** — `/_apis/build/builds/{id}/artifacts` lists per-leg containers named
   `"<Leg> test logs"` and `"<Leg> build logs"`.
4. **Artifact zip** — `...artifacts?artifactName=<name>&$format=zip` downloads the container.
   - `"<Leg> test logs"` zips contain the **`.trx`** result files (failed test names + error
     message/stack trace).
   - `"<Leg> build logs"` zips (misleadingly named) contain the **full xUnit console `.log`**
     output and a `.binlog`. The detector does **not** download these; the fixer pulls them on
     demand when it needs stdout for diagnosis.
5. **TRX parsing** — failed tests come from `//UnitTestResult[@outcome='Failed']/@testName`. The
   `@testName` is fully-qualified and may include a parameterized suffix, e.g.
   `Namespace.Class.Method(runtimeToUse: "NET", ...)`. The detector stores both the raw name and
   a normalized name (parameter suffix stripped). The assembly/TFM/arch are encoded in the TRX
   file name, e.g. `Microsoft.Build.Engine.UnitTests_net472_x86.trx`.

> **TRX has no stdout.** The `<Output>` element only contains `<ErrorInfo>` (message + stack
> trace). For console output, use the `"... build logs"` artifacts.

## Running the Detector

```pwsh
pwsh -File .github/skills/flaky-test-detector/scripts/Get-FlakyTests.ps1
```

Common options:

| Parameter | Default | Purpose |
|-----------|---------|---------|
| `-MinSources` | `3` | Distinct sources (PRs + rolling builds) required to flag a test. |
| `-DaysBack` | `14` | Look-back window. |
| `-MaxBuilds` | `60` | Max failed builds (all reasons) to fetch. |
| `-MaxArtifactDownloads` | `150` | Hard cap; tripping it sets `scanComplete: false`. |
| `-TargetBranch` | `main` | Branch to scope to (PR base + rolling source branch). |
| `-NoApprovalFilter` | _off_ | Bypass the non-draft/approved PR filter (smoke testing only). |
| `-AllLegs` | _off_ | Download every "test logs" artifact, not just failed legs. |
| `-JsonOut <path>` | — | Also write the structured JSON report to a file. |

The script writes a **human-readable report to stderr** and the **structured JSON report to
stdout** (and `-JsonOut` if given). `gh` must be installed and authenticated for the approval
filter and existing-issue cross-reference to work.

### JSON report shape

```jsonc
{
  "scanComplete": true,            // false => truncated; DO NOT act on the data
  "targetBranch": "main",
  "minSources": 3,
  "buildsScanned": 18,
  "prSources": 9,
  "rollingSources": 4,
  "flakyTests": [
    {
      "testName": "Namespace.Class.Method",   // normalized (no param suffix)
      "distinctSources": 4,
      "distinctPRs": 3,
      "prNumbers": [13458, 13501, 13620],
      "rollingBuildIds": [1443528],
      "totalFailures": 7,
      "legs": ["CoreOnLinux", "FullOnWindows Release"],
      "tfms": ["net472", "net10.0"],
      "assemblies": ["Microsoft.Build.Engine.UnitTests"],
      "rawVariants": ["...Method(runtimeToUse: \"NET\")"],
      "errorHashes": ["a1b2c3d4"],            // short SHA-256 of each error message
      "firstSeen": "2026-05-20",
      "lastSeen": "2026-05-30",
      "sampleBuildUrl": "https://dev.azure.com/.../_build/results?buildId=...",
      "sampleError": "Assert.Equal() Failure ...",
      "relatedIssues": [ { "number": 1234, "title": "...", "state": "OPEN" } ]
    }
  ]
}
```

## Interpreting Results — Flake vs Regression

The detector surfaces evidence; it does **not** by itself prove flakiness. Before acting:

- **`scanComplete` must be `true`.** A truncated scan is biased — never file issues or dispatch
  a fixer from it. Widen `-MaxBuilds` / `-MaxArtifactDownloads` and re-run.
- **Spread over time and sources is the signal.** Prefer tests whose failures span multiple days
  and multiple sources over a burst within one source.
- **Rolling-only failures may be a real regression.** If a test only fails on consecutive rolling
  `main` builds (no scattered PR evidence) and the failures are identical, treat it as a likely
  **regression**, not a flake — that is a `noop` for quarantine; flag it for human attention.
- **Check `relatedIssues`** to avoid duplicate filing; an existing open issue should be updated,
  not duplicated.

## Tiered Thresholds (recommended)

| Action | Suggested bar |
|--------|---------------|
| Mention in report | ≥ 2 distinct sources |
| File / update a `flaky-test` tracking issue | ≥ 3 distinct sources |
| Quarantine (skip) the test | ≥ 5 distinct sources **or** failures spanning multiple days |

## Conventions

- **Existing label:** `flaky-test`.
- **Issue dedup marker:** every flaky-test tracking issue body must contain a hidden, stable
  marker so future runs can find it:
  ```html
  <!-- flaky-test-id: Namespace.Class.Method -->
  ```
  Search both open and recently-closed issues for this marker before filing a new one.
- **Quarantine / skip syntax (use exactly):**
  ```csharp
  [Fact(Skip = "https://github.com/dotnet/msbuild/issues/NNNN")]
  [Theory(Skip = "https://github.com/dotnet/msbuild/issues/NNNN")]
  ```
  Do **not** add a `"Flaky:"` prefix. The `Skip` value is the tracking-issue URL.
- **Theory granularity:** skipping a `[Theory]` disables *all* rows. Prefer method-level evidence
  and note the failing parameter distribution (`rawVariants`) before quarantining a `[Theory]`.

## Locating a Test's Source

Map the failing test's **assembly** (from the TRX file name, e.g.
`Microsoft.Build.Engine.UnitTests`) to its test project under `src/` (e.g.
`src/Build.UnitTests/Microsoft.Build.Engine.UnitTests.csproj`) rather than doing a repo-wide
text search. Then locate the class/method within that project.

## Related Workflows

- `.github/workflows/flaky-test-detector.agent.md` — scheduled triage: runs this skill, files /
  updates `flaky-test` issues, and (optionally) dispatches the fixer.
- `.github/workflows/flaky-test-fix.agent.md` — dispatched fixer: reproduces locally and either
  applies a minimal determinism fix or quarantines the test, opening one PR.
