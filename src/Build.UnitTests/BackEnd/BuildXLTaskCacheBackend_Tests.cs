// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class BuildXLTaskCacheBackend_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private static readonly string s_key = new('A', 64);

    public static bool IsSupported => TaskCacheStore.IsSupported;

    [Fact]
    public void BuildXLDependencyAndImplementationFollowSourceBuildGate()
    {
        var references = typeof(BuildManager).Assembly.GetReferencedAssemblies();
        references.ShouldNotContain(reference => reference.Name!.StartsWith("BuildXL.Cache.", StringComparison.Ordinal));
#if FEATURE_BUILDXL_TASK_CACHE
        typeof(BuildXLTaskCacheBackend).Assembly.GetName().Name.ShouldBe("Microsoft.Build.TaskCache");
        typeof(BuildXLTaskCacheBackend).Assembly.GetReferencedAssemblies()
            .ShouldContain(reference => reference.Name == "BuildXL.Cache.MemoizationStore");
        typeof(BuildManager).Assembly.GetType("Microsoft.Build.BackEnd.BuildXLTaskCacheBackend").ShouldBeNull();
        TaskCacheStore.IsSupported.ShouldBe(RuntimeInformation.ProcessArchitecture == Architecture.X64);
#else
        typeof(BuildManager).Assembly.GetType("Microsoft.Build.BackEnd.BuildXLTaskCacheBackend").ShouldBeNull();
        TaskCacheStore.IsSupported.ShouldBeFalse();
#endif
    }

#if FEATURE_BUILDXL_TASK_CACHE
    [ConditionalFact(typeof(BuildXLTaskCacheBackend_Tests), nameof(IsSupported))]
    public void EmptyContentDoesNotRequireAPhysicalCasFile()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("empty.txt", String.Empty).Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(env.CreateFolder().Path);
        string digest = backend.PutFile(path);
        using Stream stream = backend.Open(digest);
        stream.Length.ShouldBe(0);
        File.Exists(backend.ContentPath(digest)).ShouldBeFalse();
    }

    [ConditionalFact(typeof(BuildXLTaskCacheBackend_Tests), nameof(IsSupported))]
    public void BuildXLQuotaEvictsUnpinnedContentBetweenSessions()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string cache = env.CreateFolder().Path;
        string path = env.CreateFile().Path;
        byte[] content = new byte[700 * 1024];
        File.WriteAllBytes(path, content);
        string firstDigest;
        using (BuildXLTaskCacheBackend backend = new(cache, quotaMegabytes: 1))
        {
            firstDigest = backend.PutFile(path);
        }

        content[0] = 1;
        File.WriteAllBytes(path, content);
        using (BuildXLTaskCacheBackend backend = new(cache, quotaMegabytes: 1))
        {
            string secondDigest = backend.PutFile(path);
            Should.Throw<IOException>(() => backend.Open(firstDigest));
            using Stream restored = backend.Open(secondDigest);
            restored.ReadByte().ShouldBe(1);
        }
    }

    [ConditionalFact(typeof(BuildXLTaskCacheBackend_Tests), nameof(IsSupported))]
    public void BuildLongPinsPreventQuotaEvictionUntilOwnerCloses()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string cache = env.CreateFolder().Path;
        string first = env.CreateFile().Path;
        string second = env.CreateFile().Path;
        byte[] content = new byte[700 * 1024];
        File.WriteAllBytes(first, content);
        content[0] = 1;
        File.WriteAllBytes(second, content);
        using BuildXLTaskCacheBackend backend = new(cache, quotaMegabytes: 1);
        string digest = backend.PutFile(first);
        Should.Throw<IOException>(() => backend.PutFile(second));
        using Stream pinned = backend.Open(digest);
        pinned.ReadByte().ShouldBe(0);
    }

    [ConditionalFact(typeof(BuildXLTaskCacheBackend_Tests), nameof(IsSupported))]
    public void ExclusiveLockFailsImmediatelyAndIsReleased()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string cache = env.CreateFolder().Path;
        using (BuildXLTaskCacheBackend first = new(cache))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Should.Throw<IOException>(() => new BuildXLTaskCacheBackend(cache)).Message.ShouldBe(
                ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskCache.OwnershipUnavailable", cache));
            watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
        }

        using BuildXLTaskCacheBackend reopened = new(cache);
        reopened.ReadManifest(s_key, 1024).ShouldBeNull();
    }

    [ConditionalFact(typeof(BuildXLTaskCacheBackend_Tests), nameof(IsSupported))]
    public void InitializationFailureAfterAcquiringLockReleasesOwnership()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string cache = env.CreateFolder().Path;
        string root = Path.Combine(cache, BuildXLTaskCacheBackend.DirectoryName);
        Directory.CreateDirectory(root);
        string obstruction = Path.Combine(root, "cache");
        File.WriteAllText(obstruction, "not a directory");
        Should.Throw<IOException>(() => new BuildXLTaskCacheBackend(cache));
        File.Delete(obstruction);
        using BuildXLTaskCacheBackend backend = new(cache);
        backend.ReadManifest(s_key, 1024).ShouldBeNull();
    }

    [ConditionalFact(typeof(BuildXLTaskCacheBackend_Tests), nameof(IsSupported))]
    public void EvictedArtifactMakesMemoizedEntryACacheMiss()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string cache = env.CreateFolder().Path;
        string path = env.CreateFile().Path;
        byte[] content = new byte[700 * 1024];
        File.WriteAllBytes(path, content);
        using (BuildXLTaskCacheBackend backend = new(cache, quotaMegabytes: 1))
        {
            string digest = backend.PutFile(path);
            backend.Publish(s_key, new byte[64], artifacts: [digest]).ShouldBeTrue();
        }

        content[0] = 1;
        File.WriteAllBytes(path, content);
        using (BuildXLTaskCacheBackend backend = new(cache, quotaMegabytes: 1))
        {
            backend.PutFile(path);
            backend.ReadManifest(s_key, 1024).ShouldBeNull();
        }
    }
#endif
}
