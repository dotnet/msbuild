# General principles that apply to the installs
- This doc is represnting ideal state and not the current state
- Only HTTPS links are allowed in any online property 
- All installers are signed properly 
- All user facing materials point to the getting started page
- The user needs extra effort to install the "bleeding edge" bits (nightly build)

# Landing Pages
[Getting Started Page](https://aka.ms/dotnetcoregs) - for customers
[Repo landing page](https://github.com/dotnet/cli/blob/rel/1.0.0/README.md) - for contributors

## Getting Started Page
The page can be found on https://aka.ms/dotnetcoregs. 

* Installation targets: native installers (bits) & "curl&run"
* Source branch: rel/1.0.0
* Debian feed: (now: Development; future: Production)
* Installation script links: Latest from rel/1.0.0
* Getting started page can never point to unstable builds
* URLs point to: latest green build of rel/1.0.0;

This is the main curated first-run experience for the dotnet CLI. The intent of the page is to help users "kick the tires" quickly and become familiar with what the platform offers. This should be the most stable and curated experience we can offer. 

## Repo Landing Page
The repo landing page can be found on: https://github.com/dotnet/cli/readme.md
* Source branch: rel/1.0.0
* Links to builds: LKG ?? latest green build of rel/1.0.0 (build status)
* The Repo Landing Page should be used primarily by contributors to the CLI
* Only links to "how to install" but no instructions
* First thing for "you want to get started" section should link to the getting started page on the marketing site
* Separate page that has instructions on how to install both the latest stable as well as latest development with proper warnings around it. The separate page is to really avoid the situation from people accidentally installing unstable bits (since SEO can drop them in the repo first). 

# Installation modes

## Interactive installation (native installers)
These installation experiences are the primary way new users are getting the bits. They are aimed towards users kicking the tires. They are found using (not not limited to) the following means:

* Web searches
* Marketing materials
* Presentations
* Documentation

The primary way to get information about this mode of installation is the marketing website. 

The native installers are:

* Deb packages for Debian-based distros
* RPM packages for RH-based distros
* Bundles for Windows
* PKG for OS X
* Homebrew for OS X

## Curl & paste installation script
### install.sh/install.ps1
This approach will be used for the below scenarios:

* Local installation (consumed by build scripts)
* Global installation (consumed by users who like command line)
* Copy & paste script: downloads and executes installation script

**TODO:** add actual commands for both Unix and Windows. 

## Docker
* Link to docker hub page
* Docker images are used either as a base or as small development envs for trying out the bits
* Docker image should have stable bits
* Should point to the "stable" tag

## Acquiring through other products (VS, VS Code)
Visual Studio will chain the native installer. The version we give them is from the rel/1.0.0 branch. 

VS Code extension will toast/point people to the installers (getting started page). 
