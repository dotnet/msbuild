# MSBuild Test Target and Task

See: [MSBuild Test Target](https://github.com/dotnet/msbuild/pull/9193)

## Motivation

The primary motivation of the MSBuild Test Target is to offer a convienent and standardardized way for executing tests within the msbuild environment. This is inspired by the simplicity of the `dotnet test` command. The proposed command for initiating test within MSBuild would be `msbuild /t:Test`

Another significatnt benefit of integrating this target is to faciliatet the caching of test executions, using MSBuild project caching capabilities. This enhancement will optimize the testing process by reducing test runs which could significantly reduce time spent building and testing, as tests would only execute, (after the initial run) if there are changes to those tests. As an example running with [MSBuildCache](https://github.com/microsoft/MSBuildCache) we can cache both build and test executions. Functionally, this means skipping test executions that have been determined to have not changed.
Example usage:
`msbuild /graph /restore:false /m /nr:false /reportfileaccesses /t:"Build;Test"`

## Design Overview

The 'Microsoft.Common.Test.targets' file contains a stub test target.

```xml
<Project>
    <Target Name="Test"></Target>
</Project>
```

This target serves a placeholder and entry point for test target implementations.

### Conditional Import

- This stub target is conditionally imported, determined by a condition named
`$(UseMSBuildTestInfrastructure)`.
- This condition allows for users to opt-in to this test target, which helps to prevent breaking changes, with respect the the target name, since there are likely 'Test' targets that exist in the wild already.

The 'Microsoft.Common.CurrentVersion.targets' file contains.

```xml
  <PropertyGroup>
    <UseMSBuildTestInfrastructure Condition="'$(UseMSBuildTestInfrastructure)' == ''">false</UseMSBuildTestInfrastructure>
  </PropertyGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.Common.Test.targets" Condition="'$(UseMSBuildTestInfrastructure)' == 'true'"/>

```

### Extensibility for Test Runners

- Test runner implemenations can hook into the provided stub using the `AfterTargets` property.
- This approach enables different test runners to extend the basic funcionarlity of the test target.

For instance, an implementation for running VSTest would look like:

```xml
<Target Name="RunVSTest" AfterTargets="Test">
  <!-- Implementation details here -->
</Target>
```

### Usage Scenario

- Users who wish to utilize this target will set the `$(UseMSBuildTestInfrastructure)` condition in their project file, rsp or via the command line.
- By executing `msbuild /t:Test`, the MSBuild engine will envoke the `Test` taget, which in turn triggers any test runner targets defined to run after it.

## Default Task Implementation

See: [MSBuild Test Task](https://github.com/microsoft/MSBuildSdks/pull/473)

### Nuget package for default implementaion

- The default implementation will be provided through a nuget package.
- This package will contain an MSBuild Task deigned to execute `vstest.console.exe`.

### MSBuild Task Functionality

- The core of this implemenation is an MSBuild task that interfaces with `vstest.console.exe`.
- This task will accept arguments as properties and pass them directly into the command line test runner.

### Using The Default Implementation

- Users would install the provided Nuget Package to incorporate it into their projects.
- Add the package to their GlobalPackageReferences or specific projects.
- Once integrated, executing `msbuild /t:Test` would trigger the MSBuild Task, ultimately executing `vstest.console.exe`.
