[Deploy-MSBuild](https://github.com/Forgind/msbuild/blob/deploy-msbuild/scripts/Deploy-MSBuild.ps1) is a way to conveniently take private bits and install them into Visual Studio (VS) for testing. To use it:
- Build MSBuild with the changes you want using `build.cmd /p:CreateBootstrap=true`.
- In an administrator powershell window, navigate to the msbuild folder.
- Run `scripts\Deploy-MSBuild.ps1`.
  - Specify the Bin folder with MSBuild in your VS install as the location when prompted. This is somewhere like `C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin`.

The Deploy-MSBuild script creates backups of the relevant MSBuild binaries, then copies the new binaries in their place. If you cannot build or cannot deploy MSBuild on the same machine on which you wish to use the updated version of VS, build and deploy to an empty folder instead. Then, manually make a backup of the files in that folder and overwrite them in the VS install of choice.
