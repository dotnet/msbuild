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
