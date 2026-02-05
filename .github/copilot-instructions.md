# MSBuild - Microsoft Build Engine

This repo contains the code for the MSBuild build engine, including its public C# API, its internal implementation of the MSBuild programming language, and core targets and tasks used for builds for .NET and Visual Studio.

Performance is very important--minimize allocations, avoid LINQ, and use the most efficient algorithms possible. The code should be easy to read and understand, but performance is the top priority.

The code is written in C# and should follow the .NET coding conventions. Use the latest C# features where appropriate, including C# 13 features and especially collection expressions--prefer `[]` to `new Type[]`.

You should generally match the style of surrounding code when making edits, but if making a substantial change, you can modernize more aggressively.
New files should use nullable types but don't refactor aggressively existing code.

Generate tests for new codepaths, and add tests for any bugs you fix. Use the existing test framework, which is xUnit with Shouldly assertions. Use Shouldly assertions for all assertions in modified code, even if the file is predominantly using xUnit assertions.

When making changes, check if related documentation exists in the `documentation/` folder (including `documentation/specs/`) and update it to reflect your changes. Keep documentation in sync with code changes, especially for telemetry, APIs, and architectural decisions.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Performance Best Practices

MSBuild is performance-critical infrastructure. Follow these patterns:

### Switch Expressions for Dispatch Logic
Use tuple switch expressions for multi-condition dispatch instead of if-else chains:
```csharp
// GOOD: Clean, O(1) dispatch
return (c0, c1) switch
{
    ('C', 'S') => Category.CSharp,
    ('F', 'S') => Category.FSharp,
    ('V', 'B') when value.Length >= 3 && value[2] == 'C' => Category.VB,
    _ => Category.Other
};

// AVOID: Verbose if-else chains
if (c0 == 'C' && c1 == 'S') return Category.CSharp;
else if (c0 == 'F' && c1 == 'S') return Category.FSharp;
// ...
```

### Range Pattern Matching
Use range patterns for numeric categorization:
```csharp
// GOOD: Clear and efficient
return errorNumber switch
{
    >= 3001 and <= 3999 => Category.Tasks,
    >= 4001 and <= 4099 => Category.General,
    >= 4100 and <= 4199 => Category.Evaluation,
    _ => Category.Other
};
```

### String Comparisons
- Use `StringComparer.OrdinalIgnoreCase` for case-insensitive HashSets/Dictionaries when the source data may vary in casing
- Use `char.ToUpperInvariant()` for single-character comparisons
- Use `ReadOnlySpan<char>` and `Slice()` to avoid string allocations when parsing substrings
- Use `int.TryParse(span, out var result)` on .NET Core+ for allocation-free parsing

### Inlining
Mark small, hot-path methods with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`:
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static bool IsCompilerPrefix(string value) => ...
```

### Conditional Compilation for Framework Differences
Use `#if NET` for APIs that differ between .NET Framework and .NET Core:
```csharp
#if NET
    return int.TryParse(span, out errorNumber);
#else
    return int.TryParse(span.ToString(), out errorNumber);
#endif
```

### Immutable Collections
Choose the right immutable collection type based on usage pattern:

**Build once, read many times** (most common in MSBuild):
- Use `ImmutableArray<T>` instead of `ImmutableList<T>` - significantly faster for read access
- Use `FrozenDictionary<TKey, TValue>` instead of `ImmutableDictionary<TKey, TValue>` - optimized for read-heavy scenarios

**Build incrementally over time** (adding items one by one):
- Use `ImmutableList<T>` and `ImmutableDictionary<TKey, TValue>` - designed for efficient `Add` operations returning new collections

```csharp
// GOOD: Build once from LINQ, then read many times
ImmutableArray<string> items = source.Select(x => x.Name).ToImmutableArray();
FrozenDictionary<string, int> lookup = pairs.ToFrozenDictionary(x => x.Key, x => x.Value);

// AVOID for read-heavy scenarios:
ImmutableList<string> items = source.Select(x => x.Name).ToImmutableList();
ImmutableDictionary<string, int> lookup = pairs.ToImmutableDictionary(x => x.Key, x => x.Value);
```

Note: `ImmutableArray<T>` is a value type. Use `IsDefault` property to check for uninitialized arrays, or use nullable `ImmutableArray<T>?` with `.Value` to unwrap.

## Working Effectively

#### Bootstrap and Build the Repository
NEVER build the repository with just `dotnet build MSBuild.slnx` or `dotnet build src/.../Project.csproj`.
Run these commands in sequence to set up a complete development environment:

**Windows:**
```cmd
# Full build with restore - NEVER CANCEL: Takes ~2-3 minutes. Set timeout to 300+ seconds.
.\build.cmd -v quiet

# Set up bootstrap environment for using built MSBuild
artifacts\msbuild-build-env.bat
```

**macOS/Linux:**
```bash
# Full build with restore - NEVER CANCEL: Takes ~2-3 minutes. Set timeout to 300+ seconds.
./build.sh -v quiet

# Set up bootstrap environment for using built MSBuild
source artifacts/sdk-build-env.sh
```

### Test the Repository
**Windows:**
```cmd
# Run all tests - NEVER CANCEL: Takes ~9 minutes but some tests may fail (this is expected). Set timeout to 900+ seconds.
.\build.cmd -test

# Run individual test project (recommended for validation):
artifacts\msbuild-build-env.bat
dotnet test src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj
```

**macOS/Linux:**
```bash
# Run all tests - NEVER CANCEL: Takes ~9 minutes but some tests may fail (this is expected). Set timeout to 900+ seconds.
./build.sh --test

# Run individual test project (recommended for validation):
source artifacts/sdk-build-env.sh
dotnet test src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj
```

