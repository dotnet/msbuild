# .NET Command Line Interface

[![.NET Slack Status](https://aspnetcoreslack.herokuapp.com/badge.svg?2)](http://tattoocoder.com/aspnet-slack-sign-up/) [![Join the chat at https://gitter.im/dotnet/cli](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/cli?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This repo contains the source code for cross-platform [.NET Core](http://github.com/dotnet/core) command line toolchain. It contains the implementation of each command, the native packages for various supported platforms and the documentation.

Looking for V1 of the .NET Core tooling?
----------------------------------------

If you are looking for the v2.0 release of the .NET Core tools (CLI, MSBuild and the new csproj), head over to https://dot.net/core and download!

> **Note:** the release/2.2.1xx branch of the CLI repo is based on an upcoming update of the SDK and is considered pre-release. For production-level usage, please use the
> released version of the tools available at https://dot.net/core

Found an issue?
---------------
You can consult the [Documents Index](Documentation/README.md) to find out the current issues and to see the workarounds.

If you don't find your issue, please file one! However, given that this is a very high-frequency repo, we've setup some [basic guidelines](Documentation/project-docs/issue-filing-guide.md) to help you. Please consult those first.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

Build Status
------------

|Windows x64|Windows x86|macOS|Linux x64 Archive|Linux arm Archive|Linux arm64 Archive|Linux Native Installers|RHEL 6 Archive|Linux-musl Archive|Windows Arm|
|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|:------:|
|[![][win-x64-build-badge]][win-x64-build]|[![][win-x86-build-badge]][win-x86-build]|[![][osx-build-badge]][osx-build]|[![][linux-build-badge]][linux-build]|[![][linux-arm-build-badge]][linux-arm-build]|[![][linux-arm64-build-badge]][linux-arm64-build]|[![][linuxnative-build-badge]][linuxnative-build]|[![][rhel6-build-badge]][rhel6-build]|[![][linux-musl-build-badge]][linux-musl-build]|[![][win-arm-build-badge]][win-arm-build]|

[win-x64-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9306/badge
[win-x64-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9306

[win-x86-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9307/badge
[win-x86-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9307

[osx-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9304/badge
[osx-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9304

[linux-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9303/badge
[linux-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9303

[linux-arm-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9301/badge
[linux-arm-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9301

[linux-arm64-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9302/badge
[linux-arm64-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9302

[linuxnative-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9299/badge
[linuxnative-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9299

[rhel6-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9305/badge
[rhel6-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9305

[linux-musl-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9300/badge
[linux-musl-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9300

[win-arm-build-badge]: https://devdiv.visualstudio.com/_apis/public/build/definitions/0bdbc590-a062-4c3f-b0f6-9383f67865ee/9300/badge
[win-arm-build]: https://devdiv.visualstudio.com/DevDiv/_build?_a=completed&definitionId=9300

Installers and Binaries
-----------------------

You can download the .NET Core SDK as either an installer (MSI, PKG) or a zip (zip, tar.gz). The .NET Core SDK contains both the .NET Core runtime and CLI tools.

To download the .NET Core runtime **without** the SDK, visit https://github.com/dotnet/core-setup#daily-builds.

> **Note:** Be aware that the following installers are the **latest bits**. If you
> want to install the latest released versions, check out the [preceding section](#looking-for-v1-of-the-net-core-tooling).
> In order to be able to restore these pre-release packages, you may need to add a NuGet feed pointing to https://dotnet.myget.org/F/dotnet-core/api/v3/index.json. Other feeds may also be necessary depending on what kind of project you are working with.

| Platform | Latest Daily Build<br>*release/2.2.1xx*<br>[![][version-badge]][version] |
| -------- | :-------------------------------------: |
| **Windows x64** | [Installer][win-x64-installer] - [Checksum][win-x64-installer-checksum]<br>[zip][win-x64-zip] - [Checksum][win-x64-zip-checksum] |
| **Windows x86** | [Installer][win-x86-installer] - [Checksum][win-x86-installer-checksum]<br>[zip][win-x86-zip] - [Checksum][win-x86-zip-checksum] |
| **macOS** | [Installer][osx-installer] - [Checksum][osx-installer-checksum]<br>[tar.gz][osx-targz] - [Checksum][osx-targz-checksum] |
| **Linux x64** | [DEB Installer][linux-DEB-installer] - [Checksum][linux-DEB-installer-checksum]<br>[RPM Installer][linux-RPM-installer] - [Checksum][linux-RPM-installer-checksum]<br>_see installer note below_<sup>1</sup><br>[tar.gz][linux-targz] - [Checksum][linux-targz-checksum] |
| **Linux arm** | [tar.gz][linux-arm-targz] - [Checksum][linux-arm-targz-checksum] |
| **Linux arm64** | [tar.gz][linux-arm64-targz] - [Checksum][linux-arm64-targz-checksum] |
| **RHEL 6** | [tar.gz][rhel-6-targz] - [Checksum][rhel-6-targz-checksum] |
| **Linux-musl** | [tar.gz][linux-musl-targz] - [Checksum][linux-musl-targz-checksum] |
| **Windows arm** | [zip][win-arm-zip] - [Checksum][win-arm-zip-checksum] |

| Latest Coherent Build<sup>2</sup><br>*release/2.2.1xx* |
|:------:|
| [![][coherent-version-badge]][coherent-version] |

Reference notes:
> **1**: *Our Debian packages are put together slightly differently than the other OS specific installers. Instead of combining everything, we have separate component packages that depend on each other. If you're installing these directly from the .deb files (via dpkg or similar), then you'll need to install the [corresponding Host, Host FX Resolver, and Shared Framework packages](https://github.com/dotnet/core-setup#daily-builds) before installing the Sdk package.*
> <br><br>**2**: *A 'coherent' build is defined as a build where the Runtime version matches between the CLI and Asp.NET.*

[version]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/latest.version
[coherent-version]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/latest.coherent.version
[comment]: # (The latest versions are always the same across all platforms. Just need one to show, so picking win-x64's svg.)
[version-badge]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/win_x64_Release_version_badge.svg
[coherent-version-badge]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/win_x64_Release_coherent_badge.svg

[win-x64-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-x64.exe
[win-x64-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-x64.exe.sha
[win-x64-zip]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-x64.zip
[win-x64-zip-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-x64.zip.sha

[win-x86-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-x86.exe
[win-x86-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-x86.exe.sha
[win-x86-zip]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-x86.zip
[win-x86-zip-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-x86.zip.sha

[osx-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-osx-x64.pkg
[osx-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-osx-x64.pkg.sha
[osx-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-osx-x64.tar.gz
[osx-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-osx-x64.tar.gz.sha

[linux-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-linux-x64.tar.gz
[linux-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-linux-x64.tar.gz.sha

[linux-arm-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-linux-arm.tar.gz
[linux-arm-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-linux-arm.tar.gz.sha

[linux-arm64-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-linux-arm64.tar.gz
[linux-arm64-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-linux-arm64.tar.gz.sha

[linux-DEB-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-x64.deb
[linux-DEB-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-x64.deb.sha

[linux-RPM-installer]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-x64.rpm
[linux-RPM-installer-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-x64.rpm.sha

[rhel-6-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-rhel.6-x64.tar.gz
[rhel-6-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-rhel.6-x64.tar.gz.sha

[linux-musl-targz]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-linux-musl-x64.tar.gz
[linux-musl-targz-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-linux-musl-x64.tar.gz.sha

[win-arm-zip]: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-arm.zip
[win-arm-zip-checksum]: https://dotnetclichecksums.blob.core.windows.net/dotnet/Sdk/release/2.2.1xx/dotnet-sdk-latest-win-arm.zip.sha

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


