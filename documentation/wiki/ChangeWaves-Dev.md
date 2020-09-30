⚠ This doc is intended for internal teams.

# What are Change Waves?
A Change Wave is a set of risky features developed under the same opt-out flag. This flag happens to be the version of MSBuild that the features were developed for. The purpose of this is to warn developers of risky changes that will become standard functionality down the line. 

## How do they work?
The opt out comes in the form of setting the environment variable `MSBuildDisableFeaturesFromVersion` to the wave (or version) that contains the feature you want **disabled**. 

**Note:** If  `MSBuildDisableFeaturesFromVersion` is set to `16.8`, this will **disable** all features under `16.8` and **any further versions**.

## What Are the Current Change Waves & Associated Features?
See the mapping of change waves to features [here](ChangeWaves.md#change-waves-&-associated-features).

# Developing With Change Waves in Mind
For the purpose of providing an example, the rest of this document assumes we're developing a feature for MSBuild version **17.4**.

The Process:
1. Develop your feature.
2. [Create the change wave](#creating-a-change-wave) (if necessary)
3. [Check if your feature is enabled](#checking-if-a-change-wave-is-enabled)
4. [Test your feature](#test-your-feature)
5. [Document it](ChangeWaves.md#change-wave-features)
6. [Delete the wave as it cycles out](#change-wave-'end-of-lifespan'-procedure)

## Creating a Change Wave
1. In the `Microsoft.Build` project, open `SharedUtilities\ChangeWaves.cs`.
2. Add a `Version` to identify the new wave, following the format:
```c#
public static readonly Version Wave16_10 = new Version(16, 10);
public static readonly Version Wave17_0 = new Version(17, 0);
public static readonly Version Wave17_4 = new Version(17, 4);
```
3. You may need to delete the lowest wave as new waves get added.
4. As you rotate in/out change waves, be sure to update the AllWaves array appropriately.
```c#
public static readonly Version[] AllWaves = { Wave16_10, Wave17_0, Wave17_4 };
```

## Checking If A Change Wave is Enabled
Surround your feature with the following:
```c#
    // Use the static Versions that exist within the ChangeWaves class.
    if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_4))
    {
        <your feature>
    }
```

If you need to condition a Task or Target, use the intrinsic function `AreFeaturesEnabled`.
```xml
<Target Name="SomeRiskyChange" Condition="$([MSBuild]::AreFeaturesEnabled('17.4'))"">
<!-- Where '17.4' is the change wave assigned to your feature. -->
```

## Test Your Feature
Create tests as you normally would. Include one test with environment variable `MSBuildDisableFeaturesFromVersion` set to `ChangeWaves.Wave17_4`. Ensure your feature does **not** run when set.

**Important!** If you need to build a project to test your feature (say, for tasks or targets), build via `ProjectCollection` in your test.

Example:
```c#
using (TestEnvironment env = TestEnvironment.Create())
{
    // Important: use the constant here
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

## Change Wave 'End of Lifespan' Procedure
These features will eventually become standard functionality. When a change wave rotates out, do the following:
1. Start by deleting the static Version `Wave17_4` that was created in step one.
2. Remove `ChangeWave.AreFeaturesEnabled` or `$([MSBuild]::AreFeaturesEnabled('17.4'))` conditions surrounding features that were assigned that change wave.
3. Remove the tests that use the static Version.
4. Clear all other issues that arose from deleting the static Version.