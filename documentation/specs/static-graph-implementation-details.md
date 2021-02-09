- [Single project isolated builds: implementation details](#single-project-isolated-builds-implementation-details)
  - [Input / Output cache implementation](#input--output-cache-implementation)
  - [Isolation implementation](#isolation-implementation)
    - [How isolation exemption complicates everything](#how-isolation-exemption-complicates-everything)

# Single project isolated builds: implementation details

<!-- workflow -->
Single project isolated builds can be achieved by providing MSBuild with input and output cache files.

The input cache files contain the cached results of all the targets that a project calls on its references. When a project builds without isolation, it builds its references via [MSBuild task](aka.ms/msbuild_tasks) calls. In isolated builds, the engine, instead of executing these tasks, serves their results from the provided input caches. In an isolated project build, only the top level project (built via the BuildManager APIs) should build targets. Any referenced projects by the top level project should be provided from the input caches.

The output cache file tells MSBuild where to serialize the results of building the current project. This output cache becomes an input cache for all other projects that depend on the current project.
The output cache file can be omitted in which case the build reuses prior results but does not write out any new results. This is useful when one wants to re-execute the build for a project without building its references.

The presence of either input or output caches turns on [isolated build constraints](static-graph.md##single-project-isolated-builds).

## Input / Output cache implementation
<!-- cache structure -->
The cache files contain the serialized state of MSBuild's [ConfigCache](https://github.com/Microsoft/msbuild/blob/master/src/Build/BackEnd/Components/Caching/ConfigCache.cs) and [ResultsCache](https://github.com/Microsoft/msbuild/blob/master/src/Build/BackEnd/Components/Caching/ResultsCache.cs). These two caches have been traditionally used by the engine to cache build results. For example, it is these caches which ensure that a target is only built once per build submission. The `ConfigCache` entries are instances of [BuildRequestConfiguration](https://github.com/microsoft/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Shared/BuildRequestConfiguration.cs#L25). The `ResultsCache` entries are instances of [BuildResult](https://github.com/microsoft/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Shared/BuildResult.cs#L34), which contain or more instances of [TargetResult](https://github.com/microsoft/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Shared/TargetResult.cs#L22). 

One can view the two caches as the following mapping: `(project path, global properties) -> results`. `(project path, global properties)` is represented by a `BuildRequestConfiguration`, and the results are represented by `BuildResult` and `TargetResult`.

<!-- cache lifetime -->
The input and output cache files have the same lifetime as the `ConfigCache` and the `ResultsCache`. The `ConfigCache` and the `ResultsCache` are owned by the [BuildManager](https://github.com/Microsoft/msbuild/blob/master/src/Build/BackEnd/BuildManager/BuildManager.cs), and their lifetimes are one `BuildManager.BeginBuild` / `BuildManager.EndBuild` session. On commandline builds, since MSBuild.exe uses one BuildManager with one BeginBuild / EndBuild session, the cache lifetime is the same as the entire process lifetime. When other processes (e.g. Visual Studio's devenv.exe) perform msbuild builds via the `BuildManager` APIs, there can be multiple build sessions in the same process.

<!-- constraints -->

When MSBuild is loading input cache files, it has to merge multiple incoming instances of `ConfigCache` and `ResultsCache` into one instance of each. The [CacheAggregator](https://github.com/microsoft/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/BuildManager/CacheAggregator.cs#L13) is responsible for stitching together the pairs of deserialized `ConfigCache`/`ResultsCache` entries from each input cache file.
The following constraints are enforced during cache aggregation:
- For each input cache, `ConfigCache.Entries.Size == ResultsCache.Entries.Size`
- For each input cache, there is exactly one mapping from ConfigCache to ResultsCache (on `BuildResult.ConfigurationId` == `BuildRequestConfiguration.ConfigurationId`)
- Colliding configurations (defined as tuples of `(project path, global properties)`) get their corresponding BuildResult entries merged at the level of TargetResult entries. TargetResult conflicts are handled via the "first one wins" strategy. This is in line with vanilla msbuild's behaviour where a target tuple of `(project path, global properties, target)` gets executed only once.

The output cache file **only contains results for additional work performed in the current BeginBuild / EndBuild session**. Entries from input caches are not transferred to the output cache.

<!-- How input / output cache entries are separated with the override caches -->
Entries that make it into the output cache file are separated from entries serialized from input cache files via the use of [ConfigCacheWithOverride](https://github.com/microsoft/msbuild/blob/master/src/Build/BackEnd/Components/Caching/ConfigCacheWithOverride.cs) and [ResultsCacheWithOverride](https://github.com/microsoft/msbuild/blob/master/src/Build/BackEnd/Components/Caching/ResultsCacheWithOverride.cs). These are composite caches. Each contains two underlying caches: a cache where input caches files are loaded into (called the override cache), and a cache where new results are written into (called the current cache). Cache reads are satisified from both underlying caches (override cache is queried first, current cache is queried second). Writes are only written to the current cache, never into the override cache. The output cache file only contains the serialized current cache, and not the override cache, thus ensuring that only newly built results are serialized in the output cache file. It is illegal for both the current cache and override cache to contain entries for the same project configuration, a constraint that is checked by the two override caches on each cache read.

## Isolation implementation

[Isolation constraints](static-graph.md##single-project-isolated-builds) are implemented in the Scheduler and the TaskBuilder. [TaskBuilder.ExecuteInstantiatedTask](https://github.com/microsoft/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Components/RequestBuilder/TaskBuilder.cs#L743) ensures that the `MSBuild` task is only called on projects declared in `ProjectReference`. [Scheduler.CheckIfCacheMissOnReferencedProjectIsAllowedAndErrorIfNot](https://github.com/microsoft/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Components/Scheduler/Scheduler.cs#L1818) ensures that all `MSBuild` tasks are cache hits.

### How isolation exemption complicates everything
<!-- Potential cache scenarios caused by exemption -->
Project references [can be exempt](static-graph.md#exempting-references-from-isolation-constraints) from isolation constraints via the `GraphIsolationExemptReference` item.

The `Scheduler` knows to skip isolation constraints on an exempt `BuildRequest` because the [ProjectBuilder compares](https://github.com/microsoft/msbuild/blob/37c5a9fec416b403212a63f95f15b03dbd5e8b5d/src/Build/BackEnd/Components/RequestBuilder/RequestBuilder.cs#L349) each new `BuildRequest` against the `GraphIsolationExemptReference` items defined in the calling project, and if exempt, sets `BuildRequest.SkipStaticGraphIsolationConstraints`. When a `BuildRequest` is marked as exempt, the `Scheduler` also marks its corresponding `BuildRequestConfiguration` as exempt as well, which aids in further identification of exempt projects outside the `Scheduler`.

The build results for the exempt project are also included in the current cache (according to the above mentioned rule that newly built results are serialized in the output cache file). This complicates the way caches interact in several scenarios:
1. the same project can be exempt via multiple references, thus potentially colliding when multiple output cache files containing the same exempt project get aggregated. For example, given the graph `{A->B, A->C}`, where both `B` and `C` build the exempt project `D`, `D` will appear in the output caches of both `B` and `C`. When `A` aggregates these two caches, it will encounter duplicate entries for `D`, and will need to merge the results.
2. a project can be both exempt and present in the graph at the same time. For example, given the graph `{A->B}`, both `A` and `B` are in the graph, but `A` can also mark `B` as exempt (meaning that `A` contains both a `ProjectReference` item to `B`, and a `GraphIsolationExemptReference` item to `B`). The fact that `B` is in the graph means that `A` will receive an input cache containing B's build results. There are two subcases here:
   1.  `A` builds targets from `B` that already exist in the input cache file from `B`. In this case, all the builds of `B` will be cache hits, and no target results from `B` will make it into `A`'s output cache, since nothing new was built.
   2.  `A` builds targets from `B` that do not exist in the input cache file from `B`. If `B` weren't exempt from isolation constraints, this scenario would lead to a build break, as cache misses are illegal under isolation. With `B` being exempt, the new builds of `B` will get included in `A`'s output cache. The results from `B`'s cache file won't get included in `A`'s output cache file, as they weren't built by `A`.
3. A project, which is not in the graph, can be exempt by two parent/child projects from the graph. For example, given the graph `{A->B}`, both `A` and `B` can exempt project `D` (meaning that neither `A` nor `B` have a `ProjectReference` to `D`, but both `A` and `B` have a `GraphIsolationExemptReference` to `D`). The fact that `B` is in the graph means that `A` will receive an input cache containing `B`'s build results. Since `B` builds targets from `D`, it means that `B`'s output cache file also contains target results from `D`. There are two subcases here:
   1. `A` builds targets from `D` that already exist in the input cache file from `B`. This is handled in the same way as the above case `2.1.`
   2. `A` builds targets from `D` that do not exist in the input cache file from `B`, meaning that `A` builds additional targets from `D` which `B` didn't build. This is handled in the same way as teh above case `2.2.`

**Current issue:** if multiple nodes in the graph exempt the same project file, the build results of the exempt project will trickle up and conflict in the first parent that tries to merge them. Documented in issue [#4386](https://github.com/dotnet/msbuild/issues/4386).
