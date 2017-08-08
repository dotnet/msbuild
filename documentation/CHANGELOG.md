# Changelog

This document forms the basis of release notes for MSBuild.

MSBuild does **not** use Semantic Versioning.

## [Unreleased]

### Added
* Enabled source-server support pointing to MSBuild's GitHub sources (#2107). Thanks, @KirillOsenkov!
* (Windows-only) When the `Copy` task fails to write to a file because it is in use, query the Restart Manager and log the process ID and name that holds the lock (#2301). Thanks, @KirillOsenkov!

### Changed
* Newlines are now allowed in property function expressions (#2324).
* Carriage return (`\r`) characters in logged messages are no longer escaped and emitted as literal `\r` to the logger (#2325).
* Use reference assemblies when possible for `Lc` and `WindMDExp` (#2181).

### Fixed
* Define `$(VisualStudioVersion)` by default in more situations (fixed #2258).
* Reduced memory allocation and GC pressure in many situations. Thanks, @davkean!
* Fixed an error that occurred when logging false import conditions (#2261).
* Fixed an error that could result in copies being erroneously skipped on non-Windows OSes. (#2331).
* Fixed an error that could result in failed builds when using .NET 3.5 tasks on machines with cultures not recognized by the older framework (#2318).
* Improved support of 64-bit MSBuild running outside of Visual Studio installations (#2355).
* Improved diagnosability for malformed TaskFactory implementations (#2363).
* Improved behavior when loading from `amd64/Microsoft.Build.dll` (#2368). Thanks, @emmanuelguerin!

[Unreleased]: https://github.com/Microsoft/msbuild/compare/vs15.3...HEAD
