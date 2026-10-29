// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.Utilities;

/// <summary>
/// Computes content-based keys and stores directory or file-set entries on local disk.
/// </summary>
/// <remarks>
/// Callers are responsible for describing all inputs and keeping them stable through execution.
/// Entries are immutable and published atomically. Concurrent processes may share the cache,
/// but must not restore to the same destinations concurrently. Restoration is not transactional:
/// callers must prepare outputs before normal execution following a failed restore.
/// The API and on-disk format are experimental and have no compatibility guarantee.
/// </remarks>
[Experimental("MSBUILD001")]
public sealed class PersistentCache
{
    private const int FormatVersion = 1;
    private const string VersionDirectory = "v1";
    private const string LastUsedFileName = "last-used";

    private static readonly bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly StringComparison s_pathComparison = s_isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Gets the absolute path of the cache directory.
    /// </summary>
    public string CacheDirectory { get; }

    /// <summary>
    /// Creates a cache backed by the specified directory. No files are created until storage.
    /// </summary>
    /// <param name="cacheDirectory">The cache path, resolved relative to the current directory.</param>
    public PersistentCache(string cacheDirectory)
    {
        PersistentCacheOutputs.ValidatePath(cacheDirectory);
        CacheDirectory = Path.GetFullPath(cacheDirectory);
    }

