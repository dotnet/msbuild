# Subtask 10: Yield/Reacquire Implementation

**Parent:** [taskhost-callbacks-implementation-plan.md](./taskhost-callbacks-implementation-plan.md)
**Phase:** 2
**Status:** ✅ Complete
**Dependencies:** Subtasks 5 (✅ Complete), 6 (✅ Complete)

---

## Objective

Implement `Yield()` and `Reacquire()` methods in TaskHost to allow tasks to temporarily release control, enabling the parent to schedule other work.

---

## Background

### Original Plan vs Actual Implementation

**CRITICAL CORRECTION:** The original plan had the semantics backwards!

- **Original plan (incorrect):** Yield() blocks, Reacquire() doesn't block
- **Correct behavior (implemented):** Yield() is non-blocking (fire-and-forget), Reacquire() blocks waiting for scheduler

### Correct Yield/Reacquire Semantics

From the in-process implementation in `TaskHost.cs` and `RequestBuilder.cs`:

1. **Yield() is non-blocking:**
   - Saves environment state
   - Sends yield notification to scheduler
   - Returns immediately (does NOT block)
   - Task can then do non-build work

2. **Reacquire() is blocking:**
   - Sends reacquire request to scheduler
   - Blocks until scheduler allows continuation
   - Restores environment state when unblocked
   - Then task continues with build work

### Flow Diagram

```
Task calls Yield()
    → TaskHost saves environment, decrements active count
    → TaskHost sends YieldRequest(Yield) - FIRE AND FORGET
    → Returns immediately to task

Task does non-build work...

Task calls Reacquire()
    → TaskHost sends YieldRequest(Reacquire) - BLOCKS HERE
    → Parent receives request, calls IBuildEngine3.Reacquire() (may block on scheduler)
    → When scheduler allows, parent sends YieldResponse
    → TaskHost receives response, increments active count, restores environment
    → Reacquire() returns

Task continues with build work...
```

---

## Implementation Completed

### New Files Created

#### `src/Shared/TaskHostYieldRequest.cs`

```csharp
internal enum YieldOperation
{
    Yield = 0,
    Reacquire = 1,
}

internal sealed class TaskHostYieldRequest : INodePacket, ITaskHostCallbackPacket
{
    private int _requestId;
    private int _taskId;
    private YieldOperation _operation;

    public TaskHostYieldRequest(int taskId, YieldOperation operation)
    {
        _taskId = taskId;
        _operation = operation;
    }

    public NodePacketType Type => NodePacketType.TaskHostYieldRequest;
    public int RequestId { get => _requestId; set => _requestId = value; }
    public int TaskId => _taskId;
    public YieldOperation Operation => _operation;
    // ... serialization
}
```

#### `src/Shared/TaskHostYieldResponse.cs`

```csharp
internal sealed class TaskHostYieldResponse : INodePacket, ITaskHostCallbackPacket
{
    private int _requestId;
    private bool _success;

    public TaskHostYieldResponse(int requestId, bool success)
    {
        _requestId = requestId;
        _success = success;
    }

    public NodePacketType Type => NodePacketType.TaskHostYieldResponse;
    public int RequestId { get => _requestId; set => _requestId = value; }
    public bool Success => _success;
    // ... serialization
}
```

**Note:** Only Reacquire gets a response. Yield is fire-and-forget.

### Modified Files

#### `src/MSBuild/OutOfProcTaskHostNode.cs`

**Yield() - Non-blocking:**
```csharp
public void Yield()
{
#if !CLR2COMPATIBILITY
    var context = GetCurrentTaskContext();
    if (context == null) return;
    if (context.State == TaskExecutionState.Yielded)
        throw new InvalidOperationException("Cannot call Yield() while already yielded.");

    SaveOperatingEnvironment(context);
    context.State = TaskExecutionState.Yielded;
    Interlocked.Decrement(ref _activeTaskCount);
    Interlocked.Increment(ref _yieldedTaskCount);

    var request = new TaskHostYieldRequest(context.TaskId, YieldOperation.Yield);
    _nodeEndpoint.SendData(request);
    // Returns immediately - no blocking!
#endif
}
```

**Reacquire() - Blocking:**
```csharp
public void Reacquire()
{
#if !CLR2COMPATIBILITY
    var context = GetCurrentTaskContext();
    if (context == null) return;
    if (context.State != TaskExecutionState.Yielded) return;

    var request = new TaskHostYieldRequest(context.TaskId, YieldOperation.Reacquire);
    var response = SendCallbackRequestAndWaitForResponse<TaskHostYieldResponse>(request);
    // ↑ Blocks here until parent responds

    Interlocked.Decrement(ref _yieldedTaskCount);
    Interlocked.Increment(ref _activeTaskCount);
    RestoreOperatingEnvironment(context);
    context.State = TaskExecutionState.Executing;
#endif
}
```

