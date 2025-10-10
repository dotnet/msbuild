# IMultiThreadableTask Analyzer - Design Proposal

**Status**: Proposal for Review  
**Target MSBuild Version**: 17.15+  
**Authors**: MSBuild Team  
**Last Updated**: September 30, 2025

---

## Executive Summary

### Problem Statement

MSBuild's multithreaded execution allows tasks implementing `IMultiThreadableTask` to run concurrently within a single process. However, many .NET APIs commonly used in tasks depend on process-global state (e.g., current working directory, environment variables), creating race conditions when tasks execute simultaneously.

**Example Race Condition**:
```csharp
// Task A: sets working directory to C:\ProjectA
// Task B: sets working directory to C:\ProjectB (races with A)
// Task A: calls File.Exists("bin\output.dll") (resolves to C:\ProjectB\bin - WRONG!)
```

### Proposed Solution

A Roslyn analyzer that detects unsafe API usage in `IMultiThreadableTask` implementations at compile time, with an automated code fixer to wrap file paths with `TaskEnvironment.GetAbsolutePath()`.

### Key Benefits

- Catches threading issues at compile time instead of runtime
- Automated fixes reduce manual effort for task authors
- Educates developers about thread-safety requirements
- Prevents race conditions in multithreaded builds

---

## 1. Design Overview

### 1.1 Scope

The analyzer detects four categories of problematic APIs, each with its own diagnostic code:

1. **MSB9999**: APIs that should cause build errors (e.g., `Environment.Exit`, `Process.Kill`)
2. **MSB9998**: APIs requiring TaskEnvironment alternatives (e.g., `Environment.CurrentDirectory`, `Process.Start`)
3. **MSB9997**: Conditionally unsafe with relative paths (e.g., `File.Exists`, `Directory.CreateDirectory`)
4. **MSB9996**: APIs that should generate warnings (e.g., `Assembly.Load`, static fields)

Detailed API list and categorization available in [thread-safe-tasks-api-analysis.md](thread-safe-tasks-api-analysis.md).

### 1.2 Diagnostic Information

| Code | Severity | Description | Code Fixer Available |
|------|----------|-------------|---------------------|
| MSB9999 | Error (proposal) | Critical errors - terminates process, kills threads | No |
| MSB9998 | Error (proposal) | Must use TaskEnvironment APIs instead | No |
| MSB9997 | Warning | Unsafe with relative paths - wrap with GetAbsolutePath | Yes |
| MSB9996 | Warning | Potential threading issues - review carefully | No |

**Category**: Microsoft.Build.Tasks  
**Activation**: Only within types implementing `IMultiThreadableTask`

### 1.3 Code Fixer Capability

Offers automated fix **only for MSB9997** (file path warnings): wraps path arguments with `TaskEnvironment.GetAbsolutePath()`.

**Example Transformation**:
```csharp
// Before (MSB9997 warning):
if (File.Exists(relativePath)) { ... }

// After (automated fix applied):
if (File.Exists(TaskEnvironment.GetAbsolutePath(relativePath))) { ... }
```

**Not available for**: MSB9999, MSB9998, MSB9996 (these require manual code changes or architectural decisions)

---

## 2. Detection Strategy

### 2.1 Category 1: Critical Errors (MSB9999)

**Severity**: Error  
**Description**: APIs that are never safe in multithreaded tasks - no acceptable alternative

Detected via exact symbol matching:
- `Environment.Exit` - Terminates entire process
- `Environment.FailFast` (all overloads) - Immediately terminates process
- `Process.GetCurrentProcess().Kill()` - Terminates entire process
- `ThreadPool.SetMinThreads`, `ThreadPool.SetMaxThreads` - Modifies process-wide thread pool settings
- `CultureInfo.DefaultThreadCurrentCulture` (setter) - Affects all new threads in process
- `CultureInfo.DefaultThreadCurrentUICulture` (setter) - Affects all new threads in process

**Code Fixer**: Not applicable (no safe alternative exists)

**Rationale**: These APIs are never acceptable in multithreaded tasks as they affect the entire MSBuild process or all threads.

### 2.2 Category 2: TaskEnvironment Required (MSB9998)

**Severity**: Warning  
**Description**: APIs that modify/access process-global state - must use TaskEnvironment instead

