// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_BUILDXL_TASK_CACHE
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using BuildXL.Cache.ContentStore.Distributed.NuCache;
using BuildXL.Cache.ContentStore.Hashing;
using BuildXL.Cache.ContentStore.Interfaces.FileSystem;
using BuildXL.Cache.ContentStore.Interfaces.Logging;
using BuildXL.Cache.ContentStore.Interfaces.Results;
using BuildXL.Cache.ContentStore.Interfaces.Sessions;
using BuildXL.Cache.ContentStore.Interfaces.Stores;
using BuildXL.Cache.ContentStore.Interfaces.Time;
using BuildXL.Cache.ContentStore.Interfaces.Tracing;
using BuildXL.Cache.ContentStore.Logging;
using BuildXL.Cache.ContentStore.Stores;
using BuildXL.Cache.ContentStore.Tracing.Internal;
using BuildXL.Cache.Host.Configuration;
using BuildXL.Cache.MemoizationStore.Interfaces.Sessions;
using BuildXL.Cache.MemoizationStore.Interfaces.Stores;
using BuildXL.Cache.MemoizationStore.Sessions;
using BuildXL.Cache.MemoizationStore.Stores;

namespace Microsoft.Build.BackEnd;

/// <summary>
/// A build-scoped LocalCache session. The outer file lock is acquired before
/// BuildXL's directory/database locks and released only after session/cache shutdown.
/// Only the coordinator opens storage; workers use the existing node connection.
/// </summary>
internal sealed class BuildXLTaskCacheBackend : TaskCacheBackend
{
    internal const string DirectoryName = "buildxl";
    private readonly FileStream _lock;
    private readonly Context _context = new(NullLogger.Instance);
    private readonly LocalCache _cache;
    private readonly AbsolutePath _cacheRoot;
    private ICacheSession? _session;
    private bool _disposed;

    internal BuildXLTaskCacheBackend(string directory, uint quotaMegabytes = 10240, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCacheStore.CreateCacheDirectory(directory);
        string root = Path.Combine(directory, DirectoryName);
        TaskCacheStore.CreateCacheDirectory(root);
        string lockPath = Path.Combine(root, "owner.lock");
        TaskCacheStore.CheckPath(lockPath, allowDirectory: false);
        try
        {
            _lock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException e)
        {
            throw new IOException(FormatOwnershipFailure(directory), e);
        }

        try
        {
            _cacheRoot = new AbsolutePath(Path.Combine(root, "cache"));
            TaskCacheStore.CreateCacheDirectory(_cacheRoot.Path);
            TaskCacheStore.CheckPath((_cacheRoot / "memoization").Path, allowDirectory: true);
            TaskCacheStore.CheckPath((_cacheRoot / "Shared").Path, allowDirectory: true);
            CapturedMemoizationConfiguration configuration = new()
            {
                Database = new RocksDbContentLocationDatabaseConfiguration(_cacheRoot / "memoization")
                {
                    CleanOnInitialize = false,
                    // Run metadata GC once on opening, with a persistent hourly cadence.
                    GarbageCollectionInterval = Timeout.InfiniteTimeSpan,
                    MetadataGarbageCollectionMaximumSizeMb = 64,
                    // Bound native background pools independently of build parallelism.
                    RocksDbPerformanceSettings = new RocksDbPerformanceSettings
                    {
                        CompactionDegreeOfParallelism = new() { ThreadCount = 1 },
                        MaxBackgroundCompactions = 1,
                        MaxBackgroundFlushes = 1,
                    },
                },
            };
            _cache = LocalCache.CreateUnknownContentStoreInProcMemoizationStoreCache(
                NullLogger.Instance,
                _cacheRoot,
                configuration,
                LocalCacheConfiguration.CreateServerDisabled(),
                new ConfigurationModel(ContentStoreConfiguration.CreateWithMaxSizeQuotaMB(quotaMegabytes)),
                assumeCallerCreatesDirectoryForPlace: true);
            Check(_cache.StartupAsync(_context).GetAwaiter().GetResult(), cancellationToken);
            byte[] gcTime = new byte[sizeof(long)];
            long lastCollection = _lock.Read(gcTime, 0, gcTime.Length) == gcTime.Length ? BitConverter.ToInt64(gcTime, 0) : 0;
            DateTime now = DateTime.UtcNow;
            if (lastCollection < now.AddHours(-1).Ticks || lastCollection > now.Ticks)
            {
                Check(configuration.Store!.RocksDbDatabase.GarbageCollectAsync(new OperationContext(_context, cancellationToken)).GetAwaiter().GetResult(), cancellationToken);
                gcTime = BitConverter.GetBytes(now.Ticks);
                _lock.Position = 0;
                _lock.Write(gcTime, 0, gcTime.Length);
                _lock.Flush(flushToDisk: true);
            }

            var session = _cache.CreateSession(_context, "MSBuildTaskCache", ImplicitPin.PutAndGet);
            _session = session.Session;
            Check(session, cancellationToken);
            if (_session is null)
            {
                throw new IOException("BuildXL did not create a task cache session.");
            }
            Check(_session.StartupAsync(_context).GetAwaiter().GetResult(), cancellationToken);
        }
        catch
        {
            try
            {
                Dispose();
            }
            catch (Exception e) when (!IsCriticalException(e))
            {
                // Preserve the initialization error while still releasing ownership.
            }
            throw;
        }
    }

