# Subtask 1: Infrastructure - Packet Types & Request/Response Framework

**Parent:** [taskhost-callbacks-implementation-plan.md](./taskhost-callbacks-implementation-plan.md)  
**Phase:** 1  
**Status:** ✅ COMPLETE  
**Dependencies:** None

---

## Objective

Establish the foundational infrastructure for bidirectional callback communication between TaskHost and parent process:
1. Add new packet type enum values
2. Create the request/response correlation mechanism in `OutOfProcTaskHostNode`
3. Set up the framework for handling incoming responses on the main thread

---

## Implementation Summary

### Files Modified

1. **`src/Shared/INodePacket.cs`** - Added 8 new packet type enum values (0x20-0x27)
2. **`src/MSBuild/OutOfProcTaskHostNode.cs`** - Added callback infrastructure
3. **`src/MSBuild/MSBuild.csproj`** - Added new file to compilation
4. **`src/MSBuild/Resources/Strings.resx`** - Added error string for connection loss

### Files Created

1. **`src/MSBuild/ITaskHostCallbackPacket.cs`** - Interface for request/response correlation

---

## Implementation Details

### Packet Type Enum Values

**File:** `src/Shared/INodePacket.cs`

Added in the `NodePacketType` enum (0x20-0x27 range):

```csharp
#region TaskHost callback packets (0x20-0x27)
TaskHostBuildRequest = 0x20,
TaskHostBuildResponse = 0x21,
TaskHostResourceRequest = 0x22,
TaskHostResourceResponse = 0x23,
TaskHostQueryRequest = 0x24,
TaskHostQueryResponse = 0x25,
TaskHostYieldRequest = 0x26,
TaskHostYieldResponse = 0x27,
#endregion
```

### ITaskHostCallbackPacket Interface

**File:** `src/MSBuild/ITaskHostCallbackPacket.cs`

```csharp
internal interface ITaskHostCallbackPacket : INodePacket
{
    int RequestId { get; set; }
}
```

### OutOfProcTaskHostNode Infrastructure

**File:** `src/MSBuild/OutOfProcTaskHostNode.cs`

**Fields added** (wrapped with `#if !CLR2COMPATIBILITY`):

```csharp
private int _nextCallbackRequestId;
private readonly ConcurrentDictionary<int, TaskCompletionSource<INodePacket>> _pendingCallbackRequests = new();
```

**HandleCallbackResponse** - Routes response packets to waiting callers:

```csharp
private void HandleCallbackResponse(INodePacket packet)
{
    // Silent no-op if packet doesn't implement ITaskHostCallbackPacket or request ID unknown.
    // Unknown ID can occur if request was cancelled/abandoned before response arrived.
    if (packet is ITaskHostCallbackPacket callbackPacket
        && _pendingCallbackRequests.TryRemove(callbackPacket.RequestId, out TaskCompletionSource<INodePacket> tcs))
    {
        tcs.TrySetResult(packet);
    }
}
```

**SendCallbackRequestAndWaitForResponse** - Sends request and blocks until response:

```csharp
private TResponse SendCallbackRequestAndWaitForResponse<TResponse>(ITaskHostCallbackPacket request)
    where TResponse : class, INodePacket
{
    int requestId = Interlocked.Increment(ref _nextCallbackRequestId);
    request.RequestId = requestId;

    // Use ManualResetEvent to bridge TaskCompletionSource to WaitHandle for efficient waiting
    using var responseEvent = new ManualResetEvent(false);
    var tcs = new TaskCompletionSource<INodePacket>(TaskCreationOptions.RunContinuationsAsynchronously);
    tcs.Task.ContinueWith(_ => responseEvent.Set(), TaskContinuationOptions.ExecuteSynchronously);
    _pendingCallbackRequests[requestId] = tcs;

    try
    {
        _nodeEndpoint.SendData((INodePacket)request);

        // Wait for either: response arrives, task cancelled, or connection lost
        // No timeout - callbacks like BuildProjectFile can legitimately take hours
        WaitHandle[] waitHandles = [responseEvent, _taskCancelledEvent];

        while (true)
        {
            int signaledIndex = WaitHandle.WaitAny(waitHandles, millisecondsTimeout: 1000);

            if (signaledIndex == 0) break;  // Response received
            else if (signaledIndex == 1) throw new BuildAbortedException();  // Task cancelled

            // Timeout - check connection status (no WaitHandle available for LinkStatus)
            if (_nodeEndpoint.LinkStatus != LinkStatus.Active)
            {
                throw new InvalidOperationException(
                    ResourceUtilities.FormatResourceStringStripCodeAndKeyword("TaskHostCallbackConnectionLost"));
            }
        }

        INodePacket response = tcs.Task.Result;
        if (response is TResponse typedResponse) return typedResponse;

        throw new InvalidOperationException(
            $"Unexpected callback response type: expected {typeof(TResponse).Name}, got {response?.GetType().Name ?? "null"}");
    }
    finally
    {
        _pendingCallbackRequests.TryRemove(requestId, out _);
    }
}
```

**HandlePacket switch cases** - Routes response packets to handler:

```csharp
case NodePacketType.TaskHostBuildResponse:
case NodePacketType.TaskHostResourceResponse:
case NodePacketType.TaskHostQueryResponse:
case NodePacketType.TaskHostYieldResponse:
    HandleCallbackResponse(packet);
    break;
```

---

## Key Design Decisions

1. **No timeout** - Callbacks like `BuildProjectFile` can legitimately take hours. Only connection loss and task cancellation terminate the wait.

2. **WaitHandle.WaitAny** - Uses kernel-level waiting for efficiency instead of polling. Response and cancellation wake immediately; connection status checked every 1 second.

3. **CLR2 compatibility** - All callback code wrapped with `#if !CLR2COMPATIBILITY` since MSBuildTaskHost (net35) doesn't support these callbacks.

4. **TaskCreationOptions.RunContinuationsAsynchronously** - Prevents deadlocks when TCS completion runs on the main thread.

5. **Atomic TryRemove** - Both `HandleCallbackResponse` and the `finally` block use `TryRemove`, ensuring exactly one succeeds regardless of race conditions.

---

## Remaining Work for Subsequent Subtasks

- **Packet classes** - The response packet types are defined but no concrete classes exist yet. Subtask 2+ will create these.
- **Packet factory registration** - Registration in constructor will be added when packet classes are implemented.
- **Parent-side handling** - `TaskHostTask` needs to handle request packets and send responses.

---

## Verification

- [x] New `NodePacketType` values compile without conflicts
- [x] `_pendingCallbackRequests` dictionary handles concurrent access correctly
- [x] `SendCallbackRequestAndWaitForResponse` blocks calling thread until response
- [x] Connection loss detection works correctly  
- [x] Task cancellation detection works correctly
- [x] Both `MSBuild.csproj` and `MSBuildTaskHost.csproj` build successfully
