// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.CommandLine;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class TaskCacheSwitch_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    public static bool IsSupported => MSBuildApp.IsTaskCacheSupported;

    [ConditionalTheory(typeof(TaskCacheSwitch_Tests), nameof(IsSupported))]
    [InlineData(true)]
    [InlineData(false)]
    public void EvaluationOnlyQueryReceivesExplicitMode(bool enabled)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = env.CreateFile("evaluation.proj", """
            <Project>
              <PropertyGroup>
                <ModeAtEvaluation>$(MSBuildTaskCacheEnabled)</ModeAtEvaluation>
              </PropertyGroup>
            </Project>
            """).Path;
        string cache = Path.Combine(env.CreateFolder().Path, "unused-cache");
        TextWriter original = Console.Out;
        using StringWriter output = new();
        try
        {
            Console.SetOut(output);
            MSBuildApp.Execute(["msbuild.exe", project, $"-taskCache:{enabled}",
                $"-p:MSBuildTaskCacheEnabled={!enabled}", $"-buildCacheDirectory:{cache}",
                "-getProperty:ModeAtEvaluation", "-noAutoResponse", "-nr:false"])
                .ShouldBe(MSBuildApp.ExitType.Success);
        }
        finally
        {
            Console.SetOut(original);
        }
        output.ToString().Trim().ShouldBe(enabled ? "true" : "false");
        Directory.Exists(cache).ShouldBeFalse();
    }

    [Fact]
    public void ExplicitCacheModeMatchesBackendAvailability()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string cacheDirectory = env.CreateFolder().Path;
        string project = env.CreateFile("availability.proj", "<Project><Target Name=\"Build\" /></Project>").Path;
        MSBuildApp.Execute(["msbuild.exe", project, "-taskCache", $"-buildCacheDirectory:{cacheDirectory}", "-noAutoResponse", "-nr:false"])
            .ShouldBe(MSBuildApp.IsTaskCacheSupported ? MSBuildApp.ExitType.Success : MSBuildApp.ExitType.SwitchError);
    }

    [Theory]
    [InlineData("-question")]
    [InlineData("-inputResultsCaches:unused.cache")]
    [InlineData("-outputResultsCache:unused.cache")]
    public void RejectsConflictingSwitches(string conflict)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = env.CreateFile("cache.proj", "<Project><Target Name=\"Build\" /></Project>").Path;
        MSBuildApp.Execute(["msbuild.exe", project, "-taskCache", conflict, "-noAutoResponse"]).ShouldBe(MSBuildApp.ExitType.SwitchError);
    }

    [ConditionalTheory(typeof(TaskCacheSwitch_Tests), nameof(IsSupported))]
    [InlineData("-taskCache")]
    [InlineData("-taskCache:true")]
    public void CliPreservesTimestampSkipping(string option)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        env.CreateFile(folder, "input.txt", "input");
        string project = env.CreateFile(folder, "cache.proj", """
            <Project>
              <Target Name="Build" Inputs="input.txt" Outputs="output.txt">
                <Error Text="An up-to-date target must not execute this task." />
              </Target>
            </Project>
            """).Path;
        string destination = Path.Combine(folder.Path, "output.txt");
        File.WriteAllText(destination, "unchanged");
        File.SetLastWriteTimeUtc(destination, DateTime.UtcNow.AddDays(1));
        MSBuildApp.Execute(["msbuild.exe", project, option, $"-buildCacheDirectory:{env.CreateFolder().Path}", "-warnAsError", "-noAutoResponse", "-nr:false"]).ShouldBe(MSBuildApp.ExitType.Success);
        File.ReadAllText(destination).ShouldBe("unchanged");
    }

    [ConditionalTheory(typeof(TaskCacheSwitch_Tests), nameof(IsSupported))]
    [InlineData(true)]
    [InlineData(false)]
    public void ModeIsAvailableDuringEvaluationAndOverridesUserValue(bool enabled)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        string cacheDirectory = env.CreateFolder().Path;
        string project = env.CreateFile(folder, "mode.proj", """
            <Project>
              <PropertyGroup><ModeAtEvaluation>$(MSBuildTaskCacheEnabled)</ModeAtEvaluation></PropertyGroup>
              <Target Name="Build">
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/mode.txt" Lines="$(ModeAtEvaluation)" Overwrite="true" />
              </Target>
            </Project>
            """).Path;
        MSBuildApp.Execute(["msbuild.exe", project, $"-taskCache:{enabled}", $"-buildCacheDirectory:{cacheDirectory}", $"-p:MSBuildTaskCacheEnabled={!enabled}", "-noAutoResponse", "-nr:false"])
            .ShouldBe(MSBuildApp.ExitType.Success);
        File.ReadAllText(Path.Combine(folder.Path, "mode.txt")).ShouldBe((enabled ? "true" : "false") + Environment.NewLine);
    }

    [ConditionalFact(typeof(TaskCacheSwitch_Tests), nameof(IsSupported))]
    public void DirectoryIsResolvedAtCliAndProjectsCannotChangeStorage()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        string forbidden = env.CreateFile("not-a-cache", "untouched").Path;
        env.SetEnvironmentVariable("MSBUILD_TASK_CACHE_DIR", forbidden);
        env.SetEnvironmentVariable("MSBuildTaskCacheDirectory", forbidden);
        env.SetCurrentDirectory(folder.Path);
        string project = env.CreateFile(folder, "directory.proj", """
            <Project TreatAsLocalProperty="MSBuildTaskCacheDirectory">
              <PropertyGroup><MSBuildTaskCacheDirectory>$(Forbidden)</MSBuildTaskCacheDirectory></PropertyGroup>
              <Target Name="Build"><Message Text="configured" /></Target>
            </Project>
            """).Path;
        MSBuildApp.Execute(["msbuild.exe", project, "-taskCache", "-buildCacheDirectory:relative-cache",
            $"-p:MSBuildTaskCacheDirectory={forbidden}", $"-p:Forbidden={forbidden}", "-noAutoResponse", "-nr:false"]).ShouldBe(MSBuildApp.ExitType.Success);
        Directory.Exists(Path.Combine(folder.Path, "relative-cache")).ShouldBeTrue();
        File.ReadAllText(forbidden).ShouldBe("untouched");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisabledModeDoesNotOpenStorageOrInjectProperties(bool explicitFalse)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        string forbidden = env.CreateFile("not-a-cache", "untouched").Path;
        string project = env.CreateFile(folder, "disabled.proj", """
            <Project>
              <Target Name="Build">
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/mode.txt" Lines="mode:$(MSBuildTaskCacheEnabled)" Overwrite="true" />
              </Target>
            </Project>
            """).Path;
        string[] args = explicitFalse
            ? ["msbuild.exe", project, "-taskCache:false", $"-buildCacheDirectory:{forbidden}", "-noAutoResponse", "-nr:false"]
            : ["msbuild.exe", project, $"-buildCacheDirectory:{forbidden}", "-noAutoResponse", "-nr:false"];
        MSBuildApp.Execute(args).ShouldBe(MSBuildApp.ExitType.Success);
        File.ReadAllText(Path.Combine(folder.Path, "mode.txt")).ShouldBe((explicitFalse ? "mode:false" : "mode:") + Environment.NewLine);
        File.ReadAllText(forbidden).ShouldBe("untouched");
    }

    [Theory]
    [InlineData("-buildCacheDirectory")]
    [InlineData("-buildCacheDirectory:")]
    [InlineData("-buildCacheDirectory:\"\"")]
    public void DirectorySwitchRequiresAnArgument(string option)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = env.CreateFile("missing-directory.proj", "<Project><Target Name=\"Build\" /></Project>").Path;
        MSBuildApp.Execute(["msbuild.exe", project, option, "-noAutoResponse", "-nr:false"]).ShouldBe(MSBuildApp.ExitType.SwitchError);
    }

    [Fact]
    public void WhitespaceDirectoryDoesNotEnableCaching()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string project = env.CreateFile("whitespace-directory.proj", "<Project><Target Name=\"Build\" /></Project>").Path;
        MSBuildApp.Execute(["msbuild.exe", project, "-buildCacheDirectory:\" \t\"", "-noAutoResponse", "-nr:false"])
            .ShouldBe(MSBuildApp.ExitType.Success);
    }

    [ConditionalFact(typeof(TaskCacheSwitch_Tests), nameof(IsSupported))]
    public void LastDirectorySwitchWinsAndQuotedPathIsNotSplit()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        string first = Path.Combine(folder.Path, "unused");
        string selected = Path.Combine(folder.Path, "cache with spaces;and,delimiters");
        string project = env.CreateFile(folder, "directory.proj", "<Project><Target Name=\"Build\" /></Project>").Path;
        MSBuildApp.Execute(["msbuild.exe", project, "-taskCache", $"-buildCacheDirectory:{first}",
            $"-buildCacheDirectory:\"{selected}\"", "-noAutoResponse", "-nr:false"]).ShouldBe(MSBuildApp.ExitType.Success);
        Directory.Exists(first).ShouldBeFalse();
        Directory.Exists(selected).ShouldBeTrue();
    }

    [ConditionalFact(typeof(TaskCacheSwitch_Tests), nameof(IsSupported))]
    public void DirectorySwitchDoesNotSynthesizeGlobalProperties()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildTaskCacheDirectory", null);
        env.SetEnvironmentVariable("BuildCacheDirectory", null);
        string directory = env.CreateFolder().Path;
        string project = env.CreateFile("no-directory-properties.proj", """
            <Project>
              <Target Name="Build">
                <Error Condition="'$(MSBuildTaskCacheDirectory)' != '' or '$(BuildCacheDirectory)' != ''"
                       Text="The directory switch must not synthesize project properties." />
              </Target>
            </Project>
            """).Path;
        MSBuildApp.Execute(["msbuild.exe", project, "-taskCache", $"-buildCacheDirectory:{directory}",
            "-noAutoResponse", "-nr:false"]).ShouldBe(MSBuildApp.ExitType.Success);
    }

    [ConditionalFact(typeof(TaskCacheSwitch_Tests), nameof(IsSupported))]
    public void ResponseFileDirectoryUsesExistingCommandLinePrecedence()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        string unused = Path.Combine(folder.Path, "response cache");
        string selected = Path.Combine(folder.Path, "command cache");
        string response = env.CreateFile(folder, "cache.rsp", $"-buildCacheDirectory:\"{unused}\"").Path;
        string project = env.CreateFile(folder, "response.proj", "<Project><Target Name=\"Build\" /></Project>").Path;
        MSBuildApp.Execute(["msbuild.exe", project, "-taskCache", "@" + response,
            $"-buildCacheDirectory:\"{selected}\"", "-noAutoResponse", "-nr:false"]).ShouldBe(MSBuildApp.ExitType.Success);
        Directory.Exists(unused).ShouldBeFalse();
        Directory.Exists(selected).ShouldBeTrue();
    }
}
