Welcome to MSBuild docs!

The folder contains collection of docs and references for MSBuild, detailed information on how to work with this repo, and covers in-depth technical topics related to implementation.

## Getting Started

* [What is MSBuild?](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild)
* Building Testing and Debugging
  * [Full Framework MSBuild](wiki/Building-Testing-and-Debugging-on-Full-Framework-MSBuild.md)
  * [.Net Core MSBuild](wiki/Building-Testing-and-Debugging-on-.Net-Core-MSBuild.md)
  * [macOS](wiki/Mac-Debugging.md)

* [MSBuild resources](wiki/MSBuild-Resources.md)
* [MSBuild tips & tricks](wiki/MSBuild-Tips-&-Tricks.md)

## NuGet packages

* [General information](consuming-nuget-package.md)

## Release information

* [Changelog](Changelog.md)
* [Release process](release.md)
* [Change waves](wiki/ChangeWaves.md)
* [Interactions with the internal repository](wiki/Interactions-with-the-internal-repository.md)

## Development and contributing

* [Providing binary logs for investigation](wiki/Providing-Binary-Logs.md)
* [Contributing code](wiki/Contributing-Code.md)
   * [Contributing tasks](wiki/Contributing-Tasks.md)
* [Error codes](assigning-msb-error-code.md)
* [Deploying built MSBuild](Deploy-MSBuild.md)
* [Events emitted by MSBuild](specs/event-source.md)
* [Change waves (for developers)](wiki/ChangeWaves-Dev.md)
* [GitHub labels](wiki/Labels.md)
* [Localization](wiki/Localization.md)

### Problems?

* [Rebuilding when nothing changed](wiki/Rebuilding-when-nothing-changed.md)
* [Controling References Behavior](wiki/Controlling-Dependencies-Behavior.md)
* [Something's wrong in my build](wiki/Something's-wrong-in-my-build.md)
* [Some gotchas around the Microsoft.Build.Framework project/assembly](wiki/Microsoft.Build.Framework.md)
* [GAC and MSBuild](wiki/UnGAC.md)
* [When globbing returns original filespec](WhenGlobbingReturnsOriginalFilespec.md)

## In-depth tech topics

* [Reserved and built-in properties](Built-in-Propeties.md)
* [`ProjectReference`](ProjectReference-Protocol.md)
* [MSBuild Server](MSBuild-Server.md)
* [Low priority nodes](specs/low-priority-switch.md)
* [Threading in MSBuild worker nodes](specs/threading.md)
* [Nodes orchestration](wiki/Nodes-Orchestration.md)
* [Project cache plugin](specs/project-cache.md)
* [Support for remote host objects](specs/remote-host-object.md)
* [Static graph](specs/static-graph.md)
* [Single project isolated builds: implementation details](specs/single-project-isolated-builds.md)
* [Task isolation](specs/task-isolation-and-dependencies.md)
* [Target maps](wiki/Target-Maps.md)
* [Managing parallelism in MSBuild](specs/resource-management.md)
* [SDK resolution](specs/sdk-resolvers-algorithm.md)
* [RAR core scenarios](specs/rar-core-scenarios.md)

### Tasks

* [`ResolveAssemblyReference`](wiki/ResolveAssemblyReference.md)

### Evaluation

* [Evaluation profiling](evaluation-profiling.md)

### Logging

* [Binary log](wiki/Binary-Log.md)
* [Terminal logger: how to opt in](terminallogger/Opt-In-Mechanism.md)

## Archived Designs
* [Resolve Assembly Reference as a service](specs/rar-as-service.md)
   * Prototype: https://github.com/dotnet/msbuild/issues/6193

## Proposed Designs
* [Packages Sourcing](specs/proposed/interactive-package-references.md)
* [Secrets Metadata](specs/proposed/security-metadata.md)

## Community contributions

* [MSBuild overview](Contributions/MSBuild-overview.md)
* [Solution parser](Contributions/solution-parser.md)

Note: community contributions has documentation that was contributed by developers or users, but it might not been fully vetted for accuracy and correctness. Explanations in this folder may not be fully accurate, but can still be very informative for developing an understanding of MSBuild or a specific problem.