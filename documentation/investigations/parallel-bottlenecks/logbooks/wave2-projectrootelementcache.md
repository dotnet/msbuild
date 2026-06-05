# Wave 2 logbook - ProjectRootElementCache

## Candidate

- Canonical object: `Microsoft.Build.Evaluation.ProjectRootElementCache`
- Reuse anchor: `Microsoft.Build.Evaluation.ProjectCollection.s_projectRootElementCache`
- Stage: project load and evaluation

## Deep-dive questions from the plan

### What code paths hit this shared state?

Primary evaluation paths all converge on `ProjectRootElementCacheBase.Get(...)` through `ProjectRootElement.OpenProjectOrSolution(...)`:

- project load via `ProjectCollection`: `src\Build\Definition\ProjectCollection.cs:1252-1260`
- direct `Project` construction: `src\Build\Definition\Project.cs:1957-1964`
- direct `ProjectInstance` construction: `src\Build\Instance\ProjectInstance.cs:311-318`
- import expansion during evaluation: `src\Build\Evaluation\Evaluator.cs:2187-2201`
- `ProjectRootElement.OpenProjectOrSolution(...)` itself delegates straight to the cache: `src\Build\Construction\ProjectRootElement.cs:1789-1800`

Conclusion: this is not an edge path. It sits directly on the project-file and import-file open path.

### How often is it likely to be touched in a multi-project build?

- Every evaluated project needs its root XML opened through this cache.
- Every import in every evaluated project also comes through the same cache path: `src\Build\Evaluation\Evaluator.cs:2187-2201`.
- The cache comments explicitly say it is consulted for both direct loads and imports: `src\Build\Evaluation\ProjectRootElementCache.cs:27-33`.
- The strong-cache size comment references ASP.NET Core projects having roughly 80-90 imports and says increasing the cache size produced a noticeable performance improvement: `src\Build\Evaluation\ProjectRootElementCache.cs:61-73`.

Conclusion: hit frequency is extremely high in realistic graph builds, especially for SDK-style projects with repeated common imports.

### Is the shared state read-mostly, write-heavy, or mixed?

Mixed, but predominantly read/hit-heavy after warmup:

- read/hit path: `_weakCache.TryGetValue(...)` plus `BoostEntryInStrongCache(...)` inside `_locker`: `src\Build\Evaluation\ProjectRootElementCache.cs:293-318`
- miss path: per-file lock plus XML load/parse, then `AddEntry(...)`: `src\Build\Evaluation\ProjectRootElementCache.cs:267-284`, `src\Build\Evaluation\ProjectRootElementCache.cs:340-357`
- lifecycle mutation: `DiscardStrongReferences`, `Clear`, `DiscardImplicitReferences`: `src\Build\Evaluation\ProjectRootElementCache.cs:425-492`

Conclusion: during the early part of a parallel evaluation burst it is mixed; once common imports are warm it becomes mostly hit traffic with repeated lock acquisition for lookup and strong-cache boosting.

### Is synchronization narrow and cheap, or could it serialize meaningful work?

There are two distinct synchronization modes:

1. **Global cache lock (`_locker`)**
   - protects cache lookup, explicit-load marking, preserve-formatting reload decision, and strong-cache maintenance: `src\Build\Evaluation\ProjectRootElementCache.cs:293-318`
   - also protects add/remove/clear operations: `src\Build\Evaluation\ProjectRootElementCache.cs:370-449`, `src\Build\Evaluation\ProjectRootElementCache.cs:506-516`, `src\Build\Evaluation\ProjectRootElementCache.cs:626-634`
   - `BoostEntryInStrongCache(...)` walks a linked list linearly while holding the lock: `src\Build\Evaluation\ProjectRootElementCache.cs:563-599`

2. **Per-file lock (`_fileLoadLocks`)**
   - used to suppress duplicate loads of the same path: `src\Build\Evaluation\ProjectRootElementCache.cs:269-275`
   - the actual parse/load happens while holding that per-file lock because `GetOrLoad(..., loadProjectRootElement, ...)` invokes the loader before `AddEntry(...)`: `src\Build\Evaluation\ProjectRootElementCache.cs:340-357`

Conclusion: the global lock is relatively small per hit but not trivial because of O(n) LRU work; the per-file lock can serialize meaningful XML load/parse work for hot shared imports.

### Is the expensive work inside or outside the critical section?

- **Outside `_locker`**:
  - normal XML load/parse via `loadProjectRootElement(...)`: `src\Build\Evaluation\ProjectRootElementCache.cs:340-357`
  - timestamp/content invalidation check: `src\Build\Evaluation\ProjectRootElementCache.cs:320-324`, `src\Build\Evaluation\ProjectRootElementCache.cs:165-220`
