# Changelog

This document forms the basis of release notes for MSBuild.

MSBuild does **not** use Semantic Versioning.

## [Unreleased]

* Enabled source-server support pointing to MSBuild's GitHub sources (#2107). Thanks, @KirillOsenkov!
* Define `$(VisualStudioVersion)` by default in more situations (fixed #2258).
* Reduced memory allocation and GC pressure in many situations (thanks, @davkean)
* Fixed an error that occurred when logging false import conditions (#2261)

[Unreleased]: https://github.com/Microsoft/msbuild/compare/vs15.3...HEAD
