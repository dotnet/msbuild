## Welcome to dotnet sdk

This repo contains core functionality needed to create .NET projects that is shared between VisualStudio and CLI.

* MSBuild tasks can be found under [/src/Tasks/Microsoft.NET.Build.Tasks/](src/Tasks/Microsoft.NET.Build.Tasks).

Please refer to [dotnet/project-system](https://github.com/dotnet/project-system) for the project system work that is specific to Visual Studio.

## Build status

|Windows x64 |
|:------:|
|[![](https://dev.azure.com/dnceng/internal/_apis/build/status/dotnet/sdk/DotNet-Core-Sdk%203.0%20(Windows)%20(YAML)%20(Official))](https://dev.azure.com/dnceng/internal/_build?definitionId=140)|

## Installing the SDK
[Official builds](https://dotnet.microsoft.com/download/dotnet-core)

[Latest builds](https://github.com/dotnet/installer#installers-and-binaries)

## How do I engage and contribute?

We welcome you to try things out, [file issues](https://github.com/dotnet/sdk/issues), make feature requests and join us in design conversations. Also be sure to check out our [project documentation](documentation)

This project has adopted the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct) to clarify expected behavior in our community.

## How do I build the SDK?

Start with the [Developer Guide](documentation/project-docs/developer-guide.md).

## How do I test an SDK I have built?

To test your locally built SDK, run `eng\dogfood.cmd` after building. That script starts a new Powershell with the environment configured to redirect SDK resolution to your build.

From that shell your SDK will be available in:

- any Visual Studio instance launched (via `& devenv.exe`)
- `dotnet build`
- `msbuild`


## How we triage and review PRs

With the SDK repo being the home for many different areas, we've started trying to label incoming issues for the area they are related to using Area- labels.  Then we rely on the [codeowners](https://github.com/dotnet/sdk/blob/main/CODEOWNERS) to manage and triages issues in their areas.  Feel free to ping the owners listed in that file if you're not getting traction on a particular issue or PR. Please try to label new issues as that'll help us route them faster.

For PRs, we assign out a reviewer once a week on Wednesday looking only at PRs that are green in the build.  If you are contributing, please get the PR green including a test if possible and then ping @dotnet-cli if you want to raise visibility of the PR.
