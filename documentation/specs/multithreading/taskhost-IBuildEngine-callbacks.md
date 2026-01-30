# Design Specification: TaskHost IBuildEngine Callback Support

**Status:** Draft (Reviewed 2024-12) | **Related Issue:** #12863

---

## 1. Problem Statement

The MSBuild TaskHost (`OutOfProcTaskHostNode`) implements `IBuildEngine10` but lacks support for several callbacks. 

TaskHost is used when:
1. `MSBUILDFORCEALLTASKSOUTOFPROC=1`
2. or `-mt` mode - forces non-thread-safe tasks out-of-proc
3. **Explicit `TaskHostFactory`** in `<UsingTask>` declarations - we don't care about this scenario

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
4. **Support multithreaded builds** - Unblock `-mt` for real-world projects such as WPF

**Non-Goal:** CLR2/net35 `MSBuildTaskHost.exe` support (never had this callback support)

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

### Proposed: Bidirectional Callback Forwarding and Yielding logic

```text
PARENT MSBuild (Worker Node)              TASKHOST Process
┌──────────────────────┐                 ┌─────────────────────────────────────┐
│ TaskHostTask         │                 │ OutOfProcTaskHostNode               │
│                      │                 │   └─Main thread (packet dispatch)   │
│                      │──TaskHostCfg───▶│       │                             │
│                      │                 │       ├─TaskThread[0] ──────────────┤
│                      │◀──LogMessage────│       │  └─TaskA.Execute()          │
│                      │                 │       │                             │
│                      │◀─YieldRequest───│       │     ┌─Yield()               │
│   [marks yielded]    │                 │       │     │  [blocks on TCS]      │
│                      │                 │       │     ▼                       │
│                      │──NewTaskCfg────▶│       ├─TaskThread[1] ──────────────┤
│                      │                 │       │  └─TaskB.Execute()          │
│                      │◀──TaskBComplete─│       │     [completes]             │
│                      │                 │       │                             │
│                      │──ReacquireAck──▶│       ├─TaskThread[0] ──────────────┤
│                      │                 │       │     [unblocks]              │
│                      │                 │       │     └─continues TaskA       │
│                      │◀──TaskAComplete─│       │                             │
└──────────────────────┘                 └─────────────────────────────────────┘

Yield/Reacquire Flow:
  1. TaskA calls Yield() → sends YieldRequest to parent
  2. Parent marks request as yielded, schedules other work
  3. Parent may send NewTaskConfiguration to same TaskHost
  4. TaskHost spawns new thread for TaskB (TaskA's thread blocked)
  5. TaskB completes → TaskHostTaskComplete sent
  6. When ready, parent sends ReacquireAck → TaskA's thread unblocks
  7. TaskA continues and eventually completes

BuildProjectFile Flow (similar):
  1. TaskA calls BuildProjectFile() → sends BuildRequest to parent
  2. Parent forwards to scheduler, may assign work back to this TaskHost
  3. TaskHost manages concurrent execution on separate threads
  4. Build result returned → TaskA's thread unblocks
```

---

## 4. Design

### 4.1 Threading Model

**Critical constraint:** TaskHost has two threads:

- **Main thread** (`Run()`) - handles IPC via `WaitHandle.WaitAny()` loop
- **Task thread** (`_taskRunnerThread`) - executes `task.Execute()`

Callbacks are invoked from the task thread but responses arrive on the main thread.
There may exist multiple concurrent tasks in TaskHost, each on its own thread when some are yielded/blocked by callbacks.

**Critical invariant (confirmed in review):** All tasks within a single project that don't explicitly opt into their own private TaskHost must run in the **same process**. This is required because:

1. **Static state sharing** - Tasks may use static fields to share state (e.g., caches of parsed file contents)
2. **`GetRegisteredTaskObject` API** - ~500 usages on GitHub storing databases, semaphores, and even Roslyn workspaces
3. **Object identity** - Tasks expect object references to remain valid across invocations

This means TaskHost must support **concurrent task execution** within a single process when tasks yield or call `BuildProjectFile*`. Spawning new TaskHost processes per yielded task would break these invariants.

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

**ITaskItem Serialization:** `TaskHostBuildResponse` will contain `IDictionary<string, ITaskItem[]>` - reuse existing `TaskParameter` class pattern from `src/Shared/TaskParameter.cs`.

