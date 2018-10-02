MSBuild is under active development primarily in two different areas: .NET Core MSBuild and Desktop MSBuild.

# .NET Core MSBuild
Our goal is the enable a subset of MSBuild to run cross-platform and build applications that target the .NET Core framework. This version should be delivered via NuGet and have no required dependencies on the machine (no installer, no registry, no GAC). It is not intended to replace the version integrated with Visual Studio nor build projects targeting the full .NET Framework.
 * Fully support Linux, OSX, and Windows with all .NET Core MSBuild features.
 * Eliminate dependency on the full .NET Framework (and mono).
 * Enable features over time to reduce feature gap ([#303](/Microsoft/msbuild/issues/303), [#304](/Microsoft/msbuild/issues/304)).

 * Note that features like toolsets, registry, GAC, Frameworks, etc. that do not apply to building .NET Core applications will remain disabled.*

# Desktop MSBuild
This version of MSBuild is the version we ship with Visual Studio (previously shipped as part of the .NET Framework) and runs on the full .NET Framework. Our goal is to maintain a single code base with a high degree of compatibility and stability between releases. As such, the bar for new features or behavior change should be very high.

## Quality
 * Performance-related fixes to improve the end-to-end experience for developers in Visual Studio. [ongoing]
 * Fix top-hitting issues gathered from feedback and Watson events (crash data). [ongoing]

## Performance
 * Address "low-hanging fruit" to improve build speed, particularly around C++. [post Update 1 time-frame]
 * Improved incremental build with help of fully deterministic Roslyn builds. [post Update 1 time-frame]

## Technical Debt
 * Merge codebases (xplat, master, and internal branches). [RC time-frame]
 * Introduce compiler constants for CoreCLR feature flags.
 * Ship MSBuild for Visual Studio out of GitHub sources. [Visual Studio vNext time-frame]

## Telemetry
 * Gather additional data on usage and issues (hangs in Visual Studio, etc). [tentatively RTM time-frame]

# Branch Strategy
 * `xplat`: Work for cross-platform support, primarily focused on CoreCLR.  In the medium term, we should build both CoreCLR and Desktop MSBuild from the same branch and merge this branch into `master`.
 * `master`: Work for Desktop MSBuild for Visual Studio "15", for now manually mirrored into internal source control.
 * Microsoft Internal: At the moment, the "official" location that produces builds that ship with Visual Studio and its updates.  Changes are manually mirrored from GitHub `master` as we go along.
 * Stabilization: when we're preparing a release, we'll start a branch for that release. Most commits should be pushed to `master` as usual. Last-minute bugfixes can have pull requests targeting the update branch. Any commit to the release branch should be followed immediately by a merge of the release branch to `master`, so that `master` is always up to date. Since `master` is now destined to release with Visual Studio "15", commits intended to be included in an update for Visual Studio 2015 (MSBuild 14.0) should go to `dev14-update`.
 * Mono support: Still TBD. See [#302](/Microsoft/msbuild/issues/302). LKG xplat [`@f9d8cc7`](https://github.com/Microsoft/msbuild/commit/f9d8cc725ca2cd46d7e01015afba0defea95ce37)
