# MSBuild EventSource tracing

This document covers **MSBuild tracing via `EventSource`**, not MSBuild's logger pipeline. If you are looking for console/file/binlog behavior, see the logging documentation instead. This page is meant to help you:

1. read an MSBuild trace quickly,
2. understand what each EventSource event means, and
3. give humans and agents a reliable source of truth when investigating MSBuild traces.

## Scope and provider basics

MSBuild's tracing provider is `Microsoft-Build`, implemented by `Microsoft.Build.Eventing.MSBuildEventSource`.

MSBuild defines two keywords:

| Keyword | Value | Meaning |
| --- | --- | --- |
| `All` | `0x1` | Applied to every MSBuild EventSource event. |
| `PerformanceLog` | `0x2` | Subset of low-volume events that MSBuild can also mirror to its text performance log. |

### How traces are collected

| Collection path | What you get |
| --- | --- |
| Event tracing tooling such as PerfView | Full `Microsoft-Build` provider events. On Windows this is the normal way to inspect traces. |
| `DOTNET_PERFLOG_DIR` environment variable | A text file written by `PerformanceLogEventListener` containing only events marked with the `PerformanceLog` keyword. |

Example PerfView collection:

```text
PerfView /OnlyProviders=*Microsoft-Build run MSBuild.exe <project-or-solution>
```

Example:

```text
..\PerfView /OnlyProviders=*Microsoft-Build run .\MSBuild.exe .\MSBuild.slnx
```

## How to read an MSBuild trace quickly

For a normal command-line build, the trace usually looks like this at a high level:

```text
MSBuildExe
  Build
    ProjectGraphConstruction        (graph builds only)
    BuildProject                    (one per project request)
      Evaluate
        LoadDocument / Parse
        EvaluatePass0..5
        EvaluateCondition
        ExpandGlob
        ApplyLazyItemOperations
        SDK resolver activities
      Target
        TargetUpToDate
        ExecuteTask
          task-specific activities such as:
            CopyUpToDate
            WriteLinesToFileUpToDate
            GenerateResourceOverall
            RarOverall / RarComputeClosure / RarLogResults
        TaskHostDispatch / TaskExecuteInHost  (only for out-of-proc task hosts)
    NodeReuseScan / NodeLaunch / NodeConnect / PacketReadSize  (multi-proc infrastructure)
```

### Practical reading order

| Start here | Why |
| --- | --- |
| `MSBuildExe*`, `MSBuildServerBuild*`, `Build*` | Tells you which high-level execution mode you are looking at and where the overall wall time went. |
| `BuildProject*` + `Evaluate*` + `Target*` | Usually the most useful structure for understanding what project MSBuild was working on and whether time was spent evaluating or executing. |
| `ExecuteTask*` and task-specific events | Shows which tasks were expensive. |
| `SdkResolver*`, `ProjectCache*`, `TaskHost*` | Explains startup, SDK lookup, cache interception, or out-of-proc execution overhead. |
| `Node*` and `PacketReadSize` | Explains worker-node startup and IPC costs in `/m` or server scenarios. |

## Activity model and correlation

MSBuild mostly models traceable work as **`Start` / `Stop` event pairs**. Treat each pair as one logical activity whose duration is the time between the two events.

Important details:

* MSBuild does **not** define custom `EventTask` values, explicit `EventOpcode` values, or custom related-activity IDs in its source.
* Correlation is therefore mostly by **paired event names**, **thread/process context**, and **payload identity** such as project path, target name, task ID, submission ID, node ID, or plugin name.
* Parent/child relationships are the code-flow relationships you see in the trace. For example, `Evaluate*` usually appears inside `BuildProject*`, and `ExecuteTask*` usually appears inside `Target*`.
* Not every `Stop` event implies success. Some stop events only mean "scope ended." Success is only explicit when there is a payload such as `result`, `success`, `succeeded`, `wasUpToDate`, `wasResultCached`, or `cacheResultType`.

### Activities that matter most

| Activity | What it represents |
| --- | --- |
| `MSBuildExe` | Outer command-line execution of `MSBuild.exe`. |
| `MSBuildServerBuild` | Client-side interaction with the MSBuild server. |
| `Build` | `BuildManager` build lifetime. |
| `BuildProject` | One project request being executed. |
| `Evaluate` | Evaluation of one project. |
| `Target` | Execution of one target. |
| `ExecuteTask` | Execution of one task in-proc on a worker node. |
| `TaskHostDispatch` | Full round-trip for a task executed in an out-of-proc task host. |
| `TaskExecuteInHost` | The actual `task.Execute()` body inside the task host process. |

