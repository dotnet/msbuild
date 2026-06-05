# Wave 1 logbook - evaluation

## Scope

- Wave: 1 broad scan
- Mode: read-only static source scan
- Requested scope: `src\Build\Evaluation\**` plus tightly-coupled evaluation helpers when directly relevant
- Explicitly ignored: `src\Tasks\**`

## Scan method

Searched evaluation code for shared mutable state, singleton-style caches, and synchronization that can affect one MSBuild process evaluating many projects in parallel. Follow-up reads focused on:

- `ConditionEvaluator`
- `ProjectRootElementCache` and its reuse path from `ProjectCollection`
- `EvaluationContext` and the helper caches it wires in for shared evaluation
- evaluation startup helpers with process-wide caches

## Candidates grouped by build stage

### Project load and evaluation

#### 1. ConditionEvaluator expression cache serializes identical conditions

- **Shared object**: static `ConditionEvaluator.s_cachedExpressionTrees`, with a per-condition `Stack<GenericExpressionNode>` pool shared across evaluations for the same parser options and condition text.
- **Symbol**: `Microsoft.Build.Evaluation.ConditionEvaluator.s_cachedExpressionTrees`
- **Evidence**:
  - cache and pooling design: `src\Build\Evaluation\ConditionEvaluator.cs:134-167`
  - global lookup by parser options and condition text: `src\Build\Evaluation\ConditionEvaluator.cs:240-248`
  - the `lock (expressionPool)` spans parse/pop, `parsedExpression.Evaluate(state)`, reset, and push-back: `src\Build\Evaluation\ConditionEvaluator.cs:250-305`
  - cache-wide replacement when a table grows too large: `src\Build\Evaluation\ConditionEvaluator.cs:308-339`
- **Why it could bottleneck**:
  - The comments describe the pool as allowing multiple expression trees under high demand, but the pool lock currently wraps the full evaluation path.
  - That means concurrent evaluations of the same condition string are serialized on the shared pool lock instead of only synchronizing the pop/push operations.
  - Imported SDK/targets conditions are often repeated across many projects, so a hot condition can become a repeated cross-project choke point during parallel evaluation.
- **Likelihood**: high
- **Impact shape**: repeated per-project contention
- **Stage**: project load and evaluation
- **Escalate to full report later?**: yes
- **Next step**:
  - measure how often the top repeated condition strings are hit in a representative graph build
  - validate whether shrinking the pool lock to pop/push only preserves correctness and improves throughput

#### 2. ProjectRootElementCache has shared singleton reuse plus coarse internal locking

- **Shared object**:
  - optional process-wide `ProjectCollection.s_projectRootElementCache`
  - each reused cache instance contains shared `_weakCache`, `_strongCache`, `_fileLoadLocks`, and `_locker`
- **Symbol**:
  - `Microsoft.Build.Evaluation.ProjectRootElementCache`
  - `Microsoft.Build.Evaluation.ProjectCollection.s_projectRootElementCache`
- **Evidence**:
  - singleton reuse path: `src\Build\Definition\ProjectCollection.cs:118`, `src\Build\Definition\ProjectCollection.cs:336-353`
  - cache purpose and build-time reuse behavior: `src\Build\Evaluation\ProjectRootElementCache.cs:23-52`
  - shared fields and lock objects: `src\Build\Evaluation\ProjectRootElementCache.cs:74-88`, `src\Build\Evaluation\ProjectRootElementCache.cs:123-156`
  - per-file lock around cache miss/load: `src\Build\Evaluation\ProjectRootElementCache.cs:245-283`
  - global `_locker` around lookup/boost and cache mutation: `src\Build\Evaluation\ProjectRootElementCache.cs:294-357`, `src\Build\Evaluation\ProjectRootElementCache.cs:370-449`, `src\Build\Evaluation\ProjectRootElementCache.cs:506-516`, `src\Build\Evaluation\ProjectRootElementCache.cs:626-634`
  - strong-cache boost is O(n) linked-list walking while holding the cache lock: `src\Build\Evaluation\ProjectRootElementCache.cs:563-599`
  - evaluation-only alternative explicitly says it avoids the class-wide lock for better parallel evaluation throughput: `src\Build\Evaluation\SimpleProjectRootElementCache.cs:16-24`
- **Why it could bottleneck**:
  - Parallel projects frequently converge on the same imported files, SDK props/targets, and shared XML roots.
  - Cache misses serialize file load/parsing per path, which is desirable for duplicate suppression but still creates startup-burst serialization on common imports.
  - Cache hits also funnel through `_locker`, and every hit may walk the strong-cache linked list while holding that lock.
  - The existence of `SimpleProjectRootElementCache` as a concurrency-oriented alternative is direct evidence that the current design is already viewed as a parallel-evaluation limiter.
- **Likelihood**: high
- **Impact shape**: startup burst, repeated per-project contention, IO serialization
- **Stage**: project load and evaluation
- **Escalate to full report later?**: yes
- **Next step**:
  - determine how often the reused singleton path is active in real graph/server builds
  - quantify lock hold time for hit-heavy paths vs miss-heavy paths
  - compare hotspot behavior against `SimpleProjectRootElementCache`

