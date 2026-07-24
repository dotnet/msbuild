# scripts/cts — Local CTS (Clever Test Selection) harness

These scripts run CTS against this repository in **VSTest mode**, which
avoids the MTP↔CTS JsonRpc hang we hit during the initial adoption attempt.

They drive the **sibling `*.UnitTests.VSTest.csproj`** wrappers next to each
test project. Each wrapper imports the original `.csproj` so the test
surface stays in sync; only the runner stack differs.

## Layout

| File                       | Role                                                       |
| -------------------------- | ---------------------------------------------------------- |
| `projects.json`            | Registry of `*.UnitTests.VSTest` projects + demo files     |
| `cts.config.json`          | CTS configuration (Modules / SourceCodeFiles / Filter)     |
| `cts.config.mtp.json`      | Experimental MTP-mode config (see [MTP mode](#mtp-mode-experimental)) |
| `_Common.ps1`              | Tiny PS helpers (paths, build, registry lookup)            |
| `Collect-Local.ps1`        | `cts collect vstest --coverage` → baseline in `.cts/`      |
| `Run-Local.ps1`            | `cts apply vstest --local-development` → impacted-only run |
| `demos/Demo-NoChange.ps1`    | Apply with no edits (expect 0 impacted)                   |
| `demos/Demo-NarrowEdit.ps1`  | Touch a narrow source file (expect partial selection)     |
| `demos/Demo-BroadEdit.ps1`   | Touch a broad source file (expect ~all selected)          |
| `demos/Demo-UnrelatedEdit.ps1` | Touch a file outside the project (expect 0 impacted)    |

Anything you would tweak as configuration belongs in `projects.json` or
`cts.config.json`, not in the PowerShell.

## Prerequisites

```powershell
dotnet tool install cts --global --prerelease `
  --add-source https://devdiv.pkgs.visualstudio.com/_packaging/VS/nuget/v3/index.json
```

The `cts` command must resolve on PATH. A `.cts/` directory at repo root is
used as the local filesystem cache (gitignored).

## Workflow

```powershell
# 1. Collect baseline (clean working tree required)
.\scripts\cts\Collect-Local.ps1 -Project StringTools

# 2. Make changes, then run only impacted tests
.\scripts\cts\Run-Local.ps1 -Project StringTools

# 3. See it in action
.\scripts\cts\demos\Demo-NoChange.ps1     -Project StringTools
.\scripts\cts\demos\Demo-NarrowEdit.ps1   -Project StringTools
.\scripts\cts\demos\Demo-BroadEdit.ps1    -Project StringTools
.\scripts\cts\demos\Demo-UnrelatedEdit.ps1 -Project StringTools
```

Omit `-Project` to operate on every project registered in `projects.json`.

## Coverage gap vs the regular PR pipeline

The wrappers are pinned to latest .NETCoreApp. xunit.v3 rejects
`net472` when `OutputType=Library` (which CTS needs so it can host the
test DLL), so we cannot run the .NET Framework leg through CTS today.

What this means in practice:

| TFM            | Regular PR pipeline | CTS pipeline |
| -------------- | ------------------- | ------------ |
| `NETCoreApp`   | ✅                  | ✅           |
| `.NETFramework` (Win) | ✅           | ❌           |

The CTS pipeline is parallel and **non-blocking**; the regular PR
pipeline continues to provide net472 signal and remains the merge gate.
CTS adds incrementality for the .NETCoreApp subset only. Closing the gap
requires either an `OutputType=Exe` net472 wrapper variant (needs
validation that `cts vstest` works against a .NET Framework Exe host) or
a legacy-xunit wrapper for .NETFramework.

## Local vs CI

These scripts are **local-only** — they use the filesystem cache under
`<repo>/.cts/` so iterating between collect and apply is instant. They
target Windows; the CI pipelines invoke `cts` directly and do not source
`_Common.ps1`, so non-Windows local users should call `cts` themselves.

CI runs the same `cts` tool but against ADO pipeline artifacts as the
snapshot store. Two pipelines drive it:

* `azure-pipelines/cts-collect.yml` — scheduled daily at 03:00 UTC against
  `main`; uses `--storage-type filesystem` locally on the agent and
  publishes the snapshot directory as `cts-baseline-<os>` (one slot per
  OS, overwritten daily) plus a sibling `cts-collect-metrics-<os>`
  artifact containing the SHA the baseline was taken at.
* `azure-pipelines/cts-apply.yml` — runs in parallel on PRs (non-blocking);
  downloads the latest `cts-baseline-<os>` from main via
  `DownloadPipelineArtifact@2`, runs `cts apply vstest` against
  `MSBuild.VSTest.slnx`. If `collectPipelineId` isn't configured yet (or
  download fails), the apply step is skipped and only metrics are emitted
  — we do **not** duplicate the regular PR pipeline's full test run.

Both pipelines emit `cts-metrics.json` per OS (schema documented in
[`METRICS.md`](METRICS.md)) so we can later quantify the incrementality
CTS achieves.

`Check-SlnxParity.ps1` verifies that `MSBuild.VSTest.slnx`'s production
project list matches `MSBuild.slnx`'s so additions to one solution don't
silently skip the other. It is wired into `azure-pipelines/cts-apply.yml` as
a **non-blocking** PR-time step, and can also be run locally:
`pwsh ./scripts/cts/Check-SlnxParity.ps1`.

## MTP mode (experimental)

> The VSTest path above remains the supported, non-blocking CI mode. The MTP
> path below is opt-in and **not yet wired into CI** — see the caveat.

CTS originally could not drive this repo's tests in their native
**Microsoft.Testing.Platform (MTP)** form (the default xUnit.v3 runner) because
the CTS↔host JsonRpc session could hang indefinitely — the reason the VSTest
wrappers exist. That hang has two independent causes, both now addressed in the
CTS tool itself (driver resilience: wait for real host exit + bounded output
drain, and an **opt-in inactivity timeout** that kills a host which has gone
completely silent and lets CTS retry it on a fresh process):

1. A Windows stdout/stderr pipe-inheritance leak past host exit.
2. A wedged JSON-RPC handshake with no timeout.

`cts.config.mtp.json` drives the **regular `*.UnitTests` MTP DLLs directly**
(no `.VSTest` wrapper): `Filter.Include` targets
`**/artifacts/bin/*.UnitTests/Debug/net10.0/*.UnitTests.dll`, and
`RunConfiguration.TestHostResponseTimeoutSeconds` (120s here) opts into the new
inactivity timeout. This is **not** a cap on total batch time — it is the
maximum time the host may go without producing *any* activity (a test-node
update or a client log) before CTS treats it as wedged. Every update from the
host resets the countdown, so a batch that keeps streaming progress runs as long
as it needs; only a host that falls silent for the whole window is killed and
retried. The CTS default is `0` (disabled), so the timeout only engages because
this config sets it. Point `cts collect testingplatform` /
`cts apply testingplatform` at this config instead of `cts.config.json`.

**Caveat (why this is still experimental):** with xUnit.v3 3.2.2, a specific
batch of tests can deterministically deadlock the test host in MTP *server*
mode (the auto-generated entry point blocks sync-over-async on its run task).
The CTS timeout+retry converts that from an infinite hang into a bounded
outcome, but the deadlocking batch can still surface as a spurious failure.
Fully green MTP-mode CTS is therefore gated on an upstream xUnit.v3 fix; until
then, VSTest mode remains the CI path.


* The `.VSTest.csproj` wrappers output to
  `artifacts/bin/<MSBuildProjectName>/Debug/net11.0/` — distinct from the
  default MTP variant.
* Demos hardcode per-project demo files via `projects.json` →
  `DemoFiles.{Broad,Narrow,Unrelated}`. Projects without `DemoFiles` skip
  the narrow/broad/unrelated demos.
