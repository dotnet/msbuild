# MSBuild Command-Line Switches
See the [MSBuild Command-Line Reference](https://learn.microsoft.com/visualstudio/msbuild/msbuild-command-line-reference) for more information on switches.
 * `MSBuild.exe -pp:<FILE>`
   * MSBuild preprocessor. Pass /pp to the command line to create a single huge XML project file with all project imports inlined in the correct order. This is useful to investigate the ordering of imports and property and target overrides during evaluation.
   * Example usage: `msbuild MyProject.csproj /pp:inlined.xml`
 * `MSBuild.exe -nr:false`
   * Disable node reuse (`/nodeReuse:false`). Don't leave MSBuild.exe processes hanging around (and possibly locking files) after the build completes. See more details in MSBuild command line help (/?). See also `MSBUILDDISABLENODEREUSE=1` below. Note that using this when building repeatedly will cause slower builds.
 * `MSBuild.exe -bl`
   * Records all build events to a structured binary log file. The [MSBuildStructuredLog](https://github.com/KirillOsenkov/MSBuildStructuredLog) tool can be used to analyze this file.
 * `MSBuild.exe -noconlog`
   * Used to suppress the usage of the console logger, which is otherwise always attached.
 * `MSBuild.exe -flp:v=diag`
   * Passes parameters to the file logger. If you want to attach multiple file loggers, you do so by specifying additional parameters in the switches /flp1, /flp2, /flp3, and so on.

# Environment Variables

 * `MSBuildDebugEngine=1` & `MSBUILDDEBUGPATH=<DIRECTORY>`
  * Set this to cause any MSBuild invocation launched within this environment to emit binary logs and additional debugging information to `<DIRECTORY>`. Useful when debugging build or evaluation issues when you can't directly influence the MSBuild invocation, such as in Visual Studio.
 * `MSBUILDTARGETOUTPUTLOGGING=1`
   * Set this to enable [printing all target outputs to the log](https://learn.microsoft.com/archive/blogs/msbuild/displaying-target-output-items-using-the-console-logger).
 * `MSBUILDLOGTASKINPUTS=1`
   * Log task inputs (not needed if there are any diagnostic loggers already).
 * `MSBUILDEMITSOLUTION=1`
   * Save the generated .proj file for the .sln that is used to build the solution.
 * `MSBUILDENABLEALLPROPERTYFUNCTIONS=1`
   * Enable [additional property functions](https://devblogs.microsoft.com/visualstudio/msbuild-property-functions/).
 * `MSBUILDLOGVERBOSERARSEARCHRESULTS=1`
   * In ResolveAssemblyReference task, log verbose search results.
 * `MSBUILDLOGCODETASKFACTORYOUTPUT=1`
   * Dump generated code for task to a <GUID>.txt file in the TEMP directory
 * `MSBUILDDISABLENODEREUSE=1`
   * Set this to not leave MSBuild processes behind (see `/nr:false` above, but the environment variable is useful to also set this for Visual Studio for example).
 * `MSBUILDLOGASYNC=1`
   * Enable asynchronous logging.
 * `MSBUILDDEBUGONSTART=1`
   * Launch debugger on build start.
   * Setting the value of 2 allows for manually attaching a debugger to a process ID.
 * `MSBUILDDEBUGSCHEDULER=1` & `MSBUILDDEBUGPATH=<DIRECTORY>`
   * Dumps scheduler state at specified directory (`MSBUILDDEBUGSCHEDULER` is implied by `MSBuildDebugEngine`).

# TreatAsLocalProperty
If MSBuild.exe is passed properties on the command line, such as `/p:Platform=AnyCPU` then this value overrides whatever assignments you have to that property inside property groups. For instance, `<Platform>x86</Platform>` will be ignored. To make sure your local assignment to properties overrides whatever they pass on the command line, add the following at the top of your MSBuild project file:

```
<Project TreatAsLocalProperty="Platform" DefaultTargets="Build">
```

This will make sure that your local assignments to the `Platform` property are respected. You can specify multiple properties in `TreatAsLocalProperty` separated by semicolon.

# Visual Studio Background Builds
Set the `TRACEDESIGNTIME=true` environment variable to output design-time build logs to TEMP: read more here: https://learn.microsoft.com/archive/blogs/jeremykuhne/vs-background-builds

# Visual Studio Design-time (IntelliSense) builds

Use this command-line to approximate what the design-time build does:

```
/t:CollectResolvedSDKReferencesDesignTime;DebugSymbolsProjectOutputGroup;CollectPackageReferences;ResolveComReferencesDesignTime;ContentFilesProjectOutputGroup;DocumentationProjectOutputGroupDependencies;SGenFilesOutputGroup;ResolveProjectReferencesDesignTime;SourceFilesProjectOutputGroup;DebugSymbolsProjectOutputGroupDependencies;SatelliteDllsProjectOutputGroup;BuiltProjectOutputGroup;SGenFilesOutputGroupDependencies;ResolveAssemblyReferencesDesignTime;CollectAnalyzersDesignTime;CollectSDKReferencesDesignTime;DocumentationProjectOutputGroup;PriFilesOutputGroup;BuiltProjectOutputGroupDependencies;ResolvePackageDependenciesDesignTime;SatelliteDllsProjectOutputGroupDependencies;SDKRedistOutputGroup;CompileDesignTime /p:SkipCompilerExecution=true /p:ProvideCommandLineArgs=true /p:BuildingInsideVisualStudio=true /p:DesignTimeBuild=true
```

# Extend all builds (at system-wide level)
See https://www.simple-talk.com/dotnet/.net-tools/extending-msbuild, "Extending all builds" section. Also read about [MSBuildUserExtensionsPath](https://referencesource.microsoft.com/#MSBuildFiles/C/ProgramFiles(x86)/MSBuild/14.0/Microsoft.Common.props,33), [CustomBeforeMicrosoftCommonProps](https://referencesource.microsoft.com/#MSBuildFiles/C/ProgramFiles(x86)/MSBuild/14.0/Microsoft.Common.props,68), [CustomBeforeMicrosoftCommonTargets](https://referencesource.microsoft.com/#MSBuildFiles/C/ProgramFiles(x86)/MSBuild/14.0/bin_/amd64/Microsoft.Common.targets,71), and CustomAfterMicrosoftCommonProps/CustomAfterMicrosoftCommonTargets.

Example:
Create this file (Custom.props) in `C:\Users\username\AppData\Local\Microsoft\MSBuild\Current\Microsoft.Common.targets\ImportAfter`:

```
<?xml version="1.0" encoding="utf-8"?>
<Project>
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
