// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Task = System.Threading.Tasks.Task;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.UnitTests;

public sealed class PersistentCache_Tests
{
    private const string Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private readonly ITestOutputHelper _output;

    public PersistentCache_Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DirectoryRoundTripReconcilesContentsAndTimestamps()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        string output = Path.Combine(root, "output");
        string nested = Path.Combine(output, "sub", "result");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(nested, "cached");
        PersistentCacheOutputs outputs = PersistentCacheOutputs.FromDirectory(output);

        cache.TryStore(Key, outputs).ShouldBeTrue();
        File.WriteAllText(nested, "changed");
        File.WriteAllText(Path.Combine(output, "stale"), "stale");
        DateTime timestamp = new(2025, 1, 2, 3, 4, 6, DateTimeKind.Utc);

        new PersistentCache(cache.CacheDirectory).TryRestore(Key, outputs, timestamp).ShouldBeTrue();
        File.ReadAllText(nested).ShouldBe("cached");
        File.Exists(Path.Combine(output, "stale")).ShouldBeFalse();
        File.GetLastWriteTimeUtc(nested).ShouldBe(timestamp);
        File.Exists(Path.Combine(cache.CacheDirectory, "v1", Key, "last-used")).ShouldBeTrue();
    }

    [Fact]
    public void FileSetRestoresPresentAndAbsentFilesWithoutTouchingOtherFiles()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        string present = Path.Combine(root, "output", "present");
        string absent = Path.Combine(root, "other", "absent");
        Directory.CreateDirectory(Path.GetDirectoryName(present)!);
        File.WriteAllText(present, "cached");
        string[] paths = [present, absent];
        PersistentCacheOutputs outputs = PersistentCacheOutputs.FromFiles(paths);
        paths[0] = Path.Combine(root, "wrong");

        cache.TryStore(Key, outputs).ShouldBeTrue();
        File.Delete(present);
        Directory.CreateDirectory(Path.GetDirectoryName(absent)!);
        File.WriteAllText(absent, "stale");
        string unrelated = Path.Combine(root, "output", "unrelated");
        File.WriteAllText(unrelated, "keep");

        cache.TryRestore(Key, outputs, DateTime.UtcNow).ShouldBeTrue();
        File.ReadAllText(present).ShouldBe("cached");
        File.Exists(absent).ShouldBeFalse();
        File.ReadAllText(unrelated).ShouldBe("keep");
    }

    [Fact]
    public void StoredEntriesAreImmutable()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        string file = Path.Combine(root, "result");
        PersistentCacheOutputs outputs = PersistentCacheOutputs.FromFiles(file);
        File.WriteAllText(file, "first");
        cache.TryStore(Key, outputs).ShouldBeTrue();
        File.WriteAllText(file, "second");
        cache.TryStore(Key, outputs).ShouldBeFalse();
        cache.TryRestore(Key, outputs, DateTime.UtcNow).ShouldBeTrue();
        File.ReadAllText(file).ShouldBe("first");
    }

    [Fact]
    public void MissingEntryDoesNotCreateOrChangeFiles()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        cache.TryRestore(Key, PersistentCacheOutputs.FromDirectory(Path.Combine(root, "output")), DateTime.UtcNow).ShouldBeFalse();
        Directory.GetFileSystemEntries(root).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("contents")]
    [InlineData("missing-file")]
    [InlineData("manifest")]
    [InlineData("checksum")]
    [InlineData("version")]
    [InlineData("traversal")]
    [InlineData("duplicate")]
    [InlineData("trailing-data")]
    public void InvalidEntryDoesNotModifyOutputs(string corruption)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        string output = Path.Combine(root, "output");
        Directory.CreateDirectory(output);
        string file = Path.Combine(output, "result");
        File.WriteAllText(file, "cached");
        PersistentCacheOutputs outputs = PersistentCacheOutputs.FromDirectory(output);
        cache.TryStore(Key, outputs).ShouldBeTrue();
        string entry = Path.Combine(cache.CacheDirectory, "v1", Key);
        string manifest = Path.Combine(entry, "manifest");

        switch (corruption)
        {
            case "contents":
                File.WriteAllText(Path.Combine(entry, "outputs", "result"), "broken");
                break;
            case "missing-file":
                File.Delete(Path.Combine(entry, "outputs", "result"));
                break;
            case "manifest":
                File.WriteAllBytes(manifest, []);
                break;
            case "checksum":
                File.WriteAllBytes(Path.Combine(entry, "manifest.sha256"), []);
                break;
            case "version":
                using (var stream = File.OpenWrite(manifest))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(99);
                }

                UpdateManifestChecksum(entry);
                break;
            case "traversal":
            case "duplicate":
                using (var writer = new BinaryWriter(File.Create(manifest)))
                {
                    writer.Write(1);
                    writer.Write(false);
                    writer.Write(2);
                    for (int i = 0; i < 2; i++)
                    {
                        writer.Write(corruption == "traversal" ? "../escape" : "result");
                        writer.Write(true);
                        string cachedFile = Path.Combine(entry, "outputs", "result");
                        writer.Write(new FileInfo(cachedFile).Length);
                        using var sha256 = SHA256.Create();
                        writer.Write(sha256.ComputeHash(File.ReadAllBytes(cachedFile)));
                    }
                }

                UpdateManifestChecksum(entry);
                break;
            case "trailing-data":
                using (var stream = new FileStream(manifest, FileMode.Append))
                {
                    stream.WriteByte(0);
                }

                UpdateManifestChecksum(entry);
                break;
        }

        File.WriteAllText(file, "keep");
        cache.TryRestore(Key, outputs, DateTime.UtcNow).ShouldBeFalse();
        File.ReadAllText(file).ShouldBe("keep");
        File.Exists(Path.Combine(root, "escape")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("directory-is-cache")]
    [InlineData("directory-in-cache")]
    [InlineData("directory-contains-cache")]
    [InlineData("file-is-cache")]
    [InlineData("file-in-cache")]
    [InlineData("file-contains-cache")]
    [InlineData("duplicate-file")]
    [InlineData("nested-files")]
    public void OverlappingPathsAreRejectedBeforeIO(string overlap)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        string file = Path.Combine(root, "result");
        PersistentCacheOutputs outputs = overlap switch
        {
            "directory-is-cache" => PersistentCacheOutputs.FromDirectory(cache.CacheDirectory),
            "directory-in-cache" => PersistentCacheOutputs.FromDirectory(Path.Combine(cache.CacheDirectory, "nested")),
            "directory-contains-cache" => PersistentCacheOutputs.FromDirectory(root),
            "file-is-cache" => PersistentCacheOutputs.FromFiles(cache.CacheDirectory),
            "file-in-cache" => PersistentCacheOutputs.FromFiles(Path.Combine(cache.CacheDirectory, "nested")),
            "file-contains-cache" => PersistentCacheOutputs.FromFiles(root),
            "duplicate-file" => PersistentCacheOutputs.FromFiles(file, file),
            _ => PersistentCacheOutputs.FromFiles(file, Path.Combine(file, "nested"))
        };

        Should.Throw<ArgumentException>(() => cache.TryStore(Key, outputs));
        Should.Throw<ArgumentException>(() => cache.TryRestore(Key, outputs, DateTime.UtcNow));
        Directory.GetFileSystemEntries(root).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void InvalidKeysAreRejected(string key)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        PersistentCacheOutputs outputs = PersistentCacheOutputs.FromFiles();
        Should.Throw<ArgumentException>(() => cache.TryStore(key, outputs));
        Should.Throw<ArgumentException>(() => cache.TryRestore(key, outputs, DateTime.UtcNow));
        Directory.GetFileSystemEntries(root).ShouldBeEmpty();
    }

    [Fact]
    public void InvalidTimestampIsRejectedBeforeRestore()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        Should.Throw<ArgumentException>(() => cache.TryRestore(Key, PersistentCacheOutputs.FromFiles(), DateTime.Now));
        Directory.GetFileSystemEntries(root).ShouldBeEmpty();
    }

    [Fact]
    public void StorageFailureLeavesNoPublishedOrStagedEntry()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        cache.TryStore(Key, PersistentCacheOutputs.FromDirectory(Path.Combine(root, "missing"))).ShouldBeFalse();
        Directory.Exists(cache.CacheDirectory).ShouldBeFalse();

        string directory = Path.Combine(root, "directory");
        Directory.CreateDirectory(directory);
        cache.TryStore(Key, PersistentCacheOutputs.FromFiles(directory)).ShouldBeFalse();
        Directory.GetFileSystemEntries(Path.Combine(cache.CacheDirectory, "v1")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ConcurrentWritersPublishOneCompleteEntry()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        string file = Path.Combine(root, "result");
        File.WriteAllText(file, "cached");
        PersistentCacheOutputs outputs = PersistentCacheOutputs.FromFiles(file);
        bool[] stored = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            new PersistentCache(cache.CacheDirectory).TryStore(Key, outputs))));
        stored.Count(value => value).ShouldBe(1);
        Directory.GetDirectories(Path.Combine(cache.CacheDirectory, "v1")).Length.ShouldBe(1);
        File.WriteAllText(file, "changed");
        cache.TryRestore(Key, outputs, DateTime.UtcNow).ShouldBeTrue();
        File.ReadAllText(file).ShouldBe("cached");
    }

    [Fact]
    public void KeyTracksContentsAndAbsenceButNotTimestamps()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string input = Path.Combine(root, "input");
        string[] paths = [input];
        PersistentCacheInputs inputs = PersistentCacheInputs.FromFiles(paths);
        paths[0] = Path.Combine(root, "wrong");
        PersistentCacheOutputs outputs = PersistentCacheOutputs.FromFiles(Path.Combine(root, "result"));
        string missing = PersistentCache.ComputeKey(inputs, outputs);
        File.WriteAllBytes(input, []);
        string empty = PersistentCache.ComputeKey(inputs, outputs);
        empty.ShouldNotBe(missing);
        File.WriteAllText(input, "one");
        string first = PersistentCache.ComputeKey(inputs, outputs);
        first.ShouldNotBe(empty);
        File.SetLastWriteTimeUtc(input, DateTime.UtcNow.AddDays(-1));
        PersistentCache.ComputeKey(inputs, outputs).ShouldBe(first);
        File.WriteAllText(input, "two");
        PersistentCache.ComputeKey(inputs, outputs).ShouldNotBe(first);
        File.Delete(input);
        PersistentCache.ComputeKey(inputs, outputs).ShouldBe(missing);
    }

    [Fact]
    public void KeyTracksOrderedPathsOutputContractAndContext()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string first = Path.Combine(root, "first");
        string second = Path.Combine(root, "second");
        File.WriteAllText(first, "same");
        File.WriteAllText(second, "same");
        PersistentCacheInputs inputs = PersistentCacheInputs.FromFiles(first, second);
        PersistentCacheOutputs outputs = PersistentCacheOutputs.FromFiles(Path.Combine(root, "result"));
        string key = PersistentCache.ComputeKey(inputs, outputs, "ab", "c");

        PersistentCache.ComputeKey(inputs, outputs, "ab", "c").ShouldBe(key);
        PersistentCache.ComputeKey(inputs, outputs, "a", "bc").ShouldNotBe(key);
        PersistentCache.ComputeKey(PersistentCacheInputs.FromFiles(second, first), outputs, "ab", "c").ShouldNotBe(key);
        PersistentCache.ComputeKey(inputs, PersistentCacheOutputs.FromDirectory(Path.Combine(root, "result")), "ab", "c").ShouldNotBe(key);
        PersistentCache.ComputeKey(inputs, PersistentCacheOutputs.FromFiles(Path.Combine(root, "other")), "ab", "c").ShouldNotBe(key);
    }

    [Fact]
    public void DifferentOutputContractsCannotRestoreTheSameEntry()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new PersistentCache(Path.Combine(root, "cache"));
        string directory = Path.Combine(root, "output");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "result");
        File.WriteAllText(file, "cached");
        cache.TryStore(Key, PersistentCacheOutputs.FromFiles(file)).ShouldBeTrue();
        File.WriteAllText(file, "keep");

        cache.TryRestore(Key, PersistentCacheOutputs.FromDirectory(directory), DateTime.UtcNow).ShouldBeFalse();
        cache.TryRestore(Key, PersistentCacheOutputs.FromFiles(file, Path.Combine(root, "absent")), DateTime.UtcNow).ShouldBeFalse();
        File.ReadAllText(file).ShouldBe("keep");
    }

    private static void UpdateManifestChecksum(string entry)
    {
        using var sha256 = SHA256.Create();
        File.WriteAllBytes(Path.Combine(entry, "manifest.sha256"), sha256.ComputeHash(File.ReadAllBytes(Path.Combine(entry, "manifest"))));
    }
}
