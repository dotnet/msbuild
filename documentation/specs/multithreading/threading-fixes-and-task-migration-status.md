# Threading Fixes and Logic Updates Missing from Main Branch

This document outlines the threading-related fixes, logic updates, and task migration changes present in the `multithreaded-prototype-rebased` feature branch that are not yet in the main branch.

## Overview

The `multithreaded-prototype-rebased` branch contains significant work to enable MSBuild to run multiple projects concurrently within the same process (multithreaded execution mode). While the main branch has foundational APIs like `IMultiThreadableTask`, `TaskEnvironment`, and `TaskRouter`, the feature branch contains critical implementation fixes and task migrations that are required for a functional multithreaded execution model.

## Threading Fixes and Core Logic Updates

### 1. Thread-Static Current Directory Support

**Status:** Missing from main branch

**Description:** The feature branch implements thread-local current directory tracking to enable concurrent task execution without directory conflicts.

**Changes Required:**

#### src/Framework/NativeMethods.cs
- Add thread-static field `CurrentThreadWorkingDirectory` to track per-thread working directory
- This allows the Expander and other components to use thread-specific directories without conflicts

```csharp
[ThreadStatic]
internal static string CurrentThreadWorkingDirectory;
```

#### src/Shared/Modifiers.cs
- Update `GetFullPath` logic to check thread-static current directory when regular current directory is unavailable
- Fallback to `NativeMethodsShared.CurrentThreadWorkingDirectory` when `currentDirectory` is null or empty

```csharp
if (string.IsNullOrEmpty(currentDirectory))
{
    currentDirectory = NativeMethodsShared.CurrentThreadWorkingDirectory
                       ?? string.Empty;
}
```

#### src/Build/BackEnd/Components/RequestBuilder/TaskBuilder.cs
- Set `NativeMethodsShared.CurrentThreadWorkingDirectory` before task execution
- This ensures each thread maintains its own view of the "current" directory

```csharp
NativeMethodsShared.CurrentThreadWorkingDirectory = requestEntry.ProjectRootDirectory;
```

**Rationale:** Without thread-local directory tracking, concurrent tasks would conflict when accessing relative paths, leading to incorrect builds or race conditions.

---

### 2. MetadataLoadContext Thread Safety

**Status:** Missing from main branch

**Description:** The feature branch implements thread-safe access to `MetadataLoadContext` used for task type loading, preventing race conditions during concurrent task discovery.

**Changes Required:**

#### src/Shared/TypeLoader.cs
- Add static lock object `s_contextLock` for synchronizing MetadataLoadContext operations
- Add reference counting `_contextRefCount` to prevent premature disposal
- Replace `CreateMetadataLoadContext` with `LoadAssemblyUsingMetadataLoadContext` that caches and reuses context
- Add `ReleaseMetadataLoadContext` method for proper resource cleanup

**Key changes:**
1. **Shared MetadataLoadContext:** Instead of creating a new context for each assembly load, maintain a single shared context with reference counting
2. **Thread-safe access:** Use locks to ensure only one thread modifies the context at a time
3. **Reference counting:** Track active users of the context to prevent disposal while in use

```csharp
private static MetadataLoadContext _context;
private static readonly object s_contextLock = new object();
private static int _contextRefCount = 0;

private static Assembly LoadAssemblyUsingMetadataLoadContext(AssemblyLoadInfo assemblyLoadInfo)
{
    lock (s_contextLock)
    {
        if (_context == null)
        {
            // Create new context with resolved assemblies
            _context = new MetadataLoadContext(new PathAssemblyResolver(assembliesDictionary.Values));
        }
        
        _contextRefCount++;
        return _context.LoadFromAssemblyPath(path);
    }
}

private static void ReleaseMetadataLoadContext()
{
    lock (s_contextLock)
    {
        _contextRefCount--;
        if (_contextRefCount <= 0)
        {
            _context?.Dispose();
            _context = null;
            _contextRefCount = 0;
        }
    }
}
```

**Rationale:** Without these changes, concurrent task loading can cause race conditions in metadata reflection, leading to crashes or incorrect task type detection.

---

### 3. TaskEnvironment Integration with Change Waves

**Status:** Missing from main branch

**Description:** The feature branch adds proper change wave gating for the transition between legacy directory/environment management and the new TaskEnvironment-based approach.

**Changes Required:**

