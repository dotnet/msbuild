// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd;

/// <summary>A worker never opens LocalCache; storage calls use its existing coordinator connection.</summary>
internal sealed class TaskCacheClient(string directory, Action<INodePacket> send) : TaskCacheBackend, INodePacketHandler
{
    private const string EmptySha256Digest = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
    private readonly ConcurrentDictionary<int, TaskCompletionSource<TaskCachePacket>> _pending = new();
    private int _nextId;
    private int _stopped;

    private TaskCachePacket Call(TaskCachePacket request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        request.Id = Interlocked.Increment(ref _nextId);
        TaskCompletionSource<TaskCachePacket> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[request.Id] = completion;
        try
        {
            if (Volatile.Read(ref _stopped) != 0)
            {
                throw new IOException("The task cache coordinator connection was closed.");
            }

            send(request);
            using CancellationTokenRegistration registration = token.Register(() =>
            {
                try
                {
                    send(new TaskCachePacket(NodePacketType.TaskCacheCancel) { Id = request.Id });
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    Dispose();
                }
            });
            // Cancellation waits for the owner's completion/cleanup acknowledgement.
            // Connection loss completes all waiters independently of this token.
            TaskCachePacket response = completion.Task.GetAwaiter().GetResult();
            if (response.Id != request.Id || response.Operation != request.Operation || response.Key != request.Key)
            {
                throw new InvalidDataException("Mismatched task cache response.");
            }
            if (response.Cancelled)
            {
                throw new OperationCanceledException(token);
            }
            if (response.Error is not null)
            {
                throw new IOException(response.Error);
            }
            token.ThrowIfCancellationRequested();
            return response;
        }
        finally
        {
            _pending.TryRemove(request.Id, out _);
        }
    }

    public void PacketReceived(int node, INodePacket packet)
    {
        TaskCachePacket response = (TaskCachePacket)packet;
        if (_pending.TryGetValue(response.Id, out TaskCompletionSource<TaskCachePacket>? completion))
        {
            completion.TrySetResult(response);
        }
        else
        {
            Dispose();
        }
    }

    internal override byte[]? ReadManifest(string key, int maximumSize, CancellationToken cancellationToken = default) =>
        Call(new(NodePacketType.TaskCacheRequest)
        {
            Operation = (int)TaskCacheOperation.ReadManifest, Key = key, MaximumSize = maximumSize,
        }, cancellationToken).Data;

    internal override Stream Open(string digest, CancellationToken cancellationToken = default)
    {
        TaskCachePacket response = Call(new(NodePacketType.TaskCacheRequest)
        {
            Operation = (int)TaskCacheOperation.Open, Key = digest,
        }, cancellationToken);
        if (digest == EmptySha256Digest)
        {
            // BuildXL can synthesize this content without a physical CAS file.
            // Still require the owner's successful, correlated response above.
            return new MemoryStream([], writable: false);
        }
        string path = response.Path ?? throw new InvalidDataException("Missing task cache content path.");
        if (!Path.IsPathRooted(path)
            || !FileUtilities.PathComparer.Equals(Path.GetFullPath(path), path)
            || !path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                FileUtilities.IsFileSystemCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Invalid task cache content path.");
        }
        TaskCacheStore.CheckPath(path, allowDirectory: false, allowReadOnlyFile: true);
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
    }

    internal override string ContentPath(string digest) => throw new NotSupportedException();

    internal override string PutFile(string path, CancellationToken cancellationToken = default) =>
        TaskCacheStore.ValidateDigest(Call(new(NodePacketType.TaskCacheRequest)
        {
            Operation = (int)TaskCacheOperation.PutFile, Path = path,
        }, cancellationToken).Path ?? String.Empty);

    internal override bool Publish(string key, byte[] manifest, Func<bool>? canPublish = null,
        IReadOnlyList<string>? artifacts = null, CancellationToken cancellationToken = default)
    {
        if (canPublish?.Invoke() == false)
        {
            return false;
        }
        string[] hashes = new string[artifacts?.Count ?? 0];
        for (int i = 0; i < hashes.Length; i++)
        {
            hashes[i] = artifacts![i];
        }
        return Call(new(NodePacketType.TaskCacheRequest)
        {
            Operation = (int)TaskCacheOperation.Publish, Key = key, Data = manifest, Artifacts = hashes,
        }, cancellationToken).Published;
    }

    public override void Dispose()
    {
        Interlocked.Exchange(ref _stopped, 1);
        foreach (TaskCompletionSource<TaskCachePacket> completion in _pending.Values)
        {
            completion.TrySetException(new IOException("The task cache coordinator connection was closed."));
        }
    }
}
