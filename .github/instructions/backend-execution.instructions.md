---
applyTo: "src/Build/BackEnd/**"
---

# BackEnd & Execution Engine Instructions

MSBuild's multi-process execution engine: `BuildManager`, node communication, scheduler, result caching, and task execution.

## Concurrency & Thread Safety (Critical)

* All shared mutable state must be synchronized — tasks run across in-proc and out-of-proc nodes concurrently.
* `BuildManager.cs` is the most complex file — `BeginBuild`/`EndBuild`/`ResetCaches` sequences must remain correct.
* Test in **both** in-proc and out-of-proc scenarios — they differ in state isolation, type loading, and serialization.
* Lock ordering must be consistent to prevent deadlocks. Document acquisition order when introducing new locks.

## Node Communication & IPC

* Never change the IPC packet format without versioning — old nodes must communicate with new ones during rolling updates.
* IPC message ordering matters — race conditions cause intermittent, hard-to-reproduce failures.
* Task host node (`NodeProviderOutOfProcTaskHost.cs`) has additional isolation constraints for type loading.

## Scheduler Correctness

* Changes can cause deadlocks, starvation, or incorrect parallelism.
* Yield/unyield semantics must be preserved — tasks that yield allow their node to process other requests.

## Result Caching

* Results are cached by `(project path, global properties, targets)` — changes to cache key computation break incremental builds.
* Cache coherence between nodes is critical — stale results cause incorrect builds.
* See [Results Cache](../../documentation/wiki/Results-Cache.md) and [Cache Flow](../../documentation/wiki/CacheFlow.png).

## SDK Resolution

* `SdkResolverService.cs` resolves SDK references during evaluation — changes affect every SDK-style project.
* SDK resolution must not have side effects that persist across evaluations.

## BuildManager Lifecycle

* `BeginBuild` → submissions → `EndBuild` is the required sequence. Handle reentrant calls and out-of-order events gracefully.
* `ResetCaches` must not lose in-flight results.

## Related Documentation

* [Nodes Orchestration](../../documentation/wiki/Nodes-Orchestration.md)
* [Results Cache](../../documentation/wiki/Results-Cache.md)
* [Logging Internals](../../documentation/wiki/Logging-Internals.md)
* [Threading spec](../../documentation/specs/threading.md)
