# BuildNonexistentProjectsByDefault Global Property

## Summary

The `_BuildNonexistentProjectsByDefault` global property enables MSBuild tasks to build in-memory or virtual projects by defaulting to `SkipNonexistentProjects=Build` behavior when the property is not explicitly specified.

## Background and Motivation

### Problem

[File-based applications][file-based-apps] (such as `dotnet run file.cs`) create in-memory MSBuild projects without corresponding physical `.csproj` files on disk. When these projects use common targets that include MSBuild tasks referencing the current project (e.g., `<MSBuild Projects="$(MSBuildProjectFullPath)" />`), the build fails because MSBuild cannot find the project file on disk, even though the project content is available in memory.

This pattern is very common in .NET SDK targets, creating friction for file-based applications that need to reuse existing build logic.

### Use Case Example

Consider a file-based application that creates an in-memory project:

```csharp
var xmlReader = XmlReader.Create(new StringReader(projectText));
var projectRoot = ProjectRootElement.Create(xmlReader);
projectRoot.FullPath = Path.Join(Environment.CurrentDirectory, "test.csproj");
// Project exists in memory but not on disk
```

When this project uses targets containing:
```xml
<MSBuild Projects="$(MSBuildProjectFullPath)" Targets="SomeTarget" />
```

The build fails with:
> MSB3202: The project file "test.csproj" was not found.

## Solution

### The `_BuildNonexistentProjectsByDefault` Property

This internal global property provides an opt-in mechanism to change the default behavior of MSBuild tasks when `SkipNonexistentProjects` is not explicitly specified.

**Property Name:** `_BuildNonexistentProjectsByDefault`  
**Type:** Boolean  
**Default:** `false` (when not set)  
**Scope:** Global property only

### Behavior

When `_BuildNonexistentProjectsByDefault` is set to `true`:

1. **MSBuild tasks** that don't explicitly specify `SkipNonexistentProjects` will default to `SkipNonexistentProjects="Build"` instead of `SkipNonexistentProjects="False"`
2. **In-memory projects** with a valid `FullPath` can be built even when no physical file exists on disk
3. **Existing explicit settings** are preserved - if `SkipNonexistentProjects` is explicitly set on the MSBuild task, that takes precedence

### Implementation Details

The property is checked in two MSBuild task implementations:

1. **`src/Tasks/MSBuild.cs`** - The standard MSBuild task implementation
2. **`src/Build/BackEnd/Components/RequestBuilder/IntrinsicTasks/MSBuild.cs`** - The backend intrinsic task implementation

The logic follows this precedence order:

1. If `SkipNonexistentProjects` is explicitly set on the MSBuild task → use that value
2. If `SkipNonexistentProjects` metadata is specified on the project item → use that value  
3. If `_BuildNonexistentProjectsByDefault=true` is set globally → default to `Build`
4. Otherwise → default to `Error` (existing behavior)

## Usage

### File-based Applications

File-based applications can set this property when building in-memory projects:

```csharp
var project = ObjectModelHelpers.CreateInMemoryProject(projectContent);
project.SetGlobalProperty("_BuildNonexistentProjectsByDefault", "true");
bool result = project.Build();
```

### SDK Integration

The .NET SDK will use this property to enable building file-based applications without workarounds when calling MSBuild tasks that reference the current project.

## Breaking Changes

**None.** This is an opt-in feature with an internal property name (prefixed with `_`). Existing behavior is preserved when the property is not set.

## Alternatives Considered

1. **Always allow building in-memory projects**: This would be a small breaking change. It would also likely require more work to implement, as we would need to detect that the project being built is an in-memory project.

2. **Add a new MSBuild task parameter**: This would require modifying all existing targets to use the new parameter, creating compatibility issues.

3. **Modify SkipNonexistentProjects default**: This would be a breaking change affecting all MSBuild usage.

4. **Engine-level configuration**: More complex to implement and would require serialization across build nodes.

The global property approach provides the needed functionality while maintaining backward compatibility and requiring minimal changes to the MSBuild task implementations.

## Related Issues

- [#12058](https://github.com/dotnet/msbuild/issues/12058) - MSBuild task should work on virtual projects
- [dotnet/sdk#49745](https://github.com/dotnet/sdk/pull/49745) - Remove MSBuild hacks for virtual project building
- [NuGet/Home#14148](https://github.com/NuGet/Home/issues/14148) - Related workaround requirements
- [File-based app spec][file-based-apps] - Motivating use-case

[file-based-apps]: https://github.com/dotnet/sdk/blob/main/documentation/general/dotnet-run-file.md
