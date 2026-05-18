# MSBuild's reserved and built-in properties

The MSBuild engine itself sets some properties for all projects. There is normal documentation for the [reserved properties and their meanings](https://docs.microsoft.com/visualstudio/msbuild/msbuild-reserved-and-well-known-properties). This document describes the implementation of these properties in MSBuild itself.

There are actually two different implementations of this functionality in MSBuild.

## Built-in properties

When evaluating an individual project, Pass 0 of the evaluation calls [`AddBuiltInProperties()`][addbuiltinproperties] which in turn calls [`SetBuiltInProperty()`][setbuiltinproperty] which sets the property basically as normal.

However, properties set there are not available at all parts of execution, and specifically they're not available when evaluating the `.tasks` file that makes MSBuild's built-in tasks available by default to all projects.

## Reserved properties

Reserved properties are [set by the toolset][toolset_reservedproperties] and are available _only_ in the `.tasks` and `.overridetasks` cases. Properties set there are not available in normal project evaluation.

## Synthesized import items

When the property `MSBuildProvideImportedProjects` is set to `true`, the engine synthesizes `MSBuildImportedProject` items during `ProjectInstance` construction. Each item represents a file imported during evaluation, with:

- **Identity** — the full path of the imported file.
- **`ImportingProjectPath`** metadata — the full path of the file containing the `<Import>` element.
- **`Sdk`** metadata — the SDK name if the import was resolved via an SDK reference (e.g. `Microsoft.NET.Sdk`); empty otherwise.

The property can be set in the project file or passed as a global property (e.g. `/p:MSBuildProvideImportedProjects=true`). The items are regular MSBuild items, so they serialize to out-of-proc worker nodes and are available to any target or task. Projects that don't set the property pay zero cost.

Each imported file appears at most once (first occurrence in depth-first evaluation order), so the collection forms a tree. The root project itself is excluded — only actual import relationships are represented.

Implementation: items are added in [`ProjectInstance.CreateImportsSnapshot()`][createimportssnapshot] from the evaluated import closure.

[createimportssnapshot]: https://github.com/dotnet/msbuild/blob/main/src/Build/Instance/ProjectInstance.cs

[addbuiltinproperties]: https://github.com/dotnet/msbuild/blob/24b33188f385cee07804cc63ec805216b3f8b72f/src/Build/Evaluation/Evaluator.cs#L609-L612

[setbuiltinproperty]: https://github.com/dotnet/msbuild/blob/24b33188f385cee07804cc63ec805216b3f8b72f/src/Build/Evaluation/Evaluator.cs#L1257

[toolset_reservedproperties]: https://github.com/dotnet/msbuild/blob/24b33188f385cee07804cc63ec805216b3f8b72f/src/Build/Definition/Toolset.cs#L914-L921