### 4.3 INodePacket.cs Changes

```csharp
public enum NodePacketType : byte
{
    // ... existing (0x00-0x15 in use, 0x3C-0x3F reserved for ServerNode) ...
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

**Note:** The enum uses `TypeMask = 0x3F` (6 bits for type, max 64 values) and `ExtendedHeaderFlag = 0x40`. Values 0x16-0x3B are available; 0x20-0x27 is a safe range.

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

## 5. Environment and Working Directory State Management

When TaskHost manages multiple concurrent tasks (due to yields or BuildProjectFile calls), each task context must maintain its own environment state.

### State to Save/Restore Per Task Context

| State | Save Point | Restore Point |
|-------|------------|---------------|
| Working directory (`Environment.CurrentDirectory`) | Before yielding or starting new task | On reacquire or resuming task |
| Environment variables | Before yielding or starting new task | On reacquire or resuming task |

### Implementation Approach

**When a task yields or calls BuildProjectFile:**
1. Capture `Environment.CurrentDirectory`
2. Capture `Environment.GetEnvironmentVariables()`
3. Store in task's `TaskExecutionContext`
4. Block task thread on `TaskCompletionSource`

**When starting a new task on yielded TaskHost:**
1. Apply new task's environment from `TaskHostConfiguration` (already contains `BuildProcessEnvironment` and `StartupDirectory`)
2. Set working directory from configuration
3. Execute task on new thread

**When task reacquires or BuildProjectFile returns:**
1. Restore saved `CurrentDirectory`
2. Restore saved environment variables (clear current, set saved)
3. Unblock task thread via `TaskCompletionSource.SetResult()`

### Important Notes

- **This is existing behavior** - environment changes during yield have always been possible. This is documented, not a new breaking change.
- **Environment restore must be atomic** with respect to the task thread resuming
- **Static state is NOT saved/restored** - tasks sharing static fields across yields is their responsibility to manage
- **Existing implementation in worker nodes** - Normal multiprocess MSBuild worker nodes already implement this exact yielding and state saving logic. See open question Q4 about reusability.

---

## 6. Phased Rollout

| Phase | Scope | Risk | Effort |
|-------|-------|------|--------|
| 1 | `IsRunningMultipleNodes`, `RequestCores`/`ReleaseCores` | Low | 1 day |
| 2 | `BuildProjectFile*` + `Yield`/`Reacquire` | High | 7-10 days |

**Rationale:** 
- Phase 1 is trivial: `IsRunningMultipleNodes` can just return `true`, `RequestCores`/`ReleaseCores` can be no-ops. These are not critical for correctness.
- Phase 2 combines `BuildProjectFile*` and `Yield`/`Reacquire` because they use a similar approach and Yield "comes almost for free" once BuildProjectFile is implemented

**Note:** Phase 2 is highest complexity due to:
- Complex `ITaskItem[]` serialization
- Recursive build scenarios  
- Concurrent task management within single TaskHost process
- Environment/CWD state save/restore per task context

---

## 7. Open Questions

### Q1: Error handling for parent crash during callback

If parent dies while TaskHost awaits response:

- A) Timeout and fail task
- B) Detect pipe closure immediately and fail
- C) Both

**Recommendation:** (C) - `_nodeEndpoint.LinkStatus` check + timeout

### Q2: Reuse of existing worker node yield/state-save logic

Normal multiprocess MSBuild worker nodes already implement yielding and environment/CWD state saving logic. Can this be reused for TaskHost?

**Existing implementation location:** `src/Build/BackEnd/Components/RequestBuilder/RequestBuilder.cs`

```csharp
// SaveOperatingEnvironment() - captures state before yield
private void SaveOperatingEnvironment()
{
    if (_componentHost.BuildParameters.SaveOperatingEnvironment)
    {
        _requestEntry.RequestConfiguration.SavedCurrentDirectory = NativeMethodsShared.GetCurrentDirectory();
        _requestEntry.RequestConfiguration.SavedEnvironmentVariables = CommunicationsUtilities.GetEnvironmentVariables();
    }
}

