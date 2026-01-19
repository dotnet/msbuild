# MSBuild Visual Studio Telemetry Data

This document describes the telemetry data collected by MSBuild and sent to Visual Studio telemetry infrastructure. The telemetry helps the MSBuild team understand build patterns, identify issues, and improve the build experience.

## Overview

MSBuild collects telemetry at multiple levels:
1. **Build-level telemetry** - Overall build metrics and outcomes
2. **Task-level telemetry** - Information about task execution
3. **Target-level telemetry** - Information about target execution and incrementality
4. **Logging configuration telemetry** - How logging was configured
5. **BuildCheck telemetry** - Static analysis rules and violations
6. **Error categorization telemetry** - Classification of build failures

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
| `SACEnabled` | bool | Whether Smart Application Control was enabled |
| `IsStandaloneExecution` | bool | True if MSBuild runs from command line |
| `InitialMSBuildServerState` | string | Server state before build: "cold", "hot", or null |
| `ServerFallbackReason` | string | If server was bypassed: "ServerBusy", "ConnectionError", or null |
| `ProjectPath` | string | Path to the project file being built |
| `FailureCategory` | string | Primary failure category when build fails (see Error Categorization) |
| `ErrorCounts` | object | Breakdown of errors by category (see Error Categorization) |

---

## 2. Error Categorization

When a build fails, errors are categorized to help identify the source of failures.

### Error Categories

| Category | Error Code Prefixes | Description |
|----------|---------------------|-------------|
| `Compiler` | CS, FS, VBC | C#, F#, and Visual Basic compiler errors |
| `MsBuildGeneral` | MSB1xxx-MSB2xxx | General MSBuild errors |
| `MsBuildEvaluation` | MSB4xxx | Project evaluation errors |
| `MsBuildExecution` | MSB5xxx-MSB6xxx | Build execution errors |
| `MsBuildGraph` | MSB4xxx (subset) | Static graph build errors |
| `Task` | MSB3xxx | Task-related errors |
| `SdkResolvers` | MSB4xxx (SDK-related) | SDK resolution errors |
| `NetSdk` | NETSDK | .NET SDK errors |
| `NuGet` | NU | NuGet package errors |
| `BuildCheck` | BC | BuildCheck rule violations |
| `NativeToolchain` | LNK, C1xxx-C4xxx, CL | Native C/C++ toolchain errors (linker, compiler) |
| `CodeAnalysis` | CA, IDE | Code analysis and IDE analyzer errors |
| `Razor` | RZ | Razor compilation errors |
| `Wpf` | XC, MC | WPF/XAML compilation errors |
| `AspNet` | ASP, BL | ASP.NET and Blazor errors |
| `Other` | (all others) | Uncategorized errors |

### MSBuild Error Code Ranges

| Range | Category |
|-------|----------|
| MSB3001-3999 | Tasks |
| MSB4001-4099 | General |
| MSB4100-4199 | Evaluation |
| MSB4200-4299 | SDKResolvers |
| MSB4300-4399 | Execution |
| MSB4400-4499 | Graph |
| MSB4500-4999 | General |
| MSB5001-5999 | Execution |
| MSB6001-6999 | Execution |

---

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

### Task Summary Event (`build/tasks`)

| Property | Type | Description |
|----------|------|-------------|
| `TasksExecutedCount` | int | Total tasks executed |
| `TaskHostTasksExecutedCount` | int | Tasks executed in task host process |

### Task Subclass Event (`build/tasks/msbuild-subclassed`)

Tracks when users subclass Microsoft-owned MSBuild tasks.

| Property | Type | Description |
|----------|------|-------------|
| `Microsoft_Build_Tasks_*` | int | Count of subclass usages per Microsoft task type |

### Tasks Summary (Activity Property)

Detailed task execution statistics attached to build activity.

```
TasksSummary: {
    Microsoft: {
        Total: { ExecutionsCount, TotalMilliseconds, TotalMemoryBytes },
        FromNuget: { ExecutionsCount, TotalMilliseconds, TotalMemoryBytes }
    },
    Custom: {
        Total: { ExecutionsCount, TotalMilliseconds, TotalMemoryBytes },
        FromNuget: { ExecutionsCount, TotalMilliseconds, TotalMemoryBytes }
    }
}
```

### Task Details (Activity Property)

Per-task execution details (when enabled).

| Field | Type | Description |
|-------|------|-------------|
| `Name` | string | Task name (hashed if custom) |
| `TotalMilliseconds` | double | Cumulative execution time |
| `ExecutionsCount` | int | Number of times executed |
| `TotalMemoryBytes` | long | Memory used by task |
| `IsCustom` | bool | Whether it's a custom task |
| `IsNuget` | bool | Whether it came from NuGet |
| `FactoryName` | string | Task factory name (hashed if custom) |
| `TaskHostRuntime` | string | Runtime if executed in task host |

