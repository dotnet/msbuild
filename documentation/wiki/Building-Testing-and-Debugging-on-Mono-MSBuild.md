MSBuild can be successfully built on Windows, OS X 10.11, Ubuntu 14.04, and Ubuntu 16.04.

Mono maintains a fork of msbuild (for now) at `https://github.com/mono/msbuild/`. You can clone that and use the `xplat-master` branch or `mono-2018-04` for the next release branch.

# Unix #

**Required packages for OSX & Ubuntu**

MSBuild currently builds with .NET Core 2.1. It requires the [.NET Core prerequisites](https://github.com/dotnet/core/blob/master/Documentation/prereqs.md), which you can acquire manually or easily get by [installing the .NET Core SDK](https://dot.net/core).

* [Mono](http://www.mono-project.com/download/) when doing a Mono-hosted version of MSBuild

**Required packages for Ubuntu**
* [libunwind](http://www.nongnu.org/libunwind/index.html) is required by .NET Core. Install it using `sudo apt-get install libunwind8`

## Build process ##

```make```

## Tests ##

```make test-mono```

## Installing ##

`./install-mono-prefix.sh </your/mono/prefix>`

## Getting Mono MSBuild binaries without building the code ##
The best way to get Mono MSBuild for OSX/macOS is to get the official [Mono package](http://www.mono-project.com/download/#download-mac). After installing it, you can run `msbuild`.
<br/>
For Linux, you can install mono and msbuild from [here](http://www.mono-project.com/download/#download-lin).