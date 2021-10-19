# MSBuild Changelog

## MSBuild 17.0.0

This version of MSBuild shipped with Visual Studio 2022 version 17.0.0 and .NET SDK 6.0.100.

### What's new

* MSBuild now reports its version as `17` and uses Visual Studio 2022 versions of tasks where appropriate.
* MSBuild now targets .NET Framework 4.7.2 and .NET 6.0.
* 64-bit MSBuild is now used for builds from Visual Studio.
* Binary logs are smaller and have more information.
* `MSBuildCopyContentTransitively` is now on by default, ensuring consistency in output folders on incremental builds.

### Detailed release notes

#### Added

* Intrinsic tasks now log their location (#6397). Thanks, @KirillOsenkov!
* `TargetSkippedEventArgs` now has `TargetSkipReason` and `OriginalBuildEventContext` (#6402, #6577). Thanks, @KirillOsenkov!
* `TaskStarted` events now log line and column (#6399). Thanks, @KirillOsenkov!

#### Changed

* The on-disk format of serialized caches has changed (#6350, #6324, #6490).
* MSBuild is now [signed with a new certificate](https://github.com/dotnet/announcements/issues/184) (#6448).
* `BuildParameters.DisableInprocNode` now applies to more processes (#6400).
* `VCTargetsPath` now defaults to `v170` (#6550).
* MSBuild no longer logs `Building with tools version "Current"` (#6627). Thanks, @KirillOsenkov!
* Text loggers now log properties and items at the end of evaluation (#6535).
* `MSBuildCopyContentTransitively` is now on by default, ensuring consistency in output folders on incremental builds (#6622).
* MSBuild on .NET 6 has improved task-assembly-reference fallback behavior (#6558).
* MSBuild features gated on the 16.8 changewave are now nonconfigurable (#6634).
* The deprecated import of `$(CoreCrossTargetingTargetsPath)` was removed (#6668). Thanks, @Nirmal4G!

#### Fixed

* Solution builds should work when using the secret environment variable `MSBUILDNOINPROCNODE` (#6385).
* Solution extensions can now use `BeforeTargets="ValidateSolutionConfiguration"` (#6454).
* Performance improvements (#6529, #6556, #6598, #6632, #6669, #6671, #6666, #6678, )
* Single-file ClickOnce publish includes file association icons (#6578).
* Improved robustness in error handling of libraries without resources (#6546).
* Fixed missing information in `Project`'s `DebuggerDisplay` (#6650).
* `ResolveAssemblyReferences` output paths are now output in normalized form (#6533).
* Improved handling of satellite assemblies in ClickOnce (#6665).

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
*

#### Documentation



#### Uncategorized

|sha | Author | subject | parents|
| --- | --- | --- | --- |
eac68aa8b | Johan Laanstra <jlaanstra1221@outlook.com> | Do not run analyzers for XamlPreCompile. (#6676) | 18ca1779d
6dba77a45 | Rainer Sigwald <raines@microsoft.com> | Move ref assembly to the obj folder (#6560) | eac68aa8b
9e576281e | Ladi Prosek <laprosek@microsoft.com> | Absolutize ref assembly path (#6695) | 6dba77a45
ef21d4144 | Forgind <Forgind@users.noreply.github.com> | Move version check earlier (#6674) | 9e576281e
65e44d936 | AR-May <67507805+AR-May@users.noreply.github.com> | Fix lock contention in ProjectRootElementCache.Get (#6680) | ef21d4144
80dae434a | Forgind <Forgind@users.noreply.github.com> | Add ETW trace for PerformDependencyAnalysis (#6658) | 65e44d936
cdb5077c4 | Mihai Codoban <micodoba@microsoft.com> | Improve debugging experience: add global switch MSBuildDebugEngine; Inject binary logger from BuildManager; print static graph as .dot file (#6639) | 80dae434a
b6d179cb9 | Roman Konecny <rokonecn@microsoft.com> | Using ArrayPool for buffers in InterningBinaryReader (#6705) | cdb5077c4
48ffc9831 | Roman Konecny <rokonecn@microsoft.com> | Fix deploy script for 64bits and net6 (#6706) | b6d179cb9
02a3a62df | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Skip Updating CopyComplete Marker When Not Necessary (#6698) | 48ffc9831
ab9e65468 | Rainer Sigwald <raines@microsoft.com> | Only look for .dll assemblies on Core | d26cfbe43
f4645e659 | Rainer Sigwald <raines@microsoft.com> | Avoid regex in GetVsRootFromMSBuildAssembly | d150e93ff
8711a30a1 | Rainer Sigwald <raines@microsoft.com> | Identify 64-bit MSBuildToolsPath from 64-bit app | f4645e659
e020ff120 | Rainer Sigwald <raines@microsoft.com> | Treat unit tests as 32-bit | 8711a30a1
9070345c0 | Rainer Sigwald <raines@microsoft.com> | Remove FindOlderVisualStudioEnvironmentByEnvironmentVariable() | e020ff120
255b4d02b | Rainer Sigwald <raines@microsoft.com> | Avoid recomputing path to MSBuild.exe under VS | 9070345c0
9f91131a3 | Ladi Prosek <laprosek@microsoft.com> | Merge pull request #6663 from rainersigwald/no-ni-on-core | 257996173 ab9e65468
d592862ed | Ladi Prosek <laprosek@microsoft.com> | Merge pull request #6683 from rainersigwald/64-bit-environment | 9f91131a3 255b4d02b
4bb26f3a9 | Rainer Sigwald <raines@microsoft.com> | Revert "Absolutize ref assembly path (#6695)" | 02a3a62df
cad7e7b33 | Rainer Sigwald <raines@microsoft.com> | Revert "Move ref assembly to the obj folder (#6560)" | 4bb26f3a9
cf722dbe1 | Marc Paine <marcpop@microsoft.com> | Merge pull request #6718 from rainersigwald/revert-ref-asm-move | 02a3a62df cad7e7b33
9128adb8f | Rainer Sigwald <raines@microsoft.com> | Merge pull request #6720 from dotnet-maestro-bot/merge/vs17.0-to-main | d592862ed cf722dbe1
b6e7d6051 | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | ProjectReferences Negotiate SetPlatform Metadata (#6655) | 9128adb8f
a66a243f7 | Sam Harwell <Sam.Harwell@microsoft.com> | Use default XlfLanguages | b6e7d6051
f1cd160db | Sam Harwell <Sam.Harwell@microsoft.com> | Add reference to Microsoft.CodeAnalysis.Collections (source package) | a66a243f7
c85cd99ad | Sam Harwell <Sam.Harwell@microsoft.com> | Use ImmutableSegmentedList<T> where appropriate | f1cd160db
b7eb19b9a | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Update description about transitive copying | b6e7d6051
aaac00a34 | dotnet-maestro[bot] <42748379+dotnet-maestro[bot]@users.noreply.github.com> | [main] Update dependencies from dotnet/arcade (#6711) | b7eb19b9a
2a85d84f6 | Forgind <Forgind@users.noreply.github.com> | Use SDK precomputed cache | aaac00a34
1b4b5fb96 | Roman Konecny <rokonecn@microsoft.com> | Remove xml declaration from Tools.csproj (#6729) | 2a85d84f6
24eea8eb3 | dotnet-maestro-bot <dotnet-maestro-bot@microsoft.com> | [automated] Merge branch 'vs16.11' => 'main' (#6626) | 1b4b5fb96
49d582fb7 | Ladi Prosek <laprosek@microsoft.com> | Optimize logging by moving message importance checks earlier (#6381) | 24eea8eb3
4f8d57b40 | Ladi Prosek <laprosek@microsoft.com> | Unbreak non-PR CI builds (#6737) | 49d582fb7
682bfcaf3 | Rainer Sigwald <raines@microsoft.com> | Miscellaneous event tweaks (#6725) | 4f8d57b40
8c92d4c19 | Lachlan Ennis <2433737+elachlan@users.noreply.github.com> | implement analyzers from runtime (#5656) | 682bfcaf3
9596593cc | Ladi Prosek <laprosek@microsoft.com> | Add PackageDescription to Microsoft.NET.StringTools (#6740) | 8c92d4c19
df9547e89 | Forgind <Forgind@users.noreply.github.com> | Add up-to-date ETW for WriteLinesToFile (#6670) | 9596593cc
b9424d916 | Mihai Codoban <micodoba@microsoft.com> | Specify project info in affinity missmatch error (#6640) | df9547e89
6bc1e0e22 | Roman Konecny <rokonecn@microsoft.com> | Deadlock at ExecuteSubmission vs LoggingService (#6717) | b9424d916
19b2630d2 | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Fix Misleading NuGet SDK Resolver Error Message #6742 | 6bc1e0e22
6ca861613 | David Wrighton <davidwr@microsoft.com> | Remove depenency on MemberRef Parent of a custom attribute constructor being a TypeReference (#6735) | 19b2630d2
b0bb46ab8 | Rainer Sigwald <raines@microsoft.com> | Recalculate MSBuild path from VS Root (#6746) | 6ca861613
62c6327ac | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | SetPlatform Negotiation: Allow MSBuild `GetTargetFrameworks` call when `SetTargetFramework` already set (#6724) | b0bb46ab8
c24b4e696 | Rainer Sigwald <raines@microsoft.com> | Nix manual XSD updates (#6759) | 62c6327ac
9c14af563 | Pranav K <prkrishn@hotmail.com> | Update XSD to include details about ImplicitUsings and Using items (#6755) | c24b4e696
cb3144483 | Ladi Prosek <laprosek@microsoft.com> | Add .NET Core solution open to OptProf training scenarios (#6758) | 9c14af563
b92bd7092 | Rainer Sigwald <raines@microsoft.com> | Delete manual Markdown ToCs (#6760) | cb3144483
00166ebca | Forgind <Forgind@users.noreply.github.com> | Update schema for combining TargetFramework info to allow for invalid xml names such as including '+' (#6699) | b92bd7092
d01fb229e | Forgind <Forgind@users.noreply.github.com> | Add CopyUpToDate ETW (#6661) | 00166ebca
c88325c78 | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Generate cache file for SuggestedBindingRedirects (#6726) | d01fb229e
ff10b9f15 | Sam Harwell <Sam.Harwell@microsoft.com> | Merge remote-tracking branch 'dotnet/main' into roslyn-collections | c85cd99ad c88325c78
46d8f9b0b | Rainer Sigwald <raines@microsoft.com> | 16.11 release note update (#6586) | c88325c78
be92f497b | Andy Gerlicher <angerlic@microsoft.com> | Block Execution of GetType() in Evaluation | c88325c78
9fbd47fae | Andy Gerlicher <angerlic@microsoft.com> | Avoid using GetType in a unit test | be92f497b
aac64bbab | Forgind <Forgind@users.noreply.github.com> | Merge pull request #6595 from sharwell/roslyn-collections | 46d8f9b0b ff10b9f15
6806583ea | Ladi Prosek <laprosek@microsoft.com> | Optimize item Remove operations (#6716) | aac64bbab
dcaef41b0 | Forgind <Forgind@users.noreply.github.com> | Merge pull request #6769 from AndyGerlicher/reject-gettype-property | 6806583ea 9fbd47fae
2a7dadfc6 | Mihai Codoban <micodoba@microsoft.com> | Propagate solution configuration information to project cache plugins (#6738) | dcaef41b0
16307632a | Damian Edwards <damian@damianedwards.com> | Add InternalsVisibleTo to common types schema (#6778) | 2a7dadfc6
414393fc1 | Ladi Prosek <laprosek@microsoft.com> | Switch to full NGEN (#6764) | 16307632a
d816e47df | Mihai Codoban <micodoba@microsoft.com> | Only set debug path when MSBuildDebugEngine is set (#6792) | 414393fc1
e65d1aeab | Rainer Sigwald <raines@microsoft.com> | Merge branch 'vs16.11' | d816e47df bba284cf4
bd6797fc8 | dotnet-maestro[bot] <42748379+dotnet-maestro[bot]@users.noreply.github.com> | [main] Update dependencies from nuget/nuget.client (#6651) | e65d1aeab
f6cf11856 | dotnet-maestro[bot] <42748379+dotnet-maestro[bot]@users.noreply.github.com> | [main] Update dependencies from dotnet/roslyn (#6722) | bd6797fc8
596f08dcf | Jake <31937616+JakeRadMSFT@users.noreply.github.com> | Update System.Text.Json to 5.0.2 (#6784) | f6cf11856
864047de1 | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Fail Builds Fast When SDKResolvers Throw Exceptions (#6763) | 596f08dcf
e923c2b80 | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Update loc update branch (#6808) | 864047de1
74e9935a4 | Saint Wesonga <sawesong@microsoft.com> | Define stand-in optional workloads targets (#6813) | d816e47df
8d665eea8 | dotnet bot <dotnet-bot@dotnetfoundation.org> | Localized file check-in by OneLocBuild Task (#6809) | e923c2b80
5e0b0ea21 | Gordon Hogenson <ghogen@microsoft.com> | Doc comments: fix validation issues in docs build (#6744) | 8d665eea8
f9e7e8ed4 | Ladi Prosek <laprosek@microsoft.com> | Add invariant check to InternableString.ExpensiveConvertToString (#6798) | 8c7337fc3
2fab8f47f | dotnet bot <dotnet-bot@dotnetfoundation.org> | Localized file check-in by OneLocBuild Task (#6824) | f9e7e8ed4
11ae61937 | Rainer Sigwald <raines@microsoft.com> | Increase ProjectRootElementCache MRU cache (#6786) | 2fab8f47f
1a1f20e49 | Rainer Sigwald <raines@microsoft.com> | Merge pull request #6815 from vs17.0 | 2c5510013 74e9935a4
c82d55e9b | Rainer Sigwald <raines@microsoft.com> | Merge remote-tracking branch 'upstream/vs16.11' into main | 1a1f20e49 9f91d117e
a9594b978 | Kirill Osenkov <KirillOsenkov@users.noreply.github.com> | Delete dead code (#6805) | c82d55e9b
8f208e609 | Rainer Sigwald <raines@microsoft.com> | Binding redirect Tasks.Extensions 4.2.0.1 (#6830) | a9594b978
e3e141ff0 | Ladi Prosek <laprosek@microsoft.com> | Expose LogTaskInputs to tasks (#6803) | 8f208e609
4ceb3f8e2 | Ladi Prosek <laprosek@microsoft.com> | Optimize InternableString.GetHashCode (#6816) | e3e141ff0
ea1d6d99a | Roman Konecny <rokonecn@microsoft.com> | Process-wide caching of ToolsetConfigurationSection (#6832) | 4ceb3f8e2
d231437ce | Ladi Prosek <laprosek@microsoft.com> | Further optimize InternableString.GetHashCode by eliminating a ref (#6845) | ea1d6d99a
6eb3976d9 | Roman Konecny <rokonecn@microsoft.com> | Fix deadlock in BuildManager vs LoggingService (#6837) | d231437ce
6cf35b8de | dotnet-maestro[bot] <42748379+dotnet-maestro[bot]@users.noreply.github.com> | [main] Update dependencies from dotnet/arcade (#6843) | 6eb3976d9
c5eef1eb2 | Kirill Osenkov <KirillOsenkov@users.noreply.github.com> | Log message arguments for warnings and errors (#6804) | f566ba17a
cb055d28f | dotnet-maestro[bot] <42748379+dotnet-maestro[bot]@users.noreply.github.com> | [main] Update dependencies from dotnet/roslyn (#6865) | c5eef1eb2
57f14a7b1 | Marcin Krystianc <marcin.krystianc@gmail.com> | Use static CoreClrAssemblyLoader for SDK resolvers (#6864) | cb055d28f
9b5ccf07e | Igor Velikorossov <RussKie@users.noreply.github.com> | Add new Windows Forms specific props (#6860) | 57f14a7b1
2f1e9cad5 | Saint Wesonga <sawesong@microsoft.com> | Revert "Define stand-in optional workloads targets (#6813)" (#6872) | 9b5ccf07e
bc68c0d7e | Sujit Nayak <sujitn@exchange.microsoft.com> | 6732: Default to sha2 digest for clickonce manifest when certificate signing algorithm is sha256/384/512 | 2f1e9cad5
8f9d79e07 | Sujit Nayak <sujitn@exchange.microsoft.com> | add comment | bc68c0d7e
d9d1d59cb | Sujit Nayak <sujitn@exchange.microsoft.com> | fix comment | 8f9d79e07
0d31bff6c | AR-May <67507805+AR-May@users.noreply.github.com> | Upgrade System.Net.Http package version (#6879) | 2f1e9cad5
a08f6bda8 | Drew Noakes <git@drewnoakes.com> | Add enumeration values for DebugType in XSD (#6849) | 0d31bff6c
9f83c725f | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Add SatelliteResourceLanguages property to common types schema (#6861) | a08f6bda8
c144bfc46 | sujitnayak <sujitn@microsoft.com> | Merge pull request #6882 from sujitnayak/main | 9f83c725f d9d1d59cb
c8300d6da | Rainer Sigwald <raines@microsoft.com> | Deploy-MSBuild shouldn't deploy en resources (#6888) | c144bfc46
c62750d64 | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Avoid appending x64 to AL path if x64 is already appended (#6884) | c8300d6da
e123a0c1f | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Add labels documentation (#6873) | c62750d64
3a1e456fe | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | SetPlatform: Use Platform Instead Of PlatformTarget (#6889) | e123a0c1f
9881f461f | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Enable File Scoped Namespaces For Resources (#6881) | 3a1e456fe
ceb2a05a0 | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Initialize XmlReaders using StreamReaders (#6863) | 9881f461f
5805e3469 | Ben Villalobos <4691428+BenVillalobos@users.noreply.github.com> | Delete intermediate sourcebuild package (#6898) | ceb2a05a0
bbcce1dff | Forgind <Forgind@users.noreply.github.com> | MSBuildLocator: Find dotnet.exe when out-of-proc (#6890) | 5805e3469
6873d6956 | Rainer Sigwald <raines@microsoft.com> | Merge branch 'vs16.11' into 'vs17.0' | bbcce1dff f32259642
b26f1a2df | Rainer Sigwald <raines@microsoft.com> | C++ CodeAnalysis assemblies to v17 (#6953) | 6873d6956
d66a44095 | Jean-Jacques Lafay <jeanjacques.lafay@gmail.com> | Fix files kept in use in XslTransformation (#6946) | b26f1a2df



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
