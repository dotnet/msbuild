# .NET Command Line Interface

[![.NET Slack Status](https://aspnetcoreslack.herokuapp.com/badge.svg?2)](http://tattoocoder.com/aspnet-slack-sign-up/) [![Join the chat at https://gitter.im/dotnet/cli](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/cli?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This repo contains the source code for cross-platform [.NET Core](http://github.com/dotnet/core) command line toolchain. It contains the implementation of each command, the native packages for various supported platforms as well as documentation. 

Preview 4 release - MSBuild based tools
---------------------------------------
As was outlined in the ["Changes to project.json" blog post](https://blogs.msdn.microsoft.com/dotnet/2016/05/23/changes-to-project-json/), we have started work to move away from project.json to csproj and MSBuild. All the new `latest` releases from this repo (from `rel/1.0.0` branch) are MSBuild-enabled tools. 

The current official release of the csproj-enabled CLI tools is **CLI Preview 4**. 

There are a couple of things to keep in mind:

* Preview 4 CLI bits are still **in development** so some rough edges are to be expected. 
* Preview 4 bits **do not support** project.json so you will have to either keep Preview 2 tools around or migrate your project or add a global.json file to your project to target preview2. You can find more information on this using the [project.json to csproj instructions](https://github.com/dotnet/cli/blob/rel/1.0.0/Documentation/ProjectJsonToCSProj.md). 
* Preview 4 refers to the **CLI tools only** and does not cover Visual Studio, VS Code or Visual Studio for Mac. 
* We welcome any and all issues that relate to MSBuild-based tools, so feel free to try them out and leave comments and file any bugs/problems.

### Download links

* Instructions and links for download:  [Preview 4 download links](https://github.com/dotnet/core/blob/master/release-notes/preview4-download.md). 
* Directory for future Preview release notes: [.NET Core release notes](https://github.com/dotnet/core/tree/master/release-notes).

Found an issue?
---------------
You can consult the [known issues page](https://github.com/dotnet/core/blob/master/cli/known-issues.md) to find out the current issues and 
to see the workarounds.  

If you don't find your issue, please file one! However, given that this is a very high-frequency repo, we've setup some [basic guidelines](Documentation/issue-filing-guide.md) to help you. Please consult those first.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

Build Status
------------

|Ubuntu 14.04 / Linux Mint 17 |Ubuntu 16.04 |Debian 8.2 |Windows x64 |Windows x86 |Mac OS X |CentOS 7.1 / Oracle Linux 7.1 |RHEL 7.2 |OpenSUSE 13.2 |Fedora 23|
|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|
|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3132/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3132)|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3618/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3618)|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3271/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3271)|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3022/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3022)|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3071/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3071)|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3397/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3397)|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3257/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3257)|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3256/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3256)|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3626/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3626)|[![](https://mseng.visualstudio.com/_apis/public/build/definitions/d09b7a4d-0a51-4c0e-a15a-07921d5b558f/3623/badge)](https://mseng.visualstudio.com/dotnetcore/_build?_a=completed&definitionId=3623)|

Installers and Binaries
-----------------------

You can download .NET Core as either an installer (MSI, PKG) or a zip (zip, gzip). You can download the product in two flavors:

- .NET Core - .NET Core runtime and framework
- .NET Core SDK - .NET Core + CLI tools

> **Note:** please be aware that below installers are the **latest bits**. If you 
> want to install the latest released versions, please check out the [section above](#download-links).)

|  | Version | .NET Core Installer | .NET Core SDK Installer | .NET Core Binaries | .NET Core SDK Binaries |
|----------------------------------|:--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|:--------------------------------------------------------------------------------------------------------------:|:---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|:-------------------------------------------------------------------------------------------------------------------------:|:-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|
| **Windows x64** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Windows_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Installers/Latest/dotnet-win-x64.latest.exe) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x64.latest.exe) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x64.latest.exe.sha) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-win-x64.latest.zip) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x64.latest.zip) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x64.latest.zip.sha) |
| **Windows x86** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Windows_x86_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Installers/Latest/dotnet-win-x86.latest.exe) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x86.latest.exe) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x86.latest.exe.sha) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-win-x86.latest.zip) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x86.latest.zip) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x86.latest.zip.sha) |
| **Ubuntu 14.04 / Linux Mint 17** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Ubuntu_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | *See Below* | *See Below* | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-ubuntu-x64.latest.tar.gz) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-ubuntu-x64.latest.tar.gz) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-ubuntu-x64.latest.tar.gz.sha) |
| **Ubuntu 16.04** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Ubuntu_16_04_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | N/A | N/A | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-ubuntu.16.04-x64.latest.tar.gz) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-ubuntu.16.04-x64.latest.tar.gz) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-ubuntu.16.04-x64.latest.tar.gz.sha) |
| **Debian 8.2** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Debian_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | N/A | N/A | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-debian-x64.latest.tar.gz) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-debian-x64.latest.tar.gz) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-debian-x64.latest.tar.gz.sha) |
| **Mac OS X** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/OSX_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Installers/Latest/dotnet-osx-x64.latest.pkg) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-osx-x64.latest.pkg) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-osx-x64.latest.pkg.sha) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-osx-x64.latest.tar.gz) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-osx-x64.latest.tar.gz) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-osx-x64.latest.tar.gz.sha) |
| **CentOS 7.1 / Oracle Linux 7** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/CentOS_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | N/A | N/A | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-centos-x64.latest.tar.gz) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-centos-x64.latest.tar.gz) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-centos-x64.latest.tar.gz.sha) |
| **RHEL 7.2** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/RHEL_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | N/A | N/A | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-rhel-x64.latest.tar.gz) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-rhel-x64.latest.tar.gz) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-rhel-x64.latest.tar.gz.sha) |
| **openSUSE 13.2** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/openSUSE_13_2_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | N/A | N/A | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-opensuse.13.2-x64.latest.tar.gz) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-opensuse.13.2-x64.latest.tar.gz) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-opensuse.13.2-x64.latest.tar.gz.sha) |
| **Fedora 23** | [![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Fedora_23_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version) | N/A | N/A | [Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Binaries/Latest/dotnet-fedora.23-x64.latest.tar.gz) | [Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-fedora.23-x64.latest.tar.gz) [Checksums](https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-fedora.23-x64.latest.tar.gz.sha) |

Ubuntu Installers
----------

*Our Debian packages are put together slightly differently than the other OS specific installers. Instead of combining everything, we have separate component packages that depend on each other. If you're installing these directly from the .deb files (via dpkg or similar), then you'll need to install them in the order presented below.*

**For Ubuntu 14.04

|         |Version |Installers|
|---------|:------:|:------:|:------:|
|**Shared Host**|[![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Ubuntu_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version)|[Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Installers/Latest/dotnet-host-ubuntu-x64.latest.deb)|
|**Host Framework Resolver**|[![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Ubuntu_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version)|[Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Installers/Latest/dotnet-hostfxr-ubuntu-x64.latest.deb)|
|**Shared Framework**|[![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Ubuntu_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version)|[Download](https://dotnetcli.blob.core.windows.net/dotnet/preview/Installers/Latest/dotnet-sharedframework-ubuntu-x64.latest.deb)|
|**Sdk**|[![](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Ubuntu_x64_Release_version_badge.svg)](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version)|[Download](https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-sdk-ubuntu-x64.latest.deb)|

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

This will drop an IL assembly in `./bin/[configuration]/[framework]/[binary name]` 
that you can run using `dotnet bin/[configuration]/[framework]/[binaryname.dll]`.

For more details, please refer to the [documentation](https://aka.ms/dotnet-cli-docs).

Building from source
--------------------

If you are building from source, take note that the build depends on NuGet packages hosted on MyGet, so if it is down, the build may fail. If that happens, you can always see the [MyGet status page](http://status.myget.org/) for more info. 

Read over the [contributing guidelines](CONTRIBUTING.md) and [developer documentation](Documentation) for prerequisites for building from source.

Questions & Comments
--------------------

For any and all feedback, please use the Issues on this repository. 

License
-------

By downloading the .zip you are agreeing to the terms in the project [EULA](https://aka.ms/dotnet-core-eula).
