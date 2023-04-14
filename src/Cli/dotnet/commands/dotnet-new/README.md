## `dotnet new`

This is a home for `dotnet new` command.

The issues for `dotnet new` be opened in dotnet/sdk repo with label [`area: dotnet new`](https://github.com/dotnet/sdk/labels/area%3A%20dotnet%20new).

To contribute or debug, follow [`dotnet/sdk` guideline](https://github.com/dotnet/sdk#how-do-i-engage-and-contribute).

Main `muscles` of `dotnet new` are implemented in [`Microsoft.TemplateEngine.Cli`](https://github.com/dotnet/sdk/tree/main/src/Cli/Microsoft.TemplateEngine.Cli).
`dotnet` mainly contains the functionality that needs other `dotnet` tools, as:
- MSBuild evaluation and related components ([project capability constraint](https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/commands/dotnet-new/MSBuildEvaluation/ProjectCapabilityConstraint.cs), [project context symbol source](https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/commands/dotnet-new/MSBuildEvaluation/ProjectContextSymbolSource.cs))
- [post actions](https://github.com/dotnet/sdk/tree/main/src/Cli/dotnet/commands/dotnet-new/PostActions) running other `dotnet` commands 
- providers for the template packages built-in to SDK, other SDK information and optional workloads 

Consider adding unit tests and/or integration tests when contributing.
The unit tests are located in:
- [`dotnet` unit tests](https://github.com/dotnet/sdk/tree/main/src/Tests/dotnet.Tests/dotnet-new)
- [`Microsoft.TemplateEngine.Cli`](https://github.com/dotnet/sdk/tree/main/src/Tests/Microsoft.TemplateEngine.Cli.UnitTests)

The integration tests are located [here](https://github.com/dotnet/sdk/tree/main/src/Tests/dotnet-new.Tests).
Please follow existing tests to see how to run `dotnet new` under different conditions.

Assets for unit and integration tests are defined [here](https://github.com/dotnet/sdk/tree/main/src/Assets/TestPackages/dotnet-new).

To work with `dotnet new`, you may also use [solution filter](https://github.com/dotnet/sdk/blob/main/TemplateEngine.slnf).