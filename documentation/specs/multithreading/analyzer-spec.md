# IMultiThreadableTask Analyzer - Design Proposal

**Target MSBuild Version**: 18.1+  
**Authors**: @JanProvaznik  
**Last Updated**: October 1, 2025

---

## Executive Summary

### Problem

MSBuild's multithreaded execution allows tasks implementing `IMultiThreadableTask` to run concurrently. Many .NET APIs depend on process-global state (working directory, environment variables), creating race conditions:

```csharp
// Task A: Environment.CurrentDirectory = "C:\\ProjectA"
// Task B: Environment.CurrentDirectory = "C:\\ProjectB" (races with A)
// Task A: File.Exists("bin\\output.dll") // Resolves incorrectly!
```

### Solution

Roslyn analyzer detecting unsafe API usage in `IMultiThreadableTask` with automated code fixers.

### Benefits

- Compile-time detection
- Automated fixes for common patterns
- Education through diagnostic messages

---

## 1. Design Overview

### 1.1 Diagnostic Codes

| Code | Severity | Description | Fixer |
|------|----------|-------------|-------|
| MSB9999 | Error | Critical APIs (no safe alternative) | No |
| MSB9998 | Warning | APIs needing TaskEnvironment | Partial |
| MSB9997 | Warning | File APIs needing absolute paths | Yes |
| MSB9996 | Warning | Potentially problematic APIs | No |

**Activation**: Only in types implementing `IMultiThreadableTask`

### 1.2 Code Fixer Support

- **MSB9997**: Wraps paths with `TaskEnvironment.GetAbsolutePath()`
- **MSB9998**: Replaces simple APIs (CurrentDirectory, GetEnvironmentVariable, GetFullPath)
- **Manual**: Process.Start, ProcessStartInfo, MSB9999, MSB9996

---

## 2. Detection Strategy

### 2.1 MSB9999: Critical Errors

**Severity**: Error | **Detection**: Symbol matching | **Fixer**: No

**APIs**:
- `Environment.Exit`, `FailFast` - Terminate process
- `Process.Kill()` - Terminates process
- `ThreadPool.SetMinThreads/MaxThreads` - Process-wide settings
- `CultureInfo.DefaultThreadCurrent*Culture` (setters) - Affect all threads

**Rationale**: No safe alternative. Tasks signal failure via return value/exception.

### 2.2 MSB9998: TaskEnvironment Required

**Severity**: Warning | **Detection**: Symbol matching | **Fixer**: Partial

**APIs**:
- `Environment.CurrentDirectory` → `TaskEnvironment.ProjectCurrentDirectory` (✅ fixer)
- `Environment.GetEnvironmentVariable` → `TaskEnvironment.GetEnvironmentVariable` (✅ fixer)
- `Environment.SetEnvironmentVariable(name, value)` → `TaskEnvironment.SetEnvironmentVariable` (✅ fixer)
- `Path.GetFullPath` → `TaskEnvironment.GetAbsolutePath` (✅ fixer)
- `Process.Start(...)`, `ProcessStartInfo` ctors → `TaskEnvironment.GetProcessStartInfo()` (❌ manual)

### 2.3 MSB9997: File System APIs

**Severity**: Warning | **Detection**: Pattern matching | **Fixer**: Yes

**Types analyzed**: `File`, `Directory`, `FileInfo`, `DirectoryInfo`, `FileStream`, `StreamReader`, `StreamWriter`

**Detection**: Warns if first `string` parameter is not:
- Wrapped with `TaskEnvironment.GetAbsolutePath()`
- A `.FullName` property
- An `AbsolutePath` type

**Example**:
```csharp
File.Exists(TaskEnvironment.GetAbsolutePath(path));  // ✅
File.Exists(fileInfo.FullName);                       // ✅
File.Exists(path);                                    // ❌ MSB9997
```

### 2.4 MSB9996: Potential Issues

**Severity**: Warning | **Detection**: Symbol matching | **Fixer**: No

