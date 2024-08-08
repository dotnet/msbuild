# `BuildCheck` reports codes and their meaning

Report codes are chosen to conform to suggested guidelines. Those guidelines are currently in revew: https://github.com/dotnet/msbuild/pull/10088

| Exit&nbsp;Code | Reason |
|:-----|----------|
| 0 | Success |
| [BC0101](#BC0101) | Shared output path. |
| [BC0102](#BC0102) | Double writes. |
| [BC0103](#BC0103) | Used environment variable. |


To enable verbose logging in order to troubleshoot issue(s), enable [binary logging](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Binary-Log.md#msbuild-binary-log-overview)

_Cmd:_
```cmd
dotnet build -bl -check
```

## <a name="BC0101"></a>BC0101 - Shared output path.

"Two projects should not share their OutputPath nor IntermediateOutputPath locations"

It is not recommended to share output path nor intermediate output path between multiple projects. Such practice can lead to silent overwrites of the outputs. Such overwrites will depend on the order of the build, that might not be guaranteed (if not explicitly configured) and hence it can cause nondeterministic behavior of the build.

If you want to produce outputs in a consolidated output folder - consider using the [Artifacts output layout](https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output) and/or [Microsoft.Build.Artifacts SDK](https://github.com/microsoft/MSBuildSdks/tree/main/src/Artifacts).


## <a name="BC0102"></a>BC0102 - Double writes.

"Two tasks should not write the same file"

This is a similar problem as ['BC0101 - Shared output path'](#BC0101) - however with higher granularity. It is not recomended that multiple tasks attempt to write to a single file - as such behavior might lead to nondeterminism of a build (as result can be dependent on the order of the tasks execution if those belong to independent projects) or/and to a lost updates.

If you want multiple tasks to update file in a one-by-one pipeline fashion, it is recommended to give each intermediate output a distinct name - preventing silent mixups if any of the tasks in the chain are skipped or removed.

## <a name="BC0103"></a>BC0103 - Used environment variable.

"Environment variables should not be used as a value source for the properties"

Using environment variables as a data source in MSBuild is problematic and can lead to nondeterministic builds.
Relying on environment variables introduces variability and unpredictability, as their values can change between builds or environments.

This practice can result in inconsistent build outcomes and makes debugging difficult, since environment variables are external to project files and build scripts. To ensure consistent and reproducible builds, avoid using environment variables. Instead, explicitly pass properties using the /p option, which offers better control and traceability.

<BR/>
<BR/>
<BR/>

### Related Resources
* [BuildCheck documentation](https://github.com/dotnet/msbuild/blob/main/documentation/specs/proposed/BuildCheck.md)