Detected via exact symbol matching:
- `Environment.CurrentDirectory` (getter/setter) - Use `TaskEnvironment.ProjectCurrentDirectory`
- `Environment.GetEnvironmentVariable` - Use `TaskEnvironment.GetEnvironmentVariable`
- `Environment.SetEnvironmentVariable(string, string)` - Use `TaskEnvironment.SetEnvironmentVariable`
- `Environment.SetEnvironmentVariable(string, string, EnvironmentVariableTarget)` - No direct equivalent (drop target parameter)
- `Path.GetFullPath` (all overloads) - Use `TaskEnvironment.GetAbsolutePath`
- `Process.Start` (non-ProcessStartInfo overloads) - Use `TaskEnvironment.GetProcessStartInfo`
- `ProcessStartInfo` constructors - Use `TaskEnvironment.GetProcessStartInfo`

**Code Fixer**: Automatic fixes provided for:
- ✅ `Environment.CurrentDirectory` → `TaskEnvironment.ProjectCurrentDirectory`
- ✅ `Environment.GetEnvironmentVariable(name)` → `TaskEnvironment.GetEnvironmentVariable(name)`
- ✅ `Environment.SetEnvironmentVariable(name, value)` → `TaskEnvironment.SetEnvironmentVariable(name, value)`
- ✅ `Path.GetFullPath(path)` → `TaskEnvironment.GetAbsolutePath(path)`
- ❌ `Environment.SetEnvironmentVariable(name, value, target)` - No code fixer (complex)
- ❌ `Process.Start(...)` - No code fixer (requires multi-statement refactoring)
- ❌ `ProcessStartInfo` constructors - No code fixer (requires multi-statement refactoring)

**Rationale**: These APIs have TaskEnvironment alternatives. Simple property/method replacements are automated; complex transformations require manual refactoring.

### 2.3 Category 3: Conditional - Require Absolute Paths (MSB9997)

**Severity**: Warning  
**Description**: File system APIs unsafe with relative paths

File system types analyzed:
- `System.IO.File`
- `System.IO.Directory`
- `System.IO.FileInfo`
- `System.IO.DirectoryInfo`
- `System.IO.FileStream`
- `System.IO.StreamReader`
- `System.IO.StreamWriter`

**Detection Logic**:
1. Check if invoked member belongs to one of these types
2. Find first `string` parameter (assumed to be file path)
3. Check if argument is wrapped with `TaskEnvironment.GetAbsolutePath()` or uses `.FullName` property
4. Report MSB9997 only if NOT wrapped

**Recognized Safe Patterns**:
```csharp
// ✅ Safe - wrapped
File.Exists(TaskEnvironment.GetAbsolutePath(path))

// ✅ Safe - absolute path property
File.Exists(fileInfo.FullName)

// ❌ MSB9997 - not wrapped
File.Exists(path)
```

**Code Fixer**: Yes - wraps first string argument with `TaskEnvironment.GetAbsolutePath(...)`

**Rationale**: These APIs are safe when used with absolute paths, making them fixable via automation.

### 2.4 Category 4: Warnings - Potential Issues (MSB9996)

**Severity**: Warning  
**Description**: APIs that may cause threading issues but require case-by-case review

Detected via exact symbol matching:
- `Assembly.Load*`, `Assembly.LoadFrom`, `Assembly.LoadFile` - May cause version conflicts
- `Activator.CreateInstance*` - May cause version conflicts
- `AppDomain.Load`, `AppDomain.CreateInstance*` - May cause version conflicts
- `Console.Write`, `Console.WriteLine`, `Console.ReadLine` - Interferes with build output
- Static field access (future enhancement) - Shared state across threads

**Code Fixer**: Not applicable (requires case-by-case analysis)

**Rationale**: These APIs aren't always wrong but require careful review for thread-safety.

**Recognized Safe Patterns**:
```csharp
// ✅ Safe - wrapped
File.Exists(TaskEnvironment.GetAbsolutePath(path))

// ✅ Safe - absolute path property
File.Exists(fileInfo.FullName)
File.Exists(directoryInfo.FullName)

// ❌ Warning - not wrapped
File.Exists(path)
```

**Design Decision**: Pattern-based detection (vs. listing every method) keeps analyzer maintainable as APIs evolve.

### 2.3 Current Limitations

- **No data-flow analysis**: Doesn't track if `path` variable already contains absolute path
- **First parameter assumption**: Only checks first string parameter for paths
- **No constant analysis**: Warns even for string literals like `"C:\\Windows\\System32"`

