Signing .NET Core Global Tool Packages
===============================

To create a signed package for your Dotnet Tool, you will need to create a signed shim. If a shim is found in the nupkg during `dotnet tool install`, it is used instead of creating one on consumer's machine.

To create a signed shim, you need to add the following extra property in you project file:

```
   <PackAsToolShimRuntimeIdentifiers>[list of RIDs]</PackAsToolShimRuntimeIdentifiers>
```

When this property is set, `dotnet pack` will generate a shim in the package (nupkg). Assuming all other other content is signed, after you sign the shim you can sign the nupkg.

Example:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.1</TargetFramework>
    <PackAsTool>true</PackAsTool>
    <PackAsToolShimRuntimeIdentifiers>win-x64;win-x86;osx-x64</PackAsToolShimRuntimeIdentifiers>
  </PropertyGroup>
</Project>
```

The result nupkg will have packaged shim included. Of course `dotnet pack` does not sign the shim or the package. The mechanism for that depends on your processes. The structure of the unzipped nupkg is:

```
│   shimexample.nuspec
│   [Content_Types].xml
│
├───package
│   └───services
│       └───metadata
│           └───core-properties
│                   9c20d06e1d8b4a4ba3e126f30013ef32.psmdcp
│
├───tools
│   └───netcoreapp2.1
│       └───any
│           │   DotnetToolSettings.xml
│           │   shimexample.deps.json
│           │   shimexample.dll
│           │   shimexample.pdb
│           │   shimexample.runtimeconfig.json
│           │
│           └───shims
│               ├───osx-x64
│               │       shimexample
│               │
│               ├───win-x64
│               │       shimexample.exe
│               │
│               └───win-x86
│                       shimexample.exe
│
└───_rels
        .rels
```
