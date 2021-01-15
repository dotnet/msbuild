- [Summary](#summary)
- [Motivation](#motivation)
- [Plugin requirements](#plugin-requirements)
- [High-level design](#high-level-design)
- [APIs and calling patterns](#apis-and-calling-patterns)
  - [From BuildManager API users who have a project dependency graph at hand and want to manually issue builds for each graph node in reverse topo sort order.](#from-buildmanager-api-users-who-have-a-project-dependency-graph-at-hand-and-want-to-manually-issue-builds-for-each-graph-node-in-reverse-topo-sort-order)
  - [From command line](#from-command-line)
  - [From Visual Studio, a temporary workaround](#from-visual-studio-a-temporary-workaround)
- [Details](#details)
- [Caveats](#caveats)
- [Future work](#future-work)
- [Potential work of dubious value](#potential-work-of-dubious-value)

# Summary

Project cache is a new assembly-based plugin extension point in MSBuild which determines whether a build request (a project) can be skipped during build. The main expected benefit is reduced build times via [caching and/or distribution](https://github.com/dotnet/msbuild/blob/master/documentation/specs/static-graph.md#weakness-of-the-old-model-caching-and-distributability).

# Motivation

As the introduction to [static graph](https://github.com/dotnet/msbuild/blob/master/documentation/specs/static-graph.md#what-is-static-graph-for) suggests, large and complex repos expose the weaknesses in MSBuild's scheduling and incrementality models as build times elongate. This project cache plugin lets MSBuild natively communicate with existing tools that enable build caching and/or distribution, enabling true scalability.

Visual Studio is one beneficiary. This plugin inverts dependencies among build systems: instead of higher level build engines ([Cloudbuild](https://www.microsoft.com/research/publication/cloudbuild-microsofts-distributed-and-caching-build-service/), [Anybuild](https://github.com/AnyBuild/AnyBuild), [BuildXL](https://github.com/microsoft/BuildXL), etc) calling into MSBuild, MSBuild calls into them, keeping MSBuild's external APIs and command line arguments largely unchanged and thus reusable by Visual Studio.

This change also simplifies and unifies user experiences. MSBuild works the same from Visual Studio or the command line without dramatically changing how it works.

# Plugin requirements

- The plugin should tell MSBuild whether a build request needs building. If a project is skipped, then the plugin needs to ensure that:
  - it makes the filesystem look as if the project built
  - it returns sufficient information back to MSBuild such that MSBuild can construct a valid [BuildResult](https://github.com/dotnet/msbuild/blob/d39f2e4f5f3d461bc456f9abed9adec4a2f0f542/src/Build/BackEnd/Shared/BuildResult.cs#L30-L33) for its internal scheduling logic, such that future requests to build a skipped project are served directly from MSBuild's internal caches.

# High-level design
- For each [BuildRequestData](https://github.com/dotnet/msbuild/blob/d39f2e4f5f3d461bc456f9abed9adec4a2f0f542/src/Build/BackEnd/BuildManager/BuildRequestData.cs#L83) ([ProjectInstance](https://github.com/dotnet/msbuild/blob/d39f2e4f5f3d461bc456f9abed9adec4a2f0f542/src/Build/Instance/ProjectInstance.cs#L71), Global Properties, Targets) submitted to the [BuildManager](https://github.com/dotnet/msbuild/blob/d39f2e4f5f3d461bc456f9abed9adec4a2f0f542/src/Build/BackEnd/BuildManager/BuildManager.cs#L38), MSBuild asks the plugin whether to build the request or not.
  - If the BuildRequestData is based on a project path instead of a ProjectInstance, the project is evaluated by the BuildManager.
- If the plugin decides to build, then MSBuild proceeds building the project as usual.
- If the plugin decides to skip the build, it needs to return back to MSBuild the target results that the build request would have produced. It can either provide the results directly, or instruct MSBuild to run a set of less expensive targets on the projects with the same effect as the expensive targets.
  - MSBuild injects the BuildResult into its Scheduler, so that future projects that need to call into the skipped project have the target results they need served directly from MSBuild's internal cache.
- Plugin dlls are discovered by MSBuild via a new special purpose `ProjectCachePlugin` [items](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-items).
  - These items can get injected into a project's import graph by package managers via the [PackageReference](https://docs.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) item.
  - MSBuild will discover the plugin by searching project evaluations for `ProjectCachePlugin` items.
- Plugin instances reside only in the BuildManager node. Having it otherwise (plugin instances residing in all nodes) means forcing the plugins to either deal with distributed state or implement a long lived service. We consider this high complexity cost to not be worth it. We also want to avoid serializing the ProjectInstance between nodes, which is expensive.
- The plugin instance will get called in reverse topo sort order (from dependencies up towards dependents). Building in reverse topo sort order is common between Visual Studio solution builds and higher build engines.
- Plugins can function with and without a static graph. When a static graph is not provided, hints about the graph entry points are provided (details in Defining the "graph" when static graph is not available).
- A single plugin is supported (for now).

# APIs and calling patterns
- Plugin APIs are found [here](https://github.com/cdmihai/msbuild/tree/projectCache/src/Build/BackEnd/Components/ProjectCache).

## From BuildManager API users who have a project dependency graph at hand and want to manually issue builds for each graph node in reverse topo sort order.
- Users set [BuildParameters.ProjectCacheDescriptor](https://github.com/cdmihai/msbuild/blob/projectCache/src/Build/BackEnd/Components/ProjectCache/ProjectCacheDescriptor.cs) which triggers MSBuild to instantiate the plugin and call `ProjectCacheBase.BeginBuildAsync` on it in `BuildManager.BeginBuild`.
  - `BuildManager.BeginBuild` does not wait for the plugin to initialize. The first query on the plugin will wait for plugin initialization.
- All the build requests submitted in the current `BuildManager.BeginBuild/EndBuild` session will get checked against the plugin instance.
- Only the user provided top level build requests are checked against the cache. The build requests issued recursively from the top level requests are not checked against the cache, since it is assumed that users issue build requests in reverse toposort order. Therefore when a project builds its references, those references should have already been built and present in MSBuild's internal cache, provided either by the project cache plugin or real builds.
- `BuildManager.EndBuild` calls `ProjectCacheBase.EndBuildAsync`.
- There is no static graph instantiated by MSBuild in this case and the user needs to set `ProjectCacheDescriptor.EntryPoints`.

## From command line
- Requires /graph. It is the easiest way to implement the plugin:
  - The static graph has all the project instances in the same process, makes it easy to find and keep plugin instances in one process.
  - Builds bottom up, so by the time a project is considered, all of its references and their build results are already present in the Scheduler.
- User calls msbuild /graph.
- MSBuild constructs the static graph.
- The graph builder finds and loads the plugin into the `BuildManager`.
  - Each graph node has a ProjectInstance, which is used to search for the plugin.
  - If a project defines a plugin, then all projects in the graph must define that same plugin.
  - The `ProjectGraph` is passed to the plugin upon initialization, so the plugin can avoid building its own static graph (in case it needs a graph).
- From this point on the calling patterns are similar to the `BuildParameters.ProjectCacheDescriptor` flow described [above](#from-buildmanager-api-users-who-have-a-project-dependency-graph-at-hand-and-want-to-manually-issue-builds-for-each-graph-node-in-reverse-topo-sort-order). The only difference is that the plugin is not instantiated in `BuildManager.BeginBuild`, but between graph construction and graph build.
  - However, if `BuildParameters.ProjectCacheDescriptor` was set and a plugin was instantiated, it will take precedence. In this case graph build will not even search the graph nodes for plugins.

## From Visual Studio, a temporary workaround
- Ideally, Visual Studio would use the [above APIs](#from-buildmanager-api-users-who-have-a-project-dependency-graph-at-hand-and-want-to-manually-issue-builds-for-each-graph-node-in-reverse-topo-sort-order) to set project cache plugins. Since VS evaluates all projects in a solution, it could search for `ProjectCachePlugin` items and provide them back to MSBuild during real builds via `BuildParameters.ProjectCacheDescriptor`. Until that happens, a workaround will be used:
  - The workaround logic activates only when MSBuild detects that it's running under VS.
  - Plugin discovery
    - When VS evaluates projects via "new Project(..)" (it does this on all the solution projects on solution load), the evaluator will search for and store all detected plugins in a static field on the `BuildManager`.
  - Plugin usage:
    - The first build request will check the static state for the presence of plugins. If there's a plugin, it will instantiate it via plugin.BeginBuild.

# Details
- Plugin discovery
  - Each project defines an item containing the path to the plugin DLL:
```xml
<ProjectCachePlugin Include="..\..\QuickbuildProjectCachePlugin.dll">
```
- Plugin acquisition
  - Via the dependency manager of choice. PackageReference / Nuget for managed projects, pacman / vcpkg / nuget on packages.config for C++. The package contents injects the plugin item into the project import graph.
- Defining the "graph" when static graph is not available
  - Plugins need to know the top level entry point for various reasons, but without a static graph the entry points need to be explicitly declared or inferred.
  - Entry points are set via `ProjectCacheDescriptor.EntryPoints`.
    - The Visual Studio workaround will use the `SolutionPath` global property as the graph entry point.
- Returning a valid BuildResult object on cache hits.
  - On cache hits, MSBuild skips the project, but needs a BuildResult with target results to send back to the [Scheduler](https://github.com/dotnet/msbuild/blob/d39f2e4f5f3d461bc456f9abed9adec4a2f0f542/src/Build/BackEnd/Components/Scheduler/Scheduler.cs#L25).
  - Plugins have three options:
    - Worst: plugins fake the build results for each target. We consider this brittle since the plugins will have to be updated whenever the build logic changes.
    - Better: plugins tell MSBuild to run a proxy target as a replacement for the expensive target (e.g. it tells MSBuild to run `GetTargetPath` and use those results for the Build target). See the [ProjectReference protocol](https://github.com/dotnet/msbuild/blob/master/documentation/ProjectReference-Protocol.md) for more details.
      - Proxy target assumptions:
        - They are very fast and only retrieve items and properties from the evaluated state (like `GetTargetPath`).
        - They do not mutate state (file system, environment variables, etc).
        - They do not MSBuild task call into other projects.
      - The BuildManager schedules the proxy targets to build on the in proc node to avoid ProjectInstance serialization costs.
    - Best: when the plugin's infrastructure (e.g. cloudbuild or anybuild builder nodes) runs and caches the build, it can tell MSBuild to serialize the BuildResult to a file via [BuildParameters.OutputResultsCacheFile](https://github.com/dotnet/msbuild/blob/d39f2e4f5f3d461bc456f9abed9adec4a2f0f542/src/Build/BackEnd/BuildManager/BuildParameters.cs#L767) or the `/outputResultsCache` command line argument. Then, on cache hits, the plugins deserialize the BuildResult and send it back to MSBuild. This is the most correct option, as it requires neither guessing nor proxy targets. Whatever the build did, that's what's returned.
      - This is not yet possible. Outputting results cache files needs to first be decoupled from `/isolate`.
      - Potential Issue: serialization format may change between runtime msbuild and the cache results file, especially if binary serialization is used.
- Configuring plugins
  - Plugin configuration options can be provided as metadata on the `ProjectCachePlugin` item.
```xml
<ProjectCachePlugin Update="@(ProjectCachePlugin)" setting1="val1" setting2="val2" />
```
- Configuring MSBuild to query the caches but not do any builds (bin-place from the cache without building anything):
  - From command line: `msbuild /graph:NoBuild`
  - From APIs: `GraphBuildRequestData.GraphBuildRequestDataFlags.{NoBuild}`
- Logging
  - Log messages from `Plugin.{BeginBuild, EndBuild}` do not have a parent build event context and get displayed at the top level in the binlog.
  - Log messages from querying a project get parented under that project's logging context.
    - This is not yet implemented. For now, all plugin log messages do not have a parent event context.

# Caveats
- Absolute paths circulating through the saved build results
  - Absolute paths will likely break the build, since they'd be captured on the machine that writes to the cache.
- Slow connections. In a coffee shop it might be faster to build everything instead of downloading from the cache. Consider racing plugin checks and building: if the bottom up build traversal reaches a node that's still querying the cache, cancel the cache query and build the node instead.
- Inferring what targets to run on each node when using /graph
  - Msbuild /graph requires that the [target inference protocol](https://github.com/dotnet/msbuild/blob/master/documentation/specs/static-graph.md#inferring-which-targets-to-run-for-a-project-within-the-graph) is good enough.
- Small repos will probably be slower with plugin implementations that access the network. Remote distribution and caching will only be worth it for repos that are large enough.

# Future work
- On cache misses plugins can build the project with IO monitoring and write to the local cache. As far as we can tell there are two main possibilities:
  - plugins build the projects themselves in isolation (without projects building their reference, probably by setting `BuildProjectReferences` to false) by calling msbuild.exe.
  - plugins request msbuild to build the projects on special out of proc nodes whose IO system calls can be monitored.

# Potential work of dubious value
- Allow multiple plugin instances and query them based on some priority, similar to sdk resolvers.
- Enable plugins to work with the just-in-time top down msbuild traversal that msbuild natively does when it's not using `/graph`.
- Extend the project cache API to allow skipping individual targets or tasks instead of entire projects. This would allow for smaller specialized plugins, like plugins that only know to distribute, cache, and skip CSC.exe calls.