#### src/Build/BackEnd/Components/RequestBuilder/TaskBuilder.cs
- Add change wave check (`ChangeWaves.Wave18_0`) to conditionally use TaskEnvironment
- When Wave18_0 is enabled, use `TaskEnvironment.ProjectDirectory` instead of `NativeMethodsShared.SetCurrentDirectory`
- Maintain backward compatibility for older change wave levels

```csharp
if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_0))
{
    _buildRequestEntry.TaskEnvironment.ProjectDirectory = new AbsolutePath(_buildRequestEntry.ProjectRootDirectory, ignoreRootedCheck: true);
}
else
{
    NativeMethodsShared.SetCurrentDirectory(_buildRequestEntry.ProjectRootDirectory);
}
```

#### src/Build/BackEnd/Components/RequestBuilder/RequestBuilder.cs
- Add change wave checks in `SetProjectDirectory`, `RestoreOperatingEnvironment`, and environment variable setting
- Dual-write to both TaskEnvironment and process environment during transition period
- Use `NativeMethodsShared.GetCurrentDirectory()` for saving/restoring in legacy mode

**Key changes in RequestBuilder.cs:**
1. **SetProjectDirectory:** Gate TaskEnvironment usage with change wave
2. **Environment variable restoration:** Dual-write to both TaskEnvironment and process environment
3. **SaveOperatingEnvironment:** Use process state APIs in legacy mode, TaskEnvironment in new mode

**Rationale:** Change waves allow gradual migration to the new model while maintaining backward compatibility for existing builds.

---

### 4. TaskRouter Enhancements

**Status:** Partially in main branch, missing enhancements

**Description:** The feature branch contains updates to task routing logic that improve how multithreaded-capable tasks are identified and routed.

**Changes Required:**

#### src/Build/BackEnd/Components/RequestBuilder/TaskRouter.cs
- Enhanced logic for detecting `MSBuildMultiThreadableTaskAttribute` on tasks
- Improved routing decisions based on execution mode and task capabilities
- Better integration with AssemblyTaskFactory for attribute detection

#### src/Build/Instance/TaskFactories/AssemblyTaskFactory.cs
- Add routing hints and capability detection for loaded tasks
- Propagate task threading capabilities to the execution engine

**Rationale:** Proper task routing is essential for ensuring multithreaded-capable tasks run in-process while legacy tasks use TaskHost isolation.

---

### 5. TaskBuilder SetTaskEnvironment Method

**Status:** Missing from main branch

**Description:** The feature branch adds a method to explicitly set TaskEnvironment on the TaskExecutionHost.

**Changes Required:**

#### src/Build/BackEnd/Components/RequestBuilder/TaskBuilder.cs
- Add `SetTaskEnvironment(TaskEnvironment taskEnvironment)` method
- This allows the execution pipeline to configure task environment before task execution

```csharp
/// <summary>
/// Sets the task environment on the TaskExecutionHost for use with IMultiThreadableTask instances.
/// </summary>
/// <param name="taskEnvironment">The task environment to set, or null to clear.</param>
public void SetTaskEnvironment(TaskEnvironment taskEnvironment)
{
    ErrorUtilities.VerifyThrow(_taskExecutionHost != null, "TaskExecutionHost must be initialized before setting TaskEnvironment.");
    _taskExecutionHost.TaskEnvironment = taskEnvironment;
}
```

**Rationale:** This provides explicit control over when and how TaskEnvironment is configured for task execution.

---

### 6. TaskExecutionHost Environment Handling

**Status:** Missing from main branch

**Description:** Updates to how TaskExecutionHost manages and passes environment information to tasks.

**Changes Required:**

#### src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs
- Remove passing of `TaskEnvironment` to legacy task execution paths
- Ensure TaskEnvironment is only used for tasks implementing `IMultiThreadableTask`
- Properly initialize TaskEnvironment property before task instantiation

**Rationale:** Clean separation between legacy task execution and multithreaded task execution paths.

---

### 7. Well-Known Functions Path Resolution

**Status:** Missing from main branch

**Description:** Updates to intrinsic functions to use TaskEnvironment for path resolution in multithreaded mode.

**Changes Required:**

#### src/Build/Evaluation/IntrinsicFunctions.cs (or similar)
- Update `GetFullPath` and related functions to respect thread-local directories
- Use `TaskEnvironment` when available for path resolution

