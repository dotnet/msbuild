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

The wrappers are pinned to `TargetFrameworks=net10.0`. xunit.v3 rejects
`net472` when `OutputType=Library` (which CTS needs so it can host the
test DLL), so we cannot run the .NET Framework leg through CTS today.

What this means in practice:

| TFM            | Regular PR pipeline | CTS pipeline |
| -------------- | ------------------- | ------------ |
| `net10.0`      | ✅                  | ✅           |
| `net472` (Win) | ✅                  | ❌           |

The CTS pipeline is parallel and **non-blocking**; the regular PR
pipeline continues to provide net472 signal and remains the merge gate.
CTS adds incrementality for the net10.0 subset only. Closing the gap
requires either an `OutputType=Exe` net472 wrapper variant (needs
validation that `cts vstest` works against a .NET Framework Exe host) or
a legacy-xunit wrapper for net472.

## Local vs CI

These scripts are **local-only** — they use the filesystem cache under
`<repo>/.cts/` so iterating between collect and apply is instant.

CI runs the same `cts` tool but against Azure DevOps pipeline artifacts as
the snapshot store. Two pipelines drive it:

* `azure-pipelines/cts-collect.yml` — scheduled daily at 06:00 UTC against
  `main`; publishes one baseline artifact per OS (`cts-baseline-<sha>-<os>`).
* `azure-pipelines/cts-apply.yml` — runs in parallel on PRs (non-blocking);
  picks the most recent baseline whose SHA is an ancestor of the PR HEAD,
  downloads it, and runs `cts apply vstest` against `MSBuild.VSTest.slnx`.

Both pipelines emit `cts-metrics.json` per OS (schema documented in
[`METRICS.md`](METRICS.md)) so we can later quantify the incrementality
CTS achieves.

The CI pipelines do **not** call `Collect-Local.ps1` / `Run-Local.ps1` —
they invoke `cts` directly because the storage backend differs. They do
reuse `cts.config.json` verbatim and `Select-Baseline.ps1` for ancestor
resolution.

## Notes

* Baseline + logs live at `<repo>/.cts/` (gitignored).
* The `.VSTest.csproj` wrappers output to
  `artifacts/bin/<MSBuildProjectName>/Debug/net10.0/` — distinct from the
  default MTP variant.
* Demos hardcode per-project demo files via `projects.json` →
  `DemoFiles.{Broad,Narrow,Unrelated}`. Projects without `DemoFiles` skip
  the narrow/broad/unrelated demos.
