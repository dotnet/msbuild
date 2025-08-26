# MSBuild Telemetry

MSBuild emits and collects telemetry to guide decisions on modernization and optimization investments. Currently the MSBuild collects telemetry only when run from SDK host (mostly the `dotnet build` and `dotnet msbuild` commands). For more details please refer to [the official SDK telemetry documentation](https://learn.microsoft.com/dotnet/core/tools/telemetry).

Visual Studio collects some build related telemetry - but that is not leveraging any MSBuild instrumentation, but rather information about count and duration of MSBuild API invocations from the caller point of view. For general information about telemetry being collected by Visual Studio Family of products and regulations compliance please refer to [the official documentation](https://learn.microsoft.com/compliance/regulatory/gdpr-dsr-visual-studio-family).

## Type of data collected

To tailor modernization and performance optimization investments we need to contain *anonymized* **Usage Data**. Those reflect type of features being used and execution time being spent in them.

## Opting out

MSBuild telemetry collection (that is turned on by default), can be opted out - same as .NET SDK telemetry in general - via setting `DOTNET_CLI_TELEMETRY_OPTOUT` environment variable to `1` or `true`.

## Datapoints overview

### Logging Configuration

Expressed and collected via [LoggingConfigurationTelemetry type](https://github.com/dotnet/msbuild/blob/94941d9cb26bb86045452b4a174a357b65a30c99/src/Framework/Telemetry/LoggingConfigurationTelemetry.cs)

| SDK versions | Data |
|--------------|------|
| >= 8.0.100   | Indication if terminal logger was used. |
| >= 8.0.100   | User choice on terminal logger enablement. |
| >= 8.0.100   | Source of user choice on terminal logger enablement. |
| >= 8.0.100   | Default choice on terminal logger enablement. |
| >= 8.0.100   | Source of default choice on terminal logger enablement. |
| >= 8.0.100   | Indication if Console logger was used. |
| >= 8.0.100   | Console logger type (serial, parallel). |
| >= 8.0.100   | Console logger verbosity. |
| >= 8.0.100   | Indication if File logger was used. |
| >= 8.0.100   | File logger type (serial, parallel). |
| >= 8.0.100   | Number of file loggers. |
| >= 8.0.100   | File logger verbosity. |
| >= 8.0.100   | Indication if Binary logger was used. |
| >= 8.0.100   | Indication if Binary logger used with default log name. |

### BuildCheck

Expressed and collected via [BuildCheckTelemetry type](https://github.com/dotnet/msbuild/blob/94941d9cb26bb86045452b4a174a357b65a30c99/src/Framework/Telemetry/BuildCheckTelemetry.cs)

#### BuildCheck Run

| SDK versions | Data |
|--------------|------|
| >= 9.0.100   | Corelation guid for the run |
| >= 9.0.100   | Count of enabled rules for the run |
| >= 9.0.100   | Count of enabled custom rules for the run |
| >= 9.0.100   | Count of violations encountered for the run |
| >= 9.0.100   | Execution time spent by BuildCheck infrastructure and rules |

#### BuildCheck Rule in a run

| SDK versions | Data |
|--------------|------|
| >= 9.0.100   | Corelation guid for the run. |
| >= 9.0.100   | Id of the rule. |
| >= 9.0.100   | Hashed Check Friendly name. |
| >= 9.0.100   | Indication if this is a built-in Check. |
| >= 9.0.100   | Default severity of a Check. |
| >= 9.0.100   | Number of projects that had this rule enabled. |
| >= 9.0.100   | List of explicit severities set for this rule (those can vary per project - hence list). |
| >= 9.0.100   | Count of diagnostics with Message severity emitted by this rule. |
| >= 9.0.100   | Count of diagnostics with Warning severity emitted by this rule. |
| >= 9.0.100   | Count of diagnostics with Error severity emitted by this rule. |
| >= 9.0.100   | Indication whether the rule was throttled. |
| >= 9.0.100   | Execution time spent by executing the Check defining this rule |

#### BuildCheck Extensibility issues

| SDK versions | Data |
|--------------|------|
| >= 9.0.100   | Corelation guid for the run. |
| >= 9.0.100   | Hashed name of assembly that was referenced as a custom Check. |
| >= 9.0.100   | Hashed exception type thrown when attempting to load the custom check. |
| >= 9.0.100   | Hashed exception message thrown when attempting to load the custom check. |

### General Build

Expressed and collected via [BuildTelemetry type](https://github.com/dotnet/msbuild/blob/94941d9cb26bb86045452b4a174a357b65a30c99/src/Framework/Telemetry/BuildTelemetry.cs)

| SDK versions | Data |
|--------------|------|
| All          | Display version of the Engine suitable for display to a user. |
| All          | Duration of the build - from when it was requested (via API or CLI). |
| All          | Duration of the build - from when it was started by internal BuildManager. |
| All          | Build engine runtime name. |
| All          | Host in which MSBuild build was executed (e.g. "VS", "VSCode", "Azure DevOps", "GitHub Action", "CLI"). |
| All          | State of MSBuild server process before this build (one of 'cold', 'hot', null (if not run as server)). |
| All          | Path to project file. |
| All          | MSBuild server fallback reason (either "ServerBusy", "ConnectionError" or null (no fallback)). |
| All          | Overall build success (true, false). |
| All          | Build target. |
| All          | Version of MSBuild. |
| >= 9.0.100   | Indication of enablement of BuildCheck feature. |
| >= 9.0.100   | Indication of Smart App Control being in evaluation mode on machine executing the build. |
