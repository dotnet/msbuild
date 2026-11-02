# MSBuild's reserved and built-in properties

The MSBuild engine itself sets some properties for all projects. There is normal documentation for the [reserved properties and their meanings](https://docs.microsoft.com/visualstudio/msbuild/msbuild-reserved-and-well-known-properties). This document describes the implementation of these properties in MSBuild itself.

There are actually two different implementations of this functionality in MSBuild.

## Built-in properties

When evaluating an individual project, Pass 0 of the evaluation calls [`AddBuiltInProperties()`][addbuiltinproperties] which in turn calls [`SetBuiltInProperty()`][setbuiltinproperty] which sets the property basically as normal.

However, properties set there are not available at all parts of execution, and specifically they're not available when evaluating the `.tasks` file that makes MSBuild's built-in tasks available by default to all projects.

## Reserved properties

Reserved properties are [set by the toolset][toolset_reservedproperties] and are available _only_ in the `.tasks` and `.overridetasks` cases. Properties set there are not available in normal project evaluation.

## Synthesized import items

When the property `MSBuildProvideImportedProjects` is set to `true`, the engine synthesizes `MSBuildImportedProject` items during evaluation. Each item represents a file imported during evaluation, with:

- **Identity** — the full path of the imported file.
- **`ImportingProjectPath`** metadata — the full path of the file containing the `<Import>` element.
- **`Sdk`** metadata — the SDK name if the import was resolved via an SDK reference (e.g. `Microsoft.NET.Sdk`); empty otherwise.

The property can be set in the project file or passed as a global property (e.g. `/p:MSBuildProvideImportedProjects=true`). The items are regular MSBuild items, so they serialize to out-of-proc worker nodes and are available to any target or task. Projects that don't set the property pay zero cost.

Each imported file appears at most once (first occurrence in depth-first evaluation order), so the collection forms a tree. The root project itself is excluded — only actual import relationships are represented.

Implementation: items are synthesized in [`Evaluator.SynthesizeImportedProjectItems()`][synthesizeimporteditems] at the start of the items evaluation pass.

## Synthesized item glob items

When the property `MSBuildProvideItemGlobs` is set to a semicolon-separated list of item types (e.g. `Compile;Content`), the engine synthesizes `MSBuildItemGlob` items during evaluation that expose the *unevaluated* include/exclude/remove glob patterns of those item types. This lets a target or task — or any consumer of the build output — recover the wildcard patterns of a project (with their precedence) without re-running evaluation or hosting the MSBuild API.

For each include element of a listed item type that contributes globs, one item is synthesized with:

- **Identity** — the item type (e.g. `Compile`). Multiple include elements of the same type each produce their own item; identities repeat.
- **`Include`** metadata — the include glob pattern(s) of the element (semicolon-separated, property references expanded, wildcards preserved).
- **`Exclude`** metadata — the exclude pattern(s) present on the element (literals and globs).
- **`Remove`** metadata — the remove pattern(s) that apply to the element (literals and globs).

`MSBuildProvideItemGlobs` is intended primarily for tooling that consumes the build output — for example a file watcher or a language service deciding whether a file belongs to the project. It can be set in the project file, in a `Directory.Build.props`, through `ProjectCollection.GlobalProperties`, or as a command-line global property. The synthesized items are regular MSBuild items, so they serialize to out-of-proc worker nodes and are available to any target or task. Projects that don't set the property pay zero cost.

> [!IMPORTANT]
> On the command line the value separator (`;`) must be escaped as `%3B`, because MSBuild's `/property` (`/p`) switch splits its argument on both `;` and `,`. For example, `/p:MSBuildProvideItemGlobs=Compile%3BContent` sets the value to `Compile;Content`, whereas an unescaped `Compile;Content` (or `Compile,Content`) would be parsed as two separate properties. This only affects command-line callers; values set in a project file or through `GlobalProperties` need no escaping.

The patterns live in metadata (never in the item's include), so they are never expanded against the file system. Items are emitted in document order, and each carries only the removes that apply to it (i.e. removes that appear after it), so replaying the records reproduces the authored include/exclude/remove precedence. The data matches what [`Project.GetAllGlobs()`][getallglobs] returns for the item type; both share [`GlobResultBuilder`][globresultbuilder]. Literal (non-glob) *includes* are omitted (a change to a literally-included file is always accompanied by a project-file change), while literal excludes and removes are retained.

Each metadata value is a standard MSBuild-escaped, semicolon-separated list — separator and escape characters that occur *within* a pattern are escaped (a literal `;` becomes `%3B`, a literal `%` becomes `%25`, and so on). For the common case, where patterns contain no literal `;` or `%`, splitting the value on `;` recovers the patterns exactly. To recover patterns that themselves contain a literal `;` (for example a directory named `a;b`, which is escaped to `a%3Bb`) without ambiguity, split the *escaped* value (e.g. `ITaskItem2.GetMetadataValueEscaped`) and unescape each element, exactly as MSBuild treats item lists — naively splitting the fully-unescaped value would merge such a pattern's embedded `;` with the list separator. Literal semicolons in file paths are rare, so in practice a plain split of the unescaped value is sufficient.

Implementation: items are synthesized in [`Evaluator.SynthesizeItemGlobItems()`][synthesizeitemglobitems] at the end of the items evaluation pass, after all items have been evaluated.

[synthesizeimporteditems]: https://github.com/dotnet/msbuild/blob/main/src/Build/Evaluation/Evaluator.cs

[synthesizeitemglobitems]: https://github.com/dotnet/msbuild/blob/main/src/Build/Evaluation/Evaluator.cs

[getallglobs]: https://github.com/dotnet/msbuild/blob/main/src/Build/Definition/Project.cs

[globresultbuilder]: https://github.com/dotnet/msbuild/blob/main/src/Build/Evaluation/GlobResultBuilder.cs

[addbuiltinproperties]: https://github.com/dotnet/msbuild/blob/24b33188f385cee07804cc63ec805216b3f8b72f/src/Build/Evaluation/Evaluator.cs#L609-L612

[setbuiltinproperty]: https://github.com/dotnet/msbuild/blob/24b33188f385cee07804cc63ec805216b3f8b72f/src/Build/Evaluation/Evaluator.cs#L1257

[toolset_reservedproperties]: https://github.com/dotnet/msbuild/blob/24b33188f385cee07804cc63ec805216b3f8b72f/src/Build/Definition/Toolset.cs#L914-L921