- **Inside per-file lock**:
  - the load path is still serialized per project path because `GetOrLoad(..., loadProjectRootElement, ...)` is invoked while `lock (perFileLock)` is held: `src\Build\Evaluation\ProjectRootElementCache.cs:269-275`
- **Inside `_locker`**:
  - lookup plus strong-cache boost and O(n) linked-list walk: `src\Build\Evaluation\ProjectRootElementCache.cs:293-318`, `src\Build\Evaluation\ProjectRootElementCache.cs:563-599`

Conclusion: the design avoids doing XML parse under the global lock, which reduces blast radius, but still serializes duplicate same-file misses under a per-file lock and all hits under the global lock.

### What evidence moves the candidate up or down?

Evidence that moves it **up**:

- evaluation-only alternative exists specifically to avoid the class-wide lock/LRU structure for parallel evaluation: `src\Build\Evaluation\SimpleProjectRootElementCache.cs:16-24`
- `ProjectCollection` can switch to that alternative via `Traits.Instance.UseSimpleProjectRootElementCacheConcurrency`: `src\Build\Definition\ProjectCollection.cs:336-339`, `src\Framework\Traits.cs:60`
- cache-size tuning already produced measurable improvement in real projects: `src\Build\Evaluation\ProjectRootElementCache.cs:66-69`

Evidence that moves it **down**:

- the most expensive work (XML parsing) is not done under the global `_locker`: `src\Build\Evaluation\ProjectRootElementCache.cs:340-357`
- per-file locking is targeted by path, so unrelated imports do not block each other: `src\Build\Evaluation\ProjectRootElementCache.cs:269-275`
- once a common import is loaded, later accesses should become short hit-path operations rather than repeated parse work.

## Reuse path findings

### How the cache becomes shared

- `ProjectCollection` stores a process-wide singleton in `s_projectRootElementCache` when `reuseProjectRootElementCache` is enabled: `src\Build\Definition\ProjectCollection.cs:340-353`
- MSBuild CLI turns that on for server nodes: `src\MSBuild\XMake.cs:1421-1432`
- `BuildParameters(ProjectCollection)` passes the collection cache through to builds/project instances: `src\Build\BackEnd\BuildManager\BuildParameters.cs:257-262`
- out-of-proc nodes also hold a static shared cache instance: `src\Build\BackEnd\Node\OutOfProcNode.cs:170-173`, `src\Build\BackEnd\Node\OutOfProcNode.cs:712-715`

Conclusion: the static `s_projectRootElementCache` matters most for long-lived server-node reuse across builds, but even within one build the same cache object is shared across many project evaluations.

## Hit/miss behavior findings

### Hit path

- `Get(...)` first probes the cache with `GetOrLoad(..., loadProjectRootElement: null, ...)`: `src\Build\Evaluation\ProjectRootElementCache.cs:258-265`
- inside `_locker`, it checks `_weakCache`, boosts the strong cache, and may mark explicit load state: `src\Build\Evaluation\ProjectRootElementCache.cs:293-307`
- successful hits return from cache immediately after invalidation check: `src\Build\Evaluation\ProjectRootElementCache.cs:327-337`

Implication: repeated imports of already-loaded shared SDK files become lock-heavy but parse-light.

### Miss path

- first miss takes/creates a per-path lock in `_fileLoadLocks`: `src\Build\Evaluation\ProjectRootElementCache.cs:269-271`
- loader runs while that per-path lock is held: `src\Build\Evaluation\ProjectRootElementCache.cs:271-275`, `src\Build\Evaluation\ProjectRootElementCache.cs:340-357`
- add/update then re-enters the global lock through `AddEntry(...)`: `src\Build\Evaluation\ProjectRootElementCache.cs:357`, `src\Build\Evaluation\ProjectRootElementCache.cs:370-377`

Implication: the first wave of parallel projects that all need the same imported file will serialize on that file’s lock while one thread parses and others wait.

## Where the contention is most plausible

### Most plausible

- early parallel evaluation of many projects sharing the same SDK/common imports
- server-node or long-lived node scenarios where one cache instance survives and is hit by many project loads across submissions

### Less plausible

- workloads where projects mostly import different files
- already-warm builds after the common imports have been parsed, where remaining cost is mostly short hit-path locking

## Final conclusion

`ProjectRootElementCache` looks like a **real but bounded** contention candidate.

- The case is **real** because every project/import open flows through it, misses of the same path are intentionally serialized, and hits still funnel through one global lock with O(n) strong-cache maintenance.
- The case is **bounded** because the most expensive work is not under the global lock, unrelated files do not block on the per-file lock, and the cache should quickly convert repeated work into cheaper hits.

Current decision:

- **Likelihood**: medium-high
- **Escalation**: justified as a full report candidate
- **Most likely contention mode**: startup burst on common imports, then lower-grade repeated lock pressure on cache hits
