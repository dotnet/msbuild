# Project Cache

## Summary

Project cache is a new assembly-based plugin extension point in MSBuild which determines whether a build request (a project) can be skipped during build. The main expected benefit is reduced build times via [caching and/or distribution](static-graph.md#weakness-of-the-old-model-caching-and-distributability).

## Motivation

As the introduction to [static graph](static-graph.md#what-is-static-graph-for) suggests, large and complex repos expose the weaknesses in MSBuild's scheduling and incrementality models as build times elongate. This project cache plugin lets MSBuild natively communicate with existing tools that enable build caching and/or distribution, enabling true scalability.

Visual Studio is one beneficiary. This plugin inverts dependencies among build systems: instead of higher level build engines ([Cloudbuild](https://www.microsoft.com/research/publication/cloudbuild-microsofts-distributed-and-caching-build-service/), [Anybuild](https://github.com/AnyBuild/AnyBuild), [BuildXL](https://github.com/microsoft/BuildXL), etc) calling into MSBuild, MSBuild calls into them, keeping MSBuild's external APIs and command line arguments largely unchanged and thus reusable by Visual Studio.

This change also simplifies and unifies user experiences. MSBuild works the same from Visual Studio or the command line without dramatically changing how it works.

## Plugin requirements

- The plugin should tell MSBuild whether a build request needs building. If a project is skipped, then the plugin needs to ensure that:
  - it makes the filesystem look as if the project built
  - it returns sufficient information back to MSBuild such that MSBuild can construct a valid [`BuildResult`](/src/Build/BackEnd/Shared/BuildResult.cs#L30-L33) for its internal scheduling logic, such that future requests to build a skipped project are served directly from MSBuild's internal caches.

## High-level design

Conceptually, there are two parts of caching: "cache get" and "cache add". "Cache get" is MSBuild asking the plugin if it wants to handle a build request, ie by fetching from some cache. "Cache add" is, upon cache miss, MSBuild providing enough information to the plugin during the build of the build request for the plugin to add the results to its cache and safely be able to retrieve it for some future build.

The "cache get" functionality was introduced in 16.9, while "cache add" was added in 17.8.

### Plugin discovery

- Plugin dlls are discovered by MSBuild via a new special purpose `ProjectCachePlugin` [items](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-items).
  - These items can get injected into a project's import graph by package managers via the [PackageReference](https://docs.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) item.
  - MSBuild will discover the plugin by searching project evaluations for `ProjectCachePlugin` items.

    ```xml
    <ItemGroup>
    <ProjectCachePlugin Include="$(SomePath)\MyAmazingCachePlugin.dll" />
    </ItemGroup>
    ```

- Programmatic usage of `BuildManager` can also set `BuildParameters.ProjectCacheDescriptor` to apply a plugin to all requests.

### Plugin lifetime

- Plugin instances reside only in the `BuildManager` node. Having it otherwise (plugin instances residing in all nodes) means forcing the plugins to either deal with distributed state or implement a long lived service. We consider this high complexity cost to not be worth it. We also want to avoid serializing the `ProjectInstance` between nodes, which is expensive.
- `BuildManager.BeginBuild` calls `ProjectCacheBase.BeginBuildAsync` on all discovered plugins. This allows plugins to start any required initialization work. It does not wait for the plugins to fully initialize, ie it is a "fire-and-forget" call at this point. The first query on the plugin will wait for plugin initialization.
  - `BeginBuildAsync` may be called with or without a `ProjectGraph`, depending on MSBuild has one to provide. When it is not provided, hints about the graph entry points are provided with which the plugin may decide to construct the `ProjectGraph` itself, if desired.
- `BuildManager.EndBuild` calls `ProjectCacheBase.EndBuildAsync` on all discovered plugins. This allows plugins to perform any required cleanup work. This is a blocking call which will be awaited before the build can complete.
- The plugin instance will get called in reverse topological sort order (from referenced projects up towards referencing projects). This happens when performing a graph build (`/graph`), Visual Studio solution builds, and commonly in higher build engines.
- Only the top-level build requests are checked against the cache. Build requests issued recursively from the top-level requests, for example a project building its dependencies, are not checked against the cache. However, because the build requests are assumed to be issued in reverse topological sort order, those requests should have already been built and present in MSBuild's internal result cache, provided either by the project cache plugin or real builds. A consequence of this is that projects which are not well-described in the graph (e.g. using `<MSBuild>` tasks directly) will not benefit from the cache.

### Cache get scenario

- For each [`BuildRequestData`](/src/Build/BackEnd/BuildManager/BuildRequestData.cs#L83) ([`ProjectInstance`](/src/Build/Instance/ProjectInstance.cs#L71), Global Properties, Targets) submitted to the [`BuildManager`](/src/Build/BackEnd/BuildManager/BuildManager.cs#L38), MSBuild asks the plugin whether to build the request or not.

  - If the `BuildRequestData` is based on a project path instead of a `ProjectInstance`, the project is evaluated by the `BuildManager`.
- If the plugin decides to build, then MSBuild proceeds building the project as usual.
- If the plugin decides to skip the build, it needs to return back to MSBuild the target results that the build request would have produced. It can either provide the results directly, or instruct MSBuild to run a set of less expensive targets on the projects with the same effect as the expensive targets ("proxy targets").
  - MSBuild injects the `BuildResult` into its Scheduler, so that future projects that need to call into the skipped project have the target results they need served directly from MSBuild's internal cache.
  - Plugins have three options:
    - Worst: plugins fake the build results for each target based on assumptions about how the target executes. We consider this brittle since the plugins will have to be updated whenever the build logic changes.
    - Better: plugins tell MSBuild to run a proxy target as a replacement for the expensive target (e.g. it tells MSBuild to run `GetTargetPath` and use those results for the `Build` target). See the [ProjectReference protocol](/documentation/ProjectReference-Protocol.md) for more details.
      - Proxy target assumptions:
        - They are very fast and only retrieve items and properties from the evaluated state (like `GetTargetPath`).
        - They do not mutate state (file system, environment variables, etc).
        - They do not MSBuild task call into other projects.
      - The BuildManager schedules the proxy targets to build on the in-proc node to avoid `ProjectInstance` serialization costs.
    - Best: A real `BuildResult` from a previous build is provided. This can either be done by serializing the `HandleProjectFinishedAsync`, or when the plugin's infrastructure (e.g. CloudBuild or AnyBuild builder nodes) runs and caches the build, it can tell MSBuild to serialize the BuildResult to a file via [BuildParameters.OutputResultsCacheFile](/src/Build/BackEnd/BuildManager/BuildParameters.cs#L767) or the `/outputResultsCache` command line argument. Then, on cache hits, the plugins deserialize the `BuildResult` and send it back to MSBuild. This is the most correct option, as it requires neither guessing nor proxy targets. Whatever a previous build did, that's exactly what's returned.
      - Potential Issue: serialization format may change between writing and reading the `BuildResult`, especially if binary serialization is used.

### Cache add scenario

- Upon a cache miss, MSBuild will generally handle a request as normal, ie by building it.
- MSBuild uses [Detours](https://github.com/microsoft/Detours) to observe file accesses of the worker nodes. To facilitate the plugin being able to handle future builds, it forwards this information as well as the build result to the plugin for it to use as desired, for example to add to a cache.
  - This functionality has some implementation restrictions so will require additional opt-in. Specifically, the `/ReportFileAccesses` command-line flag or by setting `BuildParameters.ReportFileAccesses` for programmatic use of `BuildManager`. If this is not set, no file accesses will be reported to the plugin, however the plugin will still be notified of the build result.
  - The in-proc node is disabled since MSBuild is unable to use Detours on the currently running process. It also would not want to capture the file accesses of the plugins themselves.
  - Detours adds some overhead to file accesses. Based on initial experimentation, it's around 10-15%. There's the overhead of the plugin adding to the cache. Caching becomes valuable if it can save more than the overhead on average.
- Due to the experimental nature of the feature, `/ReportFileAccesses` is only available with MSBuild.exe (ie. the Visual Studio install; not `dotnet`), only for the x64 flavor (not x86 or arm64), and only from the command-line. The Visual Studio IDE does not set `BuildParameters.ReportFileAccesses`.
- As described above, it is recommended to serialize the `BuildResult` from `HandleProjectFinishedAsync` for later replay.

## APIs and calling patterns

### Plugin API

[ProjectCachePluginBase](/src/Build/BackEnd/Components/ProjectCache/ProjectCachePluginBase.cs) is an abstract class which plugin implementors will subclass.

See the [Plugin implementation guidance and simple example design](#plugin-implementation-guidance-and-simple-example-design) section for guidance for plugin implementations.

## Configuring plugins

Plugins may need configuration options provided by the user. This can be done via metadata on the `ProjectCachePlugin` item:

```xml
<ItemGroup>
  <ProjectCachePlugin Include="$(SomePath)\MyAmazingCachePlugin.dll">
    <PluginSetting1>$(PluginSetting1)</PluginSetting1>
    <PluginSetting2>$(PluginSetting2)</PluginSetting2>
    <PluginSetting3>$(PluginSetting3)</PluginSetting3>
  </ProjectCachePlugin>
</ItemGroup>
```

This can then be accessed by the plugin in `BeginBuildAsync` as a dictionary via `CacheContext.PluginSettings`.

Note: As it is likely that plugins will be distributed through NuGet packages and those packages would define the `ProjectCachePlugin` item in a props or targets file in the package, it's recommended for plugin authors to have settings backed by MSBuild properties as in the example above. This allows the user to easily configure a plugin simply by setting the properties and including the `PackageReference`.

### Enabling from command line

- Requires `/graph` to light up cache get scenarios.
- Requires `/reportfileaccesses` to light up cache add scenarios.
- The static graph has all the project instances in the same process, making it easy to find and keep plugin instances in one process.
- MSBuild constructs the static graph and build bottom up, so by the time a project is considered, all of its references and their build results are already present in the Scheduler.

### Enabling from Visual Studio, a temporary workaround

- Ideally, Visual Studio would provide a `ProjectGraph` instance. Until that happens, a workaround is needed.
- The workaround logic activates only when MSBuild detects that it's running under VS.
- When VS evaluates projects via `new Project(..)` (it does this on all the solution projects on solution load), the evaluator will search for and store all detected plugins in a static field on the `BuildManager`.
- The first build request will check the static state for the presence of plugins. If there's a plugin, it will initialize it at that point.
- Plugins will be given the graph entry points instead of the entire graph in this scenario.
- There is currently no way to enable cache add scenarios in Visual Studio.

## Detours (cache add scenario)

In order for MSBuild to observe the file accesses as part of the build, it uses Detours on the worker nodes. In this way the Scheduler node will emit events for all file accesses done by the worker nodes. As the Scheduler knows what build request a worker node is working on at any given moment, it is able to properly associate the file access with a build request and dispatch these augmented events to plugins via the plugins' `HandleFileAccess` and `HandleProcess` implementations.

Note that the Scheduler node cannot use Detours on itself, so the in-proc node is disabled when repoting file accesses. Additionally task yielding is disabled since it would leave to improperly associated file accesses.

### Pipe synchronization

Because the Detours implementation being used communicates over a pipe, and nodes communicate over a pipe as well, and pipes are async, there is some coordination required to ensure that file accesses are associated with the proper build request. For example, if a "project finished" signal comes through the node communication pipe, but the detours pipe still has a queue of file accesses which have not been processed yet, those file accesses might be processed after the worker node has moved onto some other project.

To address this problem, when a worker node finishes a project it will emit a dummy file access with a specific format known to MSBuild. When the scheduler node receives as "project finished" event over the node communication pipe, it will wait to determine that the project is actually finished until it also receives the dummy file access. This ensures that the all file accesses associated with the project have fully flushed from the pipe before the scheduler determines the project is finished and schedules new work to the worker node (which would trigger new file accesses).

## Plugin implementation guidance and simple example design

The following will describe a very basic (and not very correct) plugin implementation.

In practice, plugins will have to choose the specific level of correctness they're willing to trade off for the ability to get cache hits. Any machine state *could* impact build results, and the plugin implementation will need to determine what state matters and what doesn't. An obvious example to consider would be the content of the project file. An example which has trade-offs would be the processes' environment variables. Even the current time could possibly impact the build ("if Tuesday copy this file"), but if considered caching would be quite infeasible.

### Fingerprinting

A "fingerprint" describes each unique input which went into the building a build request. The more granular the fingerprint, the more "correct" the caching is, as described above.

In this example, we will only consider the following as inputs, and thus part of the fingerprint:

- The global properties of the build request (eg `Configuration=Debug`, `Platform=AnyCPU`)
- The content hash of the project file
- The content hash of files defined in specific items we know contribute to the build, like `<Compile>` and `<Content>`
- The fingerprint of referenced projects

Again, this is for illustrative purposes and a real implementation will want to use additional state for fingerprinting depending on the environment in which it runs and the correctness requirements.

It can make sense for a fingerprint to be a hash of its inputs, so effectively is a byte array which can be represented by a string.

At the beginning of the build, the plugin's `BeginBuildAsync` method will be called. As part of the `CacheContext`, the plugin is either given the graph or the entry points to the graph for which it can create a graph from. The plugin can use this graph to do some initial processing, like predicting various inputs which a project is likely to use. This information can then be stored to help construct a fingerprint for a build request later.

### Cache storage

Any storage mechanism can be used as a cache implementation, for example [Azure Blob Storage](https://azure.microsoft.com/products/storage/blobs/), or even just the local filesystem. At least in this example the only real requirement is that it can be used effectively as a key-value store. In many cases it can be useful for content to be keyed by its hash, and for the metadata file to be keyed by the fingerprint. In particular when content is keyed by hash, it is effectively deduplicated across multiple copies of the same file, which is common in builds.

For illustration purposes, consider our cache implementation is based on a simple filesystem with a separate metadata and content directory inside it. Under the metadata dir, each file is a metadata file where the filename matches the fingerprint it's describing. Under the content dir, each file is a content file where the filename matches the hash of the content itself.

### First build (cache population)

In the very first build there will be no cache hits so the "cache add" scenario will be most relevant here.

For a given project, `GetCacheResultAsync` will be invoked, but will end up returning a cache miss since the cache is empty.

MSBuild will then build the project normally but under a detoured worker node. Because of this, the plugin will recieve `HandleFileAccess` and `HandleProcess` events. In this example implementation we will ignore `HandleProcess`. For `HandleFileAccess`, the plugin will simply store all `FileAccessData`s for a `FileAccessContext` to build up a list of all file accesses during the build. The plugin may decide to avoid storing the entire `FileAccessData` and instead just peel off the data it finds relevant (eg. paths, whether it was a read or write, etc).

Once MSBuild is done building the project, it will call the plugin's `HandleProjectFinishedAsync`. Now the plugin knows the project is done and can process the results and add them to a cache. In general it's only useful to cache successful results, so the plugin should filter out non-success results. The `FileAccessContext` provided can then be used to retrieve the list of `FileAccessData` the plugin recieved. These `FileAccessData` can be processed to understand which files were read and written as part of the build.

In our example, we can use the read files to construct a fingerprint for the build request. We can then add the files written during the build ("outputs") to some cache implementation.

The plugin would then create some metadata describing the outputs (eg. the paths and hashes) and the serialized `BuildResult`, and associate it with the fingerprint and put that assocation in the cache.

To illustrate this, consider a project with fingerprint `F` which wrote a single file `O` with hash `H` and had `BuildResult R`. The plugin could create a metadata file `M` which describes the outputs of the build (the path and hash of `O`) as well as the serialized `R`. Using the cache implementation described above, the plugin would write the following two files to the cache:

- `metadata/F -> M:"{outputs: [{path: 'path/to/O', hash: H}], result: R}"`
- `content/H -> O`

This can then be used for future builds.

### Second Build (cache hits)

In the second build we have a populated cache and so it could be possible to get cache hits.

For a given project, `GetCacheResultAsync` will be invoked. The plugin can fingerprint the request and use that fingerprint to look up in its cache. If the cache entry exists, it can declare a cache hit.

In the example above, if all inputs are the same as in the first build, we should end up with a fingerprint `F`. We look up in the metadata part of the cache (file `metadata/F`) and find that it exists. This means we have a cache hit. We can fetch that metadata `M` from the cache and find that it describes the output with path `O` and hash `H`. The plugin would then copy `content/H` to `O` and return the deserialized `BuildResult R` contained in `M` to MSBuild.

If the inputs were not the same as in the first build, for example if a `Compile` item (a .cs file) changed, the fingerprint would be something else besides `F` and so would not have corresponding cache entries for it, indicating a cache miss. This will then go through the "cache add" scenario described above to populate the cache with the new fingerprint.

## Caveats

- Without the "cache add" scenario enabled, the content which powers "cache get" must be populated by some external entity, for example some higher-order build engine.
- Absolute paths circulating through the saved build results
  - Absolute paths will likely break the build, since they'd be captured on the machine that writes to the cache.
  - Plugins can attempt to normalize well-known paths, like the repo root, but this can be brittle and there may be unknown path types.
- Slow connections. In a coffee shop it might be faster to build everything instead of downloading from the cache. Consider racing plugin checks and building: if the bottom up build traversal reaches a node that's still querying the cache, cancel the cache query and build the node instead.
- Inferring what targets to run on each node when using /graph
  - Msbuild /graph requires that the [target inference protocol](static-graph.md#inferring-which-targets-to-run-for-a-project-within-the-graph) is good enough.
- Small repos will probably be slower with plugin implementations that access the network. Remote distribution and caching will only be worth it for repos that are large enough.

## Potential future work of dubious value

- Enable plugins to work with the just-in-time top down msbuild traversal that msbuild natively does when it's not using `/graph`.
- Extend the project cache API to allow skipping individual targets or tasks instead of entire projects. This would allow for smaller specialized plugins, like plugins that only know to distribute, cache, and skip CSC.exe calls.
