# Controlling dependencies behavior

MSBuild recognizes [few types of dependencies](https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2015/msbuild/common-msbuild-project-items) (here we are mainly interested in `ProjectReference`, `PackageReference`, `Reference` aka assembly reference) and offers optional mechanisms to tailor some aspects of the dependencies workings - transitive dependencies resolution, multitargeted references resolution, copying dependencies to output directory.

## Access to transitive project references

In [SDK-style projects](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview) MSBuild by default makes all transitive `ProjectReference`s accessible as if they were direct dependencies.

This can lead to easy unintentional breaking out of layering architecture separation. 

This behavior can be opted-out via `DisableTransitiveProjectReferences` property on the referencing project.

<a name="OnionArchSample"></a>*Example*:

Let's imagine an `Onion Architecture` design:

```
 ---------------       ------------------       --------------
| Service Layer | --> | Repository Layer | --> | Domain Model |
 ---------------       ------------------       --------------
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

The transitive access to dependencies works by default for package dependencies as well. This can be opted out via `PrivateAssets=compile` on the `PackageReference` of the concern. (More details on [Controlling package dependency assets](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets))

*Example*:

In our previous example let's have `Repository Layer` reference `newtonsoft.json`:

```xml
<ItemGroup>
  <PackageReference Include="newtonsoft.json" Version="13.0.1">
    <!-- This prevents the reference to be available to referencing types. -->
		<PrivateAssets>compile</PrivateAssets>
	</PackageReference>
</ItemGroup>
```

Then our `Service Layer` would have access to `newtonsoft.json` (unless opted out via `PrivateAssets=compile`):

```csharp
namespace Service;
//This is allowed unless PrivateAssets=compile is set on the PackageDependency in Repository.
//using Newtonsoft.Json;
	
public class PersonsAccessor
{
	private Repository.Persona _persona;
}
```

## Not copying dependencies to output

By default the above mentioned dependency types are being copied to build output directory during the build (provided the target failed [up-to-date check](https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2015/msbuild/incremental-builds?view=vs-2015&redirectedfrom=MSDN#output-inference) and run). There can be various scenarios where this behavior is not desired (examples: dependency is compile time only or contains a logic for build; component is plugin to a main app and there is a desire not to duplicate common dependencies in output).

Overriding this logic depends on a type of dependency.

### Not copying Assembly Reference

Copying can be opted out via [Private metadata on the Reference item](https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2015/msbuild/common-msbuild-project-items?view=vs-2015#reference) (which corresponds to the `Copy Local` property of the reference in the Visual Studio properties dialog for the reference):

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

Detailed options description can be found in [Controlling package dependency assets](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets). Here we'll offer three artifical examples:

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

**Note:** There is possible need to explicitly specify `_GetChildProjectCopyToPublishDirectoryItems=false` to opt-out copying of project dependencies when builiding through [`MSBuilt` task](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-task) ([source](https://github.com/dotnet/msbuild/issues/4795#issuecomment-669885298))

## ProjectReference without accessibility and copying to output

In a specific scenarios we might want to indicate that specific project should be built prior our project but said project should not be reference accessible nor its output copied to current project output. This can be helpful for build time only dependencies - projects defining behavior that is going to be used as build step of a current project.

Such a behavior can be achived with [`ReferenceOutputAssembly` metadata](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items?view=vs-2022#projectreference):

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

**Note:** This technique doesn't fully work when referencing project with executable output type (`<OutputType>Exe</OutputType>`) - in such case the types defined within the project still cannot be referenced, however output is copied to the current project output folder. In that case we need to combine (or replace) the `ReferenceOutputAssembly` metadata with `Private` metadata - [as described above](#not-copying-projectreference).

## Forcing TargetFramework of a referenced multitargeted project

Consider agaoin our previous [Onion architecture example](#OnionArchSample), but now the individual projects will be [multitargeted](https://learn.microsoft.com/en-us/nuget/create-packages/multiple-target-frameworks-project-file). 

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

Would we want to reference the netstandard version of the Repository Layer in our Service Layer - we can force the reference chain via `SetTargetFramework` metadata on `ProjectReference` item:

```xml
  <ItemGroup>
    <ProjectReference Include="..\Repository\Repository.csproj" SetTargetFramework="TargetFramework=netstandard2.0" />
  </ItemGroup>
```

**Notes:** 

This will properly enforce the framework for the dependency chain. The output folder will contain proper version of the direct dependency - Repository Layer. The transitive dependencies might overbuild, and output folder of current project (Service Layer) might contain both versions of the transitive project dependency (Domain-net48.dll and Domain-netstd20.dll). This limitation can be workarounded by switching of the transitive project dependencies via `DisableTransitiveProjectReferences` (same as shown in [Access to transitive project references](#access-to-transitive-project-references))

`SetTargetFramework` is currently not honored by the nuget client([nuget issue #12436](https://github.com/NuGet/Home/issues/12436)), so the output folder will contain binaries from nuget packages as if this metadata was not used. To workaround this the apropriate nuget needs to be directly referenced from the project enforcing reference framework via `SetTargetFramework`, or copied to output/publish folder via different means.