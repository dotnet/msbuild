# MSBuild Visual Studio Telemetry Data

This document describes the telemetry data collected by MSBuild and sent to Visual Studio telemetry infrastructure. The telemetry helps the MSBuild team understand build patterns, identify issues, and improve the build experience.

## Overview

MSBuild collects telemetry at multiple levels:
1. **Build-level telemetry** - Overall build metrics and outcomes
2. **Task-level telemetry** - Information about task execution
3. **Target-level telemetry** - Information about target execution and incrementality
4. **Error categorization telemetry** - Classification of build failures

All telemetry events use the `VS/MSBuild/` prefix as required by VS exporting/collection. Properties use the `VS.MSBuild.` prefix.

---

## 1. Build Telemetry (`build` event)

The primary telemetry event capturing overall build information.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `BuildDurationInMilliseconds` | double | Total build duration from start to finish |
| `InnerBuildDurationInMilliseconds` | double | Duration from when BuildManager starts (excludes server connection time) |
| `BuildEngineHost` | string | Host environment: "VS", "VSCode", "Azure DevOps", "GitHub Action", "CLI", etc. |
| `BuildSuccess` | bool | Whether the build succeeded |
| `BuildTarget` | string | The target(s) being built |
| `BuildEngineVersion` | Version | MSBuild engine version |
| `BuildEngineDisplayVersion` | string | Display-friendly engine version |
| `BuildEngineFrameworkName` | string | Runtime framework name |
| `BuildCheckEnabled` | bool | Whether BuildCheck (static analysis) was enabled |
| `MultiThreadedModeEnabled` | bool | Whether multi-threaded build mode was enabled |
| `TaskHostConsoleOutputForwarded` | bool | Whether the parent received and forwarded non-empty stdout or stderr from an eligible primary `-mt` task host during this build |
| `SACEnabled` | bool | Whether Smart Application Control was enabled |
| `IsStandaloneExecution` | bool | True if MSBuild runs from command line |
| `InitialMSBuildServerState` | string | Server state before build: "cold", "hot", or null |
| `ServerFallbackReason` | string | If server was bypassed: "ServerBusy", "ConnectionError", or null |
| `ProjectPath` | string | Path to the project file being built |
| `FailureCategory` | string | Primary failure category when build fails (see Error Categorization) |
| `ErrorCounts` | object | Breakdown of errors by category (see Error Categorization) |

`TaskHostConsoleOutputForwarded` records actual use, not enablement, and contains no console text or task identity.

---

## 2. Error Categorization

When a build fails, errors are categorized to help identify the source of failures.

### Error Categories

| Category | Error Code Prefixes | Description |
|----------|---------------------|-------------|
| `Compiler` | CS, FS, VBC | C#, F#, and Visual Basic compiler errors |
| `MsBuildGeneral` | MSB4001-MSB4099, MSB4500-MSB4999 | General MSBuild errors |
| `MsBuildEvaluation` | MSB4100-MSB4199 | Project evaluation errors |
| `MsBuildExecution` | MSB4300-MSB4399, MSB5xxx-MSB6xxx | Build execution errors |
| `MsBuildGraph` | MSB4400-MSB4499 | Static graph build errors |
| `Task` | MSB3xxx | Task-related errors |
| `SdkResolvers` | MSB4200-MSB4299 | SDK resolution errors |
| `NetSdk` | NETSDK | .NET SDK errors |
| `NuGet` | NU | NuGet package errors |
| `BuildCheck` | BC | BuildCheck rule violations |
| `NativeToolchain` | LNK, C1xxx-C4xxx, CL | Native C/C++ toolchain errors (linker, compiler) |
| `CodeAnalysis` | CA, IDE | Code analysis and IDE analyzer errors |
| `Razor` | RZ | Razor compilation errors |
| `Wpf` | XC, MC | WPF/XAML compilation errors |
| `AspNet` | ASP, BL | ASP.NET and Blazor errors |
| `Other` | (all others) | Uncategorized errors |

## 3. Task Telemetry

### Task Factory Event (`build/tasks/taskfactory`)

Tracks which task factories are being used.

| Property | Type | Description |
|----------|------|-------------|
| `AssemblyTaskFactoryTasksExecutedCount` | int | Tasks loaded via AssemblyTaskFactory |
| `IntrinsicTaskFactoryTasksExecutedCount` | int | Built-in intrinsic tasks |
| `CodeTaskFactoryTasksExecutedCount` | int | Tasks created via CodeTaskFactory |
| `RoslynCodeTaskFactoryTasksExecutedCount` | int | Tasks created via RoslynCodeTaskFactory |
| `XamlTaskFactoryTasksExecutedCount` | int | Tasks created via XamlTaskFactory |
| `CustomTaskFactoryTasksExecutedCount` | int | Tasks from custom task factories |

## 4. Build Incrementality Telemetry

Classifies builds as full or incremental based on target execution patterns.

### Incrementality Info (Activity Property)

