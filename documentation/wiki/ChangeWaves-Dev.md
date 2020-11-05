⚠ This doc is intended for internal teams.

# What are Change Waves?
A Change Wave is a set of risky features developed under the same opt-out flag. The purpose of this is to warn developers of risky changes that will become standard functionality down the line. If there's something we think is worth the risk, we found that Change Waves were a good middle ground between making necessary changes and warning customers of what will soon be permanent.

## Why Opt-Out vs. Opt-In?
Opt-out is a better approach for us because we'd likely get limited feedback when a feature impacts customer builds. When a feature does impact a customer negatively, it's a quick switch to disable and allows time to adapt. The key aspect to Change Waves is that it smooths the transition for customers adapting to risky changes that the MSBuild team feels strongly enough to take.

## How do They Work?
The opt-out comes in the form of setting the environment variable `MSBuildDisableFeaturesFromVersion` to the Change Wave (or version) that contains the feature you want **disabled**. This version happens to be the version of MSBuild that the features were developed for. See the mapping of change waves to features below.

## Choosing a Change Wave for a New Feature
This is determined on a case by case basis and should be discussed with the MSBuild team. A safe bet would be to check our [currently active Change Waves](ChangeWaves.md#change-waves-&-associated-features) and pick the version after the latest MSBuild version. This version corresponds to the latest version of Visual Studio.

# Developing With Change Waves in Mind
For the purpose of providing an example, the rest of this document assumes we're developing a feature for MSBuild version **17.4**.

The Process:
1. Develop your feature.
2. [Create the Change Wave](#creating-a-change-wave) (if necessary)
3. [Condition your feature on that Change Wave](#condition-your-feature-on-a-change-wave)
4. [Test your feature](#test-your-feature)
5. [Document it](ChangeWaves.md#change-wave-features)
6. [Delete the wave as it cycles out](#change-wave-'end-of-lifespan'-procedure)

## Creating a Change Wave
1. In the `Microsoft.Build` project, open `SharedUtilities\ChangeWaves.cs`.
2. Add a static readonly Version to identify the new wave, following the format:
```c#
public static readonly Version Wave17_4 = "17.4";
```
3. You may need to delete the lowest wave as new waves get added.
4. Update the AllWaves array appropriately.
```c#
public static readonly Version[] AllWaves = { Wave16_10, Wave17_0, Wave17_4 };
```

## Condition Your Feature On A Change Wave
Surround your feature with the following:
```c#
    // If you pass an incorrectly formatted change wave, this will throw.
    // Use the readonly Version that was created in the previous step.
    if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_4))
    {
        <your feature>
    }
```

If you need to condition a Task or Target, use the built in `AreFeaturesEnabled` function.
```xml
<Target Name="SomeRiskyChange" Condition="$([MSBuild]::AreFeaturesEnabled('17.4'))"">
<!-- Where '17.4' is the change wave assigned to your feature. -->
```

## Test Your Feature
Create tests as you normally would. Include one test with environment variable `MSBuildDisableFeaturesFromVersion` set to `ChangeWaves.Wave17_4`. Set this like so:
```c#
TestEnvironment env = TestEnvironment.Create()

env.SetChangeWave(ChangeWaves.Wave17_4);
```
When the TestEnvironment is disposed, it handles special logic to properly reset Change Waves for future tests.

**Important!** If you need to build a project to test your feature (say, for tasks or targets), build via `ProjectCollection` in your test.

Example:
```c#
using (TestEnvironment env = TestEnvironment.Create())
{
    // Important: use the version here
    env.SetChangeWave(ChangeWaves.Wave17_4);

    string projectFile = @"
        <Project>
            <Target Name='HelloWorld' Condition=""$([MSBuild]::AreFeaturesEnabled('17.4'))"">
                <Message Text='Hello World!'/>
            </Target>
        </Project>";

    TransientTestFile file = env.CreateFile("proj.csproj", projectFile);

    ProjectCollection collection = new ProjectCollection();
    MockLogger log = new MockLogger();
    collection.RegisterLogger(log);

    collection.LoadProject(file.Path).Build().ShouldBeTrue();
    log.AssertLogContains("Hello World!");
}
```

## Change Wave 'End-of-Lifespan' Procedure
These features will eventually become standard functionality. When a change wave rotates out, do the following:
1. Start by deleting the readonly `Wave17_4` that was created in [Creating a Change Wave](#creating-a-change-wave).
2. Remove `ChangeWave.AreFeaturesEnabled` or `[MSBuild]::AreFeaturesEnabled` conditions surrounding features that were assigned that change wave.
3. Remove tests associated with ensuring features would not run if this wave were set.
4. Clear all other issues that arose from deleting the version.
