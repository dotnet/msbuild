# What are Change Waves?
A Change Wave is a set of risky features developed under the same opt-out flag. This flag happens to be the version of MSBuild that the features were developed for. The purpose of this is to warn developers of risky changes that will become standard functionality down the line. 

## How do they work?
The opt out comes in the form of setting the environment variable `MSBuildDisableFeaturesFromVersion` to the wave (or version) that contains the feature you want **disabled**. See the mapping of change waves to features below.

**Note:** If  `MSBuildDisableFeaturesFromVersion` is set to `16.8`, this will **disable** all features under that `16.8` and **any further versions**.

## MSBuildDisableFeaturesFromVersion Values
- If `MSBuildDisableFeaturesFromVersion` is not set, all change waves will **be enabled**
- If `MSBuildDisableFeaturesFromVersion` is set to some out of bounds version (see current rotation of waves below), you will be defaulted to the lowest wave. This will **disable all waves**.
- If `MSBuildDisableFeaturesFromVersion` is set to some invalid format (ex: 16x8, 17_0), all change waves will **be enabled**.

# Change Waves & Associated Features

## Current Rotation of Change Waves
### 16.8
- [Enable NoWarn](https://github.com/dotnet/msbuild/pull/5671)
- [Truncate Target/Task skipped log messages to 1024 chars](https://github.com/dotnet/msbuild/pull/5553)
- [Don't expand full drive globs with false condition](https://github.com/dotnet/msbuild/pull/5669)
### 16.10

### 17.0

## Change Waves No Longer In Rotation