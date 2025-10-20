# Bootstrap MSBuild

Because the binaries and build logic in this repo aren't sufficient to build real-world projects, we need a test environment that mimics the real-world combinations of MSBuild, Roslyn compilers, and other things that combine in the .NET SDK and in Visual Studio to produce a functional build environment. We call this the "bootstrap".

## Quick Intro
The document describes the logic behind the bootstrap and testing capabilities for the fresh MSBuild bits.

## History
MSBuild supports two different environments: .NET and .NET Framework. To test changes for .NET, fresh bits were published (the actual target Publish run) to the MSBuild.Bootstrap folder. These bits, along with specific dependencies, were later combined with parts of the .NET SDK that was used to build MSBuild to the bootstrap, making them ready for use with dotnet.exe. To execute the bootstrap MSBuild, you'd combine the `dotnet.exe` muxer from the .dotnet folder with the path to the bootstrap's `MSBuild.dll`.

## Current Implementation for .NET
During the bootstrap phase, install-scripts are used to download a full copy of the .NET SDK compatible with the current version. The logic for interacting with the scripts has been encapsulated in a separate MSBuild task: InstallDotNetCoreTask.cs. Here’s what happens under the hood:

The SDK is downloaded to the bootstrap folder.
Fresh MSBuild bits are then copied to this folder.
The constructed SDK is used for both local end-to-end tests and CI runs.

## Potential Cons
The reliance on downloading the SDK from a remote source requires an internet connection. For the initial build of the repository, this doesn't change as the SDK is always downloaded to the .dotnet folder first. For subsequent runs, the SDK in bootstrap will be downloaded again only **if the requested version was changed**.

## Pros
This approach simplifies testing MSBuild as part of dotnet by providing a ready and reliable environment without needing to patch anything into a globally installed SDK, as was previously required.