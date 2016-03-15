# .NET Command Line Interface

[![Join the chat at https://gitter.im/dotnet/cli](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/cli?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This repo contains the source code for cross-platform [.NET Core](http://github.com/dotnet/core) command line toolchain. It contains the implementation of each command, the native packages for various supported platforms as well as documentation. 

New to .NET CLI?
------------
Check out our http://dotnet.github.io/getting-started/ page. 

Found an issue?
---------------
You can consult the [known issues page](Documentation/known-issues.md) to find out the current issues and 
to see the workarounds.  

If you don't find your issue, please file one! However, given that this is a very high-frequency repo, we've setup some [basic guidelines](Documentation/issue-filing-guide.md) to help you. Please consult those first.

Build Status
------------

|Ubuntu 14.04 |Windows x64 |Windows x86 |Mac OS X |CentOS 7.1 |RHEL 7.2 |
|:------:|:------:|:------:|:------:|:------:|:------:|
|![](https://devdiv.visualstudio.com/DefaultCollection/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/601/badge)|![](https://mseng.visualstudio.com/DefaultCollection/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3022/badge)|![](https://mseng.visualstudio.com/DefaultCollection/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3071/badge)|![](https://devdiv.visualstudio.com/DefaultCollection/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/600/badge) |![](https://devdiv.visualstudio.com/DefaultCollection/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/597/badge) |![](https://devdiv.visualstudio.com/DefaultCollection/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/897/badge) |

Installers
----------

|         |Ubuntu 14.04 |Windows x64 |Windows x86 |Mac OS X |CentOS 7.1 |RHEL 7.2 |
|---------|:------:|:------:|:------:|:------:|:------:|:------:|
|**Version**|![](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/Ubuntu_x64_Release_version_badge.svg)|![](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/Windows_x64_Release_version_badge.svg)|![](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/Windows_x86_Release_version_badge.svg)|![](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/OSX_x64_Release_version_badge.svg)|![](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/CentOS_x64_Release_version_badge.svg)|![](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/RHEL_x64_Release_version_badge.svg)|
|**Installers**|[Download Debian Package](https://dotnetcli.blob.core.windows.net/dotnet/beta/Installers/Latest/dotnet-ubuntu-x64.latest.deb)|[Download Installer](https://dotnetcli.blob.core.windows.net/dotnet/beta/Installers/Latest/dotnet-win-x64.latest.exe)|[Download Installer](https://dotnetcli.blob.core.windows.net/dotnet/beta/Installers/Latest/dotnet-win-x86.latest.exe)|[Download Pkg](https://dotnetcli.blob.core.windows.net/dotnet/beta/Installers/Latest/dotnet-osx-x64.latest.pkg) |N/A |N/A |
|**Binaries**|[Download tar file](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/dotnet-ubuntu-x64.latest.tar.gz)|[Download zip file](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/dotnet-win-x64.latest.zip)|[Download zip file](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/dotnet-win-x86.latest.zip)|[Download tar file](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/dotnet-osx-x64.latest.tar.gz) |[Download tar file](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/dotnet-centos-x64.latest.tar.gz)|[Download tar file](https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/Latest/dotnet-rhel-x64.latest.tar.gz) |

Interested in .NET Core + ASP.NET Core 1.0 RC1 bits?
----------------------------------------------------

This toolchain is independent from the DNX-based .NET Core + ASP.NET Core 1.0 RC1 bits. If you are looking for .NET Core + ASP.NET Core 1.0 RC1 bits, you can find instructions on the http://get.asp.net/.  

Docker
------

You can also use our Docker base images found on https://hub.docker.com/r/microsoft/dotnet to set up your dev or testing environment for usage.  

Basic usage
-----------

When you have the .NET Command Line Interface installed on your OS of choice, you can try it out using some of the samples on the [dotnet/core repo](https://github.com/dotnet/core/tree/master/samples). You can download the sample in a directory, and then you can kick the tires of the CLI.


First, you will need to restore the packages:
	
	dotnet restore
	
This will restore all of the packages that are specified in the project.json file of the given sample.

Then you can either run from source or compile the sample. Running from source is straightforward:
	
	dotnet run
	
Compiling to IL is done using:
	
	dotnet build

This will drop a binary in `./bin/[configuration]/[framework]/[rid]/[binary name]` that you can just run.

For more details, please refer to the [documentation](https://github.com/dotnet/corert/tree/master/Documentation).

Building from source
--------------------

If you are building from source, take note that the build depends on NuGet packages hosted on Myget, so if it is down, the build may fail. If that happens, you can always see the [Myget status page](http://status.myget.org/) for more info. 

Read over the [contributing guidelines](https://github.com/dotnet/cli/tree/master/CONTRIBUTING.md) and [developer documentation](https://github.com/dotnet/cli/tree/master/Documentation) for prerequisites for building from source.

Questions & Comments
--------------------

For any and all feedback, please use the Issues on this repository. 