// RestoreOperatingEnvironment() - restores state on reacquire
private void RestoreOperatingEnvironment()
{
    if (_componentHost.BuildParameters.SaveOperatingEnvironment)
    {
        SetEnvironmentVariableBlock(_requestEntry.RequestConfiguration.SavedEnvironmentVariables);
        NativeMethodsShared.SetCurrentDirectory(_requestEntry.RequestConfiguration.SavedCurrentDirectory);
    }
}
```

**Reusability assessment:**
- `NativeMethodsShared` and `CommunicationsUtilities` are already in `src/Shared/` - **can reuse**
- `SetEnvironmentVariableBlock()` is private to `RequestBuilder` - **need to extract or duplicate**
- TaskHost already has similar env-setting logic in `OutOfProcTaskHostNode.SetTaskHostEnvironment()` - patterns match

**Recommendation:** Extract shared utilities for environment save/restore, or duplicate the ~20 lines of logic in TaskHost.

---

## 8. Key Decisions (from 2024-12 Review)

| Decision | Rationale |
|----------|------------|
| Implement callbacks (not migrate tasks) | We'd "own" any tasks we touch; doesn't help 3rd party |
| Implement Yield/Reacquire | Significantly improved VMR build times; comes free with BuildProjectFile |
| Single TaskHost process (not spawn on yield) | Breaks static state sharing and `GetRegisteredTaskObject` guarantees |
| Defer TaskHost pooling | Requires opt-in mechanism for stateless tasks |

---

## 9. Alternatives Considered

| Alternative | Why Rejected |
|-------------|--------------|
| **Spawn new TaskHost on yield** | Breaks static state sharing - tasks using `GetRegisteredTaskObject` or static fields rely on process affinity |
| **Task author opt-in isolation modes** | Good for future, but doesn't help existing tasks. Deferred. |
| **Migrate first-party tasks to thread-safe** | "As soon as we change something, we own them"; doesn't help 3rd party; legacy WPF toolchain can't be changed |

---

## 10. Risks

| Risk | Mitigation |
|------|------------|
| Deadlock in callback wait | Timeouts, no lock held during wait, main thread never waits on task thread |
| IPC serialization bugs | Packet round-trip unit tests |
| TaskHost complexity increase | Document as state machine managing sub-state-machines |
| Concurrent task state management | Save/restore environment per task; track pending requests per task ID |

**Note:** No "breaking existing behavior" risk - callbacks currently fail/throw, so any implementation is an improvement.

---

## 11. Testing Strategy

- **Unit:** Packet serialization round-trip, request-response correlation, timeout/cancellation
- **Integration:** End-to-end `-mt` build with callback-using task, recursive `BuildProjectFile`
- **Stress:** Many concurrent callbacks, large `ITaskItem[]` outputs

---

## 12. File Changes

**Modified:** `INodePacket.cs`, `NodePacketFactory.cs`, `OutOfProcTaskHostNode.cs`, `TaskHostTask.cs`

**New packets:** `TaskHostBuildRequest/Response.cs`, `TaskHostYieldRequest/Response.cs` (Phase 1 may not need packets - trivial returns)

---

## Appendix: References

- **OutOfProcTaskHostNode** - `src/MSBuild/OutOfProcTaskHostNode.cs`
  - IBuildEngine stub implementations (search for `BuildProjectFile`, `Yield`, `RequestCores`)
  - Main thread message loop in `Run()` method
  - Task thread spawning in `RunTask()` method
- **TaskHostTask (parent side)** - `src/Build/Instance/TaskFactories/TaskHostTask.cs`
  - Handles `LogMessagePacket`, `TaskHostTaskComplete`, `NodeShutdown`
- **Packet serialization** - `src/Shared/TaskParameter.cs`
  - `TaskParameterTaskItem` nested class for ITaskItem serialization
- **Worker node yield logic** - `src/Build/BackEnd/Components/RequestBuilder/RequestBuilder.cs`
  - `SaveOperatingEnvironment()` / `RestoreOperatingEnvironment()` methods
- **Environment utilities** - `src/Shared/CommunicationsUtilities.cs`, `src/Shared/NativeMethodsShared.cs`
- **RegisteredTaskObjectCache** - `src/Build/BackEnd/Components/Caching/RegisteredTaskObjectCacheBase.cs`
  - Uses static `s_appDomainLifetimeObjects` dictionary (process-scoped)
