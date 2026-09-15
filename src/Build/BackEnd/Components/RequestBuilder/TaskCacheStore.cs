// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Backend-independent task-contract validation, manifests, and safe restoration.
/// Restoration paths come from the current task contract, never from cache data alone.
/// </summary>
internal sealed class TaskCacheStore
{
    private const int MaxManifestSize = 64 * 1024 * 1024;
    private readonly string _directory;
    private readonly TaskCacheBackend _backend;

    internal TaskCacheStore(string directory, TaskCacheBackend backend)
    {
        EnsureSupported();
        _directory = Path.GetFullPath(directory);
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        CheckPath(_directory, allowDirectory: true);
    }

    internal bool TryRestore(string key, IReadOnlyList<string> outputPaths, out byte[] state, Action<byte[]>? validateState = null,
        CancellationToken cancellationToken = default, Action? onReplacementStarted = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePaths(outputPaths);
        TaskCacheBackend backend = _backend;
        byte[]? manifest = backend.ReadManifest(ValidateDigest(key), MaxManifestSize, cancellationToken);
        if (manifest is null)
        {
            state = [];
            return false;
        }

        using SHA256 hash = SHA256.Create();
        byte[] checksum = hash.ComputeHash(manifest, 0, manifest.Length - 32);
        for (int i = 0; i < checksum.Length; i++)
        {
            if (checksum[i] != manifest[manifest.Length - 32 + i])
            {
                throw new InvalidDataException("Task cache manifest checksum mismatch.");
            }
        }

        List<Artifact> artifacts = [];
        using (MemoryStream stream = new(manifest, 0, manifest.Length - 32, writable: false))
        using (BinaryReader reader = new(stream, Encoding.UTF8))
        {
            if (reader.ReadInt32() != outputPaths.Count)
            {
                throw new InvalidDataException("Task cache manifest does not match the task contract.");
            }

            for (int i = 0; i < outputPaths.Count; i++)
            {
                if (!String.Equals(ReadString(reader), outputPaths[i], StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Task cache output path does not match the task contract.");
                }

                bool exists = reader.ReadBoolean();
                string digest = exists ? ReadString(reader) : String.Empty;
                long ticks = reader.ReadInt64();
                int mode = reader.ReadInt32();
                if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || mode < 0 || mode > 0x1ff)
                {
                    throw new InvalidDataException("Invalid task cache file attributes.");
                }

                if (exists)
                {
                    ValidateDigest(digest);
                }

                artifacts.Add(new Artifact(exists, digest, ticks, mode));
            }

            int stateLength = reader.ReadInt32();
            if (stateLength < 0 || stateLength > stream.Length - stream.Position)
            {
                throw new InvalidDataException("Invalid task cache state size.");
            }

            state = reader.ReadBytes(stateLength);
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException("Unexpected data in task cache manifest.");
            }
        }

        // Validate raw task outputs before any output files are changed.
        validateState?.Invoke(state);

        List<string> stagingPaths = [];
        try
        {
            // All blobs are verified before any destination is touched. Once staging starts,
            // failures are fatal: falling back cannot undo arbitrary partial restoration.
            for (int i = 0; i < outputPaths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckPath(outputPaths[i], allowDirectory: false);
                if (!artifacts[i].Exists)
                {
                    stagingPaths.Add(String.Empty);
                    continue;
                }

                string parent = Path.GetDirectoryName(outputPaths[i])!;
                Directory.CreateDirectory(parent);
                string staging = Path.Combine(parent, ".msbuild-cache-" + Guid.NewGuid().ToString("N"));
                stagingPaths.Add(staging);
                using (Stream content = backend.Open(artifacts[i].Digest, cancellationToken))
                using (FileStream destination = CreateStagingFile(staging))
                {
                    content.CopyToAsync(destination, 81920, cancellationToken).GetAwaiter().GetResult();
                }

                if (!String.Equals(HashFile(staging), artifacts[i].Digest, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Task cache content changed during restoration.");
                }

#if NET
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(staging, (UnixFileMode)artifacts[i].Mode);
                }
#endif
                // Restored artifacts are newly produced for this invocation. Reusing an old
                // cache timestamp would keep timestamp-based targets perpetually out of date.
                File.SetLastWriteTimeUtc(staging, DateTime.UtcNow);
            }

            cancellationToken.ThrowIfCancellationRequested();
            // Cancellation must not interrupt the bounded replacement phase.
            onReplacementStarted?.Invoke();
            for (int i = 0; i < outputPaths.Count; i++)
            {
                CheckPath(outputPaths[i], allowDirectory: false);
                if (artifacts[i].Exists)
                {
#if NET
                    File.Move(stagingPaths[i], outputPaths[i], overwrite: true);
#else
                    if (File.Exists(outputPaths[i]))
                    {
                        File.Replace(stagingPaths[i], outputPaths[i], null);
                    }
                    else
                    {
                        File.Move(stagingPaths[i], outputPaths[i]);
                    }
#endif
                }
                else
                {
                    try
                    {
                        File.Delete(outputPaths[i]);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // A missing parent already satisfies an absent-output record.
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception e) when (e is InvalidDataException || ExceptionHandling.IsIoRelatedException(e))
        {
            throw new TaskCacheRestoreException(e);
        }
        finally
        {
            foreach (string path in stagingPaths)
            {
                if (path.Length > 0)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        // Best-effort cleanup must not hide the original restore failure.
                    }
                }
            }
        }

        return true;
    }

