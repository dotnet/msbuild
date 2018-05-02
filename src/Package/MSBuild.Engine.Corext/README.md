# MSBuild.Engine.Corext

This package is useful only inside Microsoft. It includes the files that MSBuild ships into Visual Studio via its `.vsix` in a different format that is used internally.
Paths are adjusted to match the preexisting CoreXT package MsBuild.Corext.

Files in the MSBuild VSIX which are not in this package:

- msbuild satellite assemblies (no translation needed for internal developer scenarios)
- MSBuild.exe.config (MsBuild.Corext has manual edits to this file)
- *pkgdef (VS specific)