**Rationale:** Ensures property functions like `$([System.IO.Path]::GetFullPath(...))` work correctly in multithreaded execution.

---

### 8. SDK Resolver Path Resolution

**Status:** Missing from main branch

**Description:** Updates to SDK resolver to handle paths correctly in multithreaded scenarios.

**Changes Required:**

#### SDK resolver components
- Use appropriate path resolution that respects thread-local directories
- Ensure SDK resolution doesn't rely on global process state

**Rationale:** SDK resolution must work correctly when multiple projects with different roots are being resolved concurrently.

---

### 9. Intrinsic Tasks (MSBuild, CallTarget) Multithreading Support

**Status:** Missing from main branch

**Description:** The intrinsic tasks (MSBuild and CallTarget) need to be marked as multithreaded-capable.

**Changes Required:**

#### src/Build/BackEnd/Components/RequestBuilder/IntrinsicTasks/MSBuild.cs
- Add `[MSBuildMultiThreadableTask]` attribute

#### src/Build/BackEnd/Components/RequestBuilder/IntrinsicTasks/CallTarget.cs
- Add `[MSBuildMultiThreadableTask]` attribute

**Rationale:** These tasks orchestrate build execution and must be able to run in multithreaded mode.

---

### 10. TaskHost Parameter Handling

**Status:** Missing from main branch

**Description:** Improvements to how parameters are passed to TaskHost processes, including proper handling of execution context.

**Changes Required:**

#### src/Build/Instance/TaskFactories/TaskHostTask.cs
- Ensure TaskEnvironment is properly serialized and passed to out-of-process TaskHost
- Handle TaskHost lifecycle correctly in multithreaded scenarios

**Rationale:** Tasks running in TaskHost processes need consistent environment regardless of execution mode.

---

## Tasks Migration to New API Interface

The following section documents which tasks have been migrated to implement `IMultiThreadableTask` in the feature branch but are still using legacy execution in main.

### Migration Status

The feature branch has migrated **25 tasks** to implement `IMultiThreadableTask`. These tasks use `TaskEnvironment` for thread-safe access to working directory and environment variables.

### Tasks Migrated in Feature Branch (Not in Main)

#### File I/O Tasks
1. **Copy** (`src/Tasks/Copy.cs`)
   - Implements `IMultiThreadableTask`
   - Uses `TaskEnvironment.GetAbsolutePath()` for source and destination paths
   - Uses `TaskEnvironment.GetEnvironmentVariable()` for configuration checks
   - Passes `TaskEnvironment` to `FileState` constructors

2. **Delete** (`src/Tasks/Delete.cs`)
   - Implements `IMultiThreadableTask`
   - Uses `TaskEnvironment` for path resolution

3. **MakeDir** (`src/Tasks/MakeDir.cs`)
   - Implements `IMultiThreadableTask`
   - Uses `TaskEnvironment` for directory path resolution

4. **RemoveDir** (`src/Tasks/RemoveDir.cs`)
   - Implements `IMultiThreadableTask`
   - Uses `TaskEnvironment` for directory operations

5. **Touch** (`src/Tasks/Touch.cs`)
   - Implements `IMultiThreadableTask`
   - Uses `TaskEnvironment` for file path resolution

6. **ReadLinesFromFile** (`src/Tasks/FileIO/ReadLinesFromFile.cs`)
   - Implements `IMultiThreadableTask`
   - Uses `TaskEnvironment` for file access

7. **WriteLinesToFile** (`src/Tasks/FileIO/WriteLinesToFile.cs`)
   - Implements `IMultiThreadableTask`
   - Uses `TaskEnvironment` for file writing

#### Execution Tasks
8. **Exec** (`src/Tasks/Exec.cs`)
   - Implements `IMultiThreadableTask`
   - Propagates `TaskEnvironment` to `ToolTask` base class
   - Ensures executed processes inherit correct working directory

9. **ToolTask** (`src/Utilities/ToolTask.cs`)
   - Base class updated to support `IMultiThreadableTask`
   - Uses `TaskEnvironment` for working directory when launching tool processes

#### Build Orchestration Tasks
10. **MSBuild** (`src/Tasks/MSBuild.cs`)
    - Implements `IMultiThreadableTask`
    - Critical for nested build invocations in multithreaded mode

11. **CallTarget** (`src/Tasks/CallTarget.cs`)
    - Implements `IMultiThreadableTask`
    - Allows target invocation in multithreaded scenarios

