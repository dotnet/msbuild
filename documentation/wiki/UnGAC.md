# MSBuild, the Global Assembly Cache (GAC), and You

## What is the GAC?

See the [public documentation](https://docs.microsoft.com/dotnet/framework/app-domains/gac). The GAC is a folder where different installations of VS on the same machine look for assemblies that are commonly used. If an assembly is in the GAC, it will be prioritized over any other assembly.

The only MSBuild assemblies you may see in the GAC are version 4.8. There is no reason any modern (15.1+) MSBuild assembly should be in the GAC today.

## What MSBuild Assemblies are installed on my Machine?
Run the [EnumerateMSBuild powershell script](https://github.com/Microsoft/msbuild/blob/master/scripts/EnumerateMSBuild.ps1) from our repo. It will output a `msbuild_versions.txt` file that lists MSBuild assemblies in their common folders along with their versions.

## How to Remove MSBuild Assemblies from the GAC
1. If on Visual Studio 16.8 or higher, repair your installation.
2. Run these commands on a developer command prompt with admin privileges. Match the versions as necessary.
    ```
    gacutil /u "MSBuild, Version=15.1.0.0"
    gacutil /u "Microsoft.Build.Conversion.Core, Version=15.1.0.0"
    gacutil /u "Microsoft.Build, Version=15.1.0.0"
    gacutil /u "Microsoft.Build.Engine, Version=15.1.0.0"
    gacutil /u "Microsoft.Build.Tasks.Core, Version=15.1.0.0"
    gacutil /u "Microsoft.Build.Utilities.Core, Version=15.1.0.0"
    gacutil /u "Microsoft.Build.Framework, Version=15.1.0.0"
    ```
3. If you want to do this 'safely', move the folder out of the GAC and return it if it doesn't resolve the issue.

## Related GAC issues
https://developercommunity.visualstudio.com/content/problem/311136/update-to-1580-projects-wont-load.html

https://developercommunity.visualstudio.com/content/problem/953754/after-update-to-1650-no-longer-able-to-loadcreate.html

https://developercommunity.visualstudio.com/content/problem/208631/unable-to-create-or-open-solution.html

## Other assemblies
We have an issue [tracking other assemblies](https://github.com/dotnet/msbuild/issues/5955) that can cause problems when in the GAC. If one of these assemblies is causing issues for you, refer to [How to Remove MSBuild Assemblies from the GAC](#how-to-remove-msbuild-assemblies-from-the-gac) while changing the assembly name and version.
