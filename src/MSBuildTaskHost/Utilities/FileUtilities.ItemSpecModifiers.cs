// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Build.TaskHost.Resources;

namespace Microsoft.Build.TaskHost.Utilities;

internal static partial class FileUtilities
{
    /// <summary>
    /// Encapsulates the definitions of the item-spec modifiers a.k.a. reserved item metadata.
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
        [
            FullPath,
            RootDir,
            Filename,
            Extension,
            RelativeDir,
            Directory,
            RecursiveDir, // <-- Not derivable.
            Identity,
            ModifiedTime,
            CreatedTime,
            AccessedTime,
            DefiningProjectFullPath,
            DefiningProjectDirectory,
            DefiningProjectName,
            DefiningProjectExtension
        ];

        private enum ItemSpecModifierKind
        {
            FullPath,
            RootDir,
            Filename,
            Extension,
            RelativeDir,
            Directory,
            RecursiveDir, // <-- Not derivable.
            Identity,
            ModifiedTime,
            CreatedTime,
            AccessedTime,
            DefiningProjectFullPath,
            DefiningProjectDirectory,
            DefiningProjectName,
            DefiningProjectExtension
        }

        private static readonly Dictionary<string, ItemSpecModifierKind> s_itemSpecModifierMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { FullPath, ItemSpecModifierKind.FullPath },
            { RootDir, ItemSpecModifierKind.RootDir },
            { Filename, ItemSpecModifierKind.Filename },
            { Extension, ItemSpecModifierKind.Extension },
            { RelativeDir, ItemSpecModifierKind.RelativeDir },
            { Directory, ItemSpecModifierKind.Directory },
            { RecursiveDir, ItemSpecModifierKind.RecursiveDir },
            { Identity, ItemSpecModifierKind.Identity },
            { ModifiedTime, ItemSpecModifierKind.ModifiedTime },
            { CreatedTime, ItemSpecModifierKind.CreatedTime },
            { AccessedTime, ItemSpecModifierKind.AccessedTime },
            { DefiningProjectFullPath, ItemSpecModifierKind.DefiningProjectFullPath },
            { DefiningProjectDirectory, ItemSpecModifierKind.DefiningProjectDirectory },
            { DefiningProjectName, ItemSpecModifierKind.DefiningProjectName },
            { DefiningProjectExtension, ItemSpecModifierKind.DefiningProjectExtension }
        };

        /// <summary>
        /// Indicates if the given name is reserved for an item-spec modifier.
        /// </summary>
        internal static bool IsItemSpecModifier([NotNullWhen(true)] string name)
            => name != null && s_itemSpecModifierMap.ContainsKey(name);

        /// <summary>
        /// Indicates if the given name is reserved for a derivable item-spec modifier.
        /// Derivable means it can be computed given a file name.
        /// </summary>
        /// <param name="name">Name to check.</param>
        /// <returns>true, if name of a derivable modifier</returns>
        internal static bool IsDerivableItemSpecModifier(string name)
        {
            bool isItemSpecModifier = IsItemSpecModifier(name);

            if (isItemSpecModifier && name.Length == 12 && name[0] is 'R' or 'r')
            {
                // The only 12 letter ItemSpecModifier that starts with 'R' is 'RecursiveDir'
                return false;
            }

            return isItemSpecModifier;
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
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Pre-existing")]
        internal static string GetItemSpecModifier(
            string? currentDirectory,
            string itemSpec,
            string? definingProjectEscaped,
            string modifier,
            ref string? fullPath)
        {
            ErrorUtilities.VerifyThrow(itemSpec != null, "Need item-spec to modify.");
            ErrorUtilities.VerifyThrow(modifier != null, "Need modifier to apply to item-spec.");

            try
            {
                if (s_itemSpecModifierMap.TryGetValue(modifier, out ItemSpecModifierKind kind))
                {
                    switch (kind)
                    {
                        case ItemSpecModifierKind.FullPath:
                            ComputeFullPath(itemSpec, currentDirectory, ref fullPath);
                            return fullPath;

                        case ItemSpecModifierKind.RootDir:
                            ComputeFullPath(itemSpec, currentDirectory, ref fullPath);
                            return ComputeRootDir(fullPath);

                        case ItemSpecModifierKind.Filename:
                            return ComputeFileName(itemSpec);

                        case ItemSpecModifierKind.Extension:
                            return ComputeExtension(itemSpec);

                        case ItemSpecModifierKind.RelativeDir:
                            return ComputeRelativeDir(itemSpec);

                        case ItemSpecModifierKind.Directory:
                            ComputeFullPath(itemSpec, currentDirectory, ref fullPath);
                            return ComputeDirectory(fullPath);

                        case ItemSpecModifierKind.RecursiveDir:
                            // only the BuildItem class can compute this modifier -- so leave empty
                            return string.Empty;

                        case ItemSpecModifierKind.Identity:
                            return itemSpec;

                        case ItemSpecModifierKind.ModifiedTime:
                            return ComputeModifiedTime(itemSpec);

                        case ItemSpecModifierKind.CreatedTime:
                            return ComputeCreatedTime(itemSpec);

                        case ItemSpecModifierKind.AccessedTime:
                            return ComputeAccessedTime(itemSpec);

                        case ItemSpecModifierKind.DefiningProjectDirectory:
                        case ItemSpecModifierKind.DefiningProjectFullPath:
                        case ItemSpecModifierKind.DefiningProjectName:
                        case ItemSpecModifierKind.DefiningProjectExtension:
                            if (string.IsNullOrEmpty(definingProjectEscaped))
                            {
                                // We have nothing to work with, but that's sometimes OK -- so just return String.Empty
                                return string.Empty;
                            }

                            ErrorUtilities.VerifyThrow(definingProjectEscaped != null, $"{nameof(definingProjectEscaped)} is null.");

                            switch (kind)
                            {
                                case ItemSpecModifierKind.DefiningProjectDirectory:
                                    return ComputeDefiningProjectDirectory(definingProjectEscaped, currentDirectory);

                                case ItemSpecModifierKind.DefiningProjectFullPath:
                                    return ComputeDefiningProjectFullPath(definingProjectEscaped, currentDirectory);

                                case ItemSpecModifierKind.DefiningProjectName:
                                    return ComputeDefiningProjectFileName(definingProjectEscaped);

                                case ItemSpecModifierKind.DefiningProjectExtension:
                                    return ComputeDefiningProjectExtension(definingProjectEscaped);
                            }

                            break;
                    }
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                ErrorUtilities.ThrowInvalidOperation(SR.Shared_InvalidFilespecForTransform, modifier, itemSpec, e.Message);
            }

            ErrorUtilities.ThrowInternalError($"\"{modifier}\" is not a valid item-spec modifier.");
            return null;
        }

        private static string ComputeFullPath(string itemSpec, string? currentDirectory)
        {
            currentDirectory ??= string.Empty;
            string fullPath = GetFullPath(itemSpec, currentDirectory);

            ThrowForUrl(fullPath, itemSpec, currentDirectory);

            return fullPath;
        }

        private static void ComputeFullPath(string itemSpec, string? currentDirectory, [NotNull] ref string? existingFullPath)
        {
            if (existingFullPath != null)
            {
                return;
            }

            existingFullPath = ComputeFullPath(itemSpec, currentDirectory);
        }

        private static string ComputeRootDir(string fullPath)
        {
            string rootDir = Path.GetPathRoot(fullPath);

            if (!EndsWithSlash(rootDir))
            {
                ErrorUtilities.VerifyThrow(
                    StartsWithUncPattern(rootDir),
                    "Only UNC shares should be missing trailing slashes.");

                // restore/append trailing slash if Path.GetPathRoot() has either removed it, or failed to add it
                // (this happens with UNC shares)
                rootDir += Path.DirectorySeparatorChar;
            }

            return rootDir;
        }

        private static string ComputeFileName(string itemSpec)
             // if the item-spec is a root directory, it can have no filename
             // NOTE: this is to prevent Path.GetFileNameWithoutExtension() from treating server and share elements
             // in a UNC file-spec as filenames e.g. \\server, \\server\share
             => IsRootDirectory(itemSpec)
                ? string.Empty
                : Path.GetFileNameWithoutExtension(itemSpec);

        private static string ComputeExtension(string itemSpec)
             // if the item-spec is a root directory, it can have no extension
             // NOTE: this is to prevent Path.GetFileNameWithoutExtension() from treating server and share elements
             // in a UNC file-spec as filenames e.g. \\server, \\server\share
             => IsRootDirectory(itemSpec)
                ? string.Empty
                : Path.GetExtension(itemSpec);

        private static string ComputeRelativeDir(string itemSpec)
            => GetDirectory(itemSpec);

        private static string ComputeDirectory(string fullPath)
        {
            string directory = GetDirectory(fullPath);

            int length = StartsWithDrivePattern(directory)
                ? 2
                : StartsWithUncPatternMatchLength(directory);

            if (length != -1)
            {
                ErrorUtilities.VerifyThrow(
                    directory.Length > length && IsAnySlash(directory[length]),
                    "Root directory must have a trailing slash.");

                directory = directory.Substring(length + 1);
            }

            return directory;
        }

        private static string ComputeModifiedTime(string itemSpec)
        {
            // About to go out to the filesystem.  This means data is leaving the engine, so need to unescape first.
            string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

            return NativeMethods.FileExists(unescapedItemSpec)
                ? File.GetLastWriteTime(unescapedItemSpec).ToString(FileTimeFormat, provider: null)
                : string.Empty; // File does not exist, or path is a directory
        }

        private static string ComputeCreatedTime(string itemSpec)
        {
            // About to go out to the filesystem.  This means data is leaving the engine, so need to unescape first.
            string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

            return NativeMethods.FileExists(unescapedItemSpec)
                ? File.GetCreationTime(unescapedItemSpec).ToString(FileTimeFormat, provider: null)
                : string.Empty; // File does not exist, or path is a directory
        }

        private static string ComputeAccessedTime(string itemSpec)
        {
            // About to go out to the filesystem.  This means data is leaving the engine, so need to unescape first.
            string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

            return NativeMethods.FileExists(unescapedItemSpec)
                ? File.GetLastAccessTime(unescapedItemSpec).ToString(FileTimeFormat, provider: null)
                : string.Empty; // File does not exist, or path is a directory
        }

        private static string ComputeDefiningProjectDirectory(string itemSpec, string? currentDirectory)
        {
            string fullPath = ComputeFullPath(itemSpec, currentDirectory);
            string rootDir = ComputeRootDir(fullPath);
            string directory = ComputeDirectory(fullPath);

            return Path.Combine(rootDir, directory);
        }

        private static string ComputeDefiningProjectFullPath(string itemSpec, string? currentDirectory)
            => ComputeFullPath(itemSpec, currentDirectory);

        private static string ComputeDefiningProjectFileName(string itemSpec)
            => ComputeFileName(itemSpec);

        private static string ComputeDefiningProjectExtension(string itemSpec)
            => ComputeExtension(itemSpec);

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

            int uncMatchLength = StartsWithUncPatternMatchLength(path);

            // Determine if the given path is a standard drive/unc pattern root
            if (IsDrivePattern(path) ||
                IsDrivePatternWithSlash(path) ||
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
            if (StartsWithDrivePattern(path) &&
                ((path.Length >= 3 && path[2] is not (BackSlash or ForwardSlash)) ||
                 (path.Length >= 4 && path[3] is not (BackSlash or ForwardSlash))))
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
}