---

## 4. Target Telemetry

### Targets Summary (Activity Property)

```
TargetsSummary: {
    Loaded: {
        Total: int,
        Microsoft: { Total, FromNuget, FromMetaproj },
        Custom: { Total, FromNuget, FromMetaproj }
    },
    Executed: {
        Total: int,
        Microsoft: { Total, FromNuget, FromMetaproj },
        Custom: { Total, FromNuget, FromMetaproj }
    }
}
```

### Target Details (Activity Property)

Per-target execution details (when enabled).

| Field | Type | Description |
|-------|------|-------------|
| `Name` | string | Target name (hashed if custom or metaproj) |
| `WasExecuted` | bool | Whether the target ran |
| `IsCustom` | bool | Whether it's a custom target |
| `IsNuget` | bool | Whether it came from NuGet |
| `IsMetaProj` | bool | Whether it's from a metaproject |
| `SkipReason` | enum | Why target was skipped (if applicable) |

### Target Skip Reasons

| Reason | Description |
|--------|-------------|
| `None` | Target was executed |
| `OutputsUpToDate` | Target outputs were up-to-date |
| `ConditionWasFalse` | Target condition evaluated to false |
| `PreviouslyBuiltSuccessfully` | Target was already built successfully |
| `PreviouslyBuiltUnsuccessfully` | Target was already built but failed |

---

## 5. Build Incrementality Telemetry

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

## 6. Logging Configuration Telemetry (`loggingConfiguration` event)

Describes how build logging was configured.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `TerminalLogger` | bool | Whether terminal logger was used |
| `TerminalLoggerUserIntent` | string | User's explicit intent: "on", "off", "auto", or null |
| `TerminalLoggerUserIntentSource` | string | How intent was specified: "arg", "MSBUILDTERMINALLOGGER", "MSBUILDLIVELOGGER", or null |
| `TerminalLoggerDefault` | string | Default behavior if no intent: "on", "off", "auto", or null |
| `TerminalLoggerDefaultSource` | string | Default source: "sdk", "DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER", "msbuild", or null |
| `ConsoleLogger` | bool | Whether console logger was used |
| `ConsoleLoggerVerbosity` | string | Verbosity: "quiet", "minimal", "normal", "detailed", "diagnostic" |
| `FileLogger` | bool | Whether file logger was used |
| `FileLoggerVerbosity` | string | File logger verbosity |
| `BinaryLogger` | bool | Whether binary logger (.binlog) was used |
| `BinaryLoggerUsedDefaultName` | bool | Whether binary logger used default file name |

---

## 7. BuildCheck Telemetry

Static analysis (BuildCheck) telemetry for rule execution and violations.

### Acquisition Failure Event (`buildcheck/acquisitionfailure`)

Logged when a custom check fails to load.

| Property | Type | Description |
|----------|------|-------------|
| `SubmissionId` | GUID | Unique build submission ID |
| `AssemblyName` | string | Name of assembly that failed to load |
| `ExceptionType` | string | Type of exception thrown |
| `ExceptionMessage` | string | Exception message |

### Run Event (`buildcheck/run`)

Summary of BuildCheck execution.

| Property | Type | Description |
|----------|------|-------------|
| `SubmissionId` | GUID | Unique build submission ID |
| `RulesCount` | int | Total number of rules |
| `CustomRulesCount` | int | Number of custom (non-built-in) rules |
| `ViolationsCount` | int | Total violations found |
| `TotalRuntimeInMilliseconds` | double | Total BuildCheck runtime |

### Rule Stats Event (`buildcheck/rule`)

Per-rule statistics (one event per rule).

| Property | Type | Description |
|----------|------|-------------|
| `SubmissionId` | GUID | Unique build submission ID |
| `RuleId` | string | Rule identifier (e.g., "BC0101") |
| `CheckFriendlyName` | string | Human-readable check name |
| `IsBuiltIn` | bool | Whether rule is built into MSBuild |
| `DefaultSeverityId` | int | Numeric severity level |
| `DefaultSeverity` | string | Severity name: "None", "Suggestion", "Warning", "Error" |
| `EnabledProjectsCount` | int | Number of projects with rule enabled |
| `ExplicitSeverities` | string | CSV of explicitly configured severities |
| `ExplicitSeveritiesIds` | string | CSV of explicit severity IDs |
| `ViolationMessagesCount` | int | Message-level violations |
| `ViolationWarningsCount` | int | Warning-level violations |
| `ViolationErrorsCount` | int | Error-level violations |
| `IsThrottled` | bool | Whether reporting was throttled |
| `TotalRuntimeInMilliseconds` | double | Time spent evaluating rule |

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

### Sample Rate

The default telemetry sample rate is 1:25,000 (4e-5), providing statistically significant data while minimizing collection volume.

---

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