    private static FileStream CreateStagingFile(string path)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            });
        }
#endif
        return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    }

    internal bool Store(string key, IReadOnlyList<string> outputPaths, byte[] state, Func<bool>? canPublish = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePaths(outputPaths);
        ValidateDigest(key);
        cancellationToken.ThrowIfCancellationRequested();
        TaskCacheBackend backend = _backend;
        List<string> artifacts = [];
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(outputPaths.Count);
            foreach (string output in outputPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckPath(output, allowDirectory: false);
                WriteString(writer, output);
                bool exists = File.Exists(output);
                writer.Write(exists);
                if (exists)
                {
                    string digest = backend.PutFile(output, cancellationToken);
                    artifacts.Add(digest);
                    WriteString(writer, digest);
                }

                writer.Write(exists ? File.GetLastWriteTimeUtc(output).Ticks : 0);
                int mode = 0;
#if NET
                if (exists && !OperatingSystem.IsWindows())
                {
                    mode = (int)File.GetUnixFileMode(output) & 0x1ff;
                }
#endif
                writer.Write(mode);
            }

            writer.Write(state.Length);
            writer.Write(state);
        }

        if (stream.Length + 32 > MaxManifestSize)
        {
            throw new IOException("Task cache manifest exceeds the supported size.");
        }

        byte[] payload = stream.ToArray();
        using SHA256 hash = SHA256.Create();
        byte[] checksum = hash.ComputeHash(payload);
        stream.Write(checksum, 0, checksum.Length);
        return backend.Publish(key, stream.ToArray(), canPublish, artifacts, cancellationToken);
    }

    internal static void CreateCacheDirectory(string path)
    {
        CheckPath(path, allowDirectory: true);
#if NET
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return;
        }
#endif
        Directory.CreateDirectory(path);
    }

    private void ValidatePaths(IReadOnlyList<string> paths)
    {
        CheckPath(_directory, allowDirectory: true);
        HashSet<string> unique = new(FileUtilities.PathComparer);
        foreach (string path in paths)
        {
            if (!Path.IsPathRooted(path) || !unique.Add(path)
                || FileUtilities.PathComparer.Equals(path, _directory)
                || path.StartsWith(_directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    FileUtilities.IsFileSystemCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Invalid or overlapping task cache output path.");
            }

            CheckPath(path, allowDirectory: false);
        }
    }

    internal static string ValidateDigest(string digest)
    {
        if (digest.Length != 64)
        {
            throw new InvalidDataException("Invalid task cache digest.");
        }

        foreach (char c in digest)
        {
            if (c is not (>= '0' and <= '9' or >= 'A' and <= 'F'))
            {
                throw new InvalidDataException("Invalid task cache digest.");
            }
        }

        return digest;
    }
    internal static void CheckPath(string path, bool allowDirectory, bool allowReadOnlyFile = false)
    {
        bool first = true;
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (FileNotFoundException)
            {
                first = false;
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                first = false;
                continue;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0
                || (first && !allowDirectory && (attributes & FileAttributes.Directory) != 0)
                || (first && !allowDirectory && !allowReadOnlyFile && (attributes & FileAttributes.ReadOnly) != 0))
            {
                throw new IOException("Task caching does not support symbolic links, directories as files, or read-only outputs: " + path);
            }

            first = false;
        }
    }

    private static string HashFile(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return HashStream(stream);
    }

    private static string HashStream(Stream stream)
    {
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", String.Empty);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > 1024 * 1024 || length > reader.BaseStream.Length - reader.BaseStream.Position)
        {
            throw new InvalidDataException("Invalid task cache string size.");
        }

        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    private readonly record struct Artifact(bool Exists, string Digest, long Ticks, int Mode);
    internal static bool IsSupported => TaskCacheBackend.IsSupported;

    internal static void EnsureSupported()
    {
        if (!IsSupported)
        {
            throw new NotSupportedException(ResourceUtilities.GetResourceString("TaskCache.StorageUnavailable"));
        }
    }
}

internal sealed class TaskCacheRestoreException(Exception inner)
    : IOException("Task cache output restoration failed. Outputs may be partially replaced. Fix the filesystem problem, then clean and rebuild. " + inner.Message, inner);
