MSBuild can be successfully built on Windows, OS X 10.11, Ubuntu 14.04, and Ubuntu 16.04.

Mono maintains a fork of msbuild (for now) at `https://github.com/mono/msbuild/`. You can clone that and use the `xplat-master` branch or `mono-2018-04` for the next release branch.

# Unix #

**Required packages for OSX & Ubuntu**

MSBuild requires a stable version of [Mono](http://www.mono-project.com/download/) to build itself.

## Build process ##

```make```

If you encounter errors, see [Something's wrong in my build](Something's-wrong-in-my-build.md)

## Tests ##

```make test-mono```

## Installing ##

`./install-mono-prefix.sh </your/mono/prefix>`

## Getting Mono MSBuild binaries without building the code ##
The best way to get Mono MSBuild for OSX/macOS is to get the official [Mono package](http://www.mono-project.com/download/#download-mac). After installing it, you can run `msbuild`.
<br/>
For Linux, you can install mono and msbuild from [here](http://www.mono-project.com/download/#download-lin).
