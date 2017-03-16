#Dependency Trimming

This package provides build infrastructure for trimming the output of an application.

It determines what is used by the application by examining static dependencies of the application binary as well as any directly referenced packages.  For any file that is unused it will be removed from the set of files copied to the output and publish folders and removed from the application's dependency file(`deps.json`) in the case of a .NET Core application.

Applications which rely on dynamic dependencies, for example using reflection or runtime compilation like ASP.NET MVC, can specify their dynamic dependencies by referencing packages that contain those dependencies or specifying dependent files as *[roots](#roots)*.

## How to use
First install the `Microsoft.Packaging.Tools` package in your application.

You must use *Visual Studio 2017* or later, or *.NET Core command-line (CLI) tools 1.0* or later.

### From the commandline
Specify `/p:TrimUnusedDependencies=true` when building the project with either `dotnet` or `msbuild`.

Examples:
```
dotnet build /p:TrimUnusedDependencies=true
dotnet publish /p:TrimUnusedDependencies=true
msbuild /p:TrimUnusedDependencies=true
msbuild /t:Publish /p:TrimUnusedDependencies=true
```

**Important:** Specify TrimUnusedDependencies for both *build* and *publish*, otherwise *build* will produce an application that is not trimmed and debugging will run against an untrimmed application that may hide any problems introduced by trimming, like missing dynamic dependencies.

### From the IDE or committing the change to your project

In your project (*.csproj* file) make the following change.

```xml
<PropertyGroup>
  <TrimUnusedDependencies>true</TrimUnusedDependencies>
</PropertyGroup>
```

### Additional options
`@(TrimFilesRootFiles)` -  Additional *root* files to consider part of the application.  See [roots](#roots).  
`@(TrimFilesRootPackages)` -  Additional *root* packages to consider part of the application.  See [roots](#roots).  
`@(TrimmableFiles)` - Files which should be trimmed from the application.  See [trimmable](#trimmable).  
`@(TrimmablePackages)` - Packages which should be trimmed from the application.  See [trimmable](#trimmable).  
`$(TrimFilesPreferNativeImages)` - Prefer a file with the `.ni.dll` extension over a file with the `.dll` extension.  `.ni.dll` files are native images and significantly larger than a managed assembly but will load faster since they don't need to be JIT compiled.  Default is `false`.
`$(RootPackageReference)` - Set to `false` to indicate that `PackageReferences` should not be considered as *[roots](roots)*.  Default is `true`.
`$(TreatMetaPackagesAsTrimmable)` - When set to `true` indicates that meta-packages (packages without any file assets) should be treated as *[trimmable](#trimmable)*.  Default is `true`.

**Examples:**
- Specify TrimFilesRootFiles to include file `System.IO.Pipes.dll`.

```xml
<ItemGroup>
  <TrimFilesRootFiles Include="System.IO.Pipes.dll" />
<ItemGroup>
```

- Specify TrimmablePackages to indicate that the `NuGet.Client` package should be considered trimmable and only the files in its closure that are actually used should be included.

```xml
<ItemGroup>
  <TrimmablePackages Include="NuGet.Client" />
<ItemGroup>
```

- Specify TrimFilesPreferNativeImages to prefer faster and larger native images if they exist.

```xml
<PropertyGroup>
  <TrimFilesPreferNativeImages>true</TrimFilesPreferNativeImages>
</PropertyGroup>
```

- Specify RootPackageReference to prefer avoid *rooting* packages directly reference by the project.

```xml
<PropertyGroup>
  <RootPackageReference>false</RootPackageReference>
</PropertyGroup>
```

## How it works
The trimming task examines all of the binaries and packages that make up your project and constructs a graph of the two that is related.  We start by identifying roots that are included in the application then we traverse the relationships between those to determine if other files or packages should be included in the app.

### Roots
By default the application is a *root*, as well as all `PackageReference`s from the project file.

The direct packages references may be excluded from the set of *roots* by specifying the property `RootPackageReference=false`.

Additional file *roots* may be specified using the `TrimFilesRootFiles` item.
Additional package *roots* may be specified using the `TrimFilesRootPackages` item.

### Trimmable
Files or packages may be treated as *trimmable*.  Essentially this means that when the file or package is encountered while examining dependencies, that file or package will not be included nor will its dependencies unless otherwise referenced.

If a file is *trimmable* this means that the file will not be included in the application.  This takes precedence over all other indirect or direct references, including *roots*.

If a package is *trimmable* this means that a package's files will not be included in the application unless those files are directly referenced by another file or as a root.

Additional *trimmable* files may be specified using the `TrimmableFiles` item.
Additional *trimmable* packages may be specified using the `TrimmablePackages` item.

### File relationships
Managed assemblies are related to other managed assemblies by assembly references in the compiled assembly.  Managed assemblies are related to native libraries by DllImports, P-Invokes, to those libraries.

#### Adding file relationships explicitly
Not all relationships can be discovered statically.  A file may define relationships to other files by placing a text file next to it, with the `.dependencies` extension and list other files that it depends on.

For example:
Suppose `foo.dll` depends on `somelibrary.dll` but that dependency is dynamic.  The developer of `foo.dll` can specify this dependency by placing a file foo.dll.dependencies next to foo.dll where the content of that file is a single line: `somelibrary.dll`.

### Package relationships
Packages are related to other packages by dependencies.  Files are related to packages if they are contained in a package.

Package relationships are established by the dependencies of a package.  In this way if a file (a.dll) has a dynamic dependency on another file (b.dll) which is contained in a package (B), that file may be included in a package (A) with a dependency on the other package (b).

## How to identify and fix missing dynamic dependencies
The best way to identify dynamic dependencies is to run your application with trimming enabled and without.  If it fails only with trimming enabled then the cause of the failure is likely trimming.

A missing assembly may cause the application to fail with an exception like the following:

```
Unhandled Exception: System.IO.FileNotFoundException: Could not load file or assembly
'AssemblyName, Culture=culture, PublicKeyToken=0123456789abcdef' or one of its dependencies.
The system cannot find the file specified.
```

To fix this you can *root* the assembly `'AssemblyName, Culture=culture, PublicKeyToken=0123456789abcdef'` by adding the following to your project file.

```xml
<ItemGroup>
  <TrimFilesRootFiles Include="AssemblyName.dll" />
<ItemGroup>
```

A missing native library may cause the application to fail with an exception like the following:

```
Unhandled Exception: System.DllNotFoundException: Unable to load DLL 'native.dll':
The specified module could not be found. (Exception from HRESULT: 0x8007007E)
```

To fix this you can *root* the native library `'native.dll'` by adding the following to your project file.

```xml
<ItemGroup>
  <TrimFilesRootFiles Include="native.dll" />
<ItemGroup>
```

**Note:** Just because you see these exceptions doesn't necessarily mean trimming is the root cause.  If you don't see the exception when running the application with trimming disabled then trimming is the likely cause.  If you see the exception when running the application with trimming disabled then the cause could be a missing pre-requisite or an undeclared dependency from some package.