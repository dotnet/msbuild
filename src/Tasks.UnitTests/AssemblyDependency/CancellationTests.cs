// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        MockEngine engine = new(_output);
        ResolveAssemblyReference initialTask = CreateTask(directory, engine);
        Execute(initialTask).ShouldBeTrue();
        initialTask.ResolvedFiles.Length.ShouldBe(2);
        initialTask.FilesWritten.ShouldHaveSingleItem().ItemSpec.ShouldBe(initialTask.StateFile);
        byte[] originalState = File.ReadAllBytes(initialTask.StateFile);

        ResolveAssemblyReference task = CreateTask(directory, engine);
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

        ResolveAssemblyReference recovered = CreateTask(directory, engine);
        Execute(recovered).ShouldBeTrue();
        AssertSameOutputs(initialTask, recovered);
        File.ReadAllBytes(recovered.StateFile).ShouldBe(originalState);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RemappingDoesNotPolluteSharedMetadata(bool cancelAfterRemapping, bool targetAlreadyReferenced)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = env.CreateFolder().Path;
        MockEngine engine = new(_output);
        string redist = env.CreateFile(fileName: "remapping.xml", contents: """
            <FileList Redist="TestFramework">
              <File AssemblyName="UnusedFrameworkAssembly" Version="1.0.0.0" Culture="neutral" PublicKeyToken="null" InGAC="false" />
              <Remap>
                <From AssemblyName="B">
                  <To AssemblyName="C" Version="1.0.0.0" Culture="neutral" PublicKeyToken="null" />
                </From>
              </Remap>
            </FileList>
            """).Path;
        AssemblyNameExtension originalDependency = GetAssemblyName("B.dll");
        AssemblyNameExtension existingTarget = GetAssemblyName("C.dll");
        AssemblyNameExtension[] rawDependencies = targetAlreadyReferenced
            ? [existingTarget, originalDependency]
            : [originalDependency];
        int primaryMetadataReads = 0;
        GetAssemblyMetadata metadata = (
            string path,
            ConcurrentDictionary<string, AssemblyMetadata> cache,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName? frameworkName) =>
        {
            if (Path.GetFileNameWithoutExtension(path) == "A")
            {
                primaryMetadataReads++;
                dependencies = rawDependencies;
            }
            else
            {
                dependencies = [];
            }
            scatterFiles = [];
            frameworkName = null;
        };

        ResolveAssemblyReference control = CreateTask(directory, engine);
        control.Assemblies = [new TaskItem("A")];
        Execute(control, getAssemblyMetadata: metadata).ShouldBeTrue();
        control.ResolvedDependencyFiles.Select(item => Path.GetFileNameWithoutExtension(item.ItemSpec))
            .ShouldBe(targetAlreadyReferenced ? ["B", "C"] : ["B"], ignoreOrder: true);

        ResolveAssemblyReference remapped = CreateTask(directory, engine);
        remapped.Assemblies = [new TaskItem("A")];
        remapped.InstalledAssemblyTables = [new TaskItem(redist)];
        remapped.StateFile = Path.Combine(directory, "remapped.cache");
        bool cancellationRequested = false;
        bool result = Execute(remapped, getAssemblyMetadata: metadata, getLastWriteTime: path =>
        {
            if (cancelAfterRemapping && Path.GetFileNameWithoutExtension(path) == "C")
            {
                cancellationRequested = true;
                remapped.Cancel();
            }
            return GetLastWriteTime(path);
        });
        result.ShouldBe(!cancelAfterRemapping);
        cancellationRequested.ShouldBe(cancelAfterRemapping);
        if (cancelAfterRemapping)
        {
            AssertCanceledWithoutWritingState(remapped);
        }
        else
        {
            remapped.ResolvedDependencyFiles.ShouldHaveSingleItem().GetMetadata("FusionName").ShouldBe(existingTarget.FullName);
        }

        ResolveAssemblyReference recovered = CreateTask(directory, engine);
        recovered.Assemblies = [new TaskItem("A")];
        recovered.StateFile = Path.Combine(directory, "recovered.cache");
        Execute(recovered, getAssemblyMetadata: metadata).ShouldBeTrue();
        AssertSameOutputs(control, recovered);
        primaryMetadataReads.ShouldBe(1, "the recovery must reuse the process-wide metadata, not reread it");
        rawDependencies.Last().ShouldBeSameAs(originalDependency);
        originalDependency.RemappedFromEnumerator.ShouldBeEmpty();
        existingTarget.RemappedFromEnumerator.ShouldBeEmpty();
        engine.Errors.ShouldBe(0);
        engine.Warnings.ShouldBe(0);
    }

    [Theory]
    [InlineData("timestamp")]
    [InlineData("name")]
    [InlineData("metadata")]
    public void ColdCacheRecoversAfterCancellation(string cancellationPoint)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = env.CreateFolder().Path;
        MockEngine engine = new(_output);
        ResolveAssemblyReference canceled = CreateTask(directory, engine);
        canceled.Assemblies = [new TaskItem("A")];
        int primaryNameReads = 0;
        int primaryMetadataReads = 0;
        bool requestedCancellation = false;

        void CancelAt(string point, string path)
        {
            if (!requestedCancellation && point == cancellationPoint && Path.GetFileNameWithoutExtension(path) == "A")
            {
                requestedCancellation = true;
                canceled.Cancel();
            }
        }

        GetAssemblyName name = path =>
        {
            if (Path.GetFileNameWithoutExtension(path) == "A")
            {
                primaryNameReads++;
            }
            CancelAt("name", path);
            return GetAssemblyName(path);
        };
        GetAssemblyMetadata metadata = (
            string path,
            ConcurrentDictionary<string, AssemblyMetadata> cache,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName? frameworkName) =>
        {
            if (Path.GetFileNameWithoutExtension(path) == "A")
            {
                primaryMetadataReads++;
                dependencies = [GetAssemblyName("B.dll")];
            }
            else
            {
                dependencies = [];
            }
            scatterFiles = [];
            frameworkName = null;
            CancelAt("metadata", path);
        };
        GetLastWriteTime timestamp = path =>
        {
            CancelAt("timestamp", path);
            return GetLastWriteTime(path);
        };

        Execute(canceled, name, metadata, timestamp).ShouldBeFalse();
        requestedCancellation.ShouldBeTrue();
        AssertCanceledWithoutWritingState(canceled);

        ResolveAssemblyReference recovered = CreateTask(directory, engine);
        recovered.Assemblies = [new TaskItem("A")];
        Execute(recovered, name, metadata, timestamp).ShouldBeTrue();
        recovered.ResolvedFiles.ShouldHaveSingleItem().GetMetadata("FusionName").ShouldBe(GetAssemblyName("A.dll").FullName);
        recovered.ResolvedDependencyFiles.ShouldHaveSingleItem().GetMetadata("FusionName").ShouldBe(GetAssemblyName("B.dll").FullName);
        recovered.CopyLocalFiles.Length.ShouldBe(2);
        recovered.CopyLocalFiles.ShouldAllBe(item => item.GetMetadata("CopyLocal") == "true");
        recovered.FilesWritten.ShouldHaveSingleItem().ItemSpec.ShouldBe(recovered.StateFile);

        ResolveAssemblyReference cached = CreateTask(directory, engine);
        cached.Assemblies = [new TaskItem("A")];
        Execute(cached, name, metadata, timestamp).ShouldBeTrue();
        AssertSameOutputs(recovered, cached);
        primaryNameReads.ShouldBe(1);
        primaryMetadataReads.ShouldBe(1);
        engine.Errors.ShouldBe(0);
        engine.Warnings.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssemblyFolderCacheRecoversAfterCancellation(bool useCache)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", useCache ? null : "1");
        TransientTestFolder folder = env.CreateFolder();
        env.CreateFile(folder, "A.dll", string.Empty);
        env.CreateFile(folder, "B.dll", string.Empty);
        string config = env.CreateFile(fileName: "folders.config", contents: $$"""
            <AssemblyFoldersConfig>
              <AssemblyFolders>
                <AssemblyFolder>
                  <FrameworkVersion>v4.0</FrameworkVersion>
                  <Path>{{folder.Path}}</Path>
                </AssemblyFolder>
              </AssemblyFolders>
            </AssemblyFoldersConfig>
            """).Path;
        string searchPath = $"{{AssemblyFoldersFromConfig:{config},v4.0}}";
        string cacheKey = "6f7de854-47fe-4ae2-9cfe-9b33682abd91" + searchPath;
        MockEngine engine = new(_output);
        ResolveAssemblyReference canceled = CreateTask(folder.Path, engine);
        canceled.SearchPaths = [searchPath];
        bool requestedCancellation = false;
        Execute(canceled, getAssemblyName: path =>
        {
            requestedCancellation = true;
            canceled.Cancel();
            return GetAssemblyName(path);
        }).ShouldBeFalse();
        requestedCancellation.ShouldBeTrue();
        AssertCanceledWithoutWritingState(canceled);
        object? originalCache = engine.GetRegisteredTaskObject(cacheKey, RegisteredTaskObjectLifetime.Build);
        if (useCache)
        {
            originalCache.ShouldBeOfType<AssemblyFoldersFromConfigCache>();
        }
        else
        {
            originalCache.ShouldBeNull();
        }

        ResolveAssemblyReference recovered = CreateTask(folder.Path, engine);
        recovered.SearchPaths = [searchPath];
        Execute(recovered).ShouldBeTrue();
        recovered.ResolvedFiles.Length.ShouldBe(2);
        engine.GetRegisteredTaskObject(cacheKey, RegisteredTaskObjectLifetime.Build).ShouldBeSameAs(originalCache);

        ResolveAssemblyReference control = CreateTask(folder.Path);
        control.SearchPaths = [searchPath];
        Execute(control).ShouldBeTrue();
        AssertSameOutputs(control, recovered);
        engine.Errors.ShouldBe(0);
        engine.Warnings.ShouldBe(0);
    }

    [Fact]
    public void UnrelatedCancellationExceptionFromAssemblyNameIsNotHandled()
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

    private ResolveAssemblyReference CreateTask(string directory, MockEngine? engine = null) => new()
    {
        BuildEngine = engine ?? new MockEngine(_output),
        Assemblies = [new TaskItem("A"), new TaskItem("B")],
        SearchPaths = [directory],
        AllowedAssemblyExtensions = [".dll"],
        IgnoreDefaultInstalledAssemblyTables = true,
        FindRelatedFiles = false,
        FindSatellites = false,
        FindSerializationAssemblies = false,
        StateFile = Path.Combine(directory, "references.cache")
    };

    private static void AssertSameOutputs(ResolveAssemblyReference expected, ResolveAssemblyReference actual)
    {
        Snapshot(actual.ResolvedFiles).ShouldBe(Snapshot(expected.ResolvedFiles));
        Snapshot(actual.ResolvedDependencyFiles).ShouldBe(Snapshot(expected.ResolvedDependencyFiles));
        Snapshot(actual.CopyLocalFiles).ShouldBe(Snapshot(expected.CopyLocalFiles));
        Snapshot(actual.RelatedFiles).ShouldBe(Snapshot(expected.RelatedFiles));
        Snapshot(actual.SatelliteFiles).ShouldBe(Snapshot(expected.SatelliteFiles));
        Snapshot(actual.SerializationAssemblyFiles).ShouldBe(Snapshot(expected.SerializationAssemblyFiles));
        Snapshot(actual.ScatterFiles).ShouldBe(Snapshot(expected.ScatterFiles));
        Snapshot(actual.SuggestedRedirects).ShouldBe(Snapshot(expected.SuggestedRedirects));
        actual.DependsOnSystemRuntime.ShouldBe(expected.DependsOnSystemRuntime);
        actual.DependsOnNETStandard.ShouldBe(expected.DependsOnNETStandard);
    }

    private static string[] Snapshot(ITaskItem[] items) =>
        items.Select(item =>
        {
            IDictionary metadata = item.CloneCustomMetadata();
            return item.ItemSpec + " | " + string.Join(" | ",
                metadata.Keys.Cast<string>()
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .Select(name => $"{name}={metadata[name]}"));
        })
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

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
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? DateTime.FromFileTimeUtc(1)
            : File.GetLastWriteTimeUtc(path);

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
