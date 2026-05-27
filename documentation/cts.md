# Clever Test Selection (CTS)

> **⚠️ STATUS**: CTS test execution hangs after the first batch of test invocations.
> The repo is configured for CTS on the **Debug** configuration, but actually using
> it end-to-end is blocked by an integration issue between CTS and MSBuild's tests
> running on Microsoft.Testing.Platform v2 + xUnit v3.
>
> Tracked on the `adopt-cts` branch.

[CTS](https://devdiv.visualstudio.com/DevDiv/_git/CodeCoverage) is an internal
test-impact-analysis tool. It records, for each test, the set of source files
the test exercised, and can later run only the tests affected by a given diff.

This repo is wired up for CTS via:

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

## Test runner setup

CTS requires Microsoft.Testing.Platform v2 (MTP v2). MSBuild test projects use
xUnit v3 through Arcade's `TestRunnerName=XUnitV3`, which by default pulls in the
older MTP v1 adapter (`xunit.v3.mtp-v1`). To work with CTS we override that:

- `_MSBuildMTPPin` in `Directory.Build.props` pinned to **MTP 2.2.1**.
- `_MSBuildXUnitV3Pin` pinned to **3.2.2** (xUnit v3).
- `Microsoft.Testing.Extensions.CodeCoverage` pinned to **18.7.0** (the older
  18.0.6 implements an MTP v1-only interface and crashes under MTP v2 with
  `TypeLoadException: Method 'OnTestSessionStartingAsync' ... does not have an
  implementation.`).
- `src/Directory.Build.targets` adds an explicit `xunit.v3.mtp-v2` PackageReference
  for `.NETCoreApp` test projects and uses a `_RemoveXunitMtpV1References` target
  to drop the conflicting `xunit.v3.mtp-v1.dll` from compile/runtime references.

After these changes, running a test project's `.exe` directly reports
`xUnit.net v3 Microsoft.Testing.Platform v2 Runner` and all tests pass.

## Known Issues

**CTS test execution hangs after the first batch** (as of 2026-05-27,
CTS v2.8.0-alpha.26271.2 with the MTP v2 / CodeCoverage 18.7.0 fixes above):

- Build, discovery, and the first ~9 test invocations succeed.
  The diagnostic logs under `artifacts/cts/logs/<TestProject>/testing/runTests/`
  show each spawned MTP server process running its 1 assigned test and exiting
  cleanly with `Total: 1, Errors: 0, Failed: 0`.
- After roughly `--dop` invocations, no further runTests processes are spawned and
  CTS sits idle (low CPU, no new diagnostic files).
- Reproducible with `--dop 1`, `--dop 32`, and any single test project (validated
  on `StringTools.UnitTests`).
- Earlier we also saw `System.TypeLoadException` from
  `Microsoft.Testing.Extensions.CodeCoverage 18.0.6` against MTP 2.x. That is
  fixed by upgrading the package to 18.7.0; the post-fix hang is a separate
  issue.

Workaround: none identified; needs investigation by the CTS team. See the
standalone repro at <https://github.com/jankratochvilcz/cts-xunit3> for the
simpler MTP-v2 + xUnit v3 sample that successfully runs all 7 tests end-to-end.
The difference between the two appears to be related to MSBuild's larger test
volume / xunit.runner.json single-threaded settings.

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
