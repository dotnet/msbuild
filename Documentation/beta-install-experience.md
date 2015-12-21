Beta install options and experience
===================================

For the Beta timeframe, we will have two main install/acquisition experiences:

1. Native/package installers
2. Local install

## Native/package installers

Native/package installers refers to the pre-packaged installers that are using whatever technology is "native" to the targeted platform. These installers are meant to be used mostly for setting up a development environment one one's computer for writing applications, trying out the CLI tooling etc. Their benefit is that they install all of the pre-requisites for the CLI tooling to work. Their downside is that they install a machine-wide copy of the CLI tooling and they require (with the sole exception of the Homebrew package) super-user privileges. 

For Beta in January timeframe, we will have these native installers:

* Windows
	* [MSI installer](https://dotnetcli.blob.core.windows.net/dotnet/dev/Installers/Latest/dotnet-win-x64.latest.msi)
* Ubuntu
	* Debian package through a custom apt-get feed
* OS X
	* [PKG installer](https://dotnetcli.blob.core.windows.net/dotnet/dev/Installers/Latest/dotnet-osx-x64.latest.pkg)
	* Homebrew formula - TBD
* CentOS
  * N/A

Instructions on how to get and use these above installers can be found on http://dotnet.github.io/getting-started/. 

>**Note:** as of time of this writing (12/21/2015) the Homebrew formula is still being made. 

## Local install

Local install refers to a zip/tarball that the user would need to download and then copy over to a location on disk where the tools should live. This way of installing has the benefit of providing complete control over the location of the CLI tooling, allowing multiple versions to live side-by-side as well as not requiring super-user privileges. The downside is that all of the dependencies need to be installed manually. The main scenario for using this is to avoid machine-wide installs, or in those scenarios where the CLI tools should "follow" the source (i.e. source code repos, build servers etc.) 

There are two main ways

* ZIP file for [Windows](https://dotnetcli.blob.core.windows.net/dotnet/dev/Installers/Latest/dotnet-win-x64.latest.msi)
* Tarball (tar.gz) file for *NIX
  * [Ubuntu 14.04](https://dotnetcli.blob.core.windows.net/dotnet/dev/Binaries/Latest/dotnet-linux-x64.latest.tar.gz)
  * [OS X 10.10](https://dotnetcli.blob.core.windows.net/dotnet/dev/Binaries/Latest/dotnet-osx-x64.latest.tar.gz)
  * [CentOS 7.1](https://dotnetcli.blob.core.windows.net/dotnet/dev/Binaries/Latest/dotnet-centos-x64.latest.tar.gz)

There will be two main ways to install things in a local fashion:

1. Manual way - download the archive of the tools over HTTP, install them in a wanted directory. 
2. Using the install.sh/install.ps1 scripts - this means that the user only acquires the script itself, which is then invoked with certain parameters and it does everything else. 

>**Note:** as of this writing (12/21/2015) the scripts mentioned above still need some work to be usable in this regard. This document will be updated when we make those changes. 

## Updating the bits

Once installed, how you update the bits depends on how you installed them. 

### Windows MSI 
Uninstall your copy of CLI and then reinstall using the MSI. 

### Ubuntu 
Use the following commands:

    sudo apt-get update
    sudo apt-get install dotnet

This will refresh the apt-get feeds and install the latest version of the `dotnet` package. 

### OS X PKG installer
There is no real "update" PKG install. You can just re-run the latest PKG and it will rewrite the files in the destination and create new symlinks if there are any. 

### OS X Homebrew formula
Refer to [homebrew documentation](https://github.com/Homebrew/homebrew/tree/master/share/doc/homebrew#readme) for instructions, but usually it is the case of using `brew update $FORMULA` to update the specific formula to latest. 

### Windows/*NIX local install
Updating the bits is as easy as re-running the scripts with exactly the same parameters (i.e. the same target directory for the binaries) or redoing what you did for the manual install. Since there is no machine-wide location of the binaries that make up the CLI toolset, there is nothing more to be done. 

## Removing the bits
Removing the bits from the machine also depends on how you installed them. 

### Windows MSI 
Uninstall the product using Control Panel. 

### Ubuntu 
Use the following command:

    sudo apt-get purge dotnet 
This will remove all traces of dotnet of your box. 

### OS X PKG installer
There is no "uninstall" of PKG installed software. There is a way, however, to see what files were installed and then manually remove them. [This SuperUser question](http://superuser.com/questions/36567/how-do-i-uninstall-any-apple-pkg-package-file) has all of the details on how would you do this.

### OS X Homebrew formula
Refer to [homebrew documentation](https://github.com/Homebrew/homebrew/tree/master/share/doc/homebrew#readme) for instructions, but it is usually done with `brew uninstall $FORMULA`. 

### Windows/*NIX local install
Removing the local installs is as easy as removing the directories where you dropped the binaries. 

