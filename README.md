# .NET Command Line Interface

[![.NET Slack Status](https://aspnetcoreslack.herokuapp.com/badge.svg?2)](http://tattoocoder.com/aspnet-slack-sign-up/) [![Join the chat at https://gitter.im/dotnet/cli](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/cli?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This repo contains the source code for cross-platform [.NET Core](http://github.com/dotnet/core) command line toolchain. It contains the implementation of each command, the native packages for various supported platforms as well as documentation. 

RC 4 release - MSBuild based tools
---------------------------------------
As was outlined in the ["Changes to project.json" blog post](https://blogs.msdn.microsoft.com/dotnet/2016/05/23/changes-to-project-json/), we have started work to move away from project.json to csproj and MSBuild. All the new `latest` releases from this repo (from `rel/1.0.0` branch) are MSBuild-enabled tools.

The current official release of the csproj-enabled CLI tools is **CLI RC 4**. 

There are a couple of things to keep in mind:

* RC 4 CLI bits are still **in development** so some rough edges are to be expected. 
* RC 4 bits **do not support** project.json so you will have to either keep Preview 2 tools around or migrate your project or add a global.json file to your project to target preview2. You can find more information on this using the [project.json to csproj instructions](https://github.com/dotnet/cli/blob/rel/1.0.0/Documentation/ProjectJsonToCSProj.md). 
* RC 4 refers to the **CLI tools only** and does not cover Visual Studio, VS Code or Visual Studio for Mac. 
* We welcome any and all issues that relate to MSBuild-based tools, so feel free to try them out and leave comments and file any bugs/problems.

### Download links
* Instructions and links for download:  [RC3 download links](https://github.com/dotnet/core/blob/master/release-notes/rc3-download.md).
* Directory for future Preview release notes: [.NET Core release notes](https://github.com/dotnet/core/tree/master/release-notes).

Found an issue?
---------------
You can consult the [known issues page](https://github.com/dotnet/core/blob/master/cli/known-issues.md) to find out the current issues and to see the workarounds.  

If you don't find your issue, please file one! However, given that this is a very high-frequency repo, we've setup some [basic guidelines](Documentation/issue-filing-guide.md) to help you. Please consult those first.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

Build Status
------------

|Ubuntu 14.04 / Linux Mint 17 |Ubuntu 16.04 |Debian 8.2 |Windows x64 |Windows x86 |Mac OS X |CentOS 7.1 / Oracle Linux 7.1 |RHEL 7.2 |OpenSUSE 13.2 |Fedora 23|
|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|
|[![][ubuntu-14.04-build-badge]][ubuntu-14.04-build]|[![][ubuntu-16.04-build-badge]][ubuntu-16.04-build]|[![][debian-8.2-build-badge]][debian-8.2-build]|[![][win-x64-build-badge]][win-x64-build]|[![][win-x86-build-badge]][win-x86-build]|[![][osx-build-badge]][osx-build]|[![][centos-build-badge]][centos-build]|[![][rhel-build-badge]][rhel-build]|[![][opensuse-13.2-build-badge]][opensuse-13.2-build]|[![][fedora-23-build-badge]][fedora-23-build]|

[win-x64-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5469/badge
[win-x64-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5469

[win-x86-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5470/badge
[win-x86-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5470

[ubuntu-14.04-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5467/badge
[ubuntu-14.04-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5467

[ubuntu-16.04-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5468/badge
[ubuntu-16.04-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5468

[debian-8.2-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5462/badge
[debian-8.2-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5462

[osx-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5465/badge
[osx-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5465

[centos-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5461/badge
[centos-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5461

[rhel-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5466/badge
[rhel-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5466

[opensuse-13.2-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5464/badge
[opensuse-13.2-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5464

[fedora-23-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/5463/badge
[fedora-23-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=5463

Installers and Binaries
-----------------------

You can download .NET Core SDK as either an installer (MSI, PKG) or a zip (zip, tar.gz). .NET Core SDK contains both the .NET Core runtime and CLI tools.

In order to download just the .NET Core runtime without the SDK, please visit https://github.com/dotnet/core-setup#daily-builds.

> **Note:** please be aware that below installers are the **latest bits**. If you 
> want to install the latest released versions, please check out the [section above](#download-links).)

| Platform | rel/1.0.0<br>[![][version-badge]][version] |
|----------------------------------|:--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|:---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|
| **Windows x64** | [Installer][win-x64-installer] - [Checksum][win-x64-installer-checksum]<br>[zip][win-x64-zip] - [Checksum][win-x64-zip-checksum] |
| **Windows x86** | [Installer][win-x86-installer] - [Checksum][win-x86-installer-checksum]<br>[zip][win-x86-zip] - [Checksum][win-x86-zip-checksum] |
| **Ubuntu 14.04 / Linux Mint 17** | [Installer][ubuntu-14.04-installer] - [Checksum][ubuntu-14.04-installer-checksum]<br>*See Installer Note Below<br>[tar.gz][ubuntu-14.04-targz] - [Checksum][ubuntu-14.04-targz-checksum] |
| **Ubuntu 16.04** | [Installer][ubuntu-16.04-installer] - [Checksum][ubuntu-16.04-installer-checksum]<br>*See Installer Note Below<br>[tar.gz][ubuntu-16.04-targz] - [Checksum][ubuntu-16.04-targz-checksum] |
| **Debian 8.2** | [tar.gz][debian-8.2-targz] - [Checksum][debian-8.2-targz-checksum] |
| **Mac OS X** | [Installer][osx-installer] - [Checksum][osx-installer-checksum]<br>[tar.gz][osx-targz] - [Checksum][osx-targz-checksum] |
| **CentOS 7.1 / Oracle Linux 7** | [tar.gz][centos-targz] - [Checksum][centos-targz-checksum] |
| **RHEL 7.2** | [tar.gz][rhel-targz] - [Checksum][rhel-targz-checksum] |
| **openSUSE 13.2** | [tar.gz][opensuse-13.2-targz] - [Checksum][opensuse-13.2-targz-checksum] |
| **Fedora 23** | [tar.gz][fedora-23-targz] - [Checksum][fedora-23-targz-checksum] |

*Note: Our Debian packages are put together slightly differently than the other OS specific installers. Instead of combining everything, we have separate component packages that depend on each other. If you're installing these directly from the .deb files (via dpkg or similar), then you'll need to install the [corresponding Host, Host FX Resolver, and Shared Framework packages](https://github.com/dotnet/core-setup#daily-builds) before installing the Sdk package.*

[version]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/latest.version
[comment]: # (The latest versions are always the same across all platforms. Just need one to show, so picking win-x64's svg.)
[version-badge]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/Windows_x64_Release_version_badge.svg

[win-x64-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x64.latest.exe
[win-x64-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x64.latest.exe.sha
[win-x64-zip]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x64.latest.zip
[win-x64-zip-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x64.latest.zip.sha

[win-x86-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x86.latest.exe
[win-x86-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x86.latest.exe.sha
[win-x86-zip]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x86.latest.zip
[win-x86-zip-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-win-x86.latest.zip.sha

[ubuntu-14.04-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-sdk-ubuntu-x64.latest.deb
[ubuntu-14.04-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-sdk-ubuntu-x64.latest.deb.sha
[ubuntu-14.04-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-ubuntu-x64.latest.tar.gz
[ubuntu-14.04-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-ubuntu-x64.latest.tar.gz.sha

[ubuntu-16.04-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-sdk-ubuntu.16.04-x64.latest.deb
[ubuntu-16.04-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-sdk-ubuntu.16.04-x64.latest.deb.sha
[ubuntu-16.04-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-ubuntu.16.04-x64.latest.tar.gz
[ubuntu-16.04-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-ubuntu.16.04-x64.latest.tar.gz.sha

[debian-8.2-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-debian-x64.latest.tar.gz
[debian-8.2-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-debian-x64.latest.tar.gz.sha

[osx-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-osx-x64.latest.pkg
[osx-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-osx-x64.latest.pkg.sha
[osx-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-osx-x64.latest.tar.gz
[osx-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-osx-x64.latest.tar.gz.sha

[centos-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-centos-x64.latest.tar.gz
[centos-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-centos-x64.latest.tar.gz.sha

[rhel-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-rhel-x64.latest.tar.gz
[rhel-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-rhel-x64.latest.tar.gz.sha

[opensuse-13.2-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-opensuse.13.2-x64.latest.tar.gz
[opensuse-13.2-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-opensuse.13.2-x64.latest.tar.gz.sha

[fedora-23-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-fedora.23-x64.latest.tar.gz
[fedora-23-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/rel-1.0.0/dotnet-dev-fedora.23-x64.latest.tar.gz.sha

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