| Field | Type | Description |
|-------|------|-------------|
| `Classification` | enum | `Full`, `Incremental`, or `Unknown` |
| `TotalTargetsCount` | int | Total number of targets |
| `ExecutedTargetsCount` | int | Targets that ran |
| `SkippedTargetsCount` | int | Targets that were skipped |
| `SkippedDueToUpToDateCount` | int | Skipped because outputs were current |
| `SkippedDueToConditionCount` | int | Skipped due to false condition |
| `SkippedDueToPreviouslyBuiltCount` | int | Skipped because already built |
| `IncrementalityRatio` | double | Ratio of skipped to total (0.0-1.0) |

A build is classified as **Incremental** when more than 70% of targets are skipped.

---

## Privacy Considerations

### Data Hashing

Custom and potentially sensitive data is hashed using SHA-256 before being sent:
- **Custom task names** - Hashed to protect proprietary task names
- **Custom target names** - Hashed to protect proprietary target names
- **Custom task factory names** - Hashed if not in the known list
- **Metaproj target names** - Hashed to protect solution structure

### Known Task Factory Names (Not Hashed)

The following Microsoft-owned task factory names are sent in plain text:
- `AssemblyTaskFactory`
- `TaskHostFactory`
- `CodeTaskFactory`
- `RoslynCodeTaskFactory`
- `XamlTaskFactory`
- `IntrinsicTaskFactory`

### Crash Events

Crash events don't include exception messages, which can contain customer data. They include exception types, HRESULTs, a stack hash, and stack traces with file paths removed.

## Collection and Delivery

Inside Visual Studio, MSBuild adds its events to the Visual Studio telemetry session, which Visual Studio owns. MSBuild never shuts that session down. The rest of this section applies to `MSBuild.exe` on .NET Framework, as installed with Visual Studio or Build Tools.

### Which processes report

- The `MSBuild.exe` process that you start owns a telemetry session for its whole lifetime. So does the MSBuild server node. Each build in that process adds events to the same session, so a server node sends its events only when it exits.
- MSBuild does not create sessions in worker nodes, task hosts, or RAR nodes. Build telemetry is aggregated in the entry process, so a build reports one `VS/MSBuild/build` event, however many nodes it uses.
- Tasks that post events to the Visual Studio default telemetry session, for example tasks from Visual Studio SDKs, use this session when they run in the entry process. In other processes they create their own session, as before.

### Consent

