
# BuildCheck - Architecture and Implementation Spec

This is an internal engineering document. For general overview and user point of view - please check the [BuildCheck - Design Spec](BuildCheck.md).

# Areas of Ownership

| Area     |      Owner     |
|----------|:-------------|
| PM                  | @baronfel |
| Advisory/Leadership | @rainersigwald |
| Infrastructure      | @jankrivanek |
| Configuration       | @f-alizada   |
| Custom Analyzers    | @YuliiaKovalova |
| Inbox Analyzers     | @ladipro |
| Replay Mode         | @surayya-MS |
| Tracing             | @maridematte |
| Perf Advisory       | @AR-May |


# Infrastructure and Execution

## Data Source

The major source of data for BuildCheck will be the `BuildEventArgs` data - as it is already well established diagnostic source for MSBuild builds.

BuildCheck can source this data either offline from the binlog, or as a plugged logger during the live build execution. Choice was made to support both modes.

The actual OM exposed to users will be translating/mapping/proxying the underlying MSBuild OM and hence the implementation details and actual extent of the data (whether internal or public) will be hidden.

### Sourcing unexposed data from within execution

For agility we'll be able to source internal data during the evaluation and/or execution directly from the build engine, without the `BuildEventArgs` exposure.
One example of rich data that might be helpful for internal analyses is [`Project`](https://github.com/dotnet/msbuild/blob/28f488a74ed75bf5f21ca93ac2463a8cb1586d79/src/Build/Definition/Project.cs#L49). This OM is not currently being used during the standard build execution (`ProjectInstance` is used instead) - but we can conditionaly create and expose `Project` and satisfy the current internal consumers of `ProjectInstance` - spike of that is available [in experimental branch](https://github.com/dotnet/msbuild/compare/main...JanKrivanek:msbuild:research/analyzers-evaluation-hooking#diff-08a12a2fa138c3bfcabc7639bb75dda8534f3b662db4aca4f2b5595dbf9ba197).

## Execution Modes

**Replay Mode** - so that users can choose to perform analyses post build, without impacting the performance of the build. And so that some level of analysis can be run on artifacts from builds produced by older versions of MSBuild.

**Live mode** - this is what users are used to from compilation analyses. Integrating into build execution will as well help driving adoption by opting-in users by default to some level of checking and hence exposing them to the feature.

## Live Mode Hosting

Prerequisites: [MSBuild Nodes Orchestration](../../wiki/Nodes-Orchestration.md#orchestration)

The BuildCheck infrastructure will be prepared to be available concurrently within the `scheduler node` as well as in the additional `worker nodes`. There are 2 reasons for this:
* BuildCheck will need to recognize custom analyzers packages during the evaluation time - so some basic code related to BuildCheck will need to be present in the worker node.
* Presence in worker node (as part of the `RequestBuilder`), will allow inbox analyzers to agile leverage data not available within `BuildEventArgs` (while data proven to be useful should over time be exposed to `BuildEventArgs`)

## Handling the Distributed Model

We want to get some benefits (mostly inbox analyzers agility) from hosting BuildCheck infrastructure in worker nodes, but foremost we should prevent leaking the details of this model into public API and OM, until we are sure we cannot achieve all goals from just scheduler node from `BuildEventArgs` (which will likely never happen - as the build should be fully reconstructable from the `BuildEventArgs`).

How we'll internally handle the distributed model:
* Each node will have just a single instance of infrastructure (`IBuildCheckManager`) available (registered via the MSBuild dependency injection container - `IBuildComponentHost`). This applies to a scheduler node with inproc worker node as well.
* Scheduler node will have an MSBuild `ILogger` registered that will enable communicating information from worker nodes BuildCheck module to the scheduler node BuildCheck module - namely:
    * Acquisition module from worker node will be able to communicated to the scheduler node that it encountered `PackageReference` for particular analyzer and that it should be loaded and instantiated in the main node.
    * Tracing module will be able to send perf stats from current worker node and aggregate all of those together in the main node.
    * Theoretical execution-data-only sourcing inbox analyzer will be able to aggregate data from the whole build context (again - we should use this only for agility purposes, but shoot for analyzer that needs presence only in scheduler node). The way to do that can be via being present in all worker nodes, sending a specific type of 'in progress result' BuildEventArgs and aggreggating those intermediary results in the single instance running in the main node.
* Apart from the scenarios above - the BuildCheck infrastructure modules in individual nodes should be able to function independently (namely - load the inbox analyzers that should live in nodes; send the analyzers reports via logging infrastructure; load user configuration from `.editorconfig` and decide on need to enable/disable/configure particular analyzers).
* The custom analyzers will be hosted only in the main node - and hence the distributed model will be fully hidden from them. This might be a subject for revision in future versions.
* Communication from main to worker node between BuildCheck infra modules is not planned (this might be revisited - even for the V1).

## Analyzers Lifecycle

Planned model:
* Analyzers factories get registered with the BuildCheck infrastructure (`BuildCheckManager`)
    * For inbox analyzers - this happens on startup.
    * For custom analyzers - this happens on connecting `ILogger` instance in scheduler node receives acquistion event (`BuildCheckAcquisitionEventArgs`). This event is being sent by worker node as soon as it hits a special marker (a magic property function call) during early evaluation. Loading is not processed by worker node as currently we want custom analyzers only in the main node (as they will be only given data proxied from BuildEventArgs).
    The `BuildCheckAcquisitionEventArgs` should be sent prior `ProjectEvaluationStartedEventArgs` (buffering will need to take place), or main node will need to replay some initial data after custom analyzer is registered.
* `BuildCheckManager` receives info about new project starting to be build
    * On scheduler node the information is sourced from `ProjectEvaluationStartedEventArgs`
    * On worker node this is received from `RequestBuilder.BuildProject`
* `BuildCheckManager` calls Configuration module and gets information for all analyzers in it's registry
    * Analyzers with issues in configuration (communicated via `BuildCheckConfigurationException`) will issue an error and then be deregistered for the rest of the build.
    * Global configuration issue (communicated via `BuildCheckConfigurationException`) will issue an error and then entirely disable BuildCheck.
* `BuildCheckManager` instantiates all newly enabled analyzers and updates configuration for all already instantiated analyzers.
* At that point of time analyzers are prepared for receiving data and performing their work. MSBuild will start calling `BuildCheckManager` callbacks (mostly pumping `BuildEventArgs`), passed data will be translated into BuildCheck OM and passed to analyzers.
* Analyzers may decide to report results of their findings (via `BuildCheckDataContext.ReportResult`), the infrastructure will then perform post-processing (filter out reports for `Rule`s that are disabled, set the severity based on configuration) and send the result via the standard MSBuild logging infrastructure.
* Analysis result might hence be reported after project's final `ProjectFinishedEventArgs`
* Final status of the build should not be reported (and `BuildFinishedEventArgs` logged) until all analyzers are done processing and their results are accounted for.

# Configuration

**TBD** - implementation details to be amended by @f-alizada 

# Acquisition

**TBD** - implementation details to be amended by @YuliiaKovalova


# Build OM for Analyzers Authoring

**TBD** - details for the initial inbox analyzers set to be amended by @ladipro
