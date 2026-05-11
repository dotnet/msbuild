# MSBuild's reserved and built-in properties

The MSBuild engine itself sets some properties for all projects. There is normal documentation for the [reserved properties and their meanings](https://docs.microsoft.com/visualstudio/msbuild/msbuild-reserved-and-well-known-properties). This document describes the implementation of these properties in MSBuild itself.

There are actually two different implementations of this functionality in MSBuild.

## Built-in properties

When evaluating an individual project, Pass 0 of the evaluation calls [`AddBuiltInProperties()`][addbuiltinproperties] which in turn calls [`SetBuiltInProperty()`][setbuiltinproperty] which sets the property basically as normal.

However, properties set there are not available at all parts of execution, and specifically they're not available when evaluating the `.tasks` file that makes MSBuild's built-in tasks available by default to all projects.

## Reserved properties

Reserved properties are [set by the toolset][toolset_reservedproperties] and are available _only_ in the `.tasks` and `.overridetasks` cases. Properties set there are not available in normal project evaluation.

[addbuiltinproperties]: https://github.com/dotnet/msbuild/blob/24b33188f385cee07804cc63ec805216b3f8b72f/src/Build/Evaluation/Evaluator.cs#L609-L612

[setbuiltinproperty]: https://github.com/dotnet/msbuild/blob/24b33188f385cee07804cc63ec805216b3f8b72f/src/Build/Evaluation/Evaluator.cs#L1257

[toolset_reservedproperties]: https://github.com/dotnet/msbuild/blob/24b33188f385cee07804cc63ec805216b3f8b72f/src/Build/Definition/Toolset.cs#L914-L921