#### 3. Shared EvaluationContext can funnel glob and filesystem cache activity through common dictionaries and per-key locks

- **Shared object**:
  - `EvaluationContext` in `SharingPolicy.Shared` mode
  - shared `FileEntryExpansionCache`, `CachingFileSystemWrapper`, and `FileMatcher`
- **Symbol**:
  - `Microsoft.Build.Evaluation.Context.EvaluationContext`
  - `Microsoft.Build.Shared.FileSystem.CachingFileSystemWrapper`
  - `Microsoft.Build.Shared.FileMatcher`
- **Evidence**:
  - `EvaluationContext` is explicitly thread-safe because callers can evaluate in parallel: `src\Build\Evaluation\Context\EvaluationContext.cs:17-23`
  - shared caches are created once per context and reused by derived contexts: `src\Build\Evaluation\Context\EvaluationContext.cs:61-72`, `src\Build\Evaluation\Context\EvaluationContext.cs:110-145`
  - `ProjectGraph` uses `EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared)` for its default evaluation path: `src\Build\Graph\ProjectGraph.cs:428-431`
  - `FileMatcher` uses the passed cache for directory-entry reuse: `src\Framework\Utilities\FileMatcher.cs:92-145`
  - `FileMatcher` also takes a per-enumeration lock before doing wildcard expansion work: `src\Framework\Utilities\FileMatcher.cs:1942-1967`
  - `CachingFileSystemWrapper` fronts existence and last-write-time checks with shared `ConcurrentDictionary` instances: `src\Framework\FileSystem\CachingFileSystemWrapper.cs:15-16`, `src\Framework\FileSystem\CachingFileSystemWrapper.cs:23-45`, `src\Framework\FileSystem\CachingFileSystemWrapper.cs:83-86`
- **Why it could bottleneck**:
  - When many projects share one evaluation context, repeated globbing and existence checks are intentionally funneled through common caches.
  - The design should help avoid duplicate IO, but identical wildcard expansions still serialize behind a per-key lock while filesystem enumeration runs.
  - This looks less severe than the XML root cache because it is opt-in through shared evaluation context usage and the locking is narrower, but it is still a plausible contention point in graph-heavy builds.
- **Likelihood**: medium
- **Impact shape**: repeated per-project contention, IO serialization
- **Stage**: project load and evaluation
- **Escalate to full report later?**: maybe
- **Next step**:
  - confirm how often shared evaluation context is used in the target scenarios
  - sample repeated wildcard keys and shared directory probes in large graph builds

### Entry / configuration setup

#### 4. Toolset configuration section cache

- **Shared object**: static `ToolsetConfigurationReaderHelpers` cache guarded by `s_syncLock`
- **Symbol**: `Microsoft.Build.Evaluation.ToolsetConfigurationReaderHelpers.s_toolsetConfigurationSectionCache`
- **Evidence**:
  - process-wide cached section plus lock: `src\Build\Evaluation\ToolsetElement.cs:21-53`
  - the slow path can rewrite config to a temporary file and reopen it while still in that helper: `src\Build\Evaluation\ToolsetElement.cs:83-100`
  - lazy consumer path: `src\Build\Definition\ToolsetConfigurationReader.cs:122-145`
- **Why it may bottleneck**:
  - The first reader funnels through a process-wide lock and can do configuration materialization and temporary-file based reopening on the slow path.
  - That makes it a plausible startup burst serializer when many evaluations converge on toolset setup at process start.
- **Likelihood**: low
- **Impact shape**: startup burst
- **Stage**: entry / configuration setup
- **Escalate to full report later?**: no
- **Next step**:
  - only revisit if later consolidation shows repeated toolset configuration reads during the evaluated scenario

## Non-candidates / not escalated in Wave 1

- `Expander` regex caches are static, but on modern `NET` builds they are generated regex accessors instead of lazy mutable caches; the `NETFRAMEWORK` fallback is still one-time initialization rather than a repeated synchronized hotspot (`src\Build\Evaluation\Expander.cs:3465-3511`).
- `IntrinsicFunctions` has static regex/lazy fields, but the notable shared state here is one-time initialization (`NuGetFramework`, registry regex) rather than a shared cache that all evaluations repeatedly mutate (`src\Build\Evaluation\IntrinsicFunctions.cs:45-53`).
- `ProjectParser` static `HashSet<string>` tables are immutable lookup data, so they are outside the bottleneck rubric for this pass.

## Wave 1 conclusion

The strongest evaluation-scope candidates are:

1. `ConditionEvaluator`'s per-condition pool lock, because it appears to serialize the full evaluation of repeated condition strings.
2. `ProjectRootElementCache`, because evaluation hits a shared XML/import cache with coarse locking and O(n) strong-cache maintenance, and the repo already contains a simpler concurrency-oriented replacement for evaluation scenarios.

`EvaluationContext`-driven filesystem and glob caching is worth keeping as a medium-likelihood candidate, especially for `ProjectGraph` workloads, but it currently looks more like a narrower serialization tradeoff than the two candidates above.
