# Subtask 2: Simple Callbacks - IsRunningMultipleNodes

**Parent:** [taskhost-callbacks-implementation-plan.md](./taskhost-callbacks-implementation-plan.md)  
**Phase:** 1  
**Status:** ✅ COMPLETE  
**Dependencies:** Subtask 1 (✅ Complete)

---

## Summary

This subtask implemented the `IsRunningMultipleNodes` property callback from TaskHost to parent process. It was the first callback to use the infrastructure from Subtask 1, validating the request/response pattern works correctly.

---

## What Was Implemented

### Files Created

| File | Purpose |
|------|---------|
| `src/Shared/TaskHostQueryRequest.cs` | Request packet for boolean queries |
| `src/Shared/TaskHostQueryResponse.cs` | Response packet with boolean result |
| `src/Build.UnitTests/BackEnd/TaskHostQueryPacket_Tests.cs` | Unit tests for packet serialization |

### Files Modified

| File | Changes |
|------|---------|
| `src/Shared/INodePacket.cs` | Added `TaskHostQueryRequest` and `TaskHostQueryResponse` packet types |
| `src/MSBuild/OutOfProcTaskHostNode.cs` | Updated `IsRunningMultipleNodes` to use callback infrastructure |
| `src/MSBuild/MSBuild.csproj` | Added references to new shared packet files |
| `src/Build/Microsoft.Build.csproj` | Added references to new shared packet files |
| `src/Build/Instance/TaskFactories/TaskHostTask.cs` | Added handler for `TaskHostQueryRequest` |

---

## Implementation Details

### Communication Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│ TaskHost Process                                                    │
│                                                                     │
│  Task calls IBuildEngine2.IsRunningMultipleNodes                    │
│    ↓                                                                │
│  OutOfProcTaskHostNode.IsRunningMultipleNodes                       │
│    ↓                                                                │
│  SendCallbackRequestAndWaitForResponse<TaskHostQueryResponse>()     │
│    ↓ (sends TaskHostQueryRequest, blocks on ManualResetEvent)       │
│                                                                     │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ Named Pipe
                            ↓
┌───────────────────────────┴─────────────────────────────────────────┐
│ Parent Process                                                       │
│                                                                     │
│  TaskHostTask.HandlePacket()                                        │
│    ↓                                                                │
│  HandleQueryRequest(TaskHostQueryRequest)                           │
│    ↓                                                                │
│  _buildEngine.IsRunningMultipleNodes → get actual value             │
│    ↓                                                                │
│  SendData(new TaskHostQueryResponse(requestId, result))             │
│                                                                     │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ Named Pipe
                            ↓
┌───────────────────────────┴─────────────────────────────────────────┐
│ TaskHost Process                                                    │
│                                                                     │
│  HandleCallbackResponse() signals ManualResetEvent                  │
│    ↓                                                                │
│  SendCallbackRequestAndWaitForResponse() returns response           │
│    ↓                                                                │
│  IsRunningMultipleNodes returns response.BoolResult                 │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### TaskHost Side (OutOfProcTaskHostNode.cs)

```csharp
public bool IsRunningMultipleNodes
{
    get
    {
#if CLR2COMPATIBILITY
        LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
        return false;
#else
        var request = new TaskHostQueryRequest(TaskHostQueryRequest.QueryType.IsRunningMultipleNodes);
        var response = SendCallbackRequestAndWaitForResponse<TaskHostQueryResponse>(request);
        return response.BoolResult;
#endif
    }
}
```

### Parent Side (TaskHostTask.cs)

```csharp
private void HandleQueryRequest(TaskHostQueryRequest request)
{
    bool result = request.Query switch
    {
        TaskHostQueryRequest.QueryType.IsRunningMultipleNodes 
            => _buildEngine is IBuildEngine2 engine2 && engine2.IsRunningMultipleNodes,
        _ => false
    };

    var response = new TaskHostQueryResponse(request.RequestId, result);
    _taskHostProvider.SendData(_taskHostNodeId, response);
}
```

---

## Extensibility

The `TaskHostQueryRequest.QueryType` enum can be extended for additional boolean queries:

```csharp
internal enum QueryType
{
    IsRunningMultipleNodes = 0,
    // Future: other boolean queries can be added here
}
```

---

## Validation

### Build Verification

```cmd
.\build.cmd -v quiet
artifacts\msbuild-build-env.bat
dotnet build src/Samples/Dependency/Dependency.csproj
```

### Unit Tests

```cmd
dotnet test src/Build.UnitTests/Microsoft.Build.Engine.UnitTests.csproj --filter "FullyQualifiedName~TaskHostQueryPacket"
```

### End-to-End Validation

Tested with a custom task that calls `IsRunningMultipleNodes` in out-of-proc mode:

1. **Baseline (msb2 - before changes):** Task fails with "BuildEngineCallbacksInTaskHostUnsupported" error
2. **After changes (msb1):** Task succeeds, returns correct boolean value from parent

---

## CLR2 Compatibility

The implementation is guarded with `#if !CLR2COMPATIBILITY`:
- MSBuildTaskHost (CLR2/.NET Framework 3.5) continues to use the old stub behavior
- Modern TaskHost uses the new callback mechanism
- All new packet classes are wrapped with the same guard
