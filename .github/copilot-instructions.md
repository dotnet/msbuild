# MSBuild - Microsoft Build Engine

This repo contains the code for the MSBuild build engine, including its public C# API, its internal implementation of the MSBuild programming language, and core targets and tasks used for builds for .NET and Visual Studio.

Performance is very important--minimize allocations, avoid LINQ, and use the most efficient algorithms possible. The code should be easy to read and understand, but performance is the top priority.

The code is written in C# and should follow the .NET coding conventions. Use the latest C# features where appropriate, including C# 13 features and especially collection expressions--prefer `[]` to `new Type[]`.

You should generally match the style of surrounding code when making edits, but if making a substantial change, you can modernize more aggressively.

Generate tests for new codepaths, and add tests for any bugs you fix. Use the existing test framework, which is xUnit with Shouldly assertions. Use Shouldly assertions for all assertions in modified code, even if the file is predominantly using xUnit assertions.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Bootstrap and Build the Repository
Run these commands in sequence to set up a complete development environment:

**Windows:**
```cmd
# Clone repository (if not already done)
git clone https://github.com/dotnet/msbuild
cd msbuild

# Full build with restore - NEVER CANCEL: Takes ~2-3 minutes. Set timeout to 300+ seconds.
build.cmd --restore --build

# Set up bootstrap environment for using built MSBuild
artifacts\msbuild-build-env.bat
```

**macOS/Linux:**
```bash
# Clone repository (if not already done)
git clone https://github.com/dotnet/msbuild
cd msbuild

# Full build with restore - NEVER CANCEL: Takes ~2-3 minutes. Set timeout to 300+ seconds.
./build.sh --restore --build

# Set up bootstrap environment for using built MSBuild
source artifacts/sdk-build-env.sh
```

**IMPORTANT**: If build fails with "Resource temporarily unavailable" errors for Azure DevOps package feeds, this indicates network connectivity issues to Microsoft's internal package sources. This is common in some development environments and is not a code issue. The build typically succeeds on retry or when run in environments with proper connectivity.

### Test the Repository
**Windows:**
```cmd
# Run all tests - NEVER CANCEL: Takes ~9 minutes but some tests may fail (this is expected). Set timeout to 900+ seconds.
build.cmd --test

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
# Should show: 10.0.100-preview.7.25372.107

# Build a project using the built MSBuild
dotnet build src/Samples/Dependency/Dependency.csproj

# Run MSBuild directly
dotnet artifacts/bin/MSBuild/Debug/net10.0/MSBuild.dll --help
```

**macOS/Linux:**
```bash
# Set up environment (run after every new shell session)
source artifacts/sdk-build-env.sh

# Verify environment is working
dotnet --version
# Should show: 10.0.100-preview.7.25372.107

# Build a project using the built MSBuild
dotnet build src/Samples/Dependency/Dependency.csproj

# Run MSBuild directly
dotnet artifacts/bin/MSBuild/Debug/net10.0/MSBuild.dll --help
```

## Validation

## Validation

### Always Test These Scenarios After Making Changes:
1. **Full build validation**: 
   - Windows: `build.cmd --restore --build` must complete successfully
   - macOS/Linux: `./build.sh --restore --build` must complete successfully  
   - (May fail due to network connectivity to Azure DevOps feeds)
2. **Bootstrap environment**: 
   - Windows: `artifacts\msbuild-build-env.bat && dotnet --version` shows correct preview version
   - macOS/Linux: `source artifacts/sdk-build-env.sh && dotnet --version` shows correct preview version
3. **Sample build**: `dotnet build src/Samples/Dependency/Dependency.csproj` succeeds
4. **Individual tests**: Choose a relevant test project and run `dotnet test [project.csproj]`
5. **MSBuild help**: `dotnet artifacts/bin/MSBuild/Debug/net10.0/MSBuild.dll --help` shows usage

### Manual Testing Requirements:
- ALWAYS test the full build after code changes (when network connectivity allows)
- Verify the bootstrap environment works correctly with `dotnet --version`
- Test MSBuild executable can display help and basic functionality
- Build at least one sample project to verify core functionality

## Common Tasks

### Build Commands and Timing
**Windows:**
- **`build.cmd --restore --build`** - Full build: ~2-3 minutes. NEVER CANCEL. Use 300+ second timeout.
- **`build.cmd --test`** - Run all tests: ~9 minutes with some failures expected. NEVER CANCEL. Use 900+ second timeout.
- **`build.cmd --clean`** - Clean build artifacts: ~30 seconds.

**macOS/Linux:**
- **`./build.sh --restore --build`** - Full build: ~2-3 minutes. NEVER CANCEL. Use 300+ second timeout.
- **`./build.sh --test`** - Run all tests: ~9 minutes with some failures expected. NEVER CANCEL. Use 900+ second timeout.
- **`./build.sh --clean`** - Clean build artifacts: ~30 seconds.

