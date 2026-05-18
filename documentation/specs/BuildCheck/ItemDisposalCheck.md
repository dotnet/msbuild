# BC0303 - ItemDisposalCheck Specification

## Overview

The ItemDisposalCheck analyzer detects MSBuild Targets that create "private" item lists (item types with names starting with underscore `_`) but fail to clean them up at the end of the target. This pattern can lead to memory waste as these items persist in the build's item collection even though they are only intended for use within the target.

## Motivation

MSBuild does not have a built-in concept of "private" or scoped items within targets. When a target creates items using `Include`, those items become part of the global item collection and persist for the remainder of the build. This can cause:

1. **Memory waste**: Temporary items accumulate in memory unnecessarily
2. **Naming collisions**: Future targets might accidentally reference stale items
3. **Build log noise**: More items to track and display in diagnostic output
4. **Potential correctness issues**: Incremental builds might behave unexpectedly if leftover items influence subsequent target executions

The convention of prefixing item type names with an underscore (`_`) signals that the item is intended to be "private" to the target that creates it. This check enforces that such items are properly cleaned up.

## Detection Logic

The check analyzes the static structure of MSBuild project files and reports a diagnostic when ALL of the following conditions are met:

1. **A Target contains an ItemGroup** with an item element that uses `Include` to create items
2. **The item type name starts with underscore** (`_`) - signaling private/internal usage
3. **The item type is NOT referenced** in the Target's `Outputs` or `Returns` attributes
4. **No matching Remove operation** exists within the same target that cleans up the item type

### Positive Examples (SHOULD fire)

```xml
<!-- BC0303: _TempFiles is created but never removed -->
<Target Name="BadExample1">
  <ItemGroup>
    <_TempFiles Include="$(TempDir)/*.tmp" />
  </ItemGroup>
  <Delete Files="@(_TempFiles)" />
</Target>

<!-- BC0303: _DotnetExecArgs is created but never removed -->
<Target Name="BadExample2">
  <ItemGroup>
    <_DotnetExecArgs Include="dotnet;tool;exec;$(CoolToolName)" />
  </ItemGroup>
  <Exec Command="@(_DotnetExecArgs, ' ')" />
</Target>
```

### Negative Examples (should NOT fire)

```xml
<!-- OK: Item is properly cleaned up with Remove -->
<Target Name="GoodExample1">
  <ItemGroup>
    <_TempFiles Include="$(TempDir)/*.tmp" />
  </ItemGroup>
  <Delete Files="@(_TempFiles)" />
  <ItemGroup>
    <_TempFiles Remove="@(_TempFiles)" />
  </ItemGroup>
</Target>

<!-- OK: Item type does not start with underscore (not private) -->
<Target Name="GoodExample2">
  <ItemGroup>
    <MyPublicItems Include="*.cs" />
  </ItemGroup>
</Target>

<!-- OK: Item is exposed via Returns attribute -->
<Target Name="GoodExample3" Returns="@(_ComputedItems)">
  <ItemGroup>
    <_ComputedItems Include="@(SourceFiles->'%(Filename).obj')" />
  </ItemGroup>
</Target>

<!-- OK: Item is exposed via Outputs attribute -->
<Target Name="GoodExample4" Outputs="@(_GeneratedFiles)">
  <ItemGroup>
    <_GeneratedFiles Include="$(OutputDir)/*.generated.cs" />
  </ItemGroup>
</Target>

<!-- OK: Item only uses Update (no new items created) -->
<Target Name="GoodExample5">
  <ItemGroup>
    <_ExistingItems Update="@(_ExistingItems)" SomeMetadata="value" />
  </ItemGroup>
</Target>
```

## Build Events

This check operates on **parsed/static XML structure** of the project file during evaluation. It uses the `ParsedItemsCheckData` (or via `ProjectRootElement` traversal) to examine:

- `ProjectTargetElement` - for each target in the project
- `ProjectItemGroupElement` - for ItemGroups within targets
- `ProjectItemElement` - for individual item definitions

## Scope

- **Project Types**: All MSBuild projects (SDK-style and legacy)
- **Configurations**: Applied uniformly regardless of build configuration
- **Imports**: By default, only the project file itself is analyzed. The scope can be configured via the standard `EvaluationCheckScope` setting.

## Expected Behavior

### When the check fires:
- A warning is reported at the location of the `Include` attribute on the offending item element
- The message identifies the item type name and the target name

### When the check does NOT fire:
- Item types not starting with underscore
- Items that have a matching `Remove` operation in the same target
- Items referenced in the target's `Outputs` or `Returns` attributes
- Items that only use `Update` (not `Include`)

## Configuration Options

### Severity

The default severity is `Warning`. Users can configure this via `.editorconfig`:

```ini
[*.csproj]
build_check.BC0303.severity=warning  # default
build_check.BC0303.severity=error    # treat as build error
build_check.BC0303.severity=suggestion  # informational only
build_check.BC0303.severity=none     # disable
```

### Scope

```ini
[*.csproj]
build_check.BC0303.scope=project_file    # default - only project file
build_check.BC0303.scope=work_tree_imports  # include non-SDK imports
build_check.BC0303.scope=all             # include all imports
```

## Error Code

- **Code**: BC0303
- **Title**: PrivateItemsNotDisposed
- **Category**: Build Authoring / Performance

## Message Format

```
Private item list '{0}' created in target '{1}' should be removed at the end of the target to avoid wasting memory.
```

Where:
- `{0}` = The item type name (e.g., `_TempFiles`)
- `{1}` = The target name (e.g., `CompileCore`)

## Performance Considerations

- **Impact**: Low - only examines static XML structure during evaluation
- **Caching**: Uses the existing `ProjectRootElement` cache
- **Scope optimization**: When scope is limited to project file only, imports are not traversed

## Implementation Notes

1. The check should be case-insensitive when comparing item type names (MSBuild convention)
2. When checking for `Remove` operations, the item type must match exactly
3. The `Outputs` and `Returns` attributes may contain property expressions that reference items - these should be pattern-matched for `@(ItemType)` syntax
4. Multiple `Include` operations for the same item type in the same target only need ONE matching `Remove`
5. The check should handle the case where `Include` and `Remove` are on the SAME element (which is a no-op but valid)

## Related Checks

- BC0201 - Usage of undefined property (similar pattern of detecting unbalanced operations)
- BC0203 - Property declared but never used (similar concept of detecting unused declarations)

## References

- [MSBuild Item Element Documentation](https://docs.microsoft.com/visualstudio/msbuild/item-element-msbuild)
- [MSBuild Target Element Documentation](https://docs.microsoft.com/visualstudio/msbuild/target-element-msbuild)
- [MSBuild Best Practices](https://docs.microsoft.com/visualstudio/msbuild/msbuild-best-practices)
