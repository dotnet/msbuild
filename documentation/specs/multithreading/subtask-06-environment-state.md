# Subtask 6: Environment State Save/Restore

**Parent:** [taskhost-callbacks-implementation-plan.md](./taskhost-callbacks-implementation-plan.md)
**Phase:** 2
**Status:** ✅ Complete
**Dependencies:** Subtask 5 (Task Context Management)

---

## Objective

Implement environment state (current directory and environment variables) save/restore functionality for TaskHost. This is required when:
1. A task yields (subtask 10)
2. A task calls `BuildProjectFile*` and blocks waiting for results (subtask 8)
3. A new task starts while another is yielded/blocked

---

## Background

### When Is Environment Save/Restore Needed?

| Callback | Blocks Task? | Another Task Can Run? | Save/Restore Needed? |
|----------|--------------|----------------------|---------------------|
| `IsRunningMultipleNodes` | Brief | No | No |
| `RequestCores`/`ReleaseCores` | Brief | No | No |
| `BuildProjectFile` | Long | Yes | **Yes** |
| `Yield`/`Reacquire` | Long | Yes | **Yes** |

For Phase 1 callbacks (subtasks 1-4), no save/restore is needed because the task doesn't truly yield control. This subtask implements the **infrastructure** that will be used in subtasks 8 and 10.

### Existing Utilities

The codebase already has the utilities we need:

- `CommunicationsUtilities.GetEnvironmentVariables()` - Returns environment as `FrozenDictionary<string, string>`
- `CommunicationsUtilities.SetEnvironment(IDictionary<string, string>)` - Restores environment (clears extras, updates changed)
- `NativeMethodsShared.GetCurrentDirectory()` - Gets current directory
- `NativeMethodsShared.SetCurrentDirectory(string)` - Sets current directory

### TaskExecutionContext Fields (from Subtask 5)

```csharp
public string SavedCurrentDirectory { get; set; }
public IDictionary<string, string> SavedEnvironment { get; set; }
```

---

## Implementation Summary

### Files Modified

| File | Changes |
|------|---------|
| `src/MSBuild/OutOfProcTaskHostNode.cs` | Added `SaveOperatingEnvironment` and `RestoreOperatingEnvironment` methods |

---

## Implementation Details

### Helper Methods in OutOfProcTaskHostNode

```csharp
/// <summary>
/// Saves the current operating environment to the task context.
/// Called before yielding or blocking on a callback that allows other tasks to run.
/// </summary>
/// <param name="context">The task context to save environment into.</param>
private void SaveOperatingEnvironment(TaskExecutionContext context)
{
    context.SavedCurrentDirectory = NativeMethodsShared.GetCurrentDirectory();
    // Create a mutable copy since FrozenDictionary is immutable
    context.SavedEnvironment = new Dictionary<string, string>(
        CommunicationsUtilities.GetEnvironmentVariables(),
        StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Restores the previously saved operating environment from the task context.
/// Called when resuming after yield or callback completion.
/// </summary>
/// <param name="context">The task context to restore environment from.</param>
private void RestoreOperatingEnvironment(TaskExecutionContext context)
{
    ErrorUtilities.VerifyThrow(
        context.SavedCurrentDirectory != null,
        "Current directory not previously saved for task {0}",
        context.TaskId);
    ErrorUtilities.VerifyThrow(
        context.SavedEnvironment != null,
        "Environment variables not previously saved for task {0}",
        context.TaskId);

    // Restore environment variables (handles clearing removed vars)
    CommunicationsUtilities.SetEnvironment(context.SavedEnvironment);

    // Restore current directory
    NativeMethodsShared.SetCurrentDirectory(context.SavedCurrentDirectory);

    // Clear saved state (no longer needed, prevents accidental double-restore)
    context.SavedCurrentDirectory = null;
    context.SavedEnvironment = null;
}
```

### Usage Pattern (for future subtasks)

These methods will be called in subtask 8 (BuildProjectFile) and subtask 10 (Yield/Reacquire):

```csharp
// Before blocking on a callback that allows other tasks:
SaveOperatingEnvironment(context);
context.State = TaskExecutionState.BlockedOnCallback; // or Yielded

// ... wait for response / reacquire ...

// After resuming:
RestoreOperatingEnvironment(context);
context.State = TaskExecutionState.Executing;
```

---

## Testing

### Current State

The `SaveOperatingEnvironment` and `RestoreOperatingEnvironment` methods are **not directly tested** in this subtask. They are infrastructure methods that will only be called when:
- Subtask 8 implements `BuildProjectFile` callbacks (task blocks, another task may run)
- Subtask 10 implements `Yield`/`Reacquire` (explicit yield)

### Underlying Utilities Are Tested

The utilities these methods use are already tested:
- `CommunicationsUtilities.SetEnvironment` - tested in `src/Shared/UnitTests/CommunicationUtilities_Tests.cs` (`RestoreEnvVars` test)
- `CommunicationsUtilities.GetEnvironmentVariables` - tested in same file (`GetEnvVars` test)

### Integration Testing

Real testing of environment save/restore will happen in subtask 8/10 when we have integration tests that:
1. Run a task in TaskHost
2. Have the task call `BuildProjectFile` (blocking)
3. Verify environment is restored when task resumes

### Test Results

All 31 tests from previous subtasks pass on both .NET 10.0 and .NET Framework 4.7.2:
- 25 unit tests (packet serialization, interface implementation)
- 6 integration tests (real TaskHost execution)

---

## Design Decisions

### Why Create a Copy of Environment Variables?

`CommunicationsUtilities.GetEnvironmentVariables()` returns a `FrozenDictionary` which may be cached and shared. We create a `Dictionary<string, string>` copy to:
1. Ensure we have a snapshot at save time (defensive copy)
2. Allow the `SetEnvironment` method to iterate over it

### Why Clear Saved State After Restore?

Setting `SavedCurrentDirectory` and `SavedEnvironment` to null after restore:
1. Prevents accidental double-restore
2. Makes debugging easier (can see if state was restored)
3. Allows GC to reclaim the dictionary memory

### Why Not Modify SendCallbackRequestAndWaitForResponse Now?

The current Phase 1 callbacks (`IsRunningMultipleNodes`, `RequestCores`, `ReleaseCores`) don't require environment save/restore because:
1. They complete quickly (no other task can start)
2. The task doesn't yield or block for extended periods
3. Save/restore would add unnecessary overhead

Environment save/restore will be integrated when implementing:
- Subtask 8: `BuildProjectFile` (task blocks, another task may run)
- Subtask 10: `Yield`/`Reacquire` (explicit yield)

---

## Verification Checklist

- [x] `SaveOperatingEnvironment` captures current directory
- [x] `SaveOperatingEnvironment` captures all environment variables as copy
- [x] `RestoreOperatingEnvironment` restores current directory
- [x] `RestoreOperatingEnvironment` clears variables not in saved set
- [x] `RestoreOperatingEnvironment` updates changed variables
- [x] `RestoreOperatingEnvironment` throws if not previously saved
- [x] Saved state is cleared after restore
- [x] Unit tests pass
- [x] Full build passes

---

## Notes

- This subtask implements infrastructure only; integration happens in subtasks 8 and 10
- Uses `StringComparer.OrdinalIgnoreCase` for environment variable names (Windows standard)
- The existing `_savedEnvironment` field is for process-level original environment (restored on shutdown); per-task `SavedEnvironment` is different
