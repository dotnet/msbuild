# MSBuild Change Waves

## What are Change Waves?
Sometimes we want to make a breaking change _and_ give folks a heads up as to what's breaking. So we develop the change and provide an opt-out while acknowledging that this feature will become standard functionality down the line. This opt out comes in the form of setting the environment variable `MSBuildChangeWaveVersion` to the wave that contains the feature you want **disabled**. See the mapping of features to change waves below.

## How To Disable A Change Wave
Simply set `MSBuildChangeWaveVersion` as an environment variable.

**Note:** Ensure you correctly follow the format `xx.yy`. eg; 16.8, 17.12, etc.

# Change Wave & Associated Features

## 16.8
- [Enable NoWarn](https://github.com/dotnet/msbuild/pull/5671)
- [Truncate Target/Task skipped log messages to 1024 chars](https://github.com/dotnet/msbuild/pull/5553)
- [Don't expand full drive globs with false condition](https://github.com/dotnet/msbuild/pull/5669)
## 16.10


## 17.0
