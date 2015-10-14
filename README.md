# Microsoft.Build (MSBuild)
The Microsoft Build Engine is a platform for building applications. This engine, which is also known as MSBuild, provides an XML schema for a project file that controls how the build platform processes and builds software. Visual Studio uses MSBuild, but MSBuild *does not* depend on Visual Studio. By invoking msbuild.exe on your project or solution file, you can orchestrate and build products in environments where Visual Studio isn't installed.

For more information on MSBuild, see the [MSDN documentation](https://msdn.microsoft.com/en-us/library/dd393574(v=vs.120).aspx).

[![Build Status](http://dotnet-ci.cloudapp.net/job/microsoft_msbuild/badge/icon)](http://dotnet-ci.cloudapp.net/job/microsoft_msbuild/)

### Source code

* Clone the sources: `git clone https://github.com/Microsoft/msbuild.git`

### Building
For the full supported experience, you will need to have Visual Studio 2015. You can open the solution in Visual Studio 2013, but you will encounter issues building with the provided scripts.

To get started on **Visual Studio 2015**:

1. Set up a box with Visual Studio 2015. Either 
[install  Visual Studio 2015](http://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs), 
or grab a [prebuilt Azure VM image](http://blogs.msdn.com/b/visualstudioalm/archive/2014/06/04/visual-studio-14-ctp-now-available-in-the-virtual-machine-azure-gallery.aspx).
2. Clone the source code (see above).
3. Restore NuGet packages: `msbuild /t:BulkRestoreNugetPackages build.proj`
4. Open src/MSBuild.sln solution in Visual Studio 2015.
 
## How to Engage, Contribute and Provide Feedback
Before you contribute, please read through the contributing and developer guides to get an idea of what kinds of pull requests we will or won't accept.

* [Contributing Guide](https://github.com/Microsoft/msbuild/wiki/Contributing-Code)
* [Developer Guide](https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging)

Want to get more familiar with what's going on in the code?
* [Pull requests](https://github.com/Microsoft/msbuild/pulls): [Open](https://github.com/Microsoft/msbuild/pulls?q=is%3Aopen+is%3Apr)/[Closed](https://github.com/Microsoft/msbuild/pulls?q=is%3Apr+is%3Aclosed)
* [Issues](https://github.com/Microsoft/msbuild/issues)

You are also encouraged to start a discussion by filing an issue or creating a gist.

## MSBuild Components

* **MSBuild**. [Microsoft.Build.CommandLine](https://msdn.microsoft.com/en-us/library/dd393574(v=vs.120).aspx)  is the entrypoint for the Microsoft Build Engine (MSBuild.exe).

* **Microsoft.Build**. The [Microsoft.Build](https://msdn.microsoft.com/en-us/library/gg145008(v=vs.120).aspx) namespaces contain types that provide programmatic access to, and control of, the MSBuild engine.

* **Microsoft.Build.Framework**. The [Microsoft.Build.Framework](https://msdn.microsoft.com/en-us/library/microsoft.build.framework(v=vs.120).aspx) namespace contains the types that define how tasks and loggers interact with the MSBuild engine. For additional information on this component, see our [Microsoft.Build.Framework wiki page](https://github.com/Microsoft/msbuild/wiki/Microsoft.Build.Framework).

* **Microsoft.Build.Tasks**. The [Microsoft.Build.Tasks](https://msdn.microsoft.com/en-us/library/microsoft.build.tasks(v=vs.120).aspx) namespace contains the implementation of all tasks shipping with MSBuild.

* **Microsoft.Build.Utilities**. The [Microsoft.Build.Utilities](https://msdn.microsoft.com/en-us/library/microsoft.build.utilities(v=vs.120).aspx) namespace provides helper classes that you can use to create your own MSBuild loggers and tasks.

## License

MSBuild is licensed under the [MIT license](LICENSE).
