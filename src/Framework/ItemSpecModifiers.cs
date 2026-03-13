// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

/// <summary>
///  Encapsulates the definitions of the item-spec modifiers a.k.a. reserved item metadata.
/// </summary>
internal static class ItemSpecModifiers
{
    internal const string FullPath = "FullPath";
    internal const string RootDir = "RootDir";
    internal const string Filename = "Filename";
    internal const string Extension = "Extension";
    internal const string RelativeDir = "RelativeDir";
    internal const string Directory = "Directory";
    internal const string RecursiveDir = "RecursiveDir";
    internal const string Identity = "Identity";
    internal const string ModifiedTime = "ModifiedTime";
    internal const string CreatedTime = "CreatedTime";
    internal const string AccessedTime = "AccessedTime";
    internal const string DefiningProjectFullPath = "DefiningProjectFullPath";
    internal const string DefiningProjectDirectory = "DefiningProjectDirectory";
    internal const string DefiningProjectName = "DefiningProjectName";
    internal const string DefiningProjectExtension = "DefiningProjectExtension";

    // These are all the well-known attributes.
    internal static readonly string[] All =
    {
        FullPath,
        RootDir,
        Filename,
        Extension,
        RelativeDir,
        Directory,
        RecursiveDir,    // <-- Not derivable.
        Identity,
        ModifiedTime,
        CreatedTime,
        AccessedTime,
        DefiningProjectFullPath,
        DefiningProjectDirectory,
        DefiningProjectName,
        DefiningProjectExtension
    };

    /// <summary>
    ///  <para>
    ///   Caches derivable item-spec modifier results for a single item spec.
    ///   Stored on item instances (e.g., TaskItem, ProjectItemInstance.TaskItem)
    ///   alongside the item spec, replacing the former <c>string _fullPath</c> field.
    ///  </para>
    ///  <para>
    ///   Time-based modifiers (ModifiedTime, CreatedTime, AccessedTime) and RecursiveDir
    ///   are intentionally excluded — time-based modifiers hit the file system and should
    ///   not be cached, and RecursiveDir requires wildcard context that only the caller has.
    ///  </para>
    ///  <para>
    ///   DefiningProject* modifiers are cached separately in a static shared cache
    ///   (<see cref="s_definingProjectCache"/>) keyed by the defining project path,
    ///   since many items share the same defining project.
    ///  </para>
    /// </summary>
    internal struct Cache
    {
        public string? FullPath;
        public string? RootDir;
        public string? Filename;
        public string? Extension;
        public string? RelativeDir;
        public string? Directory;

        /// <summary>
        ///  Clears all cached values. Called when the item spec changes.
        /// </summary>
        public void Clear()
            => this = default;
    }

    /// <summary>
    ///  Cached results for all four DefiningProject* modifiers, computed from a single
    ///  defining project path. Instances are shared across all items that originate from
    ///  the same project file.
    /// </summary>
    private sealed class DefiningProjectModifierCache
    {
        public readonly string FullPath;
        public readonly string Directory;
        public readonly string Name;
        public readonly string Extension;

        public DefiningProjectModifierCache(string? currentDirectory, string definingProjectEscaped)
        {
            FullPath = ComputeFullPath(currentDirectory, definingProjectEscaped);
            string rootDir = ComputeRootDir(FullPath);
            string directory = ComputeDirectory(FullPath);
            Directory = Path.Combine(rootDir, directory);
            Name = ComputeFilename(definingProjectEscaped);
            Extension = ComputeExtension(definingProjectEscaped);
        }
    }

