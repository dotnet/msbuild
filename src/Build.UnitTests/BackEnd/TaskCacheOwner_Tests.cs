// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskCacheOwner_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private static readonly string s_digest = new('A', 64);
    public static bool IsSupported => TaskCacheStore.IsSupported;

    [ConditionalFact(typeof(TaskCacheOwner_Tests), nameof(IsSupported))]
    public void TopLevelBuildOwnershipFailsFastAndSequentialBuildsReacquire()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = env.CreateFolder().Path;
        using BuildManager first = new();
        using BuildManager second = new();
        BuildParameters Parameters() => new()
        {
            TaskCache = true, BuildCacheDirectory = directory, Loggers = [new MockLogger(_output)],
        };
        first.BeginBuild(Parameters());
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Should.Throw<IOException>(() => second.BeginBuild(Parameters())).Message.ShouldContain(directory);
            watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        }
        finally
        {
            first.EndBuild();
        }
        second.BeginBuild(Parameters());
        second.EndBuild();
        first.BeginBuild(Parameters());
        first.EndBuild();
    }

    [ConditionalFact(typeof(TaskCacheOwner_Tests), nameof(IsSupported))]
    public void CancellationAndFailedInitializationReleaseOwnership()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = env.CreateFolder().Path;
        using CancellationTokenSource cancellation = new();
        using (TaskCacheOwner owner = new(directory, (_, _) => { }, cancellation.Token))
        {
            cancellation.Cancel();
            Should.Throw<OperationCanceledException>(() => owner.ReadManifest(s_digest, 1024));
        }
        using (TaskCacheOwner reopened = new(directory, (_, _) => { }, default))
        {
            reopened.ReadManifest(s_digest, 1024).ShouldBeNull();
        }

        string invalid = env.CreateFile("not-a-directory", "untouched").Path;
        Should.Throw<IOException>(() => new TaskCacheOwner(invalid, (_, _) => { }, default));
        File.Delete(invalid);
        using TaskCacheOwner recovered = new(invalid, (_, _) => { }, default);
        recovered.ReadManifest(s_digest, 1024).ShouldBeNull();
    }

    [Fact]
    public void DirectoryConfigurationIgnoresAllEnvironmentOverrides()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string expected = BuildParameters.ResolveBuildCacheDirectory();
        string other = env.CreateFolder().Path;
        env.SetEnvironmentVariable("MSBUILD_TASK_CACHE_DIR", other);
        env.SetEnvironmentVariable("MSBuildTaskCacheDirectory", other);
        env.SetEnvironmentVariable("XDG_CACHE_HOME", other);
        BuildParameters.ResolveBuildCacheDirectory().ShouldBe(expected);
        BuildParameters.ResolveBuildCacheDirectory(" \t").ShouldBe(expected);
        BuildParameters.ResolveBuildCacheDirectory(other).ShouldBe(other);
        BuildParameters.ResolveBuildCacheDirectory("relative-cache").ShouldBe(Path.GetFullPath("relative-cache"));
        new BuildParameters
        {
            GlobalProperties = new Dictionary<string, string>
            {
                ["MSBuildTaskCacheDirectory"] = other,
                ["BuildCacheDirectory"] = other,
            },
        }.BuildCacheDirectory.ShouldBe(expected);
    }

    [Fact]
    public void BuildCacheDirectoryReplacesTheExperimentalApi()
    {
        typeof(BuildParameters).GetProperty(nameof(BuildParameters.BuildCacheDirectory)).ShouldNotBeNull();
        typeof(BuildParameters).GetProperty("TaskCacheDirectory").ShouldBeNull();
        BuildParameters parameters = new() { BuildCacheDirectory = "relative-cache" };
        parameters.TaskCache.ShouldBeFalse();
        parameters.Clone().BuildCacheDirectory.ShouldBe(Path.GetFullPath("relative-cache"));
    }

    [Fact]
    public async Task RpcCancellationAcknowledgesDrainedOperation()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using CancellationTokenSource cancellation = new();
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim completed = new();
        using FakeBackend backend = new(token =>
        {
            entered.Set();
            try
            {
                token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                token.ThrowIfCancellationRequested();
                return s_digest;
            }
            finally
            {
                completed.Set();
            }
        });
        TaskCacheClient? client = null;
        string path = env.CreateFile().Path;
        using TaskCacheOwner owner = new(env.CreateFolder().Path, (_, packet) => client!.PacketReceived(0, packet), default, backend);
        using (client = new TaskCacheClient(env.CreateFolder().Path, packet => owner.PacketReceived(7, packet)))
        {
            Task<string> operation = Task.Run(() => client.PutFile(path, cancellation.Token));
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(async () => await operation);
            completed.IsSet.ShouldBeTrue();
        }
        owner.Dispose();
        backend.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task OwnerAllowsConcurrentStorageOperationsAndDrainsShutdown()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using CountdownEvent entered = new(2);
        using FakeBackend backend = new(token =>
        {
            entered.Signal();
            entered.Wait(TimeSpan.FromSeconds(10), token).ShouldBeTrue();
            token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            token.ThrowIfCancellationRequested();
            return s_digest;
        });
        using TaskCacheOwner owner = new(env.CreateFolder().Path, (_, _) => { }, default, backend);
        string path = env.CreateFile().Path;
        Task<string> first = Task.Run(() => owner.PutFile(path));
        Task<string> second = Task.Run(() => owner.PutFile(path));
        entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        owner.Dispose();
        await Should.ThrowAsync<OperationCanceledException>(async () => await first);
        await Should.ThrowAsync<OperationCanceledException>(async () => await second);
        backend.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task CancellationCallbackFailureStillDrainsAndClosesStorage()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using ManualResetEventSlim entered = new();
        using FakeBackend backend = new(token =>
        {
            using CancellationTokenRegistration registration = token.Register(() => throw new InvalidOperationException("cancellation callback failed"));
            entered.Set();
            token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            token.ThrowIfCancellationRequested();
            return s_digest;
        });
        using TaskCacheOwner owner = new(env.CreateFolder().Path, (_, _) => { }, default, backend);
        string path = env.CreateFile().Path;
        Task<string> operation = Task.Run(() => owner.PutFile(path));
        entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        Should.Throw<AggregateException>(owner.Dispose);
        await Should.ThrowAsync<OperationCanceledException>(async () => await operation);
        backend.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task ConnectionLossUnblocksPendingRequest()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using ManualResetEventSlim sent = new();
        using TaskCacheClient client = new(env.CreateFolder().Path, _ => sent.Set());
        Task<byte[]?> request = Task.Run(() => client.ReadManifest(s_digest, 1024));
        sent.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        client.Dispose();
        await Should.ThrowAsync<IOException>(async () => await request);
    }

    [Fact]
    public async Task FailedResponseSendReportsFailureAndDrainsOwner()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using ManualResetEventSlim reported = new();
        using FakeBackend backend = new(_ => s_digest);
        TaskCacheClient? client = null;
        using TaskCacheOwner owner = new(env.CreateFolder().Path, (_, _) => throw new IOException("send failed"),
            default, backend, _ =>
            {
                client!.Dispose();
                reported.Set();
            });
        using (client = new TaskCacheClient(env.CreateFolder().Path, packet => owner.PacketReceived(3, packet)))
        {
            string path = env.CreateFile().Path;
            await Should.ThrowAsync<IOException>(() => Task.Run(() => client.PutFile(path)));
            reported.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }
        owner.Dispose();
        backend.Disposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RpcRejectsMismatchedResponse(bool mismatchedOperation)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TaskCacheClient? client = null;
        using (client = new TaskCacheClient(env.CreateFolder().Path, packet =>
        {
            TaskCachePacket request = (TaskCachePacket)packet;
            client!.PacketReceived(0, new TaskCachePacket(NodePacketType.TaskCacheResponse)
            {
                Id = request.Id,
                Operation = mismatchedOperation ? (int)TaskCacheOperation.Publish : request.Operation,
                Key = mismatchedOperation ? request.Key : new string('B', 64),
            });
        }))
        {
            Should.Throw<InvalidDataException>(() => client.ReadManifest(s_digest, 1024));
        }
    }

    [Theory]
    [InlineData("relative.txt")]
    [InlineData("")]
    public void OwnerRejectsNonAbsoluteArtifactPaths(string path)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using FakeBackend backend = new(_ => s_digest);
        using TaskCacheOwner owner = new(env.CreateFolder().Path, (_, _) => { }, default, backend);
        Should.Throw<InvalidDataException>(() => owner.PutFile(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyContentClientOpenDoesNotDependOnExportedFile(bool pathExists)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = env.CreateFolder().Path;
        string path = Path.Combine(directory, "empty-content");
        if (pathExists)
        {
            File.WriteAllBytes(path, []);
        }
        int requests = 0;
        TaskCacheClient? client = null;
        using (client = new TaskCacheClient(directory, packet =>
        {
            requests++;
            TaskCachePacket request = (TaskCachePacket)packet;
            request.Operation.ShouldBe((int)TaskCacheOperation.Open);
            client!.PacketReceived(0, new TaskCachePacket(NodePacketType.TaskCacheResponse)
            {
                Id = request.Id, Operation = request.Operation, Key = request.Key, Path = path,
            });
        }))
        {
            using Stream stream = client.Open(EmptyDigest());
            stream.Length.ShouldBe(0);
            stream.ReadByte().ShouldBe(-1);
            stream.CanWrite.ShouldBeFalse();
            requests.ShouldBe(1);
            File.Exists(path).ShouldBe(pathExists);
        }
    }

    [Theory]
    [InlineData("error")]
    [InlineData("mismatch")]
    [InlineData("cancelled")]
    public void EmptyContentRequiresSuccessfulMatchingOwnerResponse(string outcome)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TaskCacheClient? client = null;
        using (client = new TaskCacheClient(env.CreateFolder().Path, packet =>
        {
            TaskCachePacket request = (TaskCachePacket)packet;
            client!.PacketReceived(0, new TaskCachePacket(NodePacketType.TaskCacheResponse)
            {
                Id = request.Id,
                Operation = request.Operation,
                Key = outcome == "mismatch" ? s_digest : request.Key,
                Error = outcome == "error" ? "owner failed" : null,
                Cancelled = outcome == "cancelled",
            });
        }))
        {
            if (outcome == "cancelled")
            {
                Should.Throw<OperationCanceledException>(() => client.Open(EmptyDigest()));
            }
            else if (outcome == "mismatch")
            {
                Should.Throw<InvalidDataException>(() => client.Open(EmptyDigest()));
            }
            else
            {
                Should.Throw<IOException>(() => client.Open(EmptyDigest())).Message.ShouldBe("owner failed");
            }
        }
    }

    [Fact]
    public void EmptyContentHonorsCancellationBeforeSending()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        int requests = 0;
        using TaskCacheClient client = new(env.CreateFolder().Path, _ => requests++);
        Should.Throw<OperationCanceledException>(() => client.Open(EmptyDigest(), cancellation.Token));
        requests.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("relative")]
    [InlineData("outside")]
    public void NonemptyContentStillRequiresAContainedPath(string? kind)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = env.CreateFolder().Path;
        string? path = kind == "outside" ? env.CreateFile().Path : kind;
        TaskCacheClient? client = null;
        using (client = new TaskCacheClient(directory, packet =>
        {
            TaskCachePacket request = (TaskCachePacket)packet;
            client!.PacketReceived(0, new TaskCachePacket(NodePacketType.TaskCacheResponse)
            {
                Id = request.Id, Operation = request.Operation, Key = request.Key, Path = path,
            });
        }))
        {
            Should.Throw<InvalidDataException>(() => client.Open(s_digest));
        }
    }

    private static string EmptyDigest()
    {
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash([])).Replace("-", String.Empty);
    }

    private sealed class FakeBackend(Func<CancellationToken, string> putFile) : TaskCacheBackend
    {
        internal bool Disposed { get; private set; }
        internal override byte[]? ReadManifest(string key, int maximumSize, CancellationToken cancellationToken = default) => null;
        internal override Stream Open(string digest, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        internal override string ContentPath(string digest) => throw new NotSupportedException();
        internal override string PutFile(string path, CancellationToken cancellationToken = default) => putFile(cancellationToken);
        internal override bool Publish(string key, byte[] manifest, Func<bool>? canPublish = null,
            IReadOnlyList<string>? artifacts = null, CancellationToken cancellationToken = default) => true;
        public override void Dispose() => Disposed = true;
    }
}
