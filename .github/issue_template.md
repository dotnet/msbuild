### Steps to reproduce

Example repro steps:

- zip file with project file, directory contents, and script that invokes msbuild.

Project file
```xml
<Project>
  <ItemGroup>
    <I Include="**/*.cs"/>
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
If applicable, version of tool that invokes MSBuild (Visual Studio, dotnet CLI, etc):
