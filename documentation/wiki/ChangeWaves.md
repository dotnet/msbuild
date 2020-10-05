# What are Change Waves?
A Change Wave is a set of risky features developed under the same opt-out flag. This flag happens to be the version of MSBuild that the features were developed for. The purpose of this is to warn developers of risky changes that will become standard functionality down the line.

## How do they work?
The opt out comes in the form of setting the environment variable `MSBuildDisableFeaturesFromVersion` to the Change Wave (or version) that contains the feature you want **disabled**. See the mapping of change waves to features below.

## MSBuildDisableFeaturesFromVersion Values & Outcomes
| `MSBuildDisableFeaturesFromVersion` Value                         | Result        | Receive Warning? |
| :-------------                                                    | :----------   | :----------: |
| Unset                                                             | All Change Waves will be enabled, meaning all features behind each Change Wave will be enabled.               | No   |
| `16.8` (or any valid & current Change Wave)                       | All features behind Change Wave `16.8` and higher will be disabled.                                           | No   |
| Invalid Value (Invalid Change Wave, or out-of-rotation version)   | You will be defaulted to the closest valid value (ascending). Ex: Setting `16.9` will default you to `16.10`. | Yes  |
| Invalid Format (ex: `16x8`, `17_0`, `garbage`)                    | All waves will be enabled.                                                                                    | Yes  |

# Change Waves & Associated Features

## Current Rotation of Change Waves
### 16.8
- [Enable NoWarn](https://github.com/dotnet/msbuild/pull/5671)
- [Truncate Target/Task skipped log messages to 1024 chars](https://github.com/dotnet/msbuild/pull/5553)
- [Don't expand full drive globs with false condition](https://github.com/dotnet/msbuild/pull/5669)
### 16.10

### 17.0

## Change Waves No Longer In Rotation