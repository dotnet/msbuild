---
name: running-unit-tests
description: Guide for running MSBuild unit tests efficiently. Use when running, scoping, filtering, or speeding up unit tests in this repository, or when finalizing a change with a heavier validation pass. Covers xUnit v3 + Microsoft.Testing.Platform (MTP) specifics and which `dotnet test` flags do and don't apply.
argument-hint: Run, filter, scope, or speed up MSBuild unit tests.
---

# Running MSBuild Unit Tests

This repo uses **xUnit v3** with the **Microsoft.Testing.Platform (MTP)** runner. Test projects are built as `OutputType=Exe`, so each test assembly is a self-contained host process — *not* a classic VSTest assembly.

There are **two ways to run tests**:

| Method | When to use |
|--------|-------------|
| `dotnet test <project>` | Dev loop — run a single test project, optionally filtered |
| `build.cmd -test` / `build.sh --test` | Final validation — builds everything and runs all test projects via the repo's Arcade harness |

Use a **fast, scoped** `dotnet test` loop while iterating, and the **full `build.cmd -test`** pass before declaring the change done.

## Repo-specific knobs to know about

These are configured in `Directory.Build.props` (repo root), `src/Directory.Build.targets`, and `src/Shared/UnitTests/xunit.runner.json`. They apply to both `dotnet test` and `build.cmd -test` unless noted otherwise:

- **Multi-targeting**: test projects target `net472` *and* `net10.0` on Windows (`net10.0` only on Linux/macOS). `dotnet test` runs the suite **once per TFM**.
- **Single-threaded by default**: `xunit.runner.json` sets `maxParallelThreads: 1` and `parallelizeTestCollections: false`. Many tests mutate process-global state (env vars, cwd, SDK resolvers), so this is intentional.
- **Auto trait filters**: platform/TFM-inappropriate tests are filtered out via `--filter-not-trait Category=...` (e.g., `nonwindowstests`, `failing`, `nonnetcoreapptests`). Don't try to "fix" tests that appear skipped because of these.
- **Coverage on non-Windows**: `--coverage --coverage-settings Coverage.config` is appended unconditionally to `XunitOptions` in `src/Directory.Build.targets`. There is no MSBuild property switch to disable it from `dotnet test` — to skip coverage, run the test exe directly without `--coverage`.
- **Test runner**: `TestRunnerName=XUnitV3`, MTP `1.9.1`, xUnit v3 `3.2.2` (set in repo-root `Directory.Build.props`).
- **DOTNET_HOST_PATH**: `RunnerUtilities.GetMSBuildEnvironmentVariables` (in `src/UnitTests.Shared/RunnerUtilities.cs`) sets `DOTNET_HOST_PATH` to the bootstrap dotnet when tests launch MSBuild as a child process, so tasks like `RoslynCodeTaskFactory` resolve the right host. Don't override `DOTNET_HOST_PATH` from a test.

## Flags — MTP vs VSTest pitfalls

This repo uses MTP, **not** VSTest. Many familiar `dotnet test` flags silently do nothing. Key differences:

- **Use** `--report-trx` (not `--logger trx`), `--coverage` (not `--collect "XPlat Code Coverage"`), `--filter-method`/`--filter-class`/`--filter-trait` for xUnit v3 native filtering.
- **Don't use** `--nologo`, `--blame`, `--settings *.runsettings`, `--diag`, `--collect`, or `-- RunConfiguration.MaxCpuCount=...` — these are VSTest-only and ignored.
- **Still works**: `-c`, `-f`, `--no-restore`, `--no-build`, `-v`, `-bl:`, `-p:Property=Value` — these are interpreted by `dotnet test` itself before MTP sees them.

For comprehensive MTP vs VSTest flag reference, see the `run-tests` skill from the `dotnet-test` plugin.

## Dev loop: fast and scoped

Aim for sub-30s iterations.