    internal override byte[]? ReadManifest(string key, int maximumSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _session!.GetContentHashListAsync(_context, FingerprintFor(key), cancellationToken).GetAwaiter().GetResult();
        Check(result, cancellationToken);
        ContentHashList? list = result.ContentHashListWithDeterminism.ContentHashList;
        if (list is null)
        {
            return null;
        }

        if (list.Hashes.Count == 0)
        {
            throw new InvalidDataException("Invalid task cache content hash list.");
        }

        foreach (ContentHash contentHash in list.Hashes)
        {
            var pin = _session.PinAsync(_context, contentHash, cancellationToken).GetAwaiter().GetResult();
            if (pin.Code == PinResult.ResultCode.ContentNotFound)
            {
                return null;
            }

            Check(pin, cancellationToken);
        }

        ContentHash digest = list.Hashes[0];
        using Stream stream = Open(digest.ToHex(), cancellationToken);
        if (stream.Length < 32 || stream.Length > maximumSize)
        {
            throw new InvalidDataException("Invalid task cache manifest size.");
        }

        using BinaryReader reader = new(stream);
        byte[] manifest = reader.ReadBytes(checked((int)stream.Length));
        using SHA256 hash = SHA256.Create();
        if (new ContentHash(HashType.SHA256, hash.ComputeHash(manifest)) != digest)
        {
            throw new InvalidDataException("Task cache manifest content checksum mismatch.");
        }

        return manifest;
    }

    internal override Stream Open(string digest, CancellationToken cancellationToken = default)
    {
        TaskCacheStore.CheckPath(ContentPath(digest), allowDirectory: false, allowReadOnlyFile: true);
        cancellationToken.ThrowIfCancellationRequested();
        var result = _session!.OpenStreamAsync(_context, new ContentHash("SHA256:" + digest), cancellationToken).GetAwaiter().GetResult();
        Check(result, cancellationToken);
        return result.Stream ?? throw new InvalidDataException("Task cache content is missing.");
    }

    internal override string ContentPath(string digest) =>
        (_cacheRoot / FileSystemContentStoreInternal.GetPrimaryRelativePath(new ContentHash("SHA256:" + digest))).Path;

    internal override string PutFile(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCacheStore.CheckPath(path, allowDirectory: false);
        var result = _session!.PutFileAsync(_context, HashType.SHA256, new AbsolutePath(path),
            FileRealizationMode.Copy, cancellationToken).GetAwaiter().GetResult();
        Check(result, cancellationToken);
        return result.ContentHash.ToHex();
    }

    internal override bool Publish(string key, byte[] manifest, Func<bool>? canPublish = null,
        IReadOnlyList<string>? artifacts = null, CancellationToken cancellationToken = default)
    {
        // The first CAS object is the MSBuild manifest. Register every artifact with
        // memoization, not just the manifest, so BuildXL can detect evicted content.
        using MemoryStream stream = new(manifest, writable: false);
        cancellationToken.ThrowIfCancellationRequested();
        var put = _session!.PutStreamAsync(_context, HashType.SHA256, stream, cancellationToken).GetAwaiter().GetResult();
        Check(put, cancellationToken);

        ContentHash[] hashes = new ContentHash[(artifacts?.Count ?? 0) + 1];
        hashes[0] = put.ContentHash;
        for (int i = 1; i < hashes.Length; i++)
        {
            hashes[i] = new ContentHash("SHA256:" + TaskCacheStore.ValidateDigest(artifacts![i - 1]));
        }
        if (canPublish?.Invoke() == false)
        {
            return false;
        }

        var result = _session.AddOrGetContentHashListAsync(_context, FingerprintFor(key),
            new ContentHashListWithDeterminism(new ContentHashList(hashes, null), CacheDeterminism.None),
            cancellationToken).GetAwaiter().GetResult();
        Check(result, cancellationToken);
        return true;
    }

    private static StrongFingerprint FingerprintFor(string key) =>
        new(new Fingerprint(key), new Selector(SHA256HashInfo.Instance.EmptyHash));

    private static void Check(ResultBase result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            throw new IOException("BuildXL task cache operation failed: " + result);
        }
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            try
            {
                if (_session is not null)
                {
                    try
                    {
                        Check(_session.ShutdownAsync(_context).GetAwaiter().GetResult());
                    }
                    finally
                    {
                        _session.Dispose();
                        _session = null;
                    }
                }
            }
            finally
            {
                if (_cache is not null)
                {
                    try
                    {
                        Check(_cache.ShutdownAsync(_context).GetAwaiter().GetResult());
                    }
                    finally
                    {
                        _cache.Dispose();
                    }
                }
            }
        }
        finally
        {
            _lock.Dispose();
        }
    }

    private sealed class CapturedMemoizationConfiguration : RocksDbMemoizationStoreConfiguration
    {
        internal RocksDbMemoizationStore? Store { get; private set; }

        public override IMemoizationStore CreateStore(ILogger logger, IClock clock) =>
            Store = (RocksDbMemoizationStore)base.CreateStore(logger, clock);
    }
}
#endif
