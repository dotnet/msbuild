### Steps to reproduce

Either include a project sample, attach a zipped project, or provide IDE / CLI steps to create the project and repro the behaviour. Example of a project sample:

Project file
```xml
<Project>
  <PropertyGroup>
    <Extension>cs</Extension>
  </PropertyGroup>
  
  <ItemGroup>
    <I Include="**/*.$(Extension)"/>
  </ItemGroup>
  
  <Target Name="Build">
    <Message Text="I: %(I.Identity)"/>
  </Target>
</Project>
```

Directory contents:
```
/
- a.cs
- b.cs
- dir/
     - c.cs
```

Command line
```
msbuild /bl
```
### Expected  behavior


### Actual behavior


### Environment data
`msbuild /version` output:

OS info:

If applicable, version of the tool that invokes MSBuild (Visual Studio, dotnet CLI, etc):
