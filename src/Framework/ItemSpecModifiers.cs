// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

/// <summary>
/// Encapsulates the definitions of the item-spec modifiers a.k.a. reserved item metadata.
/// </summary>
internal static class ItemSpecModifiers
{
    private enum ModifierKind
    {
        FullPath,
        RootDir,
        Filename,
        Extension,
        RelativeDir,
        Directory,
        RecursiveDir,
        Identity,
        ModifiedTime,
        CreatedTime,
        AccessedTime,
        DefiningProjectFullPath,
        DefiningProjectDirectory,
        DefiningProjectName,
        DefiningProjectExtension
    }

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
    ///  Resolves a modifier name to its <see cref="ModifierKind"/> using a length+char switch
    ///  instead of a dictionary lookup. Every length bucket is unique or disambiguated by at
    ///  most two character comparisons, so misses are rejected in O(1) with no hashing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetModifierKind(string name, out ModifierKind kind)
    {
        switch (name.Length)
        {
            case 7:
                // RootDir
                if (string.Equals(name, RootDir, StringComparison.OrdinalIgnoreCase))
                {
                    kind = ModifierKind.RootDir;
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
                                    kind = ModifierKind.FullPath;
                                    return true;
                                }

                                break;

                            case 'I' or 'i':
                                if (string.Equals(name, Filename, StringComparison.OrdinalIgnoreCase))
                                {
                                    kind = ModifierKind.Filename;
                                    return true;
                                }

                                break;
                        }

                        break;

                    case 'I' or 'i':
                        if (string.Equals(name, Identity, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ModifierKind.Identity;
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
                            kind = ModifierKind.Extension;
                            return true;
                        }

                        break;

                    case 'D' or 'd':
                        if (string.Equals(name, Directory, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ModifierKind.Directory;
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
                            kind = ModifierKind.RelativeDir;
                            return true;
                        }

                        break;

                    case 'C' or 'c':
                        if (string.Equals(name, CreatedTime, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ModifierKind.CreatedTime;
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
                            kind = ModifierKind.RecursiveDir;
                            return true;
                        }

                        break;

                    case 'M' or 'm':
                        if (string.Equals(name, ModifiedTime, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ModifierKind.ModifiedTime;
                            return true;
                        }

                        break;

                    case 'A' or 'a':
                        if (string.Equals(name, AccessedTime, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ModifierKind.AccessedTime;
                            return true;
                        }

                        break;
                }

                break;

            case 19:
                // DefiningProjectName
                if (string.Equals(name, DefiningProjectName, StringComparison.OrdinalIgnoreCase))
                {
                    kind = ModifierKind.DefiningProjectName;
                    return true;
                }

                break;

            case 23:
                // DefiningProjectFullPath
                if (string.Equals(name, DefiningProjectFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    kind = ModifierKind.DefiningProjectFullPath;
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
                            kind = ModifierKind.DefiningProjectDirectory;
                            return true;
                        }

                        break;

                    case 'E' or 'e':
                        if (string.Equals(name, DefiningProjectExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            kind = ModifierKind.DefiningProjectExtension;
                            return true;
                        }

                        break;
                }

                break;
        }

        kind = default;
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
        && TryGetModifierKind(name, out ModifierKind kind)
        && kind is not ModifierKind.RecursiveDir;

    /// <summary>
    /// Performs path manipulations on the given item-spec as directed.
    /// Does not cache the result.
    /// </summary>
    internal static string GetItemSpecModifier(string? currentDirectory, string itemSpec, string? definingProjectEscaped, string modifier)
    {
        string? dummy = null;
        return GetItemSpecModifier(currentDirectory, itemSpec, definingProjectEscaped, modifier, ref dummy);
    }

    /// <summary>
    /// Performs path manipulations on the given item-spec as directed.
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
    /// 2) All but the file time modifiers could be cached, but it's not worth the space. Only full path is cached, as the others are just string manipulations.
    /// </summary>
    /// <remarks>
    /// Methods of the Path class "normalize" slashes and periods. For example:
    /// 1) successive slashes are combined into 1 slash
    /// 2) trailing periods are discarded
    /// 3) forward slashes are changed to back-slashes
    ///
    /// As a result, we cannot rely on any file-spec that has passed through a Path method to remain the same. We will
    /// therefore not bother preserving slashes and periods when file-specs are transformed.
    ///
    /// Never returns null.
    /// </remarks>
    /// <param name="currentDirectory">The root directory for relative item-specs. When called on the Engine thread, this is the project directory. When called as part of building a task, it is null, indicating that the current directory should be used.</param>
    /// <param name="itemSpec">The item-spec to modify.</param>
    /// <param name="definingProjectEscaped">The path to the project that defined this item (may be null).</param>
    /// <param name="modifier">The modifier to apply to the item-spec.</param>
    /// <param name="fullPath">Full path if any was previously computed, to cache.</param>
    /// <returns>The modified item-spec (can be empty string, but will never be null).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the item-spec is not a path.</exception>
    public static string GetItemSpecModifier(string? currentDirectory, string itemSpec, string? definingProjectEscaped, string modifier, ref string? fullPath)
    {
        FrameworkErrorUtilities.VerifyThrow(itemSpec != null, "Need item-spec to modify.");
        FrameworkErrorUtilities.VerifyThrow(modifier != null, "Need modifier to apply to item-spec.");

        if (TryGetModifierKind(modifier, out ModifierKind modifierKind))
        {
            try
            {
                switch (modifierKind)
                {
                    case ModifierKind.FullPath:
                        return ComputeFullPath(currentDirectory, itemSpec, ref fullPath);

                    case ModifierKind.RootDir:
                        return ComputeRootDir(ComputeFullPath(currentDirectory, itemSpec, ref fullPath));

                    case ModifierKind.Filename:
                        return ComputeFilename(itemSpec);

                    case ModifierKind.Extension:
                        return ComputeExtension(itemSpec);

                    case ModifierKind.RelativeDir:
                        return ComputeRelativeDir(itemSpec);

                    case ModifierKind.Directory:
                        return ComputeDirectory(ComputeFullPath(currentDirectory, itemSpec, ref fullPath));

                    case ModifierKind.RecursiveDir:
                        // only the BuildItem class can compute this modifier -- so leave empty
                        return string.Empty;

                    case ModifierKind.Identity:
                        return itemSpec;

                    case ModifierKind.ModifiedTime:
                        return ComputeModifiedTime(itemSpec);

                    case ModifierKind.CreatedTime:
                        return ComputeCreatedTime(itemSpec);

                    case ModifierKind.AccessedTime:
                        return ComputeAccessedTime(itemSpec);
                }

                // At this point, we know this must be one of the DefiningProject* modifier kinds.
                if (string.IsNullOrEmpty(definingProjectEscaped))
                {
                    // We have nothing to work with, but that's sometimes OK -- so just return String.Empty
                    return string.Empty;
                }

                FrameworkErrorUtilities.VerifyThrow(definingProjectEscaped != null, "How could definingProjectEscaped by null?");

                switch (modifierKind)
                {
                    case ModifierKind.DefiningProjectDirectory:
                        {
                            string definingProjectFullPath = ComputeFullPath(currentDirectory, definingProjectEscaped);

                            // ItemSpecModifiers.Directory does not contain the root directory
                            string rootDir = ComputeRootDir(definingProjectFullPath);
                            string directory = ComputeDirectory(definingProjectFullPath);

                            return Path.Combine(rootDir, directory);
                        }

                    case ModifierKind.DefiningProjectFullPath:
                        return ComputeFullPath(currentDirectory, definingProjectEscaped);

                    case ModifierKind.DefiningProjectName:
                        return ComputeFilename(definingProjectEscaped);

                    case ModifierKind.DefiningProjectExtension:
                        return ComputeExtension(definingProjectEscaped);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                throw new InvalidOperationException(SR.FormatInvalidFilespecForTransform(modifier, itemSpec, e.Message));
            }
        }

        throw new InternalErrorException($"\"{modifier}\" is not a valid item-spec modifier.");
    }

    private static string ComputeFullPath(string? currentDirectory, string itemSpec, ref string? cachedFullPath)
        => cachedFullPath ??= ComputeFullPath(currentDirectory, itemSpec);

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
