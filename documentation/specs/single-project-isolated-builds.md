# Single Project Isolated Builds: Implementation Details

<!-- workflow -->
Single project isolated builds can be achieved by providing MSBuild with input and output cache files.

The input cache files contain the cached `TargetResult`s of all targets that a project calls on its references. When a project builds without isolation, it builds its references via [MSBuild task](aka.ms/msbuild_tasks) calls. In isolated builds, the engine, instead of executing these tasks, serves their results from the provided input caches. In an isolated project build, only the top level project (built via the `BuildManager` APIs) should build targets; Any referenced projects by the top level project should be provided from the input caches.

The output cache file tells MSBuild where to serialize the `TargetResult`s for a project's built targets and becomes an input cache for dependent projects.

The presence of either input or output caches turns on [isolated build constraints](static-graph.md##single-project-isolated-builds).

## Input / Output Cache Implementation
<!-- cache structure -->
The cache files contain the serialized state of MSBuild's [`ConfigCache`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Caching/ConfigCache.cs) and [`ResultsCache`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Caching/ResultsCache.cs), which have been traditionally used by the engine to cache build results. They ensure that a target is only built once per build submission. `ConfigCache` entries are instances of [`BuildRequestConfiguration`](https://github.com/dotnet/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Shared/BuildRequestConfiguration.cs#L25)s (a `(project path, global properties)` tuple), and `ResultsCache` entries are instances of [`BuildResult`](https://github.com/dotnet/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Shared/BuildResult.cs#L34)s, which contain [`TargetResult`](https://github.com/dotnet/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Shared/TargetResult.cs#L22)s. The `ConfigCache` entries and `ResultsCache` entries form a [bijection](https://en.wikipedia.org/wiki/Bijection).

<!-- cache lifetime -->
In a build, the input and output cache files have the same lifetime as the `ConfigCache` and  `ResultsCache`. The `ConfigCache` and  `ResultsCache` are owned by the [`BuildManager`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/BuildManager/BuildManager.cs), and their lifetimes are one `BuildManager.BeginBuild` / `BuildManager.EndBuild` session. On command-line builds, the cache lifetime is the same as the entire process lifetime since `MSBuild.exe` uses one `BuildManager` with one `BeginBuild` / `EndBuild` session. When other processes (e.g. Visual Studio's `devenv.exe`) perform MSBuild builds via the `BuildManager` APIs, there can be multiple build sessions in the same process.

<!-- constraints -->

When loading input cache files, MSBuild merges incoming instances of `ConfigCache`s and `ResultsCache`s into one instance of each with the help of the [`CacheAggregator`](https://github.com/dotnet/msbuild/blob/51df47643a8ee2715ac67fab8d652b25be070cd2/src/Build/BackEnd/BuildManager/CacheAggregator.cs#L15), which enforces the following constraints:

- No duplicate cache entries
- Bijection:
  - `ConfigCache.Entries.Size == ResultsCache.Entries.Size`
  - `BuildResult.ConfigurationId == BuildRequestConfiguration.ConfigurationId`

Note that the output cache file contains a single `BuildResult` with the `TargetResult`s from the project specified to be built in the `BeginBuild` / `EndBuild` session, as any `BuildResult`s obtained through isolation exemption are excluded to prevent potential duplicate input cache entries; Entries from input caches are not transferred to the output cache.

<!-- How input / output cache entries are separated with the override caches -->
Input cache entries are separated from output cache entries with the composite caches [`ConfigCacheWithOverride`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Caching/ConfigCacheWithOverride.cs) and [`ResultsCacheWithOverride`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Caching/ResultsCacheWithOverride.cs). Each composite cache contains two underlying caches: a cache where input caches files are loaded into (the override cache), and a cache where new results are written into (the current cache). *In the `ConfigCacheWithOverride`, these caches are instances of `ConfigCache`s and, in the `ResultsCacheWithOverride`, these caches are instances of `ResultsCache`s. A query for a cache entry is first attempted from the override cache and, if unsatisfied, a second attempt is made from the current cache. Writes are only written to the current cache, never into the override cache.* It is illegal for both the current cache and override cache to contain entries for the same project configuration, a constraint that is checked by the two override caches on each cache query.

## Isolation Implementation

[Isolation constraints](static-graph.md##single-project-isolated-builds) are implemented in the `Scheduler` and  `TaskBuilder`. [`TaskBuilder.ExecuteInstantiatedTask`](https://github.com/dotnet/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Components/RequestBuilder/TaskBuilder.cs#L743) ensures that the `MSBuild` task is only called on projects declared in a `ProjectReference` item. [`Scheduler.CheckIfCacheMissOnReferencedProjectIsAllowedAndErrorIfNot`](https://github.com/dotnet/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Components/Scheduler/Scheduler.cs#L1818) ensures that all `MSBuild` tasks are cache hits.

### Isolation Exemption

The `Scheduler` [skips isolation constraints](static-graph.md#exempting-references-from-isolation-constraints) on project references via the:

- `GraphIsolationExemptReference` item. The `RequestBuilder` sets the `SkipStaticGraphIsolationConstraints` property of a `BuildRequest` to `true` if the `RequestBuilder` matches it against a `GraphIsolationExemptReference` item defined in the calling project. Additionally, the `RequestBuilder` marks the `BuildRequest`'s corresponding `BuildRequestConfiguration` as exempt to allow the `TaskBuilder` to verify exemption from isolation constraints.

- `isolate:MessageUponIsolationViolation` switch. The `RequestBuilder` sets the `SkipStaticGraphIsolationConstraints` property of _every_ `BuildRequest` to `true`. The `TaskBuilder` verifies exemption from isolation constraints just by the switch value.

\* Except in the following scenario when a `ProjectReference` is exempted from isolation constraints: a dependency project A outputs a cache file F containing a `BuildResult` with `TargetResult`s T<sub>cached</sub> for targets t<sub>1</sub>, t<sub>2</sub>, ..., t<sub>m</sub> and a dependent project B uses F as an input cache file but builds and obtains the `TargetResult`s T<sub>new</sub> for targets t<sub>m + 1</sub>, t<sub>m + 2</sub>, ..., t<sub>n</sub> such that 0 < m < n. In this case, T<sub>new</sub> will be placed into the `ResultsCache` containing T<sub>cached</sub> to enforce no overlap between the override and current caches in the `ConfigCacheWithOverride`.
