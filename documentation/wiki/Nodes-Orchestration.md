Transcript of a talk with Rainer - [internal link] [recording](https://msit.microsoftstream.com/video/fde00840-98dc-ba75-0ebd-f1ed9ca0e097)

## Need for multiple processes
MSBuild is multiprocess system today.
Multiple processes are used in few scenarios:

1) **TaskHost** - allowing to run back compatible tasks and MSBuild plugins requiring different runtime.
Task declares (in [UsingTask](https://learn.microsoft.com/en-us/visualstudio/msbuild/usingtask-element-msbuild)) what environment it expects, and if it's not compatible with current configuration (today for VS that's x64 net4) - it'll be isolated.

    [GenerateResource](https://learn.microsoft.com/en-us/visualstudio/msbuild/generateresource-task) task uses this (used to use this).

   `TaskHost` is supported so far, but performance is not closely watched.

   Currently, [MSBuild running on .NET Core cannot run tasks compiled against the full desktop .NET environment](https://github.com/dotnet/msbuild/issues/711). Analogously, [.NET core tasks cannot be run from Visual Studio](https://github.com/dotnet/msbuild/issues/4834).

2) **Parallel builds** - needed since tasks can access process wide state - namely current working dir, environment vars. Those can change between projects (especially [`Compile Include`](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-items) often contains relative path, without specifying `$MSBuildProjectDirectory` - so it relies on current directory being the location of the project file). For this reason node in parallel build can run only one task at a time.


## Communication
In a presence of multiple processes we need interprocess communication.

### Messages (de)serialization

Communication messages should deriver from [`ITranslatable`](https://github.com/dotnet/msbuild/blob/main/src/Shared/ITranslatable.cs) - it dictates the both direction of serialization via single method - [`void Translate(ITranslator translate)`](https://github.com/dotnet/msbuild/blob/main/src/Shared/ITranslatable.cs#L16)

Majority of translations use custom binary serialization, there is though backfall to [`TranslateDoteNet`](https://github.com/dotnet/msbuild/blob/main/src/Shared/ITranslator.cs#L257) method that uses `BinaryFormatter`.

Event args use different type of serialization - a `CreateFromStream` and `WriteToStream` methods are discovered via reflection and used to serialize type (with few exceptions explicitly translated within [`LogMessagePacketBase`](https://github.com/dotnet/msbuild/blob/main/src/Shared/LogMessagePacketBase.cs)).

### Transport

Endpoints (nodes) communicate via named pipes (Windows or named pipes API implementation on other plaforms). Communication is facilitated via [`NodeProviderOutOfProcBase`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Communications/NodeProviderOutOfProcBase.cs)

The validation of transport is done via [proprietary handshake](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Communications/NodeProviderOutOfProcBase.cs#L501-L508).


## Orchestration

MSBuild consist of nodes. First spun is so called **entrypoint node**. It runs a **scheduler**. Then there are **worker nodes** - those can only execute projects. Nodes are spun by [`NodeLauncher`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Communications/NodeLauncher.cs) - this is being called from [`NodeProviderOutOfProcBase.GetNodes`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Communications/NodeProviderOutOfProcBase.cs#L186) (which can decide to reuse existing node or start a new process), that is ultimately called by [`NodeManger`](https://github.com/dotnet/msbuild/blob/main/src/Deprecated/Engine/Engine/NodeManager.cs).

`NodeManager` is a build component (`IBuildComponent`) - so it can be retrieved from build engine (via `IBuildComponentHost.GetComponent`).

Node is described by [`NodeInfo`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Communications/NodeInfo.cs).

MSBuild can be started from existing process via API, or via MSBuild.exe - in both cases this process becomes a `scheduler node` and may or may not run other work in-process. By default the main process has as well a `worker node` (project build node). This can be tweaked by API and/or [environment variables](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#configure-msbuild-in-the-net-cli).

When MSBuild start building a project it can eventually start processing MSBuild task that has [`BuildInParallel`](https://learn.microsoft.com/en-us/visualstudio/msbuild/building-multiple-projects-in-parallel-with-msbuild?view=vs-2022) set to `true` (which is a default) and multiple projects to build (e.g. a project with multiple `ProjectReference` elements) at that point `scheduler` orchestrates multiple `worker nodes` via `NodeManager` (upper limited via maximum parallelization - configurable via API or CLI (`-maxcpucount|-m`)) .

Bugs in node communication layer can manifest as a slow build - otherwise fully functional. As `NodeManager` is trying to setup new nodes (and failing) and `scheduler` is working with only a single (in-proc) node that it has.

Work unit for nodes is a `project instance` - a project together with set of glabal properties that make the project unique.

----
**Example:** 

Multitargeted project (`TargetFrameworks=x;Y`) - this will generate 'outer-build' - a project with no global properties set; and 'inner build' for each `TargetFramework` (so one instance with `TargetFramework=X`, `TargetFramework=Y`). All those are distinct - so can be scheduled on separate nodes (in practice the outer build is scheduled on a node, hits the `ResolveProjectReferences` that will produce the two projects for particular `TargetFramework` - one is scheduled on the same node, other one waits for a different node to be available/spun).

----

MSBuild scheduler maintains a list of projects that are eligible to run (not blocked) and list of free worker nodes (plus knows a mapping of projects already mapped to particular nodes) and maps the work. [It performs some heuristics](https://github.com/dotnet/msbuild/blob/7cfb36cb90d1c9cc34bc4e0910d0c9ef42ee47b6/src/Build/BackEnd/Components/Scheduler/Scheduler.cs#L776-L783) prioritizing work that is likely to request more work (e.g. dir.proj files).

## Scheduler assumptions

Node in parallel build can run only one task at a time (task can explicitly temporarily vacate the node via `IBuildEngine.Yield`, or this can implicitly happen when MSBuild discovers dependencies on tasks that have not run yet)

Once a `project instance` is assigned to a worker node - it is locked to that node (and cannot be run on another one). Above 2 facts can lead to scheduling issues (priorities inversions, blocking).

Scheduler can (opt-in) dump a graph of dependencies from last build into a text file and then use it in the next build (with option of [various scheduling algorithms](https://github.com/dotnet/msbuild/blob/7cfb36cb90d1c9cc34bc4e0910d0c9ef42ee47b6/src/Build/BackEnd/Components/Scheduler/Scheduler.cs#L833))

Another mode of building is `graph build` - where project is build only once all its dependencies are resolved (so the build graph needs to be known and unchanged upfront).