### Reading nested timing correctly

Some paired activities are intentionally more specific than their parent:

* `ExecuteTaskYield` measures time during which a task yielded the node.
* `ExecuteTaskReacquire` measures the reacquire handshake after the yield.
* `TargetUpToDate` measures dependency analysis for a target, not target execution.
* `CopyUpToDate` and `WriteLinesToFileUpToDate` measure incremental checks, not the later file write/copy work.

## Event catalog

The tables below list **all currently defined MSBuild EventSource events**. For paired events, both names and IDs are listed together.

### Process, build, and graph orchestration

Only rows marked **PerfLog = Yes** are included in the `DOTNET_PERFLOG_DIR` text performance log.

| Events | PerfLog | When emitted | Payloads |
| --- | --- | --- | --- |
| `MSBuildExeStart` (45)<br/>`MSBuildExeStop` (46) | Yes | Around the main `MSBuild.exe` command-line execution in `XMake.Execute`. This is the outermost activity for direct CLI use. | `commandLine`: reconstructed command line passed to MSBuild. |
| `MSBuildServerBuildStart` (89)<br/>`MSBuildServerBuildStop` (90) | No | Around the client-side request sent to the MSBuild server and the packet-reading loop that returns console output and the final exit result. | `commandLine`: descriptive command line sent to the server.<br/>`countOfConsoleMessages`: number of console-write packets received from the server.<br/>`sumSizeOfConsoleMessages`: total payload size of those console packets.<br/>`clientExitType`: how the client side finished.<br/>`serverExitType`: the server-side exit/result string returned by the build. |
| `BuildStart` (3)<br/>`BuildStop` (4) | Yes | Around the `BuildManager` build lifetime: setup, scheduling, execution, teardown, and reset back to idle. | No payload. |
| `BuildProjectStart` (5)<br/>`BuildProjectStop` (6) | Yes | Around one project request in `RequestBuilder.BuildProject()`, after targets are chosen and before/after target execution. | `projectPath`: full path of the project request.<br/>`targets`: comma-separated resolved target list. Note that the stop event does **not** include a success/result field. |
| `RequestThreadProcStart` (37)<br/>`RequestThreadProcStop` (38) | Yes | Around the worker thread entry point that calls `BuildProject()` for a request. Useful for request-level scheduling overhead separate from project execution details. | No payload. |
| `ProjectGraphConstructionStart` (53)<br/>`ProjectGraphConstructionStop` (54) | No | Around `ProjectGraph` construction. Appears for graph builds and graph APIs rather than every build. | `graphEntryPoints`: semicolon-separated entry-point descriptions. Each entry includes the project file and any global properties formatted as `Project(global1 = value1, ...)`. |
| `CancelSubmissionsStart` (93) | No | Emitted when `BuildManager` starts canceling active submissions. There is no matching stop event. | No payload. |

### Evaluation, parsing, and project file I/O

