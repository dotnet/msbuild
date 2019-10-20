Tool NuGet package format
-------------------------------------------

The most straightforward way to create a .NET tool package is to run `dotnet pack` with `PackAsTool` property set in the project file. However, if your build process is highly customized, `dotnet publish` may not create the right package for you. In this case, you can create a NuGet package for your tool using a *nuspec* file and explicitly placing assets into the NuGet package following these rules.

- The NuGet package has only _/tools_ folder under the root and does **not** contain any other folders; do not include folders like  _/lib_, _/content_, etc.
- Under _/tools_ folder, the subfolders must be structured with pattern _target framework short version/RID_. For example, tool assets targeting .NET core framework V2.1 that are portable across platforms should be in the folder _tools/netcoreapp2.1/any_.

Let's call assets under every _tools/target framework short version/RID_ combination "per TFM-RID assets" :
- There is a DotnetToolSettings.xml for every "per TFM-RID assets".
- The package type is DotnetTool.
- Each set of TFM-RID assets should have all the dependencies the tool requires to run. The TFM-RID assets should work correctly after being copied via `xcopy` to another machine, assuming that machine has the correct runtime version and RID environment.
- For portable app, there must be runtimeconfig.json for every "per TFM-RID assets".

# Remark:
- Currently, only portable apps are supported so the RID must be _any_.
- Only one tool per tool package.

DotnetToolSettings.xml:
Example:
```xml
<?xml version="1.0" encoding="utf-8" ?>
    <DotNetCliTool>
    <Commands>
        <Command Name="%sayhello%" EntryPoint="%console.dll%" Runner="dotnet" />
    </Commands>
</DotNetCliTool>
```
Currently only configurable part is command name: _sayhello_ and entry point: _console.dll_. Command Name is what the user will type in their shell to invoke the command. Entry point is the relative path to the entry dll with main.
