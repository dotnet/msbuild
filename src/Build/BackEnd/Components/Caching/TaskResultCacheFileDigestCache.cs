// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.BackEnd.Components.Caching
{
    internal readonly struct TaskResultCacheFileDigest
    {
        internal TaskResultCacheFileDigest(long length, byte[] hash)
        {
            Length = length;
            Hash = hash;
        }

        internal long Length { get; }

        internal byte[] Hash { get; }
    }

    internal sealed class TaskResultCacheFileDigestCache : IBuildComponent
    {
        private readonly ConcurrentDictionary<string, DigestEntry> _digests =
            new(NativeMethodsShared.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        public void InitializeComponent(IBuildComponentHost host)
        {
        }

        public void ShutdownComponent() => _digests.Clear();

        internal async ValueTask<TaskResultCacheFileDigest> GetDigestAsync(
            string path,
            CancellationToken cancellationToken)
        {
            string normalizedPath = FileUtilities.NormalizePath(path);
            if (!_digests.TryGetValue(normalizedPath, out DigestEntry? entry))
            {
                entry = _digests.GetOrAdd(normalizedPath, static path => new DigestEntry(path));
            }

            return await entry.GetDigestAsync(cancellationToken);
        }

        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            Assumed.Equal(
                type,
                BuildComponentType.TaskResultCacheFileDigestCache,
                $"Cannot create components of type {type}");
            return new TaskResultCacheFileDigestCache();
        }

        private sealed class DigestEntry(string path)
        {
            private readonly SemaphoreSlim _gate = new(1, 1);
            private DigestState? _state;

            internal async ValueTask<TaskResultCacheFileDigest> GetDigestAsync(
                CancellationToken cancellationToken)
            {
                FileFingerprint fingerprint = GetFingerprint(path);
                DigestState? state = Volatile.Read(ref _state);
                if (state is not null && state.Fingerprint.Equals(fingerprint))
                {
                    return state.Digest;
                }

                await _gate.WaitAsync(cancellationToken);
                try
                {
                    fingerprint = GetFingerprint(path);
                    state = Volatile.Read(ref _state);
                    if (state is not null && state.Fingerprint.Equals(fingerprint))
                    {
                        return state.Digest;
                    }

                    TaskResultCacheFileDigest digest =
                        await HashFileAsync(path, fingerprint.Length, cancellationToken);
                    Volatile.Write(ref _state, new DigestState(fingerprint, digest));
                    return digest;
                }
                finally
                {
                    _gate.Release();
                }
            }

            private static FileFingerprint GetFingerprint(string path)
            {
                var file = new FileInfo(path);
                return new FileFingerprint(file.Length, file.LastWriteTimeUtc.Ticks);
            }

            private static async Task<TaskResultCacheFileDigest> HashFileAsync(
                string path,
                long length,
                CancellationToken cancellationToken)
            {
                using SHA256 hash = SHA256.Create();
                using var hashStream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await stream.CopyToAsync(hashStream, 81920, cancellationToken);
                hashStream.FlushFinalBlock();
                return new TaskResultCacheFileDigest(length, hash.Hash!);
            }
        }

        private readonly struct FileFingerprint(long length, long lastWriteTimeUtcTicks)
        {
            internal long Length { get; } = length;

            private long LastWriteTimeUtcTicks { get; } = lastWriteTimeUtcTicks;

            internal bool Equals(FileFingerprint other) =>
                Length == other.Length &&
                LastWriteTimeUtcTicks == other.LastWriteTimeUtcTicks;
        }

        private sealed class DigestState(
            FileFingerprint fingerprint,
            TaskResultCacheFileDigest digest)
        {
            internal FileFingerprint Fingerprint { get; } = fingerprint;

            internal TaskResultCacheFileDigest Digest { get; } = digest;
        }
    }
}