| Events | PerfLog | When emitted | Payloads |
| --- | --- | --- | --- |
| `EvaluateStart` (11)<br/>`EvaluateStop` (12) | Yes | Around evaluation of one project in `Evaluator.Evaluate(...)`. This is the main evaluation activity to look at first. | `projectFile`: evaluated project file path. |
| `EvaluatePass0Start` (13)<br/>`EvaluatePass0Stop` (14) | No | Pass 0 of evaluation: load initial properties. | `projectFile`: evaluated project file path. |
| `EvaluatePass1Start` (15)<br/>`EvaluatePass1Stop` (16) | No | Pass 1 of evaluation: evaluate properties, process imports, and collect other top-level elements. | `projectFile`: evaluated project file path. |
| `EvaluatePass2Start` (17)<br/>`EvaluatePass2Stop` (18) | No | Pass 2 of evaluation: evaluate item definitions. | `projectFile`: evaluated project file path. |
| `EvaluatePass3Start` (19)<br/>`EvaluatePass3Stop` (20) | No | Pass 3 of evaluation: evaluate project items, then realize deferred/lazy item work. | `projectFile`: evaluated project file path. |
| `EvaluatePass4Start` (21)<br/>`EvaluatePass4Stop` (22) | No | Pass 4 of evaluation: evaluate `UsingTask` declarations and finalize default-target bookkeeping. | `projectFile`: evaluated project file path. |
| `EvaluatePass5Start` (23)<br/>`EvaluatePass5Stop` (24) | No | Pass 5 of evaluation: read targets and before/after-target mappings. Target bodies are not executed here. | `projectFile`: evaluated project file path. |
| `EvaluateConditionStart` (9)<br/>`EvaluateConditionStop` (10) | No | Around evaluation of a single MSBuild condition in the lazy item evaluator. This can be high-volume in large evaluations. | `condition`: condition text being evaluated.<br/>`result`: Boolean outcome. |
| `ApplyLazyItemOperationsStart` (1)<br/>`ApplyLazyItemOperationsStop` (2) | No | Around a lazy item operation applying selection/mutation/save work for one item type. | `itemType`: item type being materialized or updated. |
| `ExpandGlobStart` (41)<br/>`ExpandGlobStop` (42) | No | Around wildcard expansion for a single glob fragment in item evaluation. | `rootDirectory`: root directory used for the file search.<br/>`glob`: wildcard pattern being expanded.<br/>`excludedPatterns`: comma-separated exclude patterns applied to the glob. |
| `ParseStart` (33)<br/>`ParseStop` (34) | No | Around `ProjectParser.Parse`, which turns already-loaded XML into `ProjectRootElement` objects. | `projectFileName`: project file being parsed. |
| `LoadDocumentStart` (29)<br/>`LoadDocumentStop` (30) | Yes | Around loading an XML project document from disk into `XmlDocumentWithLocation`. | `fullPath`: full path of the file being loaded. |
| `SaveStart` (39)<br/>`SaveStop` (40) | No | Around saving a `ProjectRootElement` back to disk. | `fileLocation`: target file location being saved. |

### Target and task execution

| Events | PerfLog | When emitted | Payloads |
| --- | --- | --- | --- |
| `TargetStart` (43)<br/>`TargetStop` (44) | Yes | Around execution of one target in `TargetBuilder`. | `targetName`: target being executed.<br/>`result`: string form of the final `TargetResultCode` on stop. |
| `TargetUpToDateStart` (56)<br/>`TargetUpToDateStop` (57) | No | Around target dependency analysis in `TargetUpToDateChecker`, before MSBuild decides whether the target needs a full build, incremental build, or skip. | `result`: integer value of `DependencyAnalysisResult`.<br/>Current values are `0=SkipUpToDate`, `1=SkipNoInputs`, `2=SkipNoOutputs`, `3=IncrementalBuild`, `4=FullBuild`. |
| `ExecuteTaskStart` (47)<br/>`ExecuteTaskStop` (48) | No | Around in-proc task execution in `TaskBuilder`, after the task identity/factory has been resolved and before task batch finished logging. | `taskName`: task element name.<br/>`taskID`: `BuildEventContext.TaskId` for the task batch. The stop event does **not** include success/failure. |
| `ExecuteTaskYieldStart` (49)<br/>`ExecuteTaskYieldStop` (50) | No | Around the period where a task explicitly yields the node via `IBuildEngine.Yield()`. | `taskName`: task name.<br/>`taskID`: `BuildEventContext.TaskId`. |
| `ExecuteTaskReacquireStart` (51)<br/>`ExecuteTaskReacquireStop` (52) | No | Around the `IBuildEngine.Reacquire()` handshake after a yielded task asks for the node back. | `taskName`: task name.<br/>`taskID`: `BuildEventContext.TaskId`. |

### Out-of-proc task host execution

| Events | PerfLog | When emitted | Payloads |
| --- | --- | --- | --- |
| `TaskHostDispatchStart` (109)<br/>`TaskHostDispatchStop` (110) | No | Around the full dispatch of a task to an out-of-proc task host: configuration, IPC, remote execution, and result retrieval. | `taskName`: fully qualified task type name used for dispatch.<br/>`succeeded`: whether the task host returned a successful execution result. |
| `TaskExecuteInHostStart` (111)<br/>`TaskExecuteInHostStop` (112) | No | Around the actual `task.Execute()` call inside the task host process. This is the best signal for "real work inside host" versus dispatch overhead. | `taskName`: task name executed in the host.<br/>`succeeded`: Boolean return value from `Execute()`. |
| `TaskHostBuildProjectFileStart` (107)<br/>`TaskHostBuildProjectFileStop` (108) | No | Around a nested `BuildProjectFile` callback issued from the task host back to the owning build node. | `projectFiles`: semicolon-separated project file list.<br/>`targetNames`: semicolon-separated target list on start.<br/>`success`: overall callback result on stop. |

