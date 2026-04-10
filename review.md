# Correctness Review: Per-Engine Telemetry Data Ownership

## Change Summary

PR #13413 fixed thread-safety in `WorkerNodeTelemetryData` by creating a **local dictionary pair per build request**, then merging under a lock. This caused a ~200MB allocation regression on OrchardCore (233 projects, ~1000 requests/node, ~30KB Entry[]/Int32[] resize churn per request × multiple nodes).

This fix moves telemetry data ownership from the `TelemetryForwarderProvider` singleton to per-`BuildRequestEngine` instances, threaded via `NodeLoggingContext.TelemetryData`. Zero per-request allocations, zero singleton contention, and the N× duplication bug stays fixed.

### Files Changed

| File | Change |
|------|--------|
| `NodeLoggingContext.cs` | Added `TelemetryData` property |
| `BuildRequestEngine.cs` | Creates data in `InitializeForBuild`, sends in `CleanupForBuild` via `SendTelemetryData` |
| `RequestBuilder.cs` | Writes directly to `_nodeLoggingContext.TelemetryData` with `lock(telemetryData)` |
| `TelemetryForwarderProvider.cs` | Stripped to minimal `IsTelemetryCollected` flag — no shared state |
| `ITelemetryForwarder.cs` | Removed `MergeWorkerData` |
| `Telemetry_Tests.cs` | Updated to test per-engine data flow |

---

## Red-Team Review (5 Perspectives)

### 1. Thread Safety ✅

**Q: Can `TelemetryData` be null-raced between `SendTelemetryData` and a builder still iterating?**
No. `CleanupForBuild` deactivates ALL builders and waits for each to complete via `WaitForDeactivateCompletion` before calling `SendTelemetryData`. The builder also captures `TelemetryData` into a local variable at the top of `UpdateStatisticsPostBuild`, so even a late null wouldn't crash.

**Q: Can two builders within one engine call `UpdateStatisticsPostBuild` concurrently?**
Yes — each `RequestBuilder` runs on its own thread (via `StartBuilderThread` → `Task.Factory.StartNew`). When builder A blocks on a sub-request, the engine can activate builder B. Both could complete around the same time. The `lock(telemetryData)` around every `AddTarget`/`AddTask` call handles this correctly.

**Q: Is `lock(telemetryData)` safe as a lock target?**
Yes. `WorkerNodeTelemetryData` is an internal class. No other code in the repository locks on it. It's the sole synchronization point for telemetry mutations.

**Q: Is `NodeLoggingContext.TelemetryData` assignment thread-safe?**
Yes. It's set in `InitializeForBuild` (before any requests run) and nulled in `SendTelemetryData` (after all builders are deactivated). Both operations happen through the engine's serialized lifecycle, never concurrent with builder writes.

