// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Task = System.Threading.Tasks.Task;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.UnitTests;

public sealed class InvocationCache_Tests
{
    private const string Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private readonly ITestOutputHelper _output;

    public InvocationCache_Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DirectoryRoundTripReconcilesContentsAndTimestamps()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        string output = Path.Combine(root, "output");
        string nested = Path.Combine(output, "sub", "result");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(nested, "cached");
        InvocationOutputs outputs = InvocationOutputs.FromDirectory(output);

        cache.Store(Key, outputs);
        File.WriteAllText(nested, "changed");
        File.WriteAllText(Path.Combine(output, "stale"), "stale");
        DateTime timestamp = new(2025, 1, 2, 3, 4, 6, DateTimeKind.Utc);

        new InvocationCache(cache.CacheDirectory, new TestTask { BuildEngine = new MockEngine() }.Log, "Test").TryRestore(Key, outputs, timestamp, out _).ShouldBeTrue();
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
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        string present = Path.Combine(root, "output", "present");
        string absent = Path.Combine(root, "other", "absent");
        Directory.CreateDirectory(Path.GetDirectoryName(present)!);
        File.WriteAllText(present, "cached");
        string[] paths = [present, absent];
        InvocationOutputs outputs = InvocationOutputs.FromFiles(paths);
        paths[0] = Path.Combine(root, "wrong");

        cache.Store(Key, outputs);
        File.Delete(present);
        Directory.CreateDirectory(Path.GetDirectoryName(absent)!);
        File.WriteAllText(absent, "stale");
        string unrelated = Path.Combine(root, "output", "unrelated");
        File.WriteAllText(unrelated, "keep");

