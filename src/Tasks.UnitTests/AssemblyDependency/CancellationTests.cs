// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Tasks.AssemblyFoldersFromConfig;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

public sealed class CancellationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void CancelBeforeExecute()
    {
        MockEngine engine = new(_output);
        ResolveAssemblyReference task = new() { BuildEngine = engine };
        ICancelableTask cancelable = task;

        cancelable.Cancel();
        cancelable.Cancel();

        task.Execute().ShouldBeFalse();
        task.ResolvedFiles.ShouldBeEmpty();
        task.FilesWritten.ShouldBeEmpty();
        engine.Errors.ShouldBe(0);
        engine.Warnings.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelDuringPrimaryResolution(bool useAssemblyFiles)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = env.CreateFolder().Path;
        ResolveAssemblyReference task = CreateTask(directory);
        if (useAssemblyFiles)
        {
            task.Assemblies = [];
            task.AssemblyFiles = [new TaskItem(Path.Combine(directory, "A.dll")), new TaskItem(Path.Combine(directory, "B.dll"))];
        }

        List<string> assemblyReads = [];
        Execute(task, getAssemblyName: path =>
        {
            assemblyReads.Add(Path.GetFileNameWithoutExtension(path));
            task.Cancel();
            return GetAssemblyName(path);
        }).ShouldBeFalse();

        assemblyReads.ShouldBe(["A"]);
        task.ResolvedFiles.ShouldBeEmpty();
        AssertCanceledWithoutWritingState(task);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void CancelDuringDependencyDiscovery(bool findDependencies, bool autoUnify)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ResolveAssemblyReference task = CreateTask(env.CreateFolder().Path);
        task.Assemblies = [new TaskItem("A")];
        task.FindDependencies = findDependencies;
        task.AutoUnify = autoUnify;
        int metadataReads = 0;

        Execute(task, getAssemblyMetadata: (
            string path,
            ConcurrentDictionary<string, AssemblyMetadata> cache,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName? frameworkName) =>
        {
            metadataReads++;
            dependencies = [new AssemblyNameExtension("B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")];
            scatterFiles = [];
            frameworkName = null;
            task.Cancel();
        }).ShouldBeFalse();

        metadataReads.ShouldBe(1);
        AssertCanceledWithoutWritingState(task);
    }

    [Fact]
    public async System.Threading.Tasks.Task CancelFromAnotherThreadPreservesExistingState()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = env.CreateFolder().Path;
        ResolveAssemblyReference initialTask = CreateTask(directory);
        Execute(initialTask).ShouldBeTrue();
        initialTask.ResolvedFiles.Length.ShouldBe(2);
        byte[] originalState = File.ReadAllBytes(initialTask.StateFile);

        ResolveAssemblyReference task = CreateTask(directory);
        using ManualResetEventSlim readingFile = new();
        using ManualResetEventSlim cancellationRequested = new();
        System.Threading.Tasks.Task cancel = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                readingFile.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
                task.Cancel();
            }
            finally
            {
                cancellationRequested.Set();
            }
        });

        int fileReads = 0;
        bool result = Execute(task, getLastWriteTime: path =>
        {
            fileReads++;
            readingFile.Set();
            cancellationRequested.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
            return GetLastWriteTime(path);
        });
        await cancel;

        result.ShouldBeFalse();
        fileReads.ShouldBe(1);
        File.ReadAllBytes(task.StateFile).ShouldBe(originalState);
        task.FilesWritten.ShouldBeEmpty();
        ((MockEngine)task.BuildEngine).Errors.ShouldBe(0);
        ((MockEngine)task.BuildEngine).Warnings.ShouldBe(0);
    }

    [Fact]
    public void UnrelatedCancellationExceptionIsNotHandled()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        ResolveAssemblyReference task = CreateTask(env.CreateFolder().Path);
        using CancellationTokenSource unrelatedCancellation = new();
        OperationCanceledException exception = new(unrelatedCancellation.Token);

        Should.Throw<OperationCanceledException>(() =>
            Execute(task, getAssemblyName: _ => throw exception)).ShouldBeSameAs(exception);
    }

    [Fact]
    public void CancelDuringCachedAssemblyFolderSearch()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", null);
        TransientTestFolder firstFolder = env.CreateFolder();
        string firstDirectory = firstFolder.Path;
        string secondDirectory = env.CreateFolder().Path;
        env.CreateFile(firstFolder, "A.dll", string.Empty);
        string config = env.CreateFile(fileName: "folders.config", contents: $$"""
            <AssemblyFoldersConfig>
              <AssemblyFolders>
                <AssemblyFolder>
                  <FrameworkVersion>v4.0</FrameworkVersion>
                  <Path>{{firstDirectory}}</Path>
                </AssemblyFolder>
                <AssemblyFolder>
                  <FrameworkVersion>v3.5</FrameworkVersion>
                  <Path>{{secondDirectory}}</Path>
                </AssemblyFolder>
              </AssemblyFolders>
            </AssemblyFoldersConfig>
            """).Path;
        ResolveAssemblyReference task = CreateTask(firstDirectory);
        using CancellationTokenSource cancellation = new();
        int assemblyReads = 0;
        AssemblyFoldersFromConfigResolver resolver = new(
            $"{{AssemblyFoldersFromConfig:{config},v4.0}}",
            _ =>
            {
                assemblyReads++;
                cancellation.Cancel();
                // Keep searching for an MSIL assembly after finding this architecture-specific one.
                return new AssemblyNameExtension("A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=x86");
            },
            _ => throw new InvalidOperationException("The cached file lookup should be used."),
            _ => "v4.0.30319",
            new Version(4, 0),
            System.Reflection.ProcessorArchitecture.MSIL,
            true,
            task.BuildEngine,
            task.Log,
            task.TaskEnvironment)
        {
            CancellationToken = cancellation.Token
        };

        OperationCanceledException exception = Should.Throw<OperationCanceledException>(() =>
            resolver.Resolve(new AssemblyNameExtension("A"), string.Empty, string.Empty, true, false, false,
                [".dll"], string.Empty, string.Empty, [], out _, out _));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        assemblyReads.ShouldBeGreaterThan(0);
    }

    private ResolveAssemblyReference CreateTask(string directory) => new()
    {
        BuildEngine = new MockEngine(_output),
        Assemblies = [new TaskItem("A"), new TaskItem("B")],
        SearchPaths = [directory],
        AllowedAssemblyExtensions = [".dll"],
        IgnoreDefaultInstalledAssemblyTables = true,
        FindRelatedFiles = false,
        FindSatellites = false,
        FindSerializationAssemblies = false,
        StateFile = Path.Combine(directory, "references.cache")
    };

    private static void AssertCanceledWithoutWritingState(ResolveAssemblyReference task)
    {
        File.Exists(task.StateFile).ShouldBeFalse();
        task.FilesWritten.ShouldBeEmpty();
        ((MockEngine)task.BuildEngine).Errors.ShouldBe(0);
        ((MockEngine)task.BuildEngine).Warnings.ShouldBe(0);
    }

    private static AssemblyNameExtension GetAssemblyName(string path) =>
        new($"{Path.GetFileNameWithoutExtension(path)}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

    private static DateTime GetLastWriteTime(string path) =>
        DateTime.FromFileTimeUtc(path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? 1 : 0);

    private static bool Execute(
        ResolveAssemblyReference task,
        GetAssemblyName? getAssemblyName = null,
        GetAssemblyMetadata? getAssemblyMetadata = null,
        GetLastWriteTime? getLastWriteTime = null) =>
        task.Execute(
            path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase),
            _ => true,
            (_, _) => [],
            getAssemblyName ?? GetAssemblyName,
            getAssemblyMetadata ?? ((
                string path,
                ConcurrentDictionary<string, AssemblyMetadata> cache,
                out AssemblyNameExtension[] dependencies,
                out string[] scatterFiles,
                out FrameworkName? frameworkName) =>
            {
                dependencies = [];
                scatterFiles = [];
                frameworkName = null;
            }),
#if FEATURE_WIN32_REGISTRY
            (_, _) => [],
            (_, _) => string.Empty,
#endif
            getLastWriteTime ?? GetLastWriteTime,
            _ => "v4.0.30319",
#if FEATURE_WIN32_REGISTRY
            (_, _) => throw new InvalidOperationException("Registry access is not expected."),
#endif
            (_, _, _, _, _, _, _) => string.Empty,
            (string path, GetAssemblyRuntimeVersion getRuntimeVersion, FileExists fileExists, out string imageRuntimeVersion, out bool isManagedWinmd) =>
            {
                imageRuntimeVersion = "v4.0.30319";
                isManagedWinmd = false;
                return false;
            },
            _ => 0);
}
