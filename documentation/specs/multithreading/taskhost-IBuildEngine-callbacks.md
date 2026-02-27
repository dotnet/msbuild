# Design Specification: TaskHost IBuildEngine Callback Support

**Status:** Stage 1 merged ([#13149](https://github.com/dotnet/msbuild/pull/13149)) | **Related Issue:** #12863

---

## 1. Problem Statement

The MSBuild TaskHost (`OutOfProcTaskHostNode`) implements `IBuildEngine10` but lacks support for several callbacks. 

TaskHost is used when:
1. `MSBUILDFORCEALLTASKSOUTOFPROC=1`
2. or `-mt` mode - forces non-thread-safe tasks out-of-proc
3. **Explicit `TaskHostFactory`** in `<UsingTask>` declarations

If tasks in TaskHost call unsupported callbacks, the build fails with MSB5022 or `NotImplementedException`.

**Note:** This is an infrequent scenario - a compatibility layer for multithreaded MSBuild, not a hot path.

### Callback Support Status

| Callback | Interface | Status |
|----------|-----------|--------|
| `IsRunningMultipleNodes` | IBuildEngine2 | ✅ Stage 1 — forwarded to owning worker node via IPC |
| `BuildProjectFile` (4 params) | IBuildEngine | ❌ Stage 3 — logs MSB5022, returns `false` |
| `BuildProjectFile` (5 params) | IBuildEngine2 | ❌ Stage 3 — logs MSB5022, returns `false` |
| `BuildProjectFilesInParallel` (7 params) | IBuildEngine2 | ❌ Stage 3 — logs MSB5022, returns `false` |
| `BuildProjectFilesInParallel` (6 params) | IBuildEngine3 | ❌ Stage 3 — logs MSB5022, returns `false` |
| `Yield` / `Reacquire` | IBuildEngine3 | ❌ Stage 4 — silent no-op |
| `RequestCores` / `ReleaseCores` | IBuildEngine9 | ✅ Stage 2 — forwards to owning worker node via `TaskHostCoresRequest`/`Response` |

**Evidence:** src/MSBuild/OutOfProcTaskHostNode.cs

---

## 2. Goals

1. **Full IBuildEngine support in TaskHost** - Tasks work identically whether in-proc or in TaskHost
2. **Backward compatibility** - Existing behavior unchanged for tasks that don't use callbacks
3. **Acceptable performance** - IPC overhead tolerable for typical callback patterns
4. **Support multithreaded builds** - Unblock `-mt` for real-world projects such as WPF

**Non-Goal:** CLR2/net35 `MSBuildTaskHost.exe` support (never had this callback support)

---

## 3. Architecture

### Communication Flow

```text
Worker Node                               TASKHOST Process
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
```

For the detailed threading model, callback flow (sequence diagram), cancellation semantics, response guarantee, and TaskHost lifecycle, see [taskhost-threading.md](taskhost-threading.md).

---

## 4. Design

### 4.1 Threading Model

**Critical constraint:** TaskHost has two threads — main thread (`Run()`, IPC dispatch) and task runner thread (`task.Execute()`). Callbacks are invoked from the task thread but responses arrive on the main thread. See [taskhost-threading.md](taskhost-threading.md) for full details.

**Critical invariant:** All tasks within a single project that don't explicitly opt into their own private TaskHost must run in the **same process**. This is required because:

1. **Static state sharing** - Tasks may use static fields to share state (e.g., caches of parsed file contents)
2. **`GetRegisteredTaskObject` API** - ~500 usages on GitHub storing databases, semaphores, and even Roslyn workspaces
3. **Object identity** - Tasks expect object references to remain valid across invocations

This means TaskHost must support **concurrent task execution** within a single process when tasks yield or call `BuildProjectFile*`. Spawning new TaskHost processes per yielded task would break these invariants.

### 4.2 Packet Types

| Packet | Direction | Stage | Purpose |
|--------|-----------|-------|---------|
| `TaskHostIsRunningMultipleNodesRequest` | TaskHost → Worker Node | ✅ 1 | IsRunningMultipleNodes |
| `TaskHostIsRunningMultipleNodesResponse` | Worker Node → TaskHost | ✅ 1 | IsRunningMultipleNodes result |
| `TaskHostCoresRequest` | TaskHost → Worker Node | ✅ 2 | RequestCores/ReleaseCores (IsRelease bool) |
| `TaskHostCoresResponse` | Worker Node → TaskHost | ✅ 2 | Cores granted / ack |
| `TaskHostBuildRequest` | TaskHost → Worker Node | 3 | BuildProjectFile* calls |
| `TaskHostBuildResponse` | Worker Node → TaskHost | 3 | Build results + outputs |
| `TaskHostYieldRequest` | TaskHost → Worker Node | 4 | Yield/Reacquire |
| `TaskHostYieldResponse` | Worker Node → TaskHost | 4 | Acknowledgment |

**Location:** `src/Shared/` (compiled into both MSBuild.exe and Microsoft.Build.dll via `<Compile Include>` in each csproj). This follows the same pattern as all existing TaskHost packets (`TaskHostConfiguration`, `TaskHostTaskComplete`, etc.) since MSBuild.exe doesn't have `InternalsVisibleTo` from Microsoft.Build.dll.

All callback packets implement `ITaskHostCallbackPacket` (provides `RequestId` for request/response correlation).

### 4.3 NodePacketType Enum

```csharp
public enum NodePacketType : byte
{
    // ... existing (0x00-0x15 in use, 0x3C-0x3F reserved for ServerNode) ...
    TaskHostBuildRequest = 0x20,              // Stage 3
    TaskHostBuildResponse = 0x21,             // Stage 3
    TaskHostCoresRequest = 0x22,              // ✅ Stage 2
    TaskHostCoresResponse = 0x23,             // ✅ Stage 2
    TaskHostIsRunningMultipleNodesRequest = 0x24,  // ✅ Stage 1
    TaskHostIsRunningMultipleNodesResponse = 0x25,  // ✅ Stage 1
    TaskHostYieldRequest = 0x26,              // Stage 4
    TaskHostYieldResponse = 0x27,             // Stage 4
}
```

**Note:** The enum uses `TypeMask = 0x3F` (6 bits for type, max 64 values) and `ExtendedHeaderFlag = 0x40`. Values 0x16-0x3B are available; 0x20-0x27 is a safe range.

### 4.4 Cross-Version Compatibility ✅ Stage 1

New TaskHost (with callback support) may be launched by an old worker node MSBuild (without callback support). Sending callback packets to an old worker node would crash it.

**Solution: Version-gated callbacks with `Traits.cs` escape hatch**

- `PacketVersion` stays at **2** during development. It will be bumped to **3** when all callback stages are complete.
- The TaskHost stores the worker node's `PacketVersion` (received during handshake in `Run()`).
- A `CallbacksSupported` property gates all callback usage:
  ```csharp
  CallbacksSupported = _parentPacketVersion >= CallbacksMinPacketVersion  // 3
                       || Traits.Instance.EnableTaskHostCallbacks;
  ```
- When `CallbacksSupported` is **false**, callbacks return safe defaults (e.g., `IsRunningMultipleNodes → false`) — matching the pre-callback behavior. No packets are sent.
- **For development and testing**: Set `MSBUILDENABLETASKHOSTCALLBACKS=1` to enable callbacks before the version bump.

This ensures:
1. **Old worker node + new TaskHost** → callbacks disabled, safe defaults, no crash
2. **New worker node + new TaskHost (dev/test)** → env var enables callbacks before version bump
3. **Final ship** → `PacketVersion` bumped to 3, env var no longer needed

---

## 5. Phased Rollout

| Stage | Scope | Status | PR |
|-------|-------|--------|-----|
| 1 | `IsRunningMultipleNodes` + callback infrastructure | ✅ Merged | [#13149](https://github.com/dotnet/msbuild/pull/13149) |
| 2 | `RequestCores`/`ReleaseCores` | ✅ Merged | [#13306](https://github.com/dotnet/msbuild/pull/13306) |
| 3 | `BuildProjectFile*` | Planned | |
| 4 | `Yield`/`Reacquire` | Planned | |

**Stage 1 delivered:**
- `ITaskHostCallbackPacket` interface with `RequestId` for request/response correlation
- `SendCallbackRequestAndWaitForResponse<T>()` infrastructure in `OutOfProcTaskHostNode`
- `TaskHostIsRunningMultipleNodesRequest/Response` packets — actual IPC forwarding to owning worker node
- Version-gated callbacks via `PacketVersion` + `MSBUILDENABLETASKHOSTCALLBACKS=1` escape hatch
- Connection loss detection (MSB5027) — `OnLinkStatusChanged` fails all pending `TaskCompletionSource` entries
- Serialization unit tests, in-process integration tests, and cross-runtime E2E tests
- Threading and lifecycle documentation: [taskhost-threading.md](taskhost-threading.md)

**Stage 2 delivered:**
- `TaskHostCoresRequest/Response` packets — single packet pair with `IsRelease` bool for both `RequestCores` and `ReleaseCores`
- `OutOfProcTaskHostNode`: replaced `throw NotImplementedException()` with callback forwarding, input validation matching in-process behavior
- `TaskHostTask.HandleCoresRequest()`: forwards to in-process `IBuildEngine9` (implicit core accounting + scheduler)
- Serialization, integration (TaskHostFactory, MT auto-ejection, fallback MSB5022), and E2E tests

---

## 6. Stage 2+ Design Considerations

Issues discovered and design points clarified during Stage 1 implementation.

### Stage 2: RequestCores/ReleaseCores (Implemented)

**Complexity: Low** — Same pattern as `IsRunningMultipleNodes`.

- Single packet pair `TaskHostCoresRequest`/`TaskHostCoresResponse` (0x22/0x23) with `IsRelease` bool to distinguish the two operations
- OOP TaskHostNode is a thin proxy — validates input (`cores > 0`), sends request, blocks on TCS
- Worker node handler (`HandleCoresRequest`) forwards to in-process `IBuildEngine9` which handles implicit core accounting and scheduler communication
- Gated behind `CallbacksSupported`; when disabled: `RequestCores` returns 0, `ReleaseCores` is no-op (both log MSB5022)
- Tests: packet serialization, integration (TaskHostFactory, MT auto-ejection, fallback), E2E cross-runtime

### Stage 3: BuildProjectFile

**Complexity: High** — most complex stage.

- **`ITaskItem[]` serialization**: `TaskHostBuildResponse` must carry `IDictionary<string, ITaskItem[]>` target outputs. Reuse existing `TaskParameter` class pattern from `src/Shared/TaskParameter.cs` (`TaskParameterTaskItem` handles cross-process `ITaskItem` serialization).
- **Recursive builds**: The worker node scheduler may assign work back to the same TaskHost that requested the build. The main thread's `WaitAny` loop handles this correctly — `HandlePacket` routes by `NodePacketType` and doesn't block — but a new `TaskHostConfiguration` arriving while the original task is blocked on a callback creates a concurrent task execution scenario.
- **`_taskCompletePacket` is a single field**: Currently `RunTask()` stores its result in `_taskCompletePacket` and signals `_taskCompleteEvent`. With recursive builds, multiple tasks may be in-flight simultaneously. This needs per-task result tracking (e.g., `ConcurrentDictionary<taskId, TaskHostTaskComplete>`).
- **`_isTaskExecuting` is a single bool**: Same problem — needs to track per-task or be replaced with a task count.
- **Cancellation**: `BuildProjectFile` may take minutes. The cancellation "future opportunity" documented in [taskhost-threading.md](taskhost-threading.md) becomes more relevant here. For now, the worker node scheduler handles cancellation by cancelling the child build, which causes `BuildProjectFile` to return `false`.

### Stage 4: Yield/Reacquire

**Complexity: High** — coupled with Stage 3 infrastructure.

- **Concurrent tasks share `_pendingCallbackRequests`**: `RequestId` is unique across tasks (monotonically increasing `_nextCallbackRequestId`), so correlation works correctly. However, `HandleCallbackResponse` must be tested with multiple concurrent waiters.
- **Environment save/restore per-task context**: When a task yields, its environment (working directory, env vars) must be captured and restored on reacquire. The existing `RequestBuilder.SaveOperatingEnvironment()`/`RestoreOperatingEnvironment()` in worker nodes implements this pattern. Open question: extract to shared utility or duplicate the ~20 lines in TaskHost. TaskHost already has similar env-setting logic in `OutOfProcTaskHostNode.SetTaskHostEnvironment()`.
- **`_taskCompletePacket` / `_isTaskExecuting` single-field problem**: Same as Stage 3 — must be per-task by the time Yield is implemented.
- **Task thread management**: Yielded task's thread blocks on TCS. New task spawns on a new thread. `_taskRunnerThread` is currently a single reference — needs a collection of active task threads.

### Cross-cutting: Shared Infrastructure for Stages 3+4

The following single-field state in `OutOfProcTaskHostNode` must become per-task before Stages 3/4:

| Field | Current | Needed |
|-------|---------|--------|
| `_taskCompletePacket` | Single `TaskHostTaskComplete` | Per-task dictionary |
| `_isTaskExecuting` | Single `bool` | Task count or per-task flag |
| `_taskRunnerThread` | Single `Thread` | Collection of active threads |
| `_currentConfiguration` | Single `TaskHostConfiguration` | Per-task configuration |

This refactoring can be done as the first step of Stage 3.

---

## 7. Open Questions

### ~~Q1: Error handling for worker node crash during callback~~ ✅ Resolved

**Resolution:** (B) Detect pipe closure immediately. `OnLinkStatusChanged` callback fires when the pipe drops, failing all pending `TaskCompletionSource` entries with `InvalidOperationException` (MSB5027: `TaskHostCallbackConnectionLost`). No timeout needed — the OS notifies of pipe closure immediately.

### Q2: Reuse of existing worker node yield/state-save logic

Still open for Stage 4. Normal worker nodes already implement environment save/restore:

**Existing implementation:** `src/Build/BackEnd/Components/RequestBuilder/RequestBuilder.cs`
- `SaveOperatingEnvironment()` / `RestoreOperatingEnvironment()`
- Uses `NativeMethodsShared` and `CommunicationsUtilities` (already in `src/Shared/`)
- `SetEnvironmentVariableBlock()` is private to `RequestBuilder` — need to extract or duplicate

**Recommendation:** Extract shared utilities or duplicate the ~20 lines in TaskHost.

---

## 8. Key Decisions

| Decision | Rationale |
|----------|------------|
| Implement callbacks (not migrate tasks) | We'd "own" any tasks we touch; doesn't help 3rd party |
| Implement Yield/Reacquire | Significantly improved VMR build times; comes free with BuildProjectFile |
| Single TaskHost process (not spawn on yield) | Breaks static state sharing and `GetRegisteredTaskObject` guarantees |
| Defer TaskHost pooling | Requires opt-in mechanism for stateless tasks |
| Packets in `src/Shared/` not `src/MSBuild/` | MSBuild.exe lacks `InternalsVisibleTo` from Microsoft.Build.dll; follows existing packet pattern |
| `== "1"` for env var check | Avoids footgun where `MSBUILDENABLETASKHOSTCALLBACKS=0` would enable callbacks |
| Specific packet per callback (not generic query) | Each callback has different parameters/return types; generic pattern adds indirection without benefit |

---

## 9. Alternatives Considered

| Alternative | Why Rejected |
|-------------|--------------|
| **Spawn new TaskHost on yield** | Breaks static state sharing - tasks using `GetRegisteredTaskObject` or static fields rely on process affinity |
| **Task author opt-in isolation modes** | Good for future, but doesn't help existing tasks. Deferred. |
| **Migrate first-party tasks to thread-safe** | "As soon as we change something, we own them"; doesn't help 3rd party; legacy WPF toolchain can't be changed |
| **Generic query packet for all callbacks** | Each callback has different parameter/return types; `BoolResult` doesn't generalize. Replaced with specific packets per callback. |

---

## 10. Risks

| Risk | Mitigation |
|------|------------|
| Deadlock in callback wait | No lock held during wait; main thread never waits on task thread; causal dependency guarantees response (see [taskhost-threading.md](taskhost-threading.md)) |
| IPC serialization bugs | Packet round-trip unit tests for each packet type |
| TaskHost complexity increase | Threading doc + lifecycle state diagram in [taskhost-threading.md](taskhost-threading.md) |
| Concurrent task state management (Stage 3+4) | Per-task state refactoring; environment save/restore per task context |

**Note:** No "breaking existing behavior" risk - callbacks currently fail/throw, so any implementation is an improvement.

---

## 11. Testing Strategy

### Stage 1 (delivered)

- **Packet serialization**: `TaskHostCallbackPacket_Tests` — round-trip for request and response packets
- **In-process integration**: `TaskHostCallback_Tests` — `IsRunningMultipleNodes` in single-node and multi-node configs, callbacks-disabled fallback (verifies MSB5022)
- **Cross-runtime E2E**: `NetTaskHost_E2E_Tests` — .NET Framework host → .NET Core TaskHost callback via bootstrapped MSBuild
- **Lifecycle regression**: `TaskHostFactoryLifecycle_E2E_Tests` — validates all runtime/factory combinations still work

### Future stages

- **Integration:** End-to-end `-mt` build with recursive `BuildProjectFile`
- **Concurrent callbacks:** Multiple tasks yielded simultaneously, each with pending callbacks
- **Stress:** Large `ITaskItem[]` outputs, many concurrent callbacks

---

## 12. File Changes

### Stage 1 (merged)

**New files:**
- `src/Shared/ITaskHostCallbackPacket.cs` — callback packet interface
- `src/Shared/TaskHostIsRunningMultipleNodesRequest.cs` — request packet
- `src/Shared/TaskHostIsRunningMultipleNodesResponse.cs` — response packet
- `src/Build.UnitTests/BackEnd/TaskHostCallbackPacket_Tests.cs` — serialization tests
- `src/Build.UnitTests/BackEnd/TaskHostCallback_Tests.cs` — integration tests
- `src/Build.UnitTests/BackEnd/IsRunningMultipleNodesTask.cs` — shared test task
- `documentation/specs/multithreading/taskhost-threading.md` — threading documentation

**Modified:**
- `src/Shared/INodePacket.cs` — new `NodePacketType` enum values
- `src/MSBuild/OutOfProcTaskHostNode.cs` — callback infrastructure + `IsRunningMultipleNodes` implementation
- `src/Build/Instance/TaskFactories/TaskHostTask.cs` — callback request handler
- `src/Shared/TaskHostConfiguration.cs` — carries `CallbacksSupported` flag
- `src/Shared/TaskHostTaskComplete.cs` — carries callback connection state
- `src/Framework/Traits.cs` — `EnableTaskHostCallbacks` escape hatch

---

## Appendix: References

- **OutOfProcTaskHostNode** — `src/MSBuild/OutOfProcTaskHostNode.cs`
  - `SendCallbackRequestAndWaitForResponse<T>()`, `HandleCallbackResponse()`, `CallbacksSupported`
  - Main thread message loop in `Run()`, task thread spawning in `RunTask()`
- **TaskHostTask (worker node side)** — `src/Build/Instance/TaskFactories/TaskHostTask.cs`
  - `HandleIsRunningMultipleNodesRequest()`, packet handler registration
- **Threading documentation** — [taskhost-threading.md](taskhost-threading.md)
  - Thread model, callback flow, cancellation semantics, response guarantee, lifecycle
- **Packet serialization** — `src/Shared/TaskParameter.cs`
  - `TaskParameterTaskItem` nested class for `ITaskItem` serialization (needed for Stage 3)
- **Worker node yield logic** — `src/Build/BackEnd/Components/RequestBuilder/RequestBuilder.cs`
  - `SaveOperatingEnvironment()` / `RestoreOperatingEnvironment()` methods (reference for Stage 4)
- **Environment utilities** — `src/Shared/CommunicationsUtilities.cs`, `src/Shared/NativeMethodsShared.cs`
- **RegisteredTaskObjectCache** — `src/Build/BackEnd/Components/Caching/RegisteredTaskObjectCacheBase.cs`
  - Uses static `s_appDomainLifetimeObjects` dictionary (process-scoped)
