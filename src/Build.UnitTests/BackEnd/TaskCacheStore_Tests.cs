// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskCacheStore_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private static readonly string s_key = HashBytes([1]);

    public static bool IsSupported => TaskCacheStore.IsSupported;

#if NET
    public static bool IsSupportedOnUnix => IsSupported && !OperatingSystem.IsWindows();
#endif

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void StoresAndRestoresStateAndFileContents()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string outputPath = env.CreateFile("result.txt", "content").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        DateTime timestamp = DateTime.UtcNow.AddDays(-1);
        File.SetLastWriteTimeUtc(outputPath, timestamp);
        store.Store(s_key, [outputPath], [1, 2, 3]);
        File.Delete(outputPath);
        store.TryRestore(s_key, [outputPath], out byte[] state).ShouldBeTrue();
        state.ShouldBe([1, 2, 3]);
        File.ReadAllText(outputPath).ShouldBe("content");
        File.GetLastWriteTimeUtc(outputPath).ShouldBeGreaterThan(timestamp);
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void ChecksAllBlobsBeforeTouchingOutputs()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string first = env.CreateFile("first.txt", "first").Path;
        string second = env.CreateFile("second.txt", "second").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [first, second], []);
        File.WriteAllText(first, "untouched");
        CorruptContent(backend, "second");
        Should.Throw<TaskCacheRestoreException>(() => store.TryRestore(s_key, [first, second], out _));
        File.ReadAllText(first).ShouldBe("untouched");
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void RejectsManifestPathsOutsideTheCurrentContract()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string first = env.CreateFile("first.txt", "first").Path;
        string other = env.CreateFile("other.txt", "untouched").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [first], []);
        Should.Throw<InvalidDataException>(() => store.TryRestore(s_key, [other], out _));
        File.ReadAllText(other).ShouldBe("untouched");
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void RejectsCacheDirectoryOverlap()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        Should.Throw<InvalidDataException>(() => store.Store(s_key, [Path.Combine(cache, "output.txt")], []));
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void ValidatesRawStateBeforeTouchingOutputs()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("result.txt", "cached").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [path], [42]);
        File.WriteAllText(path, "untouched");
        Should.Throw<InvalidDataException>(() =>
            store.TryRestore(s_key, [path], out _, _ => throw new InvalidDataException("Invalid raw outputs.")));
        File.ReadAllText(path).ShouldBe("untouched");
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void RestoresAbsentOutputByRemovingCurrentFile()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = Path.Combine(env.CreateFolder().Path, "absent.txt");
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [path], []);
        File.WriteAllText(path, "new");
        store.TryRestore(s_key, [path], out _).ShouldBeTrue();
        File.Exists(path).ShouldBeFalse();
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void RestoresAbsentOutputWithoutCreatingMissingParent()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string parent = Path.Combine(env.CreateFolder().Path, "missing", "nested");
        string path = Path.Combine(parent, "absent.txt");
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);

        store.Store(s_key, [path], [42]).ShouldBeTrue();
        store.TryRestore(s_key, [path], out byte[] state).ShouldBeTrue();

        state.ShouldBe([42]);
        File.Exists(path).ShouldBeFalse();
        Directory.Exists(parent).ShouldBeFalse();
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void CancellationBeforePublicationDoesNotPublishManifest()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("result.txt", "content").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [path], [], () => false).ShouldBeFalse();
        store.TryRestore(s_key, [path], out _).ShouldBeFalse();
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void StagingFailureIsFatalRatherThanACacheMiss()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("result.txt", "content").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [path], []);
        File.Delete(path);
        Should.Throw<TaskCacheRestoreException>(() =>
            store.TryRestore(s_key, [path], out _, _ => Directory.CreateDirectory(path)));
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void CancellationDuringStagingPreservesAllDestinationsAndCleansFiles()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string first = env.CreateFile("first.txt", "first").Path;
        string second = env.CreateFile("second.txt", "second").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [first, second], []).ShouldBeTrue();
        File.WriteAllText(first, "unchanged first");
        File.WriteAllText(second, "unchanged second");
        using CancellationTokenSource cancellation = new();
        using StagingCancellationBackend cancelling = new(backend, cancellation);
        store = new(cache, cancelling);
        Should.Throw<OperationCanceledException>(() =>
            store.TryRestore(s_key, [first, second], out _, cancellationToken: cancellation.Token));
        File.ReadAllText(first).ShouldBe("unchanged first");
        File.ReadAllText(second).ShouldBe("unchanged second");
        Directory.GetFiles(Path.GetDirectoryName(first)!, ".msbuild-cache-*").ShouldBeEmpty();
        Directory.GetFiles(Path.GetDirectoryName(second)!, ".msbuild-cache-*").ShouldBeEmpty();
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void CancellationBeforeStoreDoesNotPublishMapping()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string cache = env.CreateFolder().Path;
        string path = env.CreateFile("result.txt", "content").Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Should.Throw<OperationCanceledException>(() =>
            store.Store(s_key, [path], [], cancellationToken: cancellation.Token));
        store.TryRestore(s_key, [path], out _).ShouldBeFalse();
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void CancellationAfterReplacementStartsFinishesAllFiles()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string first = env.CreateFile("first.txt", "cached first").Path;
        string second = env.CreateFile("second.txt", "cached second").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [first, second], []).ShouldBeTrue();
        File.WriteAllText(first, "old first");
        File.WriteAllText(second, "old second");
        using CancellationTokenSource cancellation = new();
        Should.Throw<OperationCanceledException>(() => store.TryRestore(s_key, [first, second], out _,
            cancellationToken: cancellation.Token, onReplacementStarted: cancellation.Cancel));
        File.ReadAllText(first).ShouldBe("cached first");
        File.ReadAllText(second).ShouldBe("cached second");
        Directory.GetFiles(Path.GetDirectoryName(first)!, ".msbuild-cache-*").ShouldBeEmpty();
        Directory.GetFiles(Path.GetDirectoryName(second)!, ".msbuild-cache-*").ShouldBeEmpty();
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void PartialReplacementFailureIsFatalAndRequiresCleanRebuild()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string first = env.CreateFile("first.txt", "cached first").Path;
        string second = env.CreateFile("second.txt", "cached second").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [first, second], []).ShouldBeTrue();
        File.WriteAllText(first, "old first");
        var failure = Should.Throw<TaskCacheRestoreException>(() => store.TryRestore(s_key, [first, second], out _,
            onReplacementStarted: () =>
            {
                File.Delete(second);
                Directory.CreateDirectory(second);
            }));
        File.ReadAllText(first).ShouldBe("cached first");
        Directory.Exists(second).ShouldBeTrue();
        failure.Message.ShouldContain("clean and rebuild");
        Directory.GetFiles(Path.GetDirectoryName(second)!, ".msbuild-cache-*").ShouldBeEmpty();
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void CleanupFailureDoesNotReplacePrimaryFailure()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("output.txt", "cached").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [path], []).ShouldBeTrue();
        var failure = Should.Throw<TaskCacheRestoreException>(() => store.TryRestore(s_key, [path], out _,
            onReplacementStarted: () =>
            {
                foreach (string staging in Directory.GetFiles(Path.GetDirectoryName(path)!, ".msbuild-cache-*"))
                {
                    File.Delete(staging);
                    Directory.CreateDirectory(staging);
                }
                throw new IOException("primary replacement failure");
            }));
        failure.InnerException!.Message.ShouldBe("primary replacement failure");
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public async Task ConcurrentPublicationProducesACompleteEntry()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("result.txt", "content").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheOwner owner = new(cache, (_, _) => { }, default);
        Task[] writers = new Task[8];
        for (int i = 0; i < writers.Length; i++)
        {
            writers[i] = Task.Run(() => new TaskCacheStore(cache, owner).Store(s_key, [path], [42]));
        }

        await Task.WhenAll(writers);
        File.Delete(path);
        new TaskCacheStore(cache, owner).TryRestore(s_key, [path], out byte[] state).ShouldBeTrue();
        state.ShouldBe([42]);
        File.ReadAllText(path).ShouldBe("content");
        Directory.Exists(Path.Combine(cache, "entries")).ShouldBeFalse();
        Directory.Exists(Path.Combine(cache, "blobs")).ShouldBeFalse();
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void IgnoresLegacyCacheEntries()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string cache = env.CreateFolder().Path;
        Directory.CreateDirectory(Path.Combine(cache, "entries"));
        File.WriteAllText(Path.Combine(cache, "entries", s_key), "old incompatible manifest");
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        new TaskCacheStore(cache, backend).TryRestore(s_key, [], out _).ShouldBeFalse();
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupported))]
    public void RejectsCorruptManifestStoredThroughBackend()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("result.txt", "untouched").Path;
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        backend.Publish(s_key, new byte[64]).ShouldBeTrue();

        Should.Throw<InvalidDataException>(() => new TaskCacheStore(cache, backend).TryRestore(s_key, [path], out _));
        File.ReadAllText(path).ShouldBe("untouched");
    }

    internal static void CorruptContent(string cache, string content)
    {
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        CorruptContent(backend, content);
    }

    private static void CorruptContent(TaskCacheBackend backend, string content)
    {
        string digest = HashBytes(System.Text.Encoding.UTF8.GetBytes(content));
        string path = backend.ContentPath(digest);
        File.SetAttributes(path, FileAttributes.Normal);
        File.WriteAllText(path, "corrupt");
    }