**Rationale**: These limitations prevent false negatives at cost of some false positives. Developers can suppress warnings where appropriate.

---

## 3. Code Fixer Design

### 3.1 Scope

Code fixers provided for:
- **MSB9997**: Wraps file path arguments with `TaskEnvironment.GetAbsolutePath(...)`
- **MSB9998**: Simple API migrations to TaskEnvironment (property/method replacements only)

### 3.2 MSB9997 Transformation Rules (File Paths)

**Path wrapping fixes:**
- Wraps first string argument with `TaskEnvironment.GetAbsolutePath(...)`
- Preserves all other arguments unchanged
- Adds `using Microsoft.Build.Framework;` directive if needed

**Example:**
```csharp
// Before:
File.Exists(somePath)

// After:
File.Exists(TaskEnvironment.GetAbsolutePath(somePath))
```

### 3.3 MSB9998 Transformation Rules (Simple API Migrations)

**Property replacements:**
- `Environment.CurrentDirectory` → `TaskEnvironment.ProjectCurrentDirectory`
- Handles both getter and setter contexts
- Adds cast to `string` when needed for `AbsolutePath` return type

**Method replacements:**
- `Environment.GetEnvironmentVariable(name)` → `TaskEnvironment.GetEnvironmentVariable(name)`
- `Environment.SetEnvironmentVariable(name, value)` → `TaskEnvironment.SetEnvironmentVariable(name, value)`
- `Path.GetFullPath(path)` → `TaskEnvironment.GetAbsolutePath(path)`

**Not fixable (too complex):**
- `Process.Start(...)` - Requires multi-statement refactoring to use `GetProcessStartInfo()`
- `ProcessStartInfo` constructors - Requires multi-statement refactoring
- `Environment.SetEnvironmentVariable(name, value, target)` - Target parameter must be manually removed

**Examples:**
```csharp
// Before:
string dir = Environment.CurrentDirectory;
string value = Environment.GetEnvironmentVariable("PATH");
string full = Path.GetFullPath("file.txt");

// After:
string dir = TaskEnvironment.ProjectCurrentDirectory;
string value = TaskEnvironment.GetEnvironmentVariable("PATH");
string full = TaskEnvironment.GetAbsolutePath("file.txt");
```

### 3.4 User Experience

- Fixes appear in Visual Studio Quick Actions (Ctrl+.)
- Action titles:
  - "Wrap with TaskEnvironment.GetAbsolutePath()" (MSB9997)
  - "Use TaskEnvironment.ProjectCurrentDirectory" (MSB9998)
  - "Use TaskEnvironment.GetEnvironmentVariable()" (MSB9998)
  - "Use TaskEnvironment.SetEnvironmentVariable()" (MSB9998)
  - "Use TaskEnvironment.GetAbsolutePath()" (MSB9998 for Path.GetFullPath)
- Available immediately when warnings appear

---

## 4. Open Questions for Review

### 4.1 Distribution Model (Proposal)

**Recommended**: Ship analyzer with `Microsoft.Build.Utilities.Core` NuGet package

**Pros**:
- Task authors already reference this package
- Zero configuration required
- Analyzer version stays synchronized with APIs
- Automatic updates

**Cons**:
- Increases package size
- Cannot be disabled without disabling entire analyzer infrastructure

**Alternative Considered**: Standalone `Microsoft.Build.Analyzers` package
- **Rejected**: Discovery problem, extra manual step reduces adoption

**Question**: Do we have consensus on shipping with Utilities.Core?

### 4.2 Default Severity Levels

**Current Proposal**:
- **MSB9999**: Error (no safe alternative - Environment.Exit, Process.Kill, ThreadPool settings, CultureInfo defaults)
- **MSB9998**: Warning (has TaskEnvironment alternative - Environment.CurrentDirectory, Get/SetEnvironmentVariable, Path.GetFullPath, Process.Start)
- **MSB9997**: Warning (conditional safety - file path methods)
- **MSB9996**: Warning (informational - Console, Assembly.Load)

**Rationale for MSB9999 as Error**: These APIs have no acceptable workaround and would cause serious issues in multithreaded builds (process termination, process-wide state corruption).

**Rationale for MSB9998 as Warning**: While these APIs need migration, they have clear TaskEnvironment alternatives and code fixers to ease transition. Warning severity allows gradual adoption.

