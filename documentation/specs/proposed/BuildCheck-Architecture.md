
# BuildCheck - Architecture and Implementation Spec

This is internal engineering document. For general overview and user point of view - please check the [BuildCheck - Design Spec](BuildCheck.md).

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

# Table of Contents

- [Infrastructure and Execution](#infrastructure-and-execution)
   * [Data Source](#data-source)
   * [Execution Modes](#execution-modes)
   * [Live Mode Hosting](#live-mode-hosting)
   * [Handling the Distributed Model](#handling-the-distributed-model)
   * [Analyzers Lifecycle](#analyzers-lifecycle)
- [Configuration](#configuration)
- [Acquisition](#acquisition)
- [Build OM for Analyzers Authoring](#build-om-for-analyzers-authoring)

# Infrastructure and Execution

## Data Source

The major source of data for BuildCheck will be the `BuildEventArgs` data - as it is already well established diagnostic source for MSBuild builds.

BuildCheck can source this data either offline from the binlog, or as a plugged logger during the live build execution. Choice was made to support both modes.

## Execution Modes

**Replay Mode** - so that users can choose to perform analyses post build, without impacting the performance of the build. And so that some level of analysis can be run on artifacts from builds produced by older versions of MSBuild.

**Live mode** - this is what users are used to from compilation analyses. Integrating into build execution will as well help driving adoption by opting-in users by default to some level of checking and hence exposing them to the feature.

## Live Mode Hosting

Prerequisity: [MSBuild Nodes Orchestration](../../wiki/Nodes-Orchestration.md#orchestration)

The BuildCheck infrastructure will be prepared to be available concurrently within the `entrypoint node` as well as in the additional `worker nodes`. There are 2 reasons for this:
* BuildCheck will need to recognize custom analyzers packages during the evaluation time - so some basic code related to BuildCheck will need to be present in the worker node.
* Presence in worker node (as part of the `RequestBuilder`), will allow inbox analyzers to agile leverage data not available within `BuildEventArgs` (while data prooved to be useful should over time be exposed to `BuildEventArgs`)

## Handling the Distributed Model

We want to get some bnefits (mostly inbox analyzers agility) from hosting BuildCheck infrastructure in worker nodes, but foremost we should prevent leaking the details of this model into public API and OM, until we are sure we cannot achive all goals from just entrypoint node from `BuildEventArgs` (which will likely never happen - as the build should be fully reconstructable from the `BuildEventArgs`).

How we'll internally handle the distributed model:
* Each node will have just a single instance of infrastructure (`IBuildCheckManager`) available (registered via the MSBuild DI - `IBuildComponentHost`). This applies to a entrypoint node with inproc worker node as well.
* Entrypoint node will have an MSBuild `ILogger` registered that will enable funneling data from worker nodes BuildChecks to the entrypoint node BuildCheck - namely:
    * Acquisition module will be able to communicated to the entrypoint node that particular analyzer should be loaded and instantiated
    * Tracing module will be able to send partitioned stats and aggregate them together
    * Theoretical execution-data-only sourcing inbox analyzer will be able to aggregate data from the whole build context (again - we should use this only for agility purposes, but shoot for analyzer that needs presence only in entrypoint node).
* Appart from the scenarios above - the BuildCheck infrastructure modules in individual nodes should be able to function independently (namely - load the inbox analyzers that should live in nodes; send the analyzers reports via logging infrastructure; load user configuration from `.editorconfig` and decide on need to enable/disable/configure particular analyzers).
* Communication from main to worker node between BuildCheck infra modules is not planned.

## Analyzers Lifecycle

Planned model:
* Analyzers factories get registered with the BuildCheck infrastructure (`BuildCheckManager`)
    * For inbox analyzers - this happens on startup.
    * For custom analyzers - this happens on connecting `ILogger` instance in entrypoint node receives acquistion event (`BuildCheckAcquisitionEventArgs`).
* `BuildCheckManager` receives info about new project starting to be build
    * On entrypoint node the information is sourced from `ProjectEvaluationStartedEventArgs`
    * On worker node this is received from `RequestBuilder.BuildProject`
* `BuildCheckManager` calls Configuration module and gets information for all analyzers in it's registry
    * Analyzers with issues in configuration (communicated via `BuildCheckConfigurationException`) will be deregistered for the rest of the build.
    * Global configuration issue (communicated via `BuildCheckConfigurationException`) will lead to defuncting whole BuildCheck.
* `BuildCheckManager` instantiates all newly enabled analyzers and updates configuration for all allready instantiated analyzers.
* At that point of time analyzers are prepared for receiving data and performing their work. MSBuild will start calling `BuildCheckManager` callbacks (mostly pumping `BuildEventArgs`), passed data will be transalted into BuildCheck OM and passed to analyzers.
* Analyzers may decide to report results of their findings (via `BuildCopDataContext.ReportResult`), the infrastructure will then perform post-processing (filter out reports for `Rule`s that are disabled, set the severity based on configuration) and send the result via the standard MSBuild logging infrastructure.

# Configuration

**TBD** - implementation details to be amended by @f-alizada 

# Acquisition

**TBD** - implementation details to be amended by @YuliiaKovalova


# Build OM for Analyzers Authoring

**TBD** - details for the initial inbox analyzers set to be amended by @ladipro