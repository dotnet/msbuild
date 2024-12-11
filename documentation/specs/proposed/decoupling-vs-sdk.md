# Decoupling VS builds of .NET SDK projects
The experience of building a .NET SDK project can differ significantly depending if the project was built using Visla Studio / MSBuild or `dotnet build`. The build can produce different diagnostics, use different language rules, etc... and that is because building a .NET SDK project from Visual Studio mixes components from MSBuild and the .NET SDK. This means core tooling, like the compiler, can be substantially different between the two types of build. This leads to customer confusion and hard to diagnose problems, as well as increased code workload. To solve this want to ensure that when building a .NET SDK project we use the components from the .NET SDK to do so.

## Goals and Motivation

We are aiming for:
 - Consistent end-user eperience for build in either DotNet CLI or Visual Studio.
 - Decoupling the .NET SDK experience from Visual Studio 
 - Decoupling the .NET SDK from VS.

The reason we are persuing this is for a better experience when using or writting Roslyn analyzers and MSBuild Tasks. Currently tooling authors need to target NetStandard2.0 for their projects to be recognized by VS. Another options is to multi-target but it takes a lot more effort and time spent on that. Another aspect is the user experience, if the Roslyn version for VS analyzers and generators doesn't match the one in Visual Studio, they do not work.

## Impact
There are a few area of impact:
 - .NET SDK style project builds will be more stable between VS and CLI builds, as the tooling will not be devided between different versions.
 - Reduced cost of development for external and internal teams that contribute to Roslyn Analyzers, SourceBuild generators, or MSBuild Tasks.
 - End-user will not experience mismatch between analyzer versions, and confirmation that their .NET SDK style builds will behave the same way in VS and in the command line.

## Stakeholders
For the internal stakeholder, we have the teams that will continue the work to fully complete the VS and .NET SDK decoupling feature after our base work is done. There are two handovers in this project:

1. Enabling the MSBuild.exe execution state to be the same as DotNet command line invocation so the Roslyn team can enable the use of their core compiler in VS.
2. Tasks and other projects can be written in .NET Core and the .NET SDK projects will build successuflly in VS. This enables other teams to migrate their tasks to .NET Core instead of keeping them targeting .NET Framework.

The handovers should allow other teams to proceed with their work smoothly and no change in build behaviour should be present within MSBuild.

## Risks
A few risks associated with this feature:
 - Our work is early in the development effort. If this feature is discovered to have too large of an impact in experience of performance the work might be delayed or discarded.
 - There might be a performance hit on VS once we start running tasks on .NET Core. It would depending on the amount of non-framwork tasks that the project will need to load when opening it in VS. The performance gain from pre-loading will not be available in this scenario.
 - There are no concrete deadlines for our part of the feature, but we are aiming for an early preview cycle, so we have a chance to measure the consequences and fix any issues that arise.

## Plan
 1. Ensure that MSBuild.exe provides the same execution state as the dotnet command line invocation.
    -  This is should take around 1 dev week to complete, and will be handed over to Roslyn team.
 2. Implement .NET Core task host, tasks can be executed on the .NET Core vresion instead of .NET framework.
    - This should take 1 to 2 dev months to complete, including extensive testing. This would be handed over to internal teams that have custom tasks so they can be updated and tested.
 3. Load common targets from the .NET SDK and not from .NET NetFramework on VS. This work depends on other team's finilizing their part of the feature and might not be in scope for .NET 10.
    - This should take a dev week, if there are no major bugs or regressions.

