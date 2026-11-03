# scripts/cts - Local CTS (Clever Test Selection) harness

These scripts use **native Microsoft.Testing.Platform (MTP)** hosts from the
normal test projects in `MSBuild.slnx`. No separate solution or VSTest wrapper
projects are needed.

MTP is pinned to at least 2.5.1, which includes the UTF-8 JSON-RPC framing fix
from [testfx#10085](https://github.com/microsoft/testfx/pull/10085). xUnit and
Arcade's test extensions remain on the existing compatible versions.

## Layout

| File | Role |
| --- | --- |
| `projects.json` | Local project keys, build paths and demo files |
| `cts.config.json` | Coverage/source filters and native Debug/net11 unit-test DLL discovery |
| `_Common.ps1` | Shared paths, build and registry helpers |
| `Collect-Local.ps1` | `cts collect testingplatform --coverage` |
| `Run-Local.ps1` | `cts apply testingplatform --local-development` |
| `demos/Demo-NoChange.ps1` | Apply with no edits (expect 0 impacted) |
| `demos/Demo-NarrowEdit.ps1` | Touch a narrow source file (expect partial selection) |
| `demos/Demo-BroadEdit.ps1` | Touch a broad source file (expect broad selection) |
| `demos/Demo-UnrelatedEdit.ps1` | Touch a file outside the project (expect 0 impacted) |

Configuration belongs in `projects.json` or `cts.config.json`, not in the
PowerShell. CI discovers all built modules matching
`**/artifacts/bin/*/Debug/net11.0/*.UnitTests.dll`, including Coordinator.
New matching unit-test outputs are automatically included. Add their project
entries to `projects.json` to make them available through the local scripts too.
Use a clean build output directory to avoid collecting stale matching modules.

## Prerequisites

Build the repository once using its normal build instructions to bootstrap the
repo-local SDK. Ensure the global `cts` host can find the .NET 10 runtime and
the native test hosts can find a .NET 11 runtime. The CI steps install both;
the repo-local SDK alone does not install runtimes into the global host.
CI installs the tool through `eng/common/dotnet.ps1` (Windows) or `dotnet.sh`
(Linux), which bootstraps the SDK pinned by `global.json` before the solution
build. Runtime-only agent setup cannot run `dotnet tool update` on its own.

Install the same CTS version used by the collect and apply pipelines:

```powershell
dotnet tool install cts --global --version 2.13.0-alpha.26519.2 `
  --add-source https://devdiv.pkgs.visualstudio.com/_packaging/VS/nuget/v3/index.json
```

Run tool installation outside the repository if its package source mapping
rejects `--add-source`. The `cts` command must resolve on PATH.

## Workflow

```powershell
# 1. Collect a native baseline (clean working tree required)
.\scripts\cts\Collect-Local.ps1 -Project StringTools

# 2. Make changes, rebuild, and run only impacted tests
.\scripts\cts\Run-Local.ps1 -Project StringTools

# 3. Inspect selection
.\scripts\cts\demos\Demo-NoChange.ps1       -Project StringTools
.\scripts\cts\demos\Demo-NarrowEdit.ps1     -Project StringTools
.\scripts\cts\demos\Demo-BroadEdit.ps1      -Project StringTools
.\scripts\cts\demos\Demo-UnrelatedEdit.ps1  -Project StringTools
```

Omit `-Project` to operate on all nine registered projects. `Run-Local.ps1`
builds by default; use `-SkipBuild` only when the binaries already match the
source being tested. The demos illustrate file-level selection using a blank
line edit and existing binaries, not validation of a changed implementation.
Run demos only with clean source files: they revert their touched file on exit.

Native local baselines live in `.cts/mtp-baseline`, separate from the former
VSTest cache. **Collect a fresh native baseline after upgrading.** Do not reuse
VSTest baselines; their test identities are not assumed interchangeable.
Logs remain in `.cts/logs`. These directories are gitignored.

## Coverage gap vs the regular PR pipeline

CTS targets Debug/net11 unit-test outputs, including Coordinator in addition to
the original eight projects. This migration does not add net472, macOS or Release.
Native net472 operation has not been validated.

| Target | Regular PR pipeline | CTS pipeline |
| --- | --- | --- |
| Debug/net11 unit-test modules, Windows/Linux | Yes | Yes |
| .NET Framework, Windows | Yes | Not validated |
| Remaining regular CI matrix | Yes | No |

The regular PR pipeline remains the merge gate. CTS is a separate,
**non-blocking shadow experiment**, not a replacement for full tests.

CTS starts test hosts directly, so MSBuild's `XunitOptions` trait exclusions
are not automatically forwarded. This was also true of the old CTS scripts:
shadow runs can include quarantined/platform-specific cases excluded from
regular CI. Keep their failures visible when interpreting CTS results.
The pinned CTS tool's `--test-case-filter` forwards `--filter`, which this
xUnit runner does not support; it is not a substitute for xUnit's
`--filter-not-trait` arguments.

## Local vs CI

Local scripts use the filesystem cache and currently target Windows.
Non-Windows local users should invoke `cts` directly. CI does not source
`_Common.ps1`.

The existing two pipelines remain separate:

* `azure-pipelines/cts-collect.yml` runs daily at 03:00 UTC against `main`.
  It builds `MSBuild.slnx`, runs `cts collect testingplatform --coverage`,
  and publishes `cts-mtp-baseline-<os>` plus `cts-collect-metrics-<os>`.
* `azure-pipelines/cts-apply.yml` runs alongside regular PR CI, builds the
  same solution and runs `cts apply testingplatform`. It downloads only
  native `cts-mtp-baseline-<os>` artifacts. Until the first native collect
  succeeds, or when the baseline is unavailable, apply skips CTS and emits
  fallback metrics; the regular pipeline still runs all its tests.

The new artifact names prevent old VSTest snapshots from being consumed.
Keep the collect/apply `ctsToolVersion` pins identical when updating them.
Metrics retain their existing schema; see [METRICS.md](METRICS.md).

Pipeline consolidation, build reuse and replacing full PR tests with CTS
selection are separate decisions and are not part of this migration.
