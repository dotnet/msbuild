- [Quick intro](#quick-intro)
- [Language interpretation parts](#language-interpretation-parts)
- [MSBuild APIs and components](#msbuild-apis-and-components)
- [MSBuild execution modes](#msbuild-execution-modes)

# Quick intro
MSBuild implements an actual language (... MSBuildian?). For syntax it uses XML. The XML elements and attributes represent keywords, variable names, and expressions in the language. The language is interpreted.

MSBuild has two data structures:
- [properties](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties): single valued, scalars. Like `string foo` in C#
- [items](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-items): multi valued, arrays. Like `string[] foo` in C#. Except that each array has a name called an `item-type`, and each element may not only have a value, but also have associated key-value pairs known as metadata.

Typewise, everything is a string in MSBuild.

Executing logic is grouped in [Targets](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets). They are like functions in C#. Targets can contain a single type of statement, [Tasks](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-tasks). Targets form dependencies between themselves and are executed via a stack, similar to stack based function execution in other languages.

Data structures (properties and items) can be declared either inside or outside targets. There is a single scope in MSBuild, you could call it the static scope. There's no automatic "target scopes". So if you declare a property or an item inside a target, it's still static and visible to the rest of the targets.

# Language interpretation parts 

The parser, which produces the AST: [ProjectParser](https://github.com/dotnet/msbuild/blob/bd00d6cba24d41efd6f54699c3fdbefb9f5034a1/src/Build/Evaluation/ProjectParser.cs#L125). The AST top node is the [ProjectRootElement](https://github.com/dotnet/msbuild/blob/bd00d6cba24d41efd6f54699c3fdbefb9f5034a1/src/Build/Construction/ProjectRootElement.cs#L44)

Interpretation happens in two big separate phases.

The first interpretation phase is called **project evaluation**. It's done by the msbuild [Evaluator](https://github.com/dotnet/msbuild/blob/main/src/Build/Evaluation/Evaluator.cs#L52). The result of evaluation is an object tree similar (but different) to the symbol trees done by [compilers](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/semantic-analysis#understanding-compilations-and-symbols). The semantic top nodes are `ProjectInstance` and `Project`. You can sort of think of the `ProjectRootElement` as the syntax API and the `Project` / `ProjectInstance` as the semantic API. The big difference in MSBuild is that `ProjectInstance`/`Project` also contain the actual interpretation results of MSBuild's state (properties and items) that sits outside targets.

Evaluation does not execute the targets in a project. It only interprets and stores the results of logic outside targets.

The second phase of msbuild interpretation is target execution. This happens in the [TargetBuilder](https://github.com/dotnet/msbuild/blob/bd00d6cba24d41efd6f54699c3fdbefb9f5034a1/src/Build/BackEnd/Components/RequestBuilder/TargetBuilder.cs#L100). The `TargetBuilder` uses a stack to execute targets. The initial state is the state contained inside a given `ProjectInstance`. So targets execute in a stack based manner and mutate the global state inside a `ProjectInstance`.

What's the difference between `Project` and `ProjectInstance`? While both represent evaluated projects, they are intended for different use cases. `Project` objects are specialized in introspecting / analyzing MSBuild code and also in providing high level project editing operations. `ProjectInstance` objects are  read only. So the objects in the `Project` tree point back to their corresponding `ProjectRootElement` AST elements. The objects in the `ProjectInstance` tree do not point back to the `ProjectRootElement` elements (so they have a much smaller memory footprint). For example, the `Project` tree is used by Visual Studio to analyze msbuild projects, and to reflect UI changes all the way down to the XML elements. The `TargetBuilder` only works with the lighter weight `ProjectInstance` tree, since it only needs to read state.

# MSBuild APIs and components
- `Project` / `ProjectInstance`: entrypoint APIs for working with MSBuild evaluations.
- `BuildManager`: entrypoint API for executing targets in MSBuild.
  - - Project build stack: RequestBuilder -> TargetBuilder -> TaskBuilder. The RequestBuilder is responsible for evaluating and running targets on a project. The TargetBuilder is responsible for running the target execution stack. The TaskBuilder is responsible for running individual tasks.
- MSBuild nodes
  - MSBuild distributes work across multiple processes.
  - All processes live on the same machine.
  - Types of nodes:
    - A main node, called the "BuildManager node" or the "Scheduler node". It contains BuildManager instances and its main role is to coordinate the entire build. It's a big state machine that decides when to create other nodes and on which nodes to build projects.
    - Multiple worker nodes whose single responsibility is to build projects. A worker node builds only one project at a time (99% true).
  - The process hosting the single BuildManager node can also host one worker node (called the "inproc node"). This is done for perf reasons, to avoid serialization costs. When the BuildManager node decides to spawn a new Worker node, it creates a new process for it. These are called out of process nodes, or "oop nodes". So Worker nodes can either be inproc nodes (a single one), or oop nodes (multiple).

# MSBuild execution modes
- Building from cmdline (type msbuild.exe)
    - MSBuild is responsible for discovering project dependencies, and building them in a depth first search manner
    - There is no static graph of project dependencies. MSBuild does not know ahead of time what work it needs to do. It does a Just In Time DFS descent over the project references.
    - references form a DAG. A -> {B, C}, B -> {C, D}
    - there's a convention on how to declare and build references
      - documented here: https://github.com/dotnet/msbuild/blob/main/documentation/ProjectReference-Protocol.md
      - mostly implemented here: https://github.com/dotnet/msbuild/blob/main/src/Tasks/Microsoft.Common.CurrentVersion.targets
- Building from Visual Studio (VS) (pressing ctrl+shift+B, or the context menu `Build -> Build Solution`)
    - VS implements an object model for an MSBuild dependency graph
    - VS takes over the responsibility of handling the graph dependencies and constructs its own graph
    - It bypasses MSBuild's scheduler and "orders" MSBuild to build a single project at a time, ignoring its references
    - It builds the graph bottom up in reverse toposort order (starting from nodes without references and going up).
    - Fast up to date checks:  VS hardcodes relevant project file inputs for each language SDK. (for example it knows that C# projects read global.json). It uses these hardcoded rules to skip building projects. (whereas quickbuild does this in a generic non hardcoded way by observing actual IO inputs and outputs)
- Build from commandline with static graph (type msbuild.exe /graph)
  - this works just like QB and VS: MSBuild uses its [static graph library](https://github.com/dotnet/msbuild/blob/main/documentation/specs/static-graph.md) to find the dependency graph then builds each project node from the graph in toposort order. Unlike QB, it does neither multi machine distribution nor caching