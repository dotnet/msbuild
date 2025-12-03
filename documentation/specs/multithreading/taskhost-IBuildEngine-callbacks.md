# Design Specification: TaskHost IBuildEngine Callback Support

**Status:** Draft | **Related Issue:** #12863

---

## 1. Problem Statement

The MSBuild TaskHost (`OutOfProcTaskHostNode`) implements `IBuildEngine10` but lacks support for several callbacks. TaskHost is used when:
1. **`MSBUILDFORCEALLTASKSOUTOFPROC=1`** with `-mt` mode - forces non-thread-safe tasks out-of-proc
2. **Explicit `TaskHostFactory`** in `<UsingTask>` declarations

If tasks in TaskHost call unsupported callbacks, the build fails with MSB5022 or `NotImplementedException`.

**Note:** This is an infrequent scenario - a compatibility layer for multithreaded MSBuild, not a hot path.

### Unsupported Callbacks

| Callback | Interface | Current Behavior |
|----------|-----------|------------------|
| `IsRunningMultipleNodes` | IBuildEngine2 | Logs MSB5022, returns `false` |
| `BuildProjectFile` (4 params) | IBuildEngine | Logs MSB5022, returns `false` |
| `BuildProjectFile` (5 params) | IBuildEngine2 | Logs MSB5022, returns `false` |
| `BuildProjectFilesInParallel` (7 params) | IBuildEngine2 | Logs MSB5022, returns `false` |
| `BuildProjectFilesInParallel` (6 params) | IBuildEngine3 | Logs MSB5022, returns `false` |
| `Yield` / `Reacquire` | IBuildEngine3 | Silent no-op |
| `RequestCores` / `ReleaseCores` | IBuildEngine9 | Throws `NotImplementedException` |

**Evidence:** src/MSBuild/OutOfProcTaskHostNode.cs lines 270-405

---

## 2. Goals

1. **Full IBuildEngine support in TaskHost** - Tasks work identically whether in-proc or in TaskHost
2. **Backward compatibility** - Existing behavior unchanged for tasks that don't use callbacks
3. **Acceptable performance** - IPC overhead tolerable for typical callback patterns
4. **Support multithreaded builds** - Unblock `-mt` for real-world projects

**Non-Goal:** CLR2/net35 `MSBuildTaskHost.exe` support (never had callback support)

---

## 3. Architecture

### Current Communication Flow

```text
PARENT MSBuild                           TASKHOST Process
┌─────────────┐                         ┌───────────────────────────┐
│ TaskHostTask│──TaskHostConfiguration─▶│ OutOfProcTaskHostNode     │
│             │                         │   └─_taskRunnerThread     │
│             │◀──LogMessagePacket──────│       └─Task.Execute()    │
│             │◀──TaskHostTaskComplete──│                           │
└─────────────┘                         └───────────────────────────┘
```

**Key files:**

- src/Build/Instance/TaskFactories/TaskHostTask.cs - Parent side
- src/MSBuild/OutOfProcTaskHostNode.cs - TaskHost side

### Proposed: Bidirectional Callback Forwarding

```text
PARENT MSBuild                           TASKHOST Process
┌─────────────┐                         ┌───────────────────────────┐
│ TaskHostTask│──TaskHostConfiguration─▶│ OutOfProcTaskHostNode     │
│             │                         │   └─_taskRunnerThread     │
│             │◀──LogMessagePacket──────│       │                   │
│             │◀─CallbackRequest────────│       ├─task.Execute()    │
│             │                         │       │  └─BuildProject() │
│             │──CallbackResponse──────▶│       │      [blocks]     │
│             │                         │       │                   │
│             │◀──TaskHostTaskComplete──│       └─[unblocks]        │
└─────────────┘                         └───────────────────────────┘
```

---

## 4. Design

### 4.1 Threading Model

**Critical constraint:** TaskHost has two threads:

- **Main thread** (`Run()`) - handles IPC via `WaitHandle.WaitAny()` loop
- **Task thread** (`_taskRunnerThread`) - executes `task.Execute()`

Callbacks are invoked from the task thread but responses arrive on the main thread.

**Solution:** Use `TaskCompletionSource<INodePacket>` per request:

1. Task thread creates request, registers TCS in `_pendingRequests[requestId]`
2. Task thread sends packet, calls `tcs.Task.Wait()`
3. Main thread receives response, calls `tcs.SetResult(packet)` to unblock task thread

### 4.2 New Packet Types

| Packet | Direction | Purpose |
|--------|-----------|---------|
| `TaskHostBuildRequest` | TaskHost → Parent | BuildProjectFile* calls |
| `TaskHostBuildResponse` | Parent → TaskHost | Build results + outputs |
| `TaskHostResourceRequest` | TaskHost → Parent | RequestCores/ReleaseCores |
| `TaskHostResourceResponse` | Parent → TaskHost | Cores granted |
| `TaskHostQueryRequest` | TaskHost → Parent | IsRunningMultipleNodes |
| `TaskHostQueryResponse` | Parent → TaskHost | Query result |
| `TaskHostYieldRequest` | TaskHost → Parent | Yield/Reacquire |
| `TaskHostYieldResponse` | Parent → TaskHost | Acknowledgment |

