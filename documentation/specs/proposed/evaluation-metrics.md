# MSBuild evaluation metrics

## Motivation

MSBuild project evaluations can be initiated by a build submission or directly through the object model. The latter includes SDK operations such as loading a `Project` or creating a `ProjectInstance` before a build begins. These evaluations do not necessarily reach the binary logger attached to the subsequent build, which makes their performance difficult to track consistently.

MSBuild exposes process-local evaluation measurements through `System.Diagnostics.Metrics`. MSBuild does not configure an exporter or send these measurements anywhere. Hosts and diagnostic tools choose whether and how to collect them.

## Meter

The meter name is `Microsoft.Build`, matching the assembly that emits the measurements. This is intentionally separate from the `Microsoft.Build.Telemetry*` activity sources used for sampled Visual Studio telemetry.

| Instrument | Type | Unit | Description |
| --- | --- | --- | --- |
| `msbuild.project.evaluations` | `Counter<long>` | `{evaluation}` | Number of project evaluations |
| `msbuild.project.evaluation.duration` | `Histogram<double>` | `s` | Duration of project evaluations |

The histogram also exposes a count to consumers that aggregate histogram measurements. The separate counter supports lightweight consumers that only subscribe to evaluation counts. The two counts describe the same evaluations and must not be added together.

Both instruments use the following low-cardinality tags:

| Tag | Values | Description |
| --- | --- | --- |
| `msbuild.project.evaluation.stage` | `full` | Evaluation stage; all evaluations run through every pass |
| `msbuild.project.evaluation.origin` | `build_submission`, `standalone` | Whether the evaluation belongs to a build submission or was initiated directly through the object model |
| `msbuild.project.evaluation.succeeded` | `true`, `false` | Whether evaluation completed successfully |

Project paths, target frameworks, global properties, and other potentially sensitive or high-cardinality values are intentionally excluded.

Each invocation of the evaluator is recorded separately, including explicit project reevaluations. Duration measures the evaluator passes themselves and excludes evaluator setup and final logging.

## Relationship to existing diagnostics

`MSBuildEventSource.EvaluateStart` and `EvaluateStop` remain the detailed tracing mechanism. Project evaluation build events remain available to loggers and binary logs when a binary logger is attached to the relevant logging service.

The metrics provide a low-volume aggregate view and also cover standalone evaluations without requiring a logger. A build evaluation can appear in both a binlog and the metrics stream, so consumers must not add the two counts together.

## Collection

Consumers can subscribe with `MeterListener` or an OpenTelemetry `MeterProvider`. Collection is process-local: a host that needs measurements from SDK processes, MSBuild server processes, and worker nodes must collect the `Microsoft.Build` meter from each process.

The metrics are also available on .NET Framework, where the `Microsoft.Build` package declares its `System.Diagnostics.DiagnosticSource` dependency. Metrics initialization and listener failures degrade to a no-op rather than affecting evaluation.

Metrics are local diagnostics and MSBuild does not export them. Consequently, the MSBuild and .NET SDK telemetry opt-out environment variables do not disable metric publication. A host that configures an exporter remains responsible for honoring its applicable collection and consent policies.

Evaluations performed while constructing a project graph are categorized as `standalone` because they do not belong to an active build submission.
