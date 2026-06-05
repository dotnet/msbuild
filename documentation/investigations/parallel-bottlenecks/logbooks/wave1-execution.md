# Wave 1 logbook - execution

## Scope searched

Broad static scan of execution/scheduling/backend code, centered on:

- `src\Build\BackEnd\BuildManager\BuildManager.cs`
- `src\Build\BackEnd\Components\BuildRequestEngine\**`
- `src\Build\BackEnd\Components\Caching\**`
- `src\Build\BackEnd\Components\Communications\**`
- `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs`
- `src\Build\BackEnd\Components\Scheduler\**`
- `src\Build\BackEnd\Components\SdkResolution\**`
- `src\Build\BackEnd\Node\InProcNode.cs`
- `src\Build\BackEnd\Shared\BuildResult.cs`

Search focus: shared mutable state, singleton services, coarse locking, `ConcurrentDictionary`/`Lazy<>` caches, scheduler scans, node/build coordination, and result/config cache plumbing. `src\Tasks\**` was intentionally skipped. Logging-specific analysis was also left to the separate logging wave.

## Candidate list grouped by build stage

### 1. Entry / configuration setup

#### Candidate: main-node SDK resolution funnel
- **Symbol:** `MainNodeSdkResolverService` + `CachingSdkResolverService._cache` + `CachingSdkResolverLoader.Instance`
- **Why shared:** `MainNodeSdkResolverService` is registered as the main-process build component for SDK resolution, so out-of-proc nodes route resolution through one service instance. The cache is keyed by submission id and SDK name, and the loader is a process-wide singleton.
- **Evidence:** `src\Build\BackEnd\Components\SdkResolution\MainNodeSdkResolverService.cs:18-25,29,59-95,99-106`; `src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverService.cs:20-23,52-72`; `src\Build\BackEnd\Components\SdkResolution\CachingSdkResolverLoader.cs:38-50,75-95`; `src\Build\BackEnd\Components\SdkResolution\SdkResolverService.cs:62-67`
- **Why it may bottleneck:** first-hit SDK resolution for a submission is centralized on the main node; callers for the same SDK name wait on the same `Lazy<SdkResult>`. Resolver/manifest discovery is also guarded by a singleton loader lock. This looks like a startup-burst / IO-serialization risk rather than a steady-state lock convoy.
- **Likelihood:** medium
- **Escalate to full report later:** **Yes** — especially if many SDK-style projects resolve the same few SDKs early.

#### Candidate: project-cache plugin initialization / query path
- **Symbol:** `BuildManager.ProjectCacheDescriptors` + `ProjectCacheService._projectCachePlugins`
- **Why shared:** `ProjectCacheDescriptors` is a static registry on `BuildManager`, and `ProjectCacheService` keeps shared plugin instances/tasks in a concurrent dictionary for the whole build.
- **Evidence:** `src\Build\BackEnd\BuildManager\BuildManager.cs:53-56,1564-1576`; `src\Build\BackEnd\Components\ProjectCache\ProjectCacheService.cs:50-53,102-123,128-158,160-171,590-648`
- **Why it may bottleneck:** if project cache plugins are enabled, first-use plugin initialization and later cache queries fan in through shared plugin state. The `Lazy<Task<...>>` pattern prevents duplicate initialization, but it also means followers wait for the first initializer/query path.
- **Likelihood:** low-medium
- **Escalate to full report later:** **No for now** — interesting, but heavily feature-dependent.

### 3. Scheduling / execution coordination

#### Candidate: `BuildManager._syncLock`
- **Symbol:** `Microsoft.Build.Execution.BuildManager._syncLock`
- **Why shared:** the field comment explicitly says it protects `BuildManager` shared data **and the Scheduler**. Submission setup, packet handling, result reporting, and completion cleanup all pass through it.
- **Evidence:** `src\Build\BackEnd\BuildManager\BuildManager.cs:63-66`; `src\Build\BackEnd\BuildManager\BuildManager.cs:543-585,966-980,1503-1585,1853-1887,2077-2096,2951-2993`
- **Why it may bottleneck:** this is the clearest coarse-grained process-wide gate in scope. Node packets, new submissions, configuration resolution, project-cache startup, and submission completion bookkeeping all serialize behind one monitor on the main node. Under many concurrent project completions/block/unblock events, this can become repeated per-project contention and central fan-in.
- **Likelihood:** high
- **Escalate to full report later:** **Yes**

