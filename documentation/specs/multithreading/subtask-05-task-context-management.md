# Subtask 5: Infrastructure - Concurrent Task Context Management

**Parent:** [taskhost-callbacks-implementation-plan.md](./taskhost-callbacks-implementation-plan.md)
**Phase:** 2
**Status:** ✅ Complete
**Dependencies:** Subtasks 1-4 (✅ Complete)

---

## Objective

Implement the infrastructure to manage multiple concurrent task execution contexts in TaskHost. This is required because when a task yields or calls `BuildProjectFile`, the parent may send a new task to the same TaskHost process (to maintain static state sharing guarantees).

---

## Background

### Critical Invariant (from spec)

> All tasks within a single project that don't explicitly opt into their own private TaskHost must run in the **same process**. This is required because:
> 1. **Static state sharing** - Tasks may use static fields to share state
> 2. **`GetRegisteredTaskObject` API** - ~500 usages on GitHub storing databases, semaphores, and even Roslyn workspaces
> 3. **Object identity** - Tasks expect object references to remain valid across invocations

### Current Threading Model

```
Main thread (Run())          Task thread (_taskRunnerThread)
├─ WaitHandle.WaitAny()      └─ task.Execute()
├─ HandlePacket()                └─ IBuildEngine callbacks
└─ SendData()
```

### Target Threading Model

```
Main thread (Run())          Task threads
├─ WaitHandle.WaitAny()      ├─ TaskThread[0] (TaskA - yielded, blocked on TCS)
├─ HandlePacket()            ├─ TaskThread[1] (TaskB - executing)
├─ DispatchResponses()       └─ TaskThread[2] (TaskC - awaiting callback)
└─ SendData()
```

---

## Implementation Summary

### Files Modified

| File | Changes |
|------|---------|
| `src/MSBuild/TaskExecutionContext.cs` | **NEW** - Per-task execution context class |
| `src/MSBuild/MSBuild.csproj` | Added `TaskExecutionContext.cs` to compilation |
| `src/Shared/TaskHostConfiguration.cs` | Added `TaskId` property with serialization |
| `src/Build/Instance/TaskFactories/TaskHostTask.cs` | Added `s_nextTaskId` counter for unique task IDs |
| `src/MSBuild/OutOfProcTaskHostNode.cs` | Added context management infrastructure |
| `src/Build.UnitTests/BackEnd/TaskHostConfiguration_Tests.cs` | Added `TestTranslationWithTaskId` test |

---

## Implementation Details

### 1. TaskExecutionContext Class

**File:** `src/MSBuild/TaskExecutionContext.cs`

Encapsulates per-task state for concurrent execution:

```csharp
internal sealed class TaskExecutionContext : IDisposable
{
    // Core identification
    public int TaskId { get; }
    public TaskHostConfiguration Configuration { get; }

    // Execution state
    public Thread ExecutingThread { get; set; }
    public TaskExecutionState State { get; set; }

    // Environment preservation (for yield/reacquire)
    public string SavedCurrentDirectory { get; set; }
    public IDictionary<string, string> SavedEnvironment { get; set; }

    // Per-task pending callbacks (CLR2 excluded)
    public ConcurrentDictionary<int, TaskCompletionSource<INodePacket>> PendingCallbackRequests { get; }

    // Completion signaling
    public ManualResetEvent CompletedEvent { get; }
    public ManualResetEvent CancelledEvent { get; }
    public TaskHostTaskComplete ResultPacket { get; set; }
}

internal enum TaskExecutionState
{
    Pending,           // Created but not started
    Executing,         // Actively running
    Yielded,           // Waiting for Reacquire
    BlockedOnCallback, // Waiting for callback response
    Completed,         // Finished (success or failure)
    Cancelled          // Cancelled before/during execution
}
```

### 2. OutOfProcTaskHostNode Context Management

**File:** `src/MSBuild/OutOfProcTaskHostNode.cs`

Added fields:
```csharp
// All active task contexts, keyed by task ID
private readonly ConcurrentDictionary<int, TaskExecutionContext> _taskContexts
    = new ConcurrentDictionary<int, TaskExecutionContext>();

// Thread-isolated current context
private readonly AsyncLocal<TaskExecutionContext> _currentTaskContext
    = new AsyncLocal<TaskExecutionContext>();

// Local task ID counter (fallback when parent doesn't provide)
private int _nextLocalTaskId;
```

