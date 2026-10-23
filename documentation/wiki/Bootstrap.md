# Bootstrap MSBuild

The bootstrap combines freshly built MSBuild with a complete SDK or Visual Studio-style toolset.
It provides the compilers, SDK targets, reference packs, and runtime needed to build real projects.
This lets MSBuild rebuild itself and run tests against its supported SDK baseline while engineering
tooling can use a newer SDK.

## Toolchain configuration

| Setting | Controls |
| --- | --- |
| `global.json`: `tools.dotnet` | Engineering SDK provisioned by Arcade. Core stage1 builds launch this SDK's `MSBuild.dll` directly. |
| `global.json`: `sdk.version` | SDK selected by ordinary `dotnet` commands and Framework-hosted SDK resolution. It is aligned with the bootstrap SDK. |
| `global.json`: `msbuild-sdks` | Arcade SDK package used to orchestrate both build stages. |
| `eng/Versions.props`: `BootstrapSdkVersion` | .NET SDK used as the base of the Core bootstrap. |
| `Directory.Build.props`: `LatestDotNetCoreForMSBuild` | Core product and test target framework, independent of the engineering SDK version. Source-only builds use `NetCurrent`. |

The configure-toolset hooks install the engineering and source SDKs side by side. Framework stage1
builds use installed Visual Studio MSBuild with the source SDK's targets; Core stage1 builds use the
engineering SDK's engine and targets.

## Build and test flow

For a two-stage build (`-stage2` on Windows, `--stage2` on Unix):

1. **Stage1 builds MSBuild and assembles the bootstrap.** Core bootstrap construction downloads the
   configured SDK and overlays the newly built MSBuild binaries, tasks, and targets. The SDK's remaining
   toolchain stays in place, and the copied runtime configuration is aligned with its bundled runtime.
   The Framework bootstrap assembles a Visual Studio-style layout with the new Framework binaries.
2. **Stage2 rebuilds the same source using stage1's MSBuild.** Stage1 outputs are preserved under
   `stage1`, and stage2 writes fresh outputs under `artifacts`. Core uses stage1's bootstrapped SDK;
   Framework uses stage1's Framework engine and SDK targets from stage1's Core bootstrap.
3. **Tests exercise stage2 outputs.** Unit tests load stage2 assemblies, and bootstrap integration tests
   launch the bootstrap assembled from stage2 binaries. `TestDotNetRoot` selects stage1's bootstrap
   runtime for Core test hosts; Framework tests run on .NET Framework.

Without the stage2 switch, the build and tests use the single set of outputs under `artifacts`.
Arcade orchestrates both modes.

## Using and adjusting the bootstrap

Bootstrap construction is implemented in `eng/BootStrapMsBuild.targets`. The build wrappers in
`eng/build.ps1` and `eng/build.sh` select the stage2 engine and test runtime.

The runnable layouts are under `artifacts\bin\bootstrap\core` and
`artifacts\bin\bootstrap\net472\MSBuild\Current\Bin`. A complete Framework bootstrap requires a
Visual Studio MSBuild-hosted build. For example, to build a sample with the Core bootstrap:

```powershell
artifacts\bin\bootstrap\core\dotnet.exe build src\Samples\Dependency\Dependency.csproj
```

To update engineering tooling, adjust `tools.dotnet` and the Arcade SDK package version. To change
the bootstrap baseline, keep `sdk.version` and `BootstrapSdkVersion` aligned. The bootstrap SDK must
support the product target framework and runtime dependencies of the MSBuild binaries copied into it;
changing the product target framework is a separate decision.