#### `src/Build/Instance/TaskFactories/TaskHostTask.cs`

**HandleYieldRequest:**
```csharp
private void HandleYieldRequest(TaskHostYieldRequest request)
{
    switch (request.Operation)
    {
        case YieldOperation.Yield:
            if (_buildEngine is IBuildEngine3 engine3)
                engine3.Yield();
            // No response - fire-and-forget
            break;

        case YieldOperation.Reacquire:
            if (_buildEngine is IBuildEngine3 engine3Reacquire)
                engine3Reacquire.Reacquire();
            // This may block if scheduler doesn't allow immediate reacquire
            var response = new TaskHostYieldResponse(request.RequestId, success: true);
            _taskHostProvider.SendData(_taskHostNodeId, response);
            break;
    }
}
```

### Packet Registration

Added to `NodePacketType` enum:
- `TaskHostYieldRequest`
- `TaskHostYieldResponse`

Registered handlers in:
- `OutOfProcTaskHostNode.cs` - receives YieldResponse
- `TaskHostTask.cs` - receives YieldRequest
- `NodeProviderOutOfProcTaskHost.cs` - routes YieldRequest

---

## Testing

### Unit Tests (7 tests)

**File:** `src/Build.UnitTests/BackEnd/TaskHostYieldPacket_Tests.cs`

- `TaskHostYieldRequest_Yield_RoundTrip_Serialization`
- `TaskHostYieldRequest_Reacquire_RoundTrip_Serialization`
- `TaskHostYieldRequest_DefaultRequestId_IsZero`
- `TaskHostYieldRequest_ImplementsITaskHostCallbackPacket`
- `TaskHostYieldResponse_RoundTrip_Serialization_Success`
- `TaskHostYieldResponse_RoundTrip_Serialization_Failure`
- `TaskHostYieldResponse_ImplementsITaskHostCallbackPacket`

### Integration Test (1 test)

**File:** `src/Build.UnitTests/BackEnd/TaskHostFactory_Tests.cs`

**Test:** `YieldReacquireCallbackWorksInTaskHost`

Uses `YieldReacquireTask` to verify:
- Task calls Yield() and Reacquire() successfully
- No exceptions thrown
- Task completes successfully

**Test Task:** `src/Build.UnitTests/BackEnd/YieldReacquireTask.cs`

```csharp
public class YieldReacquireTask : Task
{
    public override bool Execute()
    {
        if (BuildEngine is IBuildEngine3 engine3)
        {
            engine3.Yield();  // Non-blocking
            Thread.Sleep(100);  // Simulate non-build work
            engine3.Reacquire();  // Blocking
        }
        return true;
    }
}
```

### Test Results

All 52 callback-related tests pass on both net10.0 and net472:
```
Passed!  - Failed:     0, Passed:    52, Skipped:     0, Total:    52
```

---

## Verification Checklist

- [x] `TaskHostYieldRequest` packet serializes correctly
- [x] `TaskHostYieldResponse` packet serializes correctly
- [x] `Yield()` is non-blocking (fire-and-forget)
- [x] `Yield()` saves environment state
- [x] `Reacquire()` sends request and blocks
- [x] Response from parent unblocks Reacquire
- [x] `RestoreOperatingEnvironment` called after reacquire
- [x] Active/yielded task counts updated correctly
- [x] Unit tests pass
- [x] Integration test passes

---

## Notes

### Why Yield is Non-Blocking

The purpose of Yield is to let the task do non-build work (like I/O operations) without holding the scheduler slot. The task continues running - it just signals that it's not doing build work. The scheduler can then assign the slot to other tasks.

### Why Reacquire is Blocking

When a task wants to do build work again (like calling BuildProjectFile), it must wait for the scheduler to give it back a slot. The scheduler may be running other tasks and needs to coordinate when the task can resume build operations.

### Environment State

- `SaveOperatingEnvironment()` saves: current directory, environment variables
- `RestoreOperatingEnvironment()` restores them after reacquire
- This ensures tasks that run during yield don't pollute the original task's environment

### CLR2COMPATIBILITY

The implementation is wrapped in `#if !CLR2COMPATIBILITY` because:
1. .NET Framework 2.0 CLR doesn't have full async support
2. The callback infrastructure requires modern features
3. Legacy TaskHost can keep the no-op behavior
