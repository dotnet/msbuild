// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Components.Caching;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskResultCache_Tests(ITestOutputHelper testOutput)
{
    [Fact]
    public void RestoresOutputsAndReplaysWarnings()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);
        string inputPath = Path.Combine(projectFolder.Path, "input.txt");
        string outputPath = Path.Combine(projectFolder.Path, "output.txt");
        string projectPath = Path.Combine(projectFolder.Path, "cache.proj");
        File.WriteAllText(inputPath, "first");
        File.WriteAllText(
            projectPath,
            $"""
            <Project>
              <PropertyGroup>
                <WorkspaceRoot>true</WorkspaceRoot>
                <MSBuildContentCacheDirectory>cache</MSBuildContentCacheDirectory>
              </PropertyGroup>
              <UsingTask
                  TaskName="{typeof(TaskResultCacheTestTask).FullName}"
                  AssemblyFile="{SecurityElement.Escape(typeof(TaskResultCacheTestTask).Assembly.Location)}" />
              <ItemGroup>
                <CacheInput Include="input.txt" />
                <CacheOutput Include="output.txt" />
              </ItemGroup>
              <Target Name="Build">
                <TaskResultCacheTestTask
                    Input="@(CacheInput)"
                    OutputFile="@(CacheOutput)"
                    Marker="!"
                    DeclaredInputs="@(CacheInput)"
                    DeclaredOutputs="@(CacheOutput)" />
              </Target>
            </Project>
            """);

        MockLogger firstLogger = BuildCacheProject(projectPath);
        firstLogger.AssertNoErrors();
        firstLogger.WarningCount.ShouldBe(1);
        File.ReadAllText(outputPath).ShouldBe("first!");
        firstLogger.FullLog.ShouldContain("Content cache miss");

        File.Delete(outputPath);

        MockLogger secondLogger = BuildCacheProject(projectPath);
        secondLogger.AssertNoErrors();
        secondLogger.WarningCount.ShouldBe(1);
        File.ReadAllText(outputPath).ShouldBe("first!");
        secondLogger.FullLog.ShouldContain("Content cache hit");

        File.WriteAllText(
            projectPath,
            File.ReadAllText(projectPath).Replace(
                "Marker=\"!\"",
                "Marker=\"?\""));
        File.Delete(outputPath);

        MockLogger parameterChangeLogger = BuildCacheProject(projectPath);
        parameterChangeLogger.AssertNoErrors();
        parameterChangeLogger.WarningCount.ShouldBe(1);
        File.ReadAllText(outputPath).ShouldBe("first?");
        parameterChangeLogger.FullLog.ShouldContain("Content cache miss");

        File.WriteAllText(inputPath, "second");
        File.Delete(outputPath);

        MockLogger thirdLogger = BuildCacheProject(projectPath);
        thirdLogger.AssertNoErrors();
        thirdLogger.WarningCount.ShouldBe(1);
        File.ReadAllText(outputPath).ShouldBe("second?");
        thirdLogger.FullLog.ShouldContain("Content cache miss");
    }

    [Theory]
    [InlineData("cache", "", "cache")]
    [InlineData("cache", "false", "cache")]
    [InlineData("", "", null)]
    [InlineData("", "false", null)]
    public void DirectoryResolution(
        string configuredDirectory,
        string enabled,
        string? expected)
    {
        TaskResultCacheSession.ResolveCacheDirectory(
            configuredDirectory,
            enabled).ShouldBe(expected);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public void EnabledCacheUsesUserCacheDirectory(string enabled)
    {
        TaskResultCacheSession.ResolveCacheDirectory("", enabled)
            .ShouldBe(TaskResultCacheSession.GetDefaultCacheDirectory());
    }

    [Theory]
    [InlineData("", true, 10_485_760_000)]
    [InlineData("0", true, 0)]
    [InlineData("1", true, 1_048_576)]
    [InlineData("-1", false, 0)]
    [InlineData("invalid", false, 0)]
    [InlineData("9223372036854775807", false, 0)]
    public void SizeResolution(
        string configuredSizeMB,
        bool expectedSuccess,
        long expectedSize)
    {
        TaskResultCacheSession.TryResolveCacheSizeBytes(
            configuredSizeMB,
            out long size).ShouldBe(expectedSuccess);
        size.ShouldBe(expectedSize);
    }

    [Fact]
    public void EvictsLeastRecentlyUsedEntries()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);
        string inputPath = Path.Combine(projectFolder.Path, "input.txt");
        string outputPath = Path.Combine(projectFolder.Path, "output.txt");
        string cachePath = Path.Combine(projectFolder.Path, "cache");
        string projectPath = Path.Combine(projectFolder.Path, "cache.proj");
        File.WriteAllText(
            projectPath,
            $"""
            <Project>
              <PropertyGroup>
                <WorkspaceRoot>true</WorkspaceRoot>
                <MSBuildContentCacheDirectory>cache</MSBuildContentCacheDirectory>
                <MSBuildContentCacheSizeMB>1</MSBuildContentCacheSizeMB>
              </PropertyGroup>
              <UsingTask
                  TaskName="{typeof(TaskResultCacheTestTask).FullName}"
                  AssemblyFile="{SecurityElement.Escape(typeof(TaskResultCacheTestTask).Assembly.Location)}" />
              <ItemGroup>
                <CacheInput Include="input.txt" />
                <CacheOutput Include="output.txt" />
              </ItemGroup>
              <Target Name="Build">
                <TaskResultCacheTestTask
                    Input="@(CacheInput)"
                    OutputFile="@(CacheOutput)"
                    DeclaredInputs="@(CacheInput)"
                    DeclaredOutputs="@(CacheOutput)" />
              </Target>
            </Project>
            """);

        string firstContents = new('a', 700_000);
        File.WriteAllText(inputPath, firstContents);
        MockLogger firstLogger = BuildCacheProject(projectPath);
        firstLogger.AssertNoErrors();
        firstLogger.FullLog.ShouldContain("Content cache miss");

        string firstManifest = Directory.GetFiles(
            cachePath,
            "manifest.bin",
            SearchOption.AllDirectories).ShouldHaveSingleItem();
        string firstEntryDirectory = Path.GetDirectoryName(firstManifest)!;
        File.SetLastWriteTimeUtc(firstManifest, DateTime.UtcNow.AddHours(-1));
        File.Delete(Path.Combine(cachePath, ".trim.lock"));

        File.WriteAllText(inputPath, new string('b', 700_001));
        File.Delete(outputPath);
        MockLogger secondLogger = BuildCacheProject(projectPath);
        secondLogger.AssertNoErrors();
        secondLogger.FullLog.ShouldContain("Content cache miss");
        Directory.Exists(firstEntryDirectory).ShouldBeFalse();
        Directory.GetFiles(cachePath, "manifest.bin", SearchOption.AllDirectories)
            .ShouldHaveSingleItem();

        File.WriteAllText(inputPath, firstContents);
        File.Delete(outputPath);
        MockLogger thirdLogger = BuildCacheProject(projectPath);
        thirdLogger.AssertNoErrors();
        thirdLogger.FullLog.ShouldContain("Content cache miss");
    }

    [Fact]
    public void RestoresAbsentOutputsAndRecoversFromCorruption()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);
        string inputPath = Path.Combine(projectFolder.Path, "input.txt");
        string outputPath = Path.Combine(projectFolder.Path, "output.txt");
        string cachePath = Path.Combine(projectFolder.Path, "cache");
        string projectPath = Path.Combine(projectFolder.Path, "cache.proj");
        File.WriteAllText(inputPath, "input");
        File.WriteAllText(
            projectPath,
            $"""
            <Project>
              <PropertyGroup>
                <WorkspaceRoot>true</WorkspaceRoot>
                <MSBuildContentCacheDirectory>cache</MSBuildContentCacheDirectory>
              </PropertyGroup>
              <UsingTask
                  TaskName="{typeof(TaskResultCacheTestTask).FullName}"
                  AssemblyFile="{SecurityElement.Escape(typeof(TaskResultCacheTestTask).Assembly.Location)}" />
              <ItemGroup>
                <CacheInput Include="input.txt" />
                <CacheOutput Include="output.txt" />
              </ItemGroup>
              <Target Name="Build">
                <TaskResultCacheTestTask
                    Input="@(CacheInput)"
                    OutputFile="@(CacheOutput)"
                    WriteOutput="false"
                    DeclaredInputs="@(CacheInput)"
                    DeclaredOutputs="@(CacheOutput)" />
              </Target>
            </Project>
            """);

        MockLogger firstLogger = BuildCacheProject(projectPath);
        firstLogger.AssertNoErrors();
        File.Exists(outputPath).ShouldBeFalse();
        firstLogger.FullLog.ShouldContain("Content cache miss");

        File.WriteAllText(outputPath, "stale");

        MockLogger secondLogger = BuildCacheProject(projectPath);
        secondLogger.AssertNoErrors();
        File.Exists(outputPath).ShouldBeFalse();
        secondLogger.FullLog.ShouldContain("Content cache hit");

        string manifestPath = Directory.GetFiles(
            cachePath,
            "manifest.bin",
            SearchOption.AllDirectories).ShouldHaveSingleItem();
        File.WriteAllText(manifestPath, "corrupt");
        File.WriteAllText(outputPath, "stale");

        MockLogger thirdLogger = BuildCacheProject(projectPath);
        thirdLogger.AssertNoErrors();
        File.Exists(outputPath).ShouldBeFalse();
        thirdLogger.FullLog.ShouldContain("The content cache could not be used");
        thirdLogger.FullLog.ShouldContain("Content cache miss");
    }

    [Fact]
    public async Task AllowsConcurrentMissesAndStoresValidEntry()
    {
        string originalCurrentDirectory = Directory.GetCurrentDirectory();
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        try
        {
            TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);
            string inputPath = Path.Combine(projectFolder.Path, "input.txt");
            string outputPath = Path.Combine(projectFolder.Path, "output.txt");
            string synchronizationDirectory = Path.Combine(projectFolder.Path, "synchronization");
            string projectPath = Path.Combine(projectFolder.Path, "cache.proj");
            File.WriteAllText(inputPath, "input");
            File.WriteAllText(
                projectPath,
                $"""
                <Project>
                  <PropertyGroup>
                    <WorkspaceRoot>true</WorkspaceRoot>
                    <MSBuildContentCacheDirectory>cache</MSBuildContentCacheDirectory>
                  </PropertyGroup>
                  <UsingTask
                      TaskName="{typeof(TaskResultCacheTestTask).FullName}"
                      AssemblyFile="{SecurityElement.Escape(typeof(TaskResultCacheTestTask).Assembly.Location)}" />
                  <ItemGroup>
                    <CacheInput Include="input.txt" />
                    <CacheOutput Include="output.txt" />
                  </ItemGroup>
                  <Target Name="Build">
                    <TaskResultCacheTestTask
                        Input="@(CacheInput)"
                        OutputFile="@(CacheOutput)"
                        SynchronizationDirectory="{SecurityElement.Escape(synchronizationDirectory)}"
                        DeclaredInputs="@(CacheInput)"
                        DeclaredOutputs="@(CacheOutput)" />
                  </Target>
                </Project>
                """);

            Task<MockLogger> firstBuild =
                Task.Run(() => BuildCacheProject(projectPath));
            Task<MockLogger> secondBuild =
                Task.Run(() => BuildCacheProject(projectPath));
            MockLogger[] loggers = await Task.WhenAll(firstBuild, secondBuild);

            loggers[0].AssertNoErrors();
            loggers[1].AssertNoErrors();
            File.ReadAllText(outputPath).ShouldBe("input");

            File.Delete(outputPath);
            MockLogger thirdLogger = BuildCacheProject(projectPath);

            thirdLogger.AssertNoErrors();
            thirdLogger.FullLog.ShouldContain("Content cache hit");
            File.ReadAllText(outputPath).ShouldBe("input");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }
    }

    [Fact]
    public async Task ReusesUnchangedDigestAndRehashesChangedInput()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        TransientTestFile input = env.CreateFile("input.txt", "first");
        var fileDigestCache = new TaskResultCacheFileDigestCache();

        var requests = new Task<TaskResultCacheFileDigest>[16];
        for (int i = 0; i < requests.Length; i++)
        {
            requests[i] = fileDigestCache
                .GetDigestAsync(input.Path, CancellationToken.None)
                .AsTask();
        }

        TaskResultCacheFileDigest[] digests = await Task.WhenAll(requests);
        TaskResultCacheFileDigest first = digests[0];
        for (int i = 1; i < digests.Length; i++)
        {
            digests[i].Hash.ShouldBeSameAs(first.Hash);
        }

        File.WriteAllText(input.Path, "second");
        TaskResultCacheFileDigest second =
            await fileDigestCache.GetDigestAsync(input.Path, CancellationToken.None);

        second.Hash.ShouldNotBe(first.Hash);

        fileDigestCache.ShutdownComponent();
        TaskResultCacheFileDigest nextBuild =
            await fileDigestCache.GetDigestAsync(input.Path, CancellationToken.None);
        nextBuild.Hash.ShouldBe(second.Hash);
        nextBuild.Hash.ShouldNotBeSameAs(second.Hash);
    }

    [Fact]
    public void HonorsRequiresUnset()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);
        string inputPath = Path.Combine(projectFolder.Path, "input.txt");
        string outputPath = Path.Combine(projectFolder.Path, "output.txt");
        string projectPath = Path.Combine(projectFolder.Path, "cache.proj");
        File.WriteAllText(inputPath, "input");
        File.WriteAllText(
            projectPath,
            $"""
            <Project>
              <PropertyGroup>
                <MSBuildContentCacheDirectory>cache</MSBuildContentCacheDirectory>
              </PropertyGroup>
              <UsingTask
                  TaskName="{typeof(TaskResultCacheTestTask).FullName}"
                  AssemblyFile="{SecurityElement.Escape(typeof(TaskResultCacheTestTask).Assembly.Location)}" />
              <Target Name="Build">
                <TaskResultCacheTestTask
                    Input="input.txt"
                    OutputFile="output.txt"
                    UncacheableMode="true"
                    DeclaredInputs="input.txt"
                    DeclaredOutputs="output.txt" />
              </Target>
            </Project>
            """);

        MockLogger firstLogger = BuildCacheProject(projectPath);
        firstLogger.AssertNoErrors();
        firstLogger.FullLog.ShouldContain(
            "the task parameter \"UncacheableMode\" must be unset");
        firstLogger.FullLog.ShouldNotContain("Content cache miss");

        File.Delete(outputPath);

        MockLogger secondLogger = BuildCacheProject(projectPath);
        secondLogger.AssertNoErrors();
        secondLogger.FullLog.ShouldContain(
            "the task parameter \"UncacheableMode\" must be unset");
        secondLogger.FullLog.ShouldNotContain("Content cache hit");
        File.ReadAllText(outputPath).ShouldBe("input");
    }

    [Fact]
    public void DeclaredPathGetterFailureRunsTask()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);
        string inputPath = Path.Combine(projectFolder.Path, "input.txt");
        string outputPath = Path.Combine(projectFolder.Path, "output.txt");
        string projectPath = Path.Combine(projectFolder.Path, "cache.proj");
        File.WriteAllText(inputPath, "input");
        File.WriteAllText(
            projectPath,
            $"""
            <Project>
              <PropertyGroup>
                <MSBuildContentCacheDirectory>cache</MSBuildContentCacheDirectory>
              </PropertyGroup>
              <UsingTask
                  TaskName="{typeof(TaskResultCacheTestTask).FullName}"
                  AssemblyFile="{SecurityElement.Escape(typeof(TaskResultCacheTestTask).Assembly.Location)}" />
              <Target Name="Build">
                <TaskResultCacheTestTask
                    Input="input.txt"
                    OutputFile="output.txt"
                    ThrowOnDeclaredInputsRead="true"
                    DeclaredInputs="input.txt"
                    DeclaredOutputs="output.txt" />
              </Target>
            </Project>
            """);

        MockLogger logger = BuildCacheProject(projectPath);

        logger.AssertNoErrors();
        logger.FullLog.ShouldContain("declared-input-getter-failure");
        logger.FullLog.ShouldNotContain("Content cache miss");
        File.ReadAllText(outputPath).ShouldBe("input");
    }

