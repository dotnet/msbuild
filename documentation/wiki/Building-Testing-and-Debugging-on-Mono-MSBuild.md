MSBuild can be successfully built on Windows, OS X 10.11, Ubuntu 14.04, and Ubuntu 16.04.

Mono maintains a fork of msbuild (for now) at `https://github.com/mono/msbuild/`. You can clone that and use the `xplat-master` branch or `mono-2017-10` for the next release branch.

# Unix #

**Required packages for OSX & Ubuntu**

MSBuild currently builds with a prerelease version of .NET Core 1.0. It requires the [.NET Core prerequisites](https://github.com/dotnet/core/blob/master/Documentation/prereqs.md), which you can acquire manually or easily get by [installing the .NET Core SDK](https://dot.net/core).

* *OpenSSL*: MSBuild uses the .Net CLI to download Nuget packages during its build process. The CLI requires a recent OpenSSL library available in `/usr/lib`. This can be downloaded using [brew](http://brew.sh/) on OS X (`brew install openssl`) and apt-get (`apt-get install openssl`) on Ubuntu, or [building from source](https://wiki.openssl.org/index.php/Compilation_and_Installation#Mac). If you use a different package manager and see an error that says `Unable to load DLL 'System.Security.Cryptography.Native'`, `dotnet` may be looking in the wrong place for the library.

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