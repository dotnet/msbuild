# MSBuild Change Waves For Devs

## What are Change Waves?
Sometimes we want to make a breaking change _and_ give customers a heads up as to what's breaking. So we develop the change and provide an opt-out while acknowledging that this feature will become a standard functionality down the line.

## Developing With Change Waves in Mind
For the purpose of providing an example, the rest of this document assumes we're developing a feature for MSBuild version **17.4**.

The Process:
1. Develop your feature.
2. [Create the change wave](#creating-a-change-wave)
3. [Check if your change wave is enabled](#checking-if-a-change-wave-is-enabled)
4. [Test your feature](#test-your-feature)
5. [Document it](ChangeWaves.md#change-wave-features)
6. [Delete the wave as it cycles out](#change-wave-'end-of-lifespan'-procedure)

## Creating a Change Wave
1. In the `Microsoft.Build` project, open `SharedUtilities\ChangeWaves.cs`.
1. Add a const string to identify the new wave, following the format:
```c#
    public const string Wave17_4 = "17.4"
```
1. As you rotate in/out change waves, be sure to update the AllWaves array appropriately.
```c#
public static readonly string[] AllWaves = { Wave16_10, Wave17_0, Wave17_4 };
```

## Checking If A Change Wave is Enabled
Surround your feature with the following:
```c#
    // If you pass an incorrectly formatted change wave, this will throw.
    // Use the const string you created in the previous step.
    if (ChangeWaves.IsChangeWaveEnabled(ChangeWaves.Wave17_4))
    {
        <your feature>
    }
```

If you need to condition a Task or Target, condition it based on `MSBuildChangeWaveVersion`
```xml
<Target Name="SomeBreakingChange" Condition="$([MSBuild]::VersionLessThan('17.4', '$(MSBuildChangeWaveVersion)'))"">
<!-- Where '17.4' is the change wave assigned to your feature. -->
```
**NOTE**: If `MSBuildChangeWaveVersion` is in an invalid format, the build will fail when `VersionLessThan` is called.

## Test Your Feature
Create tests as you normally would. Include one test with `MSBuildChangeWaveVersion` set as an environment variable. It should be set to the change wave associated with your feature, and you must verify your feature did not run.

**Important!** If you need to build a project to test your feature (say, for tasks or targets), build via `ProjectCollection` in your test.


Example:
```c#
using (TestEnvironment env = TestEnvironment.Create())
{
    // Important: use the constant here
    env.SetEnvironmentVariable("MSBUILDCHANGEWAVEVERSION", ChangeWaves.Wave17_4);

    string projectFile = @"
        <Project>
            <Target Name='HelloWorld' Condition=""$([MSBuild]::VersionLessThan('17.4', '$(MSBUILDCHANGEWAVEVERSION)'))"">
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
1. Start by deleting the constant `Wave17_4` that was created in step one.
2. Remove ChangeWave conditions surrounding features that were assigned that change wave.
3. Remove tests associated with ensuring features would not run if this wave were set.
4. Clear all other issues that arose from deleting the constant.