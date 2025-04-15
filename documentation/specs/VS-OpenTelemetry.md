# Telemetry via OpenTelemetry design

VS OTel provide packages compatible with ingesting data to their backend if we instrument it via OpenTelemetry traces (System.Diagnostics.Activity).
VS OTel packages are not open source so we need to conditionally include them in our build only for VS and MSBuild.exe

> this formatting is a comment describing how the implementation turned out in 17.14 when our original goals were different

[Onepager](https://github.com/dotnet/msbuild/blob/main/documentation/specs/proposed/telemetry-onepager.md)

## Concepts

It's a bit confusing how things are named in OpenTelemetry and .NET and VS Telemetry and what they do.

| OTel concept | .NET/VS | Description |
| --- | --- | --- |
| Span/Trace | System.Diagnostics.Activity |  Trace is a tree of Spans. Activities can be nested.|
| Tracer | System.Diagnostics.ActivitySource | Creates activites.  |
| Processor/Exporter | VS OTel provided default config | filters and saves telemetry as files in a desired format |
| TracerProvider | OTel SDK TracerProvider | Singleton that is aware of processors, exporters and Tracers and listens (in .NET a bit looser relationship because it does not create Tracers just hooks to them) |
| Collector | VS OTel Collector | Sends to VS backend |

## Requirements

### Performance

- If not sampled, no infra initialization overhead.
- Avoid allocations when not sampled.
- Has to have no impact on Core without opting into tracing, small impact on Framework
- No regression in VS perf ddrit scenarios.

> there is an allocation regression when sampled, one of the reasons why it's not enabled by default

### Privacy

- Hashing data points that could identify customers (e.g. names of targets)
- Opt out capability

### Security

- Providing or/and documenting a method for creating a hook in Framework MSBuild
- If custom hooking solution will be used - document the security implications of hooking custom telemetry Exporters/Collectors in Framework
- other security requirements (transportation, rate limiting, sanitization, data access) are implemented by VS Telemetry library or the backend

> hooking in Framework not implemented

### Data handling

- Implement head [Sampling](https://opentelemetry.io/docs/concepts/sampling/) with the granularity of a MSBuild.exe invocation/VS instance.
- VS Data handle tail sampling in their infrastructure not to overwhelm storage with a lot of build events.

#### Data points

The data sent via VS OpenTelemetry is neither a subset neither a superset of what is sent to SDK telemetry and it is not a purpose of this design to unify them.

##### Basic info

- Build duration
- Host
- Build success/failure
- Version
- Target (hashed)

##### Evnironment

- SAC (Smart app control) enabled

##### Features

- BuildCheck enabled
- Tasks runtimes and memory usage
- Tasks summary - whether they come from Nuget or are custom
- Targets summary - how many loaded and executed, how many come from nuget, how many come from metaproject

The design should allow for easy instrumentation of additional data points.
> current implementation has only one datapoint and that is the whole build `vs/msbuild/build`, the instrumentaiton of additional datapoints is gated by first checking that telemetry is running and using `Activity` classes only in helper methods gated by `[MethodImpl(MethodImplOptions.NoInlining)]` to avoid System.Diagnostics.DiagnosticSource dll load.

## Core `dotnet build` scenario

- Telemetry should not be collected via VS OpenTelemetry mechanism because it's already collected in sdk.
- opt in to initialize the ActivitySource to avoid degrading performance.
- [baronfel/otel-startup-hook: A .NET CLR Startup Hook that exports OpenTelemetry metrics via the OTLP Exporter to an OpenTelemetry Collector](https://github.com/baronfel/otel-startup-hook/) and similar enable collecting telemetry data locally by listening to the ActivitySource prefix defined in MSBuild.

> this hook can be used when the customer specifies that they want to listen to the prefix `Microsoft.VisualStudio.OpenTelemetry.MSBuild`, opt in by setting environment variables `MSBUILD_TELEMETRY_OPTIN=1`,`MSBUILD_TELEMETRY_SAMPLE_RATE=1.0`

## Standalone MSBuild.exe scenario

- Initialize and finalize in Xmake.cs
 ActivitySource, TracerProvider, VS Collector
- overhead of starting VS collector is nonzero
- head sampling should avoid initializing if not sampled

## VS in proc (devenv) scenario

- VS can call `BuildManager` in a thread unsafe way the telemetry implementation has to be mindful of [BuildManager instances acquire its own BuildTelemetry instance by rokonec · Pull Request #8444 · dotnet/msbuild](https://github.com/dotnet/msbuild/pull/8444)
  - ensure no race conditions in initialization
  - only 1 TracerProvider with VS defined processing should exist
- Visual Studio should be responsible for having a running collector, we don't want this overhead in MSBuild and eventually many will use it

> this was not achieved in 17.14 so we start collector every time

## Implementation and MSBuild developer experience

### ActivitySource names

- Microsoft.VisualStudio.OpenTelemetry.MSBuild.Default

### Sampling

Our estimation from VS and SDK data is that there are 10M-100M build events per day.
For proportion estimation (of fairly common occurence in the builds), with not very strict confidnece (95%) and margin for error (5%) sampling 1:25000 would be enough.

- this would apply for the DefaultActivitySource
- other ActivitySources could be sampled more frequently to get enough data
- Collecting has a cost, especially in standalone scenario where we have to start the collector. We might decide to undersample in standalone to avoid performance frequent impact.
- We want to avoid that cost when not sampled, therefore we prefer head sampling.
- Enables opt-in and opt-out for guaranteed sample or not sampled.
- nullable ActivitySource, using `?` when working with them, we can be initialized but not sampled -> it will not reinitialize but not collect telemetry.

- for 17.14 we can't use the new OTel assemblies and their dependencies, so everything has to be opt in.
- eventually OpenTelemetry will be available and usable by default
- We can use experiments in VS to pass the environment variable to initialize

> Targeted notification can be set that samples 100% of customers to which it is sent

### Initialization at entrypoints

- There are 2 entrypoints:
  - for VS in BuildManager.BeginBuild
  - for standalone in Xmake.cs Main

### Exiting

Force flush TracerProvider's exporter in BuildManager.EndBuild.
Dispose collector in Xmake.cs at the end of Main.

### Configuration

- Class that's responsible for configuring and initializing telemetry and handles optouts, holding tracer and collector.
- Wrapping source so that it has correct prefixes for VS backend to ingest.

### Instrumenting

2 ways of instrumenting:

#### Instrument areas in code running in the main process

```csharp
using (Activity? myActivity = OpenTelemetryManager.DefaultActivitySource?.StartActivity(TelemetryConstants.NameFromAConstantToAvoidAllocation))
{
// something happens here

// add data to the trace
myActivity?.WithTag("SpecialEvent","fail")
}
```

Interface for classes holding telemetry data

```csharp
IActivityTelemetryDataHolder data = new SomeData();
...
myActivity?.WithTags(data);
```

> currently this should be gated in a separate method to avoid System.DiagnosticDiagnosticsource dll load.

#### Default Build activity in EndBuild

- this activity would always be created at the same point when sdk telemetry is sent in Core
- we can add data to it that we want in general builds
- the desired count of data from this should control the sample rate of DefaultActivitySource

#### Multiple Activity Sources

We want to create ActivitySources with different sample rates, this requires either implementation server side or a custom Processor.

We potentially want apart from the Default ActivitySource:

1. Other activity sources with different sample rates (in order to get significant data for rarer events such as custom tasks).
2. a way to override sampling decision - ad hoc starting telemetry infrastructure to catch rare events

- Create a way of using a "HighPrioActivitySource" which would override sampling and initialize Collector in MSBuild.exe scenario/tracerprovider in VS.
- this would enable us to catch rare events

> not implemented

### Implementation details

- `OpenTelemetryManager` - singleton that manages lifetime of OpenTelemetry objects listening to `Activity`ies, start by initializing in `Xmake` or `BuildManager`.
- Task and Target data is forwarded from worker nodes via `TelemetryForwarder` and `InternalTelemetryForwardingLogger` and then aggregated to stats and serialized in `TelemetryDataUtils` and attached to the default `vs/msbuild/build` event.

## Future work when/if we decide to invest in telemetry again

- avoid initializing/finalizing collector in VS when there is one running
- multiple levels of sampling for different types of events
- running by default with head sampling (simplifies instrumentation with `Activity`ies)
- implement anonymization consistently in an OTel processor and not ad hoc in each usage
- add datapoints helping perf optimization decisions/ reliability investigations
