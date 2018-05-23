# Microsoft.Build (MSBuild)
The Microsoft Build Engine is a platform for building applications. This engine, which is also known as MSBuild, provides an XML schema for a project file that controls how the build platform processes and builds software. Visual Studio uses MSBuild, but MSBuild *does not* depend on Visual Studio. By invoking msbuild.exe on your project or solution file, you can orchestrate and build products in environments where Visual Studio isn't installed.

For more information on MSBuild, see the [MSBuild documentation](https://docs.microsoft.com/visualstudio/msbuild/msbuild) on docs.microsoft.com.

### Build Status

The current development branch is `master`. It builds for .NET Core and the full desktop .NET framework. Changes in `master` will go into the next "major" update of MSBuild.

| Runtime\OS | Windows | Ubuntu 14.04 | Ubuntu 16.04 |Mac OS X|
|:------|:------:|:------:|:------:|:------:|
| **Full Framework** |[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_Windows_NT_Full)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_Windows_NT_Full)| N/A | N/A | N/A |
|**.NET Core**|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_Windows_NT_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_Windows_NT_CoreCLR)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_Ubuntu14.04_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_Ubuntu14.04_CoreCLR)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_Ubuntu16.04_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_Ubuntu16.04_CoreCLR)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_OSX10.13_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_OSX10.13_CoreCLR)|
|**Mono**|returning soon|

We have entered stabilization for the 15.7 release (corresponding to Visual Studio 15.7) in the [`vs15.7`](https://github.com/Microsoft/msbuild/tree/vs15.7) branch.

| Runtime\OS | Windows | Ubuntu 14.04 | Ubuntu 16.04 |Mac OS X|
|:------|:------:|:------:|:------:|:------:|
| **Full Framework** |[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/vs15.7/innerloop_Windows_NT_Full)](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.7/job/innerloop_Windows_NT_Full)| N/A | N/A | N/A |
|**.NET Core**|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/vs15.7/innerloop_Windows_NT_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.7/job/innerloop_Windows_NT_CoreCLR)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/vs15.7/innerloop_Ubuntu14.04_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.7/job/innerloop_Ubuntu14.04_CoreCLR)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/vs15.7/innerloop_Ubuntu16.04_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.7/job/innerloop_Ubuntu16.04_CoreCLR)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/vs15.7/innerloop_OSX10.13_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.7/job/innerloop_OSX10.13_CoreCLR)|

### Source code

* Clone the sources: `git clone https://github.com/Microsoft/msbuild.git`

## Building

### Building MSBuild with Visual Studio 2017

For the full supported experience, you will need to have Visual Studio 2017.

To get started on **Visual Studio 2017**:

1. [Install Visual Studio 2017](https://www.visualstudio.com/vs/).  Select the following Workloads:
  - _.NET desktop development_
  - _.NET Core cross-platform development_
    - Optional, not strictly required (yet) but used to develop .NET Core applications.
2. Clone the source code (see above).
2. Open a `Developer Command Prompt for VS 2017` prompt.
3. Build the code using the `build.cmd` script. This also restores packages needed to open the projects in Visual Studio.
5. Open `MSBuild.sln` in Visual Studio 2017.

### Building MSBuild in Unix (Mac & Linux)

MSBuild can be run on Unix systems that support .NET Core. Set-up instructions can be viewed on the wiki:   [Building Testing and Debugging on .Net Core MSBuild](https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging-on-.Net-Core-MSBuild)

## Localization

You can turn on localized builds via the `/p:LocalizedBuild=true` command line argument. For more information on localized builds and how to make contributions to MSBuild's translations, see our [localization wiki](https://github.com/Microsoft/msbuild/wiki/Localization)

## How to Engage, Contribute and Provide Feedback

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

#### Getting Started

Before you contribute, please read through the contributing and developer guides to get an idea of what kinds of pull requests we will or won't accept.

* [Contributing Guide](https://github.com/Microsoft/msbuild/wiki/Contributing-Code)
* [Developer Guide](https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging)

Looking for something to work on? This list of [up for grabs issues](https://github.com/Microsoft/msbuild/issues?q=is%3Aopen+is%3Aissue+label%3Aup-for-grabs) is a great place to start.

You are also encouraged to start a discussion by filing an issue or creating a gist.

### MSBuild Components

* **MSBuild**. [Microsoft.Build.CommandLine](https://docs.microsoft.com/visualstudio/msbuild/msbuild)  is the entrypoint for the Microsoft Build Engine (MSBuild.exe).

* **Microsoft.Build**. The [Microsoft.Build](https://docs.microsoft.com/dotnet/api/?term=Microsoft.Build) namespaces contain types that provide programmatic access to, and control of, the MSBuild engine.

* **Microsoft.Build.Framework**. The [Microsoft.Build.Framework](https://docs.microsoft.com/dotnet/api/microsoft.build.framework) namespace contains the types that define how tasks and loggers interact with the MSBuild engine. For additional information on this component, see our [Microsoft.Build.Framework wiki page](https://github.com/Microsoft/msbuild/wiki/Microsoft.Build.Framework).

* **Microsoft.Build.Tasks**. The [Microsoft.Build.Tasks](https://docs.microsoft.com/dotnet/api/microsoft.build.tasks) namespace contains the implementation of all tasks shipping with MSBuild.

* **Microsoft.Build.Utilities**. The [Microsoft.Build.Utilities](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities) namespace provides helper classes that you can use to create your own MSBuild loggers and tasks.

### License

MSBuild is licensed under the [MIT license](LICENSE).