> Examples below use Windows-style backslashes and PowerShell line continuations. Forward slashes work everywhere with `dotnet` (`src/Tasks.UnitTests/Microsoft.Build.Tasks.UnitTests.csproj`); on Linux/macOS use `/` and shell line continuations (`\`), and the exe path becomes `artifacts/bin/<Proj>/Debug/net10.0/<Proj>` (no `.exe`).

1. **Run via `dotnet test` with a single TFM and filter** (incremental build handles rebuilding automatically):
   ```powershell
   dotnet test src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.csproj `
     -f net10.0 --filter "FullyQualifiedName~MyFeature"
   ```
2. **Or run the MTP exe directly** (fastest — no SDK overhead; build first if source changed):
   ```powershell
   dotnet build src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.csproj -c Debug -f net10.0
   artifacts\bin\Microsoft.Build.Tasks.UnitTests\Debug\net10.0\Microsoft.Build.Tasks.UnitTests.exe `
     --filter-method "*MyFeature*" --no-progress
   ```
   The exe path is TFM-specific: `Debug\net10.0\...exe` for net10.0, `Debug\net472\...exe` for net472. Switching TFM without rebuilding silently runs stale binaries.

### Speeding up the dev loop further

These trade safety for speed — use during iteration, **revert before final validation**:

- **Single TFM**: pass `-f net10.0`. Halves runtime on Windows by skipping `net472`.
- **Temporarily relax single-threaded execution**: drop a `xunit.runner.json` next to the test project (or override the existing one) with:
  ```json
  {
    "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
    "longRunningTestSeconds": 60,
    "maxParallelThreads": -1,
    "parallelizeTestCollections": true
  }
  ```
  Or pass it as MTP args after `--`: `-- xUnit.MaxParallelThreads=-1 xUnit.ParallelizeTestCollections=true`. Expect flakes in tests that touch env vars, cwd, the file system, or the global ProjectCollection — **don't ship a fix that depends on this being on**.
- **Skip bootstrap packaging** for non-bootstrap test projects:
  `dotnet build ... -p:CreateBootstrap=false` (useful when the local bootstrap SDK payload is missing).
- **Narrow with traits**: `--filter-trait Category=mytraitduringdev` if you've tagged a focused subset.
- **Skip code coverage on Linux/macOS**: run the test exe directly without `--coverage`. This repo does not expose a supported `dotnet test` property switch to disable the auto-added coverage arguments.

## Final validation pass

Before saying "done," run the heavy configuration. Don't skip TFMs and don't keep parallel-overrides.

1. **Restore parallelism settings** (revert any local `xunit.runner.json` change).
2. **Run all TFMs for affected projects**:
   ```powershell
   dotnet test src\Build.UnitTests\Microsoft.Build.Engine.UnitTests.csproj -c Release
   dotnet test src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.csproj -c Release
   # Add other UnitTests projects whose code paths you touched
   ```
3. **For broad changes (engine, framework, shared)**, run the full repo test suite (~9 minutes — do not cancel):
   ```powershell
   .\build.cmd -test -c Release       # Windows
   ./build.sh --test -c Release       # Linux/macOS
   ```
4. **Capture a TRX** if reporting results to the user or CI:
   ```powershell
   dotnet test <project> -c Release `
     --report-trx --report-trx-filename validation.trx `
     --results-directory artifacts\TestResults
   ```

## Reading the summary line

MTP's final summary reports `passed`, `failed`, and `skipped` separately. **Always check the `skipped` count** — platform-conditional attributes (`WindowsOnlyFact`, `UnixOnlyFact`, etc.) and the auto trait filters cause expected skips, but a sudden jump in skipped count can hide a regression where a test became inapplicable on the current platform without you intending it.

## Picking the right project to run

Match the source area you changed to its `*.UnitTests` project:

| Source area | Test project |
|-------------|--------------|
| `src/Build/**` (engine, evaluation, backend) | `src/Build.UnitTests/Microsoft.Build.Engine.UnitTests.csproj` |
| `src/Framework/**` | `src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj` |
| `src/Tasks/**` | `src/Tasks.UnitTests/Microsoft.Build.Tasks.UnitTests.csproj` |
| `src/Utilities/**` | `src/Utilities.UnitTests/Microsoft.Build.Utilities.UnitTests.csproj` |
| `src/MSBuild/**` (CLI) | `src/MSBuild.UnitTests/Microsoft.Build.CommandLine.UnitTests.csproj` |
| `src/Build/BuildCheck/**` | `src/BuildCheck.UnitTests/Microsoft.Build.BuildCheck.UnitTests.csproj` |
| `src/Shared/**` | Run the consumers above (Build, Tasks, Utilities) — shared code is linked into all of them. |

## Quick reference

| Scenario | Command |
|----------|---------|
| Fast scoped dev loop | `dotnet test <proj> -f net10.0 --filter "FullyQualifiedName~X"` |
| Direct MTP exe | `artifacts\bin\<Proj>\Debug\net10.0\<Proj>.exe --filter-method "*X*" --no-progress` |
| Single test by name | `dotnet test <proj> --filter "FullyQualifiedName~MyTestMethod"` |
| Final per-project validation | `dotnet test <proj> -c Release` (all TFMs) |
| Final full validation | `.\build.cmd -test -c Release` / `./build.sh --test -c Release` |
| TRX report | `--report-trx --report-trx-filename out.trx --results-directory artifacts\TestResults` |

## See also

- `.github/instructions/tests.instructions.md` — test authoring conventions (xUnit v3, Shouldly, `TestEnvironment`, `MockLogger`).
- `src/Shared/UnitTests/xunit.runner.json` — repo-wide xUnit settings.
- `src/Directory.Build.targets` — `XunitOptions`, auto trait filters, coverage wiring.
- `documentation/wiki/Building-Testing-and-Debugging-on-Full-Framework-MSBuild.md`
