# Wave 2 logbook - ResultsCache

## Scope and files re-read

- `documentation\investigations\parallel-bottlenecks\plan.md`
- `documentation\wiki\Results-Cache.md`
- `documentation\specs\threading.md`
- `src\Build\BackEnd\Components\BuildComponentFactoryCollection.cs`
- `src\Build\BackEnd\Components\Caching\ResultsCache.cs`
- `src\Build\BackEnd\Components\Caching\ResultsCacheWithOverride.cs`
- `src\Build\BackEnd\Shared\BuildResult.cs`
- `src\Build\BackEnd\BuildManager\BuildManager.cs`
- `src\Build\BackEnd\Components\Scheduler\Scheduler.cs`
- `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs`
- `src\Build\BackEnd\Components\RequestBuilder\TargetBuilder.cs`
- `src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs`
- `src\Build\BackEnd\Node\InProcNode.cs`

## Evidence log

### 1. `ResultsCache` is genuinely shared for one-process multi-project builds

- The component factory registers `BuildComponentType.ResultsCache` as a singleton (`src\Build\BackEnd\Components\BuildComponentFactoryCollection.cs:60-65`).
- In multi-threaded mode, the in-proc node provider can create up to `MaxNodeCount` in-proc nodes, not just one (`src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs:106-111,187-205`).
- Each `InProcNode` creates its own `BuildRequestEngine`, but initializes it against the shared component host (`src\Build\BackEnd\Node\InProcNode.cs:111-123`).

Working conclusion: in one-process parallel builds, the cache is shared across the coordinator plus every in-proc node / request engine hosted in that process.

### 2. The main cache object is a `ConcurrentDictionary`, but the real API is still serialized with one coarse monitor

- `_resultsByConfiguration` is a `ConcurrentDictionary<int, BuildResult>` (`src\Build\BackEnd\Components\Caching\ResultsCache.cs:28-39`).
- The public methods still take `lock (_resultsByConfiguration)` around meaningful operations:
  - `AddResult` (`ResultsCache.cs:64-85`)
  - `GetResultForRequest` (`ResultsCache.cs:109-126`)
  - `GetResultsForConfiguration` (`ResultsCache.cs:134-142`)
  - `SatisfyRequest` (`ResultsCache.cs:162-223`)
  - `ClearResultsForConfiguration` (`ResultsCache.cs:230-237`)
  - `WriteResultsToDisk` (`ResultsCache.cs:259-267`)

So the concurrent dictionary mostly provides storage; the cache API behaves like a single coarse critical section.

### 3. `AddResult` intentionally shares mutable `BuildResult` objects instead of copying them

- On first insert, `AddResult` explicitly does not copy the `BuildResult`, because `TargetBuilder` relies on re-entry being able to observe already-built targets (`src\Build\BackEnd\Components\Caching\ResultsCache.cs:76-83`).
- `TargetBuilder` reads the current cache entry, creates a `BuildResult` for the current request, and inserts it if the configuration has no entry yet so re-entered builds can reuse partial target results (`src\Build\BackEnd\Components\RequestBuilder\TargetBuilder.cs:137-147`).
- When a configuration already has cached results, `AddResult` merges into the existing object via `BuildResult.MergeResults` (`ResultsCache.cs:68-77`), and `MergeResults` iterates the incoming target map and writes every target into `_resultsByTarget` (`src\Build\BackEnd\Shared\BuildResult.cs:581-608`).

This is the strongest reason the coarse cache lock still exists: the cache is protecting compound operations over shared mutable `BuildResult` instances, not just dictionary slot replacement.

### 4. `SatisfyRequest` does non-trivial work while holding the cache lock

Inside the lock, `SatisfyRequest`:

- looks up the configuration entry (`ResultsCache.cs:167-170`)
- evaluates flag / state compatibility (`ResultsCache.cs:171-173,349-396`)
- scans explicit, initial, and possibly default targets via `CheckResults` (`ResultsCache.cs:176-199,311-340`)
- allocates a list of targets to include (`ResultsCache.cs:201-215`)
- constructs a filtered `BuildResult` clone (`ResultsCache.cs:216`)

That filtered-clone constructor copies selected target entries from the existing result (`src\Build\BackEnd\Shared\BuildResult.cs:233-257,794-803`).

Working conclusion: this is not a tiny lock body. Cache-hit processing includes compatibility checks, several target scans, allocation, and per-target copying while other threads are excluded.

### 5. Hot call sites exist on both coordinator and worker paths

Coordinator / scheduler side:

