# What are Change Waves?
A Change Wave is a set of risky features developed under the same opt-out flag. The purpose of this is to warn developers of "risky" changes that will become standard functionality down the line. If there's something we think is worth the risk, we found that Change Waves were a good middle ground between making necessary changes and warning customers of what will soon be permanent.

## Why Opt-Out vs. Opt-In?
Opt-out is a better approach for us because we'd likely get limited feedback when a feature impacts customer builds. When a feature does impact a customer negatively, it's a quick switch to disable and allows time to adapt. The key aspect to Change Waves is that it smooths the transition for customers adapting to risky changes that the MSBuild team feels strongly enough to take.

## How do they work?
The opt-out comes in the form of setting the environment variable `MSBUILDDISABLEFEATURESFROMVERSION` to the Change Wave (or version) that contains the feature you want **disabled**. This version happens to be the version of MSBuild that the features were developed for. See the mapping of change waves to features below.
The opt-out should be just a *temporary* workaround for a problem - as the feature will anyways become permanent eventually. For this reason - **please make sure to create or upvote a bug describing the issue making you opt-out**.

## When do they become permanent?
A wave of features is set to "rotate out" (i.e. become standard functionality) two bands after its release. For example, wave 16.8 stayed opt-out through wave 16.10, becoming standard functionality when wave 17.0 is introduced.

## MSBUILDDISABLEFEATURESFROMVERSION Values & Outcomes
| `MSBUILDDISABLEFEATURESFROMVERSION` Value                         | Result        | Receive Warning? |
| :-------------                                                    | :----------   | :----------: |
| Unset                                                             | All Change Waves will be enabled, meaning all features behind each Change Wave will be enabled.               | No   |
| Any valid & current Change Wave (Ex: `16.8`)                      | All features behind Change Wave `16.8` and higher will be disabled.                                           | No   |
| Invalid Value (Ex: `16.9` when valid waves are `16.8` and `16.10`)| Default to the closest valid value (ascending). Ex: Setting `16.9` will default you to `16.10`.               | No   |
| Out of Rotation (Ex: `17.1` when the highest wave is `17.0`)      | Clamp to the closest valid value. Ex: `17.1` clamps to `17.0`, and `16.5` clamps to `16.8`                    | Yes  |
| Invalid Format (Ex: `16x8`, `17_0`, `garbage`)                    | All Change Waves will be enabled, meaning all features behind each Change Wave will be enabled.               | Yes  |

# Change Waves & Associated Features

## Current Rotation of Change Waves

