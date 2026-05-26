# Clever Test Selection (CTS)

[CTS](https://devdiv.visualstudio.com/DevDiv/_git/CodeCoverage) is an internal
test-impact-analysis tool. It records, for each test, the set of source files
the test exercised, and can later run only the tests affected by a given diff.

This repo is wired up for CTS on the **Debug** configuration via:

| File | Purpose |
|------|---------|
| `cts.json` | Source/module/filter configuration |
| `eng/cts/collect.ps1` | Builds a baseline by running all Debug tests |
| `eng/cts/apply.ps1` | Runs only tests affected by local changes |

## Prerequisites

Install the `cts` global tool (one-time):

```powershell
dotnet tool install cts --global --prerelease `
    --add-source https://devdiv.pkgs.visualstudio.com/_packaging/VS/nuget/v3/index.json `
    --interactive
```

The feed is internal; you'll need the [Azure Artifacts Credential Provider](https://github.com/microsoft/artifacts-credprovider).

## Usage

Build the repo in Debug first so the test DLLs exist under `artifacts/bin/**/Debug/`:

```powershell
.\build.cmd -v quiet
```

Create a baseline (do this on a clean checkout of `main`):

```powershell
.\eng\cts\collect.ps1 -Tag main
```

Then, after making changes, run only the impacted tests:

```powershell
.\eng\cts\apply.ps1 -Tag main
```

The baseline lives in `artifacts/cts/baseline` and logs in `artifacts/cts/logs`,
both of which are inside the gitignored `artifacts/` tree.

## Notes

- If a modified source file is outside `SourceCodeFiles.Include` or matches
  `SourceCodeFiles.Exclude`, CTS conservatively runs **all** tests in the
  affected test modules.
- `Filter.Include` in `cts.json` targets `artifacts/bin/**/Debug/**/*UnitTests*.dll`.
  Other configurations (Release, etc.) are not currently wired up; pass
  `--filter` to override.
