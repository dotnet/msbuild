# Static Graph

## Overview

### Motivations
- Stock projects can build with "project-level build" and if clean onboard to MS internal build engines with cache/distribution
- Stock projects will be "project-level build" clean.
- Add determinism to MSBuild
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

**OPEN ISSUE:** What about MSBuild calls which pass in global properties not on the ProjectReference? Worse, if the global properties are computed at runtime? The sfproj SDK does this with PublishDir, which tells the project where to put its outputs so the parent sfproj can consume them. We may need to work with sfproj folks and other SDK owners, which may not be necessarily a bad thing since already the sfproj SDK requires 2 complete builds of dependencies (ProjectReference + this PublishDir thing) and can be optimized. A possible short term fix: Add nodes to graph to special case these kinds of SDKs.

Note that we are not taking into account different target lists. Since we're going down the route of export targets (see below), there's no need to track the specific targets which projects need to call in the p2p relationship. This is important to call out though since this means that the project graph feature may not be very usable outside of the export targets feature, meaning the features may be somewhat coupled.

Also note that graph cycles are disallowed, even if they're using disconnected targets. This is a breaking change, as today you can have two projects where each project depends on a target from the other project, but that target doesn't depend the default target or anything in its target graph.

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

## Export Targets
The idea behind the export targets feature is that it changes the behavior of the MSBuild task to always pull the target results from cache rather than actually evaluating and executing the list targets. This has several benefits such as each project having a concrete lifetime within the build, may reduce re-evaluation cost, and opens up maybe caching and distribution scenarios.

### Syntax
- Option 1: <ExportTarget Name="GetTargetPath" />.
  - Pros: Easy to search, very extensible (can export targets which you don't "own")
  - Cons: Separated from the target definition, breaking change
- Option 2: <Target Name="GetTargetPath" Exported="true">
  - Pros: Near target definition, mostly reuses existing syntax
  - Cons: Not as extensible, breaking change
- Option 3: <Project DefaultTargets="Build" ExportTargets="GetTargetPath">
  - Pros: Easy to find (ish. Could be defined in imported props/targets), similar to DefaultTargets and InitialTargets, not a breaking change
  - Cons: Separated from the target definition
- Option 4: Property: $(MSBuildExportedTargets)
  - Pros: Easy to use and query, completely non-breaking
  - Cons: Bad actors can override instead of append

**Option 4** seems like the best choice.

### Building using export targets
When using export targets, the project graph is required to build the projects in the correct order. An individual project will be built using `/p:BuildProjectReferences=false` to avoid recursive builds inside a project

**OPEN ISSUE:** Can we get away with not setting `/p:BuildProjectReferences=false`? It would mean we'd need to export Build, Rebuild, and Clean, but would eliminate any need for setting extra global properties. Perhaps this is fine, or perhaps the entry point for a project is always considered exported?

When building a project, run the initial targets, default targets, and export targets. Export targets will run last, assuming they're not needed earlier in the build by other targets. Likely the implementation of this will simply be to push the export targets onto the target execution stack in reverse order before the default and initial targets.

After execution, the results of the export targets will be cached and the project can be disposed. Note that this gives project execution a concrete start and end, while previously projects needed to remain active just in case any other project needed to execute a target on it.

For projects which depend on other projects, the MSBuild task will look up the target result in the cache. If the target is missing from the cache, an error will be logged and the build will fail. If the project is calling into itself either via `CallTarget` or via `MSBuild` with a different set of global properties, this will be always allowed (support for cross-targeting).

This makes the P2P contract explicit as it requires both projects to declare targets which are part of the contract.

Initially this result cache will just be in-memory, similar to the existing MSBuild target result cache, but eventually could persist across builds so that an incremental build of a particular project in the project graph would not require re-evaluation or any target execution in that project's dependencies. This may have significant perf improvements for incremental builds.

Note that because projects have a concrete start and end time, this allows for easy integration with Tracker to observe all inputs and outputs for a project. Note however that export target I/O would be attributed to the dependency (project doing the exporting), not the dependent project (project with the project reference). This differs from today's behavior, but seems like a desirable difference anyway.

## Distribution
To support distribution with export targets (eg. for the MS internal build engine), we need a solution for a project and a dependency building on different machines.
- Option 1 (easier): Run export targets only for dependencies, no build.
  - Requires re-evaluation of the dependency, but no big MSBuild support beyond allowing the no-Build mode for dependencies.
- Option 2 (better): Serialize the export target state
  - Best for perf, but requires a serialization protocol and a way to specify how to import the state
  - Possibly same mechanism as the persistent cross-build cache described above.

## I/O Tracking
**WIP**