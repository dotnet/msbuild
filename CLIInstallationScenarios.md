# General rules
- No http links in any readme page (use https)
- All installers signed
- All user facing materials should point to the getting started page

# Landing Pages
[Getting Started Page](https://aka.ms/dotnetcoregs) - for customers
[Repo landing page](https://github.com/dotnet/cli/blob/rel/1.0.0/README.md) - for contributors

## Getting Started Page
https://aka.ms/dotnetcoregs
* Installation targets: native installers & "curl&run"
* Source branch: rel/1.0.0
* Linked builds: LKG ?? latest green build of rel/1.0.0
* Debian feed: Release Nightly
* Installation script links: Latest from rel/1.0.0

This is the main curated first-run experience for the dotnet CLI. The intent of the page is to help users "kick the tires" quickly and become familiar with what the platform offers. This should be the most stable and curated experience we can offer.

## Repo Landing Page
https://github.com/dotnet/cli/readme.md
* Installation targets: native installers & "curl&run" (should be obscured by getting started link: i.e. on the bottom of the page)
* Source branch: rel/1.0.0
* URLs point to: latest green build of rel/1.0.0;

Download links on the landing page should be decreased in importance. First thing for "you want to get started" section should link to the getting started page on the marketing site. 

The Repo Landing Page should be used primarily by contributors to the CLI. There should be a separate page that has instructions on how to install both the latest stable as well as latest development with proper warnings around it. 

## Interactive installation (native installers)
These installation experiences are the primary way new users are getting the bits. They are aimed towards users kicking the tires. They are found using (not not limited to) the following means:

* Web searches
* Marketing materials
* Presentations
* Documentation

## Curl & paste installation script
### install.sh/install.ps1
* Local installation (consumed by build scripts)
* Global installation (consumed by users who like command line)
* Copy&paste script: downloads and executes installation script

## Docker 
Docker images are used either as a base or as small development envs for trying out the bits. We should follow through the above principles with Docker. 

## Acquiring through other products (VS, VS Code)
TODO: acquire through VS (OOB)
TODO: acquire through VS Code (extension) 


