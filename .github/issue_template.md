### Steps to reproduce

Example repro steps:

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

Command line
```
msbuild /bl
```
### Expected  behavior


### Actual behavior


### Environment data
`msbuild /version` output:
If applicable, version of tool that invokes MSBuild (Visual Studio, dotnet CLI, etc):
