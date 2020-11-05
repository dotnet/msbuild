// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// </summary>
    /// <comment>
    /// Partial class in order to reduce the amount of sharing into different assemblies
    /// </comment>
    static internal partial class FileUtilities
    {
        /// <summary>
        /// Encapsulates the definitions of the item-spec modifiers a.k.a. reserved item metadata.
        /// </summary>
        static internal class ItemSpecModifiers
        {
#if DEBUG
            /// <summary>
            /// Whether to dump when a modifier is in the "wrong" (slow) casing
            /// </summary>       
            private static readonly bool s_traceModifierCasing = (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDTRACEMODIFIERCASING")));
#endif

            // NOTE: If you add an item here that starts with a new letter, you need to update the case 
            // statements in IsItemSpecModifier and IsDerivableItemSpecModifier.
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

            private static HashSet<string> s_tableOfItemSpecModifiers = new HashSet<string>(All, StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Indicates if the given name is reserved for an item-spec modifier.
            /// </summary>
            [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Performance")]
            internal static bool IsItemSpecModifier(string name)
            {
                if (name == null)
                {
                    return false;
                }


                /* 
                 * What follows requires some explanation.
                 * 
                 * This function is called many times and slowness here will be amplified 
                 * in critical performance scenarios.
                 * 
                 * The following switch statement attempts to identify item spec modifiers that
                 * have the exact case that our constants in ItemSpecModifiers have. This is the 
                 * 99% case.
                 * 
                 * Further, the switch statement can identify certain cases in which there is
                 * definitely no chance that 'name' is an item spec modifier. For example, a
                 * 7 letter 'name' that doesn't start with 'r' or 'R' can't be RootDir and
                 * therefore is not an item spec modifier.
                 * 
                 */
                switch (name.Length)
                {
                    case 7: // RootDir
                        switch (name[0])
                        {
                            default:
                                return false;
                            case 'R': // RootDir
                                if (name == FileUtilities.ItemSpecModifiers.RootDir)
                                {
                                    return true;
                                }
                                break;
                            case 'r':
                                break;
                        }
                        break;
                    case 8: // FullPath, Filename, Identity

                        switch (name[0])
                        {
                            default:
                                return false;
                            case 'F': // Filename, FullPath
                                if (name == FileUtilities.ItemSpecModifiers.FullPath)
                                {
                                    return true;
                                }
                                if (name == FileUtilities.ItemSpecModifiers.Filename)
                                {
                                    return true;
                                }
                                break;
                            case 'f':
                                break;
                            case 'I': // Identity
                                if (name == FileUtilities.ItemSpecModifiers.Identity)
                                {
                                    return true;
                                }
                                break;
                            case 'i':
                                break;
                        }
                        break;
                    case 9: // Extension, Directory
                        switch (name[0])
                        {
                            default:
                                return false;
                            case 'D': // Directory
                                if (name == FileUtilities.ItemSpecModifiers.Directory)
                                {
                                    return true;
                                }
                                break;
                            case 'd':
                                break;
                            case 'E': // Extension
                                if (name == FileUtilities.ItemSpecModifiers.Extension)
                                {
                                    return true;
                                }
                                break;
                            case 'e':
                                break;
                        }
                        break;
                    case 11: // RelativeDir, CreatedTime
                        switch (name[0])
                        {
                            default:
                                return false;
                            case 'C': // CreatedTime
                                if (name == FileUtilities.ItemSpecModifiers.CreatedTime)
                                {
                                    return true;
                                }
                                break;
                            case 'c':
                                break;
                            case 'R': // RelativeDir
                                if (name == FileUtilities.ItemSpecModifiers.RelativeDir)
                                {
                                    return true;
                                }
                                break;
                            case 'r':
                                break;
                        }
                        break;
                    case 12: // RecursiveDir, ModifiedTime, AccessedTime

                        switch (name[0])
                        {
                            default:
                                return false;
                            case 'A': // AccessedTime
                                if (name == FileUtilities.ItemSpecModifiers.AccessedTime)
                                {
                                    return true;
                                }
                                break;
                            case 'a':
                                break;
                            case 'M': // ModifiedTime
                                if (name == FileUtilities.ItemSpecModifiers.ModifiedTime)
                                {
                                    return true;
                                }
                                break;
                            case 'm':
                                break;
                            case 'R': // RecursiveDir
                                if (name == FileUtilities.ItemSpecModifiers.RecursiveDir)
                                {
                                    return true;
                                }
                                break;
                            case 'r':
                                break;
                        }
                        break;
                    case 19:
                    case 23:
                    case 24:
                        return IsDefiningProjectModifier(name);
                    default:
                        // Not the right length for a match.
                        return false;
                }

                // Could still be a case-insensitive match.
                bool result = s_tableOfItemSpecModifiers.Contains(name);

#if DEBUG
                if (result && s_traceModifierCasing)
                {
                    Console.WriteLine("'{0}' is a non-standard casing. Replace the use with the standard casing like 'RecursiveDir' or 'FullPath' for a small performance improvement.", name);
                }
#endif

                return result;
            }

            /// <summary>
            /// Indicates if the given name is reserved for one of the specific subset of itemspec 
            /// modifiers to do with the defining project of the item. 
            /// </summary>
            internal static bool IsDefiningProjectModifier(string name)
            {
                switch (name.Length)
                {
                    case 19: // DefiningProjectName
                        if (name == FileUtilities.ItemSpecModifiers.DefiningProjectName)
                        {
                            return true;
                        }
                        break;
                    case 23: // DefiningProjectFullPath
                        if (name == FileUtilities.ItemSpecModifiers.DefiningProjectFullPath)
                        {
                            return true;
                        }
                        break;
                    case 24: // DefiningProjectDirectory, DefiningProjectExtension

                        switch (name[15])
                        {
                            default:
                                return false;
                            case 'D': // DefiningProjectDirectory
                                if (name == FileUtilities.ItemSpecModifiers.DefiningProjectDirectory)
                                {
                                    return true;
                                }
                                break;
                            case 'd':
                                break;
                            case 'E': // DefiningProjectExtension
                                if (name == FileUtilities.ItemSpecModifiers.DefiningProjectExtension)
                                {
                                    return true;
                                }
                                break;
                            case 'e':
                                break;
                        }
                        break;
                    default:
                        return false;
                }

                // Could still be a case-insensitive match.
                bool result = s_tableOfItemSpecModifiers.Contains(name);

#if DEBUG
                if (result && s_traceModifierCasing)
                {
                    Console.WriteLine("'{0}' is a non-standard casing. Replace the use with the standard casing like 'RecursiveDir' or 'FullPath' for a small performance improvement.", name);
                }
#endif

                return result;
            }

            /// <summary>
            /// Indicates if the given name is reserved for a derivable item-spec modifier.
            /// Derivable means it can be computed given a file name.
            /// </summary>
            /// <param name="name">Name to check.</param>
            /// <returns>true, if name of a derivable modifier</returns>
            internal static bool IsDerivableItemSpecModifier(string name)
            {
                bool isItemSpecModifier = IsItemSpecModifier(name);

                if (isItemSpecModifier)
                {
                    if (name.Length == 12)
                    {
                        if (name[0] == 'R' || name[0] == 'r')
                        {
                            // The only 12 letter ItemSpecModifier that starts with 'R' is 'RecursiveDir' 
                            return false;
                        }
                    }
                }

                return isItemSpecModifier;
            }

            /// <summary>
            /// Performs path manipulations on the given item-spec as directed.
            /// Does not cache the result.
            /// </summary>
            internal static string GetItemSpecModifier(string currentDirectory, string itemSpec, string definingProjectEscaped, string modifier)
            {
                string dummy = null;
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
            [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Pre-existing")]
            internal static string GetItemSpecModifier(string currentDirectory, string itemSpec, string definingProjectEscaped, string modifier, ref string fullPath)
            {
                ErrorUtilities.VerifyThrow(itemSpec != null, "Need item-spec to modify.");
                ErrorUtilities.VerifyThrow(modifier != null, "Need modifier to apply to item-spec.");

                string modifiedItemSpec = null;

                try
                {
                    if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.FullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (fullPath != null)
                        {
                            return fullPath;
                        }

                        if (currentDirectory == null)
                        {
                            currentDirectory = String.Empty;
                        }

                        modifiedItemSpec = GetFullPath(itemSpec, currentDirectory);
                        fullPath = modifiedItemSpec;

                        ThrowForUrl(modifiedItemSpec, itemSpec, currentDirectory);
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.RootDir, StringComparison.OrdinalIgnoreCase))
                    {
                        GetItemSpecModifier(currentDirectory, itemSpec, definingProjectEscaped, ItemSpecModifiers.FullPath, ref fullPath);

                        modifiedItemSpec = Path.GetPathRoot(fullPath);

                        if (!EndsWithSlash(modifiedItemSpec))
                        {
                            ErrorUtilities.VerifyThrow(FileUtilitiesRegex.StartsWithUncPattern(modifiedItemSpec),
                                "Only UNC shares should be missing trailing slashes.");

                            // restore/append trailing slash if Path.GetPathRoot() has either removed it, or failed to add it
                            // (this happens with UNC shares)
                            modifiedItemSpec += Path.DirectorySeparatorChar;
                        }
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.Filename, StringComparison.OrdinalIgnoreCase))
                    {
                        // if the item-spec is a root directory, it can have no filename
                        if (IsRootDirectory(itemSpec))
                        {
                            // NOTE: this is to prevent Path.GetFileNameWithoutExtension() from treating server and share elements
                            // in a UNC file-spec as filenames e.g. \\server, \\server\share
                            modifiedItemSpec = String.Empty;
                        }
                        else
                        {
                            // Fix path to avoid problem with Path.GetFileNameWithoutExtension when backslashes in itemSpec on Unix
                            modifiedItemSpec = Path.GetFileNameWithoutExtension(FixFilePath(itemSpec));
                        }
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.Extension, StringComparison.OrdinalIgnoreCase))
                    {
                        // if the item-spec is a root directory, it can have no extension
                        if (IsRootDirectory(itemSpec))
                        {
                            // NOTE: this is to prevent Path.GetExtension() from treating server and share elements in a UNC
                            // file-spec as filenames e.g. \\server.ext, \\server\share.ext
                            modifiedItemSpec = String.Empty;
                        }
                        else
                        {
                            modifiedItemSpec = Path.GetExtension(itemSpec);
                        }
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.RelativeDir, StringComparison.OrdinalIgnoreCase))
                    {
                        modifiedItemSpec = GetDirectory(itemSpec);
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.Directory, StringComparison.OrdinalIgnoreCase))
                    {
                        GetItemSpecModifier(currentDirectory, itemSpec, definingProjectEscaped, ItemSpecModifiers.FullPath, ref fullPath);

                        modifiedItemSpec = GetDirectory(fullPath);

                        if (NativeMethodsShared.IsWindows)
                        {
                            int length = -1;
                            if (FileUtilitiesRegex.StartsWithDrivePattern(modifiedItemSpec))
                            {
                                length = 2;
                            }
                            else
                            {
                                length = FileUtilitiesRegex.StartsWithUncPatternMatchLength(modifiedItemSpec);
                            }

                            if (length != -1)
                            {
                                ErrorUtilities.VerifyThrow((modifiedItemSpec.Length > length) && IsSlash(modifiedItemSpec[length]),
                                                           "Root directory must have a trailing slash.");

                                modifiedItemSpec = modifiedItemSpec.Substring(length + 1);
                            }
                        }
                        else
                        {
                            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(modifiedItemSpec) && IsSlash(modifiedItemSpec[0]),
                                                       "Expected a full non-windows path rooted at '/'.");

                            // A full unix path is always rooted at
                            // `/`, and a root-relative path is the
                            // rest of the string.
                            modifiedItemSpec = modifiedItemSpec.Substring(1);
                        }
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.RecursiveDir, StringComparison.OrdinalIgnoreCase))
                    {
                        // only the BuildItem class can compute this modifier -- so leave empty
                        modifiedItemSpec = String.Empty;
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.Identity, StringComparison.OrdinalIgnoreCase))
                    {
                        modifiedItemSpec = itemSpec;
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.ModifiedTime, StringComparison.OrdinalIgnoreCase))
                    {
                        // About to go out to the filesystem.  This means data is leaving the engine, so need
                        // to unescape first.
                        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

                        FileInfo info = FileUtilities.GetFileInfoNoThrow(unescapedItemSpec);

                        if (info != null)
                        {
                            modifiedItemSpec = info.LastWriteTime.ToString(FileTimeFormat, null);
                        }
                        else
                        {
                            // File does not exist, or path is a directory
                            modifiedItemSpec = String.Empty;
                        }
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.CreatedTime, StringComparison.OrdinalIgnoreCase))
                    {
                        // About to go out to the filesystem.  This means data is leaving the engine, so need
                        // to unescape first.
                        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

                        if (FileSystems.Default.FileExists(unescapedItemSpec))
                        {
                            modifiedItemSpec = File.GetCreationTime(unescapedItemSpec).ToString(FileTimeFormat, null);
                        }
                        else
                        {
                            // File does not exist, or path is a directory                        
                            modifiedItemSpec = String.Empty;
                        }
                    }
                    else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.AccessedTime, StringComparison.OrdinalIgnoreCase))
                    {
                        // About to go out to the filesystem.  This means data is leaving the engine, so need
                        // to unescape first.
                        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

                        if (FileSystems.Default.FileExists(unescapedItemSpec))
                        {
                            modifiedItemSpec = File.GetLastAccessTime(unescapedItemSpec).ToString(FileTimeFormat, null);
                        }
                        else
                        {
                            // File does not exist, or path is a directory                        
                            modifiedItemSpec = String.Empty;
                        }
                    }
                    else if (IsDefiningProjectModifier(modifier))
                    {
                        if (String.IsNullOrEmpty(definingProjectEscaped))
                        {
                            // We have nothing to work with, but that's sometimes OK -- so just return String.Empty
                            modifiedItemSpec = String.Empty;
                        }
                        else
                        {
                            if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.DefiningProjectDirectory, StringComparison.OrdinalIgnoreCase))
                            {
                                // ItemSpecModifiers.Directory does not contain the root directory
                                modifiedItemSpec = Path.Combine
                                    (
                                        GetItemSpecModifier(currentDirectory, definingProjectEscaped, null, ItemSpecModifiers.RootDir),
                                        GetItemSpecModifier(currentDirectory, definingProjectEscaped, null, ItemSpecModifiers.Directory)
                                    );
                            }
                            else
                            {
                                string additionalModifier = null;

                                if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.DefiningProjectFullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    additionalModifier = ItemSpecModifiers.FullPath;
                                }
                                else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.DefiningProjectName, StringComparison.OrdinalIgnoreCase))
                                {
                                    additionalModifier = ItemSpecModifiers.Filename;
                                }
                                else if (string.Equals(modifier, FileUtilities.ItemSpecModifiers.DefiningProjectExtension, StringComparison.OrdinalIgnoreCase))
                                {
                                    additionalModifier = ItemSpecModifiers.Extension;
                                }
                                else
                                {
                                    ErrorUtilities.ThrowInternalError("\"{0}\" is not a valid item-spec modifier.", modifier);
                                }

                                modifiedItemSpec = GetItemSpecModifier(currentDirectory, definingProjectEscaped, null, additionalModifier);
                            }
                        }
                    }
                    else
                    {
                        ErrorUtilities.ThrowInternalError("\"{0}\" is not a valid item-spec modifier.", modifier);
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    ErrorUtilities.VerifyThrowInvalidOperation(false, "Shared.InvalidFilespecForTransform", modifier, itemSpec, e.Message);
                }

                return modifiedItemSpec;
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
                    fullPath = Path.GetFullPath(Path.Combine(currentDirectory, itemSpec));
                }
            }
        }
    }
}