- `MSBUILD_TELEMETRY_OPTOUT=1` or `DOTNET_CLI_TELEMETRY_OPTOUT=1` (or `true`) turns telemetry off for `MSBuild.exe`. No telemetry assemblies are loaded, no session is created, and nothing is sent. `DOTNET_CLI_TELEMETRY_OPTOUT` also applies to MSBuild running on .NET, for example `dotnet build`.
- Otherwise the session uses the Visual Studio consent: the [Visual Studio Customer Experience Improvement Program](https://learn.microsoft.com/visualstudio/ide/visual-studio-experience-improvement-program) setting, including machine-wide policy. When consent is not given, the session is created but no events are sent. MSBuild does not change consent, and running in CI does not opt a machine in.

### Delivery

- Outside CI, events are saved on exit to the local Visual Studio telemetry store. A later Visual Studio or `MSBuild.exe` process uploads them.
- In CI, as detected by the same environment variables that disable the terminal logger (for example `CI`, `TF_BUILD`, `GITHUB_ACTIONS`, `TEAMCITY_VERSION`, `JENKINS_URL`, or `GITLAB_CI`), `MSBuild.exe` uploads pending events just before it exits. Ephemeral agents are often discarded before a later process could do it.
- The upload is best effort and bounded. The whole shutdown waits at most 10 seconds by default; `MSBUILD_TELEMETRY_SHUTDOWN_TIMEOUT_MS` changes this budget, and `0` means don't wait. When the budget runs out, `MSBuild.exe` exits without waiting further, and pending events may be lost. Telemetry never changes the build result or exit code.
- Events are not delivered in these cases:
  - Telemetry is opted out or consent is not given.
  - The process is terminated forcibly.
  - The collector is unreachable within the budget.
  - Another Visual Studio telemetry process on the same machine holds the upload lock. That process uploads the stored events instead.
  - A worker node crashes with an unhandled exception.

### Host identification

`BuildEngineHost` is determined in this order:

1. `VS` when MSBuild runs inside Visual Studio.
2. The value of `MSBUILD_HOST_NAME`, when set.
3. `Azure DevOps` when `TF_BUILD` is `true`.
4. `GitHub Action` when `GITHUB_ACTIONS` is `true`.
5. `VSCode` when `VSCODE_CWD` is set or `TERM_PROGRAM` is `vscode`.
6. Otherwise no host is reported.

Other CI systems are detected for upload on exit, but they are not reported as a host.

### Deployment requirements

`MSBuild.exe` loads these files from `MSBuild\Current\Bin`:

- `Microsoft.VisualStudio.Telemetry.dll`
- `Microsoft.VisualStudio.RemoteControl.dll`
- `Microsoft.VisualStudio.Utilities.Internal.dll`
- `Newtonsoft.Json.dll`

The 64-bit and ARM64 `MSBuild.exe` find them through `codeBase` entries in their `MSBuild.exe.config`. If the files can't be loaded, telemetry is disabled for that process, and the build is not affected.

### Diagnostics

Set `MSBUILD_TELEMETRY_DIAGNOSTICS=1` to write telemetry status lines, prefixed with `MSBuild telemetry:`, to standard error. They report:

- Whether telemetry was opted out.
- Initialization: session ownership, consent, CI detection, and the path of the loaded telemetry assembly.
- Initialization and dependency failures, with the exception.
- How long shutdown took, whether events were saved or uploaded, and whether it completed or timed out.
- Shutdown failures, with the exception.

The messages contain no session identifiers or event data. CI steps that fail on any standard error output (for example `failOnStderr`) will fail when this is on.

### Background: how delivery happened before this change

Before the upload-on-exit behavior described above, `MSBuild.exe` only saved events to the local store on exit; it never uploaded them itself. Whether those events still reached the collector depended on which of two mechanisms applied.

**In-process hosting (for example, Visual Studio's own build engine).** `BuildManager.EndBuildTelemetry` calls `TelemetryManager.Instance.Initialize(isStandalone: false)`, which resolves the session through `TelemetryService.DefaultSession` rather than creating one. When MSBuild's Framework assemblies are loaded in-process by a host that already created a default session (Visual Studio does this early at startup for its own telemetry), MSBuild's build event is posted onto that same live session object. MSBuild does not own or dispose it; the host's own long-running session delivers it through its normal flush cadence. This path is unaffected by this change and was always reliable.

**Separate `MSBuild.exe` processes (standalone, including CI and builds started from a VS Developer Command Prompt).** `XMake.cs` always calls `Initialize(isStandalone: true)` first, so every `MSBuild.exe` process owns its own session, created with `TelemetryService.CreateAndGetDefaultSession`, regardless of how it was launched. That session persists events to a storage folder keyed by the collector's instrumentation key (`%LOCALAPPDATA%\Microsoft\VSApplicationInsights\vstel<hash>\*.trn`), shared by every process on the machine that uses the same collector key. Before this change, delivery from that folder depended entirely on some process acting as sender:

- The owning `PersistenceTransmitter` tries to acquire a named, cross-process mutex over the folder. Only the process holding the mutex runs a `Sender` that polls the folder and uploads files.
- A sufficiently long-running process can deliver its own data: the in-memory buffer auto-flushes to disk every 30 seconds (`FlushManager.FlushDelay`), and the sender polls every 10 seconds when idle (`Sender.sendingIntervalOnNoData`). But events raised close to exit, including the build's own end event, can still be left in a final `.trn` file written during session disposal, after the last auto-flush.
- If no delivery happens before exit, the `.trn` file sits in the shared folder until another process using the same collector key starts, acquires the now-free sender mutex, and uploads it. That process can be Visual Studio itself (`devenv.exe`, since many builds happen under VS or a VS Developer Command Prompt) or simply a later `MSBuild.exe` invocation on the same machine. On a reused dev or build machine, a single drain can pick up leftovers from several prior runs if nothing drained them in between.
- This was verified experimentally: a second `MSBuild.exe` process uploaded a first process's leftover file, and a minimal harness using the same `TelemetryService.DefaultSession` API that Visual Studio uses delivered a child `MSBuild.exe` process's file about 7 seconds after the child exited.

**The gap this change closes**: on a persistent machine, a drain partner (Visual Studio, or a later build) is usually present eventually, so pre-fix delivery was opportunistic rather than reliably lost. On an ephemeral CI agent, no later process ever starts, so pending events were never delivered. The CI-aware upload-before-exit behavior described above under "Delivery" removes the dependence on an external drain partner.

## Related Files

| File | Description |
|------|-------------|
| [BuildTelemetry.cs](../src/Framework/Telemetry/BuildTelemetry.cs) | Main build telemetry class |
| [BuildInsights.cs](../src/Framework/Telemetry/BuildInsights.cs) | Container for detailed insights |
| [TelemetryDataUtils.cs](../src/Framework/Telemetry/TelemetryDataUtils.cs) | Data transformation utilities |
| [BuildErrorTelemetryTracker.cs](../src/Build/BackEnd/Components/Logging/BuildErrorTelemetryTracker.cs) | Error categorization |
| [ProjectTelemetry.cs](../src/Build/BackEnd/Components/Logging/ProjectTelemetry.cs) | Per-project task telemetry |
| [LoggingConfigurationTelemetry.cs](../src/Framework/Telemetry/LoggingConfigurationTelemetry.cs) | Logger configuration |
| [BuildCheckTelemetry.cs](../src/Framework/Telemetry/BuildCheckTelemetry.cs) | BuildCheck telemetry |
| [KnownTelemetry.cs](../src/Framework/Telemetry/KnownTelemetry.cs) | Static telemetry accessors |
| [TelemetryConstants.cs](../src/Framework/Telemetry/TelemetryConstants.cs) | Telemetry naming constants |
