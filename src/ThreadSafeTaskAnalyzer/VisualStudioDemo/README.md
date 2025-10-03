# IMultiThreadableTask Analyzer Demo

## Purpose

This demo project validates the IMultiThreadableTask analyzer and code fixer functionality in Visual Studio. It demonstrates detection of unsafe APIs across 4 diagnostic categories and automated remediation through code fixes.

## Quick Start

1. **Open the solution**: `VisualStudioDemo.sln`

2. **Open the demo file**: `DemoTask.cs`

3. **Observe diagnostics**: ProblematicTask should show 13 diagnostics (2 errors, 11 warnings)

4. **Test code fixers**:
   - Place cursor on MSB9997 or MSB9998 warnings
   - Press `Ctrl+.` (or click the lightbulb 💡)
   - Select the appropriate fix
   - Verify warning disappears

## Expected Results

### ProblematicTask (13 diagnostics across 4 categories)

#### MSB9999 Errors (2) - Critical APIs with no safe alternative
- Line 76: `ThreadPool.SetMaxThreads(10, 10)` - Modifies process-wide settings
- Line 81: `Environment.Exit(1)` - Terminates entire process

#### MSB9998 Warnings (3) - TaskEnvironment required (Code fixers available!)
- Line 39: `Environment.CurrentDirectory` → Use `TaskEnvironment.ProjectCurrentDirectory`
- Line 42: `Environment.GetEnvironmentVariable("PATH")` → Use `TaskEnvironment.GetEnvironmentVariable`
- Line 58: `Path.GetFullPath(InputFile)` → Use `TaskEnvironment.GetAbsolutePath`

#### MSB9997 Warnings (7) - File paths need absolute (Code fixers available!)
- Line 45: `File.Exists(InputFile)`
- Line 48: `Directory.Exists(OutputDirectory)`
- Line 51: `Directory.CreateDirectory(OutputDirectory)`
- Line 55: `File.ReadAllText(InputFile)`
- Line 61: `new FileInfo(InputFile)`
- Line 64: `new DirectoryInfo(OutputDirectory)`
- Line 67: `new StreamReader(InputFile)`

#### MSB9996 Warnings (1) - Potential issues
- Line 73: `Console.WriteLine(...)` - May interfere with build logging

### CorrectTask

**0 diagnostics** - all code uses TaskEnvironment APIs correctly

## Build Verification

```powershell
dotnet build VisualStudioDemo.csproj
```

**Expected**: 2 MSB9999 errors, 11 warnings (3 MSB9998, 7 MSB9997, 1 MSB9996)

## What This Demonstrates

### 4-Category Diagnostic System

| Code | Severity | Example | Code Fixer |
|------|----------|---------|------------|
| MSB9999 | Error | `Environment.Exit`, `ThreadPool.SetMaxThreads` | ❌ No |
| MSB9998 | Warning | `Environment.CurrentDirectory`, `Path.GetFullPath` | ✅ Yes |
| MSB9997 | Warning | `File.Exists(path)` without wrapping | ✅ Yes |
| MSB9996 | Warning | `Console.WriteLine` | ❌ No |

### Code Fixer Examples

**MSB9998**: Simple API replacements
- `Environment.CurrentDirectory` → `TaskEnvironment.ProjectCurrentDirectory`
- `Environment.GetEnvironmentVariable("X")` → `TaskEnvironment.GetEnvironmentVariable("X")`
- `Path.GetFullPath(p)` → `TaskEnvironment.GetAbsolutePath(p)`

**MSB9997**: Path wrapping
- `File.Exists(path)` → `File.Exists(TaskEnvironment.GetAbsolutePath(path))`

## Reference

- [analyzer-spec.md](../../../../documentation/specs/multithreading/analyzer-spec.md) - Full specification
- [thread-safe-tasks.md](../../../../documentation/specs/multithreading/thread-safe-tasks.md) - Thread-Safe Tasks API reference