**Q: `registeredTaskRecord.Statistics.Reset()` is outside the lock — race?**
This is a **pre-existing issue** (present before PR #13413). Two builders sharing the same `ProjectInstance`/`TaskRegistry` could race on `Reset()`. Not introduced or changed by this fix.

---

### 2. Data Completeness ✅

**Q: Could a builder complete AFTER `SendTelemetryData` sends the data?**
No. `CleanupForBuild` calls `BeginDeactivateBuildRequest` + `WaitForDeactivateCompletion` for every active entry before reaching `SendTelemetryData`. The sequence is: cancel all → wait all → send telemetry → shutdown.

**Q: What if `InitializeForBuild` was never called?**
`TelemetryData` stays null. `UpdateStatisticsPostBuild` checks `telemetryData is null` first and returns early. Same effective behavior as the old `NullTelemetryForwarder.IsTelemetryCollected => false` path.

**Q: Do cached/skipped requests lose telemetry?**
Cache hits are short-circuited by the Scheduler or engine-level cache path — they never create a `RequestBuilder`, so `UpdateStatisticsPostBuild` is never called. This is **identical** to the behavior before this change.

**Q: Do error paths lose telemetry?**
If `BuildTargets` throws, `UpdateStatisticsPostBuild` is skipped for that request. This is **identical** to the pre-change behavior.

**Q: Does `IsTelemetryEnabled` match the old `IsTelemetryCollected` gate?**
Yes. Both derive from `BuildParameters.IsTelemetryEnabled`. The old forwarder returned `true`/`false` from `IsTelemetryCollected` based on the same flag. The new code gates on `TelemetryData != null`, which is set in `InitializeForBuild` only when `IsTelemetryEnabled` is true.

---

### 3. Data Duplication ✅

**Q: Can `SendTelemetryData` be called more than once per build?**
No. It's called from `CleanupForBuild`, which runs once per build lifecycle. No other call site exists.

**Q: Can the same data be sent twice?**
No. `SendTelemetryData` nulls `loggingContext.TelemetryData` after capturing the snapshot. There's no second path to send the data.

**Q: The old N× duplication bug — is it still fixed?**
Yes, by construction. Each engine owns its own `WorkerNodeTelemetryData`. Each engine sends only what its builders accumulated. The old bug was caused by N engines all calling `FinalizeProcessing` on the same shared singleton, each sending the entire accumulated data. That shared singleton no longer holds data.

**Q: Is `NodeLoggingContext` truly per-engine?**
Yes. `InProcNode.HandleNodeConfiguration` creates a new `NodeLoggingContext` per build and passes it to `_buildRequestEngine.InitializeForBuild`. Two engines never share one `NodeLoggingContext`.

**Q: The old `TelemetryForwarderProvider.FinalizeProcessing` — could it still duplicate?**
No. It's now a no-op (empty method body). The call was removed from `CleanupForBuild`.

---

### 4. Node Lifecycle & Reuse ✅

**Q: Does node reuse get fresh telemetry data?**
Yes. On reuse: `CleanupForBuild` sends data and nulls `TelemetryData`. Next build calls `HandleNodeConfiguration` → new `NodeLoggingContext` → `InitializeForBuild` → new `WorkerNodeTelemetryData`.

**Q: Can `InitializeForBuild` be called twice without cleanup?**
No. It asserts `_status == Uninitialized`. `CleanupForBuild` is the only path that resets status to `Uninitialized`. Calling `InitializeForBuild` twice would throw.

**Q: What about server mode (`BuildManager` persistence)?**
`InProcNode` is reused via `NodeProviderInProc._nodeContexts`, but `NodeLoggingContext` is recreated every build in `HandleNodeConfiguration`. The engine gets a fresh context each time.

**Q: What about abrupt termination (kill/timeout)?**
Telemetry is lost — `CleanupForBuild` never runs. This is **identical** to the pre-change behavior. The only send path is through `CleanupForBuild`.

---

### 5. Consumer Compatibility ✅

**Q: Does `InternalTelemetryConsumingLogger` handle multiple events?**
Yes. It calls `_workerNodeTelemetryData.Add(e.WorkerNodeTelemetryData)` on each `WorkerNodeTelemetryEventArgs`, merging into a single per-build aggregate. Multiple events (one per engine) are handled identically to multiple events from out-of-proc nodes in the old design.

**Q: Is the binlog format changed?**
No. `WorkerNodeTelemetryEventArgs.WriteToStream`/`CreateFromStream` are unchanged. There may be more packets per build (one per engine vs one from the singleton), but each packet has the same format. Replay tools see more `LogMessage` packets, not a format change.

**Q: Any external consumers affected?**
No. `WorkerNodeTelemetryEventArgs` and `IEventSource5.WorkerNodeTelemetryLogged` are both internal. No public API surface is affected.

**Q: Is event ordering preserved?**
Yes. Telemetry is sent in `CleanupForBuild` before `BuildFinished` is logged in `BuildManager.EndBuild`. Same relative ordering as before.

**Q: Is the VS telemetry path affected?**
No. `InternalTelemetryForwardingLogger` forwards `WorkerNodeTelemetryEventArgs` via `BuildEventRedirector.ForwardEvent`. The events it receives have the same type and content; there may just be more of them (per-engine instead of per-singleton).

---

## Pre-existing Issue (Not Introduced)

`registeredTaskRecord.Statistics.Reset()` in `RequestBuilder.CollectTasksStats` is unsynchronized. If two builders share the same `ProjectInstance`/`TaskRegistry` (via `BuildRequestConfiguration`), they could race on `Reset()`. This existed before PR #13413 and is unchanged by this fix.

---

## Verdict

**No correctness regressions found.** The change is safe across all five dimensions: thread safety, data completeness, data duplication, node lifecycle, and consumer compatibility. All behavioral differences from the old code are improvements (elimination of N× duplication, elimination of per-request allocations), not regressions.
