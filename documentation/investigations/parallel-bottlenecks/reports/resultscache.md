# ResultsCache

## Why shared

`ResultsCache` is registered as a singleton build component, so one build host shares one cache instance (`src\Build\BackEnd\Components\BuildComponentFactoryCollection.cs:60-65`). In one-process multi-threaded builds, `NodeProviderInProc` can create up to `MaxNodeCount` in-proc nodes (`src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs:106-111,187-205`), and each `InProcNode` creates its own request engine against that same shared component host (`src\Build\BackEnd\Node\InProcNode.cs:111-123`). That makes the cache a real cross-node shared object for in-proc parallelism.

## Why it might bottleneck

Although the backing store is a `ConcurrentDictionary<int, BuildResult>`, the main API is guarded by `lock (_resultsByConfiguration)` (`src\Build\BackEnd\Components\Caching\ResultsCache.cs:28-39,64-85,109-142,162-223,230-237,259-267`). The hottest operations do more than a quick lookup:

- `AddResult` may merge into an existing mutable `BuildResult` (`ResultsCache.cs:68-77`; `src\Build\BackEnd\Shared\BuildResult.cs:581-608`)
- `SatisfyRequest` performs compatibility checks, scans explicit / initial / default targets, and constructs a filtered `BuildResult` while holding the lock (`ResultsCache.cs:167-223,311-396`; `BuildResult.cs:233-257,794-803`)

That makes the lock a serialization point for cache hits and result publication, not just dictionary mutation.

## Evidence

- Singleton registration: `src\Build\BackEnd\Components\BuildComponentFactoryCollection.cs:60-65`
- Multi-threaded in-proc nodes share the host: `src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs:106-111,187-205`; `src\Build\BackEnd\Node\InProcNode.cs:111-123`
- Coarse lock coverage:
  - `AddResult`: `src\Build\BackEnd\Components\Caching\ResultsCache.cs:64-85`
  - `GetResultForRequest`: `ResultsCache.cs:109-126`
  - `GetResultsForConfiguration`: `ResultsCache.cs:134-142`
  - `SatisfyRequest`: `ResultsCache.cs:162-223`
  - `ClearResultsForConfiguration`: `ResultsCache.cs:230-237`
  - `WriteResultsToDisk`: `ResultsCache.cs:259-267`
- Mutable shared result behavior:
  - no-copy insertion for re-entry reuse: `ResultsCache.cs:80-83`
  - target-builder re-entry path: `src\Build\BackEnd\Components\RequestBuilder\TargetBuilder.cs:137-147`
  - merge implementation: `src\Build\BackEnd\Shared\BuildResult.cs:581-608`
- Hot live call sites:
  - request-engine transfer / cache satisfaction / pressure handling: `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:377-411,487-503,583-610,927-934,1267-1283`
  - scheduler aborted-result path: `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1680-1688`
  - build-manager configuration replacement / cache-backed completion: `src\Build\BackEnd\BuildManager\BuildManager.cs:742-769,2497-2503,2563-2574`

## Likelihood

Medium.

This is a real shared lock with enough work inside to matter, but it looks less likely than the BuildManager / scheduler control plane to be the dominant throughput limiter. The strongest case is one-process, multi-threaded, in-proc-heavy builds with many small requests, re-entries, or result transfers.

## Expected contention mode

The most realistic mode is repeated serialization of `SatisfyRequest` and `AddResult` across the shared in-proc cache as multiple in-proc nodes publish or probe results for the same process. A secondary mode is occasional long lock hold time when `WriteResultsToDisk` or `ClearResultsForConfiguration` runs and does per-result cleanup inside the same lock (`src\Build\BackEnd\Components\Caching\ResultsCache.cs:230-237,259-267`).

## Where it is used

- `TargetBuilder` project entry / re-entry reuse (`src\Build\BackEnd\Components\RequestBuilder\TargetBuilder.cs:137-147`)
- `BuildRequestEngine` result transfer, cache-hit probing, and memory-pressure persistence (`src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:377-411,487-503,583-610,927-934,1267-1283`)
- `Scheduler` aborted-result recording (`src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1680-1688`)
- `BuildManager` cache reset / configuration replacement / cache-backed completion (`src\Build\BackEnd\BuildManager\BuildManager.cs:742-769,2497-2503,2563-2574`)
- override-cache mode still falls through to a live `CurrentCache` on misses (`src\Build\BackEnd\Components\Caching\ResultsCacheWithOverride.cs:18,44-70,72-111`)

## Why it may or may not matter in practice

Reasons it may matter:

- one-process builds can have multiple in-proc nodes sharing this cache
- the lock protects compound work, not just a single map read
- the cache stores mutable `BuildResult` objects specifically so re-entry can see partial prior work

Reasons it may not dominate:

- each `BuildRequestEngine` runs in a single-threaded `ActionBlock` context (`src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:107-110,231-235`)
- MSBuild's worker-node model is mostly continuation-based, so many builds do not create large numbers of simultaneous request-builder threads (`documentation\specs\threading.md:9-17`)
- out-of-proc nodes use separate process-local caches, so this particular shared lock is mainly an in-proc / one-process concern (`src\Build\BackEnd\Node\OutOfProcNode.cs:554-564`)

## How to validate

- Instrument `ResultsCache.AddResult`, `SatisfyRequest`, `GetResultsForConfiguration`, `WriteResultsToDisk`, and `ClearResultsForConfiguration` with:
  - call counts
  - inclusive time
  - time spent waiting to enter `lock (_resultsByConfiguration)`
  - hold time once the lock is acquired
- Correlate those metrics with:
  - number of in-proc nodes
  - number of yielded request-builder threads
  - cache hit rate vs miss rate
  - number of small project / target requests and re-entries
- Compare:
  - single-node vs multi-threaded in-proc builds
  - in-proc-heavy builds vs out-of-proc-heavy builds
  - normal builds vs memory-pressure cases that trigger `WriteResultsToDisk`
- If lock wait stays low but inclusive time is high, the cache is probably downstream of another coordinator bottleneck. If lock wait rises with in-proc node count and cache-hit traffic, this candidate is confirmed as a real throughput limiter.
