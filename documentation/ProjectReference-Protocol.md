# The `ProjectReference` Protocol

The MSBuild engine doesn't have a notion of a “project reference”—it only provides the [`MSBuild` task](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-task) to allow cross-project communication.

That's a powerful tool, but no one would want to have to specify how to build every single reference in every single project. The common targets introduce an item, `ProjectReference`, and a default process for building references declared via that item.

Default protocol implementation:
- https://github.com/Microsoft/msbuild/blob/master/src/Tasks/Microsoft.Common.CurrentVersion.targets
- https://github.com/Microsoft/msbuild/blob/master/src/Tasks/Microsoft.Common.CrossTargeting.targets

## Projects that have references

In its simplest form, a project need only specify the path to another project in a `ProjectReference` item. For example,

```csproj
<ItemGroup>
  <ProjectReference Include="..\..\some\other\project.csproj" />
</ItemGroup>
```

Importing `Microsoft.Common.targets` includes logic that consumes these items and transforms them into compile-time references before the compiler runs. 

## Who this document is for

This document describes that process, including what is required of a project to be referenceable through a `ProjectReference`. It is intended for for MSBuild SDK maintainers, and those who have created a completely custom project type that needs to interoperate with other projects. It may also be of interest if you'd like to see the implementation details of references. Understanding the details should not be necessary to _use_ `ProjectReferences` in your project.

## Targets related to consuming a reference

The bulk of the work of transforming `ProjectReference` items into something suitable to feed the compiler is done by tasks listed in the `ResolveReferencesDependsOn` property defined in `Microsoft.Common.CurrentVersion.targets`.

There are empty hooks in the default targets for

* `BeforeResolveReferences`—run before the primary work of resolving references.
* `AfterResolveReferences`—run after the primary work of resolving references.

`AssignProjectConfiguration` runs when building in a solution context, and ensures that the right `Configuration` and `Platform` are assigned to each reference. For example, if a solution specifies (using the Solution Build Manager) that for a given solution configuration, a project should always be built `Release`, that is applied inside MSBuild in this target.

`PrepareProjectReferences` then runs, ensuring that each referenced project exists (creating the item `@(_MSBuildProjectReferenceExistent)`).

`_ComputeProjectReferenceTargetFrameworkMatches` calls `GetTargetFrameworks` in existent ProjectReferences and determines the parameters needed to produce a compatible build by calling the `AssignReferenceProperties` task for each reference that multitargets.

`ResolveProjectReferences` does the bulk of the work, building the referenced projects and collecting their outputs.

After the compiler is invoked, `GetCopyToOutputDirectoryItems` pulls child-project outputs into the current project to be copied to its output directory.

When `Clean`ing the output of a project, `CleanReferencedProjects` ensures that referenced projects also clean.

## Targets required to be referenceable

These targets should exist in a project to be compatible with the common targets' `ProjectReference`. Some are called only conditionally.

These targets are all defined in `Microsoft.Common.targets` and are defined in Microsoft SDKs. You should only have to implement them yourself if you require custom behavior or are authoring a project that doesn't import the common targets.

If implementing a project with an “outer” (determine what properties to pass to the real build) and “inner” (fully specified) build, only `GetTargetFrameworkProperties` is required in the “outer” build. The other targets listed can be “inner” build only.

* `GetTargetFrameworks` tells referencing projects what options are available to the build.
  * It returns an item with metadata `TargetFrameworks` indicating what TargetFrameworks are available in the project, as well as boolean metadata `HasSingleTargetFramework` and `IsRidAgnostic`.
  * This target is _optional_. If not present, the reference will be built with no additional properties.
  * **New** in MSBuild 15.5.
* `GetTargetFrameworkProperties` determines what properties should be passed to the “main” target for a given `ReferringTargetFramework`.
  * **Deprecated** in MSBuild 15.5.
  * New for MSBuild 15/Visual Studio 2017. Supports the cross-targeting feature allowing a project to have multiple `TargetFrameworks`.
  * **Conditions**: only when metadata `SkipGetTargetFrameworkProperties` for each reference is not true.
  * Skipped for `*.vcxproj` by default.
  * This should return either
    * a string of the form `TargetFramework=$(NearestTargetFramework);ProjectHasSingleTargetFramework=$(_HasSingleTargetFramework);ProjectIsRidAgnostic=$(_IsRidAgnostic)`, where the value of `NearestTargetFramework` will be used to formulate `TargetFramework` for the following calls and the other two properties are booleans, or
    * an item with metadata `DesiredTargetFrameworkProperties` (key-value pairs of the form `TargetFramework=net46`), `HasSingleTargetFramework` (boolean), and `IsRidAgnostic` (boolean).
* `GetTargetPath` should return the path of the project's output, but _not_ build that output.
  * **Conditions**: this is used for builds inside Visual Studio, but not on the command line.
  * It's also used when the property `BuildProjectReferences` is `false`, manually indicating that all `ProjectReferences` are up to date and shouldn't be (re)built.
  * This should return a single item that is the primary output of the project, with metadata describing that output. See [`TargetPathWithTargetPlatformMoniker`](https://github.com/Microsoft/msbuild/blob/080ef976a428f6ff7bf53ca5dd4ee637b3fe949c/src/Tasks/Microsoft.Common.CurrentVersion.targets#L1834-L1842) for the default metadata.
* **Default** targets should do the full build and return an assembly to be referenced.
  * **Conditions**: this is _not_ called when building inside Visual Studio. Instead, Visual Studio builds each project in isolation but in order, so the path returned from `GetTargetPath` can be assumed to exist at consumption time.
  * If the `ProjectReference` defines the `Targets` metadata, it is used. If not, no target is passed, and the default target of the reference (usually `Build`) is built.
  * The return value of this target should be identical to that of `GetTargetPath`.
* `GetNativeManifest` should return a manifest suitable for passing to the `ResolveNativeReferences` target.
  * As of 15.7, this is _optional_. If a project does not contain a `GetNativeManifest` target, it will not be referencable by native projects but will not fail the build.
* `GetCopyToOutputDirectoryItems` should return the outputs of a project that should be copied to the output of a referencing project.
  * As of 15.7, this is _optional_. If a project does not contain a `GetCopyToOutputDirectoryItems` target, projects that reference it will not copy any of its outputs to their own output folders, but the build can succeed.
* `Clean` should delete all outputs of the project.
  * It is not called during a normal build, only during "Clean" and "Rebuild".

## Other protocol requirements

As with all MSBuild logic, targets can be added to do other work with `ProjectReference`s.

In particular, NuGet depends on being able to identify referenced projects' package dependencies, and calls some targets that are imported through `Microsoft.Common.targets` to do so. At the time of writing this this is in [`NuGet.targets`](https://github.com/NuGet/NuGet.Client/blob/79264a74262354c1a8f899c2c9ddcaff58afaf62/src/NuGet.Core/NuGet.Build.Tasks/NuGet.targets).

`Microsoft.AppxPackage.targets` adds a dependency on the target `GetPackagingOutputs`.
