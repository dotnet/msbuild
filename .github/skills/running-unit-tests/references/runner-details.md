# MSBuild runner details

Read the section needed for the selected test path. Versions and configuration values below come from linked sources, not a permanently pinned copy of their current values.

## SDK and runner selection

[global.json](../../../../global.json) selects both the SDK and the MTP `dotnet test` experience. [Directory.Build.props](../../../../Directory.Build.props) selects `XUnitV3`, executable test projects, and **minimum** xUnit/MTP versions; Arcade can supply newer versions. [src/Directory.Build.props](../../../../src/Directory.Build.props) controls library/runtime TFMs and their platform conditions.

For the MTP-native CLI, select with `--project` or `--solution`, not the legacy positional project syntax. Runner arguments can follow `--`; keep their names/values together. The selected SDK and registered test extensions determine supported options.

The CI-only [MSBuild.VSTest.slnx](../../../../MSBuild.VSTest.slnx) uses separate `*.UnitTests.VSTest.csproj` wrappers for Clever Test Selection. Those deliberately disable the MTP runner; do not apply MTP flags to them or change their runner as part of an ordinary local test task.

Authoritative references: [dotnet test runner selection](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test) and [MTP command reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp). Use current local help when the installed SDK differs from the documentation.

If a repo-local host needs runtime discovery, set its installation directory as `DOTNET_ROOT` and prepend that directory to `PATH` in the same shell invocation. Do not set it to a different SDK installation. A shell tool may start a fresh process on each call.

## Scoped examples

Replace the project, `<TFM>`, and selectors with the intended values:

```powershell
dotnet test --project src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.csproj `
  --framework <TFM> -- --filter-method "*Feature*"
```

Choose the smallest existing method/class/trait filter supported by the runner. Preserve existing runner configuration and check the executed count. An empty selection can hide a misspelled method or the wrong project.

Use `--no-build` only when the selected configuration/TFM's outputs are current; it also skips restore. It does not establish that an existing test executable corresponds to the edited source.

For all applicable TFMs of an affected project, omit the single-framework selector. Add `--configuration Release` only when Release behavior or the requested validation requires it.

## Direct executable path

An already-built xUnit v3 test application can run without another SDK build:

```powershell
& '<actual-built-test-executable>' --filter-method "*Feature*" --no-progress
```

Build the intended project/configuration/TFM first if its source changed. Locate its actual `TargetPath`/apphost instead of guessing a fixed `artifacts\bin` path; architecture and TFM can change the output directory.

Direct invocation bypasses MSBuild's test-launch arguments. Preserve required trait exclusions from [src/Directory.Build.targets](../../../../src/Directory.Build.targets); otherwise a "fast" invocation can run quarantined or platform-inapplicable tests.

`CreateBootstrap=false` skips the [bootstrap target](../../../../eng/BootStrapMsBuild.targets), but does not make bootstrap-dependent tests independent of its output. Use it only for a deliberately scoped build whose tests do not need bootstrap, never as a generic missing-SDK workaround.

## Isolation, traits, and coverage

[xunit.runner.json](../../../../src/Shared/UnitTests/xunit.runner.json) disables collection parallelism and limits test threads because many tests mutate process-global state. Do not rewrite this configuration as an optimization.

[src/Directory.Build.targets](../../../../src/Directory.Build.targets) supplies platform/TFM exclusions and the `Category=failing` quarantine filter through `XunitOptions` and `TestRunnerAdditionalArguments`. This is harness wiring: verify which arguments the selected native CLI/direct launch path actually forwards, and preserve required exclusions explicitly if it bypasses that wiring.

Normal runs exclude quarantined tests. `RunQuarantinedTests=true` intentionally selects only those tests and ignores certain exit codes; it is not an ordinary validation mode. Use per-test results, not a successful quarantine process exit, to assess failures.

Non-Windows normal runs add coverage; quarantined runs are excluded from that condition. Direct execution can omit coverage, but it also omits the injected trait arguments. Never describe coverage as unconditional or silently change filters along with it.

[RunnerUtilities](../../../../src/UnitTests.Shared/RunnerUtilities.cs) configures child MSBuild environments, including the bootstrap host where appropriate. Do not override `DOTNET_HOST_PATH` inside a test to compensate for using the wrong parent SDK or stale bootstrap.

## Results and escalation

TRX support requires the corresponding registered extension. If supported by the selected test app:

```powershell
dotnet test --project <project> --results-directory artifacts\TestResults `
  -- --report-trx --report-trx-filename validation.trx
```

Check passed, failed, skipped, and executed counts. Expected platform skips differ from a new skip that removes the regression scenario.

For a justified full repository run, use the existing harness:

```powershell
.\build.cmd -test
```

On Unix use `./build.sh --test`. Allow long-running validation to finish; do not repeatedly restart a build whose original process is still running.

## Source-to-test map

| Changed source | Existing project |
|---|---|
| Engine/evaluation/backend | [Microsoft.Build.Engine.UnitTests](../../../../src/Build.UnitTests/Microsoft.Build.Engine.UnitTests.csproj) |
| Engine object model | [Microsoft.Build.Engine.OM.UnitTests](../../../../src/Build.OM.UnitTests/Microsoft.Build.Engine.OM.UnitTests.csproj) |
| Framework and its utilities | [Microsoft.Build.Framework.UnitTests](../../../../src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj) |
| Tasks | [Microsoft.Build.Tasks.UnitTests](../../../../src/Tasks.UnitTests/Microsoft.Build.Tasks.UnitTests.csproj) |
| Utilities | [Microsoft.Build.Utilities.UnitTests](../../../../src/Utilities.UnitTests/Microsoft.Build.Utilities.UnitTests.csproj) |
| CLI | [Microsoft.Build.CommandLine.UnitTests](../../../../src/MSBuild.UnitTests/Microsoft.Build.CommandLine.UnitTests.csproj) |
| BuildCheck | [Microsoft.Build.BuildCheck.UnitTests](../../../../src/BuildCheck.UnitTests/Microsoft.Build.BuildCheck.UnitTests.csproj) |
| StringTools | [StringTools.UnitTests](../../../../src/StringTools.UnitTests/StringTools.UnitTests.csproj); inspect its separate runtime/library variants |
| Linked shared code | Find actual consuming projects and select their affected tests |

This map is a starting point, not a requirement to execute every project. Some integration tests require a current bootstrap; use the [bootstrap skill](../../use-bootstrap-msbuild/SKILL.md) for that boundary.
