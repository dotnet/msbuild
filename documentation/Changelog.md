# MSBuild Changelog

## MSBuild 17.0.0

This version of MSBuild shipped with Visual Studio 2022 version 17.0.0 and .NET SDK 6.0.100.

### What's new

* MSBuild now reports its version as `17` and uses Visual Studio 2022 versions of tasks where appropriate.
* MSBuild now targets .NET Framework 4.7.2 and .NET 6.0.
* 64-bit MSBuild is now used for builds from Visual Studio.
* Binary logs are smaller and have more information.
* `MSBuildCopyContentTransitively` is now on by default, ensuring consistency in output folders on incremental builds.
* The method `GetType()` can no longer be called in property functions.

### Detailed release notes

#### Added

* Intrinsic tasks now log their location (#6397). Thanks, @KirillOsenkov!
* `TargetSkippedEventArgs` now has `TargetSkipReason` and `OriginalBuildEventContext` (#6402, #6577). Thanks, @KirillOsenkov!
* `TaskStarted` events now log line and column (#6399). Thanks, @KirillOsenkov!
* ETW trace events for PerformDependencyAnalysis (#6658), WriteLinesToFile (#6670), CopyUpToDate (#6661).
* If the environment variable `MSBuildDebugEngine` is set, MSBuild will create binary logs for all operations to `MSBUILDDEBUGPATH` regardless of how it is called (#6639, #6792).
* `ProjectReference`s can now negotiate `Platform` (#6655, #6724, #6889).
* Tasks can now call `TaskLoggingHelper.LogsMessagesOfImportance` to determine if any attached logger would preserve a log message before constructing it (to save time in the not-being-logged case) (#6381, #6737).
* Support referencing assemblies with generic attributes (#6735). Thanks, @davidwrighton!
* XSD-based MSBuild IntelliSense now supports `ImplicitUsings` and `Using` items (#6755), `InternalsVisibleTo` (#6778), Windows Forms properties (#6860), `DebugType` (#6849), and `SatelliteResourceLanguages` (#6861). Thanks, @pranavkm, @DamianEdwards, @RussKie, and @drewnoakes!
* Tasks can now call `TaskLoggingHelper.IsTaskInputLoggingEnabled` and avoid redundant logging of inputs (#6803).
* Support extracting resource namespace from C# source that uses file-scoped namespaces (#6881).

#### Changed

* The on-disk format of serialized caches has changed (#6350, #6324, #6490, #6674).
* MSBuild is now [signed with a new certificate](https://github.com/dotnet/announcements/issues/184) (#6448).
* `BuildParameters.DisableInprocNode` now applies to more processes (#6400).
* `VCTargetsPath` now defaults to `v170` (#6550).
* MSBuild no longer logs `Building with tools version "Current"` (#6627). Thanks, @KirillOsenkov!
* Text loggers now log properties and items at the end of evaluation (#6535).
* `MSBuildCopyContentTransitively` is now on by default, ensuring consistency in output folders on incremental builds (#6622, #6703).
* MSBuild on .NET 6 has improved task-assembly-reference fallback behavior (#6558).
* MSBuild features gated on the 16.8 changewave are now nonconfigurable (#6634).
* The deprecated import of `$(CoreCrossTargetingTargetsPath)` was removed (#6668). Thanks, @Nirmal4G!
* Improved error message for `MSB4213` (#6640).
* The method `GetType()` can no longer be called in property functions (#6769).
* MSBuild is now fully NGENed by Visual Studio setup (#6764).
* MSBuild (and Visual Studio) now reference `System.Text.Json` 5.0.2 (#6784). Thanks, @JakeRadMSFT!
* Default to SHA2 digest for ClickOnce manifest when certificate signing algorithm is sha256/384/512 (#6882).

#### Fixed

* Solution builds should work when using the secret environment variable `MSBUILDNOINPROCNODE` (#6385).
* Solution extensions can now use `BeforeTargets="ValidateSolutionConfiguration"` (#6454).
* Performance improvements (#6529, #6556, #6598, #6632, #6669, #6671, #6666, #6678, #6680, #6705, #6595, #6716, #6786, #6816, #6832, #6845).
* Single-file ClickOnce publish includes file association icons (#6578).
* Improved robustness in error handling of libraries without resources (#6546).
* Fixed missing information in `Project`'s `DebuggerDisplay` (#6650).
* `ResolveAssemblyReferences` output paths are now output in normalized form (#6533).
* Improved handling of satellite assemblies in ClickOnce (#6665).
* Roslyn code analyzers are no longer run during XAML precompilation (#6676). Thanks, @jlaanstra!
* 64-bit API callers no longer need to set `MSBUILD_EXE_PATH` (#6683, #6746).
* `EvaluateStop` ETW events are now automatically correlated with `EvaluateStart` (#6725).
* Evaluation time is included in text performance traces (#6725).
* Add PackageDescription to `Microsoft.NET.StringTools` (#6740).
* Fixed deadlock between `ExecuteSubmission` and `LoggingService` (#6717).
* Narrowed conditions where MSBuild would blame NuGet for SDK resolution problems (#6742).
* `CombineTargetFrameworkInfoProperties` no longer fails on portable framework names (#6699).
* Avoid needless builds of `GenerateBindingRedirects` (#6726).
* The solution configuration is now passed to experimental cache plugins (#6738).
* Clearer errors when SDK resolvers throw exceptions (#6763).
* Improved errors from `InternableString.ExpensiveConvertToString` (#6798).
* Binding redirects for all `System.*` assemblies updated (#6830).
* Fixed deadlock between `BuildManager` and `LoggingService` (#6837).
* Log message arguments for warnings and errors (#6804). Thanks, @KirillOsenkov!
* Use static CoreClrAssemblyLoader for SDK resolvers (#6864). Thanks, @marcin-krystianc!
* Avoid break caused by fix and workaround for AL path colliding (#6884).
* Support private-use area Unicode characters in paths passed to `XslTransformation` (#6863, #6946). Thanks, @lanfeust69!
* Use the correct .NET host when called from a .NET 6.0 application (#6890).

#### Infrastructure

* This repo now builds with Arcade 6.0 (#6143).
* Use newer Ubuntu versions for Linux CI builds (#6488).
* MSBuild now uses [Arcade-powered source build](https://github.com/dotnet/source-build/tree/ba0b33e9f96354b8d07317c3cdf406ce666921f8/Documentation/planning/arcade-powered-source-build) (#6387).
* Improved repo issue templates and automation (#6557).
* Whitespace cleanup (#6565).
* This repo no longer needs to double-specify the SDK version (#6596).
* Simplify references to `TargetFramework` using new intrinsics (#5799).
* Reference the `Microsoft.DotNet.XUnitExtensions` package from Arcade instead of our fork (#6638).
* Use [`BannedApiAnalyzers`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers/) (#6675).
* Enable analyzers for the MSBuild repo with rules similar to `dotnet/runtime` (#5656). Thanks, @elachlan!
* Improved internal OptProf training scenarios (#6758).
* Delete Unreachable code (#6805). Thanks, @KirillOsenkov!
* Upgrade System.Net.Http package version used in tests (#6879).

#### Documentation

* Use GitHub-generated Markdown tables of contents (#6760).
* Fixed validation issues in docs build (#6744).
* Descriptions of labels in use in this repo (#6873).

## MSBuild 16.11.0

This version of MSBuild shipped with Visual Studio 2019 version 16.11.0 and .NET SDK 5.0.400.

### What's new

* MSBuild now supports long paths in the 64-bit `amd64\MSBuild.exe` executable.
* New version properties `MSBuildFileVersion` (4-part, matches file version) and `MSBuildSemanticVersion` (matches package versions) are now available for use (#6534).

### Detailed release notes

#### Added

* Additional properties documented and available for completion in Visual Studio (#6500, #6530).
* The `SignFile` task is now available in MSBuild on .NET 5.0 (#6509). Thanks, @Zastai!
* New version properties `MSBuildFileVersion` (4-part, matches file version) and `MSBuildSemanticVersion` (matches package versions) are now available for use (#6534).
#### Changed

* When using the experimental cache API, schedule proxy builds to the in-proc node for performance (#6386).
* Experimental cache queries are now executed in parallel (#6468).
* The ETW events generated in `ResolveAssemblyReference` now include an approximation of the "size" of the RAR request (#6410).

#### Fixed

* Fixed memory leak in `ProjectRootElement.Reload` (#6457).
* Added locking to avoid race conditions in `BuildManager` (#6412).
* Allow `ResolveAssemblyReferences` precomputed cache files to be in read-only locations (#6393).
* 64-bit `al.exe` is used when targeting 64-bit architectures (for real this time) (#6484).
* Builds with `ProduceOnlyReferenceAssembly` no longer expect debug symbols to be produced (#6511). Thanks, @Zastai!
* 64-bit `MSBuild.exe` supports long paths (and other .NET default behaviors) (#6562).
* Non-graph builds no longer crash in the experimental project cache (#6568).
* The experimental project cache is initialized only once (#6569).
* The experimental project cache no longer tries to schedule proxy builds to the in-proc node (#6635).

#### Infrastructure

* Use a packaged C# compiler to avoid changes in reference assembly generation caused by compiler changes (#6431).
* Use more resilient test-result upload patterns (#6489).
* Conditional compilation for .NET Core within our repo now includes new .NET 5.0+ runtimes (#6538).
* Switched to OneLocBuild for localization PRs (#6561).
* Moved to latest Ubuntu image for PR test legs (#6573).

## MSBuild 16.10.2

This version of MSBuild shipped with Visual Studio 2019 version 16.10.2 and will ship with .NET SDK 5.0.302.

#### Fixed

* Fixed a regression in the `MakeRelative` property function that dropped trailing slashes (#6513). Thanks, @dsparkplug and @pmisik!
* Fixed a regression in glob matching where files without extensions were erroneously not matched (#6531).
* Fixed a change in logging that caused crashes in Azure DevOps loggers (#6520).

## MSBuild 16.10.2

This version of MSBuild shipped with Visual Studio 2019 version 16.10.2 and will ship with .NET SDK 5.0.302.

#### Fixed

* Fixed a regression in the `MakeRelative` property function that dropped trailing slashes (#6513). Thanks, @dsparkplug and @pmisik!
* Fixed a regression in glob matching where files without extensions were erroneously not matched (#6531).
* Fixed a change in logging that caused crashes in Azure DevOps loggers (#6520).

## MSBuild 16.10.1

This version of MSBuild shipped with Visual Studio 2019 version 16.10.1 and .NET SDK 5.0.301.

#### Fixed

* Restore support for building individual project(s) within solutions by specifying `-t:Project` (#6465).

## MSBuild 16.9.2

This version of MSBuild shipped with Visual Studio 2019 version 16.9.7.

#### Fixed

* Fixed MSB0001 error when building large solutions (#6437).

## MSBuild 16.10.0

This version of MSBuild shipped with Visual Studio 2019 version 16.10.0 and .NET SDK 5.0.300.

### What's new

* MSBuild now targets .NET 5.0 and .NET Framework 4.7.2.
* MSBuild is faster and uses less memory.
* Binary logs are smaller and have less performance overhead.
* Tasks can now opt into resource management to improve parallelism in large builds.
* It's now possible to optionally embed arbitrary files in a binary log.

### Detailed release notes

#### Added

* Projects can now specify `AdditionalTargetFrameworkInfoProperty` items to indicate that referencing projects should get those properties exposed as `AdditionalPropertiesFromProject` metadata on resolved reference items. (#5994).
* The `Unzip` task now accepts `Include` and `Exclude` arguments to filter what is extracted from the zip file (#6018). Thanks, @IvanLieckens!
* The `-graph:noBuild` command line argument can be used to validate that a graph is buildable without actually building it (#6016).
* `TaskParameterEventArgs` allow logging task parameters and values in a compact, structured way (#6155). Thanks, @KirillOsenkov!
* ClickOnce publish now supports Ready To Run (#6244).
* .NET 5.0 applications may now specify a toolset configuration file (#6220).
* `ResolveAssemblyReferences` can now consume information about assemblies distributed as part of the SDK (#6017).
* Allow constructing a `ProjectInstance` from a `ProjectLink` (#6262).
* Introduce cross-process resource management for tasks (#5859).
* `ProjectEvaluationFinished` now has fields for properties and items (#6287). Thanks, @KirillOsenkov!
* `WriteCodeFragment` can now write assembly attributes of specified types, and infers some common types (#6285). Thanks, @reduckted!
* The `-detailedSummary` option now accepts a boolean argument, preventing dumping details to the console logger when building with `-bl -ds:false` (#6338). Thanks, @KirillOsenkov!
* Binary logs now include files listed in the item `EmbedInBinlog` as well as MSBuild projects (#6339). Thanks, @KirillOsenkov!
* The `FindInvalidProjectReferences` task is now available in .NET Core/5.0+ scenarios (#6365).

#### Changed

* String deduplication is now much more sophisticated, reducing memory usage (#5663).
* Refactoring and performance improvements in `ResolveAssemblyReferences` (#5929, #6094).
* Binary logs now store strings only once, dramatically reducing log size (#6017, #6326). Thanks, @KirillOsenkov!
* Refactoring and code cleanup (#6120, #6159, #6158, #6282). Thanks, @Nirmal4G!
* `Span<T>`-based methods are used on .NET Framework MSBuild as well as .NET 5.0 (#6130).
* Improved `MSB4064` error to include information about the loaded task that didn't have the argument (#5945). Thanks, @BartoszKlonowski!
* Performance improvements in inter-node communication (#6023). Thanks, @KirillOsenkov!
* Performance improvements in matching items based on metadata (#6035), property expansion (#6128), glob evaluation (#6151), enumerating files (#6227).
* When evaluated with `IgnoreInvalidImports`, _empty_ imports are also allowed (#6222).
* `Log.HasLoggedError` now respects `MSBuildWarningsAsErrors` (#6174).
* `TargetPath` metadata is now respected on items that copy to output directories, and takes precedence over `Link` (#6237).
* The `Restore` operation now fails when SDKs are unresolvable (#6312).
* `MSBuild.exe.config` now has explicit binding redirects for all assemblies in the MSBuild VSIX (#6334).

#### Fixed

* Inconsistencies between `XamlPreCompile` and the `CoreCompile` C## compiler invocation (#6093). Thanks, @huoyaoyuan!
* Wait for child nodes to exit before exiting the entry-point node in VSTest scenarios (#6053). Thanks, @tmds!
* Fix bad plugin EndBuild exception handling during graph builds (#6110).
* Allow specifying `UseUtf8Encoding` in `ToolTask`s (#6188).
* Failures on big-endian systems (#6204). Thanks, @uweigand!
* 64-bit `al.exe` is used when targeting 64-bit architectures (#6207).
* Improved error messages when encountering a `BadImageReferenceException` in `ResolveAssemblyReferences` (#6240, #6270). Thanks, @FiniteReality!
* Escape special characters in `Exec`’s generated batch files, allowing builds as users with some special characters in their Windows username (#6233).
* Permit comments and trailing commas in solution filter files (#6346).
* Exceptions thrown from experimental cache plugins are now handled and logged better (#6345, #6368).
* Source generators with configuration files can now be used in XamlPreCompile (#6438).
* Large builds no longer crash with an exception in `LogProjectStarted` (#6437).

#### Infrastructure

* Update to Arcade 5.0 and .NET 5.0 (#5836).
* The primary development branch is now named `main`.
* Test robustness improvements (#6055, #6336, #6337, #6332). Thanks, @tmds and @KirillOsenkov!
* Remove unnecessary NuGet package references (#6036). Thanks, @teo-tsirpanis!
* Correctly mark .NET Framework 3.5 reference assembly package dependency as private (#6214).
* Our own builds opt into text-based performance logging (#6274).
* Update to Arcade publishing v3 (#6349).
* Use OneLocBuild localization process (#6378).

#### Documentation

* Updates to static graph documentation (#6043).
* Short doc on the threading model (#6042).
* Update help text to indicate that `--` is a valid argument prefix (#6205). Thanks, @BartoszKlonowski!
* API documentation improvements (#6246, #6284).
* Details about interactions with the Global Assembly Cache (#6173).

## MSBuild 16.9.0.2116703

⚠ This release should have been versioned `16.9.1` but was erroneously released as 16.9.0.

This version of MSBuild shipped with Visual Studio 2019 version 16.9.3.

#### Fixed

* Restore support for building solutions with web site projects (#6238).

## MSBuild 16.9.0

This version of MSBuild shipped with Visual Studio 2019 version 16.9.0 and .NET SDK 5.0.200.

### What's new

* `MSB3277` warnings now include information about the assembly identities involved, instead of saying to rerun under higher verbosity.
* It's now possible to opt out of culture-name detection of `EmbeddedResource`s, for instance to have a resource named `a.cs.template`.
* Common targets now support `$(BaseOutputPath)`, with the default value `bin`.
* Item `Update`s are no longer case-sensitive, fixing a regression in MSBuild 16.6 (#5888).
* `ParentBuildEventContext` now includes a parent `MSBuild` task if relevant, enabling proper nesting in GUI viewers.
* Builds that fail because a warning was elevated to an error now report overall failure in the `MSBuild.exe` exit code.

### Detailed release notes

#### Added

* The `MSB4006` error has been enhanced to describe the cycle when possible (#5711). Thanks, @haiyuzhu!.
* More information is logged under `MSBUILDDEBUGCOMM` (#5759).
* The command line parser now accepts arguments with double hyphens (`--argument`) as well as single hyphens (`-argument`) and forward slashes (`/argument`) (#5786). Thanks, @BartoszKlonowski!
* MSBuild now participates in the .NET CLI text performance log system on an opt-in basis (#5861).
* Common targets now support `$(BaseOutputPath)`, with the default value `bin` (#5238). Thanks, @Nirmal4G!
* `Microsoft.Build.Exceptions.CircularDependencyException` is now public (#5988). Thanks, @tflynt91!
* `EvaluationId` is now preserved in the `ProjectStarted` event, allowing disambiguating related project start events (#5997). Thanks, @KirillOsenkov!
* The `ResolveAssemblyReference` task can now optionally emit items describing unresolved assembly conflicts (#5990).
* Experimental `ProjectCache` API to enable higher-order build systems (#5936).

#### Changed

* Warnings suppressed via `$(NoWarn)` (which formerly applied only to targets that opted in like the C## compiler) are now treated as `$(MSBuildWarningsAsMessages)` (#5671).
* Warnings elevated via `$(WarningsAsErrors )` (which formerly applied only to targets that opted in like the C## compiler) are now treated as `$(MSBuildWarningsAsErrors)` (#5774).
* Improved error message when using an old .NET (Core) SDK and targeting .NET 5.0 (#5826).
* Trailing spaces in property expressions inside conditionals now emit an error instead of silently expanding to the empty string (#5672, #5868). Thanks, @mfkl!
* `MSB3277` warnings now include information about the assembly identities involved, instead of saying to rerun under higher verbosity (#5798).
* `MSB5009` errors now indicate the project in the solution that is causing the nesting error (#5835). Thanks, @BartoszKlonowski!
* Avoid spawning a process to determine processor architecture (#5897). Thanks, @tmds!
* It's now possible to opt out of culture-name detection of `EmbeddedResource`s, for instance to have a resource named `a.cs.template` (#5824).
* `ProjectInSolution.AbsolutePath` returns a normalized full path when possible (#5949).
* Evaluation pass-stop events now include information about the "size" (number of properties/items/imports) of the project (#5978). Thanks, @arkalyanms!

#### Fixed

* `AllowFailureWithoutError` now does what it said it would do (#5743).
* The solution parser now no longer skips projects that are missing an EndProject line (#5808). Thanks, @BartoszKlonowski!
* `ProjectReference`s to `.vcxproj` projects from multi-targeted .NET projects no longer overbuild (#5838).
* Removed unused `InternalsVisibleTo` to obsolete test assemblies (#5914). Thanks, @SingleAccretion!
* Respect conditions when removing all items from an existing list at evaluation time (#5927).
* Common targets should no longer break if the environment variable `OS` is set (#5916).
* Some internal errors will now be reported as errors instead of hanging the build (#5917).
* Item `Update`s are no longer case-sensitive, fixing a regression in MSBuild 16.6 (#5888).
* Use lazy string formatting in more places (#5924).
* Redundant references to MSBuild assemblies no longer fail in 64 MSBuild inline tasks (#5975).
* The `Exec` task will now no longer emit the expanded `Command` to the log on failure (#5962). Thanks, @tmds!
* Tasks generated with `RoslynCodeTaskFactory` now no longer rebuild for every use, even with identical inputs (#5988). Thanks, @KirillOsenkov!
* `ParentBuildEventContext` now includes a parent `MSBuild` task if relevant (#5966). Thanks, @KirillOsenkov!
* Builds that fail because a warning was elevated to an error now report overall failure in the `MSBuild.exe` exit code (#6006).
* Performance of projects with large numbers of consecutive item updates without wildcards improved (#5853).
* Performance improvements in `ResolveAssemblyReferences` (#5973).
* PackageReferences that are marked as development dependencies are removed from the ClickOnce manifest (#6037).
* Stop overfiltering .NET Core assemblies from the ClickOnce manifest (#6080).

#### Infrastructure

* The MSBuild codebase now warns for unused `using` statements (#5761).
* The MSBuild codebase is now indexed for [Rich Code Navigation](https://visualstudio.microsoft.com/services/rich-code-navigation/) on CI build (#5790). Thanks, @jepetty!
* The 64-bit bootstrap directory is more usable (#5825).
* Test robustness improvements (#5827, #5944, #5995).
* Make non-shipping NuGet packages compliant (#5823).
* Use [Darc](https://github.com/dotnet/arcade/blob/main/Documentation/Darc.md) to keep bootstrap dependencies up to date (#5909).
* Replace MSBuild.Dev.sln and MSBuild.SourceBuild.sln with solution filters (#6010).
* Minimize and update NuGet feeds (#6019, #6136).

#### Documentation

* Improvements to MSBuild-internal Change Wave docs (#5770, #5851).
* High-level documentation for static graph functionality added (#5741).
* Instructions on testing private bits (#5818, #5831).
* XML doc comments updated to match public-ready API docs pages (#6028). Thanks, @ghogen!
