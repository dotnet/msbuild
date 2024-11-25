# Decoupling VS builds of SDK projects
*The objective of this one-pager is to provide clear and concise information about a feature or project to foster inclusion and collaboration by enabling anyone who is interested to learn the essential information, such as the project's goals, motivation, impact, and risks. The points and questions in each section are illustrative, they are not intended as a definite list of questions to be answered.*

Ensure that all the logic from a build, from an SDK project comes from the SDK independently of where it is being built.

## Description
 - Short summary of the feature or effort.

## Goals and Motivation
 - What are we trying to achieve and why? 

Consistency of end-user build experience
Decoupling the SDK from VS
Isolate the SDK builds to use only their components.

Why:
Experience of tooling authors
 - Roslyn analyzer authors
 - MSBuild Task authors

experience of end users
 - Anyone who uses the analyzers or source egenrators

Tooling authors need to target NetStandard2.0, or multi target and dependencies in multitargets is annoying. If you don't match the roslyn version for VS the analyzers and generators don't work.

## Impact
Multiple layer of impact:
 - Project construction
 - reduce cost of development for internal teams that contribute to Roslyn analyzers / source build generators and MSBuild Tasks.

 End users will not experience mismatch between analyzer versions. And they will be sure that the build will be the same as the command line invocation.

## Stakeholders
internal folks are the ones that will continue the work to fully complete the feature. 
 - VS Perf team: 
 - Project System team:
 - Roslyn team: Handover - once we're sending the environmental variable, they can enable the use of the core compiler in VS. The second handover is the same as the rest: you can write only .net code tasks and the sdk projects will build successfully.

These are the folks that will benefit of these changes:
 - Analyzer / source generator author
 - MSBuild Task authors

## Risks
 - There might be a performance hit on VS depending on how many non-framework tasks the project needs to load. As we can't do some pre-loading.
 - There should be no breaking from SDK only users. The IDE tooling might have a different version, which leads to discrepancy on partial builds.
 - We are early in the development effort, so if later there is a larger impact on perf or other issues, the effort in general might be delayed(?), but our part would already have been completed.
 - Deadline: no concrete deadline, but early in the preview cycle (preview 4-5) to get a sense of consequences of this change.
 - If we don't do this: Worse experience for devs that work in the area.

## Cost
1. Dev week's time
2. Dev 1-2 months time.
3. Dev 1 week if things do not go wrong at all.

## Plan
 1. Ensure that MSBuild.exe provides the same execution state as the dotnet command line invocation. MSExtensionPath (cant do that), DotNetHostPath, MSSDKsPath (cant do that).
    -  Low effort, should be done first. 
 2. Implement .NET core task host, so we can execute core version of tasks.
    - Get Rainer feedback, seems like a medium sized.
 3. Load common targets from the SDK and not .NetFramework (the VS version of it). This might be out of scope for .NET 10
    - Medium effort, can have behavioral changes.