    /// <summary>
    ///  Static cache of DefiningProject* results keyed by the escaped defining project path.
    ///  In a typical build there are only a handful of distinct defining projects (tens, not thousands),
    ///  so this dictionary stays very small. The cache lives for the lifetime of the process.
    /// </summary>
    private static readonly ConcurrentDictionary<string, DefiningProjectModifierCache> s_definingProjectCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Resolves a modifier name to its <see cref="ItemSpecModifierKind"/> using a length+char switch
    ///  instead of a dictionary lookup. Every length bucket is unique or disambiguated by at
    ///  most two character comparisons, so misses are rejected in O(1) with no hashing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetModifierKind(string name, out ItemSpecModifierKind kind)
    {
        switch (name.Length)
        {
            case 7:
                // RootDir
                if (string.Equals(name, RootDir, StringComparison.OrdinalIgnoreCase))
                {
                    kind = ItemSpecModifierKind.RootDir;
                    return true;
                }

                break;

            case 8:
                // FullPath, Filename, Identity
                switch (name[0])
                {
                    case 'F' or 'f':
                        switch (name[1])
                        {
                            case 'U' or 'u':
                                if (string.Equals(name, FullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    kind = ItemSpecModifierKind.FullPath;
                                    return true;
                                }

                                break;

                            case 'I' or 'i':
                                if (string.Equals(name, Filename, StringComparison.OrdinalIgnoreCase))
                                {
                                    kind = ItemSpecModifierKind.Filename;
                                    return true;
                                }

                                break;
                        }

                        break;

                    case 'I' or 'i':
                        if (string.Equals(name, Identity, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.Identity;
                            return true;
                        }

                        break;
                }

                break;

            case 9:
                // Extension, Directory
                switch (name[0])
                {
                    case 'E' or 'e':
                        if (string.Equals(name, Extension, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.Extension;
                            return true;
                        }

                        break;

                    case 'D' or 'd':
                        if (string.Equals(name, Directory, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.Directory;
                            return true;
                        }

                        break;
                }

                break;

            case 11:
                // RelativeDir, CreatedTime
                switch (name[0])
                {
                    case 'R' or 'r':
                        if (string.Equals(name, RelativeDir, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.RelativeDir;
                            return true;
                        }

                        break;

                    case 'C' or 'c':
                        if (string.Equals(name, CreatedTime, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.CreatedTime;
                            return true;
                        }

                        break;
                }

                break;

            case 12:
                // RecursiveDir, ModifiedTime, AccessedTime
                switch (name[0])
                {
                    case 'R' or 'r':
                        if (string.Equals(name, RecursiveDir, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.RecursiveDir;
                            return true;
                        }

                        break;

                    case 'M' or 'm':
                        if (string.Equals(name, ModifiedTime, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.ModifiedTime;
                            return true;
                        }

                        break;

                    case 'A' or 'a':
                        if (string.Equals(name, AccessedTime, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.AccessedTime;
                            return true;
                        }

                        break;
                }

                break;

            case 19:
                // DefiningProjectName
                if (string.Equals(name, DefiningProjectName, StringComparison.OrdinalIgnoreCase))
                {
                    kind = ItemSpecModifierKind.DefiningProjectName;
                    return true;
                }

                break;

            case 23:
                // DefiningProjectFullPath
                if (string.Equals(name, DefiningProjectFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    kind = ItemSpecModifierKind.DefiningProjectFullPath;
                    return true;
                }

                break;

            case 24:
                // DefiningProjectDirectory, DefiningProjectExtension
                switch (name[15])
                {
                    case 'D' or 'd':
                        if (string.Equals(name, DefiningProjectDirectory, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.DefiningProjectDirectory;
                            return true;
                        }

                        break;

                    case 'E' or 'e':
                        if (string.Equals(name, DefiningProjectExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ItemSpecModifierKind.DefiningProjectExtension;
                            return true;
                        }

                        break;
                }

                break;
        }

        kind = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetDerivableModifierKind(string name, out ItemSpecModifierKind result)
    {
        if (TryGetModifierKind(name, out ItemSpecModifierKind kind) &&
            kind is not ItemSpecModifierKind.RecursiveDir)
        {
            result = kind;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Indicates if the given name is reserved for an item-spec modifier.
    /// </summary>
    public static bool IsItemSpecModifier([NotNullWhen(true)] string? name)
        => name is not null
        && TryGetModifierKind(name, out _);

    /// <summary>
    /// Indicates if the given name is reserved for a derivable item-spec modifier.
    /// Derivable means it can be computed given a file name.
    /// </summary>
    /// <param name="name">Name to check.</param>
    /// <returns>true, if name of a derivable modifier</returns>
    public static bool IsDerivableItemSpecModifier([NotNullWhen(true)] string? name)
        => name is not null
        && TryGetDerivableModifierKind(name, out _);

    /// <summary>
    ///  Performs path manipulations on the given item-spec as directed.
    ///  Does not cache the result.
    /// </summary>
    internal static string GetItemSpecModifier(string? currentDirectory, string itemSpec, string? definingProjectEscaped, string modifier)
    {
        if (!TryGetModifierKind(modifier, out ItemSpecModifierKind kind))
        {
            throw new InternalErrorException($"\"{modifier}\" is not a valid item-spec modifier.");
        }

        Cache cache = default;
        return GetItemSpecModifier(currentDirectory, itemSpec, definingProjectEscaped, kind, ref cache);
    }

    /// <summary>
    /// Performs path manipulations on the given item-spec as directed, caching
    /// derivable results in <paramref name="cache"/> for subsequent calls on the same item spec.
    ///
    /// Supported modifiers:
    ///     %(FullPath)         = full path of item
    ///     %(RootDir)          = root directory of item
    ///     %(Filename)         = item filename without extension
    ///     %(Extension)        = item filename extension
    ///     %(RelativeDir)      = item directory as given in item-spec
    ///     %(Directory)        = full path of item directory relative to root
    ///     %(RecursiveDir)     = portion of item path that matched a recursive wildcard
    ///     %(Identity)         = item-spec as given
    ///     %(ModifiedTime)     = last write time of item
    ///     %(CreatedTime)      = creation time of item
    ///     %(AccessedTime)     = last access time of item
    ///
    /// NOTES:
    /// 1) This method always returns an empty string for the %(RecursiveDir) modifier because it does not have enough
    ///    information to compute it -- only the BuildItem class can compute this modifier.
    /// 2) Time-based modifiers are not cached — they hit the file system and may change between calls.
    /// 3) DefiningProject* modifiers operate on <paramref name="definingProjectEscaped"/>, not <paramref name="itemSpec"/>.
    ///    Their results are cached in a static shared cache keyed by the defining project path, since many
    ///    items share the same defining project and the set of distinct projects is small (typically tens).
    /// </summary>
    /// <remarks>
    /// Never returns null.
    /// </remarks>
    /// <param name="currentDirectory">The root directory for relative item-specs.</param>
    /// <param name="itemSpec">The item-spec to modify.</param>
    /// <param name="definingProjectEscaped">The path to the project that defined this item (may be null).</param>
    /// <param name="modifierKind">The modifier to apply to the item-spec.</param>
    /// <param name="cache">Per-item cache of derivable modifier values.</param>
    /// <returns>The modified item-spec (can be empty string, but will never be null).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the item-spec is not a path.</exception>
    public static string GetItemSpecModifier(
        string? currentDirectory,
        string itemSpec,
        string? definingProjectEscaped,
        ItemSpecModifierKind modifierKind,
        ref Cache cache)
    {
        FrameworkErrorUtilities.VerifyThrow(itemSpec != null, "Need item-spec to modify.");

        try
        {
            switch (modifierKind)
            {
                case ItemSpecModifierKind.FullPath:
                    return cache.FullPath ??= ComputeFullPath(currentDirectory, itemSpec);

                case ItemSpecModifierKind.RootDir:
                    return cache.RootDir ??= ComputeRootDir(cache.FullPath ??= ComputeFullPath(currentDirectory, itemSpec));

                case ItemSpecModifierKind.Filename:
                    return cache.Filename ??= ComputeFilename(itemSpec);

                case ItemSpecModifierKind.Extension:
                    return cache.Extension ??= ComputeExtension(itemSpec);

                case ItemSpecModifierKind.RelativeDir:
                    return cache.RelativeDir ??= ComputeRelativeDir(itemSpec);

                case ItemSpecModifierKind.Directory:
                    return cache.Directory ??= ComputeDirectory(cache.FullPath ??= ComputeFullPath(currentDirectory, itemSpec));

                case ItemSpecModifierKind.RecursiveDir:
                    return string.Empty;

                case ItemSpecModifierKind.Identity:
                    return itemSpec;

                // Time-based modifiers are NOT cached - they hit the file system.
                case ItemSpecModifierKind.ModifiedTime:
                    return ComputeModifiedTime(itemSpec);

                case ItemSpecModifierKind.CreatedTime:
                    return ComputeCreatedTime(itemSpec);

                case ItemSpecModifierKind.AccessedTime:
                    return ComputeAccessedTime(itemSpec);

                default:
                    break;
            }

            // DefiningProject* modifiers — these operate on definingProjectEscaped, NOT itemSpec.
            // Results are cached in a static shared dictionary keyed by the defining project path.
            if (string.IsNullOrEmpty(definingProjectEscaped))
            {
                return string.Empty;
            }

            FrameworkErrorUtilities.VerifyThrow(definingProjectEscaped != null, "How could definingProjectEscaped by null?");

            // Fast path: check if we already have cached results for this defining project.
            // This avoids any closure allocation on the hot path. The miss path only runs once per distinct defining project.
            if (!s_definingProjectCache.TryGetValue(definingProjectEscaped, out DefiningProjectModifierCache? definingProjectModifiers))
            {
                string? dir = currentDirectory;
                definingProjectModifiers = s_definingProjectCache.GetOrAdd(
                    definingProjectEscaped,
                    key => new DefiningProjectModifierCache(dir, key));
            }

            switch (modifierKind)
            {
                case ItemSpecModifierKind.DefiningProjectFullPath:
                    return definingProjectModifiers.FullPath;

                case ItemSpecModifierKind.DefiningProjectDirectory:
                    return definingProjectModifiers.Directory;

                case ItemSpecModifierKind.DefiningProjectName:
                    return definingProjectModifiers.Name;

                case ItemSpecModifierKind.DefiningProjectExtension:
                    return definingProjectModifiers.Extension;
            }
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            throw new InvalidOperationException(SR.FormatInvalidFilespecForTransform(modifierKind, itemSpec, e.Message));
        }

        throw new InternalErrorException($"\"{modifierKind}\" is not a valid item-spec modifier.");
    }

    private static string ComputeFullPath(string? currentDirectory, string itemSpec)
    {
        currentDirectory ??= FileUtilities.CurrentThreadWorkingDirectory ?? string.Empty;

        string result = FileUtilities.GetFullPath(itemSpec, currentDirectory);

        ThrowForUrl(result, itemSpec, currentDirectory);

        return result;
    }

    private static string ComputeRootDir(string fullPath)
    {
        string? root = Path.GetPathRoot(fullPath)!;

        if (!FileUtilities.EndsWithSlash(root))
        {
            FrameworkErrorUtilities.VerifyThrow(
                FileUtilitiesRegex.StartsWithUncPattern(root),
                "Only UNC shares should be missing trailing slashes.");

            // restore/append trailing slash if Path.GetPathRoot() has either removed it, or failed to add it
            // (this happens with UNC shares)
            root += Path.DirectorySeparatorChar;
        }

        return root;
    }

    private static string ComputeFilename(string itemSpec)
    {
        // if the item-spec is a root directory, it can have no filename
        if (IsRootDirectory(itemSpec))
        {
            // NOTE: this is to prevent Path.GetFileNameWithoutExtension() from treating server and share elements
            // in a UNC file-spec as filenames e.g. \\server, \\server\share
            return string.Empty;
        }
        else
        {
            // Fix path to avoid problem with Path.GetFileNameWithoutExtension when backslashes in itemSpec on Unix
            return Path.GetFileNameWithoutExtension(FileUtilities.FixFilePath(itemSpec));
        }
    }

    private static string ComputeExtension(string itemSpec)
    {
        // if the item-spec is a root directory, it can have no extension
        if (IsRootDirectory(itemSpec))
        {
            // NOTE: this is to prevent Path.GetExtension() from treating server and share elements in a UNC
            // file-spec as filenames e.g. \\server.ext, \\server\share.ext
            return string.Empty;
        }
        else
        {
            return Path.GetExtension(itemSpec);
        }
    }

    private static string ComputeRelativeDir(string itemSpec)
        => FileUtilities.GetDirectory(itemSpec);

    private static string ComputeDirectory(string fullPath)
    {
        string directory = FileUtilities.GetDirectory(fullPath);

        if (NativeMethods.IsWindows)
        {
            int length;

            if (FileUtilitiesRegex.StartsWithDrivePattern(directory))
            {
                length = 2;
            }
            else
            {
                length = FileUtilitiesRegex.StartsWithUncPatternMatchLength(directory);
            }

            if (length != -1)
            {
                FrameworkErrorUtilities.VerifyThrow(
                    (directory.Length > length) && FileUtilities.IsSlash(directory[length]),
                    "Root directory must have a trailing slash.");

                return directory.Substring(length + 1);
            }

            return directory;
        }

        FrameworkErrorUtilities.VerifyThrow(
            !string.IsNullOrEmpty(directory) && FileUtilities.IsSlash(directory[0]),
            "Expected a full non-windows path rooted at '/'.");

        // A full unix path is always rooted at
        // `/`, and a root-relative path is the
        // rest of the string.
        return directory.Substring(1);
    }

    private static string ComputeModifiedTime(string itemSpec)
        => TryGetFileInfo(itemSpec, out FileInfo? info)
            ? info.LastWriteTime.ToString(FileUtilities.FileTimeFormat)
            : string.Empty;

    private static string ComputeCreatedTime(string itemSpec)
        => TryGetFileInfo(itemSpec, out FileInfo? info)
            ? info.CreationTime.ToString(FileUtilities.FileTimeFormat)
            : string.Empty;

    private static string ComputeAccessedTime(string itemSpec)
        => TryGetFileInfo(itemSpec, out FileInfo? info)
            ? info.LastAccessTime.ToString(FileUtilities.FileTimeFormat)
            : string.Empty;

    private static bool TryGetFileInfo(string itemSpec, [NotNullWhen(true)] out FileInfo? result)
    {
        // About to go out to the file system.  This means data is leaving the engine, so need to unescape first.
        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

        result = FileUtilities.GetFileInfoNoThrow(unescapedItemSpec);
        return result is not null;
    }

    /// <summary>
    /// Indicates whether the given path is a UNC or drive pattern root directory.
    /// <para>Note: This function mimics the behavior of checking if Path.GetDirectoryName(path) == null.</para>
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static bool IsRootDirectory(string path)
    {
        // Eliminate all non-rooted paths
        if (!Path.IsPathRooted(path))
        {
            return false;
        }

        int uncMatchLength = FileUtilitiesRegex.StartsWithUncPatternMatchLength(path);

        // Determine if the given path is a standard drive/unc pattern root
        if (FileUtilitiesRegex.IsDrivePattern(path) ||
            FileUtilitiesRegex.IsDrivePatternWithSlash(path) ||
            uncMatchLength == path.Length)
        {
            return true;
        }

        // Eliminate all non-root unc paths.
        if (uncMatchLength != -1)
        {
            return false;
        }

        // Eliminate any drive patterns that don't have a slash after the colon or where the 4th character is a non-slash
        // A non-slash at [3] is specifically checked here because Path.GetDirectoryName
        // considers "C:///" a valid root.
        if (FileUtilitiesRegex.StartsWithDrivePattern(path) &&
            ((path.Length >= 3 && path[2] != '\\' && path[2] != '/') ||
            (path.Length >= 4 && path[3] != '\\' && path[3] != '/')))
        {
            return false;
        }

        // There are some edge cases that can get to this point.
        // After eliminating valid / invalid roots, fall back on original behavior.
        return Path.GetDirectoryName(path) == null;
    }

    /// <summary>
    /// Temporary check for something like http://foo which will end up like c:\foo\bar\http://foo
    /// We should either have no colon, or exactly one colon.
    /// UNDONE: This is a minimal safe change for Dev10. The correct fix should be to make GetFullPath/NormalizePath throw for this.
    /// </summary>
    private static void ThrowForUrl(string fullPath, string itemSpec, string currentDirectory)
    {
        if (fullPath.IndexOf(':') != fullPath.LastIndexOf(':'))
        {
            // Cause a better error to appear
            _ = Path.GetFullPath(Path.Combine(currentDirectory, itemSpec));
        }
    }
}
