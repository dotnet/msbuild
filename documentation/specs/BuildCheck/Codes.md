# `BuildCheck` reports codes and their meaning

Report codes are chosen to conform to suggested guidelines. Those guidelines are currently in revew: https://github.com/dotnet/msbuild/pull/10088

| Diagnostic&nbsp;Code | Default Severity | Reason |
|:-----|-------|----------|
| [BC0101](#BC0101) | Warning | Shared output path. |
| [BC0102](#BC0102) | Warning | Double writes. |
| [BC0103](#BC0103) | Suggestion | Used environment variable. |
| [BC0201](#BC0201) | Warning | Usage of undefined property. |
| [BC0202](#BC0202) | Warning | Property first declared after it was used. |
| [BC0203](#BC0203) | None | Property declared but never used. |


To enable verbose logging in order to troubleshoot issue(s), enable [binary logging](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Binary-Log.md#msbuild-binary-log-overview)

_Cmd:_
```cmd
dotnet build -bl -analyze
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

## <a name="BC0201"></a>BC0201 - Usage of undefined property.

"A property that is accessed should be declared first."

This check indicates that a property was acessed without being declared (the declaration might have happen later - see [BC0202](#BC0202) for such checking). Only accessing in the configured scope (by default it's the project file only) are checked.

There are couple cases which are allowed by the check:

* Selfreferencing declaration is allowed - e.g.:
  `<ChainProp>$(ChainProp)</ChainProp>`

* Checking the property for emptyness - e.g.:
  `<PropertyGroup Condition="'$(PropertyThatMightNotBeDefined)' == ''">`

* Any usage of property in condition. This can be opted out vie the configuration `AllowUninitializedPropertiesInConditions` - e.g.:
  ```ini
  [*.csproj]
  build_check.BC0201.severity=error
  build_check.BC0201.AllowUninitializedPropertiesInConditions=false
  build_check.BC0202.AllowUninitializedPropertiesInConditions=false
  ```

  BC0201 and BC0202 must have same value for the optional switch - as both operate on top of same data and same filtering.

## <a name="BC0202"></a>BC0202 - Property first declared after it was used.

"A property should be declared before it is first used."

This check indicates that a property was acessed before it was declared. The default scope of this rule is the project file only. The scope captures the read and write operations as well. So this rule reports:
 * Uninitialized reads that happened anywhere during the build, while the uninitialized property was later defined within the scope of this check (e.g. project file).
 * Uninitialized reads that happened within the scope of check (e.g. project file), while later defined anywhere in the build

If `BC0202` and [BC0201](#BC0201) are both enabled - then `BC0201` reports only the undefined reads that are not reported by this rule (so those that do not have late definitions).

## <a name="BC0203"></a>BC0203 -  Property declared but never used.

"A property that is not used should not be declared."

This check indicates that a property was defined in the observed scope (by default it's the project file only) and it was then not used anywhere in the build.

This is a runtime check, not a static analysis check - so it can have false positives (as property not used in particular build might be needed in a build with different conditions). For this reasons it's currently only suggestion.

<BR/>
<BR/>
<BR/>

### Related Resources
* [BuildCheck documentation](https://github.com/dotnet/msbuild/blob/main/documentation/specs/proposed/BuildCheck.md)
