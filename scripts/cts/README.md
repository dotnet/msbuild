# scripts/cts — Local CTS (Clever Test Selection) harness

These scripts run CTS against this repository in **VSTest mode**, which avoids
the MTP↔CTS JsonRpc hang we hit during the original adoption attempt (see
`artifacts/cts/bisect/BISECT.md`).

They drive the **sibling `*.UnitTests.VSTest.csproj`** wrappers next to each
test project. Each wrapper imports the original `.csproj` so the test surface
stays in sync; only the runner stack (Microsoft.NET.Test.Sdk +
`xunit.runner.visualstudio`, no MTP) differs.

## Prerequisites

```powershell
dotnet tool install cts --global --prerelease --add-source https://devdiv.pkgs.visualstudio.com/_packaging/VS/nuget/v3/index.json
```

The `cts` command must resolve on PATH. A `.cts/` directory at repo root is
used as the local filesystem cache (gitignored).

## Workflows

### `Collect-Local.ps1` — establish baseline

Builds every `*.UnitTests.VSTest.csproj` and runs `cts collect vstest` against
each. The result is a baseline keyed off the current `HEAD` SHA, stored in
`.cts/baseline/`.

```powershell
.\scripts\cts\Collect-Local.ps1                # all projects
.\scripts\cts\Collect-Local.ps1 -Project Framework    # one project
.\scripts\cts\Collect-Local.ps1 -SkipBuild     # reuse previous build outputs
```

CTS requires a clean working tree to collect — the script aborts if `git
status` is dirty.

### `Run-Local.ps1` — incremental test run

Builds the VSTest variants incrementally and asks CTS which tests are
impacted by your working-tree changes (`cts apply vstest`). Only those tests
are run.

```powershell
.\scripts\cts\Run-Local.ps1                    # all impacted tests
.\scripts\cts\Run-Local.ps1 -Project StringTools     # one project
.\scripts\cts\Run-Local.ps1 -SkipBuild         # if you just built
```

If no baseline exists, the script reminds you to run `Collect-Local.ps1`.

## Notes

* Local cache lives at `<repo>/.cts/` (baseline + logs); gitignored.
* The `*.UnitTests.VSTest.csproj` wrappers output to
  `artifacts/bin/<MSBuildProjectName>/Debug/net10.0/` — distinct from the
  MTP variant produced by the regular build.
* Filter passed to `cts` is the relative path glob to that DLL, so impact
  analysis stays scoped to the VSTest binaries only.