#if NET
    [LinuxOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void CorruptEntryDeletionDoesNotBlockReplacementPublication()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        CacheProject project = CreateCacheProject(env);

        MockLogger firstLogger = BuildCacheProject(project.ProjectPath);
        firstLogger.AssertNoErrors();
        firstLogger.FullLog.ShouldContain("Content cache miss");

        string manifestPath = Directory.GetFiles(
            project.CachePath,
            "manifest.bin",
            SearchOption.AllDirectories).ShouldHaveSingleItem();
        string entryDirectory = Path.GetDirectoryName(manifestPath)!;
        string shardDirectory = Path.GetDirectoryName(entryDirectory)!;
        UnixFileMode entryMode = File.GetUnixFileMode(entryDirectory);
        File.WriteAllText(manifestPath, "corrupt");
        File.SetUnixFileMode(
            entryDirectory,
            entryMode & ~UnixFileMode.UserWrite & ~UnixFileMode.GroupWrite & ~UnixFileMode.OtherWrite);

        try
        {
            File.Delete(project.OutputPath);
            MockLogger secondLogger = BuildCacheProject(project.ProjectPath);
            secondLogger.AssertNoErrors();
            secondLogger.FullLog.ShouldContain("Content cache miss");
            Directory.GetDirectories(
                shardDirectory,
                "*.delete-*").ShouldHaveSingleItem();

            File.Delete(project.OutputPath);
            MockLogger thirdLogger = BuildCacheProject(project.ProjectPath);
            thirdLogger.AssertNoErrors();
            thirdLogger.FullLog.ShouldContain("Content cache hit");
            File.ReadAllText(project.OutputPath).ShouldBe("input");
        }
        finally
        {
            foreach (string directory in Directory.GetDirectories(shardDirectory))
            {
                File.SetUnixFileMode(directory, entryMode);
            }
        }
    }