#### Resolution and Assembly Tasks
12. **ResolveAssemblyReference** (`src/Tasks/AssemblyDependency/ResolveAssemblyReference.cs`)
    - Implements `IMultiThreadableTask`
    - Most complex migration due to extensive file system access
    - Updates to multiple resolver classes:
      - `AssemblyFoldersExResolver`
      - `AssemblyFoldersFromConfigResolver`
      - `AssemblyFoldersFromConfigCache`
      - `AssemblyResolution`
      - `CandidateAssemblyFilesResolver`
      - `DirectoryResolver`
      - `FrameworkPathResolver`
      - `GacResolver`
      - `HintPathResolver`
      - `RawFilenameResolver`
      - `ReferenceTable`
      - `Resolver` (base class)
    - All resolvers updated to accept and use `TaskEnvironment` for path resolution

13. **ResolveComReference** (`src/Tasks/ResolveComReference.cs`)
    - Implements `IMultiThreadableTask`
    - Uses `TaskEnvironment` for COM assembly resolution

14. **ResolveProjectBase** (`src/Tasks/ResolveProjectBase.cs`)
    - Base class for resolution tasks
    - Implements `IMultiThreadableTask`

#### Path and Culture Tasks
15. **AssignCulture** (`src/Tasks/AssignCulture.cs`)
    - Implements `IMultiThreadableTask`

16. **AssignTargetPath** (`src/Tasks/AssignTargetPath.cs`)
    - Implements `IMultiThreadableTask`
    - Uses `TaskEnvironment` for target path calculation

17. **ConvertToAbsolutePath** (`src/Tasks/ConvertToAbsolutePath.cs`)
    - Implements `IMultiThreadableTask`
    - Critical for path normalization in multithreaded scenarios

18. **FindUnderPath** (`src/Tasks/ListOperators/FindUnderPath.cs`)
    - Implements `IMultiThreadableTask`
    - Uses `TaskEnvironment` for path comparisons

#### Other Tasks
19. **Message** (`src/Tasks/Message.cs`)
    - Implements `IMultiThreadableTask`
    - Simple task, good example of minimal migration

20. **Hash** (`src/Tasks/Hash.cs`)
    - Implements `IMultiThreadableTask`

21. **WriteCodeFragment** (`src/Tasks/WriteCodeFragment.cs`)
    - Implements `IMultiThreadableTask`
    - Uses `TaskEnvironment` for output file path

22. **FindAppConfigFile** (`src/Tasks/FindAppConfigFile.cs`)
    - Implements `IMultiThreadableTask`

23. **GetFrameworkPath** (`src/Tasks/GetFrameworkPath.cs`)
    - Implements `IMultiThreadableTask`

24. **GenerateBootstrapper** (`src/Tasks/GenerateBootstrapper.cs`)
    - Implements `IMultiThreadableTask`

25. **RemoveDuplicates** (`src/Tasks/ListOperators/RemoveDuplicates.cs`)
    - Implements `IMultiThreadableTask`

26. **SetRidAgnosticValueForProjects** (`src/Tasks/SetRidAgnosticValueForProjects.cs`)
    - Implements `IMultiThreadableTask`

### Migration Pattern

All migrated tasks follow this pattern:

1. **Implement `IMultiThreadableTask` interface**
   ```csharp
   public class TaskName : TaskExtension, IMultiThreadableTask
   ```

2. **Add TaskEnvironment property**
   ```csharp
   public TaskEnvironment TaskEnvironment { get; set; }
   ```

3. **Replace direct file system access with TaskEnvironment methods:**
   - `Directory.GetCurrentDirectory()` → `TaskEnvironment.ProjectDirectory.Value`
   - `Path.GetFullPath(relativePath)` → `TaskEnvironment.GetAbsolutePath(relativePath)`
   - `Environment.GetEnvironmentVariable(name)` → `TaskEnvironment.GetEnvironmentVariable(name)`
   - `Environment.SetEnvironmentVariable(...)` → `TaskEnvironment.SetEnvironmentVariable(...)`

4. **Update helper classes and methods to accept TaskEnvironment**
   - Pass `TaskEnvironment` to constructors and methods that need path resolution
   - Example: `new FileState(path, TaskEnvironment)`

### Tasks Still Requiring Migration

The following high-priority tasks in the main codebase still need migration:

