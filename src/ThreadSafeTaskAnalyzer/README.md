# IMultiThreadableTask Analyzer

## Overview

Roslyn analyzer and code fixer for detecting unsafe API usage in MSBuild tasks implementing `IMultiThreadableTask`. Enforces threading safety requirements by identifying APIs that rely on or modify global process state.

## Purpose

Multithreaded MSBuild tasks must avoid APIs that depend on process-global state (working directory, environment variables, culture settings). This analyzer:

1. **Detects unsafe API usage** with MSB9999 diagnostics
2. **Provides automated fixes** via code actions for file system operations
3. **Guides developers** toward thread-safe alternatives using `TaskEnvironment`

## Components

- **IMultiThreadableTaskBannedAnalyzer.cs** - Abstract analyzer base class
- **CSharpIMultiThreadableTaskBannedAnalyzer.cs** - C# implementation
- **CSharpIMultiThreadableTaskCodeFixProvider.cs** - Automated code fixes
- **VisualStudioDemo/** - Interactive demonstration project

## Detection Modes

### Always-Banned APIs

APIs that should **never** be used in multithreaded tasks:

- `Path.GetFullPath()` - Use `TaskEnvironment.GetAbsolutePath()` instead
- `Environment.Exit()`, `Environment.CurrentDirectory` - Process-level operations
- `Process.Kill()`, `Console.WriteLine()` - Interfere with build process
- ThreadPool, Culture, Assembly loading APIs - Process-wide modifications

### Conditionally-Banned APIs (Smart Detection)

File system APIs are safe with **absolute paths** but dangerous with relative paths:

- ❌ `File.Exists(relativePath)` → MSB4260 warning
- ✅ `File.Exists(TaskEnvironment.GetAbsolutePath(relativePath))` → No warning

**Detected types**: File, Directory, FileInfo, DirectoryInfo, FileStream, StreamReader, StreamWriter

## Code Fixer

Provides "Wrap with TaskEnvironment.GetAbsolutePath()" quick action:

```csharp
// Before (warns)
File.Exists(path)

// After applying fix (no warning)
File.Exists(TaskEnvironment.GetAbsolutePath(path))
```

**Usage**: Press `Ctrl+.` on MSB4260 warning in Visual Studio

## Demo Project

Test the analyzer interactively:

```powershell
cd VisualStudioDemo
start VisualStudioDemo.sln
```

Open `DemoTask.cs` to see:

- **ProblematicTask**: 9 MSB4260 warnings (unwrapped paths)
- **CorrectTask**: 0 warnings (proper TaskEnvironment usage)

See [VisualStudioDemo/README.md](VisualStudioDemo/README.md) for testing guide.

## Build & Verify

```powershell
# Build analyzer
dotnet build ThreadSafeTaskAnalyzer.csproj

# Verify with demo (expect 9 warnings)
dotnet build VisualStudioDemo/VisualStudioDemo.csproj
```

## Documentation

- **[analyzer-spec.md](../../analyzer-spec.md)** - Complete implementation specification
- **[mtspec.md](../../mtspec.md)** - Thread-Safe Tasks API reference
- **[VisualStudioDemo/README.md](VisualStudioDemo/README.md)** - Demo testing guide

## Diagnostic

**ID**: MSB4260  
**Category**: Microsoft.Build.Tasks  
**Severity**: Warning  
**Message**: "Symbol '{0}' is banned in IMultiThreadableTask implementations{1}"

## Integration

Reference the analyzer in task projects:

```xml
<ItemGroup>
  <Analyzer Include="path\to\Microsoft.Build.Utilities.MultiThreadableTaskAnalyzer.dll" />
</ItemGroup>
```

Or via NuGet package (when available):

```xml
<PackageReference Include="Microsoft.Build.Utilities.MultiThreadableTaskAnalyzer" Version="1.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>analyzers</IncludeAssets>
</PackageReference>
```

## Status

**Implementation**: Prototype/Reference  
**Target Framework**: netstandard2.0  
**Dependencies**: Microsoft.CodeAnalysis.CSharp (Roslyn 4.x), Microsoft.CodeAnalysis.Workspaces

---

For next developer implementing final version: See [analyzer-spec.md](../../analyzer-spec.md) for comprehensive design rationale, algorithms, and enhancement opportunities.