#endif

    [Fact]
    public void PayloadLengthMismatchRunsTaskAndRepublishes()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        CacheProject project = CreateCacheProject(env);

        BuildCacheProject(project.ProjectPath)
            .FullLog.ShouldContain("Content cache miss");
        string payloadPath = Directory.GetFiles(
            project.CachePath,
            "0.bin",
            SearchOption.AllDirectories).ShouldHaveSingleItem();
        File.AppendAllText(payloadPath, "corrupt");

        File.Delete(project.OutputPath);
        BuildCacheProject(project.ProjectPath)
            .FullLog.ShouldContain("Content cache miss");
        File.ReadAllText(project.OutputPath).ShouldBe("input");

        File.Delete(project.OutputPath);
        BuildCacheProject(project.ProjectPath)
            .FullLog.ShouldContain("Content cache hit");
        File.ReadAllText(project.OutputPath).ShouldBe("input");
    }

    [Fact]
    public void MultiOutputRestoreRollsBackBeforeTaskFallback()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);
        string inputPath = Path.Combine(projectFolder.Path, "input.txt");
        string firstOutputPath = Path.Combine(projectFolder.Path, "first.txt");
        string secondOutputPath = Path.Combine(projectFolder.Path, "second.txt");
        string projectPath = Path.Combine(projectFolder.Path, "cache.proj");
        File.WriteAllText(inputPath, "input");
        File.WriteAllText(
            projectPath,
            $"""
            <Project>
              <PropertyGroup>
                <MSBuildContentCacheDirectory>cache</MSBuildContentCacheDirectory>
              </PropertyGroup>
              <UsingTask
                  TaskName="{typeof(TaskResultCacheTestTask).FullName}"
                  AssemblyFile="{SecurityElement.Escape(typeof(TaskResultCacheTestTask).Assembly.Location)}" />
              <ItemGroup>
                <CacheInput Include="input.txt" />
                <CacheOutput Include="first.txt;second.txt" />
              </ItemGroup>
              <Target Name="Build">
                <TaskResultCacheTestTask
                    Input="@(CacheInput)"
                    OutputFile="first.txt"
                    AdditionalOutputFile="second.txt"
                    LogDeclaredOutputState="true"
                    DeclaredInputs="@(CacheInput)"
                    DeclaredOutputs="@(CacheOutput)" />
              </Target>
            </Project>
            """);

        MockLogger firstLogger = BuildCacheProject(projectPath);
        firstLogger.AssertNoErrors();
        firstLogger.FullLog.ShouldContain("Content cache miss");

        File.WriteAllText(firstOutputPath, "original");
        File.Delete(secondOutputPath);
        Directory.CreateDirectory(secondOutputPath);

        MockLogger secondLogger = BuildCacheProject(projectPath);

        secondLogger.AssertNoErrors();
        secondLogger.FullLog.ShouldContain(
            "declared-output-state:file:original|directory");
        secondLogger.FullLog.ShouldContain("The content cache could not be used");
        File.ReadAllText(firstOutputPath).ShouldBe("input");
        File.ReadAllText(secondOutputPath).ShouldBe("input");
    }

    private static CacheProject CreateCacheProject(TestEnvironment env)
    {
        TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);
        string inputPath = Path.Combine(projectFolder.Path, "input.txt");
        string outputPath = Path.Combine(projectFolder.Path, "output.txt");
        string cachePath = Path.Combine(projectFolder.Path, "cache");
        string projectPath = Path.Combine(projectFolder.Path, "cache.proj");
        File.WriteAllText(inputPath, "input");
        File.WriteAllText(
            projectPath,
            $"""
            <Project>
              <PropertyGroup>
                <WorkspaceRoot>true</WorkspaceRoot>
                <MSBuildContentCacheDirectory>cache</MSBuildContentCacheDirectory>
              </PropertyGroup>
              <UsingTask
                  TaskName="{typeof(TaskResultCacheTestTask).FullName}"
                  AssemblyFile="{SecurityElement.Escape(typeof(TaskResultCacheTestTask).Assembly.Location)}" />
              <ItemGroup>
                <CacheInput Include="input.txt" />
                <CacheOutput Include="output.txt" />
              </ItemGroup>
              <Target Name="Build">
                <TaskResultCacheTestTask
                    Input="@(CacheInput)"
                    OutputFile="@(CacheOutput)"
                    DeclaredInputs="@(CacheInput)"
                    DeclaredOutputs="@(CacheOutput)" />
              </Target>
            </Project>
            """);
        return new CacheProject(projectPath, outputPath, cachePath);
    }

    private MockLogger BuildCacheProject(string projectPath)
    {
        using var projectCollection = new ProjectCollection();
        var logger = new MockLogger(testOutput)
        {
            Verbosity = LoggerVerbosity.Diagnostic,
        };
        var parameters = new BuildParameters(projectCollection)
        {
            EnableNodeReuse = false,
            Loggers = [logger],
            MaxNodeCount = 1,
            ShutdownInProcNodeOnBuildFinish = true,
        };
        Project project = projectCollection.LoadProject(projectPath);
        using var buildManager = new BuildManager();
        BuildResult result = buildManager.Build(
            parameters,
            new BuildRequestData(project.CreateProjectInstance(), ["Build"]));

        result.ShouldHaveSucceeded();
        return logger;
    }

    private readonly record struct CacheProject(
        string ProjectPath,
        string OutputPath,
        string CachePath);
}