#### Candidate: scheduler core (`Scheduler` + `SchedulingData`)
- **Symbol:** `Microsoft.Build.BackEnd.Scheduler` / `Microsoft.Build.BackEnd.SchedulingData`
- **Why shared:** one scheduler owns the build-wide request/node/configuration state tables and is driven by result, blocker, node-creation, and resource-request events from across the build.
- **Evidence:** `src\Build\BackEnd\Components\Scheduler\SchedulingData.cs:20-109,254-285,290-410,424-533`; `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:635-663,735-845,993-1030,1050-1075,1089-1122,1961-2015`
- **Why it may bottleneck:** scheduling is not just “a lock”; it is a centralized scan-heavy control loop. Each cycle recomputes idle nodes, resumes ready work, scans unscheduled requests, and sometimes does per-idle-node or recursive dependency/waiting analysis. Since `BuildManager` routes scheduler interactions through `_syncLock`, these O(nodes × requests) and queue-draining passes can directly limit throughput.
- **Likelihood:** high
- **Escalate to full report later:** **Yes**

#### Candidate: results cache coarse lock
- **Symbol:** `ResultsCache._resultsByConfiguration`
- **Why shared:** one results cache instance hangs off `BuildManager` and is used by scheduler/request-builder paths for result insertion, lookup, cache satisfaction, and disk-caching cleanup.
- **Evidence:** `src\Build\BackEnd\Components\Caching\ResultsCache.cs:29-39,64-85,109-123,162-223,230-267`; `src\Build\BackEnd\Shared\BuildResult.cs:581-608,732-748`
- **Why it may bottleneck:** the class uses a `ConcurrentDictionary<int, BuildResult>` but still locks the whole dictionary for add/merge, lookups, cache-satisfaction checks, clear, and write-to-disk. The critical sections include `BuildResult.MergeResults(...)`, target-result inspection, and disk-cache cleanup triggers, so this is a plausible long critical section on a hot shared path.
- **Likelihood:** high
- **Escalate to full report later:** **Yes**

#### Candidate: in-proc request engine serialization
- **Symbol:** `BuildRequestEngine._workQueue` + `BuildRequestEntry.GlobalLock`
- **Why shared:** the engine explicitly relies on a single-threaded `ActionBlock` for its local mutable state, and the scheduler preferentially routes traversal/proxy work to the single in-proc node. That in-proc node owns one `BuildRequestEngine`.
- **Evidence:** `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEngine.cs:72-116,231-235,1436-1455`; `src\Build\BackEnd\Components\BuildRequestEngine\BuildRequestEntry.cs:131-149,222-231,242-260`; `src\Build\BackEnd\Node\InProcNode.cs:47-50,120-123`; `src\Build\BackEnd\Components\Scheduler\Scheduler.cs:1036-1075`
- **Why it may bottleneck:** this is not process-wide across all nodes, but it is a real funnel for the scheduler process’s own execution path. Traversal/proxy/in-proc work, unblock transitions, and request-state updates all queue through one action pipe, so high churn on in-proc requests can cap useful concurrency on the main process.
- **Likelihood:** medium
- **Escalate to full report later:** **Yes**

#### Candidate: graph-build coordination lock
- **Symbol:** local `graphBuildStateLock` in `BuildManager` graph-build execution
- **Why shared:** one lock protects the blocked/building/finished/results collections for the whole graph build loop and is reacquired by each async completion callback.
- **Evidence:** `src\Build\BackEnd\BuildManager\BuildManager.cs:2254-2326`
- **Why it may bottleneck:** the loop waits for node completions, then under one lock scans blocked graph nodes to find newly-unblocked work; every completion callback also reacquires that lock to update the same dictionaries. On large static graphs, that makes graph scheduling/bookkeeping a serialized coordination point.
- **Likelihood:** medium-low
- **Escalate to full report later:** **Maybe** — worth revisiting if graph-build scenarios are a priority.

## Weaker candidates / likely non-bottlenecks

- **`ConfigCache._configurations`** — shared and important, but the main structure is two concurrent dictionaries rather than one coarse lock; the expensive paths are sweep/disk-cache maintenance, not every request (`src\Build\BackEnd\Components\Caching\ConfigCache.cs:15-20,73-80,115-147,163-170,238-273,346-392`).
- **`NodeManager._nodeIdToProvider` / `NodeProviderInProc._nodeContexts`** — mostly startup/shutdown/node-admin state, not obviously on the hottest steady-state scheduling path (`src\Build\BackEnd\Components\Communications\NodeManager.cs:34-37,91-113,308-338`; `src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs:61-73,187-206`).
- **`NodeProviderInProc.InProcNodeOwningOperatingEnvironment`** — this can cap in-proc parallelism, but only in the special `SaveOperatingEnvironment` / `MSBUILDINPROCENVCHECK` path, so it looks more like an explicit guardrail than a general bottleneck (`src\Build\BackEnd\Components\Communications\NodeProviderInProc.cs:73,214-239`).
- **`BuildManager.ProjectCacheDescriptors` by itself** — clearly shared, but by itself it looks more like a dedup/registry mechanism than a clear throughput limiter unless project-cache scenarios are active (`src\Build\BackEnd\BuildManager\BuildManager.cs:53-56`).
