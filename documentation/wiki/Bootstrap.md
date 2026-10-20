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

## Engineering SDK and Bootstrap SDK

The engineering SDK and the bootstrap SDK can use different .NET releases.
`tools.dotnet` in `global.json` provisions the SDK used by Arcade for the outer build.
`sdk.version` selects the SDK for ordinary SDK commands and Framework-hosted SDK resolution.
`BootstrapSdkVersion` in `eng/Versions.props` selects the SDK used to test the freshly built MSBuild.
The product target framework is controlled separately by `LatestDotNetCoreForMSBuild`.

The checked-in `global.json` keeps the source SDK at the bootstrap version. The configure-toolset
hooks install it alongside the engineering SDK, preserving the newer shared host files. Core
engineering builds launch the engineering SDK's actual `MSBuild.dll` directly, independently of
the source SDK selector. Framework builds use the source SDK's targets and runtime configuration.

For a `-stage2` build (`--stage2` on Unix), the build scripts select the freshly built Core or
Framework engine, the bootstrap .NET host, and the already-restored stage1 Arcade toolset.
An early validation target checks the selected SDK imports and runtime configuration.
Neither stage rewrites `global.json`, and no checkout lock, backup, or recovery protocol is needed.

The build scripts also set Arcade's `TestDotNetRoot` to that bootstrap installation so test apphosts
run on its runtime rather than rolling forward to the engineering SDK's newer runtime.
API-level task hosts use the test host's selected `dotnet`; explicit bootstrap-launch helpers select
the bootstrap host separately. This also lets the non-self-build test lane run on the engineering
runtime without forcing its task hosts onto the older bootstrap runtime.

To build a repository sample with the Core bootstrap directly:

```powershell
artifacts\bin\bootstrap\core\dotnet.exe build src\Samples\Dependency\Dependency.csproj
```

## Potential Cons
The reliance on downloading the SDK from a remote source requires an internet connection. For the initial build of the repository, this doesn't change as the SDK is always downloaded to the .dotnet folder first. For subsequent runs, the SDK in bootstrap will be downloaded again only **if the requested version was changed**.

## Pros
This approach simplifies testing MSBuild as part of dotnet by providing a ready and reliable environment without needing to patch anything into a globally installed SDK, as was previously required.