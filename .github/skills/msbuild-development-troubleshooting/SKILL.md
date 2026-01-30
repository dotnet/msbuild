---
name: msbuild-development-troubleshooting
description: Advanced debugging techniques for MSBuild engine issues. Use when investigating build failures, task execution problems, or need to understand MSBuild internals through logging and diagnostics.
---

# MSBuild Debugging Techniques

This skill covers advanced debugging approaches for investigating MSBuild engine behavior.

## Binary Logs

Binary logs capture everything MSBuild does. Useful for sharing with humans or when a GUI viewer is available:

```cmd
dotnet build project.csproj -bl
# Creates msbuild.binlog - open with MSBuild Structured Log Viewer (msbuildlog.com)
```

For CLI-based debugging, use diagnostic verbosity instead (see below).

## Diagnostic Verbosity

When binary logs aren't enough, increase console verbosity:

```cmd
dotnet build -v:diag > build.log 2>&1
```

Search the log for:
- `Building target` - Which targets executed
- `Skipping target` - Why targets were skipped (up-to-date checks)
- `Output Item(s)` - What items tasks produced
- `Set Property` - Property value changes

## Environment Variables for Deep Debugging

```cmd
:: Show imports and their locations
set MSBUILDLOGIMPORTS=1

:: Log detailed evaluation info
set MSBUILDDEBUGEVALUATION=1

:: Log target outputs in detail
set MSBUILDTARGETOUTPUTLOGGING=1

:: Force debugger attach on MSBuild start
set MSBUILDDEBUGONSTART=1

:: Debug scheduler decisions
set MSBUILDDEBUGSCHEDULER=1

:: Write debug files to specific location
set MSBUILDDEBUGPATH=C:\temp\msbuild-debug
```

## Task Debugging

### Finding Task Source
Tasks live in `src/Tasks/`. To find a specific task:
```powershell
Get-ChildItem -Recurse src/Tasks -Filter "*.cs" | Select-String "class.*TaskName.*:" | Select-Object -First 5
```

### Task Logging
Inside task code, use `Log` property:
```csharp
Log.LogMessage(MessageImportance.High, "Debug: value={0}", someValue);
Log.LogWarning("Suspicious condition: {0}", condition);
```

## Incremental Build Debugging

When builds don't detect changes correctly:

1. **Check inputs/outputs**: Targets use `Inputs` and `Outputs` attributes
   ```cmd
   dotnet build -v:diag | findstr "Skipping target"
   ```

2. **Force rebuild of specific target**:
   ```cmd
   dotnet build -t:TargetName
   ```

3. **See what's considered up-to-date**:
   - Binary log shows "Target is up-to-date" with file comparisons
   - Check timestamps match expectations

## Key Source Locations

| Component | Location | Purpose |
|-----------|----------|---------|
| Engine core | `src/Build/BackEnd/` | Build execution, scheduling |
| Task execution | `src/Build/BackEnd/TaskExecutionHost/` | How tasks are invoked |
| Project evaluation | `src/Build/Evaluation/` | Reading and evaluating projects |
| Built-in tasks | `src/Tasks/` | Copy, Exec, Message, etc. |

## Attaching Debugger

### To Main Build Process
```cmd
set MSBUILDDEBUGONSTART=1
dotnet build project.csproj
# Dialog appears - attach Visual Studio
```

### To Worker Nodes
Worker nodes are separate processes. Use Process Explorer or Task Manager to find `dotnet.exe` processes running MSBuild, then attach.

## References

- [Binary Log Documentation](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Binary-Log.md)
- [Providing Binary Logs for Investigation](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Providing-Binary-Logs.md)
- [MSBuild Target Maps](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Target-Maps.md)
