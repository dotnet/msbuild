# Fast Builds for MSBuild Development

This document describes optimized build workflows for faster development iteration.

## Quick Start

For the fastest possible development workflow:

```bash
# First time: Minimal build with bootstrap (creates usable MSBuild)
.\build-minimal.cmd

# After that: Fast incremental rebuilds (no bootstrap, ~10 seconds)
.\build-minimal.cmd -nobootstrap
```

## Build Scripts

### `build-minimal.cmd` / `build-minimal.sh`

A specialized script that builds only the minimal MSBuild assemblies without tests, samples, or package projects. This is significantly faster than a full build.

| Scenario | Approximate Time |
|----------|------------------|
| Full build (`build.cmd`) | 2-3 minutes |
| Minimal build with bootstrap (cold) | ~1 minute |
| Minimal build with bootstrap (incremental) | ~15 seconds |
| Minimal build without bootstrap (incremental) | ~10 seconds |

#### Options

```
.\build-minimal.cmd [options]

Options:
  -nobootstrap      Skip creating the bootstrap folder (fastest builds)
  -release          Build in Release configuration (default: Debug)
  -debug            Build in Debug configuration
  -rebuild          Force a rebuild (clean + build)
  -v <level>        Verbosity: q[uiet], m[inimal], n[ormal], d[etailed]
```

#### Examples

```bash
# Standard minimal build with bootstrap
.\build-minimal.cmd

# Fast incremental build (when bootstrap already exists)
.\build-minimal.cmd -nobootstrap

# Release build
.\build-minimal.cmd -release

# Force clean rebuild
.\build-minimal.cmd -rebuild
```

### Unix/macOS

```bash
./build-minimal.sh --nobootstrap
./build-minimal.sh --release
```

## Solution Filters

The repository includes solution filters for different development scenarios:

| Filter | Description |
|--------|-------------|
| `MSBuild.Minimal.slnf` | Minimal runtime projects only (no tests, no samples) |
| `MSBuild.Dev.slnf` | Core + test projects |
| `MSBuild.sln` | Full solution |

For day-to-day development, open `MSBuild.Minimal.slnf` or `MSBuild.Dev.slnf` in Visual Studio for faster IDE operations.

## Benchmarking

Use `benchmark-build.cmd` to objectively measure build times on your machine:

```bash
.\benchmark-build.cmd
```

This runs multiple build scenarios and reports timing for each, saving results to `build-benchmark-results.txt`.

## Optimizing Bootstrap

The bootstrap process creates a usable copy of MSBuild from your local build. Tips:

1. **Skip when not needed**: Use `-nobootstrap` when you're just iterating on code and don't need to run the built MSBuild.

2. **Bootstrap is incremental**: The bootstrap now uses `SkipUnchangedFiles="true"`, so subsequent builds with bootstrap are much faster.

3. **The SDK is cached**: The .NET SDK for bootstrap is downloaded once and reused. You won't re-download it on every build.

## Best Practices for Fast Iteration

1. **Initial Setup**: Run `.\build-minimal.cmd` once to create the bootstrap environment.

2. **Iterating on Code**: Use `.\build-minimal.cmd -nobootstrap` for fast rebuilds.

3. **Need to Test Changes**: Run `.\build-minimal.cmd` (with bootstrap) to update your local MSBuild.

4. **Running Tests**: After building minimal, you can run individual test projects:
   ```bash
   .\artifacts\msbuild-build-env.bat
   dotnet test src\Build.UnitTests\Microsoft.Build.Engine.UnitTests.csproj --filter "FullyQualifiedName~YourTest"
   ```

5. **Visual Studio**: Use `MSBuild.Minimal.slnf` for faster solution load and build times within VS.

## What's in the Minimal Build?

The `MSBuild.Minimal.slnf` includes only the essential runtime assemblies:

- `Microsoft.Build.Framework` - Core interfaces and types
- `Microsoft.Build` - Main build engine
- `Microsoft.Build.Tasks` - Built-in tasks
- `Microsoft.Build.Utilities` - Utilities for task authors
- `StringTools` - String handling utilities
- `MSBuild.exe` - Command-line entry point
- `MSBuild.Bootstrap` - Bootstrap environment setup

## Comparison with Full Build

| What's Built | `build.cmd` | `build-minimal.cmd` |
|--------------|-------------|---------------------|
| Core assemblies | ✓ | ✓ |
| Test projects | ✓ | ✗ |
| Sample projects | ✓ | ✗ |
| Package projects | ✓ | ✗ |
| Bootstrap | ✓ | ✓ (optional) |

Use `build-minimal.cmd` during development, and `build.cmd -test` before submitting PRs.
