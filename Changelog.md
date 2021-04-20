# MSBuild 16.10.0

This version of MSBuild will ship with Visual Studio 2019 version 16.10.0 and .NET SDK 5.0.300.

## What's new

* MSBuild now targets .NET 5.0 and .NET Framework 4.7.2.
* MSBuild is faster and uses less memory.
* Binary logs are smaller and have less performance overhead.
* Tasks can now opt into resource management to improve parallelism in large builds.

## Detailed release notes

### Added

* Projects can now specify `AdditionalTargetFrameworkInfoProperty` items to indicate that referencing projects should get those properties exposed as `AdditionalPropertiesFromProject` metadata on resolved reference items. (#5994).
* The `Unzip` task now accepts `Include` and `Exclude` arguments to filter what is extracted from the zip file (#6018). Thanks, @IvanLieckens!
* The `-graph:noBuild` command line argument can be used to validate that a graph is buildable without actually building it (#6016).
* When building a solution filter, `$(SolutionFilterName)` is now defined (#6171).
* `TaskParameterEventArgs` allow logging task parameters and values in a compact, structured way (#6155). Thanks, @KirillOsenkov!
* ClickOnce publish now supports Ready To Run (#6244).
* .NET 5.0 applications may now specify a toolset configuration file (#6220).
* `ResolveAssemblyReferences` can now consume information about assemblies distributed as part of the SDK (#6017).
* Allow constructing a `ProjectInstance` from a `ProjectLink` (#6262).
* Introduce cross-process resource management for tasks (#5859).
* `ProjectEvaluationFinished` now has fields for properties and items (#6287). Thanks, @KirillOsenkov!
* `WriteCodeFragment` can now write assembly attributes of specified types, and infers some common types (#6285). Thanks, @reduckted!
* The `-detailedSummary` option now accepts a boolean argument, preventing dumping details to the console logger when building with `-bl -ds:false` (#6338). Thanks, @KirillOsenkov!

### Changed

* String deduplication is now much more sophisticated, reducing memory usage (#5663).
* Improved memory usage and JIT time on MSBuild on .NET 5.0 and higher (#6126, #6189).
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
* The `Restore` operation now fails when SDKs are unresolvable or no `Restore` target exists (#6312).

### Fixed

* Inconsistencies between `XamlPreCompile` and the `CoreCompile` C# compiler invocation (#6093). Thanks, @huoyaoyuan!
* Wait for child nodes to exit before exiting the entry-point node in VSTest scenarios (#6053). Thanks, @tmds!
* Fix bad plugin EndBuild exception handling during graph builds (#6110).
* Allow specifying `UseUtf8Encoding` in `ToolTask`s (#6188).
* Failures on big-endian systems (#6204). Thanks, @uweigand!
* 64-bit `al.exe` is used when targeting 64-bit architectures (#6207).
* Improved error messages when encountering a `BadImageReferenceException` in `ResolveAssemblyReferences` (#6240, #6270). Thanks, @FiniteReality!
* Escape special characters in `Exec`’s generated batch files, allowing builds as users with some special characters in their Windows username (#6233).
* Permit comments and trailing commas in solution filter files (#6346).

### Infrastructure

* Update to Arcade 5.0 and .NET 5.0 (#5836).
* The primary development branch is now named `main`.
* Test robustness improvements (#6055). Thanks, @tmds!
* Remove unnecessary NuGet package references (#6036). Thanks, @teo-tsirpanis!
* Correctly mark .NET Framework 3.5 reference assembly package dependency as private (#6214).
* Our own builds opt into text-based performance logging (#6274).

### Documentation

* Updates to static graph documentation (#6043).
* Short doc on the threading model (#6042).
* Update help text to indicate that `--` is a valid argument prefix (#6205). Thanks, @BartoszKlonowski!
* API documentation improvements (#6246, #6284).
* Details about interactions with the Global Assembly Cache (#6173).
