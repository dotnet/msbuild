---
applyTo: "src/*.UnitTests/**/*.cs"
---

# Testing Instructions

Use xUnit with Shouldly assertions. Use Shouldly assertions for all assertions in modified code, even if the file is predominantly using xUnit assertions.

## Capturing Output

Inject xUnit's `ITestOutputHelper` via the test class constructor and store it as `_output`. Pass it to `TestEnvironment.Create(_output)` so that test infrastructure output is captured. Use `_output.WriteLine(...)` instead of `Console.WriteLine` for diagnostic output in tests.

## Use `TestEnvironment` for Test Setup and Cleanup

Use `TestEnvironment` (`Microsoft.Build.UnitTests.TestEnvironment`) to manage test state. Create with `using TestEnvironment env = TestEnvironment.Create(output);` — prefer a `using` declaration (no braces/indentation). `TestEnvironment` automatically reverts all registered state on dispose. Don't write manual `try`/`finally` blocks to restore state.

`TestEnvironment` can manage environment variables, temporary files and folders, the working directory, the system temp path, `ProjectCollection` lifetimes, test project scaffolding (`TransientTestProjectWithFiles`), process lifetimes, and test invariants.

## Writing Project XML in Tests

Prefer multiline raw string literals (`"""`) with natural indentation for inline MSBuild XML. Use the `.Cleanup()` extension method (from `Microsoft.Build.UnitTests.ObjectModelHelpers`) to replace `msbuildnamespace` and `msbuilddefaulttoolsversion` placeholders with real values. In raw strings, use normal double-quotes for XML attributes. Fall back to `@""` with backtick-to-quote replacement via `.Cleanup()` only when raw strings are not workable.
```csharp
string project = """
    <Project>
        <Target Name="Build"><Message Text="Hello" /></Target>
    </Project>
    """.Cleanup();
```

Do not specify `xmlns` in test XML snippets unless it is important to the functionality of the test.

## Build and Task Testing Helpers

Use `MockLogger` (`Microsoft.Build.UnitTests.MockLogger`) to capture and assert build output. It provides `AssertLogContains`, `AssertLogDoesntContain`, `AssertNoErrors`, `AssertNoWarnings`, and typed event collections (`Errors`, `Warnings`, `TargetStartedEvents`, etc.). Do not assume the test locale will be English--assert on invariant substrings or explicit localized resources, not full English strings. Tests that invoke builds via `ProjectCollection` or `BuildManager` must always attach a `MockLogger(_output)` so that build errors and warnings appear in the .trx test output for CI diagnostics.

Use `MockEngine` (`Microsoft.Build.UnitTests.MockEngine`) as an `IBuildEngine` implementation for testing individual tasks in isolation without a full build.

Use `ObjectModelHelpers.BuildProjectExpectSuccess`/`BuildProjectExpectFailure` (`Microsoft.Build.UnitTests.ObjectModelHelpers`) for quick in-memory builds that return a `MockLogger`. Use `ProjectFromString` (`Microsoft.Build.UnitTests.ProjectFromString`) — disposable, use with a `using` declaration — to create `Project` instances from XML for object model inspection.

## Platform-Conditional Tests

Use existing custom attributes for platform-specific tests instead of runtime `if` checks that silently skip assertions. Available attributes include `WindowsOnlyFact`, `WindowsFullFrameworkOnlyFact`, `UnixOnlyFact`, `RequiresSymbolicLinksFactAttribute`, `LongPathSupportDisabledFactAttribute`, and `SkipOnPlatform`. Use `ConditionalFact(nameof(ConditionMethod))` for custom conditions.

## Assembly Fixtures and Collections

Test assemblies use assembly fixtures (for example, `Microsoft.Build.UnitTests.MSBuildTestAssemblyFixture` and `Microsoft.Build.UnitTests.MSBuildTestEnvironmentFixture`). Avoid duplicating global setup in individual tests.

## Data-Driven and Async Tests

Use `[Theory]` with `[InlineData]` for simple inputs. Use `[MemberData]` or `TheoryData<T>` for complex objects (returning `IEnumerable<object[]>`). If the data type is custom, implement `IXunitSerializable`.

```csharp
[Theory]
[InlineData("input1", "expected1")]
public void TestLogic(string input, string expected) { ... }
```

For async tests, always return `async Task` (never `async void`) and `await` asynchronous operations.
```csharp
[Fact]
public async Task AsyncOperation_Works() {
    await service.DoWorkAsync();
}
```

## Test Resources

Resolve test data files via `AppContext.BaseDirectory` (for example, `Path.Combine(AppContext.BaseDirectory, "TestResources", "file.bin")`) so paths work across runners.

## Assertion Helpers

Use `Shouldly` extensions for `BuildResult`: `result.ShouldHaveSucceeded()` or `result.ShouldHaveFailed()`.
Use `ObjectModelHelpers.AssertItemsMatch` (`Microsoft.Build.UnitTests.ObjectModelHelpers`) to validate items and metadata using a compact string format:
```csharp
ObjectModelHelpers.AssertItemsMatch(
    "Item1 : Meta=Val; Item2", 
    project.GetItems("MyItem"));
```

Use `ObjectModelHelpers.AssertSingleItem`, `AssertItems`, and `AssertItemHasMetadata` for item/property assertions, and `NormalizeSlashes` for cross-platform path comparisons.