[MSBuildDeclaredIOTask]
[MSBuildDeclaredIORequiresUnset(nameof(UncacheableMode))]
public sealed class TaskResultCacheTestTask : Microsoft.Build.Utilities.Task
{
    private ITaskItem[] _declaredInputs = [];

    public ITaskItem Input { get; set; } = null!;

    public ITaskItem OutputFile { get; set; } = null!;

    public ITaskItem? AdditionalOutputFile { get; set; }

    public string Marker { get; set; } = String.Empty;

    public bool WriteOutput { get; set; } = true;

    public string SynchronizationDirectory { get; set; } = String.Empty;

    public bool UncacheableMode { get; set; }

    public bool ThrowOnDeclaredInputsRead { get; set; }

    public bool LogDeclaredOutputState { get; set; }

    public ITaskItem[] DeclaredInputs
    {
        get => ThrowOnDeclaredInputsRead
            ? throw new InvalidOperationException("declared-input-getter-failure")
            : _declaredInputs;
        set => _declaredInputs = value;
    }

    public ITaskItem[] DeclaredOutputs { get; set; } = [];

    public override bool Execute()
    {
        if (LogDeclaredOutputState)
        {
            var states = new string[DeclaredOutputs.Length];
            for (int i = 0; i < DeclaredOutputs.Length; i++)
            {
                string path = DeclaredOutputs[i].ItemSpec;
                states[i] = Directory.Exists(path)
                    ? "directory"
                    : File.Exists(path)
                        ? "file:" + File.ReadAllText(path)
                        : "missing";
            }

            Log.LogMessage(
                MessageImportance.High,
                "declared-output-state:" + String.Join("|", states));
        }

        Log.LogMessage(
            MessageImportance.Normal,
            "content-cache-test-message");
        Log.LogWarning("content-cache-test-warning");
        string? synchronizationMarker = null;
        if (!String.IsNullOrEmpty(SynchronizationDirectory))
        {
            Directory.CreateDirectory(SynchronizationDirectory);
            synchronizationMarker =
                Path.Combine(
                    SynchronizationDirectory,
                    Guid.NewGuid().ToString("N") + ".ready");
            File.WriteAllText(synchronizationMarker, String.Empty);
            if (!SpinWait.SpinUntil(
                    () => Directory.GetFiles(
                        SynchronizationDirectory,
                        "*.ready",
                        SearchOption.TopDirectoryOnly).Length >= 2,
                    TimeSpan.FromSeconds(10)))
            {
                Log.LogError("Concurrent task execution did not start.");
                return false;
            }

            string[] markers = Directory.GetFiles(
                SynchronizationDirectory,
                "*.ready",
                SearchOption.TopDirectoryOnly);
            Array.Sort(markers, StringComparer.Ordinal);
            string firstMarker = markers[0];
            if (!String.Equals(
                    synchronizationMarker,
                    firstMarker,
                    StringComparison.Ordinal) &&
                !SpinWait.SpinUntil(
                    () => File.Exists(firstMarker + ".done"),
                    TimeSpan.FromSeconds(10)))
            {
                Log.LogError("Concurrent task execution did not make progress.");
                return false;
            }
        }

        if (WriteOutput)
        {
            string contents = File.ReadAllText(Input.ItemSpec) + Marker;
            File.WriteAllText(OutputFile.ItemSpec, contents);
            if (AdditionalOutputFile is not null)
            {
                if (Directory.Exists(AdditionalOutputFile.ItemSpec))
                {
                    Directory.Delete(AdditionalOutputFile.ItemSpec);
                }

                File.WriteAllText(AdditionalOutputFile.ItemSpec, contents);
            }
        }
        else
        {
            File.Delete(OutputFile.ItemSpec);
        }

        if (synchronizationMarker is not null)
        {
            File.WriteAllText(synchronizationMarker + ".done", String.Empty);
            if (!SpinWait.SpinUntil(
                    () => Directory.GetFiles(
                        SynchronizationDirectory,
                        "*.done",
                        SearchOption.TopDirectoryOnly).Length >= 2,
                    TimeSpan.FromSeconds(10)))
            {
                Log.LogError("Concurrent task execution did not finish.");
                return false;
            }
        }

        return true;
    }
}
