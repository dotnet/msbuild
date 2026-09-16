// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskResultCacheDeletion_Tests(ITestOutputHelper testOutput)
{
#if NET
    [UnixOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void CorruptEntryDeletionDoesNotBlockReplacementPublication()
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
                <MSBuildTaskCacheDirectory>cache</MSBuildTaskCacheDirectory>
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

        MockLogger firstLogger = BuildCacheProject(projectPath);
        firstLogger.AssertNoErrors();
        firstLogger.FullLog.ShouldContain("Task result cache miss");

        string manifestPath = Directory.GetFiles(
            cachePath,
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
            File.Delete(outputPath);
            MockLogger secondLogger = BuildCacheProject(projectPath);
            secondLogger.AssertNoErrors();
            secondLogger.FullLog.ShouldContain("Task result cache miss");
            Directory.GetDirectories(shardDirectory, "*.delete-*").ShouldHaveSingleItem();

            File.Delete(outputPath);
            MockLogger thirdLogger = BuildCacheProject(projectPath);
            thirdLogger.AssertNoErrors();
            thirdLogger.FullLog.ShouldContain("Task result cache hit");
            File.ReadAllText(outputPath).ShouldBe("input");
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
}