### SDK resolution and type loading

| Events | PerfLog | When emitted | Payloads |
| --- | --- | --- | --- |
| `SdkResolverLoadAllResolversStart` (62)<br/>`SdkResolverLoadAllResolversStop` (63) | No | Around discovery/loading of the full resolver set from the MSBuild `SdkResolvers` directory in the legacy loader path. | `resolverCount`: number of resolvers loaded. |
| `SdkResolverFindResolversManifestsStart` (81)<br/>`SdkResolverFindResolversManifestsStop` (82) | No | Around scanning resolver manifests (`*.xml`) for the manifest-based resolver path. | `resolverManifestCount`: number of manifests found. |
| `SdkResolverLoadResolversStart` (83)<br/>`SdkResolverLoadResolversStop` (84) | No | Around loading resolvers described by a single resolver manifest. | `manifestName`: display name/path of the manifest being processed.<br/>`resolverCount`: number of resolvers loaded from that manifest. |
| `SdkResolverResolveSdkStart` (64)<br/>`SdkResolverResolveSdkStop` (65) | No | Around one call to one SDK resolver's `Resolve(...)` method. The start event has no payload because the winning resolver details are only known after the call returns. | `resolverName`: resolver that was called.<br/>`sdkName`: requested SDK name.<br/>`solutionPath`: current solution path if available, otherwise empty string.<br/>`projectPath`: current project path if available, otherwise empty string.<br/>`sdkPath`: resolved SDK path, or empty string on failure.<br/>`success`: resolver success flag. |
| `CachedSdkResolverServiceResolveSdkStart` (66)<br/>`CachedSdkResolverServiceResolveSdkStop` (67) | No | Around submission-scoped SDK resolution through the caching wrapper. | `sdkName`: requested SDK name.<br/>`solutionPath`: solution path or empty string.<br/>`projectPath`: project path or empty string.<br/>`success`: final resolution success.<br/>`wasResultCached`: whether the result came from the submission cache rather than a fresh resolver call. |
| `OutOfProcSdkResolverServiceRequestSdkPathFromMainNodeStart` (79)<br/>`OutOfProcSdkResolverServiceRequestSdkPathFromMainNodeStop` (80) | No | Around a worker node asking the main node to resolve an SDK. This is the cross-node SDK-resolution activity. | `submissionId`: build submission requesting the SDK.<br/>`sdkName`: requested SDK name.<br/>`solutionPath`: solution path or empty string.<br/>`projectPath`: project path or empty string.<br/>`success`: whether the main-node response succeeded.<br/>`wasResultCached`: whether the worker reused a cached response. |
| `SdkResolverServiceNodeShutDownSet` (94) | No | Point-in-time marker emitted when the hosted SDK resolver service marks itself shut down during node teardown. | No payload. |
| `CreateLoadedTypeStart` (85)<br/>`CreateLoadedTypeStop` (86) | No | Around creation of a `LoadedType` from an assembly in `TypeLoader`. Useful when diagnosing task/logger/resolver/plugin type loading. | `assemblyName`: assembly display name being processed. |
| `LoadAssemblyAndFindTypeStart` (87)<br/>`LoadAssemblyAndFindTypeStop` (88) | No | Around loading an assembly and scanning it for candidate public types. | Start event: no payload.<br/>Stop event: `assemblyPath` is the assembly path inspected, and `numberOfPublicTypesSearched` is the number of public types scanned before completion. |
| `FallbackAssemblyLoadStart` (97)<br/>`FallbackAssemblyLoadStop` (98) | No | Around `TypeLoader`'s fallback assembly loading path. Usually only interesting when normal loading was insufficient. | Current implementation emits no payload on either event, even though the methods take an `assemblyName` parameter. |

### Project cache

