---
applyTo: "src/*.UnitTests/**/*.cs,src/UnitTests.Shared/**/*.cs,src/Shared/UnitTests/**/*.cs"
---

# Test authoring

## State, output, and assertions

- Use xUnit and Shouldly; use Shouldly for assertions in modified code.
- Capture diagnostics through `ITestOutputHelper`, usually `_output`, and pass it to `TestEnvironment.Create(_output)` and `MockLogger`. Avoid `Console.WriteLine` for test diagnostics.
- Use a `using` declaration with [TestEnvironment](../../src/UnitTests.Shared/TestEnvironment.cs) for supported temporary files, environment variables, current directory, and other registered state. It reverts registered state, not arbitrary side effects; use explicit cleanup for unsupported resources.
- Keep the repository's process-global-state isolation. Do not enable parallel collections or alter traits to speed up a test run.
- Use [MockLogger](../../src/UnitTests.Shared/MockLogger.cs) for builds and the existing `MockEngine` for isolated task tests. Attach a logger when invoking builds so failures are diagnosable.
- Assert relevant structured events or invariant/localized content, not an assumed English full message.

## Projects and fixtures

- Prefer raw string literals for project XML. Use `.Cleanup()` only when placeholders or the helper's normalization are needed; ordinary XML needs no namespace unless the scenario tests one.
- Reuse [ObjectModelHelpers](../../src/UnitTests.Shared/ObjectModelHelpers.cs), [ProjectFromString](../../src/UnitTests.Shared/ProjectFromString.cs), and the scaffolding in [EngineTestEnvironment](../../src/UnitTests.Shared/EngineTestEnvironment.cs). Read their actual signatures before copying examples.
- Use existing platform/conditional attributes rather than returning early and silently skipping assertions.
- Reuse assembly fixtures and existing collections instead of duplicating global setup.
- Use `[Theory]` with data appropriate to the runner. Follow established serialization patterns for custom theory data when serialization is required; not every custom value needs its own `IXunitSerializable` implementation.
- Async tests return `Task` and await work. Do not use `async void`.
- Locate copied test resources relative to the test output, commonly `AppContext.BaseDirectory`; do not depend on the shell's current directory.

## Evidence

A regression assertion must distinguish the broken behavior from the fix, not merely "no crash" or "nonempty." For transport, concurrency, and build-mode changes, prove the relevant path ran.

For runner commands, filtering, supported TFMs, and escalation, read [running-unit-tests](../skills/running-unit-tests/SKILL.md). Test authoring does not by itself require a test-generation plugin.