Added helper methods:
```csharp
// Get context for current thread (returns null if none - safe fallback)
private TaskExecutionContext GetCurrentTaskContext()
{
    return _currentTaskContext.Value;
}

// Create and register a new context
private TaskExecutionContext CreateTaskContext(TaskHostConfiguration configuration)
{
    int taskId = configuration.TaskId > 0
        ? configuration.TaskId
        : Interlocked.Increment(ref _nextLocalTaskId);

    var context = new TaskExecutionContext(taskId, configuration);

    if (!_taskContexts.TryAdd(taskId, context))
    {
        throw new InvalidOperationException($"Task ID {taskId} already exists");
    }

    return context;
}

// Clean up after task completion
private void RemoveTaskContext(int taskId)
{
    if (_taskContexts.TryRemove(taskId, out var context))
    {
        context.Dispose();
    }
}
```

### 3. Callback Response Routing

Updated `HandleCallbackResponse` to route responses to per-task pending requests:

```csharp
private void HandleCallbackResponse(INodePacket packet)
{
    if (packet is ITaskHostCallbackPacket callbackPacket)
    {
        int requestId = callbackPacket.RequestId;

        // Search per-task pending requests first
        foreach (var context in _taskContexts.Values)
        {
            if (context.PendingCallbackRequests.TryRemove(requestId, out var tcs))
            {
                tcs.TrySetResult(packet);
                return;
            }
        }

        // Fall back to global pending requests (single-task mode)
        if (_pendingCallbackRequests.TryRemove(requestId, out var globalTcs))
        {
            globalTcs.TrySetResult(packet);
        }
    }
}
```

### 4. Request ID Uniqueness

**Critical Design Decision:** Request IDs must be globally unique across all task contexts.

```csharp
// In SendCallbackRequestAndWaitForResponse:
// IMPORTANT: Request IDs must be globally unique across all task contexts
// to prevent collisions when multiple tasks are blocked simultaneously.
int requestId = Interlocked.Increment(ref _nextCallbackRequestId);
request.RequestId = requestId;

// Store in per-task dictionary (if available) or global
var context = GetCurrentTaskContext();
var pendingRequests = context?.PendingCallbackRequests ?? _pendingCallbackRequests;
pendingRequests[requestId] = tcs;
```

### 5. TaskId in TaskHostConfiguration

**File:** `src/Shared/TaskHostConfiguration.cs`

```csharp
private int _taskId;

public int TaskId
{
    [DebuggerStepThrough]
    get => _taskId;
    set => _taskId = value;
}

// In Translate():
translator.Translate(ref _taskId);
```

### 6. Parent-Side Task ID Generation

**File:** `src/Build/Instance/TaskFactories/TaskHostTask.cs`

```csharp
private static int s_nextTaskId;

// In Execute(), before sending configuration:
hostConfiguration.TaskId = Interlocked.Increment(ref s_nextTaskId);
```

---

## Testing

### Unit Tests Added

**File:** `src/Build.UnitTests/BackEnd/TaskHostConfiguration_Tests.cs`

```csharp
[Theory]
[InlineData(0)]
[InlineData(1)]
[InlineData(42)]
[InlineData(int.MaxValue)]
public void TestTranslationWithTaskId(int taskId)
{
    // Verifies TaskId survives serialization round-trip
}
```

### Test Results

All 37 tests pass on both .NET 10.0 and .NET Framework 4.7.2:
- 33 Phase 1 callback tests (from Subtask 4)
- 4 TaskId serialization tests (from Subtask 5)

---

## Design Decisions

### Why AsyncLocal<T> instead of ThreadLocal<T>?

Tasks may use `async/await` internally, which can switch threads. `AsyncLocal<T>` flows across async boundaries, ensuring the context remains accessible.

### Why global request IDs with per-task storage?

- **Global IDs**: Prevents collision when Task A (ID 1, request 1) and Task B (ID 2, request 1) are both blocked
- **Per-task storage**: Allows routing responses back to the correct task's waiting code

### Why O(n) search in HandleCallbackResponse?

The number of concurrent tasks (n) is typically small (<5). A more complex data structure (e.g., global dictionary with task ID in key) would add complexity without measurable benefit.

### Why no cleanup in HandleShutdown?

The TaskHost process is terminating - the OS will reclaim all resources. Adding explicit cleanup would be unnecessary complexity.

---

## Verification Checklist

- [x] `TaskExecutionContext` properly encapsulates task state
- [x] `_taskContexts` dictionary handles concurrent access correctly
- [x] `AsyncLocal<TaskExecutionContext>` provides thread-isolated context
- [x] Request IDs are globally unique (prevents collision)
- [x] Task IDs are unique across concurrent executions
- [x] Existing single-task execution still works (backward compatible)
- [x] TaskId serialization tested
- [x] All 37 tests pass

---

## Notes

- This subtask sets up infrastructure; actual concurrent execution is activated in subtasks 8-10
- The `SavedCurrentDirectory` and `SavedEnvironment` fields are populated in Subtask 6 (Environment State)
- The `Yielded` state is used in Subtask 10 (Yield/Reacquire)
- CLR2 compatibility is maintained via `#if !CLR2COMPATIBILITY` guards