**APIs**:
- `Assembly.Load*`, `LoadFrom`, `LoadFile` - May cause conflicts
- `Activator.CreateInstance*`, `AppDomain.Load/CreateInstance*` - May cause conflicts
- `Console.*` (Write, WriteLine, ReadLine, etc.) - Interferes with logging

**Rationale**: Not always wrong, requires case-by-case review.

### 2.5 Limitations

- No data-flow analysis (doesn't track if variable is absolute)
- First-parameter heuristic only
- No constant folding (warns on `"C:\\Windows"` literals)

**Suppression**: `#pragma`, `.editorconfig`, or `[SuppressMessage]`

---

## 3. Code Fixers

### 3.1 MSB9998: API Replacements

```csharp
Environment.CurrentDirectory       → TaskEnvironment.ProjectCurrentDirectory
Environment.GetEnvironmentVariable → TaskEnvironment.GetEnvironmentVariable  
Environment.SetEnvironmentVariable → TaskEnvironment.SetEnvironmentVariable
Path.GetFullPath                   → TaskEnvironment.GetAbsolutePath
```

### 3.2 MSB9997: Path Wrapping

Wraps first `string` argument:

```csharp
- File.Exists(somePath)
+ File.Exists(TaskEnvironment.GetAbsolutePath(somePath))
```

### 3.3 User Experience

**IDE**: Quick Actions (Ctrl+.) with "Fix All" support  
**CLI**: `dotnet format analyzers --diagnostics MSB9997 MSB9998`

---

## 4. Open Questions

### 4.1 Distribution

**Proposal**: Ship with `Microsoft.Build.Utilities.Core` NuGet package

**Pros**: Already referenced, zero config, automatic updates  
**Cons**: Increases package size

**Question**: Consensus on Utilities.Core vs standalone package?

### 4.2 Severity Levels

**Proposal**:
- MSB9999: Error (no alternative exists)
- MSB9998: Warning (migration path available)
- MSB9997: Warning
- MSB9996: Warning

**Question**: Should MSB9998 become Error in future release?

### 4.3 Opt-Out

**Proposal**: `<EnableMSBuildThreadSafetyAnalyzer>false</EnableMSBuildThreadSafetyAnalyzer>`

**Question**: Property name OK? Or only `.editorconfig` support?

### 4.4 Scope

**Proposal**: Only analyze `IMultiThreadableTask` implementations

**Question**: Offer opt-in for all Task types?

---

## 5. Testing

**Demo**: `src/ThreadSafeTaskAnalyzer/VisualStudioDemo/`
- `ProblematicTask`: 13 diagnostics (2 errors, 11 warnings)
- `CorrectTask`: 0 diagnostics

**Validation**: Open in VS, test fixers via Ctrl+., or `dotnet build`

---

## 6. Design Decisions

**Detection strategies**:
- Symbol matching (MSB9999/98/96): Exact identification for always-unsafe APIs
- Pattern matching (MSB9997): Type-based for conditionally-unsafe APIs

**First-parameter heuristic**: BCL file APIs consistently put path first. Simple and fast.

**No data-flow analysis**: Minimizes false positives by detecting actual problematic API calls, not guessing about data. Path analysis would be complex, hurt performance, and still couldn't handle runtime values.


---

## 8. Known Limitations

1. Warns on absolute path literals (`"C:\\Windows"`)
2. May miss `GetAbsolutePath()` in complex expressions
3. Cannot detect reflection-based API calls
4. No analysis of generated code

---

## 9. References

**Full PR**: https://github.com/dotnet/msbuild/pull/12143

- [thread-safe-tasks-api-analysis.md](thread-safe-tasks-api-analysis.md) - Complete API list
- [thread-safe-tasks.md](thread-safe-tasks.md) - Thread-safe tasks overview
- [multithreaded-msbuild.md](multithreaded-msbuild.md) - Multithreaded MSBuild spec
- Demo: `src/ThreadSafeTaskAnalyzer/VisualStudioDemo/`
