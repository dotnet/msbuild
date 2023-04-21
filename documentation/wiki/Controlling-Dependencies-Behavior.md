# Controlling references behavior

MSBuild recognizes a [few types of references](https://learn.microsoft.com/previous-versions/visualstudio/visual-studio-2015/msbuild/common-msbuild-project-items) (here we are mainly interested in `ProjectReference`, `PackageReference`, `Reference` aka assembly reference) and offers optional mechanisms to tailor some aspects of the references workings - transitive references resolution, multitargeted references resolution, copying references to output directory.

## .NET SDK projects and access to transitive references

[.NET SDK projects](https://learn.microsoft.com/dotnet/core/project-sdk/overview) by default make all transitive references accessible as if they were direct references.

This is provided for the compiler and analyzers to be able to properly inspect the whole dependency or/and inheritance chain of types when deciding about particular checks.

It is facilitated via `project.assets.json` file created by NuGet client during the restore operation. This file captures the whole transitive closure of the project dependency tree.

SDK build tasks require existence of this file (hence the infamous `Assets file <path>\project.assets.json not found` if the MSBuild.exe is run without prior restore operation). It is used to reconstruct the `ProjectReference`s and create `Reference` items for the content of `PackageReference`s for the project and make them available to the rest of the build. For this reason MSBuild and compiler by default sees those transitive references as if they were direct references.

## Access to transitive project references

Above described behavior can lead to easy unintentional breaking out of layering architecture separation. 

This behavior can be opted-out for `ProjectReference`s via `DisableTransitiveProjectReferences` property on the referencing project.

<a name="OnionArchSample"></a>*Example*:

Let's imagine an `Onion Architecture` design:

```mermaid
flowchart LR
    Service[Service Layer] --> Repository
    Repository[Repository Layer] --> Domain[Domain Layer]
```

Service Layer definition:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Repository\Repository.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>10</LangVersion>
    <!-- This prevents referencing types from transitive project references. -->
    <DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>
  </PropertyGroup>
</Project>
```

```csharp
namespace Service;
	
public class PersonsAccessor
{
    private Repository.Persona _persona;
    // This is allowed unless DisableTransitiveProjectReferences=true is passed into build.
    // private Domain.PersonTable _tbl;
}
```

## Access to transitive package references

The transitive access to references works by default for package references as well. This can be opted out for referencing projects via `PrivateAssets=compile` on the `PackageReference` of the concern. (More details on [Controlling package dependency assets](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets)).

When using this metadatum - the access to the package, its dirrect and transitive dependencies is **not** restricted for the project declaring the refenerence on the package in its `Project` element. It is restricted for the projects referencing the project (or package) that specified the `PackageRegerence` with the `PrivateAssets` metadatum.

*Example*:

In our previous example let's have `Repository Layer` reference `newtonsoft.json`:

```mermaid
flowchart LR
    Service[Service Layer] --> Repository
    Repository[Repository Layer] --> newtonsoft.json[newtonsoft.json]
```

We are not able to influence access to `newtonsoft.json` and its dependencies (would there be any) in the `Repository Layer`, but we can prevent it from propagating to `Service Layer`.

`Repository Layer`:

```xml
<ItemGroup>
  <PackageReference Include="newtonsoft.json" Version="13.0.1">
    <!-- This prevents the reference to be available to referencing types. -->
    <PrivateAssets>compile</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

Unless opted out via `PrivateAssets=compile`, our `Service Layer` would have access to `newtonsoft.json`:

```csharp
namespace Service;
//This is allowed unless PrivateAssets=compile is set on the PackageDependency in Repository.
//using Newtonsoft.Json;
	
public class PersonsAccessor
{
    private Repository.Persona _persona;
}
```

**Notes:**
   `PrivateAssets` metadatum (and it's counterparts `IncludeAssets` and `ExcludeAssets`) is applicable to `PackageReference` and controls exposure of dependencies to the consuming projects, not the current project. It is currently not possible to prevent access to package references from within directly referencing project - this is purely decision of the package itself (as it can define it's dependencies as `PrivateAssets`).

## Not copying dependencies to output

By default the above mentioned dependency types are copied to the build output directory during the build. There can be various scenarios where this behavior is not desired (examples: dependency is compile time only or contains a logic for build; component is plugin to a main app and there is a desire not to duplicate common dependencies in output).

Overriding this logic depends on the type of the dependency.

### Not copying Assembly Reference

Copying can be opted out via [Private metadata on the Reference item](https://learn.microsoft.com/previous-versions/visualstudio/visual-studio-2015/msbuild/common-msbuild-project-items?view=vs-2015#reference) (which corresponds to the `Copy Local` property of the reference in the Visual Studio properties dialog for the reference):

```xml
<ItemGroup>
  <Reference Include="mydll">
    <HintPath>..\somepath\mydll.dll</HintPath>
    <!-- This indicates that the reference should not be copied to output folder. -->
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

### Not copying PackageReference

Detailed options description can be found in [Controlling package dependency assets](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets). Here we'll offer three artifical examples:

**Not copying package dependency to the immediate output folder:**

```xml
<ItemGroup>
  <PackageReference Include="newtonsoft.json" Version="13.0.1">
    <!-- This allows compiling against the dependency, but prevents it's copying to output folder or flow to downstream dependant projects. -->
    <IncludeAssets>compile</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

**Not copying package dependency to the downstream dependants output folder:**

```xml
<ItemGroup>
  <PackageReference Include="newtonsoft.json" Version="13.0.1">
    <!-- The dependency is copied to output folder in current referencing project, 
           but it's not copied to output folder of projects referencing current project. -->
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

**Not copying package dependency from the upstream dependencies:**

```xml
<ItemGroup>
  <ProjectReference Include="../somepath/MyProj.csproj">
    <!-- This prevents PackageReferences from MyProj.csproj to be copied to output of current project. -->
    <ExcludeAssets>all</ExcludeAssets>
  </ProjectReference>
</ItemGroup>
```

### Not copying ProjectReference

The opt-out mechanism is analogous to [Assembly Reference copy opt-out](#not-copying-assembly-reference):

```xml
<ItemGroup>
  <ProjectReference Include="../somepath/MyProj.csproj">
    <!-- This indicates that the referenced project output should not be copied to output folder. -->
    <Private>false</Private>
  </ProjectReference>
</ItemGroup>
```

Same metadata and logic applies here as it is being inherited from the `Reference` Item definition and the logic treats it identicaly. 

## ProjectReference without accessibility and copying to output

In a specific scenarios we might want to indicate that specific project should be built prior our project but said project should not be reference accessible nor its output copied to current project output. This can be helpful for build time only dependencies - projects defining behavior that is going to be used as build step of a current project.

Such a behavior can be achived with [`ReferenceOutputAssembly` metadata](https://learn.microsoft.com/visualstudio/msbuild/common-msbuild-project-items?view=vs-2022#projectreference):

```xml
<ItemGroup>
  <ProjectReference Include="../somepath/MyProj.csproj">
    <!-- This indicates that the referenced project should not be referenced in code and output should not be copied to output folder. 
         This way we basically only indicate the build order.
    -->
    <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
  </ProjectReference>
</ItemGroup>
```

**Note:** This technique has possibly unexpected behavior when referencing project with executable output type (`<OutputType>Exe</OutputType>`) - in such case the output assembly (`.dll`) is still not copied and referenced (as the metadatum name implies) and hence the types defined within the project cannot be referenced, however other supplementary output (added as `content` or `none`) is copied to the current project output folder (for .NET Core this includes `deps.json`, `runtimeconfig.json` and mainly `<app>.exe`). In that case we can combine (or replace) the `ReferenceOutputAssembly` metadata with `Private` metadata - [as described above](#not-copying-projectreference). More details on this case [here](https://github.com/dotnet/msbuild/issues/4795#issuecomment-1442390297)

## Forcing TargetFramework of a referenced multitargeted project

Consider agaoin our previous [Onion architecture example](#OnionArchSample), but now the individual projects will be [multitargeted](https://learn.microsoft.com/nuget/create-packages/multiple-target-frameworks-project-file). 

Repository Layer:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net48</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'net48'">
    <ProjectReference Include="..\Domain-net48\Domain-net48.csproj" />
    <PackageReference Include="System.Text.Json" Version="7.0.2" />
  </ItemGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
    <ProjectReference Include="..\Domain-netstd20\Domain-netstd20.csproj" />
    <PackageReference Include="newtonsoft.json" Version="13.0.1">
  </ItemGroup>
</Project>
```

And it's going to be referenced by Service Layer:


```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>net48;netstandard2.0</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Repository\Repository.csproj"  />
  </ItemGroup>
</Project>
```

Building the Service Layer will create output folders for `net7` and `net48`:

```
net48
 |---- Repository.dll (targeted for net48)
 |---- Domain-net48.dll
 |---- System.Text.Json.dll

net7
 |---- Repository.dll (targeted for netstandard2.0)
 |---- Domain-netstd20.dll
 |---- Newtonsoft.Json.dll 
```

Should we want to reference the netstandard version of the Repository Layer in our Service Layer - we can force the reference chain via `SetTargetFramework` metadata on `ProjectReference` item:

```xml
  <ItemGroup>
    <ProjectReference Include="..\Repository\Repository.csproj" SetTargetFramework="TargetFramework=netstandard2.0" />
  </ItemGroup>
```

**Notes:** 

`SetTargetFramework` is currently not honored by the NuGet client([nuget issue #12436](https://github.com/NuGet/Home/issues/12436)), so the output folder will contain binaries from nuget packages as if this metadata was not used. To workaround this the apropriate nuget needs to be directly referenced from the project enforcing reference framework via `SetTargetFramework`, or copied to output/publish folder via different means.


`SetTargetFramework` will properly enforce the framework for the `ProjectReference` chain. Once the `TargetFramework` overriding is encountered it is passed down the reference chain and the `ProjectReference`s respect it during the `TargetFramework` resolution. Due to the nature of handling of [transitive references in .NET-SDK style projects](#net-sdk-projects-and-access-to-transitive-references) and the fact that NuGet client doesn't honor `SetTargetFramework`, the transitive references can get resolved and built for multiple `TargetFramework`s. This means the output folder will contain proper version of the direct dependency - Repository Layer. The transitive references might overbuild, and output folder of current project (Service Layer) might contain both versions of the transitive project dependency (Domain-net48.dll and Domain-netstd20.dll). This limitation can be workarounded by switching of the transitive project references via `DisableTransitiveProjectReferences` (same as shown in [Access to transitive project references](#access-to-transitive-project-references))