#if NET
    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupportedOnUnix))]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void StagesPrivateOutputsWithOwnerOnlyPermissions()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("private.txt", "private content").Path;
        const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        File.SetUnixFileMode(path, mode);
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        new TaskCacheStore(cache, backend).Store(s_key, [path], []).ShouldBeTrue();
        File.Delete(path);
        int inspections = 0;
        using StagingInspectionBackend inspecting = new(backend, staging =>
        {
            File.GetUnixFileMode(staging).ShouldBe(mode);
            inspections++;
        });

        new TaskCacheStore(cache, inspecting).TryRestore(s_key, [path], out _).ShouldBeTrue();

        inspections.ShouldBe(2);
        File.GetUnixFileMode(path).ShouldBe(mode);
        File.ReadAllText(path).ShouldBe("private content");
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupportedOnUnix))]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void RestoresExecutableModeWithoutSharingCacheInodes()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string path = env.CreateFile("tool", "original").Path;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        store.Store(s_key, [path], []).ShouldBeTrue();
        File.WriteAllText(path, "changed after put");
        store.TryRestore(s_key, [path], out _).ShouldBeTrue();
        File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllText(path, "changed after restore");
        store.TryRestore(s_key, [path], out _).ShouldBeTrue();
        File.ReadAllText(path).ShouldBe("original");
    }

    [ConditionalFact(typeof(TaskCacheStore_Tests), nameof(IsSupportedOnUnix))]
    public void RejectsSymbolicLinkDestinations()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string source = env.CreateFile("source.txt", "untouched").Path;
        string link = Path.Combine(env.CreateFolder().Path, "link.txt");
        File.CreateSymbolicLink(link, source);
        string cache = env.CreateFolder().Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        Should.Throw<IOException>(() => store.Store(s_key, [link], []));
        File.ReadAllText(source).ShouldBe("untouched");
    }

    private sealed class StagingInspectionBackend(TaskCacheBackend backend, Action<string> inspect) : TaskCacheBackend
    {
        internal override byte[]? ReadManifest(string key, int maximumSize, CancellationToken cancellationToken = default) =>
            backend.ReadManifest(key, maximumSize, cancellationToken);

        internal override Stream Open(string digest, CancellationToken cancellationToken = default)
        {
            using Stream source = backend.Open(digest, cancellationToken);
            using MemoryStream contents = new();
            source.CopyTo(contents);
            return new InspectingStream(contents.ToArray(), inspect);
        }

        internal override string ContentPath(string digest) => backend.ContentPath(digest);
        internal override string PutFile(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        internal override bool Publish(string key, byte[] manifest, Func<bool>? canPublish = null,
            IReadOnlyList<string>? artifacts = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override void Dispose() { }
    }

    private sealed class InspectingStream(byte[] contents, Action<string> inspect) : MemoryStream(contents, writable: false)
    {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            string path = destination.ShouldBeOfType<FileStream>().Name;
            inspect(path);
            await base.CopyToAsync(destination, bufferSize, cancellationToken);
            inspect(path);
        }
    }
#endif

    private static string HashBytes(byte[] bytes)
    {
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", String.Empty);
    }

    private sealed class StagingCancellationBackend(TaskCacheBackend backend, CancellationTokenSource cancellation) : TaskCacheBackend
    {
        private int _opened;
        internal override byte[]? ReadManifest(string key, int maximumSize, CancellationToken cancellationToken = default) =>
            backend.ReadManifest(key, maximumSize, cancellationToken);
        internal override Stream Open(string digest, CancellationToken cancellationToken = default)
        {
            if (++_opened == 2)
            {
                cancellation.Cancel();
            }
            return backend.Open(digest, cancellationToken);
        }
        internal override string ContentPath(string digest) => backend.ContentPath(digest);
        internal override string PutFile(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        internal override bool Publish(string key, byte[] manifest, Func<bool>? canPublish = null,
            IReadOnlyList<string>? artifacts = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override void Dispose() { }
    }
}