**Location:** `src/MSBuild/` (linked into Microsoft.Build.csproj). NOT in `src/Shared/` since MSBuildTaskHost (CLR2) is out of scope.

### 4.3 INodePacket.cs Changes

```csharp
public enum NodePacketType : byte
{
    // ... existing ...
    TaskHostBuildRequest = 0x20,
    TaskHostBuildResponse = 0x21,
    TaskHostResourceRequest = 0x22,
    TaskHostResourceResponse = 0x23,
    TaskHostQueryRequest = 0x24,
    TaskHostQueryResponse = 0x25,
    TaskHostYieldRequest = 0x26,
    TaskHostYieldResponse = 0x27,
}
```

### 4.4 Key Implementation Points

**OutOfProcTaskHostNode (TaskHost side):**

- Add `ConcurrentDictionary<int, TaskCompletionSource<INodePacket>> _pendingRequests`
- Add `SendRequestAndWaitForResponse<TRequest, TResponse>()` helper
- Replace stub implementations with forwarding calls
- Add response handling in `HandlePacket()`

**TaskHostTask (Parent side):**

- Register handlers for new request packet types
- Add `HandleBuildRequest()`, `HandleResourceRequest()`, etc.
- Forward to real `IBuildEngine` and send response

---

## 5. ITaskItem Serialization

`TaskHostBuildResponse.TargetOutputsPerProject` contains `IDictionary<string, ITaskItem[]>` per project.

**Existing pattern:** `TaskParameter` class handles `ITaskItem` serialization for `TaskHostTaskComplete`. Use same approach.

**Reference:** src/Shared/TaskParameter.cs

---

## 6. Phased Rollout

| Phase | Scope | Risk | Effort |
|-------|-------|------|--------|
| 1 | `IsRunningMultipleNodes` | Low | 2 days |
| 2 | `RequestCores`/`ReleaseCores` | Medium | 3 days |
| 3 | `Yield`/`Reacquire` | Medium | 3 days |
| 4 | `BuildProjectFile*` | High | 5-7 days |

**Rationale:** Phase 1 validates the forwarding infrastructure with minimal risk. Phase 4 is highest risk due to complex `ITaskItem[]` serialization and recursive build scenarios.

---

## 7. Open Questions for Review

### Q1: Yield semantics in TaskHost

Current no-op may be intentional - TaskHost is single-threaded per process. Options:

- A) Forward to parent and actually yield (allows scheduler to run other work)
- B) Keep as no-op (current behavior, safest)

**Recommendation:** (B) initially - Yield/Reacquire are rarely used by tasks, and current no-op behavior has shipped. Revisit if real-world need arises.

### Q2: Error handling for parent crash during callback

If parent dies while TaskHost awaits response:

- A) Timeout and fail task
- B) Detect pipe closure immediately and fail
- C) Both

**Recommendation:** (C) - `_nodeEndpoint.LinkStatus` check + timeout

---

## 8. Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Deadlock in callback wait | Low | High | Timeouts, no lock held during wait, main thread never waits on task thread |
| IPC serialization bugs | Medium | Medium | Packet round-trip unit tests |

**Note:** No "breaking existing behavior" risk - callbacks currently fail/throw, so any implementation is an improvement.

---

## 9. Testing Strategy

### Unit Tests

- Packet serialization round-trip
- Request-response correlation
- Timeout handling
- Cancellation during callback

### Integration Tests

- End-to-end `-mt` build with callback-using task
- TaskHost reuse across multiple tasks
- Recursive `BuildProjectFile` scenarios

### Stress Tests

- Many concurrent callbacks
- Large `ITaskItem[]` outputs

---

## 10. File Change Summary

| File | Change |
|------|--------|
| `src/Shared/INodePacket.cs` | Add enum values |
| `src/MSBuild/TaskHostBuildRequest.cs` | New (link to Microsoft.Build.csproj) |
| `src/MSBuild/TaskHostBuildResponse.cs` | New (link to Microsoft.Build.csproj) |
| `src/MSBuild/TaskHostResourceRequest.cs` | New (link to Microsoft.Build.csproj) |
| `src/MSBuild/TaskHostResourceResponse.cs` | New (link to Microsoft.Build.csproj) |
| `src/MSBuild/TaskHostQueryRequest.cs` | New (link to Microsoft.Build.csproj) |
| `src/MSBuild/TaskHostQueryResponse.cs` | New (link to Microsoft.Build.csproj) |
| `src/MSBuild/TaskHostYieldRequest.cs` | New (link to Microsoft.Build.csproj) |
| `src/MSBuild/TaskHostYieldResponse.cs` | New (link to Microsoft.Build.csproj) |
| `src/MSBuild/OutOfProcTaskHostNode.cs` | Implement forwarding |
| `src/Build/Instance/TaskFactories/TaskHostTask.cs` | Handle requests |

---

## Appendix: References

- Current stub implementations: src/MSBuild/OutOfProcTaskHostNode.cs lines 270-405
- Existing packet serialization: src/Shared/TaskParameter.cs
- TaskHost message loop: src/MSBuild/OutOfProcTaskHostNode.cs lines 650-710
- Parent message loop: src/Build/Instance/TaskFactories/TaskHostTask.cs lines 270-320
