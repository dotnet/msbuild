// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Framework;

/// <summary>
/// Compares full paths the way the file system resolves them. Two paths that differ only in letter case are equal
/// only if every differing segment is inside a case-insensitive directory.
/// </summary>
/// <remarks>
/// Paths that are equal under this comparer are also equal under <see cref="StringComparer.OrdinalIgnoreCase"/>,
/// so the hash code is the same. The file system is queried only for paths that differ only in case.
/// </remarks>
internal sealed class FileSystemPathComparer : IEqualityComparer<string?>
{
    internal static FileSystemPathComparer Instance { get; } = new();

    private FileSystemPathComparer()
    {
    }

    public bool Equals(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || !string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(x, y, StringComparison.Ordinal) || CaseDifferencesAreIgnoredByFileSystem(x, y);
    }

    public int GetHashCode(string? obj) => obj is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj);

    /// <summary>
    /// For two paths that are equal ignoring case, checks each segment that differs in case against the
    /// case sensitivity of the directory that contains it.
    /// </summary>
    private static bool CaseDifferencesAreIgnoredByFileSystem(string x, string y)
    {
        // Drive letters and UNC server and share names are case-insensitive.
        int segmentStart = Path.GetPathRoot(x)?.Length ?? 0;
        bool segmentDiffers = false;

        for (int i = segmentStart; i <= x.Length; i++)
        {
            if (i == x.Length || FileUtilities.IsSlash(x[i]))
            {
                if (segmentDiffers)
                {
                    bool caseSensitive = segmentStart == 0
                        ? FileUtilities.IsFileSystemCaseSensitive
                        : DirectoryCaseSensitivity.IsCaseSensitive(x.Substring(0, segmentStart));

                    if (caseSensitive)
                    {
                        return false;
                    }

                    segmentDiffers = false;
                }

                segmentStart = i + 1;
            }
            else if (x[i] != y[i])
            {
                segmentDiffers = true;
            }
        }

        return true;
    }
}