#### High Priority (Commonly Used)
- **Csc** (C# compilation) - Complex due to compiler interaction
- **Vbc** (VB compilation) - Similar complexity to Csc
- **ResGen** (Resource generation) - File I/O intensive
- **GenerateResource** - File I/O intensive
- **AL** (Assembly Linker) - Assembly generation
- **AspNetCompiler** - Web compilation
- **CreateCSharpManifestResourceName** - Resource naming
- **CreateVisualBasicManifestResourceName** - Resource naming

#### Medium Priority
- **CombinePath** - Path operations
- **GetFileHash** - File operations
- **Unzip** - File operations
- **ZipDirectory** - File operations
- **DownloadFile** - Network and file operations
- **Move** - File operations
- **RoslynCodeTaskFactory tasks** - May need special handling

#### Low Priority (Less Common)
- Various toolset-specific tasks
- Legacy tasks maintained for compatibility
- Third-party integration tasks

### Testing Requirements

For each migrated task, the following should be tested:

1. **Concurrent execution:** Multiple instances of the task running simultaneously
2. **Path resolution:** Correct working directory and path handling
3. **Environment variables:** Proper isolation of environment changes
4. **Backward compatibility:** Task still works in single-threaded mode
5. **Error handling:** Proper error messages in multithreaded scenarios

### Migration Checklist for Each Task

When migrating a task to the new API:

- [ ] Add `IMultiThreadableTask` interface implementation
- [ ] Add `TaskEnvironment` property
- [ ] Replace `Directory.GetCurrentDirectory()` calls
- [ ] Replace `Path.GetFullPath()` calls with relative path handling
- [ ] Replace `Environment.GetEnvironmentVariable()` calls
- [ ] Replace `Environment.SetEnvironmentVariable()` calls
- [ ] Update any helper classes/methods to accept `TaskEnvironment`
- [ ] Update file path construction to use `TaskEnvironment.GetAbsolutePath()`
- [ ] Add unit tests for concurrent execution
- [ ] Update task documentation
- [ ] Test in multithreaded build scenario
- [ ] Test backward compatibility in single-threaded mode

## Implementation Priority

Based on the analysis, the recommended implementation order is:

### Phase 1: Core Infrastructure (Critical)
1. Thread-static current directory support (NativeMethods, Modifiers, TaskBuilder)
2. MetadataLoadContext thread safety (TypeLoader)
3. TaskEnvironment change wave integration (RequestBuilder, TaskBuilder)

### Phase 2: Essential Task Support
4. TaskRouter enhancements
5. TaskBuilder.SetTaskEnvironment method
6. TaskExecutionHost environment handling
7. Intrinsic tasks multithreading support (MSBuild, CallTarget)

### Phase 3: Advanced Features
8. Well-known functions path resolution
9. SDK resolver path resolution
10. TaskHost parameter handling

### Phase 4: Task Migration
11. File I/O tasks (Copy, Delete, MakeDir, etc.)
12. Execution tasks (Exec, ToolTask)
13. Resolution tasks (RAR and related resolvers)
14. Build orchestration tasks (MSBuild, CallTarget)
15. Remaining utility tasks

## Related Work Items

This spec identifies the gaps between main and the multithreaded-prototype-rebased branch. The following areas need additional work not covered in this document:

1. **Thread node implementation:** The actual thread node provider and scheduler integration
2. **Sidecar TaskHost:** Long-lived TaskHost process management
3. **Performance testing:** Comprehensive benchmarking of multithreaded execution
4. **Visual Studio integration:** Handling of DisableInProcNode scenarios
5. **MSBuild Server integration:** Ensuring server mode works with multithreading

## Testing Strategy

Each fix and migration should include:

1. **Unit tests:** Verify the specific functionality works correctly
2. **Integration tests:** Test interaction with other components
3. **Concurrency tests:** Verify thread-safety under load
4. **Regression tests:** Ensure backward compatibility

## Conclusion

The multithreaded-prototype-rebased branch contains substantial work beyond the basic API definitions present in main. The core infrastructure fixes (thread-static directories, MetadataLoadContext thread safety, change wave integration) are essential prerequisites for any multithreaded execution. The task migrations demonstrate the viability of the API and provide a template for migrating additional tasks.

The work should be merged to main in phases, with each phase building on the previous one and including comprehensive testing to ensure no regressions in existing functionality.
