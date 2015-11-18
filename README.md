# .NET Command Line Interface

This repo contains the source code for cross-platform [.NET Core](http://github.com/dotnet/core) command line toolchain. It contains the implementation of each command, the native packages for various supported platforms as well as documentation. 

Build Status
------------

|         |Ubuntu 14.04 |Windows |Mac OS X |
|---------|:------:|:------:|:------:|
|**Debug**|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_debug_ubuntu)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_debug_ubuntu/)|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_debug_windows_nt)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_debug_windows_nt/)|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_debug_osx)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_debug_osx/) |
|**Release**|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_release_ubuntu)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_release_ubuntu/)|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_release_windows_nt)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_release_windows_nt/)|[![Build Status](http://dotnet-ci.cloudapp.net/buildStatus/icon?job=Private/dotnet_cli_release_osx)](http://dotnet-ci.cloudapp.net/job/Private/job/dotnet_cli_release_osx/) |

Installers
----------

|         |Ubuntu 14.04 |Windows |Mac OS X |
|---------|:------:|:------:|:------:|
|**Installers**|[Download Debian Package](https://dotnetcli.blob.core.windows.net/dotnet/dev/Installers/Latest/dotnet-linux-x64.latest.deb)|[Download Msi](https://dotnetcli.blob.core.windows.net/dotnet/dev/Installers/Latest/dotnet-win-x64.latest.msi)|[Download Pkg](https://dotnetcli.blob.core.windows.net/dotnet/dev/Installers/Latest/dotnet-osx-x64.latest.pkg) |
|**Binaries**|[Download tar file](https://dotnetcli.blob.core.windows.net/dotnet/dev/Binaries/Latest/dotnet-linux-x64.latest.tar.gz)|[Download zip file](https://dotnetcli.blob.core.windows.net/dotnet/dev/Binaries/Latest/dotnet-win-x64.latest.zip)|[Download tar file](https://dotnetcli.blob.core.windows.net/dotnet/dev/Binaries/Latest/dotnet-osx-x64.latest.tar.gz) |

Docker
------

You can also use our Docker base images found on https://hub.docker.com/r/microsoft/dotnet to set up your dev or testing environment for usage.  

Basic usage
-----------

When you have the .NET Command Line Interface installed on your OS of choice, you can try it out using some of the samples on the [main Core repo](https://github.com/dotnet/core/). You can download the sample in a directory, and then do you can kick the tires of the CLI.

First, you will need to restore the packages:
	
	dotnet restore
	
This will restore all of the packages that are specified in the project.json file of the given sample.

Then you can either run from source or compile the sample. Running from source is straightforward:
	
	dotnet run
	
Compiling to IL is done using:
	
	dotnet compile
This will drop a binary in `./bin/[configuration]/[framework]/[binary name]` that you can just run.

Finally, you can also try out native compilation on Windows and Linux. **Note:** at this point, only the `helloworld` and `dotnetbot` samples will work with native compilation.

	dotnet compile --native
	
This will drop a native single binary in `./bin/[configuration]/[framework]/native/[binary name]` that you can run.  

Questions & Comments
--------------------

For any and all feedback, please use the Issues on this repository. 
