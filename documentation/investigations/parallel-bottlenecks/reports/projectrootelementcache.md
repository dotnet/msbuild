# ProjectRootElementCache

## Why shared

`ProjectRootElementCache` is the shared XML root cache behind `ProjectRootElement.OpenProjectOrSolution(...)`, so project-file loads and import-file loads converge on it. `ProjectCollection` can additionally reuse one process-wide instance through `ProjectCollection.s_projectRootElementCache` when `reuseProjectRootElementCache` is enabled (`src\Build\Definition\ProjectCollection.cs:340-353`). MSBuild turns that on for server nodes (`src\MSBuild\XMake.cs:1421-1432`).

## Why it might bottleneck

Two shared synchronization points can limit parallel evaluation throughput:

- a global `_locker` protects lookup/mutation and also runs strong-cache maintenance (`src\Build\Evaluation\ProjectRootElementCache.cs:293-318`, `src\Build\Evaluation\ProjectRootElementCache.cs:370-449`)
- a per-file lock serializes duplicate misses for the same path, including the XML load/parse for that file (`src\Build\Evaluation\ProjectRootElementCache.cs:269-275`, `src\Build\Evaluation\ProjectRootElementCache.cs:340-357`)

In a one-process multi-project build, many projects often import the same SDK props/targets, so they naturally converge on both mechanisms.

## Evidence

- cache is consulted for both direct loads and imports: `src\Build\Evaluation\ProjectRootElementCache.cs:27-33`
- import evaluation opens each imported file through the cache: `src\Build\Evaluation\Evaluator.cs:2193-2201`
- project load also opens through the cache:
  - `src\Build\Definition\ProjectCollection.cs:1252-1260`
  - `src\Build\Definition\Project.cs:1957-1964`
  - `src\Build\Instance\ProjectInstance.cs:311-318`
- all of those paths delegate to `ProjectRootElement.OpenProjectOrSolution(...)`, which calls `projectRootElementCache.Get(...)`: `src\Build\Construction\ProjectRootElement.cs:1789-1800`
- hit path acquires `_locker`, probes `_weakCache`, and boosts the strong cache: `src\Build\Evaluation\ProjectRootElementCache.cs:293-318`
- strong-cache boost is O(n) linked-list work while holding the lock: `src\Build\Evaluation\ProjectRootElementCache.cs:563-599`
- miss path uses `_fileLoadLocks` to serialize same-file loads: `src\Build\Evaluation\ProjectRootElementCache.cs:269-275`
- evaluation-only alternative exists specifically to avoid the LRU cache and class-wide lock for parallel evaluation: `src\Build\Evaluation\SimpleProjectRootElementCache.cs:16-24`
- the repo exposes an opt-in trait for that alternative: `src\Build\Definition\ProjectCollection.cs:336-339`, `src\Framework\Traits.cs:60`

## Likelihood

**Medium-high**.

This candidate is stronger than a speculative “maybe there is a shared cache somewhere” claim because the repository already contains two direct signals:

1. the cache sits on the project/import open path, which is a universally exercised path during evaluation
2. there is an evaluation-only alternate implementation whose stated purpose is better parallel-evaluation performance

The main reason it is not “high with certainty” is that the heaviest work, XML parsing, is intentionally kept out of the global lock.

## Expected contention mode

- **Startup burst / IO serialization**: many parallel projects hit the same imported file for the first time; one thread parses while others wait on the per-file lock
- **Repeated per-project contention**: once shared imports are warm, many threads still acquire the global `_locker` on hits to probe `_weakCache` and maintain the strong-cache LRU list

The first mode is likely more important than the second.

## Where it is used

- project load in `ProjectCollection`: `src\Build\Definition\ProjectCollection.cs:1252-1260`
- direct `Project` construction: `src\Build\Definition\Project.cs:1957-1964`
- direct `ProjectInstance` construction: `src\Build\Instance\ProjectInstance.cs:311-318`
- import expansion in `Evaluator`: `src\Build\Evaluation\Evaluator.cs:2193-2201`
- build-scoped sharing via `BuildParameters(ProjectCollection)`: `src\Build\BackEnd\BuildManager\BuildParameters.cs:257-262`
- process-wide reuse on server nodes:
  - `src\MSBuild\XMake.cs:1421-1432`
  - `src\Build\BackEnd\Node\OutOfProcNode.cs:170-173`
  - `src\Build\BackEnd\Node\OutOfProcNode.cs:712-715`

## Why it may or may not matter in practice

### Why it may matter

- SDK-style projects repeatedly import the same shared files, so same-path miss serialization is a realistic scenario.
- The cache-size comment says increasing the strong-cache size produced a noticeable performance improvement on real projects, which suggests this path is material enough to tune (`src\Build\Evaluation\ProjectRootElementCache.cs:66-69`).
- Shared cache reuse extends beyond one evaluation when server-node reuse is active.

### Why it may not matter much

- XML parsing is not done under the global `_locker`, reducing cross-file serialization.
- The per-file lock is path-specific, so unrelated imports can still load in parallel.
- After warmup, the cache should trade repeated parse cost for shorter hit-path lock traffic.

Overall judgment: this is likely a **real startup-phase throughput limiter**, but probably not a “whole build stays globally serialized” bottleneck.

## How to validate

1. Instrument `ProjectRootElementCache.Get`, `GetOrLoad`, and `BoostEntryInStrongCache` to record:
   - hit vs miss counts
   - wait time on `_locker`
   - wait time on per-file locks
   - hottest file paths
2. Run a one-process multi-project workload with many shared SDK imports.
3. Compare:
   - default `ProjectRootElementCache`
   - `MsBuildUseSimpleProjectRootElementCacheConcurrency=1`
4. Look specifically for:
   - large startup lock waits on a small set of import paths
   - high hit counts coupled with measurable `_locker` contention
   - throughput improvement when the simple cache is enabled

If the simple-cache switch measurably improves graph evaluation throughput, that would strongly confirm this candidate.