### 17.14
- ~[.SLNX support - use the new parser for .sln and .slnx](https://github.com/dotnet/msbuild/pull/10836)~ reverted after compat problems discovered
- [Support custom culture in RAR](https://github.com/dotnet/msbuild/pull/11000)
- [VS Telemetry](https://github.com/dotnet/msbuild/pull/11255)

### 17.12
- [Log TaskParameterEvent for scalar parameters](https://github.com/dotnet/msbuild/pull/9908)
- [Convert.ToString during a property evaluation uses the InvariantCulture for all types](https://github.com/dotnet/msbuild/pull/9874)
- [Fix oversharing of build results in ResultsCache](https://github.com/dotnet/msbuild/pull/9987)
- [Add ParameterName and PropertyName to TaskParameterEventArgs](https://github.com/dotnet/msbuild/pull/10130)
- [Emit eval props if requested by any sink](https://github.com/dotnet/msbuild/pull/10243)
- [Load Microsoft.DotNet.MSBuildSdkResolver into default load context (MSBuild.exe only)](https://github.com/dotnet/msbuild/pull/10603)

### 17.10
- [AppDomain configuration is serialized without using BinFmt](https://github.com/dotnet/msbuild/pull/9320) - feature can be opted out only if [BinaryFormatter](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter) is allowed at runtime by editing `MSBuild.runtimeconfig.json`. **Please note that [any usage of BinaryFormatter is insecure](https://learn.microsoft.com/dotnet/standard/serialization/binaryformatter-security-guide).**
- [Warning on serialization custom events by default in .NET framework](https://github.com/dotnet/msbuild/pull/9318)
- [Cache SDK resolver data process-wide](https://github.com/dotnet/msbuild/pull/9335)
- [Target parameters will be unquoted](https://github.com/dotnet/msbuild/pull/9452), meaning  the ';' symbol in the parameter target name will always be treated as separator
- [Add Link metadata to Resources in AssignLinkMetadata target](https://github.com/dotnet/msbuild/pull/9464)
- [Change Version switch output to finish with a newline](https://github.com/dotnet/msbuild/pull/9485)
- [Load NuGet.Frameworks into secondary AppDomain (MSBuild.exe only)](https://github.com/dotnet/msbuild/pull/9446)
- [Update Traits when environment has been changed](https://github.com/dotnet/msbuild/pull/9655)
- [Exec task does not trim leading whitespaces for ConsoleOutput](https://github.com/dotnet/msbuild/pull/9722)
- [Introduce [MSBuild]::StableStringHash overloads](https://github.com/dotnet/msbuild/issues/9519)
- [Keep the encoding of standard output & error consistent with the console code page for ToolTask](https://github.com/dotnet/msbuild/pull/9539)

## Change Waves No Longer In Rotation
### 16.8
- [Enable NoWarn](https://github.com/dotnet/msbuild/pull/5671)
- [Truncate Target/Task skipped log messages to 1024 chars](https://github.com/dotnet/msbuild/pull/5553)
- [Don't expand full drive globs with false condition](https://github.com/dotnet/msbuild/pull/5669)

### 16.10
- [Error when a property expansion in a condition has whitespace](https://github.com/dotnet/msbuild/pull/5672)
- [Allow Custom CopyToOutputDirectory Location With TargetPath](https://github.com/dotnet/msbuild/pull/6237)
- [Allow users that have certain special characters in their username to build successfully when using exec](https://github.com/dotnet/msbuild/pull/6223)
- [Fail restore operations when an SDK is unresolveable](https://github.com/dotnet/msbuild/pull/6430)
- [Optimize glob evaluation](https://github.com/dotnet/msbuild/pull/6151)

### 17.0
- [Scheduler should honor BuildParameters.DisableInprocNode](https://github.com/dotnet/msbuild/pull/6400)
- [Don't compile globbing regexes on .NET Framework](https://github.com/dotnet/msbuild/pull/6632)
- [Default to transitively copying content items](https://github.com/dotnet/msbuild/pull/6622)
- [Reference assemblies are now no longer placed in the `bin` directory by default](https://github.com/dotnet/msbuild/pull/6560) (reverted [here](https://github.com/dotnet/msbuild/pull/6718) and brought back [here](https://github.com/dotnet/msbuild/pull/7075))
- [Improve debugging experience: add global switch MSBuildDebugEngine; Inject binary logger from BuildManager; print static graph as .dot file](https://github.com/dotnet/msbuild/pull/6639)
- [Fix deadlock in BuildManager vs LoggingService](https://github.com/dotnet/msbuild/pull/6837)
- [Optimize diag level for file logger and console logger](https://github.com/dotnet/msbuild/pull/7026)
- [Optimized immutable files up to date checks](https://github.com/dotnet/msbuild/pull/6974)
- [Add Microsoft.IO.Redist for directory enumeration](https://github.com/dotnet/msbuild/pull/6771)
- [Process-wide caching of ToolsetConfigurationSection](https://github.com/dotnet/msbuild/pull/6832)
- [Normalize RAR output paths](https://github.com/dotnet/msbuild/pull/6533)

### 17.4

- [Respect deps.json when loading assemblies](https://github.com/dotnet/msbuild/pull/7520)
- [Consider `Platform` as default during Platform Negotiation](https://github.com/dotnet/msbuild/pull/7511)
- [Adding accepted SDK name match pattern to SDK manifests](https://github.com/dotnet/msbuild/pull/7597)
- [Throw warning indicating invalid project types](https://github.com/dotnet/msbuild/pull/7708)
- [MSBuild server](https://github.com/dotnet/msbuild/pull/7634)

### 17.6

- [Parse invalid property under target](https://github.com/dotnet/msbuild/pull/8190)
- [Eliminate project string cache](https://github.com/dotnet/msbuild/pull/7965)
- [Log an error when no provided search path for an import exists](https://github.com/dotnet/msbuild/pull/8095)
- [Log assembly loads](https://github.com/dotnet/msbuild/pull/8316)
- [AnyHaveMetadataValue returns false when passed an empty list](https://github.com/dotnet/msbuild/pull/8603)
- [Log item self-expansion](https://github.com/dotnet/msbuild/pull/8581)

### 17.8

- [[RAR] Don't do I/O on SDK-provided references](https://github.com/dotnet/msbuild/pull/8688)
- [Delete destination file before copy](https://github.com/dotnet/msbuild/pull/8685)
- [Moving from SHA1 to SHA256 for Hash task](https://github.com/dotnet/msbuild/pull/8812)
- [Deprecating custom derived BuildEventArgs](https://github.com/dotnet/msbuild/pull/8917) - feature can be opted out only if [BinaryFormatter](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter) is allowed at runtime by editing `MSBuild.runtimeconfig.json`
