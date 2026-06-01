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
     output and a `.binlog`. The scan does **not** download these; the fix phase pulls them on
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
| `-IncludePassed` | _off_ | Also record **passing** observations and emit a `passedTests` aggregate. Forces `-AllLegs` and queries **all completed** builds (not just failed). Used against the quarantine pipeline (def 344) to drive un-quarantining. |
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
  ],
  "passedTests": [               // ONLY populated with -IncludePassed (e.g. def 344)
    {
      "testName": "Namespace.Class.Method",   // normalized (no param suffix)
      "totalPassed": 11,         // total passing UnitTestResults observed in the window
      "distinctBuilds": 6,       // distinct quarantine builds it was seen green in
      "buildIds": [1450001, 1450200],
      "distinctDays": 4,         // distinct calendar days with a green observation
      "legs": ["CoreOnWindows", "CoreOnLinux", "CoreOnMacOS"],
      "tfms": ["net472", "net10.0"],
      "assemblies": ["Microsoft.Build.Engine.UnitTests"],
      "firstSeen": "2026-05-20",
      "lastSeen": "2026-05-30"
    }
  ]
}
```

## Quarantine-Pipeline Health (definition 344)

The same detector, run with `-DefinitionId 344 -IncludePassed`, reads the scheduled **quarantine
pipeline** (`azure-pipelines/quarantine.yml`), which re-runs **only** the quarantined
(`[ActiveIssue]` / `Category=failing`) tests on Windows/Linux/macOS twice daily. This is the signal
for **clearing the quarantine backlog**:

```pwsh
pwsh -File .github/skills/flaky-test-detector/scripts/Get-FlakyTests.ps1 `
  -DefinitionId 344 -MinSources 2 -IncludePassed -DaysBack 21 -JsonOut quarantine-health.json
```

- Because most quarantine builds finish **green** (they succeed unless a quarantined test fails),
  `-IncludePassed` queries **all completed** builds (`statusFilter=completed`, not `resultFilter=failed`)
  and forces `-AllLegs` so green legs are scanned too.
- Each quarantine build runs each quarantined test **once per leg**, so a pass/fail **rate** only emerges
  by **aggregating across many builds** over the window (twice-daily × N days). `distinctBuilds` /
  `distinctDays` in `passedTests` are how you gauge that.
- In this mode: `flakyTests` = quarantined tests **still flaking** in 344; `passedTests` = quarantined
  tests **observed passing** there. A test in **both** is still flaky.

### Un-quarantine criteria (avoiding false un-quarantines)

A "green" reading must mean the test **ran and passed**, not merely "was absent from failures" (it could
be absent because a leg did not run or an artifact was missing). Only un-quarantine a test when **all**
hold:

- `scanComplete == true` for the 344 scan (a truncated scan is biased — do nothing).
- the test is in `passedTests` with `distinctBuilds >= 4` **and** `distinctDays >= 3`, **and**
- the test is **not** in the 344 `flakyTests` (zero failures over the window), **and**
- the green evidence covers the platform scope the `[ActiveIssue]` applies to: an **unconditional**
  quarantine needs green legs on **Windows, Linux, and macOS**; a **platform-scoped** one needs green on
  the matching leg(s). When green on only some platforms, **narrow** the attribute rather than removing
  it — never fully un-quarantine on partial evidence.

Un-quarantining does **not** require local reproduction: def 344 is real CI across many builds/days and
is stronger evidence than a single local pass on one OS. Removing the attribute = deleting the
`[ActiveIssue(...)]` line above the test.

## Interpreting Results — Flake vs Regression

The detector surfaces evidence; it does **not** by itself prove flakiness. Before acting:

- **`scanComplete` must be `true`.** A truncated scan is biased — never file issues, edit sources,
  or open a PR from it. Widen `-MaxBuilds` / `-MaxArtifactDownloads` and re-run.
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
| Quarantine the test (`[ActiveIssue]`) | ≥ 5 distinct sources **or** failures spanning multiple days |

## Conventions

- **Existing label:** `flaky-test`.
- **Issue dedup marker:** every flaky-test tracking issue body must contain a hidden, stable
  marker so future runs can find it:
  ```html
  <!-- flaky-test-id: Namespace.Class.Method -->
  ```
  Search both open and recently-closed issues for this marker before filing a new one.
- **Quarantine syntax (use exactly):** quarantine with `[ActiveIssue]` from
  `Microsoft.DotNet.XUnitV3Extensions` (namespace `Xunit`, already referenced and imported by every
  test project) — **not** `[Fact(Skip=...)]`. Add it above the existing `[Fact]`/`[Theory]`, keeping
  the test method intact:
  ```csharp
  [ActiveIssue("https://github.com/dotnet/msbuild/issues/NNNN")]
  [Fact]
  // platform-scoped — re-validated on the matching OS leg of the pipeline (see note below):
  [ActiveIssue("https://github.com/dotnet/msbuild/issues/NNNN", TestPlatforms.Linux)]
  ```
  The issue URL is the tracking-issue URL; do **not** add a `"Flaky:"` prefix. `[ActiveIssue]` stamps
  the `Category=failing` trait, which normal CI excludes and the scheduled `azure-pipelines/quarantine.yml`
  pipeline runs on its own to keep collecting signal on quarantined tests. **Prefer the unconditional
  form** unless the flake is clearly platform-specific: that pipeline runs on **Windows, Linux and
  macOS**, so a platform-scoped quarantine (`Windows`, `Linux`, `OSX`, `AnyUnix`) is still re-validated
  on the matching leg. Only scope when the flake is confined to that platform.
- **Theory granularity:** quarantining a `[Theory]` disables *all* rows. Prefer method-level evidence
  and note the failing parameter distribution (`rawVariants`) before quarantining a `[Theory]`.

## Locating a Test's Source

Map the failing test's **assembly** (from the TRX file name, e.g.
`Microsoft.Build.Engine.UnitTests`) to its test project under `src/` (e.g.
`src/Build.UnitTests/Microsoft.Build.Engine.UnitTests.csproj`) rather than doing a repo-wide
text search. Then locate the class/method within that project.

## Related Workflows

- `.github/workflows/flaky-test-detector.agent.md` — the scheduled **daily** workflow that runs this
  skill end to end: it scans CI, files/updates `flaky-test` tracking issues, then reproduces and either
  applies a minimal determinism fix or quarantines each new candidate. It **also** consumes the
  quarantine-pipeline health (def 344, via `-IncludePassed`) to **un-quarantine** consistently-green
  tests and **re-fix** tests still flaking there, opening **one combined draft PR per run** with all of
  that day's quarantines, fixes, and un-quarantines.
- `azure-pipelines/quarantine.yml` — scheduled (twice-daily) AzDO pipeline (definition **344**) that
  runs **only** the quarantined (`[ActiveIssue]` / `Category=failing`) tests, so quarantined tests keep
  producing pass/fail signal. A test that has gone consistently green there is a candidate to
  un-quarantine (see **Quarantine-Pipeline Health** above for the criteria).
