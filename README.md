## Welcome to dotnet sdk

This repo contains core functionality needed to create .NET Core projects, that is shared between VisualStudio and CLI.

* MSBuild tasks can be found under [/src/Tasks/Microsoft.NET.Build.Tasks/](src/Tasks/Microsoft.NET.Build.Tasks).

Please refer to [dotnet/project-system](https://github.com/dotnet/project-system) for the project system work that is specific to Visual Studio.

## Build status

|Windows x64 |
|:------:|
|[![](https://dev.azure.com/dnceng/internal/_apis/build/status/dotnet/sdk/DotNet-Core-Sdk%203.0%20(Windows)%20(YAML)%20(Official))](https://dev.azure.com/dnceng/internal/_build?definitionId=140)|

## Testing a local build

To test your locally built SDK, run `eng\dogfood.cmd` after building. That script starts a new Powershell with the environment configured to redirect SDK resolution to your build.

From that shell your SDK will be available in:

- any Visual Studio instance launched (via `& devenv.exe`)
- `dotnet build`
- `msbuild`

## How do I engage and contribute?

We welcome you to try things out, [file issues](https://github.com/dotnet/sdk/issues), make feature requests and join us in design conversations.

This project has adopted a code of conduct adapted from the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. This code of conduct has been [adopted by many other projects](http://contributor-covenant.org/adopters/). For more information see [Contributors Code of conduct](https://github.com/dotnet/home/blob/master/guidance/be-nice.md).
