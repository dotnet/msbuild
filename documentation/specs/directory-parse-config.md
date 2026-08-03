# Directory.Parse.config — Ignoring Unexpected Attributes and Elements

## Summary

MSBuild supports a configuration file (`Directory.Parse.config`) that allows specific unrecognized
attributes or child elements to be silently skipped during project parsing, instead of throwing
`InvalidProjectFileException` (MSB4066/MSB4067).

This enables compatibility scenarios where project files include attributes or elements intended for
newer MSBuild versions or third-party tools, and the build is still correct without them.

## Configuration file format

`Directory.Parse.config` is an XML file, consistent with MSBuild itself and with the other `.config`
files in the ecosystem (`NuGet.config`, `app.config`):

```xml
<ParseConfig>
  <!-- Permit an attribute on a given element -->
  <AllowAttribute Element="Target" Name="CustomAttr" />

  <!-- Permit a child element beneath a given parent -->
  <AllowElement Parent="Project" Name="ToolConfiguration" />
</ParseConfig>
```

Element and attribute names are matched case-insensitively.

Permissions are granted per name. There is no way to express "tolerate anything": an unrecognized
name that is not listed still produces MSB4066/MSB4067, so ordinary typos remain errors.

### Diagnostics

Two categories of problem are reported at low importance during evaluation rather than failing the
build:

- **Unrecognized directives** (an element under `<ParseConfig>` this engine does not know) are
  ignored, so that a future MSBuild can add directives without older engines rejecting the file.
- **Malformed directives** (a known directive missing a required attribute) are ignored and reported.

A file that is not well-formed XML contributes nothing at all — no partial configuration is applied —
and the parse error is reported.

Reporting both means a typo such as `<AlowAttribute>` shows up in the log rather than presenting later
as an apparently unrelated MSB4066.

### Valid `Element` values for `AllowAttribute`

- `Target`, `PropertyGroup`, `ItemGroup`, `Import`, `ImportGroup`, `UsingTask`, `OnError`, `Output`,
  `Choose`, `Otherwise`, `ProjectExtensions`

The following are not elements, but may be specified to target attributes on those kinds of element:

- `Property`: attributes on elements inside a `PropertyGroup`
- `Item`: attributes on elements inside an `ItemGroup`
- `Metadata`: attributes on elements inside an `Item` or `ItemDefinitionGroup`
- `Parameter`: attributes on elements in a `ParameterGroup` inside a `UsingTask`
- `UsingTaskBody`: attributes on `Task` elements inside a `UsingTask`

### Valid `Parent` values for `AllowElement`

- `Project`, `Import`, `ImportGroup`, `UsingTask`, `OnError`, `Output`, `Choose`, `When`, `Otherwise`

`Target`, `PropertyGroup`, `ItemGroup` and `ProjectExtensions` cannot be specified because their
children are open-ended by definition and are already accepted.

## Discovery

The configuration is resolved **once per build**, anchored on the directory of the **entry project**,
by walking up to the nearest `Directory.Parse.config`. The current working directory is used only when
no project was specified.

This matches how `Directory.Build.rsp` is discovered (`CommandLineParser.GetProjectDirectory`), and how
`Directory.Build.props` and `Directory.Solution.props` are located.

**First found wins.** There is no layering: a `Directory.Parse.config` in a subdirectory replaces one
higher up rather than adding to it. A nearer file that omits a permission granted by a farther one fails
loudly, with MSB4066/MSB4067 naming the attribute or element.

There are no machine-wide or environment-variable sources. Whether a project loads should not depend on
state that is not in source control.

Set `MSBUILD_DISABLE_PARSE_CONFIG=1` to disable the feature entirely.

## One build, one set of rules

Because the configuration is anchored on the entry project, a single build has exactly one set of parse
rules. Building a project in a directory that has no `Directory.Parse.config` means nothing is permitted
anywhere in that build, even for referenced projects whose own directories contain one.

This is what makes rules from one workspace unable to leak into another.

## Ownership and lifetime

The resolved configuration is an immutable object with a content-based identity. Two configurations
permitting the same names are interchangeable; any difference in permitted names is a different identity.

`ProjectRootElementCache` takes the configuration as a **constructor parameter** and keeps it for its
lifetime. A cache therefore can never hold elements parsed under differing rules. Where a cache outlives
a build, the configuration is compared and the cache is replaced when it differs, rather than mutated:

| Path | Collection | Cache | Behaviour |
|---|---|---|---|
| CLI, no server | fresh per build | fresh per build | nothing to do |
| MSBuild Server | fresh per build | static, reused | identity checked before adopting `s_projectRootElementCache` |
| Worker nodes | n/a | process-static, reused | identity checked in `OutOfProcNode.HandleNodeConfiguration`; cache replaced on mismatch |
| Long-lived host (VS) | persists with loaded projects | persists | resolved from the first project loaded and then frozen; re-resolved only while no projects are loaded |

Editing `Directory.Parse.config` changes its identity, so the next build re-parses under the new rules.
No node recycling or cache invalidation logic is required.

The configuration is carried on `BuildParameters` purely so that it reaches worker nodes via
`NodeConfiguration`; worker nodes never perform their own directory walk, so the main node and every
worker always agree about how a given file parses.

## Hosts

Hosts that know nothing about this feature — notably Visual Studio — are supported without any host-side
change: a `ProjectCollection` that was not given a configuration resolves one from the directory of the
first project loaded into it.

Note that permitted names are tolerated by the engine but will still be flagged by the Visual Studio XML
editor, whose IntelliSense is driven by the XSDs shipped in the Visual Studio installation.

## Logging

During evaluation, low-importance messages report the config file that was loaded and a summary of the
names actually skipped, so a binary log shows both which rules applied and whether they were used.
