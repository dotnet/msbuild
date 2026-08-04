# Directory.Parse.config — Ignoring Unexpected Attributes and Elements

## Summary

MSBuild now supports a configuration file (`Directory.Parse.config`) that allows specific unrecognized attributes or child elements to be silently skipped during project parsing, instead of throwing `InvalidProjectFileException` (MSB4066/MSB4067).

This enables compatibility scenarios where project files may include attributes/elements intended for newer MSBuild versions or third-party tools that can still build without those attributes/elements.

## Configuration File Format

`Directory.Parse.config` is an XML file:

```xml
<ParseConfig>
  <IgnoreAttributes>
    <Ignore Element="Target" Name="CustomAttr" />
    <Ignore Element="Property" Name="NewFeatureFlag" />
  </IgnoreAttributes>
  <IgnoreChildren>
    <Ignore Element="Project" Name="CustomElement" />
    <Ignore Element="Task" Name="CustomChild" />
  </IgnoreChildren>
</ParseConfig>
```

- **Root element**: `<ParseConfig>`
- **`<IgnoreAttributes>`**: Contains `<Ignore>` entries for attributes to skip. Appears 0 or 1 times.
- **`<IgnoreChildren>`**: Contains `<Ignore>` entries for child elements to skip. Appears 0 or 1 times.
- **`<Ignore>`**: Each entry has `Element` (the parent/owner element name) and `Name` (the attribute or child to allow).

Matching is case-insensitive. Entries with missing or empty `Element`/`Name` attributes are silently ignored. Unrecognized sections or elements are ignored.

### Valid `IgnoreAttributes` Elements

The `Element` attribute in `<IgnoreAttributes>` entries can be:
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

Generic names for dynamically-named elements:

| Generic Name | Applies To |
|---|---|
| `Property` | Any property element (child of `PropertyGroup`) |
| `Item` | Any item element (child of `ItemGroup`) |
| `ItemDefinition` | Any item definition element (child of `ItemDefinitionGroup`) |
| `Metadata` | Any metadata element (child of an `Item` or `ItemDefinition`) |
| `Parameter` | Any parameter element in a `ParameterGroup` inside `UsingTask` |
| `UsingTaskBody` | The `Task` element inside `UsingTask` |

### Valid `IgnoreChildren` Elements

The `Element` attribute in `<IgnoreChildren>` entries can be:
- `Project`
- `Import`
- `ImportGroup`
- `Task`
- `UsingTask`
- `OnError`
- `Output`
- `Choose`
- `When`
- `Otherwise`

Not applicable (children are free-form by design):

- `Target`
- `PropertyGroup`
- `ItemGroup`
- `ItemDefinitionGroup`
- `ProjectExtensions`

## Discovery and Loading

Configuration is discovered from two sources:

1. **`MSBUILD_PARSE_CONFIG` environment variable** — Semicolon-separated (Windows) or colon-separated (Unix) list of config file paths. Always loaded when a `ProjectCollection` is created.

2. **Project directory walk** — The CLI walks up from the target project file's directory looking for `Directory.Parse.config` (same pattern as `Directory.Build.rsp`). Called via `ProjectCollection.LoadParseConfigForStartup(directory)` before any project is loaded.

These are merged additively — entries from either source are combined.

## Public API

### `ProjectCollection.LoadParseConfigForStartup(string startingDirectory)`

Walks up from `startingDirectory` to find a `Directory.Parse.config`, merges it with the current config, and sets it on the cache. Must be called before loading projects. Call `UnloadParseConfigForStartup()` after the build to restore the previous state.

### `ProjectCollection.UnloadParseConfigForStartup()`

Restores the parse configuration to the state before `LoadParseConfigForStartup` was called.


## Logging

- **Config files loaded**: Logged at evaluation start (`MessageImportance.Low`). Lists all loaded config file paths.
- **Config files embedded**: Each loaded config file is logged as a `ProjectImportedEventArgs`, causing the binary logger to embed the file content in the binlog.
- **Skipped items summary**: Logged after evaluation completes (`MessageImportance.Low`). Lists all skipped attributes/elements with occurrence counts.

## Feature Flag

Set `MSBUILD_DISABLE_PARSE_CONFIG=1` to disable all automatic config loading.

## Examples

### Allow a custom attribute on all Target elements

```xml
<ParseConfig>
  <IgnoreAttributes>
    <Ignore Element="Target" Name="CustomAttr" />
  </IgnoreAttributes>
</ParseConfig>
```

Allows `<Target Name="Build" CustomAttr="value">` without error.

### Allow a custom child element under Project

```xml
<ParseConfig>
  <IgnoreChildren>
    <Ignore Element="Project" Name="ToolConfiguration" />
  </IgnoreChildren>
</ParseConfig>
```

Allows `<ToolConfiguration ... />` as a direct child of `<Project>` without error.
