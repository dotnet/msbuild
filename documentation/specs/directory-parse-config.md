# Directory.Parse.config — Ignoring Unexpected Attributes and Elements

## Summary

MSBuild now supports a configuration file (`Directory.Parse.config`) that allows specific unrecognized attributes or child elements to be silently skipped during project parsing, instead of throwing `InvalidProjectFileException` (MSB4066/MSB4067).

This enables compatibility scenarios where project files may include attributes/elements intended for newer MSBuild versions or third-party tools that can still build without those attributes/elements.

## Configuration File Format

`Directory.Parse.config` is a plain text file with one entry per line:

```
# Lines starting with # are comments
# Empty lines are ignored

# Format: Type:Name:AllowedName
Attribute:Target:CustomAttr
Element:Project:CustomElement
Attribute:PropertyGroup:NewFeatureFlag
```

- **Type**: Either `Attribute` or `Element` (case-insensitive)
- **Name**:  `Attribute`: the name/type of the element; `Element`: the name/type of the parent element
- **AllowedName**: The name of the attribute or child element to allow

Matching is case-insensitive.
Invalid lines (wrong format, unknown type, wrong number of colons) are silently ignored.

For more details on `Name`:

### Valid Attribute Names

For attributes the `Name` can be the name of the following MSBuild elements:
- `Target`
- `PropertyGroup`
- `ItemGroup`
- `Import`
- `ImportGroup`
- `UsingTask`
- `OnError`
- `Output`
- `Choose`
- `Otherwise`
- `ProjectExtensions`

Although the following are not elements, they can be specified to target attributes on these types of elements:
- `Property`: attributes on elements inside a `PropertyGroup`
- `Item`: attributes on elements inside a `ItemGroup`
- `Metadata`: attributes on elements inside an `Item` or `ItemDefinitionGroup`
- `Parameter`: attributes on elements in a ParameterGroup inside a UsingTask
- `UsingTaskBody`: attributes on Task elements inside a UsingTask

### Valid Element Names

The following names can be specified:
- `Project`
- `Import`
- `ImportGroup`
- `UsingTask`
- `OnError`
- `Output`
- `Choose`
- `When`
- `Otherwise`

The following cannot be specified since their children are by definition always valid:
- `Target`
- `PropertyGroup`
- `ItemGroup`
- `ProjectExtensions`

## Discovery and Loading

Configuration files are discovered and merged additively:

1. **Global config at build start**
   - Next to the MSBuild executable
   - User profile: `%USERPROFILE%\.msbuild\Directory.Parse.config` (Windows) or `$HOME/.msbuild/Directory.Parse.config` (Unix)
   - Paths listed in `MSBUILD_PARSE_CONFIG`, split using the platform path separator
   - The startup directory of MSBuild

2. **Directory-specific config at evaluation start**
   - MSBuild walks up from the project file's directory looking for `Directory.Parse.config`
   - If found and that file was not already loaded globally, it is loaded and merged for that evaluation

## Centralized Loading (BuildManager)

In a normal build flow, the main node loads global config once during `BuildManager.BeginBuild()`. The config is:
- Stored on `BuildParameters.UnknownElementsConfiguration`
- Serialized to worker nodes via the standard `ITranslatable` mechanism
- Set on the shared `ProjectRootElementCache`, so parsing uses the same configuration

At evaluation start, MSBuild may merge in one additional `Directory.Parse.config` discovered from the project file's directory walk.

Users of the `ProjectCollection` API can also set `ProjectCollection.UnknownElementsConfiguration` directly, which flows into `BuildParameters` when builds are started.

## Logging

During evaluation, two low-importance messages can be logged to the binary log:

- **Config files loaded**: Lists all discovered and loaded config file paths.
- **Skipped items summary**: Lists all unrecognized attributes/elements that were skipped, with occurrence counts. This is logged after evaluation work has completed so it reflects actual skips from that evaluation.

Example log messages:
```
Loaded Directory.Parse.config from: C:\repo\Directory.Parse.config, C:\Users\me\.msbuild\Directory.Parse.config
Skipped unrecognized items allowed by Directory.Parse.config: Attribute:Target:CustomAttr (2 occurrences); Element:Project:Widget (1 occurrence)
```

## Examples

### Allow a custom attribute on Target elements

Directory.Parse.config:
```
Attribute:Target:CustomAttr
```

This allows `<Target Name="Build" CustomAttr="value">` without error.

### Allow a custom child element under Project

Directory.Parse.config:
```
Element:Project:ToolConfiguration
```

This allows `<ToolConfiguration ... />` as a direct child of `<Project>` without error.