| Events | PerfLog | When emitted | Payloads |
| --- | --- | --- | --- |
| `ProjectCacheCreatePluginInstanceStart` (71)<br/>`ProjectCacheCreatePluginInstanceStop` (72) | No | Around loading the plugin assembly and constructing the plugin instance. This does **not** include `BeginBuildAsync`. | `pluginAssemblyPath`: assembly path requested.<br/>`pluginTypeName`: resolved plugin type name on stop. If loading fails, the stop event still fires and the type name may remain the assembly path. |
| `ProjectCacheBeginBuildStart` (73)<br/>`ProjectCacheBeginBuildStop` (74) | No | Around the plugin's `BeginBuildAsync` initialization hook. | `pluginTypeName`: plugin name/type. |
| `ProjectCacheGetCacheResultStart` (75)<br/>`ProjectCacheGetCacheResultStop` (76) | No | Around `GetCacheResultAsync` for one project request. This is the main cache-query activity. | `pluginTypeName`: plugin name/type.<br/>`projectPath`: project being queried.<br/>`targets`: target list for the request. The start event uses MSBuild's default-target marker when appropriate; the stop event may use the literal `"<default>"` text for that same case.<br/>`cacheResultType`: string form of the final `CacheResultType` on stop. |
| `ProjectCacheHandleBuildResultStart` (91)<br/>`ProjectCacheHandleBuildResultStop` (92) | No | Around the plugin callback that observes a completed project build (`HandleProjectFinishedAsync`). | `pluginTypeName`: plugin name/type.<br/>`projectPath`: completed project path.<br/>`targets`: target list associated with the build result. |
| `ProjectCacheEndBuildStart` (77)<br/>`ProjectCacheEndBuildStop` (78) | No | Around the plugin's `EndBuildAsync` cleanup hook during project-cache disposal. | `pluginTypeName`: plugin name/type. |

### Task-specific and incremental helper activities

| Events | PerfLog | When emitted | Payloads |
| --- | --- | --- | --- |
| `GenerateResourceOverallStart` (25)<br/>`GenerateResourceOverallStop` (26) | No | Around the overall `GenerateResource` task body. | No payload. |
| `RarOverallStart` (27)<br/>`RarOverallStop` (28) | Yes | Around the full `ResolveAssemblyReference` task execution. This is the top-level RAR activity to look at first. | `assembliesCount`: length of the primary assembly-name input array.<br/>`assemblyFilesCount`: length of the assembly-file input array.<br/>`resolvedFilesCount`: number of resolved primary files produced.<br/>`resolvedDependencyFilesCount`: number of dependency files produced.<br/>`copyLocalFilesCount`: number of copy-local files produced.<br/>`findDependencies`: whether dependency walking was enabled.<br/>Counts can be `-1` if execution ended before a given result array was populated. |
| `RarLogResultsStart` (31)<br/>`RarLogResultsStop` (32) | No | Around the phase where `ResolveAssemblyReference` logs its results. | No payload. |
| `RarComputeClosureStart` (7)<br/>`RarComputeClosureStop` (8) | No | Around the dependency-closure computation inside `ReferenceTable`. | No payload. |
| `RarRemoveReferencesMarkedForExclusionStart` (35)<br/>`RarRemoveReferencesMarkedForExclusionStop` (36) | No | Around the pass that removes references that match RAR exclusion rules/deny lists. | No payload. |
| `CopyUpToDateStart` (58)<br/>`CopyUpToDateStop` (59) | No | Around the `Copy` task's up-to-date / duplicate-destination check for one destination file. This does not necessarily mean a copy occurs. | `path`: destination path being considered.<br/>`wasUpToDate`: whether the destination was treated as already current and the copy could be skipped. |
| `WriteLinesToFileUpToDateStart` (60)<br/>`WriteLinesToFileUpToDateStop` (61) | No | Around `WriteLinesToFile`'s "write only when different" incremental check. | `fileItemSpec`: file being compared on stop.<br/>`wasUpToDate`: whether the existing file content already matched and the write could be skipped. |

### Node, IPC, and internal diagnostics

