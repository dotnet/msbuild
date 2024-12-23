# Telemetry via OpenTelemetry design

VS OTel provide packages compatible with ingesting data to their backend if we instrument it via OpenTelemetry traces (System.Diagnostics.Activity).
VS OTel packages are not open source so we need to conditionally include them in our build only for VS and MSBuild.exe

[Onepager](https://github.com/dotnet/msbuild/blob/main/documentation/specs/proposed/telemetry-onepager.md)

## Requirements

### Performance

- If not sampled, no infra initialization overhead.
- Avoid allocations when not sampled.
- Has to have no impact on Core without opting into tracing, small impact on Framework

### Privacy

- Hashing data points that could identify customers (e.g. names of targets)
- Opt out capability

### Security

- Providing a method for creating a hook in Framework MSBuild
- document the security implications of hooking custom telemetry Exporters/Collectors in Framework
- other security requirements (transportation, rate limiting, sanitization, data access) are implemented by VS Telemetry library or the backend

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

- BuildCheck enabled?

The design allows for easy instrumentation of additional data points.

## Core `dotnet build` scenario

- Telemetry should not be collected via VS OpenTelemetry mechanism because it's already collected in sdk.
- There should be an opt in to initialize the ActivitySource to avoid degrading performance.
- [baronfel/otel-startup-hook: A .NET CLR Startup Hook that exports OpenTelemetry metrics via the OTLP Exporter to an OpenTelemetry Collector](https://github.com/baronfel/otel-startup-hook/) and similar enable collecting telemetry data locally by listening to the ActivitySource name defined in MSBuild.

## Standalone MSBuild.exe scenario

- Initialize and finalize in Xmake.cs
	- ActivitySource, TracerProvider, VS Collector
		- overhead of starting VS collector is fairly big (0.3s on Devbox)[JanProvaznik/VSCollectorBenchmarks](https://github.com/JanProvaznik/VSCollectorBenchmarks)
			- head sampling should avoid initializing if not sampled

## VS scenario

- VS can call `BuildManager` in a thread unsafe way the telemetry implementation has to be mindful of [BuildManager instances acquire its own BuildTelemetry instance by rokonec · Pull Request #8444 · dotnet/msbuild](https://github.com/dotnet/msbuild/pull/8444)
	- ensure no race conditions in initialization
	- only 1 TracerProvider with VS defined processing should exist
- Visual Studio should be responsible for having a running collector, we don't want this overhead in MSBuild and eventually many components can use it

## Implementation and MSBuild developer experience

### Sampling

- We need to sample before initalizing infrastructure to avoid overhead.
- Enables opt-in and opt-out for guaranteed sample or not sampled.
- nullable ActivitySource, using `?` when working with them, we can be initialized but not sampled -> it will not reinitialize but not collect telemetry.

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

#### Add data to activity in EndBuild

- this activity would always be created at the same point when sdk telemetry is sent in Core and we can add data to it

## Looking ahead

- Create a way of using a "HighPrioActivitySource" which would override sampling and initialize Collector in MSBuild.exe scenario/tracerprovider in VS.
    - this would enable us to catch rare events

## Uncertainties

- Configuring tail sampling in VS telemetry server side infrastructure to not overflow them with data.
- How much head sampling.
- In standalone we could start the collector async without waiting which would potentially miss some earlier traces (unlikely to miss the important end of build trace though) but would degrade performance less than waiting for it's startup. The performance and utility tradeoff is not clear.
- Can we make collector startup faster?
- We could let users configure sample rate via env variable.
- Do we want to send antivirus state? It seems slow.
