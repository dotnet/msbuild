# .NET Command Line Interface

[![.NET Slack Status](https://aspnetcoreslack.herokuapp.com/badge.svg?2)](http://tattoocoder.com/aspnet-slack-sign-up/) [![Join the chat at https://gitter.im/dotnet/cli](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/cli?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This repo contains the source code for cross-platform [.NET Core](http://github.com/dotnet/core) command line toolchain. It contains the implementation of each command, the native packages for various supported platforms and the documentation.

Looking for V1 of the .NET Core tooling?
----------------------------------------

If you are looking for the v1.0.1 release of the .NET Core tools (CLI, MSBuild and the new csproj), see https://dot.net/core.

> **Note:** the release/15.5 branch of the CLI repo is based on an upcoming update of the SDK and is considered pre-release. For production-level usage, please use the
> released version of the tools available at https://dot.net/core

Found an issue?
---------------
You can consult the [Documents Index](Documentation/README.md) to find out the current issues and to see the workarounds.  

If you don't find your issue, please file one! However, given that this is a very high-frequency repo, we've setup some [basic guidelines](Documentation/project-docs/issue-filing-guide.md) to help you. Please consult those first.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

Build Status
------------

|Windows x64 |Windows x86 |Mac OS X |Linux x64 |Ubuntu 14.04 / Linux Mint 17 |Ubuntu 16.04 |Ubuntu 16.10 |Debian 8 |RHEL 7.2 |
|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|
|[![][win-x64-build-badge]][win-x64-build]|[![][win-x86-build-badge]][win-x86-build]|[![][osx-build-badge]][osx-build]|[![][linux-build-badge]][linux-build]|[![][ubuntu-14.04-build-badge]][ubuntu-14.04-build]|[![][ubuntu-16.04-build-badge]][ubuntu-16.04-build]|[![][ubuntu-16.10-build-badge]][ubuntu-16.10-build]|[![][debian-8-build-badge]][debian-8-build]|[![][rhel-build-badge]][rhel-build]|

[win-x64-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/6786/badge
[win-x64-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=6786

[win-x86-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/6787/badge
[win-x86-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=6787

[osx-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/6781/badge
[osx-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=6781

[linux-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/6780/badge
[linux-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=6780

[ubuntu-14.04-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/6783/badge
[ubuntu-14.04-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=6783

[ubuntu-16.04-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/6784/badge
[ubuntu-16.04-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=6784

[ubuntu-16.10-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/6785/badge
[ubuntu-16.10-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=6785

[debian-8-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/6778/badge
[debian-8-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=6778

[rhel-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/6782/badge
[rhel-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=6782

Installers and Binaries
-----------------------

You can download the .NET Core SDK as either an installer (MSI, PKG) or a zip (zip, tar.gz). The .NET Core SDK contains both the .NET Core runtime and CLI tools.

To download the .NET Core runtime **without** the SDK, visit https://github.com/dotnet/core-setup#daily-builds.

> **Note:** Be aware that the following installers are the **latest bits**. If you
> want to install the latest released versions, check out the [preceding section](#looking-for-v1-of-the-net-core-tooling).

| Platform | Latest Daily Build<br>*release/15.5*<br>[![][version-badge]][version] |
| -------- | :-------------------------------------: |
| **Windows x64** | [Installer][win-x64-installer] - [Checksum][win-x64-installer-checksum]<br>[zip][win-x64-zip] - [Checksum][win-x64-zip-checksum] |
| **Windows x86** | [Installer][win-x86-installer] - [Checksum][win-x86-installer-checksum]<br>[zip][win-x86-zip] - [Checksum][win-x86-zip-checksum] |
| **Mac OS X** | [Installer][osx-installer] - [Checksum][osx-installer-checksum]<br>[tar.gz][osx-targz] - [Checksum][osx-targz-checksum] |
| **Linux x64** | [tar.gz][linux-targz] - [Checksum][linux-targz-checksum] |
| **Ubuntu 14.04 / Linux Mint 17** | [Installer][ubuntu-14.04-installer] - [Checksum][ubuntu-14.04-installer-checksum]<br>_see installer note below_<sup>1</sup><br>tar.gz - See **Linux x64** |
| **Ubuntu 16.04** | [Installer][ubuntu-16.04-installer] - [Checksum][ubuntu-16.04-installer-checksum]<br>_see installer note below_<sup>1</sup><br>tar.gz - See **Linux x64** |
| **Ubuntu 16.10** | [Installer][ubuntu-16.10-installer] - [Checksum][ubuntu-16.10-installer-checksum]<br>_see installer note below_<sup>1</sup><br>tar.gz - See **Linux x64** |
| **Debian 8**  | [Installer][debian-8-installer] - [Checksum][debian-8-installer-checksum]<br>_see installer note below_<sup>1</sup><br>tar.gz - See **Linux x64** |
| **RHEL 7.2** | [Installer][rhel-7-installer] - [Checksum][rhel-7-installer-checksum]<br>_see installer note below_<sup>1</sup><br>tar.gz - See **Linux x64** |
| **CentOS 7.1 / Oracle Linux 7** | tar.gz - See **Linux x64** |
| **Fedora 24** | tar.gz - See **Linux x64** |
| **OpenSUSE 42.1** | tar.gz - See **Linux x64** |


| Latest Coherent Build<sup>2</sup><br>*release/15.5* |
|:------:|
| [![][coherent-version-badge]][coherent-version] |

Reference notes:
> **1**: *Our Debian packages are put together slightly differently than the other OS specific installers. Instead of combining everything, we have separate component packages that depend on each other. If you're installing these directly from the .deb files (via dpkg or similar), then you'll need to install the [corresponding Host, Host FX Resolver, and Shared Framework packages](https://github.com/dotnet/core-setup#daily-builds) before installing the Sdk package.*
> <br><br>**2**: *A 'coherent' build is defined as a build where the Runtime version matches between the CLI and Asp.NET.*

[comment]: # (The latest versions are always the same across all platforms. Just need one to show, so picking win-x64's svg.)
[version]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/latest.version
[version-badge]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/win_x64_Release_version_badge.svg
[coherent-version]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/latest.coherent.version
[coherent-version-badge]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/win_x86_Release_coherent_badge.svg

[win-x64-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-win-x64.exe
[win-x64-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-win-x64.exe.sha
[win-x64-zip]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-win-x64.zip
[win-x64-zip-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-win-x64.zip.sha

[win-x86-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-win-x86.exe
[win-x86-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-win-x86.exe.sha
[win-x86-zip]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-win-x86.zip
[win-x86-zip-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-win-x86.zip.sha

[osx-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-osx-x64.pkg
[osx-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-osx-x64.pkg.sha
[osx-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-osx-x64.tar.gz
[osx-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-osx-x64.tar.gz.sha

[linux-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-linux-x64.tar.gz
[linux-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-linux-x64.tar.gz.sha

[ubuntu-14.04-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-ubuntu-x64.deb
[ubuntu-14.04-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-ubuntu-x64.deb.sha

[ubuntu-16.04-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-ubuntu.16.04-x64.deb
[ubuntu-16.04-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-ubuntu.16.04-x64.deb.sha

[ubuntu-16.10-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-ubuntu.16.10-x64.deb
[ubuntu-16.10-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-ubuntu.16.10-x64.deb.sha

[debian-8-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-debian-x64.deb
[debian-8-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-debian-x64.deb.sha

[rhel-7-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-rhel-x64.rpm
[rhel-7-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/15.5/dotnet-sdk-latest-rhel-x64.rpm.sha

# Debian daily feed

Newest SDK binaries for 2.0.0 in debian feed may be delayed due to external issues by up to 24h.

## Obtaining binaries

### Add debian feed:
Ubuntu 14.04
```
sudo sh -c 'echo "deb [arch=amd64] http://apt-mo.trafficmanager.net/repos/dotnet/ trusty main" > /etc/apt/sources.list.d/dotnetdev.list'

sudo apt-key adv --keyserver apt-mo.trafficmanager.net --recv-keys 417A0893

sudo apt-get update
```

Ubuntu 16.04
```
sudo sh -c 'echo "deb [arch=amd64] http://apt-mo.trafficmanager.net/repos/dotnet/ xenial main" > /etc/apt/sources.list.d/dotnetdev.list'

sudo apt-key adv --keyserver apt-mo.trafficmanager.net --recv-keys 417A0893

sudo apt-get update
```

Ubuntu 16.10
```
sudo sh -c 'echo "deb [arch=amd64] http://apt-mo.trafficmanager.net/repos/dotnet/ yakkety main" > /etc/apt/sources.list.d/dotnetdev.list'

sudo apt-key adv --keyserver apt-mo.trafficmanager.net --recv-keys 417A0893

sudo apt-get update
```

Debian 8
```
sudo sh -c 'echo "deb [arch=amd64] http://apt-mo.trafficmanager.net/repos/dotnet/ jessie main" > /etc/apt/sources.list.d/dotnetdev.list'

sudo apt-key adv --keyserver apt-mo.trafficmanager.net --recv-keys 417A0893

sudo apt-get update
```

### Install:
```
sudo apt-get install <DebianPackageName>=<Version>
```

### To list available packages:
```
apt-cache search dotnet-sdk | grep 2.0.0
```

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

For more details, refer to the [documentation](https://aka.ms/dotnet-cli-docs).

Building from source
--------------------

If you are building from source, take note that the build depends on NuGet packages hosted on MyGet, so if it is down, the build may fail. If that happens, you can always see the [MyGet status page](http://status.myget.org/) for more info.

Read over the [contributing guidelines](CONTRIBUTING.md) and [developer documentation](Documentation) for prerequisites for building from source.

Questions & Comments
--------------------

For all feedback, use the Issues on this repository.

License
-------

By downloading the .zip you are agreeing to the terms in the project [EULA](https://aka.ms/dotnet-core-eula).