| Events | PerfLog | When emitted | Payloads |
| --- | --- | --- | --- |
| `NodeReuseScanStart` (101)<br/>`NodeReuseScanStop` (102) | No | Around scanning the machine for reusable MSBuild worker/task-host processes. | `candidateCount`: number of candidate processes found. |
| `NodeConnectStart` (99)<br/>`NodeConnectStop` (100) | No | Around connecting MSBuild to a worker node, whether reused or newly launched. | `nodeId`: logical node ID assigned by MSBuild.<br/>`processId`: OS process ID of the connected node.<br/>`isReused`: whether the connected process was reused rather than newly launched. |
| `NodeLaunchStart` (103)<br/>`NodeLaunchStop` (104) | No | Around launching a new out-of-proc worker node process. | `nodeId`: logical node ID.<br/>`processId`: OS process ID of the launched node. |
| `NodePipeConnectStart` (105)<br/>`NodePipeConnectStop` (106) | No | Around connecting to a node's communication pipe/stream after choosing or launching a process. | `nodeId`: logical node ID.<br/>`processId`: OS process ID of the target node.<br/>`succeeded`: whether the pipe connection succeeded. |
| `PacketReadSize` (55) | No | Point-in-time marker emitted whenever MSBuild reads a packet from node IPC. | `size`: packet size in bytes. This is useful for diagnosing communication volume, not duration. |
| `OutOfProcNodeShutDownStart` (95)<br/>`OutOfProcNodeShutDownStop` (96) | No | Around worker-node shutdown and cleanup, including SDK resolver shutdown, engine cleanup, logging shutdown, and endpoint disconnect. | `shutdownReason`: string form of the node shutdown reason on stop. |
| `ReusableStringBuilderFactoryStart` (68)<br/>`ReusableStringBuilderFactoryStop` (69) | No | Internal diagnostics for `ReuseableStringBuilder` pool usage. These are primarily a debugging aid for pool efficiency and are only emitted in debug builds. | `hash`: `StringBuilder` object hash code used for correlation.<br/>`newCapacity`: requested/new pooled capacity on start.<br/>`oldCapacity`: previous pooled capacity on start.<br/>`returningCapacity`: capacity when returning on stop.<br/>`returningLength`: builder length at return time.<br/>`type`: classification such as `hit`, `miss`, `miss-need-bigger`, `return`, `return-new`, or `discard`. |
| `ReusableStringBuilderFactoryUnbalanced` (70) | No | Point-in-time diagnostic emitted when `ReuseableStringBuilder` detects incorrect pool usage, typically cross-thread or nested misuse. This event is emitted in release builds too. | `oldHash`: previous pooled builder hash code.<br/>`newHash`: replacement builder hash code. |

## Trace reading notes by subsystem

### Evaluation

* The most useful first cut is usually: `Evaluate` -> pass breakdown -> `ExpandGlob` / `EvaluateCondition`.
* `EvaluatePass3` is where item evaluation happens, so large globbing or item transforms often show up there.
* `LoadDocument` and `Parse` are separate: document load is file/XML read, parse is conversion into MSBuild object model structures.

### Targets and tasks

* Start by breaking overall execution time down by `Target` duration and then `ExecuteTask` duration. That is usually the fastest way to find the expensive part of execution.
* `TargetStart` / `TargetStop` tells you target result; `ExecuteTaskStart` / `ExecuteTaskStop` does **not** tell you task success directly.
* For task-hosted tasks, separate the time into:
  1. `TaskHostDispatch` = whole round trip,
  2. `TaskExecuteInHost` = actual work inside host,
  3. the difference = serialization/IPC/host overhead.
* `ExecuteTaskYield` time is not active computation on the node; it is time the task spent yielded.

### Multi-proc and server builds

* `MSBuildServerBuild*` appears on the client side of server-backed builds.
* `NodeReuseScan`, `NodeLaunch`, `NodePipeConnect`, and `NodeConnect` explain startup overhead for worker nodes.
* `PacketReadSize` is a useful proxy for IPC volume when a build is communication-heavy.

### Correlating with MSBuild logging/binlogs

This document is intentionally about tracing, not logging, but a few payloads line up well with build-log concepts:

* `taskID` in `ExecuteTask*` matches `BuildEventContext.TaskId`.
* `projectPath`, `targetName`, `taskName`, and `submissionId` are the main join keys when you compare traces with binlogs or logger output.
* `TargetStop.result`, `TaskHostDispatchStop.succeeded`, `TaskExecuteInHostStop.succeeded`, `SdkResolverResolveSdkStop.success`, `CopyUpToDateStop.wasUpToDate`, and similar payloads are the places where the trace directly exposes an outcome.

## Summary

If you only remember one reading strategy, use this one:

1. find the outer activity (`MSBuildExe`, `MSBuildServerBuild`, `Build`),
2. break time down by `BuildProject`, then `Evaluate` versus `Target`,
3. inspect `ExecuteTask`, SDK resolver, project-cache, and node events only where the high-level timing says they matter.

That matches how MSBuild's EventSource instrumentation is structured in code and is the fastest way to turn a raw trace into a useful picture of the build.
