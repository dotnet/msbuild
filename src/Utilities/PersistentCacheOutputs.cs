// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.Utilities;

/// <summary>
/// Describes the files owned by an entry in a <see cref="PersistentCache"/>.
/// </summary>
/// <remarks>
/// An owned directory is replaced in its entirety on restore. An ordered file set
/// restores only those files, including deleting files recorded as absent.
/// </remarks>
[Experimental("MSBUILD001")]
public sealed class PersistentCacheOutputs
{
    internal string? DirectoryPath { get; }
    internal string[] Files { get; }

    private PersistentCacheOutputs(string? directoryPath, string[] files)
    {
        DirectoryPath = directoryPath;
        Files = files;
    }

    /// <summary>
    /// Describes a directory whose entire contents belong to the cache entry.
    /// </summary>
    /// <param name="path">The directory path, resolved relative to the current directory.</param>
    /// <returns>The owned-directory description.</returns>
    public static PersistentCacheOutputs FromDirectory(string path)
    {
        ValidatePath(path);
        return new PersistentCacheOutputs(Path.GetFullPath(path), []);
    }

    /// <summary>
    /// Describes an ordered set of files, some of which may be absent.
    /// </summary>
    /// <param name="files">
    /// File paths, resolved relative to the current directory. Their order must remain
    /// the same when storing and restoring a key.
    /// </param>
    /// <returns>The file-set description, independent of subsequent changes to the supplied array.</returns>
    public static PersistentCacheOutputs FromFiles(params string[] files)
    {
        if (files is null)
        {
            throw new ArgumentNullException(nameof(files));
        }

        return new PersistentCacheOutputs(null, Array.ConvertAll(files, path =>
        {
            ValidatePath(path);
            return Path.GetFullPath(path);
        }));
    }

    internal static void ValidatePath(string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (path.Length == 0)
        {
            throw new ArgumentException("The path must not be empty.", nameof(path));
        }
    }
}
