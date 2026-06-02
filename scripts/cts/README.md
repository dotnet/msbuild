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

## Notes

* Baseline + logs live at `<repo>/.cts/` (gitignored).
* The `.VSTest.csproj` wrappers output to
  `artifacts/bin/<MSBuildProjectName>/Debug/net10.0/` — distinct from the
  default MTP variant.
* Demos hardcode per-project demo files via `projects.json` →
  `DemoFiles.{Broad,Narrow,Unrelated}`. Projects without `DemoFiles` skip
  the narrow/broad/unrelated demos.
