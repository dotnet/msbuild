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

### Handshake types

The internal C# 15 `Handshake` union contains five protocol types:

| Type | Installation identity | Session and compatibility rules |
| --- | --- | --- |
| `WorkerNodeHandshake` | Current tools directory and Framework assembly file version | Includes the Windows session and resolved change wave. |
| `ServerNodeHandshake` | Current tools directory and Framework assembly file version | Uses session zero. Its key omits the session, and its first-byte check remains disabled. RAR uses this same protocol. |
| `NetTaskHostHandshake` | SDK tools directory and version sentinel `99` | Includes the Windows session but excludes change waves. Preserves the existing architecture and drive-letter tolerance. |
| `FrameworkTaskHostHandshake` | Current installation, or explicit host tools directory and file version | Includes the Windows session but excludes change waves. Supports CLR4 hosts from another Visual Studio installation. |
| `Clr2TaskHostHandshake` | Task-host executable directory and file version | Includes the Windows session but excludes change waves. Matches the standalone CLR2 host protocol. |

`TaskHostHandshake` is a separate union that contains only the three task-host types.
Task-host configuration options cannot name a different runtime or node kind.
Worker handshakes derive their configuration from the current process.

`CommunicationsUtilities` provides purpose-specific factories instead of a general `GetHandshakeOptions` API.
Worker, server, RAR, and child task-host factories return the required handshake type directly.
Parent task-host factories return runtime-specific configuration structs.
Each struct's `CreateHandshake` method returns its concrete handshake type.

CLR4 and CLR2 configurations accept the resolved host tools directory and file version when they create the handshake.
`GetTaskHostHandshakeConfiguration` selects a configuration through a closed union when the requested runtime is determined at run time.
`TaskHostNodeKey` carries that configuration through acquisition, so its runtime flags cannot disagree with the launch configuration.
The provider matches that union before it selects a launch path.
Configuration flags remain available for connection keys and existence checks, without calculating installation identity.

Explicit .NET requests omit architecture bits because the parent cannot determine the SDK host's architecture.
Child factories preserve the current architecture bits for compatibility with deployed parents.
CLR2 configuration factories do not enable node reuse.
Task-host options use one architecture value before they produce wire flags.
Unspecified and x86 remain distinct in memory but share the same wire identity and reusable hosts.

`NodeEndpointOutOfProcBase<THandshake>` binds each endpoint to its required handshake type.
The CLI task-host endpoint uses `NetTaskHostHandshake` or `FrameworkTaskHostHandshake`, depending on its runtime.
The standalone CLR2 endpoint constructs only its own `Clr2TaskHostHandshake`.
`NodeProviderOutOfProcBase<THandshake>` accepts only `NodeLaunchData<THandshake>`.
Worker providers require `WorkerNodeHandshake`, and task-host providers require `TaskHostHandshake`.
`NodeConnection<THandshake>` also requires the selected handshake type when it connects to a pipe.
A worker handshake cannot be passed to a server endpoint or converted to `TaskHostHandshake`.
The process launcher receives only process configuration, not an untyped handshake.

This refactoring does not change the wire format.
All protocols still send seven big-endian integers and the existing end marker.
Task hosts retain their packet-version negotiation and legacy version-one fallback.
Node reuse and task-host connection lifetimes retain their existing rules.
The refactoring changes internal APIs only, so it does not require a ChangeWave.

`NodeEndpointOutOfProcBase` combines queued logging packet frames into pipe writes up to a configurable byte budget. `MSBUILDLOGPACKETBATCHSIZE` sets that budget in bytes: the default is 64 KiB, zero disables batching, and values from zero through 1 MiB are accepted inclusively. Invalid values use the default. Each packet is serialized independently with its existing type, length, payload, and negotiated version; batching does not change the wire format or binary log contents. Pending frames are written when the queue drains, before control packets, and before shutdown, without waiting for more packets. Frames at least as large as a configured positive limit bypass the batch buffer. This setting controls user-space logging writes independently of `MSBUILDNODECONNECTIONBUFFERSIZE`, which controls the kernel pipe buffer size.


## Orchestration

MSBuild consist of nodes. First spun is so called **entrypoint node**. It runs a **scheduler**. Then there are **worker nodes** - those can only execute projects. Nodes are spun by [`NodeLauncher`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Communications/NodeLauncher.cs) - this is being called from [`NodeProviderOutOfProcBase.GetNodes`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Communications/NodeProviderOutOfProcBase.cs#L186) (which can decide to reuse existing node or start a new process), that is ultimately called by [`NodeManger`](https://github.com/dotnet/msbuild/blob/main/src/Deprecated/Engine/Engine/NodeManager.cs).

`NodeManager` is a build component (`IBuildComponent`) - so it can be retrieved from build engine (via `IBuildComponentHost.GetComponent`).

Node is described by [`NodeInfo`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/Communications/NodeInfo.cs).

MSBuild can be started from existing process via API, or via MSBuild.exe - in both cases this process becomes a `scheduler node` and may or may not run other work in-process. By default the main process has as well a `worker node` (project build node). This can be tweaked by API and/or [environment variables](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#configure-msbuild-in-the-net-cli).

When MSBuild starts building a project, it can eventually start processing an MSBuild task that has [`BuildInParallel`](https://learn.microsoft.com/en-us/visualstudio/msbuild/building-multiple-projects-in-parallel-with-msbuild) set to `true` (the default) and multiple projects to build (for example, a project with multiple `ProjectReference` elements). At that point, the `scheduler` orchestrates multiple `worker nodes` via `NodeManager` (upper limited via maximum parallelization - configurable via API or CLI (`-maxcpucount|-m`)).

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