### Development Workflow
1. Make your changes to source code
2. Run the full build to compile (WAIT for completion - takes 2-3 minutes):
   - Windows: `build.cmd --restore --build`
   - macOS/Linux: `./build.sh --restore --build`
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

### Environment Variables for Development
Set these for debugging and development:
- `MSBUILDDEBUGONSTART=2` - Wait for debugger attach on MSBuild start
- `MSBUILDLOGVERBOSERARSEARCHRESULTS=1` - Verbose ResolveAssemblyReference logging
- `MSBUILDDISABLENODEREUSE=1` - Disable MSBuild process reuse

### Debugging Options
For debugging MSBuild:
- Set `MSBUILDDEBUGONSTART=2` environment variable, then attach a debugger after the process starts
- Use Visual Studio with "Use previews of the .NET SDK (requires restart)" enabled
- Build first, then run `artifacts\msbuild-build-env.bat` (Windows) or `source artifacts/sdk-build-env.sh` (Linux/macOS) before debugging

### Contributing Guidelines
Before submitting code:
- Discuss significant changes with the team first
- Follow [.NET Runtime Coding Guidelines](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)
- Use Shouldly assertion framework for new or modified test cases
- Ensure backwards compatibility
- Write tests for new code paths and bug fixes
- Performance is critical: minimize allocations, avoid LINQ, use efficient algorithms

## Troubleshooting

### Common Issues and Solutions

**Build fails with "Could not resolve SDK":**
- Ensure you run `source artifacts/sdk-build-env.sh` after building
- Verify `dotnet --version` shows the preview version (10.0.100-preview.7.25372.107)

**Tests fail:**
- This is EXPECTED for some tests in CI/development environments
- Run individual test projects instead: `dotnet test src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj`

**Certificate errors with external projects:**
- Use the repository's sample projects for testing instead of creating new external projects
- The bootstrap environment is designed for building the MSBuild repository itself

**Network connectivity issues during build:**
- "Resource temporarily unavailable" errors from dnceng.pkgs.visualstudio.com indicate Azure DevOps connectivity issues
- These are environment/network related, not code issues
- Retry the build or work in an environment with better connectivity to Microsoft's package feeds

**Long build times:**
- First build after clone takes longer due to package restoration
- Subsequent builds are faster (incremental)
- NEVER cancel long-running commands - they WILL complete

### Files You Should NOT Modify
- `global.json` - Controls .NET SDK version
- `NuGet.config` - Package source configuration
- `artifacts/` directory contents - Generated during build
- `.dotnet/` directory contents - Bootstrap SDK location

### Key Files to Understand
- `build.sh` / `build.cmd` - Main build entry points
- `eng/common/build.sh` - Actual build implementation  
- `MSBuild.sln` - Main solution file (large, contains all projects)
- `MSBuild.Dev.slnf` - Filtered solution for development
- `Directory.Build.props` - Common MSBuild properties
- `src/Directory.Build.targets` - Common build targets

### Important Documentation Links
- [Building, Testing and Debugging Guide](documentation/wiki/Building-Testing-and-Debugging-on-.Net-Core-MSBuild.md)
- [Contributing Code Guidelines](documentation/wiki/Contributing-Code.md)
- [MSBuild Tips & Tricks](documentation/wiki/MSBuild-Tips-&-Tricks.md)
- [Binary Logs for Investigation](documentation/wiki/Providing-Binary-Logs.md)
- [Something's wrong in my build troubleshooting](documentation/wiki/Something's-wrong-in-my-build.md)

## Quick Reference

### Essential Commands (Copy-Paste Ready)
**Windows:**
```cmd
# Fresh setup from clone
build.cmd --restore --build
artifacts\msbuild-build-env.bat

# Validate setup works  
dotnet --version
dotnet build src/Samples/Dependency/Dependency.csproj

# Test individual component
dotnet test src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj

# Run MSBuild with your changes
dotnet artifacts/bin/MSBuild/Debug/net10.0/MSBuild.dll [your-project.csproj]
```

**macOS/Linux:**
```bash
# Fresh setup from clone
./build.sh --restore --build
source artifacts/sdk-build-env.sh

# Validate setup works  
dotnet --version
dotnet build src/Samples/Dependency/Dependency.csproj

# Test individual component
dotnet test src/Framework.UnitTests/Microsoft.Build.Framework.UnitTests.csproj

# Run MSBuild with your changes
dotnet artifacts/bin/MSBuild/Debug/net10.0/MSBuild.dll [your-project.csproj]
```

### Repository Information
- **Main branch**: `main`
- **Language**: C# (.NET 10 preview)
- **Build system**: MSBuild (self-hosting)
- **Test framework**: xUnit
- **License**: MIT

### Package Outputs
After building, NuGet packages are created in `artifacts/packages/Debug/Shipping/`:
- Microsoft.Build.*.nupkg - Core MSBuild packages
- Microsoft.NET.StringTools.*.nupkg - String utilities
- Microsoft.Build.Framework.*.nupkg - MSBuild framework