**CRITICAL**: Some unit tests fail in the full test suite due to environment/CI dependencies. This is EXPECTED and normal in development environments. Individual test projects typically work correctly.

### Using the Built MSBuild

After building, use the bootstrap environment to work with the locally built MSBuild:

**Windows:**
```cmd
# Set up environment (run after every new shell session)
artifacts\msbuild-build-env.bat

# Verify environment is working
dotnet --version
# Should show something like: 10.0.100-preview.7.25372.107

# Build a project using the built MSBuild
dotnet build src/Samples/Dependency/Dependency.csproj

# Run MSBuild directly
dotnet artifacts/bin/bootstrap/core/MSBuild.dll --help
```

**macOS/Linux:**
```bash
# Set up environment (run after every new shell session)
source artifacts/sdk-build-env.sh

# Verify environment is working
dotnet --version
# Should show something like: 10.0.100-preview.7.25372.107

# Build a project using the built MSBuild
dotnet build src/Samples/Dependency/Dependency.csproj

# Run MSBuild directly
dotnet artifacts/bin/bootstrap/core/MSBuild.dll --help
```

## Breaking Changes

Because MSBuild is a critical part of the build process for a huge number of customers, we avoid breaking changes. Adding new errors or warnings, even when well-intentioned and pointing out things that are very likely to be wrong, is an unacceptable breaking change. Adding warnings is a breaking change because many production builds use `/WarnAsError`.

The exception to this policy is in new, opt-in behavior. In new, opt-in functionality, liberally emit warnings and errors--they can always be removed later.

When reviewing PRs, always consider whether the behavior change could be experienced as a break in existing builds and flag any new warnings or errors.

## Validation

### Always Test These Scenarios After Making Changes:
1. **Full build validation**: 
   - Windows: `.\build.cmd` must complete successfully
   - macOS/Linux: `./build.sh` must complete successfully
2. **Bootstrap environment**: 
   - Windows: `artifacts\msbuild-build-env.bat && dotnet --version` shows correct preview version
   - macOS/Linux: `source artifacts/sdk-build-env.sh && dotnet --version` shows correct preview version
3. **Sample build**: `dotnet build src/Samples/Dependency/Dependency.csproj` succeeds
4. **Individual tests**: Choose a relevant test project and run `dotnet test [project.csproj]`
5. **MSBuild help**: `dotnet artifacts/bin/bootstrap/core/MSBuild.dll --help` shows usage

### Manual Testing Requirements:
- ALWAYS test the full build after code changes
- Verify the bootstrap environment works correctly with `dotnet --version`
- Test MSBuild executable can display help and basic functionality
- Build at least one sample project to verify core functionality

## Common Tasks

### Build Commands and Timing
**Windows:**
- **`.\build.cmd`** - Full build: ~2-3 minutes. NEVER CANCEL. Use 300+ second timeout.
- **`.\build.cmd -test`** - Run all tests: ~9 minutes with some failures expected. NEVER CANCEL. Use 900+ second timeout.
- **`.\build.cmd -clean`** - Clean build artifacts: ~30 seconds.

**macOS/Linux:**
- **`./build.sh`** - Full build: ~2-3 minutes. NEVER CANCEL. Use 300+ second timeout.
- **`./build.sh -test`** - Run all tests: ~9 minutes with some failures expected. NEVER CANCEL. Use 900+ second timeout.
- **`./build.sh -clean`** - Clean build artifacts: ~30 seconds.

### Development Workflow
1. Make your changes to source code
2. Run the full build to compile (WAIT for completion - takes 2-3 minutes):
   - Windows: `.\build.cmd -v quiet`
   - macOS/Linux: `./build.sh -v quiet`
3. Set up environment:
   - Windows: `artifacts\msbuild-build-env.bat`
   - macOS/Linux: `source artifacts/sdk-build-env.sh`
4. Test your changes: `dotnet build src/Samples/Dependency/Dependency.csproj`
5. Run relevant individual tests, not the full test suite
6. Commit your changes

### Key Project Structure
```
src/
├── Build/                    # Core MSBuild engine (Microsoft.Build)
├── MSBuild/                  # MSBuild command-line tool
├── Framework/                # MSBuild Framework (Microsoft.Build.Framework)
├── Tasks/                    # Built-in MSBuild tasks (Microsoft.Build.Tasks)
├── Utilities/                # MSBuild utilities (Microsoft.Build.Utilities)
├── Samples/                  # Sample projects and extensions
└── [Component].UnitTests/    # Unit tests for each component

artifacts/
├── bin/                      # Built binaries and tools
├── sdk-build-env.sh         # Bootstrap environment script (Linux/macOS)
├── msbuild-build-env.bat    # Bootstrap environment script (Windows)
└── packages/                # Built NuGet packages

documentation/
├── wiki/                     # Developer documentation
├── specs/                    # Technical specifications
└── *.md                     # Various documentation files
```

## Troubleshooting

### Common Issues and Solutions

**Build fails with "Could not resolve SDK":**
- Ensure you run `source artifacts/sdk-build-env.sh` after building
- Verify `dotnet --version` shows the preview/RC/internal version (e.g. 10.0.100-preview.7.25372.107)

**Tests fail:**
- Run individual test projects instead: `dotnet test src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj`

**Certificate errors with external projects:**
- Use the repository's sample projects for testing instead of creating new external projects
- The bootstrap environment is designed for building the MSBuild repository itself

### Files You Should NOT Modify
- `global.json` - Controls .NET SDK version
- `NuGet.config` - Package source configuration
- `artifacts/` directory contents - Generated during build
- `.dotnet/` directory contents - Local SDK location used to build
