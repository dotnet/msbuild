# Agent Instructions

Instructions for GitHub Copilot and other AI coding agents working with the MSBuild repository.

## Repository Overview

**MSBuild** is the Microsoft Build Engine - performance-critical infrastructure for .NET and Visual Studio builds. This repository contains the code for the MSBuild build engine, including its public C# API, its internal implementation of the MSBuild programming language, and core targets and tasks used for builds.

### Key Components
- **Microsoft.Build**: Core MSBuild engine and public API
- **Microsoft.Build.Framework**: Framework interfaces and base types
- **Microsoft.Build.Tasks**: Built-in MSBuild tasks
- **Microsoft.Build.Utilities**: Utility classes for task authors
- **MSBuild CLI**: Command-line tool for invoking builds

### Technology Stack
- .NET 10.0 and .NET Framework 4.7.2
- C# 13 features (especially collection expressions)
- xUnit with Shouldly for testing
- Multi-platform support (Windows, Linux, macOS)

## General

* Performance is the top priority - minimize allocations, avoid LINQ in hot paths, use efficient algorithms.
* Always use the latest C# features, currently C# 13, especially collection expressions (`[]` over `new Type[]`).
* Match the style of surrounding code when making edits, but modernize aggressively for substantial changes.

## Code Review Instructions

### Performance Considerations

When reviewing pull requests:

* **Flag any unnecessary allocations** in hot paths
* **Flag LINQ usage** in performance-critical code paths
* **Check for proper use of `Span<T>`** and `ReadOnlySpan<T>` for string parsing
* **Ensure immutable collections** use the correct type (`ImmutableArray<T>` and `FrozenDictionary<TKey, TValue>` for read-heavy, `ImmutableList<T>` for incremental building)

### NuGet Feed Configuration

When reviewing pull requests:

* **Flag any changes to NuGet.config** that add external package sources without justification
* Package sources should use approved internal feeds when possible

## Formatting

* Apply code-formatting style defined in `.editorconfig`.
* Prefer file-scoped namespace declarations and single-line using directives.
* Insert a newline before the opening curly brace of any code block.
* Use pattern matching and switch expressions wherever possible.
* Use `nameof` instead of string literals when referring to member names.

### Nullable Reference Types

* **New files**: Always use nullable reference types (do NOT add `#nullable disable`)
* **Existing files with `#nullable disable`**: Match the existing style; don't add nullable annotations (`?`) to types
* **Existing files with nullable enabled**: Use proper nullable annotations
* Always use `is null` or `is not null` instead of `== null` or `!= null`

## Performance Best Practices

### Range Pattern Matching

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

### String Handling

* Use `MSBuildNameIgnoreCaseComparer` for case-insensitive comparisons of MSBuild names; use `StringComparer.OrdinalIgnoreCase` only for non-MSBuild string comparisons
* Use `char.ToUpperInvariant()` for single-character comparisons
* Use `ReadOnlySpan<char>` and `Slice()` to avoid string allocations
* Use `int.TryParse(span, out var result)` on .NET Core+ for allocation-free parsing

### Inlining Hot Paths

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static bool IsCompilerPrefix(string value) => ...
```

### Immutable Collections

**Build once, read many times** (most common in MSBuild):
```csharp
ImmutableArray<string> items = source.Select(x => x.Name).ToImmutableArray();
FrozenDictionary<string, int> lookup = pairs.ToFrozenDictionary(x => x.Key, x => x.Value);
```

**Build incrementally over time**:
```csharp
ImmutableList<string> items = ...;  // Use when adding items one by one
ImmutableDictionary<string, int> lookup = ...;
```

## Building

**CRITICAL**: Never build with just `dotnet build MSBuild.slnx` or `dotnet build src/.../Project.csproj`. Always use the build scripts.

### Build Commands - NEVER CANCEL

| Platform | Command | Timeout |
|----------|---------|---------|
| Windows | `.\build.cmd -v quiet` | 300+ seconds (~2-3 minutes) |
| macOS/Linux | `./build.sh -v quiet` | 300+ seconds (~2-3 minutes) |

### Bootstrap Environment Setup

After building, activate the bootstrap environment before any `dotnet` commands:

**Windows:**
```cmd
artifacts\msbuild-build-env.bat
```

**macOS/Linux:**
```bash
source artifacts/sdk-build-env.sh
```

### Verify Environment

```bash
dotnet --version
# Should show something like: 10.0.100-preview.7.25372.107
```

### Build Troubleshooting

* If build fails with "Could not resolve SDK", run the bootstrap environment script
* Verify `dotnet --version` shows the preview/internal version
* Use repository sample projects for testing, not external projects
* Build artifacts go to `./artifacts/` directory

## Testing

* We use xUnit with Shouldly assertions
* Use Shouldly assertions for all assertions in modified code
* Do not emit "Act", "Arrange" or "Assert" comments
* Copy existing style in nearby files for test method names

### Running Tests

**Windows:**
```cmd
# Full test suite (~9 minutes) - NEVER CANCEL
.\build.cmd -test

