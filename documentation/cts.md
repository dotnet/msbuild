# Clever Test Selection (CTS)

> **⚠️ STATUS**: CTS hangs indefinitely during test execution with Microsoft.Testing.Platform (xUnit v3).  
> The configuration is complete but blocked by a CTS/testingplatform integration issue.  
> Tracked in the adoption branch `adopt-cts`.

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

## Known Issues

**CTS hangs during test execution** (as of 2026-05-26, CTS v2.8.0-alpha.26271.2):
- Discovery completes successfully (91 tests found in StringTools.UnitTests)
- Execution phase (`=== Run tests ===`) hangs indefinitely
- Test hosts connect to CTS but no tests run
- Affects both `--dop 1` and default parallelism
- Workaround: None identified yet; may require CTS team investigation

## Test Environment Compatibility

MSBuild tests use `EnvironmentInvariant` to detect environment pollution. CTS compatibility required:
- Ignoring .NET profiler vars (`CORECLR_PROFILER`, `MicrosoftInstrumentationEngine_*`, etc.)
- Ignoring MSBuild CLI vars (`MSBuildLoadMicrosoftTargetsReadOnly`, `MSBUILDLOADALLFILESASWRITEABLE`)

These exemptions are in `src/UnitTests.Shared/TestEnvironment.cs`.

## Notes

- If a modified source file is outside `SourceCodeFiles.Include` or matches
  `SourceCodeFiles.Exclude`, CTS conservatively runs **all** tests in the
  affected test modules.
- `Filter.Include` in `cts.json` targets `artifacts/bin/**/Debug/net10.0/*UnitTests*.dll`.
  Other configurations (Release, net472) are not included.
- The scripts use `cts collect|apply testingplatform` (not `vstest`) because MSBuild
  tests run on Microsoft.Testing.Platform + xUnit v3, not VSTest.

