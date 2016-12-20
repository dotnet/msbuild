# Microsoft.Build (MSBuild)
The Microsoft Build Engine is a platform for building applications. This engine, which is also known as MSBuild, provides an XML schema for a project file that controls how the build platform processes and builds software. Visual Studio uses MSBuild, but MSBuild *does not* depend on Visual Studio. By invoking msbuild.exe on your project or solution file, you can orchestrate and build products in environments where Visual Studio isn't installed.

For more information on MSBuild, see the [MSDN documentation](https://msdn.microsoft.com/en-us/library/dd393574%28v=vs.140%29.aspx).

### Build Status

The current development branch is `xplat`. It builds for .NET Core and the full desktop .NET framework.

| Runtime\OS | Windows | Ubuntu 14.04 | Ubuntu 16.04 |Mac OS X|
|:------|:------:|:------:|:------:|:------:|
| **Full Framework** |[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_xplat_Windows_NT_Desktop)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_xplat_Windows_NT_Desktop)| N/A | N/A | N/A |
|**.NET Core**|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_xplat_Windows_NT_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_xplat_Windows_NT_CoreCLR)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_xplat_Ubuntu14.04_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_xplat_Ubuntu14.04_CoreCLR)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_xplat_Ubuntu16.04_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_xplat_Ubuntu16.04_CoreCLR)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_xplat_OSX_CoreCLR)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_xplat_OSX_CoreCLR)|
|**Mono**|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_xplat_Windows_NT_Mono)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_xplat_Windows_NT_Mono)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_xplat_Ubuntu14.04_Mono)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_xplat_Ubuntu14.04_Mono)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_xplat_Ubuntu16.04_Mono)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_xplat_Ubuntu16.04_Mono)|[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_xplat_OSX_Mono)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_xplat_OSX_Mono)|

Full-framework-only build from `master` (deprecated):
[![Build Status](https://ci2.dot.net/buildStatus/icon?job=Microsoft_msbuild/master/innerloop_master_Windows_NT_Desktop)](https://ci2.dot.net/job/Microsoft_msbuild/job/master/job/innerloop_master_Windows_NT_Desktop)


[![Join the chat at https://gitter.im/Microsoft/msbuild](https://badges.gitter.im/Microsoft/msbuild.svg)](https://gitter.im/Microsoft/msbuild?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

### Source code

* Clone the sources: `git clone https://github.com/Microsoft/msbuild.git`

## Building
### Building MSBuild with VS 2015
For the full supported experience, you will need to have Visual Studio 2015. You can open the solution in Visual Studio 2017 RC, but you will encounter issues building with the provided scripts.

To get started on **Visual Studio 2015**:

1. [Install Visual Studio 2015](http://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs).  Select the following optional components:
  - _Microsoft Web Developer Tools_
  - _Universal Windows App Development Tools_
    - _Tools and Windows SDK 10.0.10240_
2. Clone the source code (see above).
3. Build the code using the `cibuild.cmd` script.
5. Open src/MSBuild.sln solution in Visual Studio 2015.

### Building MSBuild in Unix (Mac & Linux)
MSBuild's xplat branch allows MSBuild to be run on Unix Systems. Set-up instructions can be viewed on the wiki:   [Building Testing and Debugging on .Net Core MSBuild](https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging-on-.Net-Core-MSBuild)

## Localization
You can turn on localized builds via the `/p:LocalizedBuild=true` command line argument. For more information on localized builds and how to make contributions to MSBuild's translations, see our [localization wiki](https://github.com/Microsoft/msbuild/wiki/Localization)

### How to Engage, Contribute and Provide Feedback
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

#### Getting Started
Before you contribute, please read through the contributing and developer guides to get an idea of what kinds of pull requests we will or won't accept.

* [Contributing Guide](https://github.com/Microsoft/msbuild/wiki/Contributing-Code)
* [Developer Guide](https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging)

Looking for something to work on? This list of [up for grabs issues](https://github.com/Microsoft/msbuild/issues?q=is%3Aopen+is%3Aissue+label%3Aup-for-grabs) is a great place to start.

You are also encouraged to start a discussion by filing an issue or creating a gist.

### MSBuild Components

* **MSBuild**. [Microsoft.Build.CommandLine](https://msdn.microsoft.com/en-us/library/dd393574(v=vs.120).aspx)  is the entrypoint for the Microsoft Build Engine (MSBuild.exe).

* **Microsoft.Build**. The [Microsoft.Build](https://msdn.microsoft.com/en-us/library/gg145008(v=vs.120).aspx) namespaces contain types that provide programmatic access to, and control of, the MSBuild engine.

* **Microsoft.Build.Framework**. The [Microsoft.Build.Framework](https://msdn.microsoft.com/en-us/library/microsoft.build.framework(v=vs.120).aspx) namespace contains the types that define how tasks and loggers interact with the MSBuild engine. For additional information on this component, see our [Microsoft.Build.Framework wiki page](https://github.com/Microsoft/msbuild/wiki/Microsoft.Build.Framework).

* **Microsoft.Build.Tasks**. The [Microsoft.Build.Tasks](https://msdn.microsoft.com/en-us/library/microsoft.build.tasks(v=vs.120).aspx) namespace contains the implementation of all tasks shipping with MSBuild.

* **Microsoft.Build.Utilities**. The [Microsoft.Build.Utilities](https://msdn.microsoft.com/en-us/library/microsoft.build.utilities(v=vs.120).aspx) namespace provides helper classes that you can use to create your own MSBuild loggers and tasks.

### License

MSBuild is licensed under the [MIT license](LICENSE).