        cache.TryRestore(Key, outputs, DateTime.UtcNow, out _).ShouldBeTrue();
        File.ReadAllText(present).ShouldBe("cached");
        File.Exists(absent).ShouldBeFalse();
        File.ReadAllText(unrelated).ShouldBe("keep");
    }

    [Fact]
    public void StoredEntriesAreImmutable()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        string file = Path.Combine(root, "result");
        InvocationOutputs outputs = InvocationOutputs.FromFiles(file);
        File.WriteAllText(file, "first");
        cache.Store(Key, outputs);
        File.WriteAllText(file, "second");
        cache.Store(Key, outputs);
        cache.TryRestore(Key, outputs, DateTime.UtcNow, out _).ShouldBeTrue();
        File.ReadAllText(file).ShouldBe("first");
    }

    [Fact]
    public void MissingEntryDoesNotCreateOrChangeFiles()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        cache.TryRestore(Key, InvocationOutputs.FromDirectory(Path.Combine(root, "output")), DateTime.UtcNow, out _).ShouldBeFalse();
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
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine(_output) }.Log, "Test");
        string output = Path.Combine(root, "output");
        Directory.CreateDirectory(output);
        string file = Path.Combine(output, "result");
        File.WriteAllText(file, "cached");
        InvocationOutputs outputs = InvocationOutputs.FromDirectory(output);
        cache.Store(Key, outputs);
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
        cache.TryRestore(Key, outputs, DateTime.UtcNow, out _).ShouldBeFalse();
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
    [InlineData("nested-files-reversed")]
    [InlineData("nested-files-with-sibling-prefix")]
    public void OverlappingPathsAreRejectedBeforeIO(string overlap)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        string file = Path.Combine(root, "result");
        InvocationOutputs outputs = overlap switch
        {
            "directory-is-cache" => InvocationOutputs.FromDirectory(cache.CacheDirectory),
            "directory-in-cache" => InvocationOutputs.FromDirectory(Path.Combine(cache.CacheDirectory, "nested")),
            "directory-contains-cache" => InvocationOutputs.FromDirectory(root),
            "file-is-cache" => InvocationOutputs.FromFiles(cache.CacheDirectory),
            "file-in-cache" => InvocationOutputs.FromFiles(Path.Combine(cache.CacheDirectory, "nested")),
            "file-contains-cache" => InvocationOutputs.FromFiles(root),
            "duplicate-file" => InvocationOutputs.FromFiles(file, file),
            "nested-files-reversed" => InvocationOutputs.FromFiles(Path.Combine(file, "nested"), file),
            "nested-files-with-sibling-prefix" => InvocationOutputs.FromFiles(file, file + "-sibling", Path.Combine(file, "nested")),
            _ => InvocationOutputs.FromFiles(file, Path.Combine(file, "nested"))
        };

        Should.Throw<ArgumentException>(() => cache.Store(Key, outputs));
        Should.Throw<ArgumentException>(() => cache.TryRestore(Key, outputs, DateTime.UtcNow, out _));
        Directory.GetFileSystemEntries(root).ShouldBeEmpty();
    }

    [Fact]
    public void NonOverlappingSiblingPrefixOutputsPreserveOrder()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        string sibling = Path.Combine(root, "result-sibling");
        string file = Path.Combine(root, "result");
        File.WriteAllText(sibling, "sibling");
        File.WriteAllText(file, "file");
        InvocationOutputs outputs = InvocationOutputs.FromFiles(sibling, file);

        cache.Store(Key, outputs);
        outputs.Files.ShouldBe([sibling, file]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void InvalidKeysAreRejected(string key)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        InvocationOutputs outputs = InvocationOutputs.FromFiles();
        Should.Throw<ArgumentException>(() => cache.Store(key, outputs));
        Should.Throw<ArgumentException>(() => cache.TryRestore(key, outputs, DateTime.UtcNow, out _));
        Directory.GetFileSystemEntries(root).ShouldBeEmpty();
    }

    [Fact]
    public void InvalidTimestampIsRejectedBeforeRestore()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        Should.Throw<ArgumentException>(() => cache.TryRestore(Key, InvocationOutputs.FromFiles(), DateTime.Now, out _));
        Directory.GetFileSystemEntries(root).ShouldBeEmpty();
    }

    [Fact]
    public void StorageFailureLeavesNoPublishedOrStagedEntry()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        cache.Store(Key, InvocationOutputs.FromDirectory(Path.Combine(root, "missing")));
        Directory.Exists(cache.CacheDirectory).ShouldBeFalse();

        string directory = Path.Combine(root, "directory");
        Directory.CreateDirectory(directory);
        cache.Store(Key, InvocationOutputs.FromFiles(directory));
        Directory.GetFileSystemEntries(Path.Combine(cache.CacheDirectory, "v1")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ConcurrentWritersPublishOneCompleteEntry()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        string file = Path.Combine(root, "result");
        File.WriteAllText(file, "cached");
        InvocationOutputs outputs = InvocationOutputs.FromFiles(file);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            new InvocationCache(cache.CacheDirectory, new TestTask { BuildEngine = new MockEngine() }.Log, "Test").Store(Key, outputs))));
        Directory.GetDirectories(Path.Combine(cache.CacheDirectory, "v1")).Length.ShouldBe(1);
        File.WriteAllText(file, "changed");
        cache.TryRestore(Key, outputs, DateTime.UtcNow, out _).ShouldBeTrue();
        File.ReadAllText(file).ShouldBe("cached");
    }

    [Fact]
    public void KeyTracksContentsAndAbsenceButNotTimestamps()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string input = Path.Combine(root, "input");
        string[] paths = [input];
        InvocationInputs inputs = InvocationInputs.FromFiles(paths);
        paths[0] = Path.Combine(root, "wrong");
        InvocationOutputs outputs = InvocationOutputs.FromFiles(Path.Combine(root, "result"));
        string missing = InvocationCache.ComputeKey(new InvocationDescription(inputs, [], outputs));
        File.WriteAllBytes(input, []);
        string empty = InvocationCache.ComputeKey(new InvocationDescription(inputs, [], outputs));
        empty.ShouldNotBe(missing);
        File.WriteAllText(input, "one");
        string first = InvocationCache.ComputeKey(new InvocationDescription(inputs, [], outputs));
        first.ShouldNotBe(empty);
        File.SetLastWriteTimeUtc(input, DateTime.UtcNow.AddDays(-1));
        InvocationCache.ComputeKey(new InvocationDescription(inputs, [], outputs)).ShouldBe(first);
        File.WriteAllText(input, "two");
        InvocationCache.ComputeKey(new InvocationDescription(inputs, [], outputs)).ShouldNotBe(first);
        File.Delete(input);
        InvocationCache.ComputeKey(new InvocationDescription(inputs, [], outputs)).ShouldBe(missing);
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
        InvocationInputs inputs = InvocationInputs.FromFiles(first, second);
        InvocationOutputs outputs = InvocationOutputs.FromFiles(Path.Combine(root, "result"));
        string key = InvocationCache.ComputeKey(new InvocationDescription(inputs, ["ab", "c"], outputs));

        InvocationCache.ComputeKey(new InvocationDescription(inputs, ["ab", "c"], outputs)).ShouldBe(key);
        InvocationCache.ComputeKey(new InvocationDescription(inputs, ["a", "bc"], outputs)).ShouldNotBe(key);
        InvocationCache.ComputeKey(new InvocationDescription(InvocationInputs.FromFiles(second, first), ["ab", "c"], outputs)).ShouldNotBe(key);
        InvocationCache.ComputeKey(new InvocationDescription(inputs, ["ab", "c"], InvocationOutputs.FromDirectory(Path.Combine(root, "result")))).ShouldNotBe(key);
        InvocationCache.ComputeKey(new InvocationDescription(inputs, ["ab", "c"], InvocationOutputs.FromFiles(Path.Combine(root, "other")))).ShouldNotBe(key);
    }

    [Fact]
    public void KeyUsesExplicitWorkingDirectoryWithoutChangingProcessDirectory()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.SetCurrentDirectory(root);
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), [], InvocationOutputs.FromFiles());

        string processKey = InvocationCache.ComputeKey(invocation);
        InvocationCache.ComputeKey(invocation, root).ShouldBe(processKey);
        InvocationCache.ComputeKey(invocation, Path.Combine(root, "other")).ShouldNotBe(processKey);
        Environment.CurrentDirectory.ShouldBe(root);
    }

    [Fact]
    public void DifferentOutputContractsCannotRestoreTheSameEntry()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        string directory = Path.Combine(root, "output");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "result");
        File.WriteAllText(file, "cached");
        cache.Store(Key, InvocationOutputs.FromFiles(file));
        File.WriteAllText(file, "keep");

        cache.TryRestore(Key, InvocationOutputs.FromDirectory(directory), DateTime.UtcNow, out _).ShouldBeFalse();
        cache.TryRestore(Key, InvocationOutputs.FromFiles(file, Path.Combine(root, "absent")), DateTime.UtcNow, out _).ShouldBeFalse();
        File.ReadAllText(file).ShouldBe("keep");
    }

    private static void UpdateManifestChecksum(string entry)
    {
        using var sha256 = SHA256.Create();
        File.WriteAllBytes(Path.Combine(entry, "manifest.sha256"), sha256.ComputeHash(File.ReadAllBytes(Path.Combine(entry, "manifest"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutionRoundTripsFilesAndRawDiagnostics(bool ownedDirectory)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var engine = new MockEngine(_output);
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = engine }.Log, "Test");
        string directory = Path.Combine(root, "output");
        string file = Path.Combine(directory, "result");
        string absent = Path.Combine(directory, "absent");
        Directory.CreateDirectory(directory);
        InvocationOutputs outputs = ownedDirectory ? InvocationOutputs.FromDirectory(directory) : InvocationOutputs.FromFiles(file, absent);
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), ["arguments"], outputs);
        string[] stdout = ["warning CACHE0001: warning", "", "stdout \u00e9"];
        string[] stderr = ["stderr", ""];
        int executions = 0;

        InvocationResult Execute()
        {
            executions++;
            File.WriteAllText(file, "cached");
            return new InvocationResult(0, stdout, stderr);
        }

        cache.TryExecuteCached(invocation, Execute, out InvocationResult cold).ShouldBeTrue();
        cold.StandardOutput.ShouldBeSameAs(stdout);
        cold.StandardError.ShouldBeSameAs(stderr);
        File.WriteAllText(file, "changed");
        File.WriteAllText(absent, "stale");
        string unrelated = Path.Combine(directory, "unrelated");
        File.WriteAllText(unrelated, "keep");

        cache.TryExecuteCached(invocation, Execute, out InvocationResult warm).ShouldBeTrue();
        executions.ShouldBe(1);
        warm.ExitCode.ShouldBe(0);
        warm.StandardOutput.ShouldBe(stdout);
        warm.StandardError.ShouldBe(stderr);
        engine.Warnings.ShouldBe(0);
        engine.Errors.ShouldBe(0);
        File.ReadAllText(file).ShouldBe("cached");
        File.Exists(absent).ShouldBeFalse();
        File.Exists(unrelated).ShouldBe(!ownedDirectory);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(42, false)]
    [InlineData(42, true)]
    public void AdmissionDependsOnToolExitCodeNotLoggingPolicy(int exitCode, bool logError)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var task = new TestTask { BuildEngine = new MockEngine(_output) };
        var cache = new InvocationCache(Path.Combine(root, "cache"), task.Log, "Test");
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), [], InvocationOutputs.FromFiles());
        int executions = 0;

        InvocationResult Execute()
        {
            executions++;
            if (logError)
            {
                task.Log.LogError("task error");
            }

            return new InvocationResult(exitCode, ["stdout"], ["stderr"]);
        }

        for (int i = 0; i < 2; i++)
        {
            cache.TryExecuteCached(invocation, Execute, out InvocationResult result).ShouldBeTrue();
            result.ExitCode.ShouldBe(exitCode);
            result.StandardOutput.ShouldBe(["stdout"]);
            result.StandardError.ShouldBe(["stderr"]);
        }

        executions.ShouldBe(exitCode == 0 ? 1 : 2);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("contents")]
    [InlineData("negative-count")]
    [InlineData("oversized-count")]
    [InlineData("truncated-line")]
    [InlineData("trailing-data")]
    [InlineData("legacy-manifest")]
    public void InvalidDiagnosticsAreRejectedBeforeChangingOutputs(string corruption)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine(_output) }.Log, "Test");
        string file = Path.Combine(root, "result");
        File.WriteAllText(file, "cached");
        InvocationOutputs outputs = InvocationOutputs.FromFiles(file);
        cache.Store(Key, outputs, ["stdout"], ["stderr"]);
        string entry = Path.Combine(cache.CacheDirectory, "v1", Key);
        string diagnostics = Path.Combine(entry, "diagnostics");
        string manifest = Path.Combine(entry, "manifest");

        switch (corruption)
        {
            case "missing":
                File.Delete(diagnostics);
                break;
            case "contents":
                File.WriteAllBytes(diagnostics, []);
                break;
            case "legacy-manifest":
                using (var stream = File.OpenWrite(manifest))
                {
                    stream.SetLength(stream.Length - 40);
                }

                UpdateManifestChecksum(entry);
                break;
            default:
                using (var writer = new BinaryWriter(File.Create(diagnostics)))
                {
                    if (corruption is "negative-count" or "oversized-count")
                    {
                        writer.Write(corruption == "negative-count" ? -1 : int.MaxValue);
                        writer.Write(0);
                    }
                    else if (corruption == "truncated-line")
                    {
                        writer.Write(1);
                        writer.Write((byte)100);
                    }
                    else
                    {
                        writer.Write(0);
                        writer.Write(0);
                        writer.Write((byte)0);
                    }
                }

                using (var stream = File.OpenWrite(manifest))
                using (var writer = new BinaryWriter(stream))
                {
                    stream.Position = stream.Length - 40;
                    writer.Write(new FileInfo(diagnostics).Length);
                    using var sha256 = SHA256.Create();
                    writer.Write(sha256.ComputeHash(File.ReadAllBytes(diagnostics)));
                }

                UpdateManifestChecksum(entry);
                break;
        }

        File.WriteAllText(file, "keep");
        cache.TryRestore(Key, outputs, DateTime.UtcNow, out InvocationResult result).ShouldBeFalse();
        result.ExitCode.ShouldBe(-1);
        result.StandardOutput.ShouldBeEmpty();
        result.StandardError.ShouldBeEmpty();
        File.ReadAllText(file).ShouldBe("keep");
    }

    [Fact]
    public void InputIdentityFailureBypassesWithoutExecutingOrCleaningOutputs()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var engine = new MockEngine(_output);
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = engine }.Log, "Test");
        string output = Path.Combine(root, "result");
        File.WriteAllText(output, "keep");
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(root), [], InvocationOutputs.FromFiles(output));
        bool executed = false;

        cache.TryExecuteCached(invocation, () =>
        {
            executed = true;
            return new InvocationResult(0);
        }, out InvocationResult result).ShouldBeFalse();

        executed.ShouldBeFalse();
        result.ExitCode.ShouldBe(-1);
        File.ReadAllText(output).ShouldBe("keep");
        Directory.Exists(cache.CacheDirectory).ShouldBeFalse();
        engine.Log.ShouldContain("input identity could not be computed");
    }

    [Fact]
    public void ExecutionExceptionsAreNotRetriedOrStored()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), [], InvocationOutputs.FromFiles());
        var expected = new InvalidOperationException("execution failure");
        int executions = 0;

        Should.Throw<InvalidOperationException>(() => cache.TryExecuteCached(invocation, () =>
        {
            executions++;
            throw expected;
        }, out _)).ShouldBeSameAs(expected);

        executions.ShouldBe(1);
        Directory.Exists(cache.CacheDirectory).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("false", false)]
    [InlineData("TRUE", true)]
    [InlineData("1", false)]
    public void ConfigurationReadsCurrentEnvironmentWithoutCreatingStorage(string? enabled, bool expected)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, enabled);
        string first = Path.Combine(root, "first");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, first);
        var task = new TestTask { BuildEngine = new MockEngine(_output) };

        InvocationCache? cache = InvocationCache.TryCreateFromEnvironment(task.Log, TestCachedToolTask.Configuration);
        (cache is not null).ShouldBe(expected);
        cache?.CacheDirectory.ShouldBe(first);
        string second = Path.Combine(root, "second");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, second);
        InvocationCache.TryCreateFromEnvironment(task.Log, TestCachedToolTask.Configuration)?.CacheDirectory.ShouldBe(second);
        Directory.GetFileSystemEntries(root).ShouldBeEmpty();
    }

    [LinuxOnlyFact]
    public void ConfigurationUsesTaskLocalXdgCacheHome()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, null);
        env.SetEnvironmentVariable("XDG_CACHE_HOME", Path.Combine(root, "process"));
        TaskEnvironment taskEnvironment = env.WithTransientTestState(new TransientTaskEnvironment(root)).Environment;
        taskEnvironment.SetEnvironmentVariable("XDG_CACHE_HOME", Path.Combine(root, "task"));
        var task = new TestTask { BuildEngine = new MockEngine(_output) };

        InvocationCache? cache = InvocationCache.TryCreateFromEnvironment(task.Log, TestCachedToolTask.Configuration, taskEnvironment);
        cache.ShouldNotBeNull();
        cache!.CacheDirectory.ShouldBe(Path.Combine(root, "task", "test"));
        Directory.GetFileSystemEntries(root).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ToolTaskGatesDiscoveryAndExecution(bool enabled, bool eligible)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, enabled.ToString());
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, Path.Combine(root, "cache"));
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), [], InvocationOutputs.FromFiles());

        for (int i = 0; i < 2; i++)
        {
            var task = new TestCachedToolTask
            {
                BuildEngine = new MockEngine(_output),
                Description = eligible ? invocation : null,
                Execution = () => 0
            };
            task.Run().ShouldBe(0);
            task.Descriptions.ShouldBe(enabled ? 1 : 0);
            task.Executions.ShouldBe(enabled && eligible && i == 1 ? 0 : 1);
        }
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 42)]
    public void ToolTaskCapturesAndReplaysUsingCurrentLoggingPolicy(bool stderrAsError, int toolExitCode)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, Path.Combine(root, "cache"));
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), [], InvocationOutputs.FromFiles());

        for (int i = 0; i < 2; i++)
        {
            var engine = new MockEngine(_output);
            var task = new TestCachedToolTask
            {
                BuildEngine = engine,
                Description = invocation,
                ToolExitCode = toolExitCode,
                LogStandardErrorAsError = i == 0 && stderrAsError
            };

            task.Execute().ShouldBe(toolExitCode == 0 && (i != 0 || !stderrAsError));
            task.Executions.ShouldBe(i == 1 && toolExitCode == 0 ? 0 : 1);
            task.ReplayedLines.ShouldBe(i == 0 && stderrAsError
                ? ["first", "warning CACHE0001: warning", "", "last"]
                : ["first", "warning CACHE0001: warning", "", "last", "stderr"]);
            engine.Warnings.ShouldBe(1);
            if (i == 0 && stderrAsError)
            {
                engine.Errors.ShouldBe(1);
            }
        }
    }

    [Fact]
    public void ToolTaskHonorsDerivedErrorPolicyAfterColdAndWarmReplay()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, Path.Combine(root, "cache"));
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), [], InvocationOutputs.FromFiles());

        for (int i = 0; i < 2; i++)
        {
            var engine = new MockEngine(_output);
            var task = new TestCachedToolTask
            {
                BuildEngine = engine,
                Description = invocation,
                LogAdditionalErrorOnReplay = true
            };

            task.Execute().ShouldBeFalse();
            task.Executions.ShouldBe(i == 0 ? 1 : 0);
            task.ExitCode.ShouldBe(-1);
            task.ExitCodeOverriddenInErrorHandler.ShouldBe(true);
            engine.Errors.ShouldBe(1);
            task.Log.HasLoggedErrors.ShouldBeFalse();
        }
    }

    [Fact]
    public void ToolTaskSetsExitCodeOverriddenFlagOnColdAndWarmReplayErrors()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, Path.Combine(root, "cache"));
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), [], InvocationOutputs.FromFiles());

        for (int i = 0; i < 2; i++)
        {
            var engine = new MockEngine(_output);
            var task = new TestCachedToolTask
            {
                BuildEngine = engine,
                Description = invocation,
                LogStandardErrorAsError = true
            };

            task.Execute().ShouldBeFalse();
            task.Executions.ShouldBe(i == 0 ? 1 : 0);
            task.ExitCode.ShouldBe(-1);
            task.ExitCodeOverriddenInErrorHandler.ShouldBe(true);
            engine.Errors.ShouldBe(1);
        }

        var failedEngine = new MockEngine(_output);
        var failedTask = new TestCachedToolTask
        {
            BuildEngine = failedEngine,
            Description = new InvocationDescription(InvocationInputs.FromFiles(), ["nonzero"], InvocationOutputs.FromFiles()),
            ToolExitCode = 42,
            LogStandardErrorAsError = true
        };

        failedTask.Execute().ShouldBeFalse();
        failedTask.ExitCode.ShouldBe(42);
        failedTask.ExitCodeOverriddenInErrorHandler.ShouldBe(false);
    }

    [Fact]
    public async Task OutputCaptureIgnoresLateWritesAfterCompletion()
    {
        var capture = new CachedToolTask.OutputCapture();
        capture.TryAddStandardOutput("before-out").ShouldBeTrue();
        capture.TryAddStandardError("before-err").ShouldBeTrue();

        using var stop = new CancellationTokenSource();
        Task writer = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                capture.TryAddStandardOutput("late-out");
                capture.TryAddStandardError("late-err");
            }
        });

        capture.Complete(out IReadOnlyList<string> standardOutput, out IReadOnlyList<string> standardError);
        int expectedOutCount = standardOutput.Count;
        int expectedErrCount = standardError.Count;

        for (int i = 0; i < 200; i++)
        {
            standardOutput.ToArray().Length.ShouldBe(expectedOutCount);
            standardError.ToArray().Length.ShouldBe(expectedErrCount);
        }

        capture.TryAddStandardOutput("after-out").ShouldBeFalse();
        capture.TryAddStandardError("after-err").ShouldBeFalse();
        standardOutput.Count.ShouldBe(expectedOutCount);
        standardError.Count.ShouldBe(expectedErrCount);

        stop.Cancel();
        await writer;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToolTaskUsesTaskLocalCacheConfiguration(bool enabled)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.SetCurrentDirectory(root);
        string projectDirectory = Path.Combine(root, "project");
        Directory.CreateDirectory(projectDirectory);
        string processCache = Path.Combine(root, "process-cache");
        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, (!enabled).ToString());
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, processCache);
        TaskEnvironment taskEnvironment = env.WithTransientTestState(new TransientTaskEnvironment(projectDirectory)).Environment;
        taskEnvironment.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, enabled.ToString());
        taskEnvironment.SetEnvironmentVariable(TestCachedToolTask.PathVariable, "task-cache");
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), [], InvocationOutputs.FromFiles());

        for (int i = 0; i < 2; i++)
        {
            var task = new TestCachedToolTask
            {
                BuildEngine = new MockEngine(_output),
                TaskEnvironment = taskEnvironment,
                Description = invocation,
                Execution = () => 0
            };

            task.Run().ShouldBe(0);
            task.Descriptions.ShouldBe(enabled ? 1 : 0);
            task.Executions.ShouldBe(enabled && i == 1 ? 0 : 1);
        }

        Directory.Exists(Path.Combine(projectDirectory, "task-cache")).ShouldBe(enabled);
        Directory.Exists(processCache).ShouldBeFalse();
        Directory.Exists(Path.Combine(root, "task-cache")).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void ToolTaskKeysUseEffectiveWorkingDirectory(bool overrideWorkingDirectory, bool useTaskEnvironment)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.SetCurrentDirectory(root);
        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, Path.Combine(root, "cache"));
        string projectDirectory = Path.Combine(root, "project");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "first"));
        Directory.CreateDirectory(Path.Combine(projectDirectory, "second"));
        var invocation = new InvocationDescription(InvocationInputs.FromFiles(), [], InvocationOutputs.FromFiles());

        for (int i = 0; i < 3; i++)
        {
            string directoryName = i == 1 ? "second" : "first";
            TaskEnvironment taskEnvironment = useTaskEnvironment
                ? env.WithTransientTestState(new TransientTaskEnvironment(
                    overrideWorkingDirectory ? projectDirectory : Path.Combine(projectDirectory, directoryName))).Environment
                : TaskEnvironment.Fallback;
            var task = new TestCachedToolTask
            {
                BuildEngine = new MockEngine(_output),
                TaskEnvironment = taskEnvironment,
                Description = invocation,
                WorkingDirectoryOverride = overrideWorkingDirectory
                    ? useTaskEnvironment ? directoryName : Path.Combine("project", directoryName)
                    : null
            };

            task.Execute().ShouldBeTrue();
            task.Executions.ShouldBe(i == 2 ? 0 : 1);
            if (i != 2)
            {
                task.WorkingDirectoryUsed.ShouldBe(Path.Combine(projectDirectory, directoryName));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PurgeHonorsLastUseAndIgnoresUnpublishedEntries(bool expired)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        cache.Store(Key, InvocationOutputs.FromFiles());
        string entry = Path.Combine(cache.CacheDirectory, "v1", Key);
        string staging = Path.Combine(cache.CacheDirectory, "v1", Key + ".tmp");
        Directory.CreateDirectory(staging);
        DateTimeOffset cutoff = DateTimeOffset.UtcNow;
        File.WriteAllText(Path.Combine(entry, "last-used"), cutoff.AddDays(expired ? -1 : 1).ToString("O"));
        using var output = new StringWriter();
        using var error = new StringWriter();

        InvocationCachePurge.Run(cache.CacheDirectory, "v1", "Test", cutoff, output, error).ShouldBe(0);

        Directory.Exists(entry).ShouldBe(!expired);
        Directory.Exists(staging).ShouldBeTrue();
        error.ToString().ShouldBeEmpty();
        output.ToString().ShouldContain(expired ? "Deleted: 1, Kept: 0" : "Deleted: 0, Kept: 1");
    }

    [Fact]
    public void TaskEnvironmentOverloadsResolveRelativePathsAndDefaultHome()
    {
        using TestEnvironment env = TestEnvironment.Create();
        string projectDir = env.CreateFolder().Path;
        string inputPath = Path.Combine(projectDir, "input.txt");
        File.WriteAllText(inputPath, "hello");
        string outputDir = Path.Combine(projectDir, "out");
        string cacheDir = Path.Combine(projectDir, "cache");
        TaskEnvironment taskEnv = env.WithTransientTestState(new TransientTaskEnvironment(projectDir)).Environment;

        InvocationInputs relativeInputs = InvocationInputs.FromFiles(["input.txt"], taskEnv);
        InvocationOutputs relativeOutputs = InvocationOutputs.FromDirectory("out", taskEnv);
        InvocationOutputs relativeFileOutputs = InvocationOutputs.FromFiles(["out/a.txt"], taskEnv);
        var description = new InvocationDescription(relativeInputs, ["arg"], relativeOutputs);

        InvocationCache? cache = InvocationCache.TryCreate("cache", new TestTask { BuildEngine = new MockEngine() }.Log, "Test", taskEnv);
        cache.ShouldNotBeNull();
        cache.CacheDirectory.ShouldBe(Path.GetFullPath(cacheDir));
        relativeFileOutputs.Files.ShouldBe([Path.GetFullPath(Path.Combine(outputDir, "a.txt"))]);

        int executions = 0;
        cache.TryExecuteCached(description, () =>
        {
            executions++;
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "a.txt"), "output");
            return new InvocationResult(0);
        }, out InvocationResult first).ShouldBeTrue();
        first.ExitCode.ShouldBe(0);
        executions.ShouldBe(1);

        // Second call with explicit canonicalized paths and explicit projectDir must hit the same entry.
        var absoluteDescription = new InvocationDescription(
            InvocationInputs.FromFiles(inputPath),
            ["arg"],
            InvocationOutputs.FromDirectory(outputDir));
        cache.TryExecuteCached(absoluteDescription, () =>
        {
            executions++;
            return new InvocationResult(0);
        }, projectDir, out InvocationResult second).ShouldBeTrue();
        second.ExitCode.ShouldBe(0);
        executions.ShouldBe(1);

        // GetDefaultCacheDirectory honors task-local LOCALAPPDATA (Windows) or HOME (Unix).
        string fakeHome = env.CreateFolder().Path;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            taskEnv.SetEnvironmentVariable("LOCALAPPDATA", fakeHome);
            InvocationCacheConfiguration.GetDefaultCacheDirectory("Test", taskEnv).ShouldBe(Path.Combine(fakeHome, "test"));
        }
        else
        {
            taskEnv.SetEnvironmentVariable("XDG_CACHE_HOME", null);
            taskEnv.SetEnvironmentVariable("HOME", fakeHome);
            string expected = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Path.Combine(fakeHome, "Library", "Caches", "test")
                : Path.Combine(fakeHome, ".cache", "test");
            InvocationCacheConfiguration.GetDefaultCacheDirectory("Test", taskEnv).ShouldBe(expected);
        }
    }

    [Fact]
    public void CacheCanonicalizesPathsAndRejectsInvalidToolNamesAndWhitespaceCacheDirectory()
    {
        using TestEnvironment env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        TaskLoggingHelper log = new TestTask { BuildEngine = new MockEngine() }.Log;

        string nonCanonicalCache = Path.Combine(root, "sub", "..", "cache");
        var cache = new InvocationCache(nonCanonicalCache, log, "Test");
        cache.CacheDirectory.ShouldBe(Path.GetFullPath(Path.Combine(root, "cache")));

        // Overlap validation must catch output inside the canonical cache path.
        Should.Throw<ArgumentException>(() => cache.ValidateOutputs(InvocationOutputs.FromDirectory(Path.Combine(root, "cache", "out"))));

        TaskEnvironment taskEnv = env.WithTransientTestState(new TransientTaskEnvironment(root)).Environment;
        InvocationCache? fromTryCreate = InvocationCache.TryCreate(Path.Combine("sub", "..", "cache"), log, "Test", taskEnv);
        fromTryCreate.ShouldNotBeNull();
        fromTryCreate.CacheDirectory.ShouldBe(Path.GetFullPath(Path.Combine(root, "cache")));

        // Whitespace cacheDirectory falls back to platform default instead of creating a literal space-named directory.
        string fakeHome = env.CreateFolder().Path;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            taskEnv.SetEnvironmentVariable("LOCALAPPDATA", fakeHome);
        }
        else
        {
            taskEnv.SetEnvironmentVariable("XDG_CACHE_HOME", fakeHome);
        }

        InvocationCache? whitespaceCache = InvocationCache.TryCreate("   ", log, "Test", taskEnv);
        whitespaceCache.ShouldNotBeNull();
        whitespaceCache.CacheDirectory.ShouldBe(Path.Combine(fakeHome, "test"));

        foreach (string badToolName in new[] { "", "   ", ".", "..", "a/b", "a\\b" })
        {
            Should.Throw<ArgumentException>(() => new InvocationCacheConfiguration(badToolName, "E", "P"));
            Should.Throw<ArgumentException>(() => new InvocationCache(Path.Combine(root, "cache"), log, badToolName));
            Should.Throw<ArgumentException>(() => InvocationCache.TryCreate(Path.Combine(root, "cache"), log, badToolName, taskEnv));
        }
    }

    [Fact]
    public void CacheHitLoggingFailureDoesNotReExecuteTool()
    {
        using TestEnvironment env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        string cacheDir = Path.Combine(root, "cache");
        string inputFile = Path.Combine(root, "in.txt");
        string outputDir = Path.Combine(root, "out");
        File.WriteAllText(inputFile, "data");

        var description = new InvocationDescription(
            InvocationInputs.FromFiles(inputFile),
            ["arg"],
            InvocationOutputs.FromDirectory(outputDir));

        var seedCache = new InvocationCache(cacheDir, new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        seedCache.TryExecuteCached(description, () =>
        {
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "a.txt"), "ok");
            return new InvocationResult(0);
        }, out _).ShouldBeTrue();

        var throwingEngine = new ThrowingOnHitMockEngine();
        var hitCache = new InvocationCache(cacheDir, new TestTask { BuildEngine = throwingEngine }.Log, "Test");
        int executions = 0;

        Should.Throw<IOException>(() => hitCache.TryExecuteCached(description, () =>
        {
            executions++;
            return new InvocationResult(0);
        }, out _));

        executions.ShouldBe(0);
    }

    [Fact]
    public void CachedToolTaskReplaysPartialOutputAndReleasesCaptureWhenExecuteToolCoreThrows()
    {
        using TestEnvironment env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        string cacheDir = Path.Combine(root, "cache");
        string outputDir = Path.Combine(root, "out");
        TaskEnvironment taskEnv = env.WithTransientTestState(new TransientTaskEnvironment(root)).Environment;
        taskEnv.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        taskEnv.SetEnvironmentVariable(TestCachedToolTask.PathVariable, cacheDir);

        var engine = new MockEngine();
        var task = new TestCachedToolTask
        {
            BuildEngine = engine,
            TaskEnvironment = taskEnv,
            Description = new InvocationDescription(
                InvocationInputs.FromFiles(),
                ["throw-case"],
                InvocationOutputs.FromDirectory(outputDir)),
        };
        task.Execution = () =>
        {
            task.TryCaptureStandardOutputData("partial-stdout").ShouldBeTrue();
            task.TryCaptureStandardErrorData("partial-stderr").ShouldBeTrue();
            throw new InvalidOperationException("boom");
        };

        Should.Throw<InvalidOperationException>(() => task.Run());
        task.ReplayedLines.ShouldContain("partial-stdout");
        task.ReplayedLines.ShouldContain("partial-stderr");
        Directory.Exists(Path.Combine(cacheDir, "v1")).ShouldBeFalse();

        // Capture must be cleared so subsequent non-cached calls are not captured.
        task.TryCaptureStandardOutputData("after-throw").ShouldBeFalse();
    }

    [Fact]
    public void CachedToolTaskSupportsResourceManagerConstructorsAndCustomStartToolProcess()
    {
        using TestEnvironment env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        string cacheDir = Path.Combine(root, "cache");
        string outputDir = Path.Combine(root, "out");
        TaskEnvironment taskEnv = env.WithTransientTestState(new TransientTaskEnvironment(root)).Environment;
        taskEnv.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        taskEnv.SetEnvironmentVariable(TestCachedToolTask.PathVariable, cacheDir);

        var resources = new System.Resources.ResourceManager("TestResources", typeof(InvocationCache_Tests).Assembly);
        var description = new InvocationDescription(
            InvocationInputs.FromFiles(),
            ["custom-start-process"],
            InvocationOutputs.FromDirectory(outputDir));

        var first = new CustomStartProcessCachedToolTask(resources, "Prefix.")
        {
            BuildEngine = new MockEngine(),
            TaskEnvironment = taskEnv,
            Description = description,
            OutputDir = outputDir,
        };
        first.Execute().ShouldBeTrue();
        first.Executions.ShouldBe(1);
        first.ReplayedLines.ShouldContain("first");

        var second = new CustomStartProcessCachedToolTask(resources)
        {
            BuildEngine = new MockEngine(),
            TaskEnvironment = taskEnv,
            Description = description,
            OutputDir = outputDir,
        };
        second.Execute().ShouldBeTrue();
        second.Executions.ShouldBe(0);
        second.ReplayedLines.ShouldContain("first");
    }

    [Fact]
    public void PurgeFallsBackToDirectoryCreationTimeWhenLastUsedMissingOrInvalid()
    {
        using TestEnvironment env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        cache.Store(Key, InvocationOutputs.FromFiles());
        string entry = Path.Combine(cache.CacheDirectory, "v1", Key);
        File.WriteAllText(Path.Combine(entry, "last-used"), "not-a-timestamp");
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(1);
        using var output = new StringWriter();
        using var error = new StringWriter();

        InvocationCachePurge.Run(cache.CacheDirectory, "v1", "Test", cutoff, output, error).ShouldBe(0);

        Directory.Exists(entry).ShouldBeFalse();
        error.ToString().ShouldContain("using directory creation time");
        output.ToString().ShouldContain("Deleted: 1, Kept: 0, Errors: 0");
    }

    [Fact]
    public void PurgeReportsErrorWhenVersionDirectoryIsMissing()
    {
        using TestEnvironment env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        using var output = new StringWriter();
        using var error = new StringWriter();

        InvocationCachePurge.Run(Path.Combine(root, "missing-cache"), "v1", "Test", DateTimeOffset.UtcNow, output, error).ShouldBe(1);

        error.ToString().ShouldContain("Could not enumerate Test cache");
        output.ToString().ShouldContain("Deleted: 0, Kept: 0, Errors: 1");
    }

    private sealed class ThrowingOnHitMockEngine : IBuildEngine
    {
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => string.Empty;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs)
            => false;

        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public void LogErrorEvent(BuildErrorEventArgs e) { }
        public void LogWarningEvent(BuildWarningEventArgs e) { }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            if (e.Message is not null && e.Message.Contains("cache hit:"))
            {
                throw new IOException("simulated logger failure");
            }
        }
    }

    [Fact]
    public void PublicApisValidateArgumentsUpfrontAndDoNotSwallowNullArguments()
    {
        using var env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        TaskLoggingHelper log = new TestTask { BuildEngine = new MockEngine() }.Log;
        var cache = new InvocationCache(Path.Combine(root, "cache"), log, "Test");
        var validInputs = InvocationInputs.FromFiles([Path.Combine(root, "a.txt")]);
        var validOutputs = InvocationOutputs.FromDirectory(Path.Combine(root, "out"));
        var validDescription = new InvocationDescription(validInputs, ["args"], validOutputs);

        Should.Throw<ArgumentNullException>(() => new InvocationCache(Path.Combine(root, "cache"), null!, "Test"));
        Should.Throw<ArgumentNullException>(() => InvocationCache.TryCreateFromEnvironment(null!, TestCachedToolTask.Configuration));
        Should.Throw<ArgumentNullException>(() => InvocationCache.TryCreateFromEnvironment(log, null!));
        Should.Throw<ArgumentNullException>(() => cache.TryExecuteCached(null!, () => new InvocationResult(0), root, out _));
        Should.Throw<ArgumentNullException>(() => cache.TryExecuteCached(validDescription, null!, root, out _));
        Should.Throw<ArgumentNullException>(() => cache.TryExecuteCached(validDescription, () => new InvocationResult(0), null!, out _));
        Should.Throw<ArgumentNullException>(() => new InvocationCacheConfiguration("Test", null!, "PATH_VAR"));
        Should.Throw<ArgumentNullException>(() => new InvocationCacheConfiguration("Test", "ENABLED_VAR", null!));
        Should.Throw<ArgumentException>(() => new InvocationCacheConfiguration("Test", "   ", "PATH_VAR"));
        Should.Throw<ArgumentNullException>(() => new InvocationDescription(null!, ["args"], validOutputs));
        Should.Throw<ArgumentNullException>(() => new InvocationDescription(validInputs, null!, validOutputs));
        Should.Throw<ArgumentNullException>(() => new InvocationDescription(validInputs, ["args"], null!));
        Should.Throw<ArgumentNullException>(() => InvocationInputs.FromFiles(null!));
        Should.Throw<ArgumentNullException>(() => InvocationInputs.FromFiles([Path.Combine(root, "a.txt")], null!));
        Should.Throw<ArgumentNullException>(() => InvocationOutputs.FromDirectory(null!));
        Should.Throw<ArgumentException>(() => InvocationOutputs.FromDirectory("   "));
        Should.Throw<ArgumentNullException>(() => InvocationOutputs.FromDirectory("out", null!));
        Should.Throw<ArgumentNullException>(() => InvocationOutputs.FromFiles(null!));
        Should.Throw<ArgumentNullException>(() => InvocationOutputs.FromFiles([Path.Combine(root, "a.txt")], null!));
        Should.Throw<ArgumentException>(() => InvocationCachePurge.Run(null!, "v1", "Test", DateTimeOffset.UtcNow, new StringWriter(), new StringWriter()));
        Should.Throw<ArgumentException>(() => InvocationCachePurge.Run(root, null!, "Test", DateTimeOffset.UtcNow, new StringWriter(), new StringWriter()));
        Should.Throw<ArgumentException>(() => InvocationCachePurge.Run(root, "v1", null!, DateTimeOffset.UtcNow, new StringWriter(), new StringWriter()));
        Should.Throw<ArgumentNullException>(() => InvocationCachePurge.Run(root, "v1", "Test", DateTimeOffset.UtcNow, null!, new StringWriter()));
        Should.Throw<ArgumentNullException>(() => InvocationCachePurge.Run(root, "v1", "Test", DateTimeOffset.UtcNow, new StringWriter(), null!));
    }

    [Fact]
    public void CachedToolTaskBypassesCacheWithoutMsb6001WhenOutputOverlapsCacheDirectory()
    {
        using var env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        string cacheDir = Path.Combine(root, "cache");
        string input = Path.Combine(root, "in.txt");
        File.WriteAllText(input, "input");
        string overlappingOutputDir = Path.Combine(cacheDir, "out");

        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, cacheDir);

        var engine = new MockEngine();
        var task = new TestCachedToolTask
        {
            BuildEngine = engine,
            Description = new InvocationDescription(
                InvocationInputs.FromFiles([input]),
                ["args"],
                InvocationOutputs.FromDirectory(overlappingOutputDir)),
        };

        task.Execute().ShouldBeTrue();
        task.Executions.ShouldBe(1);
        engine.Errors.ShouldBe(0);
        engine.Log.ShouldNotContain("MSB6001");
        engine.Log.ShouldContain("Test cache bypassed:");
    }

    [Fact]
    public void CachedToolTaskDoesNotCacheCanceledExecutionAndResetsTerminatedStateOnReuse()
    {
        using var env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        string cacheDir = Path.Combine(root, "cache");
        string input = Path.Combine(root, "in.txt");
        string output = Path.Combine(root, "out.txt");
        File.WriteAllText(input, "input");

        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, cacheDir);

        var description = new InvocationDescription(
            InvocationInputs.FromFiles([input]),
            ["cancel-test"],
            InvocationOutputs.FromFiles([output]));

        var canceledTask = new TestCachedToolTask
        {
            BuildEngine = new MockEngine(),
            Description = description,
            Execution = () =>
            {
                File.WriteAllText(output, "partial");
                return 0;
            },
        };
        canceledTask.Cancel();

        canceledTask.Execute().ShouldBeTrue();
        canceledTask.Executions.ShouldBe(1);

        // Because the first run was canceled, it must not have been stored in the cache.
        var secondTask = new TestCachedToolTask
        {
            BuildEngine = new MockEngine(),
            Description = description,
            Execution = () =>
            {
                File.WriteAllText(output, "complete");
                return 0;
            },
        };
        secondTask.Execute().ShouldBeTrue();
        secondTask.Executions.ShouldBe(1);
        File.ReadAllText(output).ShouldBe("complete");

        // Now verify a reused task instance resets _terminatedTool before a subsequent cache hit.
        var reusedTask = new TestCachedToolTask
        {
            BuildEngine = new MockEngine(),
            Timeout = 1,
            Description = new InvocationDescription(
                InvocationInputs.FromFiles([input]),
                ["timeout-then-hit"],
                InvocationOutputs.FromFiles([Path.Combine(root, "timeout-out.txt")])),
            Execution = () =>
            {
                // Run a real short-lived sleep via base.ExecuteTool so ToolTask sets _terminatedTool = true.
                return -1;
            },
        };
        reusedTask.Execution = null;
        reusedTask.CommandOverride = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "/d /c \"ping -n 5 127.0.0.1 >nul\""
            : "-c \"sleep 5\"";
        reusedTask.Execute().ShouldBeFalse();

        // Reuse the same instance on the already-cached description: must succeed as a cache hit.
        reusedTask.Timeout = System.Threading.Timeout.Infinite;
        reusedTask.CommandOverride = null;
        reusedTask.Description = description;
        reusedTask.Execute().ShouldBeTrue();
        reusedTask.Executions.ShouldBe(1);
    }

    [Fact]
    public void CachedToolTaskDoesNotCacheWhenOutputCaptureEndsBeforeEof()
    {
        using var env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        string cacheDir = Path.Combine(root, "cache");
        string input = Path.Combine(root, "in.txt");
        string output = Path.Combine(root, "out.txt");
        File.WriteAllText(input, "input");

        env.SetEnvironmentVariable(TestCachedToolTask.EnabledVariable, "true");
        env.SetEnvironmentVariable(TestCachedToolTask.PathVariable, cacheDir);

        var description = new InvocationDescription(
            InvocationInputs.FromFiles([input]),
            ["eof-timeout"],
            InvocationOutputs.FromFiles([output]));

        var firstEngine = new MockEngine();
        var firstTask = new TestCachedToolTask
        {
            BuildEngine = firstEngine,
            Description = description,
            SimulateEofTimeout = true,
            Execution = () =>
            {
                File.WriteAllText(output, "v1");
                return 0;
            },
        };

        firstTask.Execute().ShouldBeTrue();
        firstTask.Executions.ShouldBe(1);
        firstEngine.Log.ShouldContain("output capture did not reach EOF");

        var secondTask = new TestCachedToolTask
        {
            BuildEngine = new MockEngine(),
            Description = description,
            Execution = () =>
            {
                File.WriteAllText(output, "v2");
                return 0;
            },
        };

        secondTask.Execute().ShouldBeTrue();
        secondTask.Executions.ShouldBe(1);
        File.ReadAllText(output).ShouldBe("v2");
    }

    [Fact]
    public void TryRestoreOverwritesAndDeletesReadOnlyOutputs()
    {
        using var env = TestEnvironment.Create();
        string root = env.CreateFolder().Path;
        var cache = new InvocationCache(Path.Combine(root, "cache"), new TestTask { BuildEngine = new MockEngine() }.Log, "Test");
        DateTime timestamp = DateTime.UtcNow;

        string dirKey = new string('a', 64);
        string fileKey = new string('b', 64);

        // Directory mode with read-only existing file.
        string outDir = Path.Combine(root, "dir-out");
        Directory.CreateDirectory(outDir);
        string dirFile = Path.Combine(outDir, "a.txt");
        File.WriteAllText(dirFile, "v1");
        InvocationOutputs dirOutputs = InvocationOutputs.FromDirectory(outDir);
        cache.Store(dirKey, dirOutputs);

        File.WriteAllText(dirFile, "stale");
        File.SetAttributes(dirFile, File.GetAttributes(dirFile) | FileAttributes.ReadOnly);
        try
        {
            cache.TryRestore(dirKey, dirOutputs, timestamp, out _).ShouldBeTrue();
            File.ReadAllText(dirFile).ShouldBe("v1");
        }
        finally
        {
            if (File.Exists(dirFile))
            {
                File.SetAttributes(dirFile, File.GetAttributes(dirFile) & ~FileAttributes.ReadOnly);
            }
        }

        // File-set mode with read-only existing present file and read-only existing absent file.
        string presentFile = Path.Combine(root, "present.txt");
        string absentFile = Path.Combine(root, "absent.txt");
        File.WriteAllText(presentFile, "cached");
        InvocationOutputs fileOutputs = InvocationOutputs.FromFiles([presentFile, absentFile]);
        cache.Store(fileKey, fileOutputs);

        File.WriteAllText(presentFile, "stale");
        File.WriteAllText(absentFile, "should-be-deleted");
        File.SetAttributes(presentFile, File.GetAttributes(presentFile) | FileAttributes.ReadOnly);
        File.SetAttributes(absentFile, File.GetAttributes(absentFile) | FileAttributes.ReadOnly);
        try
        {
            cache.TryRestore(fileKey, fileOutputs, timestamp, out _).ShouldBeTrue();
            File.ReadAllText(presentFile).ShouldBe("cached");
            File.Exists(absentFile).ShouldBeFalse();
        }
        finally
        {
            foreach (string path in new[] { presentFile, absentFile })
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                }
            }
        }
    }

    private sealed class CustomStartProcessCachedToolTask : CachedToolTask
    {
        internal InvocationDescription? Description { get; set; }
        internal string OutputDir { get; set; } = string.Empty;
        internal int Executions { get; private set; }
        internal List<string> ReplayedLines { get; } = [];

        public CustomStartProcessCachedToolTask(System.Resources.ResourceManager taskResources)
            : base(taskResources)
        {
        }

        public CustomStartProcessCachedToolTask(System.Resources.ResourceManager taskResources, string helpKeywordPrefix)
            : base(taskResources, helpKeywordPrefix)
        {
        }

        protected override string ToolName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sh";
        protected override InvocationCacheConfiguration CacheConfiguration => TestCachedToolTask.Configuration;

        protected override string GenerateFullPathToTool() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), ToolName)
            : "/bin/sh";

        protected override string GenerateCommandLineCommands() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "/d /c \"echo first\""
            : "-c \"echo first\"";

        protected override InvocationDescription? DescribeInvocation(string pathToTool, string responseFileCommands, string commandLineCommands)
            => Description;

        protected override int ExecuteToolCore(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            Executions++;
            Directory.CreateDirectory(OutputDir);
            return base.ExecuteToolCore(pathToTool, responseFileCommands, commandLineCommands);
        }

        // Deliberately starts the process directly without calling base.StartToolProcess.
        protected override Process StartToolProcess(Process proc)
        {
            proc.Start();
            return proc;
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            ReplayedLines.Add(singleLine);
            base.LogEventsFromTextOutput(singleLine, messageImportance);
        }
    }

    private sealed class TestTask : Microsoft.Build.Utilities.Task
    {
        public override bool Execute() => true;
    }

    private sealed class TransientTaskEnvironment : TransientTestState
    {
        private readonly string? _previousDirectory = Microsoft.Build.Framework.FileUtilities.CurrentThreadWorkingDirectory;

        internal TaskEnvironment Environment { get; }

        internal TransientTaskEnvironment(string projectDirectory) =>
            Environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory);

        public override void Revert()
        {
            Environment.Dispose();
            Microsoft.Build.Framework.FileUtilities.CurrentThreadWorkingDirectory = _previousDirectory;
        }
    }

    private sealed class TestCachedToolTask : CachedToolTask
    {
        private TaskLoggingHelper? _additionalLog;

        internal const string EnabledVariable = "MSBUILD_INVOCATION_CACHE_TEST_ENABLED";
        internal const string PathVariable = "MSBUILD_INVOCATION_CACHE_TEST_PATH";
        internal static readonly InvocationCacheConfiguration Configuration = new("Test", EnabledVariable, PathVariable);

        internal InvocationDescription? Description { get; set; }
        internal Func<int>? Execution { get; set; }
        internal int ToolExitCode { get; set; }
        internal int Descriptions { get; private set; }
        internal int Executions { get; private set; }
        internal List<string> ReplayedLines { get; } = [];
        internal bool LogAdditionalErrorOnReplay { get; set; }
        internal string? WorkingDirectoryOverride { get; set; }
        internal string? WorkingDirectoryUsed { get; private set; }
        internal string? CommandOverride { get; set; }
        internal bool SimulateEofTimeout { get; set; }
        internal bool? ExitCodeOverriddenInErrorHandler { get; private set; }

        internal override bool EofWaitTimedOut => SimulateEofTimeout || base.EofWaitTimedOut;

        internal int Run() => ExecuteTool("tool", "response", "command");

        protected override string ToolName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sh";
        protected override InvocationCacheConfiguration CacheConfiguration => Configuration;
        protected override bool HasLoggedErrors => base.HasLoggedErrors || (_additionalLog?.HasLoggedErrors ?? false);
        protected override string GetWorkingDirectory() => WorkingDirectoryOverride ?? string.Empty;

        protected override bool HandleTaskExecutionErrors()
        {
            ExitCodeOverriddenInErrorHandler = ExitCodeOverriddenToIndicateErrors;
            return base.HandleTaskExecutionErrors();
        }

        protected override string GenerateFullPathToTool() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), ToolName)
            : "/bin/sh";

        protected override string GenerateCommandLineCommands() => CommandOverride ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"/d /c \"echo first&echo warning CACHE0001: warning&echo.&echo last&1>&2 echo stderr&exit /b {ToolExitCode}\""
            : $"-c \"echo first; echo 'warning CACHE0001: warning'; echo; echo last; echo stderr >&2; exit {ToolExitCode}\"");

        protected override InvocationDescription? DescribeInvocation(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            Descriptions++;
            return Description;
        }

        protected override int ExecuteToolCore(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            Executions++;
            return Execution is not null ? Execution() : base.ExecuteToolCore(pathToTool, responseFileCommands, commandLineCommands);
        }

        protected override Process StartToolProcess(Process process)
        {
            WorkingDirectoryUsed = process.StartInfo.WorkingDirectory;
            return base.StartToolProcess(process);
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            ReplayedLines.Add(singleLine);
            if (LogAdditionalErrorOnReplay && singleLine == "first")
            {
                (_additionalLog ??= new TaskLoggingHelper(this)).LogError("Additional logger error.");
            }

            base.LogEventsFromTextOutput(singleLine, messageImportance);
        }
    }
}
