# Evaluation, imports, and restore

## Follow nested imports

An SDK-style project's implicit imports are created at the top and bottom by [ProjectRootElement.GetImplicitImportNodes](../../../../src/Build/Construction/ProjectRootElement.cs). For an SDK using the common .NET target chain, the shape is approximately:

```text
implicit Sdk.props
  ... Microsoft.Common.props
    Directory.Build.props
    ... Common defaults and project-extension props
  ... remaining SDK props
project body and its explicit imports
implicit Sdk.targets
  ... language/Common target imports
    Microsoft.Common.CurrentVersion.targets
    generated project-extension targets
    Directory.Build.targets
  ... remaining SDK target imports
```

This is a navigation model, not a universal import list for every SDK. Read the selected SDK and project imports to establish the real sequence.

[Microsoft.Common.props](../../../../src/Tasks/Microsoft.Common.props) imports `Directory.Build.props` before important defaults such as `BaseIntermediateOutputPath`. Its location is an early extension point, not "after all core defaults."

[Microsoft.Common.targets](../../../../src/Tasks/Microsoft.Common.targets) imports Common CurrentVersion targets, project-extension targets, and `Directory.Build.targets`. The directory targets are nested in this chain; they are not a separate stage preceding every statement in `Sdk.targets`.

The normal directory-file search selects the nearest applicable file unless paths/import controls change it. Ancestor layering requires explicit imports; do not assume every ancestor's `Directory.Build.props` or targets is automatically merged.

## Evaluation order versus precedence

Full evaluation initializes properties, processes property/import declarations, then item definitions/items and using-task/target registration. Some APIs/commands can request a partial evaluation; inspect the entry point when that matters.

XML position alone does not determine whether a property can be replaced. Global properties normally retain precedence over project assignments, with exceptions such as `TreatAsLocalProperty`. See [Evaluator](../../../../src/Build/Evaluation/Evaluator.cs), especially initial-property setup and `EvaluatePropertyElement`.

For an overridable fallback default, an empty-value condition is appropriate:

```xml
<PropertyGroup>
  <OutputType Condition="'$(OutputType)' == ''">Library</OutputType>
</PropertyGroup>
```

That is not a rule to conditionalize every assignment. Computed properties, normalization, and deliberate appends have different contracts. Trace when their inputs become available and where later imports overwrite them.

For a wrong value, establish the global inputs, first/default assignment, reassignment, import condition, and actual consumer. Existing binlogs or import traces can help, but a cached/partial log is not proof of every intermediate value.

## Restore is a distinct evaluation context

Standard NuGet/SDK restore can generate imports needed by the subsequent build. Running `Restore;Build` as targets in the same evaluated project instance does not refresh those imports.

Use the supported restore-then-build path, such as MSBuild's `-restore`, rather than requiring separate process invocations. [XMake.ExecuteRestore](../../../../src/MSBuild/XMake.cs) creates a restore request with a different global-property/evaluation context and then executes the build request through the build manager.

`dotnet build` normally includes restore; `--no-restore` assumes restore inputs/outputs are already available. Identify what the actual host requested rather than assuming a textual target list means the supported transition was used.

The Common imports also change behavior during restore:

- [Common props](../../../../src/Tasks/Microsoft.Common.props) controls project-extension props through `MSBuildIsRestoring` and `ImportProjectExtensionProps`.
- [Common targets](../../../../src/Tasks/Microsoft.Common.targets) has the corresponding project-extension target controls.

Do not casually unify global-property sets or reuse an evaluated instance to remove a perceived redundant evaluation. Restore can change which files exist and which imports are valid; the standard path also owns cache invalidation and missing-import handling.

## Choosing evidence

For import/default changes, cover the actual SDK/import style and property precedence at issue. For restore transitions, use the relevant missing/stale generated-import scenario and distinguish first restore from an already-restored build.

If the required SDK/host implementation is not available, state that boundary. Do not change `global.json`, install a different SDK, or claim a locally installed unrelated SDK proves the requested behavior.
