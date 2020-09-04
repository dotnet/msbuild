# MSBuild Change Waves For Devs

## What are Change Waves?
Sometimes we want to make a breaking change _and_ give customers a heads up as to what's breaking. So we develop the change and give them an opt-out while letting them know that this will become a standard feature down the line.

## Developing With Change Waves in Mind
For the purpose of providing an example, the rest of this document assumes we're developing for MSBuild version **17.4**

It's a 4 step process:
1. Develop your feature.
1. [Create the change wave](#creating-a-change-wave)
1. [Check if your change wave is enabled](#checking-if-a-change-wave-is-enabled)
1. [Test your feature](#test-your-feature)
1. [Delete the wave as it cycles out](#change-wave-'end-of-lifespan'-procedure)

### Creating a Change Wave
1. In the `Microsoft.Build` project, open `SharedUtilities\ChangeWaves.cs`.
1. Add another const string to identify the new wave, following the format:
```c#
    public const string Wave17_4 = "17.4"`
```

### Checking If A Change Wave is Enabled
Surround your feature with the following:
```c#
    // If you pass an incorrectly formatted change wave, this will throw.
    if (ChangeWaves.IsChangeWaveEnabled(ChangeWaves.Wave17_4))
    {
        <your feature>
    }
```
If you need to condition a Task or Target, condition it based on `MSBuildChangeWaveVersion`
```xml
<Target Name="SomeBreakingChange" Condition="$([MSBuild]::VersionGreaterThan('$(MSBuildChangeWaveVersion)', '17.4'))"">
```
**NOTE**: If `MSBuildChangeWaveVersion` is in an invalid format, the build will fail here.

### Test Your Feature
Create tests as you normally would, but include tests with `MSBuildChangeWaveVersion` set to:
1. Some version prior (your feature should NOT run)
1. The same version (your feature should NOT run)
1. Some version after (your feature SHOULD run)
**NOTE:** Don't forget

### Change Wave 'End of Lifespan' Procedure
Your feature will eventually become standard functionality. When a change wave rotates out, do the following:
1. Start by deleting the constant `Wave17_4` that was created in step one.
1. Remove **all** if-statements surrounding features that were assigned that change wave.

# Questions!
1. How do we correctly check for $(MSBUILDCHANGEWAVEVERSION) <= 16.7 when MSBUILDCHANGEWAVEVERSION is 16.10?
 - Done, we use the instrinsic function `MSBuild::VersionGreaterThan` (thanks Nick!)

IDEA: What if we didn't throw a warning in the static `IsChangeWaveEnabled` class? How could we?

Dev calls IsChangeWaveEnabled incorrectly? Fail the build. Explicitly let them know they messed up and should be using the constants.