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

## Choosing the Bootstrap SDK

By default, the bootstrap uses `BootstrapSdkVersion` from `eng/Versions.props`. The SDK used to **build this repository** remains selected by the repository's `global.json`, independently of the bootstrap SDK.

To create the bootstrap with the latest published daily SDK from **`dotnet/dotnet` main**:

```powershell
.\build.cmd -bootstrapDaily
```

```bash
./build.sh --bootstrap-daily
```

The build discovers main's daily channel from the [VMR builds table](https://github.com/dotnet/dotnet/blob/main/docs/builds-table.md), then invokes the official dotnet-install script with that channel and `Quality=daily`. It does not infer main from this repository's pinned version or the SDK that builds the VMR.

To choose a different daily channel, or bypass automatic channel discovery:

```powershell
.\build.cmd -bootstrapDaily -bootstrapSdkChannel 11.0.1xx-rc2
```

```bash
./build.sh --bootstrap-daily --bootstrap-sdk-channel 11.0.1xx-rc2
```

Use numeric installer channels such as `11.0`, `11.0.1xx`, or `11.0.1xx-rc2`. `main`, `LTS`, and `STS` are not daily channel overrides. The channel switch requires the daily switch.

Direct MSBuild invocations can pass `-p:UseDailyBootstrapSdk=true` and optionally `-p:BootstrapSdkChannel=<channel>`. The pinned `BootstrapSdkVersion` remains unchanged.

For two-stage builds, combine `-stage2` with `-bootstrapDaily` (or `--stage2` with `--bootstrap-daily`). The intermediate bootstrap used to rebuild the repository stays pinned; only the final bootstrap uses the daily SDK.

Omit the daily switch on the next build to return to the pinned bootstrap. SDK versions already downloaded remain available for reuse.

## Using the Selected SDK

The core bootstrap is installed under `artifacts/bin/bootstrap/core`. Fresh MSBuild binaries are overlaid into the **exact SDK version selected by the installer**, including its `Current` directory. Runtimeconfig files use the exact runtime bundled with that SDK.

After successful bootstrap creation, this directory contains:

- `bootstrap-sdk.version`: the selected SDK version, also used by bootstrap test helpers.
- `global.json`: a bootstrap-local SDK selection file, preventing another installed SDK from winning when running from this directory.

For example, in PowerShell:

```powershell
Push-Location .\artifacts\bin\bootstrap\core
.\dotnet.exe --version
.\dotnet.exe build C:\path\to\repro.csproj
Pop-Location
```

On Unix:

```bash
cd artifacts/bin/bootstrap/core
./dotnet --version
./dotnet build /path/to/repro.csproj
```

The repository's own `global.json` is deliberately not rewritten. Invoking `dotnet` from the repository root still requests its pinned SDK, and a repro's own SDK selection rules still apply. Do not relax the repository pin to use the daily bootstrap.

Bootstrap test helpers use the selected SDK's MSBuild entry point and resolver paths rather than guessing the highest installed SDK. This also keeps .NET task hosts launched by .NET Framework tests aligned with the selected core bootstrap.

## Potential Cons
The reliance on downloading the SDK from a remote source requires an internet connection. For the initial build of the repository, this doesn't change as the SDK is always downloaded to the .dotnet folder first. Pinned builds reuse an installed SDK without checking for a newer version.

Daily builds check main's channel and the latest published version on every opted-in build. The SDK archive is downloaded only when that exact version is not already installed. A channel override skips the VMR metadata request, but the installer still resolves the latest daily version. Discovery or installation failures fail the build; they do not silently use the pinned SDK.

Daily SDKs can expose upstream compatibility or package-publication issues. This option does not change the repository's NuGet feeds or build SDK to conceal those failures.

## Pros
This approach simplifies testing MSBuild as part of dotnet by providing a ready and reliable environment without needing to patch anything into a globally installed SDK, as was previously required.