    /// <summary>
    /// Computes a SHA-256 key from input paths and contents, output paths, and execution context.
    /// </summary>
    /// <param name="inputs">The ordered input files, including relevant toolchain files.</param>
    /// <param name="outputs">The owned directory or ordered file set.</param>
    /// <param name="context">
    /// Ordered values such as effective arguments and relevant environment settings.
    /// </param>
    /// <returns>A lowercase SHA-256 hexadecimal key.</returns>
    /// <remarks>
    /// The current directory, operating system, process architecture, and cultures also
    /// contribute to the key. Compute the key before executing the tool and reuse it for
    /// lookup and storage. Missing input files are distinguished from empty files.
    /// Input read failures other than missing files propagate to the caller.
    /// </remarks>
    public static string ComputeKey(PersistentCacheInputs inputs, PersistentCacheOutputs outputs, params string[] context)
    {
        if (inputs is null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        if (outputs is null)
        {
            throw new ArgumentNullException(nameof(outputs));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        using var sha256 = SHA256.Create();
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data, Encoding.UTF8, leaveOpen: true);
        writer.Write("invocation-cache-key-v1");
        writer.Write(Environment.CurrentDirectory);
        writer.Write(RuntimeInformation.OSDescription);
        writer.Write(RuntimeInformation.ProcessArchitecture.ToString());
        writer.Write(CultureInfo.CurrentCulture.Name);
        writer.Write(CultureInfo.CurrentUICulture.Name);
        writer.Write(context.Length);
        foreach (string value in context)
        {
            writer.Write(value);
        }

        writer.Write(outputs.DirectoryPath is not null);
        if (outputs.DirectoryPath is not null)
        {
            writer.Write(outputs.DirectoryPath);
        }

        writer.Write(outputs.Files.Length);
        foreach (string file in outputs.Files)
        {
            writer.Write(file);
        }

        writer.Write(inputs.Files.Length);
        foreach (string input in inputs.Files)
        {
            writer.Write(input);
            try
            {
                using FileStream file = File.OpenRead(input);
                writer.Write(true);
                writer.Write(sha256.ComputeHash(file));
            }
            catch (FileNotFoundException)
            {
                writer.Write(false);
            }
            catch (DirectoryNotFoundException)
            {
                writer.Write(false);
            }
        }

        writer.Flush();
        data.Position = 0;
        return BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Validates and restores an entry, replacing the declared outputs.
    /// </summary>
    /// <param name="key">A lowercase SHA-256 hexadecimal key.</param>
    /// <param name="outputs">The owned directory or ordered file set to restore.</param>
    /// <param name="outputTimestampUtc">The UTC modification time to assign to restored files.</param>
    /// <returns>
    /// <see langword="true"/> if restoration succeeded; <see langword="false"/> if the entry
    /// is absent, invalid, incompatible, or inaccessible, or if restoring a file failed.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The key or timestamp is invalid, or the outputs overlap each other or the cache.
    /// </exception>
    public bool TryRestore(string key, PersistentCacheOutputs outputs, DateTime outputTimestampUtc)
    {
        string entryDirectory = GetEntryDirectory(key);
        ValidateOutputs(outputs);
        if (outputTimestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The output timestamp must be UTC.", nameof(outputTimestampUtc));
        }

        if (!Directory.Exists(entryDirectory))
        {
            return false;
        }

        try
        {
            List<(string Name, bool Present)> files = ReadManifest(entryDirectory, outputs);

            // Validate every cached file before changing any destination.
            if (outputs.DirectoryPath is not null)
            {
                if (Directory.Exists(outputs.DirectoryPath))
                {
                    Directory.Delete(outputs.DirectoryPath, recursive: true);
                }

                Directory.CreateDirectory(outputs.DirectoryPath);
            }

            for (int i = 0; i < files.Count; i++)
            {
                string destination = GetOutputPath(outputs, files[i].Name, i);
                if (files[i].Present)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(Path.Combine(entryDirectory, "outputs", files[i].Name), destination, overwrite: true);
                    File.SetLastWriteTimeUtc(destination, outputTimestampUtc);
                }
                else
                {
                    DeleteOutputFile(destination);
                }
            }

            UpdateLastUsed(entryDirectory);
            return true;
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
            return false;
        }
    }

    /// <summary>
    /// Copies the declared outputs into a new immutable cache entry.
    /// </summary>
    /// <param name="key">A lowercase SHA-256 hexadecimal key.</param>
    /// <param name="outputs">The owned directory or ordered file set to store.</param>
    /// <returns>
    /// <see langword="true"/> if a new entry was published; <see langword="false"/> if an entry
    /// already exists, another writer owns the key, or a recoverable storage failure occurred.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The key is invalid, or the outputs overlap each other or the cache.
    /// </exception>
    public bool TryStore(string key, PersistentCacheOutputs outputs)
    {
        string entryDirectory = GetEntryDirectory(key);
        ValidateOutputs(outputs);
        string? stagingDirectory = null;

        try
        {
            using var mutex = new Mutex(initiallyOwned: false, GetWriterMutexName(key));
            bool acquired;
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                return false;
            }

            try
            {
                if (Directory.Exists(entryDirectory))
                {
                    return false;
                }

                List<string> files = GetFileNames(outputs);
                bool fileSet = outputs.DirectoryPath is null;
                string parentDirectory = Path.GetDirectoryName(entryDirectory)!;
                Directory.CreateDirectory(parentDirectory);
                stagingDirectory = Path.Combine(parentDirectory, key + "." + Guid.NewGuid().ToString("N") + ".tmp");
                Directory.CreateDirectory(stagingDirectory);

                using (var manifest = new BinaryWriter(File.Create(Path.Combine(stagingDirectory, "manifest"))))
                {
                    manifest.Write(FormatVersion);
                    manifest.Write(fileSet);
                    manifest.Write(files.Count);
                    for (int i = 0; i < files.Count; i++)
                    {
                        string name = files[i];
                        string cachedFile = Path.Combine(stagingDirectory, "outputs", name);
                        Directory.CreateDirectory(Path.GetDirectoryName(cachedFile)!);
                        bool present = true;
                        try
                        {
                            File.Copy(GetOutputPath(outputs, name, i), cachedFile);
                        }
                        catch (Exception ex) when (fileSet && ex is FileNotFoundException or DirectoryNotFoundException)
                        {
                            present = false;
                        }

                        manifest.Write(name);
                        manifest.Write(present);
                        if (present)
                        {
                            manifest.Write(new FileInfo(cachedFile).Length);
                            manifest.Write(HashFile(cachedFile));
                        }
                    }
                }

                File.WriteAllBytes(Path.Combine(stagingDirectory, "manifest.sha256"), HashFile(Path.Combine(stagingDirectory, "manifest")));
                WriteLastUsed(stagingDirectory);
                Directory.Move(stagingDirectory, entryDirectory);
                stagingDirectory = null;
                return true;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
            return false;
        }
        finally
        {
            if (stagingDirectory is not null)
            {
                try
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static void WriteLastUsed(string entryDirectory) =>
        File.WriteAllText(Path.Combine(entryDirectory, LastUsedFileName), DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

    private static void UpdateLastUsed(string entryDirectory)
    {
        try
        {
            WriteLastUsed(entryDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private string GetEntryDirectory(string key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (key.Length != 64)
        {
            throw new ArgumentException("The key must be a lowercase SHA-256 hexadecimal string.", nameof(key));
        }

        foreach (char c in key)
        {
            if (c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                throw new ArgumentException("The key must be a lowercase SHA-256 hexadecimal string.", nameof(key));
            }
        }

        return Path.Combine(CacheDirectory, VersionDirectory, key);
    }

    private string GetWriterMutexName(string key)
    {
        string directory = CacheDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (s_isWindows)
        {
            directory = directory.ToUpperInvariant();
        }

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(directory + "\n" + key));
        return @"Global\MSBuildPersistentCache-" + BitConverter.ToString(hash).Replace("-", "");
    }

    private static List<(string Name, bool Present)> ReadManifest(string entryDirectory, PersistentCacheOutputs outputs)
    {
        bool fileSet = outputs.DirectoryPath is null;
        if (!HashesEqual(File.ReadAllBytes(Path.Combine(entryDirectory, "manifest.sha256")), HashFile(Path.Combine(entryDirectory, "manifest"))))
        {
            throw new InvalidDataException();
        }

        using var manifest = new BinaryReader(File.OpenRead(Path.Combine(entryDirectory, "manifest")));
        if (manifest.ReadInt32() != FormatVersion || manifest.ReadBoolean() != fileSet)
        {
            throw new InvalidDataException();
        }

        int fileCount = manifest.ReadInt32();
        if (fileCount < 0 || fileCount > manifest.BaseStream.Length - manifest.BaseStream.Position ||
            (fileSet && fileCount != outputs.Files.Length))
        {
            throw new InvalidDataException();
        }

        var files = new List<(string Name, bool Present)>();
        var paths = new HashSet<string>(s_isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        string outputsDirectory = Path.Combine(entryDirectory, "outputs");
        for (int i = 0; i < fileCount; i++)
        {
            string name = manifest.ReadString();
            ValidateRelativePath(name);
            if (!paths.Add(name) || (fileSet && name != i.ToString(CultureInfo.InvariantCulture)))
            {
                throw new InvalidDataException();
            }

            bool present = manifest.ReadBoolean();
            if (!present && !fileSet)
            {
                throw new InvalidDataException();
            }

            if (present)
            {
                long length = manifest.ReadInt64();
                byte[] expectedHash = manifest.ReadBytes(32);
                string cachedFile = Path.Combine(outputsDirectory, name);
                if (expectedHash.Length != 32 || new FileInfo(cachedFile).Length != length ||
                    !HashesEqual(expectedHash, HashFile(cachedFile)))
                {
                    throw new InvalidDataException();
                }
            }

            files.Add((name, present));
        }

        if (manifest.BaseStream.Position != manifest.BaseStream.Length)
        {
            throw new InvalidDataException();
        }

        return files;
    }

    private static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path))
        {
            throw new InvalidDataException();
        }

        foreach (string component in path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (component is "" or "." or ".." || component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                (s_isWindows && component[component.Length - 1] is '.' or ' '))
            {
                throw new InvalidDataException();
            }
        }
    }

    private void ValidateOutputs(PersistentCacheOutputs outputs)
    {
        if (outputs is null)
        {
            throw new ArgumentNullException(nameof(outputs));
        }

        string cachePrefix = WithTrailingSeparator(CacheDirectory);
        if (outputs.DirectoryPath is not null)
        {
            string outputPrefix = WithTrailingSeparator(outputs.DirectoryPath);
            if (cachePrefix.StartsWith(outputPrefix, s_pathComparison) || outputPrefix.StartsWith(cachePrefix, s_pathComparison))
            {
                throw new ArgumentException("The cache and output directories must not contain each other.", nameof(outputs));
            }
        }
        else
        {
            var paths = new HashSet<string>(s_isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (string file in outputs.Files)
            {
                string filePrefix = WithTrailingSeparator(file);
                foreach (string existing in paths)
                {
                    if (existing.StartsWith(filePrefix, s_pathComparison) || file.StartsWith(WithTrailingSeparator(existing), s_pathComparison))
                    {
                        throw new ArgumentException("Declared output files must not contain each other.", nameof(outputs));
                    }
                }

                if (!paths.Add(file) || file.StartsWith(cachePrefix, s_pathComparison) || cachePrefix.StartsWith(filePrefix, s_pathComparison))
                {
                    throw new ArgumentException("Cache outputs must be distinct files outside the cache.", nameof(outputs));
                }
            }
        }
    }

    private static string WithTrailingSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

    private static List<string> GetFileNames(PersistentCacheOutputs outputs)
    {
        var files = new List<string>();
        if (outputs.DirectoryPath is not null)
        {
            Collect(outputs.DirectoryPath, "");
            files.Sort(StringComparer.Ordinal);
        }
        else
        {
            for (int i = 0; i < outputs.Files.Length; i++)
            {
                files.Add(i.ToString(CultureInfo.InvariantCulture));
            }
        }

        return files;

        void Collect(string directory, string relativeDirectory)
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                string relativePath = Path.Combine(relativeDirectory, Path.GetFileName(path));
                if (Directory.Exists(path))
                {
                    Collect(path, relativePath);
                }
                else
                {
                    files.Add(relativePath);
                }
            }
        }
    }

    private static string GetOutputPath(PersistentCacheOutputs outputs, string name, int index) =>
        outputs.DirectoryPath is not null ? Path.Combine(outputs.DirectoryPath, name) : outputs.Files[index];

    private static void DeleteOutputFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // A missing parent already satisfies the declared file's absence.
        }
    }

    private static byte[] HashFile(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        return sha256.ComputeHash(stream);
    }

    private static bool HashesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCacheFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException;
}