# Individual test project (recommended):
artifacts\msbuild-build-env.bat
dotnet test src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj
```

**macOS/Linux:**
```bash
# Full test suite (~9 minutes) - NEVER CANCEL
./build.sh --test

# Individual test project (recommended):
source artifacts/sdk-build-env.sh
dotnet test src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj
```

### Test Verification

* **Individual Test Project**: ~10-60 seconds per project
* **Full Test Suite**: ~9 minutes

## Project Layout and Architecture

### Directory Structure

```
src/
├── Build/                    # Core MSBuild engine (Microsoft.Build)
├── Build.UnitTests/          # Unit tests for core engine
├── MSBuild/                  # MSBuild command-line tool
├── Framework/                # MSBuild Framework (Microsoft.Build.Framework)
├── Framework.UnitTests/      # Unit tests for framework
├── Tasks/                    # Built-in MSBuild tasks (Microsoft.Build.Tasks)
├── Tasks.UnitTests/          # Unit tests for tasks
├── Utilities/                # MSBuild utilities (Microsoft.Build.Utilities)
├── Utilities.UnitTests/      # Unit tests for utilities
├── Shared/                   # Shared code across assemblies
└── Samples/                  # Sample projects and extensions

artifacts/
├── bin/                      # Built binaries
│   └── bootstrap/
│       └── core/
│           └── MSBuild.dll   # Built MSBuild executable
├── sdk-build-env.sh          # Bootstrap script (Linux/macOS)
├── msbuild-build-env.bat     # Bootstrap script (Windows)
└── packages/                 # Built NuGet packages

documentation/
├── wiki/                     # Developer documentation
├── specs/                    # Technical specifications
└── *.md                      # Various documentation files
```

### Key Configuration Files

* **`global.json`**: Pins .NET SDK version - never modify without explicit request
* **`NuGet.config`**: Package source configuration - never modify without explicit request
* **`.editorconfig`**: Code formatting rules
* **`Directory.Build.props`**: Shared MSBuild properties across all projects
* **`Directory.Packages.props`**: Centralized package version management
* **`MSBuild.slnx`**: Main solution file

## Validation Checklist

Before completing any change:

1. ✅ Full build completes successfully (`.\build.cmd` or `./build.sh`)
2. ✅ Bootstrap environment activates correctly (`dotnet --version` shows preview)
3. ✅ Sample project builds: `dotnet build src/Samples/Dependency/Dependency.csproj`
4. ✅ Relevant unit tests pass
5. ✅ `dotnet artifacts/bin/bootstrap/core/MSBuild.dll --help` works

## Do NOT Modify

* `artifacts/` directory contents - Generated during build
* `.dotnet/` directory contents - Local SDK location

See **Key Configuration Files** section for files that should not be modified without explicit request.

## Documentation

When making changes, check if related documentation exists in the `documentation/` folder (including `documentation/specs/`) and update it to reflect your changes. Keep documentation in sync with code changes.

## Breaking Changes

Because MSBuild is a critical part of the build process for a huge number of customers, we avoid breaking changes. Adding new errors or warnings, even when well-intentioned and pointing out things that are very likely to be wrong, is an unacceptable breaking change. Adding warnings is a breaking change because many production builds use `/WarnAsError`.

The exception to this policy is in new, opt-in behavior. In new, opt-in functionality, liberally emit warnings and errors--they can always be removed later.

When reviewing PRs, always consider whether the behavior change could be experienced as a break in existing builds and flag any new warnings or errors.

## Development Workflow

1. Make your changes to source code
2. Run the full build (WAIT for completion - takes 2-3 minutes):
   - Windows: `.\build.cmd -v quiet`
   - macOS/Linux: `./build.sh -v quiet`
3. Set up environment:
   - Windows: `artifacts\msbuild-build-env.bat`
   - macOS/Linux: `source artifacts/sdk-build-env.sh`
4. Test your changes: `dotnet build src/Samples/Dependency/Dependency.csproj`
5. Run relevant individual tests, not the full test suite
6. Commit your changes

## Trust These Instructions

These instructions are comprehensive and tested. Only search for additional information if:
1. The instructions appear outdated or incorrect
2. You encounter specific errors not covered here
3. You need details about new features not yet documented

For most development tasks, following these instructions should be sufficient to build, test, and validate changes successfully.

## Updating these instructions

When working on a task, if user input is required to complete the task or feedback is provided for guidance around a specific area of the code, evaluate that feedback/guidance and update this document to incorporate that feedback if it's missing.  This document should be a live, evolving set of instructions.
