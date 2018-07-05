# MSBuild.exe /pp
MSBuild preprocessor. Pass /pp to the command line to create a single huge XML project file with all project imports inlined in the correct order. This is useful to investigate the ordering of evaluation and execution.

Example:
```
msbuild MyProject.csproj /pp:inlined.proj
```

# MSBuild.exe /m
Parallel build. Many people still don't know that they can significantly speed up their builds by passing /m to MSBuild.exe.

# MSBuild.exe /nr:false
Disable node reuse (/nodeReuse:false). Don't leave MSBuild.exe processes hanging around locking files after the build completes. See more details in MSBuild command line help (/?). See also `MSBUILDDISABLENODEREUSE=1` below.

# EnvironmentVariables
 * `MSBUILDTARGETOUTPUTLOGGING=1` - set this to enable [printing all target outputs to the log](https://blogs.msdn.microsoft.com/msbuild/2010/03/31/displaying-target-output-items-using-the-console-logger).
 * `MSBUILDLOGTASKINPUTS=1` - log task inputs (not needed if there are any diagnostic loggers already).
 * `MSBUILDEMITSOLUTION=1` - save the generated .proj file for the .sln that is used to build the solution.
 * `MSBUILDENABLEALLPROPERTYFUNCTIONS=1` - enable [additional property functions](https://blogs.msdn.microsoft.com/visualstudio/2010/04/02/msbuild-property-functions).
 * `MSBUILDLOGVERBOSERARSEARCHRESULTS=1` - in ResolveAssemblyReference task, log verbose search results.
 * `MSBUILDLOGCODETASKFACTORYOUTPUT=1` - dump generated code for task to a <GUID>.txt file in the TEMP directory
 * `MSBUILDDISABLENODEREUSE=1` - set this to not leave MSBuild processes behind (see /nr:false above, but the environment variable is useful to also set this for Visual Studio for example).
 * `MSBUILDLOGASYNC=1` - enable asynchronous logging.

# TreatAsLocalProperty
If MSBuild.exe is passed properties on the command line, such as `/p:Platform=AnyCPU` then this value overrides whatever assignments you have to that property inside property groups. For instance, `<Platform>x86</Platform>` will be ignored. To make sure your local assignment to properties overrides whatever they pass on the command line, add the following at the top of your MSBuild project file:

```
<Project TreatAsLocalProperty="Platform" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
```

This will make sure that your local assignments to the `Platform` property are respected. You can specify multiple properties in `TreatAsLocalProperty` separated by semicolon.

# Visual Studio Background Builds
Set the `TRACEDESIGNTIME=true` environment variable to output design-time build logs to TEMP: read more here: https://blogs.msdn.microsoft.com/jeremykuhne/2016/06/06/vs-background-builds

# Visual Studio Design-time (IntelliSense) builds

Use this command-line to approximate what the design-time build does:

```
/t:CollectResolvedSDKReferencesDesignTime;DebugSymbolsProjectOutputGroup;CollectPackageReferences;ResolveComReferencesDesignTime;ContentFilesProjectOutputGroup;DocumentationProjectOutputGroupDependencies;SGenFilesOutputGroup;ResolveProjectReferencesDesignTime;SourceFilesProjectOutputGroup;DebugSymbolsProjectOutputGroupDependencies;SatelliteDllsProjectOutputGroup;BuiltProjectOutputGroup;SGenFilesOutputGroupDependencies;ResolveAssemblyReferencesDesignTime;CollectAnalyzersDesignTime;CollectSDKReferencesDesignTime;DocumentationProjectOutputGroup;PriFilesOutputGroup;BuiltProjectOutputGroupDependencies;ResolvePackageDependenciesDesignTime;SatelliteDllsProjectOutputGroupDependencies;SDKRedistOutputGroup;CompileDesignTime /p:SkipCompilerExecution=true /p:ProvideCommandLineArgs=true /p:BuildingInsideVisualStudio=true /p:DesignTimeBuild=true
```

# Extend all builds (at system-wide level)
See https://www.simple-talk.com/dotnet/.net-tools/extending-msbuild, "Extending all builds" section. Also read about [MSBuildUserExtensionsPath](http://referencesource.microsoft.com/#MSBuildFiles/C/ProgramFiles(x86)/MSBuild/14.0/Microsoft.Common.props,33), [CustomBeforeMicrosoftCommonProps](http://referencesource.microsoft.com/#MSBuildFiles/C/ProgramFiles(x86)/MSBuild/14.0/Microsoft.Common.props,68), [CustomBeforeMicrosoftCommonTargets](http://referencesource.microsoft.com/#MSBuildFiles/C/ProgramFiles(x86)/MSBuild/14.0/bin_/amd64/Microsoft.Common.targets,71), and CustomAfterMicrosoftCommonProps/CustomAfterMicrosoftCommonTargets.

Example:
Create this file (Custom.props) in `C:\Users\username\AppData\Local\Microsoft\MSBuild\14.0\Microsoft.Common.targets\ImportAfter`:

```
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <MyCustomProperty>Value!</MyCustomProperty>
  </PropertyGroup>
</Project>
```

then build any project. It will have MyCustomProperty set to Value!

# Diagnose WPF temporary assembly compilation issues

Set the property `GenerateTemporaryTargetAssemblyDebuggingInformation` on the `GenerateTemporaryTargetAssembly` task:
https://referencesource.microsoft.com/#PresentationBuildTasks/BuildTasks/Microsoft/Build/Tasks/Windows/GenerateTemporaryTargetAssembly.cs,4571677f19ba0d24,references

If the property `$(GenerateTemporaryTargetAssemblyDebuggingInformation)` is set, the temporary project generated during XAML project build will not be deleted and will be available for inspection. This is only available in the recent versions of .NET Framework, so check if your `Microsoft.WinFX.targets` file has it.

Also the name of the project was renamed from `*.tmp_proj` to `*_wpftmp.csproj` so the file extension is now C#: `WpfApp1_jzmidb3d_wpftmp.csproj`