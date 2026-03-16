---
applyTo: "src/Build/BackEnd/**"
---

# BackEnd & Execution Engine Instructions

The BackEnd folder (473 review comments — highest in the repo) contains MSBuild's multi-process execution engine: `BuildManager`, node communication, the scheduler, result caching, and task execution.

## Concurrency & Thread Safety (Critical)

* MSBuild runs tasks across in-proc and out-of-proc nodes concurrently. All shared mutable state must be synchronized.
* `BuildManager.cs` (54 review comments) is the most complex file. Changes here affect the entire build lifecycle — `BeginBuild`/`EndBuild`/`ResetCaches` sequences must remain correct.
* Test behavior in **both** in-proc and out-of-proc scenarios — they have different state isolation, type loading, and serialization requirements.
* Lock ordering must be consistent to prevent deadlocks. Document lock acquisition order in comments when introducing new locks.

## Node Communication & IPC

* `NodeProviderOutOfProcBase.cs` (41 comments) handles IPC. Packet serialization must handle all MSBuild types correctly and maintain backward compatibility.
* Never change the IPC packet format without versioning. Old nodes must be able to communicate with new ones during rolling updates.
* IPC message ordering matters — race conditions in node communication cause intermittent, hard-to-reproduce failures.
* Task host node (`NodeProviderOutOfProcTaskHost.cs`, 23 comments) has additional isolation constraints for type loading.

## Scheduler Correctness

* The scheduler assigns build requests to nodes. Changes can cause deadlocks, starvation, or incorrect parallelism.
* Yield/unyield semantics must be preserved — tasks that yield allow their node to process other requests.

## Result Caching

* Build results are cached by `(project path, global properties, targets)`. Changes to cache key computation break incremental builds.
* Cache coherence between nodes is critical — stale results cause incorrect builds.
* See [Results Cache](../../documentation/wiki/Results-Cache.md) and [Cache Flow](../../documentation/wiki/CacheFlow.png).

## SDK Resolution

* `SdkResolverService.cs` (29 comments) resolves SDK references during evaluation. Changes affect every SDK-style project.
* SDK resolution must not have side effects that persist across evaluations.

## BuildManager Lifecycle

* `BeginBuild`→ submissions → `EndBuild` is the required sequence. Reentrant calls and out-of-order lifecycle events must be handled gracefully.
* `ResetCaches` must not lose in-flight results.

## Related Documentation

* [Nodes Orchestration](../../documentation/wiki/Nodes-Orchestration.md)
* [Results Cache](../../documentation/wiki/Results-Cache.md)
* [Logging Internals](../../documentation/wiki/Logging-Internals.md)
* [Threading spec](../../documentation/specs/threading.md)