- `Scheduler.MarkRequestAborted` records aborted results into the cache (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1680-1688`).
- `BuildManager` clears per-configuration results when configurations are replaced or dropped (`src\Build\BackEnd\BuildManager\BuildManager.cs:742-769,2497-2503`).
- `BuildManager` also inserts results returned from cache-backed submission completion (`BuildManager.cs:2563-2574`).

Worker / request execution side:

- `BuildRequestEngine` retrieves configuration results for result transfer (`src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:377-411`).
- It adds transferred results back into the local cache (`BuildRequestEngine.cs:487-503`).
- It probes `SatisfyRequest` while resolving configuration updates and while issuing child requests (`BuildRequestEngine.cs:583-610,1267-1283`).
- Under memory pressure it calls `WriteResultsToDisk` (`BuildRequestEngine.cs:927-934`).
- `TargetBuilder` hits the cache at project entry / re-entry (`src\Build\BackEnd\Components\RequestBuilder\TargetBuilder.cs:137-147`).

### 6. Practical contention is real, but narrower than the lock coverage first suggests

Evidence pulling the likelihood down:

- `BuildRequestEngine` explicitly documents that it runs in a single-threaded context because of its `ActionBlock` (`src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:107-110,231-235`).
- That means many request-engine cache calls are already serialized per node.
- MSBuild's threading model is continuation-oriented, and only yielded work tends to create multiple request-builder threads on one node (`documentation\specs\threading.md:9-17`).
- Out-of-proc nodes keep separate caches in separate processes; their shutdown path clears a process-local cache, not the main-process singleton (`src\Build\BackEnd\Node\OutOfProcNode.cs:554-564`).

Evidence keeping the candidate alive:

- In one-process multi-threaded builds, there can still be multiple in-proc nodes in the same process (`NodeProviderInProc.cs:106-111,187-205`), each with its own single-threaded request engine but a shared singleton cache (`BuildComponentFactoryCollection.cs:60-65`, `InProcNode.cs:111-123`).
- Yielded tasks can create additional request-builder threads in one worker node (`documentation\specs\threading.md:11-17`).
- The hottest cache APIs (`AddResult`, `SatisfyRequest`) hold the same coarse lock while doing more than a quick dictionary lookup.

Refined contention story: the realistic bottleneck is not "every cache lookup blocks the world." It is serialized cache-hit / cache-merge work across the shared in-proc cache when many small requests, re-entries, or result transfers are active in the same process.

### 7. Long-hold maintenance paths exist, but they are probably secondary

- `ClearResultsForConfiguration` removes an entry and then calls `removedResult?.ClearCachedFiles()` while still under the cache lock (`src\Build\BackEnd\Components\Caching\ResultsCache.cs:230-237`).
- `WriteResultsToDisk` iterates every cached result and calls `CacheIfPossible()` while still under the cache lock (`ResultsCache.cs:259-267`).

These can produce much longer lock hold times than `AddResult` / `SatisfyRequest`, but they look like cleanup / pressure paths, not the steady-state throughput story for normal parallel builds.

### 8. Override-cache mode does not remove the current-cache lock from the live build path

- `ResultsCacheWithOverride` fronts an override cache, but on misses it still delegates to `CurrentCache` for `GetResultForRequest`, `GetResultsForConfiguration`, `SatisfyRequest`, `ClearResultsForConfiguration`, and `WriteResultsToDisk` (`src\Build\BackEnd\Components\Caching\ResultsCacheWithOverride.cs:18,44-70,72-111`).

So input-cache scenarios reduce some live misses/hits, but they do not eliminate the current-cache lock from the active build path.

## Final assessment for the report

- **Why shared:** clear yes
- **Why it might bottleneck:** coarse lock around shared mutable `BuildResult` state; non-trivial work under the lock; shared by all in-proc nodes
- **Best realistic contention mode:** repeated `SatisfyRequest` / `AddResult` serialization across the shared in-proc cache, with occasional long stalls from `WriteResultsToDisk` / clear paths
- **Likelihood:** medium
- **Escalation status:** yes, but weaker than the BuildManager / scheduler control-plane candidate because much of the traffic is already serialized before it reaches the cache

## Weaker claims discarded during the deep dive

- "`ConcurrentDictionary` means the cache is already fine" — rejected; almost all important operations still take the same coarse lock.
- "`ResultsCache` is automatically a top-tier bottleneck because it is shared" — weakened; several major callers are already single-threaded or coordinator-serialized.
- "`WriteResultsToDisk` is the primary throughput bottleneck" — likely too strong; it is more plausibly a rare stall path than the normal steady-state limiter.