**Question**: Should MSB9998 be elevated to Error in a future release after adoption period?

### 4.3 Opt-Out Mechanism

**Proposal**: MSBuild property `EnableMSBuildThreadSafetyAnalyzer`

```xml
<PropertyGroup>
  <EnableMSBuildThreadSafetyAnalyzer>false</EnableMSBuildThreadSafetyAnalyzer>
</PropertyGroup>
```

**Question**: Is property name acceptable? Should we support per-diagnostic severity via `.editorconfig` only?

### 4.4 Scope of Analysis

**Proposal**: Analyzer only activates within types implementing `IMultiThreadableTask`

**Alternative**: Analyze all code
- **Rejected**: Creates noise for non-multithreadable tasks

**Question**: Should we offer opt-in to analyze all Task types (not just IMultiThreadableTask)?

---

## 5. Testing Approach

### 5.1 Validation

A demo project demonstrates analyzer behavior:
- **ProblematicTask**: Task with 9 unsafe API usages → expects mixture of MSB9999/MSB9998/MSB9997/MSB9996 warnings
- **CorrectTask**: Same logic using wrapped paths → expects 0 warnings

**Location**: `src/ThreadSafeTaskAnalyzer/VisualStudioDemo/`

### 5.2 Manual Testing

1. Open demo in Visual Studio
2. Observe diagnostics on unsafe API usages (squiggles based on severity)
3. Verify Quick Action offers "Wrap with..." fix for MSB9997 only
4. Apply fix and confirm warning disappears

---

## 6. Design Rationale

### 6.1 Why Two Detection Modes?

**Always-banned APIs** require explicit symbol matching because:
- No safe usage pattern exists (e.g., `Environment.Exit` always wrong)
- Need precise, actionable error messages

**Conditionally-banned APIs** use pattern detection because:
- Safe when used correctly (with absolute paths)
- Hundreds of methods across file system types - listing all is unmaintainable

### 6.2 Why Check Only First String Parameter?

.NET file system APIs follow consistent pattern:
```csharp
File.ReadAllText(string path)            // path is first param
Directory.CreateDirectory(string path)   // path is first param
new FileStream(string path, FileMode mode) // path is first param
```

**Tradeoff**: Simplicity and performance vs. handling rare edge cases. False negatives possible for unusual APIs, but unlikely in practice.

### 6.3 Why Not Full Data-Flow Analysis?

**Rejected**: Tracking whether variables contain absolute paths via data-flow analysis

**Reasons**:
- Extremely complex for limited benefit
- Performance impact on live editing
- Many code paths are unknowable at compile time (user input, config files, etc.)

**Chosen approach**: Conservative warnings + developer suppressions for known-safe cases

---

## 7. Future Enhancements (Out of Scope for v1)

1. **Constant analysis**: Suppress warnings for string literal absolute paths (`"C:\\Windows"`)
2. **Data-flow tracking**: Recognize when variable provably contains absolute path
3. **Strict mode**: Warn on all path operations, even when wrapped
4. **VB.NET support**: Currently C# only
5. **Additional banned APIs**: Console.SetOut, AppDomain.SetData, etc.
6. **Build-time enforcement**: MSBuild task that fails build on violations

---

## 8. Known Limitations

1. **String literals**: Warns on `File.Exists("C:\\absolute\\path")` even though safe
2. **Method chaining**: May not recognize `GetAbsolutePath()` in complex expressions
3. **Reflection**: Cannot detect dynamic API calls via reflection
4. **Code generation**: Does not analyze dynamically generated code
5. **Performance**: No specific performance optimizations yet implemented

These are acceptable for initial version. Can be addressed based on user feedback.

---

## 9. References

- [thread-safe-tasks-api-analysis.md](thread-safe-tasks-api-analysis.md) - Complete list of banned APIs
- [thread-safe-tasks.md](thread-safe-tasks.md) - Thread-safe tasks overview
- [multithreaded-msbuild.md](multithreaded-msbuild.md) - Multithreaded MSBuild specification
- Demo implementation: `src/ThreadSafeTaskAnalyzer/`
- Sample usage: `src/ThreadSafeTaskAnalyzer/VisualStudioDemo/`

---

**Document Version**: 1.0  
**Status**: Awaiting review and decision on open questions
