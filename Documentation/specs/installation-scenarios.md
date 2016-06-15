# Notes
This section describes placeholders used inside this spec.

| Placeholder | Description |
| ---: | :--- |
| `<Channel>` | `(future|preview|production)`. [See more info](#channels) |
| `<Version>` | version string |
| `<OSName>`  | `(win|ubuntu|rhel|osx|debian)` - code for OS name |
| `<LowestSupportedOSVersion>` | Lowest supported OS Version |
| `<Architecture>` | Processor architecture related to binaries produced |
| `<Extension>` | File extension. This will be described in more details later for each OS separately. |
| `<OSID>` | Abbreviation for: `<OSName><LowestSupportedOSVersion>.<Architecture>`. [See more info](#osid) |
| `<VersionPointer>` | `(latest|lkg)` |
| `<ExecutableExtension>` | Executable extension including dot specific to OS (can be empty string) |
| `<CommitHash>` | Commit hash related to state of repository from where build with specific `<Version>` was build |
| `<DebianPackageName>` | Name of the debian package. [See more info](#debian-feed-relation) |

# Build Output
Each official, successful build should create and upload packages to location described by following URLs:
```
https://dotnetcli.blob.core.windows.net/dotnet/<Channel>/<Version>/dotnet.<OSID>.<Version>.<Extension>

Currently:
https://dotnetcli.blob.core.windows.net/dotnet/<Channel>/Binaries/<Version>/dotnet-sharedframework-<OSName>-<Architecture>.<Version>.zip
https://dotnetcli.blob.core.windows.net/dotnet/<Channel>/Binaries/<Version>/dotnet-host-<OSName>-<Architecture>.<Version>.zip
https://dotnetcli.blob.core.windows.net/dotnet/<Channel>/Binaries/<Version>/dotnet-<OSName>-<Architecture>.<Version>.zip
```
Content of the package should contain binaries which layout will be described later.

Additionally each build should update `latest` [version descriptors](#version-descriptors)

## Windows output

Nuget - WIP, this should include versioning scheme

| `<Extension>` | Description |
| --- | :--- |
| exe | Installer bundle. It should be used by end customers |
| zip | Packed binaries. Used by [installation script](#installation-scripts) |
| symbols.zip | Packed binaries with included symbols. See [symbol packages](#symbol-packages) |

### Including dotnet cli installer as part of your bundle

In order to install dotnet cli with other installer you need an MSI package. To get MSI, download exe bundle and extract it using `dark` tool which is part of [WiX Toolset](http://wixtoolset.org):
```
dark.exe -x <FolderWhereToExtract> <InstallerPath>
```

## OSX output

| `<Extension>` | Description |
| --- | :--- |
| pkg | WIP |
| tar.gz | Packed binaries. Used by [installation script](#installation-scripts) |
| symbols.tar.gz | Packed binaries with included symbols. See [symbol packages](#symbol-packages) |

## Ubuntu output

| `<Extension>` | Description |
| --- | :--- |
| deb | Debian package. This package is being pushed to a [debian feed](#debian-feed) |
| tar.gz | Packed binaries. Used by [installation script](#installation-scripts) |
| symbols.tar.gz | Packed binaries with included symbols. See [symbol packages](#symbol-packages) |

## RedHat/CentOS output

| `<Extension>` | Description |
| --- | :--- |
| tar.gz | Packed binaries. Used by [installation script](#installation-scripts) |
| symbols.tar.gz | Packed binaries with included symbols. See [symbol packages](#symbol-packages) |

## Debian output

| `<Extension>` | Description |
| --- | :--- |
| tar.gz | Packed binaries. Used by [installation script](#installation-scripts) |
| symbols.tar.gz | Packed binaries with included symbols. See [symbol packages](#symbol-packages) |

## Example build output links
WIP

## Questions
- Should <Version> include channel name to avoid situation where you have two files on your computer and latest file might have lower version than the newest?

# Obtaining dotnet

## Installation scripts

Installation script is a shell script which lets customers install dotnet.

For Windows we are using PowerShell script (install-dotnet.ps1).
For any other OS we are using bash script (install-dotnet.sh)

WIP: Exact script action description.

### Script arguments description

| PowerShell/Bash script | Bash script only | Default | Description |
| --- | --- | --- | --- |
| -Channel | --channel | production | Which [channel](#channels) to install from. Possible values: `future`, `preview`, `production` |
| -Version | --version | `global.json` or `latest` | `global.json` currently not supported |
| -InstallDir | --prefix | Windows: `%LocalAppData%\Microsoft\.dotnet` | Path to where install dotnet. Note that binaries will be placed directly in a given directory. |
| -Architecture | ~~--architecture~~ | auto | Possible values: `auto`, `x64`, `x86`. `auto` refers to currently running OS architecture. This switch is currently not supported in bash scripts. |
| -DebugSymbols | --debug-symbols | `<not set>` | If switch present, installation will include debug symbol |
| -DryRun | --dry-run | `<not set>` | If switch present, installation will not be performed and instead deterministic invocation with specific version and zip location will be displayed. |
| -NoPath | --no-path | `<not set>` | If switch present the script will not set PATH environmental variable for the current process. |
| -Verbose | --verbose | `<not set>` | If switch present displays diagnostics information. |
| -AzureFeed | --azure-feed | See description | Azure feed URL, default: `https://dotnetcli.blob.core.windows.net/dotnet` |

### Script location
WIP: permanent link for obtaining latest version
WIP: versioning description
Newest version of the scripts can be found in the repository under following directory:
```
https://github.com/dotnet/cli/tree/rel/1.0.0/scripts/obtain
```

Older version of the script can be obtained using:
```
https://github.com/dotnet/cli/blob/<commit_hash>/scripts/obtain
```

## Getting started page
WIP

## Repo landing page
WIP

# Version descriptors
## Version pointers
Version pointers represent URLs to the latest and Last Known Good (LKG) builds.
Specific URLs TBD. This will be something similar to following:
```
<Domain>/dotnet/<Channel>/<VersionPointer>.<OSID>.version
```

`<Domain>` TBD

## Version files
Version files can be found in multiple places:
- Package: relative path inside the package ./.version
- Latest/LKG version file: WIP

URL:
```
https://dotnetcli.blob.core.windows.net/dotnet/<Channel>/<VersionPointer>.<OSID>.version
```

### File content
Each version file contains two lines describing the build:
```
<CommitHash>
<Version>
```

## Version badge
Version badge (SVG) is an image with textual representation of `<Version>`. It can be found under following URL:
```
https://dotnetcli.blob.core.windows.net/dotnet/<Channel>/<VersionPointer>.<OSID>.svg
```

## Questions/gaps
- Version Pointer links should be permanent and hosted on a separate domain

# Package content
Currently package is required to contain two files:
- .version - [version file](#version-file)
- dotnet<ExecutableExtension> - entry point for all dotnet commands

## Disk Layout
```
.\
    .version
    bin\
        dotnet<ExecutableExtension>
```

# Channels
Currently we have 3 channels which gives us idea about stability and quality of our product.

| Channel name | Description |
| :---: | :--- |
| future | This channel can contain new features which may not be fully complete. This is usually most unstable channel. |
| preview | This channel is in the process of stablization. Most of the bugs and gaps are known. No new features expected. |
| production | This is the most stable channel. Features and gaps are known. No breaking changes can be expected. This channel will only be producing new versions on hotfixes. |

## Github branches relation

Each branch on each successful build produces packages described in [build output](#build-output). Mapping between branches and channel name can be found in the table below:

| Channel name | Github branch |
| :---: | :--- |
| future | master |
| preview | rel/1.0.0 |
| production | N/A, prod? |

## Debian feed relation

After each successful build package is being pushed to the debian feed. More information on debian feed can be found [here](#debian-feed).

| Channel name | `<DebianPackageName>` |
| :---: | :--- |
| future | dotnet-future |
| preview | dotnet-preview |
| production | dotnet |

## Nuget semantic version suffix relation
WIP

## Questions
- What is the bar for triggering hotfix?

# OSID

OSID represents abbreviation for:
```
<OSName><LowestSupportedOSVersion>.<Architecture>
```
This naming scheme gives us flexibility to easily create new binaries when OS makes a breaking change without creating confusing names.

In example, we currently put `api-ms-*.dll` files in our binaries. Those files are not needed on Windows 8 and higher. When using name `win7.x64` we can easily decide to get rid of `api-ms-*.dll` in the newest packages and simply call new version `win8.x64` which would mean that from Windows 8 forward those are recommended binaries (there is currently no issue with those files and this should be only treated as an example).

# Debian feed

Newest binaries in debian feed may be delayed due to external issues by up to 24h.

## Obtaining binaries

Add debian feed:
```
sudo sh -c 'echo "deb [arch=amd64] http://apt-mo.trafficmanager.net/repos/dotnet/ trusty main" > /etc/apt/sources.list.d/dotnetdev.list'

sudo apt-key adv --keyserver apt-mo.trafficmanager.net --recv-keys 417A0893

sudo apt-get update
```

Install:
```
sudo apt-get install <DebianPackageName>=<Version>
```

## Questions
- Is debian version compatible with `<Version>` or does it require additional revision number, i.e.: `1.0.0.001598-1`?
- How to differentiate between Debian package for Debian and Debian package for Ubuntu?
- 
# Symbol packages
WIP
