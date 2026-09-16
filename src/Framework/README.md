# Microsoft.Build.Framework

This package contains `Microsoft.Build.Framework.dll`, which defines [fundamental types](https://docs.microsoft.com/dotnet/api/microsoft.build.framework) used in MSBuild's API and extensibility model.

The items in this namespace are primarily base-level classes and interfaces shared across MSBuild's object model.  MSBuild task or extension developers can reference this package to implement interfaces such as
[`ITask`](https://docs.microsoft.com/dotnet/api/microsoft.build.framework.itask), and [`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.build.framework.ilogger).

### netstandard2.0 target
The `netstandard2.0` target of this build is configured only to output reference assemblies; at runtime MSBuild will be `net11.0` or `net472`. Please use the `net11.0`-targeted assemblies for .NET 11+ scenarios.

### TaskAnalyzer in Microsoft.Build.Framework
`Microsoft.Build.Framework` also includes the TaskAnalyzer under `analyzers/dotnet/cs` for projects that consume the package directly. This is the supported shipping path for the analyzer; no additional package reference is required.

The shipped analyzer defaults to the safe MT-only scope: regular task implementations do not receive new default warnings or errors. Migration mode remains available through the supported analyzer option:

```ini
# .globalconfig
is_global = true
global_level = 100

msbuild_task_analyzer.run_mt_analyzers_on_all_tasks = true
```

If a repository wants to suppress or tune a specific rule, set the corresponding Roslyn severity in `.editorconfig` or `.globalconfig`:

```ini
# Disable a rule for this repository.
dotnet_diagnostic.MSBuildTask0004.severity = none

# Or raise a rule to an error.
dotnet_diagnostic.MSBuildTask0001.severity = error
```

For more detail, see the TaskAnalyzer README in the repository or the bundled rule documentation in the shipping release notes.

For context, see https://github.com/dotnet/msbuild/pull/6148