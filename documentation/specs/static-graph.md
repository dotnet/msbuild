- [Static Graph](#static-graph)
  - [Overview](#overview)
    - [Motivations](#motivations)
    - [M1 - MVP](#m1---mvp)
    - [M1 non-goals](#m1-non-goals)
    - [M2 - Single Project Enforcement](#m2---single-project-enforcement)
    - [M3 - Dependency Tracking](#m3---dependency-tracking)
    - [M4 - Ready for Caching/Distribution](#m4---ready-for-cachingdistribution)
    - [M5 - Performance](#m5---performance)
  - [Project Graph](#project-graph)
    - [Building a project graph](#building-a-project-graph)
    - [Public API](#public-api)
  - [Inferring which targets to run for a project within the graph](#inferring-which-targets-to-run-for-a-project-within-the-graph)
  - [Building a single project](#building-a-single-project)
  - [Distribution](#distribution)
  - [I/O Tracking](#io-tracking)

# Static Graph

## Overview

### Motivations
- Stock projects can build with "project-level build" and if clean onboard to MS internal build engines with cache/distribution
- Stock projects will be "project-level build" clean.
- Add determinism to MSBuild w.r.t. project dependencies. Today MSBuild discovers projects just in time, as it finds MSBuild tasks. This means there’s no guarantee that the same graph is produced two executions in a row, other than hopefully sane project files. With the static graph, you’d know the shape of the build graph before the build starts.
- Potential perf gain in graph scheduling
- Increase perf of interaction between MSBuild and higher-order build engines (eg. MS internal build engines) - reuse evaluation and MSBuild nodes
- **Existing functionality must still work. This new behavior is opt-in only.**

### M1 - MVP
- [ ] Add new project graph discovery
  - [ ] Evaluation and statically follow ProjectReference.
  - [ ] Graph and fully-evaluated instances returned
  - [ ] Nodes in the graph:
    - [ ] List of edges
    - [ ] Instances of evaluated project

- [ ] BuildSingleProject API:
  - [ ] Build a project/target with /p:BuildProjectReferences=false
  - [ ] Warn on MSBuild invocation in this mode
- [ ] Simple BuildGraph API added
- [ ] Gather data on migration costs and how far off we are from a usable solution

### M1 non-goals
- Caching
- Distribution
- Input/Output tracking or validation
- Incremental build
- Perf (no multi-core, evaluation re-use, etc)
- Blocking "bad" P2P calls

### M2 - Single Project Enforcement
- [ ] BuildSingleProject
  - [ ] New syntax to allow a project to export state to consuming projects
  - [ ] Modify common targets to, by default, export state for P2P protocol data (probably non-Core first)
  - [ ] Error on MSBuild invocations not exported by target Project

### M3 - Dependency Tracking
- [ ] BuildSingleProject
  - [ ] Add Tracker to record input/outputs read for each project built
  - [ ] Some sort of content store for this data?

### M4 - Ready for Caching/Distribution
- [ ] BuildGraph API
  - [ ] Validate project outputs read as inputs are declared by a ProjectReference
  - [ ] Other BuildCop stuff?
- [ ] API to import/export tracker data and exported project state
- [ ] Ability to distribute and cache from QB

### M5 - Performance
- [ ] Optimized graph scheduling
- [ ] Evaluation re-use
- [ ] Scoped evaluation (parse only what we need)

## Project Graph

Calculating the project graph will be very similar to the MS internal build engine's existing Traversal logic. For a given evaluated project, all project references will be identified and recursively evaluated (with deduping). Each project + global props combo can be evaluated in parallel.

For cross-targeting projects, the project will be seen just as 1 uber project. Behaviorally this is the same as what happens when building a cross-targeting project directly today; the outer build ends up calling the MSBuild task for every target framework.

For example if project A cross targets net471 and netcoreapp2.0, then the graph will have just 1 node: project A + no global properties.

For project references to projects which cross-target, it is non-trivial to figure out which target framework to build without running NuGet targets, so as a simplification the dependency will be on the cross-targeting node in the graph (ie. "all target frameworks"), rather than the individual target framework which is actually needed. Note that this may lead to over-building.

For example if project B had a project reference to the example project A above, then project B would depend on project A (without global properties).

Because we are using ProjectReferences to determine the graph, we will need to duplicate the mapping of ProjectReference metadata to global properties given to the MSBuild task. This means that we need to couple the engine to the concept of cross-targeting and how it's done. Which means that whenever we change the cross-targeting MSBuild code we need to update the project graph construction code.

**OPEN ISSUE:** Are there other "build dimensions" similar to cross-targeting? Cross-targeting has a well-known pattern, but there may be others. Like runtime id (e.g. win10, win8, osx, linux14, linux16, etc), which give TFM x RID combinations. We wouldn't want to teach the engine how to handle each type of build dimension. Even if we have to hardcode details about certain build dimensions (to avoid having a ton of sdk writers to change), should we bother designing a way in which sdk writers can declare how new build dimensions affect the graph? To avoid adding more and more coupling to build logic?

**OPEN ISSUE:** What about MSBuild calls which pass in global properties not on the ProjectReference? Worse, if the global properties are computed at runtime? The sfproj SDK does this with PublishDir, which tells the project where to put its outputs so the referencing sfproj can consume them. We may need to work with sfproj folks and other SDK owners, which may not be necessarily a bad thing since already the sfproj SDK requires 2 complete builds of dependencies (ProjectReference + this PublishDir thing) and can be optimized. A possible short term fix: Add nodes to graph to special case these kinds of SDKs.

Note that graph cycles are disallowed, even if they're using disconnected targets. This is a breaking change, as today you can have two projects where each project depends on a target from the other project, but that target doesn't depend the default target or anything in its target graph.

### Building a project graph
There are conceptually two options when building a project graph: building the entire graph or building a single project in the graph. There is one other scenario, which is building a part of the graph (a project and everything it needs), but that's effectively just rooting the graph at that project and building that whole new graph.

When building a whole graph, we can think of it as just building the root project recursively. So really the two scenarios break down to just building the root project recursively or not, which is actually very similar to how "msbuild" vs "msbuild /p:BuildProjectReferences=false" works today.

Additionally, since building a graph is effectively just building a project recursively, then invoking MSBuild with a target list should behave similarly to how to behaves today. The top-level project will be invoked with those targets, and project references won't be affected by that target list. However, targets like "Clean" and "Rebuild" will need to be special-cased, as they recursively call those targets on their project references.

Short-term (M1/MVP), building a graph is the same behaviorally as building the root project (no export target enforcement). The main difference will be in scheduling. Instead of dependency projects being evaluated and executed just in time, MSBuild will schedule dependencies before projects which depend on them. In most cases when building a graph in this order, all MSBuild task executions will come from the MSBuild result cache, which leads fairly cleanly into export targets, which would effectively be the enforcement of that.

**OPEN ISSUE:** What do we do about properties provided by the SLN? Under the hood, this is effectively different sets of global properties provided to each MSBuild invocation (or ProjectReference?) in the metaproj.

### Public API ###
This is a proposal for what the public API may look like:

    namespace Microsoft.Build.Graph
    {
        public class ProjectGraph
        {
            // Creates a graph starting at the given project file.
            public ProjectGraph(string projectFile) { }

            // Creates a graph starting at the given project files, for example all projects in a solution.
            public ProjectGraph(IEnumerable<string> projectFiles) { }

            // All project nodes in the graph.
            IReadOnlyCollection<ProjectGraphNode> ProjectNodes { get; }

            // Build the graph with the given target, if any
            BuildResult Build(string target = null);
        }

        public class ProjectGraphNode
        {
            // No public creation.
            internal ProjectGraphNode() { }

            // Projects which this project references.
            IReadOnlyCollection<ProjectGraphNode> ReferencedProjects { get; }

            // Projects which reference this project.
            IReadOnlyCollection<ProjectGraphNode> ReferencingProjects { get; }

            // The evaluated project
            Project Project { get; }
        }
    }

## Inferring which targets to run for a project within the graph

One property of the static build graph is that a project (project, globalproperties, toolsversion) is executed only once when the project graph is executed. This is in conflict with current P2P patterns (project-to-project) where a project may be called multiple times with different targets and global properties (examples in [p2p protocol](https://github.com/Microsoft/msbuild/blob/master/documentation/ProjectReference-Protocol.md)).

During a static graph based build projects are built before the projects that reference them, whereas the classical msbuild scheduler builds projects just in time. In the classic traversal, the referencing project chooses which targets to call on the referenced projects. But in the static graph traversal, we don't know the entry targets for a reference because we visit the references before the referencing projects.

We need to represent project-to-project calling patterns in such a way that a graph build can infer the entry targets for a project. These protocols apply to an entire graph. They can be regular or irregular. **Regular** operations consist of recursively calling the same target on all referenced projects (e.g. build, clean, rebuild). **Irregular** operations consist of calling different targets on different nodes in the graph (e.g. publish calls publish on the root node but build on the rest). As a complication, today nothing prevents projects to turn regular operations into irregular operations (e.g. a project file reimplements Clean by calling Clean2 on referenced projects).

Some project reference protocols require multiple calls (MSBuild task calls) between the referencing project and referenced project. Therefore we need to not only specify the entry targets, but also the helper targets.

Therefore the project reference protocols will need to be explicitly described. Each project specifies the project reference protocol targets it supports, in the form of a target mapping:

$target \to targetList$

$target$ represents the project reference entry target, and $targetList$ represents the list of targets that $target$ ends up calling on each referenced project.

For example, a simple recursive rule would be $A \to A$, which says that a project called with target $A$ will call target $A$ on its referenced projects. Here's an example execution with two nodes:

```
Execute target A+-->Proj1   A->A
               +
               |
               | A
               |
               v
             Proj2   A->A
```

Proj1 depends on Proj2, and we want to build the graph with target $A$. Proj1 gets inspected for the project reference protocol for target $A$ (represented to the right of Proj1). The protocol says the referenced projects will be called with $A$. Therefore Proj2 gets called with target $A$. After Proj2 builds, Proj1 then also builds with $A$ (because Proj1 is the root of the graph).

A project reference protocol could contain multiple targets, for example $A \to B, A$. This means that building $A$ on the referencing project will lead to $B$ and $A$ getting called on the referenced projects. If all nodes in the graph repeat the same rule, then the rule is repeated recursively on all nodes (this would be a regular operation). However, a node can choose to implement the protocol differently, which would lead to an irregular protocol. In the following example, the entry targets are:
- Proj4 is called with targets $B, A, C, D$. On multiple references, the incoming targets get concatenated. The order does not matter, as MSBuild has non-deterministic p2p ordering.
- Proj3 and Proj2 get called with $B, A$, as specified by the rule in Proj1.
- Proj1 builds with A, because it's the root of the graph.

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

$Build \to GetTargetFrameworks, \langle default \rangle, GetNativeManifest, GetCopyToOutputDirectoryItems$

The default target (represented in this spec's pseudo protocol representation as $\langle default \rangle$) is resolved for each project.

$Clean \to GetTargetFrameworks, Clean$

$Rebuild \to \$Clean, \$Build$

$Rebuild$ maps to two referenced protocols. $\$Clean$ and $\$Build$ get substituted with the contents of the $Clean$ and $Build$ protocols.

Restore is a composition of two rules:
- $Restore \to \_IsProjectRestoreSupported, \_GenerateRestoreProjectPathWalk, \_GenerateRestoreGraphProjectEntry$

- $\_GenerateRestoreProjectPathWalk \to \_IsProjectRestoreSupported, \_GenerateRestoreProjectPathWalk, \_GenerateRestoreGraphProjectEntry$

**Open Issue:** Restore is a bit complicated, and we may need new concepts to represent it. The root project calls the recursive $\_GenerateRestoreProjectPathWalk$ on itself to collect the referenced projects closure, and then after the recursion call returns (after having walked the graph), it calls the other targets on each returned referenced project in a non-recursive manner. So the above protocol is not a truthful representation of what happens, but it correctly captures all targets called on each node in the graph.

**Open Issue:** How do we differentiate between targets called on the outer build project and targets called on the inner build projects?

We'll represent the project reference protocols as items in MSBuild. The target list will be stored as properties for ease of extensibility.
```xml
<PropertyGroup>
  <ProjectReferenceTargetsForClean>GetTargetFrameworks;Clean</ProjectReferenceTargetsForClean>
</PropertyGroup>

<ItemGroup>
  <ProjectReferenceTargets Include="Clean" Targets="$(ProjectReferenceTargetsForClean)"/>
</ItemGroup>
```

## Building a single project
The project graph is required to build the projects in the correct order. An individual project will be built using `/p:BuildProjectReferences=false` to avoid recursive builds inside a project.

**OPEN ISSUE:** Can we get away with not setting `/p:BuildProjectReferences=false`?

When building a project, run the initial targets and the entry targets as computed by the [project reference protocol](#inferring-which-targets-to-run-for-a-project-within-the-graph).

After execution, the results of the entry targets will be cached in the BuildManager and the project can be disposed. Note that this gives project execution a concrete start and end, while previously projects needed to remain active just in case any other project needed to execute a target on it.

For projects which depend on other projects, the MSBuild task will look up the target result in the cache. If the target is missing from the cache, an error will be logged and the build will fail. If the project is calling into itself either via `CallTarget` or via `MSBuild` with a different set of global properties, this will be always allowed (support for cross-targeting).

Initially this result cache will just be in-memory, similar to the existing MSBuild target result cache, but eventually could persist across builds so that an incremental build of a particular project in the project graph would not require re-evaluation or any target execution in that project's dependencies. This may have significant performance improvements for incremental builds.

Note that because projects have a concrete start and end time, this allows for easy integration with Tracker to observe all inputs and outputs for a project. Note however that export target I/O would be attributed to the dependency (project doing the exporting), not the dependent project (project with the project reference). This differs from today's behavior, but seems like a desirable difference anyway.

## Distribution
To support distribution with export targets (eg. for the MS internal build engine), we need a solution for a project and a dependency building on different machines.
- Option 1 (easier): Run export targets only for dependencies, no build.
  - Requires re-evaluation of the dependency, but no big MSBuild support beyond allowing the no-Build mode for dependencies.
- Option 2 (better): Serialize the export target state
  - Best for performance, but requires a serialization protocol and a way to specify how to import the state
  - Possibly same mechanism as the persistent cross-build cache described above.

## I/O Tracking
**WIP**
