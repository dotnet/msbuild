// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using ExceptionDispatchInfo = System.Runtime.ExceptionServices.ExceptionDispatchInfo;

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Owns storage until the build and all storage calls have drained. The gate only
/// protects call lifetimes; no I/O, task execution, or packet sending runs under it.
/// </summary>
internal sealed class TaskCacheOwner : TaskCacheBackend, INodePacketHandler
{
    private readonly TaskCacheBackend _backend;
    private readonly string _directory;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown;
    private readonly Dictionary<(int Node, int Id), CancellationTokenSource> _requests = [];
    private readonly Action<int, INodePacket> _send;
    private readonly Action<Exception>? _reportFailure;
    private int _active;
    private bool _stopping;

    internal TaskCacheOwner(string directory, Action<int, INodePacket> send, CancellationToken cancellationToken,
        TaskCacheBackend? backend = null, Action<Exception>? reportFailure = null)
    {
        _directory = directory;
        _send = send;
        _reportFailure = reportFailure;
        _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _backend = backend ?? Create(directory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            try
            {
                _backend?.Dispose();
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // Preserve the initialization failure.
            }
            _shutdown.Dispose();
            throw;
        }
    }

    private CancellationTokenSource Enter(CancellationToken token)
    {
        lock (_gate)
        {
            if (_stopping)
            {
                throw new OperationCanceledException("The task cache owner is shutting down.");
            }

            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _shutdown.Token);
            _active++;
            return linked;
        }
    }

    private void Leave()
    {
        lock (_gate)
        {
            _active--;
            Monitor.PulseAll(_gate);
        }
    }

    private T Run<T>(CancellationToken token, Func<CancellationToken, T> action)
    {
        using CancellationTokenSource linked = Enter(token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            return action(linked.Token);
        }
        finally
        {
            Leave();
        }
    }

    internal override byte[]? ReadManifest(string key, int maximumSize, CancellationToken cancellationToken = default) =>
        Run(cancellationToken, token => _backend.ReadManifest(TaskCacheStore.ValidateDigest(key), maximumSize, token));

    internal override Stream Open(string digest, CancellationToken cancellationToken = default) =>
        Run(cancellationToken, token => _backend.Open(TaskCacheStore.ValidateDigest(digest), token));

    internal override string ContentPath(string digest) => _backend.ContentPath(TaskCacheStore.ValidateDigest(digest));

    internal override string PutFile(string path, CancellationToken cancellationToken = default) =>
        Run(cancellationToken, token =>
        {
            ValidateInputPath(path);
            return _backend.PutFile(path, token);
        });

    internal override bool Publish(string key, byte[] manifest, Func<bool>? canPublish = null,
        IReadOnlyList<string>? artifacts = null, CancellationToken cancellationToken = default) =>
        Run(cancellationToken, token => _backend.Publish(TaskCacheStore.ValidateDigest(key), manifest, canPublish, artifacts, token));

    private void ValidateInputPath(string path)
    {
        if (!Path.IsPathRooted(path) || !FileUtilities.PathComparer.Equals(Path.GetFullPath(path), path)
            || FileUtilities.PathComparer.Equals(path, _directory)
            || path.StartsWith(_directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                FileUtilities.IsFileSystemCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Invalid task cache artifact path.");
        }

        TaskCacheStore.CheckPath(path, allowDirectory: false);
    }

    public void PacketReceived(int node, INodePacket packet)
    {
        TaskCachePacket request = (TaskCachePacket)packet;
        if (packet.Type == NodePacketType.TaskCacheCancel)
        {
            CancellationTokenSource? cancellation;
            lock (_gate)
            {
                _requests.TryGetValue((node, request.Id), out cancellation);
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Completion raced cancellation; its response has already been sent.
            }
            return;
        }

        CancellationTokenSource operation = Enter(default);
        lock (_gate)
        {
            if (_requests.ContainsKey((node, request.Id)))
            {
                Leave();
                operation.Dispose();
                throw new InvalidDataException("Duplicate task cache request.");
            }
            _requests.Add((node, request.Id), operation);
        }

        // Do not run storage on the packet pump or the scheduler queue. Either may
        // be needed to deliver the result that allows the requesting task to finish.
        _ = Task.Run(() =>
        {
            TaskCachePacket response = new(NodePacketType.TaskCacheResponse)
            {
                Id = request.Id,
                Operation = request.Operation,
                Key = request.Key,
            };
            try
            {
                operation.Token.ThrowIfCancellationRequested();
                switch ((TaskCacheOperation)request.Operation)
                {
                    case TaskCacheOperation.ReadManifest:
                        if (request.MaximumSize is < 32 or > 64 * 1024 * 1024)
                        {
                            throw new InvalidDataException("Invalid task cache manifest limit.");
                        }
                        response.Data = ReadManifest(request.Key, request.MaximumSize, operation.Token);
                        break;
                    case TaskCacheOperation.Open:
                        using (Open(request.Key, operation.Token))
                        {
                            response.Path = ContentPath(request.Key);
                        }
                        break;
                    case TaskCacheOperation.PutFile:
                        response.Path = PutFile(request.Path ?? throw new InvalidDataException("Missing artifact path."), operation.Token);
                        break;
                    case TaskCacheOperation.Publish:
                        if (request.Data is null || request.Data.Length is < 32 or > 64 * 1024 * 1024)
                        {
                            throw new InvalidDataException("Invalid task cache manifest size.");
                        }
                        response.Published = Publish(request.Key, request.Data, artifacts: request.Artifacts, cancellationToken: operation.Token);
                        break;
                    default:
                        throw new InvalidDataException("Unknown task cache operation.");
                }
            }
            catch (OperationCanceledException)
            {
                response.Cancelled = true;
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                response.Error = e.Message;
            }
            finally
            {
                try
                {
                    _send(node, response);
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    // Sending can fail before the transport reports connection loss.
                    // Fail the build so shutdown also releases the worker's waiter.
                    _reportFailure?.Invoke(e);
                }
                finally
                {
                    lock (_gate)
                    {
                        _requests.Remove((node, request.Id));
                        operation.Dispose();
                    }
                    Leave();
                }
            }
        });
    }

    public override void Dispose()
    {
        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }
            _stopping = true;
        }

        Exception? cancellationFailure = null;
        try
        {
            _shutdown.Cancel();
        }
        catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
        {
            cancellationFailure = e;
        }
        lock (_gate)
        {
            while (_active != 0)
            {
                Monitor.Wait(_gate);
            }
        }

        try
        {
            _backend.Dispose();
        }
        catch (Exception e) when (cancellationFailure is not null && !ExceptionHandling.IsCriticalException(e))
        {
            // Preserve the earlier cancellation failure, after releasing storage.
        }
        finally
        {
            _shutdown.Dispose();
        }

        if (cancellationFailure is not null)
        {
            ExceptionDispatchInfo.Capture(cancellationFailure).Throw();
        }
    }
}
