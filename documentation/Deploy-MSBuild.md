# Deploying Just-Built MSBuild

## Visual Studio

[Deploy-MSBuild](https://github.com/dotnet/msbuild/blob/deploy-msbuild/scripts/Deploy-MSBuild.ps1) is a way to conveniently take private bits and install them into Visual Studio (VS) for testing. To use it:

- If you haven't already, clone [MSBuild](https://github.com/dotnet/msbuild) and make the changes you want.
- Build MSBuild with the changes you want using `build.cmd /p:CreateBootstrap=true`.
- In an administrator powershell window, navigate to the msbuild folder.
- Run `scripts\Deploy-MSBuild.ps1 -destination {destination} -configuration {configuration}`.
  - Specify the Bin folder of MSBuild in your VS install as the destination. This is somewhere like `"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin"`.
  - Make sure the `{configuration}` you pass to the deploy script matches the one you gave to `build.cmd` (this is `Debug` by default).

The Deploy-MSBuild script creates backups of the relevant MSBuild binaries, then copies the new binaries in their place.

⚠ CAUTION: If you overwrite the MSBuild in Visual Studio you can break Visual Studio. That in turn can prevent you from building MSBuild to fix your bug! The deploy script makes backups by default which you may need to manually copy back over.

### Crossing machines

If you cannot build or cannot deploy MSBuild on the same machine on which you wish to use the updated version of VS, build and deploy to an empty folder instead. Then, manually make a backup of the files in that folder and overwrite them in the VS install of choice.

## .NET (Core) SDK

Deploy-MSBuild can also patch a .NET (Core) SDK installation. Pass the `-runtime Core` argument to `Deploy-MSBuild.ps1` to ensure that it selects .NET Core MSBuild.
