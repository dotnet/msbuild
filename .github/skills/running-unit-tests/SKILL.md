---
name: running-unit-tests
description: Guide for running MSBuild unit tests efficiently. Use when running, scoping, filtering, or speeding up unit tests in this repository, or when finalizing a change with a heavier validation pass. Covers xUnit v3 + Microsoft.Testing.Platform (MTP) specifics and which `dotnet test` flags do and don't apply.
---

# Running MSBuild Unit Tests

This repo uses **xUnit v3** with the **Microsoft.Testing.Platform (MTP)** runner. Test projects are built as `OutputType=Exe`, so each test assembly is a self-contained host process — *not* a classic VSTest assembly. That changes which CLI flags are useful and which silently do nothing.

Use a **fast, scoped** loop while iterating, and a **heavier, complete** pass before declaring the change done.

## Repo-specific knobs to know about

These are baked into `src/Directory.Build.props`, `src/Directory.Build.targets`, and `src/Shared/UnitTests/xunit.runner.json` — they affect every test run:

- **Multi-targeting**: test projects target `net472` *and* `net10.0` on Windows (`net10.0` only on Linux/macOS). `dotnet test` runs the suite **once per TFM**.
- **Single-threaded by default**: `xunit.runner.json` sets `maxParallelThreads: 1` and `parallelizeTestCollections: false`. Many tests mutate process-global state (env vars, cwd, SDK resolvers), so this is intentional.
- **Auto trait filters**: platform/TFM-inappropriate tests are filtered out via `--filter-not-trait Category=...` (e.g., `nonwindowstests`, `failing`, `nonnetcoreapptests`). Don't try to "fix" tests that appear skipped because of these.
- **Coverage on non-Windows**: `--coverage --coverage-settings Coverage.config` is added automatically when `IsUnitTestProject=true`.
- **Test runner**: `TestRunnerName=XUnitV3`, MTP `1.9.1`, xUnit v3 `3.2.2`.
- **Assembly fixtures**: `MSBuildTestAssemblyFixture` sets `DOTNET_HOST_PATH` to the bootstrap dotnet so tasks like `RoslynCodeTaskFactory` work in tests. Don't override `DOTNET_HOST_PATH`.

## Flags and arguments — what to use, what to skip

`dotnet test` forwards unknown args to the MTP runner. With MTP + xUnit v3, prefer the MTP-native flags below.

### Use these (MTP / xUnit v3 native)

| Need | Flag |
|------|------|
| Filter by fully qualified name substring | `--filter "FullyQualifiedName~SomeMethod"` (translated by the SDK) — or run the test exe directly with `--filter-method "*.SomeMethod"` |
| Filter by trait | `--filter-trait Category=foo` / `--filter-not-trait Category=failing` |
| Filter by class / namespace | `--filter-class "Microsoft.Build.UnitTests.SomeClass"` / `--filter-namespace "Microsoft.Build.UnitTests"` |
| Quiet, CI-friendly output | `--no-progress --no-ansi` |
| TRX report | `--report-trx --report-trx-filename my.trx` |
| Results directory | `--results-directory artifacts/TestResults` |
| Hard timeout | `--timeout 5m` |
| Repeat to surface flakes | `--retry-failed-tests 3` |
| Tree-style output | `--output detailed` |

### Do NOT use these (silently no-op or wrong runner)

- `--nologo` / `--no-logo` — not an MTP flag. MTP's banner is controlled by `--no-progress`/`--no-ansi`.
- `--logger "trx;LogFileName=..."` / `--logger console;verbosity=...` — VSTest loggers; ignored by MTP. Use `--report-trx` instead.
- `--collect "XPlat Code Coverage"` — VSTest collector; coverage with MTP uses `--coverage`.
- `--blame`, `--blame-hang`, `--blame-crash` — VSTest-only.
- `--settings *.runsettings` — `.runsettings` is VSTest-only and ignored by MTP runs in this repo.
- `dotnet test -- RunConfiguration.MaxCpuCount=...` — VSTest MSBuild-style args; doesn't apply.
- `--diag` — VSTest-only.

### `dotnet test` flags that *do* still work

`-c Release`, `-f net10.0`, `--no-restore`, `--no-build`, `-v q|m|n|d|diag`, `-bl:path.binlog`, `-p:Property=Value`. These are interpreted by `dotnet test` itself before MTP sees them.

## Dev loop: fast and scoped

Aim for sub-30s iterations. Build once, then run the test exe directly to skip restore/build/discovery overhead.

1. **Build the test project once** (or `./build.cmd` if you've changed engine code):
   ```powershell
   dotnet build src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.csproj -c Debug -f net10.0
   ```
2. **Run via `dotnet test` with `--no-restore --no-build` and a single TFM**:
   ```powershell
   dotnet test src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.csproj `
     -f net10.0 --no-restore --no-build `
     --filter "FullyQualifiedName~MyFeature"
   ```
3. **Or run the MTP exe directly** (fastest — no SDK overhead):
   ```powershell
   artifacts\bin\Microsoft.Build.Tasks.UnitTests\Debug\net10.0\Microsoft.Build.Tasks.UnitTests.exe `
     --filter-method "*MyFeature*" --no-progress
   ```

### Speeding up the dev loop further

These trade safety for speed — use during iteration, **revert before final validation**:

- **Single TFM**: pass `-f net10.0`. Halves runtime on Windows by skipping `net472`.
- **Use `--no-restore --no-build`**: only after a successful build. If you change source, you must rebuild.
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
- **Skip code coverage on Linux/macOS**: run the test exe directly without `--coverage`, or pass `-p:CollectCoverage=false`.

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

## Authoring tip: keep tests scoped-friendly

When adding tests, make them filterable and parallel-safe where possible. Prefer `TestEnvironment.Create(_output)` for state cleanup so a future relaxation of `maxParallelThreads` doesn't break them. See [`tests.instructions.md`](../../instructions/tests.instructions.md).

## Quick reference

| Scenario | Command |
|----------|---------|
| Fast scoped dev loop | `dotnet test <proj> -f net10.0 --no-restore --no-build --filter "FullyQualifiedName~X"` |
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
