---
name: running-unit-tests
description: "Run or filter existing MSBuild unit tests with the checkout's SDK and runner. Use for local test execution and scoped validation, not CI triage or generating a new test suite."
---

# Run existing MSBuild tests

## Before running

1. Read [global.json](../../../global.json) for SDK and test-runner selection, [Directory.Build.props](../../../Directory.Build.props) for runner version floors, and [src/Directory.Build.props](../../../src/Directory.Build.props) for applicable TFMs.
2. Identify the affected test project and a filter that exercises the behavior. Use one invocation for related selectors supported by the same runner.
3. Check that the selected SDK exists. Do not substitute an older installed SDK, alter `global.json`, or change feeds to get past a setup error.

## Current MTP command shape

With `test.runner` set to `Microsoft.Testing.Platform`, use an explicit project selector. Replace `<TFM>` and the filter below with values for the current checkout:

```powershell
dotnet test --project src\Framework.UnitTests\Microsoft.Build.Framework.UnitTests.csproj `
  --framework <TFM> -- --filter-method "*FeatureUnderTest*"
```

Use the appropriate shell's path separators and continuation syntax. Run through the selected repo SDK; when using a repo-local dotnet host, keep `DOTNET_ROOT` and `PATH` consistent in the same command environment.

Runner-specific flags come from the test application's registered extensions. Inspect `dotnet test --help` for the selected SDK/project or the built test executable's help if an option is uncertain. Do not mix VSTest's `--filter`, `--logger`, or `--collect` with MTP examples.

## Isolation and stopping criteria

- Preserve the single-threaded settings in [xunit.runner.json](../../../src/Shared/UnitTests/xunit.runner.json). Do not temporarily enable parallel collections, remove trait filters, or use quarantine mode as a speed switch.
- Confirm that the filter selected the intended tests; zero executed tests is not success. Check failures and unexpected skips, not only process exit status.
- Start with the affected project/TFM. Add other supported TFMs, consumers, or full-suite validation only when the changed boundary or failures justify it.
- Documentation-only edits do not require unit tests or a product rebuild.
- A regression proof additionally needs evidence that the assertion fails on the broken baseline and exercises the fixed implementation.

## Load only the needed reference

- [Commands, direct executables, traits, coverage, and project map](references/runner-details.md).
- [Test authoring conventions](../../instructions/tests.instructions.md).
- [Bootstrap reproduction](../use-bootstrap-msbuild/SKILL.md) for tests of product executables or SDK integration.
