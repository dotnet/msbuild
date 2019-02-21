- [Static Graph](#static-graph)
  - [Overview](#overview)
    - [Motivations](#motivations)
  - [Project Graph](#project-graph)
    - [Build dimensions](#build-dimensions)
    - [Building a project graph](#building-a-project-graph)
    - [Inferring which targets to run for a project within the graph](#inferring-which-targets-to-run-for-a-project-within-the-graph)
    - [Underspecified graphs](#underspecified-graphs)
    - [Public API](#public-api)
  - [Isolated builds](#isolated-builds)
    - [Isolated graph builds](#isolated-graph-builds)
    - [Single project isolated builds](#single-project-builds)
  - [Distribution](#distribution)
    - [Serialization](#serialization)
    - [Deserialization](#deserialization)
  - [I/O Tracking](#io-tracking)
    - [Detours](#detours)
    - [Isolation requirement](#isolation-requirement)
    - [Tool servers](#tool-servers)

# Static Graph

## Overview

### Motivations
- Stock projects can build with "project-level build" and if clean onboard to MS internal build engines with cache/distribution
- Stock projects will be "project-level build" clean.
- Add determinism to MSBuild w.r.t. project dependencies. Today MSBuild discovers projects just in time, as it finds MSBuild tasks. This means there’s no guarantee that the same graph is produced two executions in a row, other than hopefully sane project files. With the static graph, you’d know the shape of the build graph before the build starts.
- Potential perf gain in graph scheduling
- Increase perf of interaction between MSBuild and higher-order build engines (eg. MS internal build engines) - reuse evaluation and MSBuild nodes
- **Existing functionality must still work. This new behavior is opt-in only.**

## Project Graph

Calculating the project graph will be very similar to the MS internal build engine's existing Traversal logic. For a given evaluated project, all project references will be identified and recursively evaluated (with deduping).

A node in the graph is a tuple of the project file and global properties. Each (project, global properties) combo can be evaluated in parallel.

### Build dimensions

Build dimensions can be thought of as different ways to build a particular project. For example, a project can be built Debug or Retail, x86 or x64, for .NET Framework 4.7.1 or .NET Core 2.0.

For multitargeting projects (.NET projects which have multiple `TargetFrameworks`), the graph node will be just as 1 uber node. Behaviorally this is the same as what happens when building a multitargeting project directly today; the outer build ends up calling the MSBuild task for every target framework.

For example if project A multitargets `net471` and `netcoreapp2.0`, then the graph will have just 1 node: project A + no global properties.

For project references to projects which multitarget, it is non-trivial to figure out which target framework to build without running NuGet targets, so as a simplification the dependency will be on the multitargeting node in the graph (ie. "all target frameworks"), rather than the individual target framework which is actually needed. Note that this may lead to over-building when building an entire graph. However, Visual Studio also exhibits this same behavior today.

For example if project B targeted `net471` and had a project reference to the example project A above, then project B would depend on project A (without global properties). However when building project B from the command-line, project A would only build its `net471` (project A + global properties of `TargetFramework=net471`) variant, and the `netcoreapp2.0` variant would not actually be built.

Because we are using ProjectReferences to determine the graph, we will need to duplicate the mapping of ProjectReference metadata to global properties given to the MSBuild task. This means that we need to couple the engine to the common MSBuild targets, which means that whenever we change those targets we need to update the project graph construction code as well.

The graph also supports multiple entry points, so this enables scenarios where projects build with different platforms in one build. For example, one entry point could be a project with global properties of `Platform=x64` and another entry point might be that same project with `Platform=x86`. Because the project graph understands the metadata on project references as well, including `GlobalPropertiesToRemove`, this also enables the notion of "platform agnostic projects" which should only build once regardless of the platform.

For example, if project A had a project reference to project B with `GlobalPropertiesToRemove=Platform`, and we wanted to build project A for x86 and x64 so used both as entry points, the graph would consist of 3 nodes: project A with `Platform=x86`, project A with `Platform=x64`, and project B with no global properties set.

To summarize, there are two main patterns for build dimensions which are handled:
1. The project builds itself multiple times like multitargeting. This will be seen as a single graph node
2. A different set of global properties are used to choose the dimension like with Configuration or Platform. The project graph supports this via multiple entry points.

Note that multiple entry points will not useable from the command-line due to the inability to express it concisely, and so programatic access must be used in that case.

**OPEN ISSUE:** What about MSBuild calls which pass in global properties not on the ProjectReference? Worse, if the global properties are computed at runtime? The sfproj SDK does this with PublishDir, which tells the project where to put its outputs so the referencing sfproj can consume them. We may need to work with sfproj folks and other SDK owners, which may not be necessarily a bad thing since already the sfproj SDK requires 2 complete builds of dependencies (normal ProjectReference build + this PublishDir thing) and can be optimized. A possible short term fix: Add nodes to graph to special case these kinds of SDKs. This is only really a problem for isolated builds.

Note that graph cycles are disallowed, even if they're using disconnected targets. This is a breaking change, as today you can have two projects where each project depends on a target from the other project, but that target doesn't depend the default target or anything in its target graph.

### Building a project graph
When building a graph, project references should be built before the projects that reference them, as opposed to the existing msbuild scheduler which builds projects just in time.

For example if project A depends on project B, then project B should build first, then project A. Existing msbuild scheduling would start building project A, reach an MSBuild task for project B, yield project A, build project B, then resume project A once unblocked.

Building in this way should make better use of parallelism as all CPU cores can be saturated immediately, rather than waiting for projects to get to the phase in their execution where they build their project references. More subtly, less execution state needs to be held in memory at any given time as there are no paused builds waiting to be unblocked. Knowing the shape of the graph may be able to better inform the scheduler to prevent scheduling projects that could run in parallel onto the same node.

### Inferring which targets to run for a project within the graph
In the classic traversal, the referencing project chooses which targets to call on the referenced projects and may call into a project multiple times with different target lists and global properties (examples in [project reference protocol](../ProjectReference-Protocol.md)). When building a graph, where projects are built before the projects that reference them, we have to determine the target list to execute on each project statically.

To do this, we need to explicitly describe the project-to-project calling patterns in such a way that a graph build can infer the entry targets for a project.

Each project specifies the project reference protocol targets it supports, in the form of a target mapping: `target -> targetList`. The `target` represents the project reference entry target, and the `targetList` represents the list of targets that `target` ends up calling on each referenced project.

For example, a simple recursive rule would be `A -> A`, which says that a project called with target `A` will call target `A` on its referenced projects. Here's an example execution with two nodes:

```
Execute target A+-->Proj1   A->A
                    +
                    |
                    | A
                    |
                    v
                    Proj2   A->A
```

Proj1 depends on Proj2, and we want to build the graph with target `A`. Proj1 gets inspected for the project reference protocol for target `A` (represented to the right of Proj1). The protocol says the referenced projects will be called with `A`. Therefore Proj2 gets called with target `A`. After Proj2 builds, Proj1 then also builds with `A` because Proj1 is an entry point and `A` is what was requested by the user.

A project reference protocol may contain multiple targets, for example `A -> B, A`. This means that building `A` on the referencing project will lead to `B` and `A` getting called on the referenced projects. If all nodes in the graph repeat the same rule, then the rule is repeated recursively on all nodes. However, a project can choose to implement the protocol differently. In the following example, the entry targets are:
- Proj4 is called with targets `B, A, C, D`. On multiple references, the incoming targets get concatenated. The order of these target lists does not matter, as MSBuild has non-deterministic p2p ordering, however the order within the target lists does. IE. `B, A, C, D` and `C, D, B, A` are valid, while `A, B, C, D` is not.
- Proj3 and Proj2 get called with `B, A`, as specified by the rule in Proj1.
- Proj1 builds with `A`, because it's the root of the graph.

```
            A+-->Proj1   A->B, A
                 /    \
           B, A /      \ B, A
               /        \
              v          v
  A->B, A   Proj3      Proj2   A->C, D
             \            /
        B, A  \          /  C, D
               \        /
                \      /
                 v    v
                 Proj4   A->B, A
```

The common project reference protocols (Build, Rebuild, Restore, Clean) will be specified by the common props and targets file in the msbuild repository. Other SDKs can implement their own protocols (e.g. ASPNET implementing Publish).

Here are the rules for the common protocols:

`Build -> GetTargetFrameworks, <default>, GetNativeManifest, GetCopyToOutputDirectoryItems`

The default target (represented in this spec's pseudo protocol representation as `<default>`) is resolved for each project.

`Clean -> GetTargetFrameworks, Clean`

`Rebuild -> GetTargetFrameworks, Clean, <default>, GetNativeManifest, GetCopyToOutputDirectoryItems`

`Rebuild` actually calls `Clean` and `Build`, which in turn uses the concatenation of the `Clean` and `Build` mappings. `GetTargetFrameworks` is repeated so only the first call to it remains in the final target list.

Restore is a composition of two rules:
- `Restore -> _IsProjectRestoreSupported, _GenerateRestoreProjectPathWalk, _GenerateRestoreGraphProjectEntry`
- `_GenerateRestoreProjectPathWalk -> _IsProjectRestoreSupported, _GenerateRestoreProjectPathWalk, _GenerateRestoreGraphProjectEntry`

**Open Issue:** Restore is a bit complicated, and we may need new concepts to represent it. The root project calls the recursive `_GenerateRestoreProjectPathWalk` on itself to collect the referenced projects closure, and then after the recursion call returns (after having walked the graph), it calls the other targets on each returned referenced project in a non-recursive manner. So the above protocol is not a truthful representation of what happens, but it correctly captures all targets called on each node in the graph.

We'll represent the project reference protocols as `ProjectReferenceTargets` items in MSBuild. For extensibility, the target list for the core mappings will be stored as properties so that users can append to it, but the `ProjectReferenceTargets` item ultimately is what will actually be read by the ProjectGraph.

```xml
<PropertyGroup>
  <ProjectReferenceTargetsForClean>GetTargetFrameworks;Clean</ProjectReferenceTargetsForClean>
</PropertyGroup>

<ItemGroup>
  <ProjectReferenceTargets Include="Clean" Targets="$(ProjectReferenceTargetsForClean)"/>
</ItemGroup>
```

### Underspecified graphs
The intention is that the project graph and the target lists for each node be exactly correct, however MSBuild is quite flexible and particular projects or project types may not adequately describe these for the project graph.

If a project calls into another project which either isn't represented in the graph or with a target list which isn't represented by the graph, it will fall back to classical MSBuild behavior and execute that target on the project reference just-in-time. This has the consequence of still requiring all project state be kept in memory in case any arbitrary project wants to execute targets on any other arbitrary project.

To enable further optimizations (and strictness), graph builds can run [isolated](#isolated-builds) which enforces that the graph be entirely accurate.

### Public API
This is a proposal for what the public API for ProjectGraph may look like:

```csharp
namespace Microsoft.Build.Experimental.Graph
{
    public class ProjectGraph
    {
        // Creates a graph starting at the given project file.
        public ProjectGraph(string projectFile) { }
        public ProjectGraph(string entryProjectFile, IDictionary<string, string> globalProperties) { }

        // Creates a graph starting at the given project files.
        public ProjectGraph(IEnumerable<string> projectFiles) { }
        public ProjectGraph(IEnumerable<string> entryProjectFiles, IDictionary<string, string> globalProperties) { }

        // Creates a graph starting at the given entry point(s). An entry point is a (project file, global properties) pair.
        public ProjectGraph(ProjectGraphEntryPoint entryPoint) { }
        public ProjectGraph(IEnumerable<ProjectGraphEntryPoint> entryPoints) { }

        /* Also various constructor overloads which take a ProjectCollection */

        // Nodes for the provided entry points
        IReadOnlyCollection<ProjectGraphNode> EntryPointNodes { get; }

        // All project nodes in the graph.
        IReadOnlyCollection<ProjectGraphNode> ProjectNodes { get; }
    }

    public struct ProjectGraphEntryPoint
    {
        public ProjectGraphEntryPoint(string projectFile) { }

        public ProjectGraphEntryPoint(string projectFile, IDictionary<string, string> globalProperties) { }

        // The project file to use for this entry point
        public string ProjectFile { get; }

        // The global properties to use for this entry point
        public IDictionary<string, string> GlobalProperties { get; }
    }

    public class ProjectGraphNode
    {
        // No public creation.
        internal ProjectGraphNode() { }

        // Projects which this project references.
        IReadOnlyCollection<ProjectGraphNode> ProjectReferences { get; }

        // Projects which reference this project.
        IReadOnlyCollection<ProjectGraphNode> ReferencingProjects { get; }

        // The evaluated project
        Project Project { get; }
    }
}
```

## Isolated builds
Building a project in isolation means that any build results for project references must be pre-computed and provided as input.

If a project uses the MSBuild task, the build result must be in MSBuild's build result cache instead of just-in-time executing targets on that referenced project. If it is not in the build result cache, an error will be logged and the build will fail. If the project is calling into itself either via `CallTarget` or the MSBuild task with a different set of global properties, this will be allowed to support multitargeting and other build dimensions implemented in a similar way.

Because referenced projects and their entry targets are guaranteed to be in the cache, they will not build again. Therefore we do not need to set `/p:BuildProjectReferences=false` or any other gesture that tells SDKs to not do recursive operations.

**Open Issue:** With this approach initial targets will also get included due to how the `RequestBuilder` composes entry targets and how `TargetBuilder` reports build results. Is this an issue? Is it just a perf optimization to take them out from the cache? It seems like target mapping incompleteness at the very least, since if the project changes its initial targets, the cache hit from the referencing project would suddenly be a miss.

**Open Issue:** How do we differentiate between targets called on the outer build project and targets called on the inner build projects? For multitargeting builds, a project will actually reach into the inner build, but the graph expresses a dependency on the outer build.

### Isolated graph builds
When building a graph in isolated mode, the graph is used to traverse and build the projects in the right order, but each individual project is built in isolation. The build result cache will just be in memory exactly as it is today, but on cache miss it will error. This enforces that both the graph and target mappings are complete and correct.

Furthermore, running in this mode enforces that each (project, global properties) pair is executed only once and must execute all targets needed by all projects which reference that node. This gives it a concrete start and end time, which leads to some perf optimizations, like garbage collecting all project state (except the build results) once it finishes building. This can greatly reduce the memory overhead for large builds.

This discrete start and end time also allows for easy integration with [I/O Tracking](#io-tracking) to observe all inputs and outputs for a project. Note however that I/O during target execution, particular target execution which may not normally happen as part of a project's individual build execution, would be attributed to the project reference project rather the project with the project reference. This differs from today's behavior, but seems like a desirable difference anyway.

### Single project isolated builds
When building a single project in isolation, all project references' build results must be provided to the project externally. Specifically, the results will need to be [deserialized](#deserialization) from files and loaded into the build result cache in memory.

Because of this, single project isolated builds is quite restrictive and is not intended to be used directly by end-users. Instead the scenario is intended for higher-order build engines which support caching and [distribution](#distribution).

There is also the possibility for these higher-order build engines and even Visual Studio to enable extremely fast incremental builds for a project. For example, when all project references' build results are provided (and validated as up to date by that higher-order build engine), there is no need to evaluate or execute any targets on any other project.

These incremental builds can even be extended to multiple projects by keeping a project graph in memory as well as the last build result for each node and whether that build result is valid. The higher-order build engine can then itself traverse the graph and do single project isolated builds for projects which are not currently up to date.

## Distribution
To support distribution for isolated builds (eg. for the MS internal build engine), we need a solution for a project and a dependency building on different machines. To facilitate this, we need to serialize the build result cache to disk on the source machine which built the project reference, and deserialize it on the destination machine which is about to build the project referencing the project reference.

In fact, this same mechanism can be used for non-distributed builds as well, for example for incremental builds. The caller would still need to ensure that the build result cache was valid (ie. the project reference project was not changed since the build result cache for it was produced), but the benefit would be that only the current project being built would need to be evaluated and only targets on that project would need to execute.

### Serialization
**WIP**

### Deserialization
**WIP**

## I/O Tracking
To help facilitate caching of build outputs by a higher-order build engine, MSBuild needs to track all I/O that happens as part of a build.

**OPEN ISSUE:** This isn't actually true in most scenarios. Today the MS internal build engine can wrap any arbitrary process to track the I/O that happens as part of its execution as well as its children. That's sufficient for all scenarios except compiler servers or an MSBuild server (see below). Additionally, if the MS internal build engine supports any other build type besides MSBuild (or older versions of MSBuild), it will still need to be able to detour the process itself anyway.

**NOTE**: Based on the complexity and challenges involved, the feature of I/O tracking in MSBuild is currently on hold and not scheduled to be implemented. This section intends to describe these challenges and be a dump of the current thinking on the subject.

### Detours
[Detours](https://github.com/microsoft/detours) will be used to intercept Windows API calls to track I/O. This is the same technology that [FileTracker](../../src/Utilities/TrackedDependencies/FileTracker.cs) and [FullTracking](../../src/Build/BackEnd/Components/RequestBuilder/FullTracking.cs) use as well as what the MS internal build engine ("BuildXL Tracker") uses to track I/O.

Today FileTracker and FullTracking are currently a bit specific to generating tlogs, and do not collect all the I/O operations we would want to collect like directory enumerations and probes. Additionally, the BuildXL Tracker implementation does not currently have the ability to attach to the currently running process.

Either existing implementation would require some work to fit this scenario. Because FileTracker/FullTracking aren't actively being improved unlike BuildXL Tracker, we will likely be adding the necessary functionality to BuildXL Tracker.

Elsewhere in this spec the final Detours-based file tracking implementation will simply be referred to as "Tracker".

### Isolation requirement
I/O Tracking will only be available when running isolated builds, as the current implementation of project yielding in MSBuild makes it exceedingly difficult to attribute any observed I/O to the correct project. Isolated builds make this feasible since each MSBuild node will be building exactly one project configuration at any given moment and each project configuration has a concrete start and stop time. This allows us to turn on I/O tracking for the MSBuild process and start and stop tracking with the project start and stop.

**OPEN ISSUE:** For graph-based isolated builds, project evaluation happens in parallel on the main node. Any I/O that happens as part of evaluation should be reported for that specific project, but there's no good way to do that here.

### Tool servers
Tool servers are long-lived processes which can be reused multiple times across builds. This causes problems for Tracker, as that long-lived process is not a child process of MSBuild, so many I/O operations would be missed.

For example, when `SharedCompilation=true`, the Roslyn compiler (csc.exe) will launch in server mode. This causes the `Csc` task to connect to any existing csc.exe process and pass the compilation request over a named pipe.

To support this scenario, a new MSBuild Task API could be introduced which allows build tasks which interact with tool servers to manually report I/O. In the Roslyn example above, it would report reads to all assemblies passed via `/reference` parameters, all source files, analyzers passed by `/analyzer`, and report writes to the assembly output file, pdb, and xml, and any other reads and writes which can happen as part of the tool. Effectively, the task will be responsible for reporting what file operations the tool server may perform for the reqest it makes to it. Note that the tool server may cache file reads internally, but the task should still report the I/O as if the internal cache was empty.

**OPEN ISSUE:** Analyzers are just arbitrary C# code, so there's no guarantees as to what I/O it may do, leading to possibly incorrect tracking.

Similarly for a theoretical server mode for MSBuild, MSBuild would need to report its own I/O rather than the higher-order build engine detouring the process externally. For example, if the higher-order build engine connected to an existing running MSBuild process to make build requests, it could not detour that process and so MSBuild would need to report all I/O done as part of a particular build request.

**OPEN ISSUE:** As described above in an open issue, tool servers are the only scenario which would not be supportable by just externally detouring the MSBuild process. The amount of investment required to enable tool servers is quite high and spans across multiple codebases: MSBuild needs to detour itself, MSBuild need to expose a new Tasks API, the `Csc` task needs to opt into that API, and the higher-order build engine needs to opt-in to MSBuild reporting its own I/O, as well as detecting that the feature is supported in the version of MSBuild it's using. Tool servers may add substantial performance gain, but the investment is also substantial.
