// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// PERF\COVERAGE NOTE: Try to keep classes in 'shared' as granular as possible. All the methods in 
    /// each class get pulled into the resulting assembly.
    /// </summary>
    /// <owner>SumedhK</owner>
    static internal class FileUtilities
    {
        #region Item-spec modifiers

        /// <summary>
        /// Encapsulates the definitions of the item-spec modifiers a.k.a. reserved item metadata.
        /// </summary>
        /// <owner>SumedhK</owner>
        static internal class ItemSpecModifiers
        {
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
                    AccessedTime
                };

            private static Hashtable tableOfItemSpecModifiers;

            internal static Hashtable TableOfItemSpecModifiers
            {
                get
                {
                    if (ItemSpecModifiers.tableOfItemSpecModifiers == null)
                    {
                        ItemSpecModifiers.tableOfItemSpecModifiers = new Hashtable(ItemSpecModifiers.All.Length, StringComparer.OrdinalIgnoreCase);

                        // Populate the hashtable with all the valid item spec modifiers, for fast lookup.
                        // The key in the hashtable is the name of the item spec modifiers.  The value
                        // is always null.
                        foreach (string itemSpecModifier in ItemSpecModifiers.All)
                        {
                            ItemSpecModifiers.tableOfItemSpecModifiers[itemSpecModifier] = String.Empty;
                        }
                    }

                    return ItemSpecModifiers.tableOfItemSpecModifiers;
                }
            }
        }

        #endregion

        /// <summary>
        /// Indicates if the given name is reserved for an item-spec modifier.
        /// </summary>
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
                            if (name == ItemSpecModifiers.RootDir)
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
                            if (name == ItemSpecModifiers.FullPath)
                            {
                                return true;
                            }
                            if (name == ItemSpecModifiers.Filename)
                            {
                                return true;
                            }
                            break;
                        case 'f':
                            break;
                        case 'I': // Identity
                            if (name == ItemSpecModifiers.Identity)
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
                            if (name == ItemSpecModifiers.Directory)
                            {
                                return true;
                            }
                            break;
                        case 'd':        
                            break;
                        case 'E': // Extension
                            if (name == ItemSpecModifiers.Extension)
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
                            if (name == ItemSpecModifiers.CreatedTime)
                            {
                                return true;
                            }
                            break;
                        case 'c':
                            break;
                        case 'R': // RelativeDir
                            if (name == ItemSpecModifiers.RelativeDir)
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
                            if (name == ItemSpecModifiers.AccessedTime)
                            {
                                return true;
                            }
                            break;
                        case 'a':        
                            break;
                        case 'M': // ModifiedTime
                            if (name == ItemSpecModifiers.ModifiedTime)
                            {
                                return true;
                            }
                            break;
                        case 'm':        
                            break;
                        case 'R': // RecursiveDir
                            if (name == ItemSpecModifiers.RecursiveDir)
                            {
                                return true;
                            }
                            break;
                        case 'r':        
                            break;
                    }
                    break;
                default:
                    // Not the right length for a match.
                    return false;
            }
                   

            // Could still be a case-insensitive match.
            bool result = ItemSpecModifiers.TableOfItemSpecModifiers.ContainsKey(name);
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
        /// 2) The %(ModifiedTime), %(CreatedTime) and %(AccessedTime) modifiers are never cached because they are not constants.
        /// </summary>
        /// <remarks>
        /// Methods of the Path class "normalize" slashes and periods. For example:
        /// 1) successive slashes are combined into 1 slash
        /// 2) trailing periods are discarded
        /// 3) forward slashes are changed to back-slashes
        /// 
        /// As a result, we cannot rely on any file-spec that has passed through a Path method to remain the same. We will
        /// therefore not bother preserving slashes and periods when file-specs are transformed.
        /// </remarks>
        /// <owner>SumedhK</owner>
        /// <param name="currentDirectory">The root directory for relative item-specs. When called on the Engine thread, this is the project directory. When called as part of building a task, it is null, indicating that the current directory should be used.</param>
        /// <param name="itemSpec">The item-spec to modify.</param>
        /// <param name="modifier">The modifier to apply to the item-spec.</param>
        /// <param name="cachedModifiers">Cache of previously computed modifiers (if null, this method will create it unless the modifier cannot be cached).</param>
        /// <returns>The modified item-spec (can be empty string, but will never be null).</returns>
        /// <exception cref="InvalidOperationException">Thrown when the item-spec is not a path.</exception>
        internal static string GetItemSpecModifier(string currentDirectory, string itemSpec, string modifier, ref Hashtable cachedModifiers)
        {
            ErrorUtilities.VerifyThrow(itemSpec != null, "Need item-spec to modify.");
            ErrorUtilities.VerifyThrow(modifier != null, "Need modifier to apply to item-spec.");

            string modifiedItemSpec = null;

            // check if we have computed this modifier before
            if (cachedModifiers != null)
            {
                ErrorUtilities.VerifyThrow((string)cachedModifiers[String.Empty] == itemSpec,
                    "The cache of modifiers is only valid for one item-spec. If the item-spec changes, the cache must be nulled out, or a different cache passed in.");

                modifiedItemSpec = (string)cachedModifiers[modifier];
            }

            if (modifiedItemSpec == null)
            {
                // certain properties can't be cached -- this will be turned to true in those cases
                bool isVolatile = false;

                try
                {
                    if (String.Equals(modifier, ItemSpecModifiers.FullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if(currentDirectory == null)
                        {
                            currentDirectory = String.Empty;
                        }

                        modifiedItemSpec = GetFullPath(itemSpec, currentDirectory);
                    }
                    else if (String.Equals(modifier, ItemSpecModifiers.RootDir, StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentDirectory == null)
                        {
                            currentDirectory = String.Empty;
                        }

                        string fullPath = Path.GetFullPath(Path.Combine(currentDirectory, itemSpec));
                        modifiedItemSpec = Path.GetPathRoot(fullPath);

                        if (!EndsWithSlash(modifiedItemSpec))
                        {
                            Debug.Assert(FileUtilitiesRegex.UNCPattern.IsMatch(modifiedItemSpec),
                                "Only UNC shares should be missing trailing slashes.");

                            // restore/append trailing slash if Path.GetPathRoot() has either removed it, or failed to add it
                            // (this happens with UNC shares)
                            modifiedItemSpec += Path.DirectorySeparatorChar;
                        }
                    }
                    else if (String.Equals(modifier, ItemSpecModifiers.Filename, StringComparison.OrdinalIgnoreCase))
                    {
                        // if the item-spec is a root directory, it can have no filename
                        if (Path.GetDirectoryName(itemSpec) == null)
                        {
                            // NOTE: this is to prevent Path.GetFileNameWithoutExtension() from treating server and share elements
                            // in a UNC file-spec as filenames e.g. \\server, \\server\share
                            modifiedItemSpec = String.Empty;
                        }
                        else
                        {
                            modifiedItemSpec = Path.GetFileNameWithoutExtension(itemSpec);
                        }
                    }
                    else if (String.Equals(modifier, ItemSpecModifiers.Extension, StringComparison.OrdinalIgnoreCase))
                    {
                        // if the item-spec is a root directory, it can have no extension
                        if (Path.GetDirectoryName(itemSpec) == null)
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
                    else if (String.Equals(modifier, ItemSpecModifiers.RelativeDir, StringComparison.OrdinalIgnoreCase))
                    {
                        modifiedItemSpec = GetDirectory(itemSpec);
                    }
                    else if (String.Equals(modifier, ItemSpecModifiers.Directory, StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentDirectory == null)
                        {
                            currentDirectory = String.Empty;
                        }

                        modifiedItemSpec = GetDirectory(GetFullPath(itemSpec, currentDirectory));
                        Match root = FileUtilitiesRegex.DrivePattern.Match(modifiedItemSpec);

                        if (!root.Success)
                        {
                            root = FileUtilitiesRegex.UNCPattern.Match(modifiedItemSpec);
                        }

                        if (root.Success)
                        {
                            ErrorUtilities.VerifyThrow((modifiedItemSpec.Length > root.Length) && IsSlash(modifiedItemSpec[root.Length]),
                                "Root directory must have a trailing slash.");

                            modifiedItemSpec = modifiedItemSpec.Substring(root.Length + 1);
                        }
                    }
                    else if (String.Equals(modifier, ItemSpecModifiers.RecursiveDir, StringComparison.OrdinalIgnoreCase))
                    {
                        // only the BuildItem class can compute this modifier -- so leave empty
                        modifiedItemSpec = String.Empty;
                    }
                    else if (String.Equals(modifier, ItemSpecModifiers.Identity, StringComparison.OrdinalIgnoreCase))
                    {
                        modifiedItemSpec = itemSpec;
                    }
                    else if (String.Equals(modifier, ItemSpecModifiers.ModifiedTime, StringComparison.OrdinalIgnoreCase))
                    {
                        isVolatile = true;

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
                    else if (String.Equals(modifier, ItemSpecModifiers.CreatedTime, StringComparison.OrdinalIgnoreCase))
                    {
                        isVolatile = true;

                        // About to go out to the filesystem.  This means data is leaving the engine, so need
                        // to unescape first.
                        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

                        if (File.Exists(unescapedItemSpec))
                        {
                            modifiedItemSpec = File.GetCreationTime(unescapedItemSpec).ToString(FileTimeFormat, null);
                        }
                        else
                        {
                            // File does not exist, or path is a directory                        
                            modifiedItemSpec = String.Empty;
                        }
                    }
                    else if (String.Equals(modifier, ItemSpecModifiers.AccessedTime, StringComparison.OrdinalIgnoreCase))
                    {
                        isVolatile = true;

                        // About to go out to the filesystem.  This means data is leaving the engine, so need
                        // to unescape first.
                        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

                        if (File.Exists(unescapedItemSpec))
                        {
                            modifiedItemSpec = File.GetLastAccessTime(unescapedItemSpec).ToString(FileTimeFormat, null);
                        }
                        else
                        {
                            // File does not exist, or path is a directory                        
                            modifiedItemSpec = String.Empty;
                        }
                    }
                    else
                    {
                        ErrorUtilities.VerifyThrow(false, "\"{0}\" is not a valid item-spec modifier.", modifier);
                    }
                }
                catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
                {
                    if (ExceptionHandling.NotExpectedException(e))
                        throw;
                    ErrorUtilities.VerifyThrowInvalidOperation(false, "Shared.InvalidFilespecForTransform", modifier, itemSpec, e.Message);
                }

                ErrorUtilities.VerifyThrow(modifiedItemSpec != null, "The item-spec modifier \"{0}\" was not evaluated.", modifier);

                // cache the modifier
                if (!isVolatile)
                {
                    if (cachedModifiers == null)
                    {
                        cachedModifiers = new Hashtable(StringComparer.OrdinalIgnoreCase);
     
                        // mark the cache to indicate the item-spec for which it was created
                        // NOTE: we've intentionally picked a key here that will never conflict with any modifier name -- if we
                        // use the item-spec as the key, it's possible for it to conflict with the name of a modifier
                        cachedModifiers[String.Empty] = itemSpec;
                    }

                    cachedModifiers[modifier] = modifiedItemSpec;
                }
            }

            return modifiedItemSpec;
        }

        /// <summary>
        /// If the given path doesn't have a trailing slash then add one.
        /// </summary>
        /// <param name="fileSpec">The path to check.</param>
        /// <returns>A path with a slash.</returns>
        internal static string EnsureTrailingSlash(string fileSpec)
        {
            if (!EndsWithSlash(fileSpec))
            {
                fileSpec += Path.DirectorySeparatorChar;
            }

            return fileSpec;
        }

        /// <summary>
        /// Ensures the path does not have a leading slash.
        /// </summary>
        internal static string EnsureNoLeadingSlash(string path)
        {
            if (path.Length > 0 && IsSlash(path[0]))
            {
                path = path.Substring(1);
            }

            return path;
        }

        /// <summary>
        /// Ensures the path does not have a trailing slash.
        /// </summary>
        internal static string EnsureNoTrailingSlash(string path)
        {
            if (EndsWithSlash(path))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }

        /// <summary>
        /// Indicates if the given file-spec ends with a slash.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="fileSpec">The file spec.</param>
        /// <returns>true, if file-spec has trailing slash</returns>
        internal static bool EndsWithSlash(string fileSpec)
        {
            return (fileSpec.Length > 0)
                ? IsSlash(fileSpec[fileSpec.Length - 1])
                : false;
        }

        /// <summary>
        /// Indicates if the given character is a slash. 
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="c"></param>
        /// <returns>true, if slash</returns>
        internal static bool IsSlash(char c)
        {
            return (c == Path.DirectorySeparatorChar) || (c == Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Trims the string and removes any double quotes around it.
        /// </summary>
        internal static string TrimAndStripAnyQuotes(string path)
        {
            // Trim returns the same string if trimming isn't needed
            path = path.Trim();
            path = path.Trim(new char[] { '"' });

            return path;
        }

        /// <summary>
        /// Determines the full path for the given file-spec.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="fileSpec">The file spec to get the full path of.</param>
        /// <param name="currentDirectory"></param>
        /// <returns>full path</returns>
        private static string GetFullPath(string fileSpec, string currentDirectory)
        {
            // Sending data out of the engine into the filesystem, so time to unescape.
            fileSpec = EscapingUtilities.UnescapeAll(fileSpec);

            // Data coming back from the filesystem into the engine, so time to escape it back.
            string fullPath = EscapingUtilities.Escape(Path.GetFullPath(Path.Combine(currentDirectory, fileSpec)));

            if (!EndsWithSlash(fullPath))
            {
                Match drive = FileUtilitiesRegex.DrivePattern.Match(fileSpec);
                Match UNCShare = FileUtilitiesRegex.UNCPattern.Match(fullPath);

                if ((drive.Success && (drive.Length == fileSpec.Length)) ||
                    (UNCShare.Success && (UNCShare.Length == fullPath.Length)))
                {
                    // append trailing slash if Path.GetFullPath failed to (this happens with drive-specs and UNC shares)
                    fullPath += Path.DirectorySeparatorChar;
                }
            }

            return fullPath;
        }

        /// <summary>
        /// Extracts the directory from the given file-spec.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="fileSpec">The filespec.</param>
        /// <returns>directory path</returns>
        internal static string GetDirectory(string fileSpec)
        {
            string directory = Path.GetDirectoryName(fileSpec);

            // if file-spec is a root directory e.g. c:, c:\, \, \\server\share
            // NOTE: Path.GetDirectoryName also treats invalid UNC file-specs as root directories e.g. \\, \\server
            if (directory == null)
            {
                // just use the file-spec as-is
                directory = fileSpec;
            }
            else if ((directory.Length > 0) && !EndsWithSlash(directory))
            {
                // restore trailing slash if Path.GetDirectoryName has removed it (this happens with non-root directories)
                directory += Path.DirectorySeparatorChar;
            }

            return directory;
        }

        /// <summary>
        /// Determines whether the given assembly file name has one of the listed extensions.
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <param name="allowedExtensions">Array of extensions to consider.</param>
        /// <returns></returns>
        internal static bool HasExtension(string fileName, string[] allowedExtensions)
        {
            string fileExtension = Path.GetExtension(fileName);
            foreach (string extension in allowedExtensions)
            {
                if (String.Compare(fileExtension, extension, true /* ignore case */, CultureInfo.CurrentCulture) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        // ISO 8601 Universal time with sortable format
        internal const string FileTimeFormat = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fffffff";

        /// <summary>
        /// Cached path to the current exe
        /// </summary>
        private static string executablePath;

        /// <summary>
        /// Full path to the current exe (for example, msbuild.exe) including the file name
        /// </summary>
        internal static string CurrentExecutablePath
        {
            get
            {
                if (executablePath == null)
                {
                    StringBuilder sb = new StringBuilder(NativeMethods.MAX_PATH);
                    NativeMethods.GetModuleFileName(NativeMethods.NullHandleRef, sb, sb.Capacity);
                    executablePath = sb.ToString();
                }

                return executablePath;
            }
        }

        /// <summary>
        /// Full path to the directory that the current exe (for example, msbuild.exe) is located in
        /// </summary>
        internal static string CurrentExecutableDirectory
        {
            get
            {
                return Path.GetDirectoryName(CurrentExecutablePath);
            }
        }

        /// <summary>
        /// Full path to the current config file (for example, msbuild.exe.config)
        /// </summary>
        internal static string CurrentExecutableConfigurationFilePath
        {
            get
            {
                return String.Concat(CurrentExecutablePath, ".config");
            }
        }

        /// <summary>
        /// Gets a file info object for the specified file path. If the file path
        /// is invalid, or is a directory, or cannot be accessed, or does not exist,
        /// it returns null rather than throwing or returning a FileInfo around a non-existent file. 
        /// This allows it to be called where File.Exists() (which never throws, and returns false
        /// for directories) was called - but with the advantage that a FileInfo object is returned
        /// that can be queried (e.g., for LastWriteTime) without hitting the disk again.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>FileInfo around path if it is an existing /file/, else null</returns>
        internal static FileInfo GetFileInfoNoThrow(string filePath)
        {
            FileInfo fileInfo;

            try
            {
                fileInfo = new FileInfo(filePath);
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedException(e))
                    throw;

                // Invalid or inaccessible path: treat as if nonexistent file, just as File.Exists does
                return null;
            }

            if (fileInfo.Exists)
            {
                // It's an existing file
                return fileInfo;
            }
            else
            {
                // Nonexistent, or existing but a directory, just as File.Exists behaves
                return null;
            }
        }

        /// <summary>
        /// Gets the current directory using a static buffer to cut down on allocations and permission checking.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        internal static string GetCurrentDirectoryStaticBuffer(StringBuilder buffer)
        {
            if (NativeMethods.GetCurrentDirectory(buffer.Capacity, buffer) == 0)
            {
                throw new System.ComponentModel.Win32Exception();
            }

            return buffer.ToString();
        }

        /// <summary>
        /// Returns true if the specified filename is a VC++ project file, otherwise returns false
        /// </summary>
        internal static bool IsVCProjFilename(string filename)
        {
            return String.Equals(Path.GetExtension(filename), ".vcproj", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Given the absolute location of a file, and a disc location, returns relative file path to that disk location. 
        /// Throws UriFormatException.
        /// </summary>
        /// <param name="basePath">
        /// The base path we want to relativize to. Must be absolute.  
        /// Should <i>not</i> include a filename as the last segment will be interpreted as a directory.
        /// </param>
        /// <param name="path">
        /// The path we need to make relative to basePath.  The path can be either absolute path or a relative path in which case it is relative to the base path.
        /// If the path cannot be made relative to the base path (for example, it is on another drive), it is returned verbatim.
        /// If the basePath is an empty string, returns the path.
        /// </param>
        /// <returns>relative path (can be the full path)</returns>
        internal static string MakeRelative(string basePath, string path)
        {
            ErrorUtilities.VerifyThrowArgumentNull(basePath, nameof(basePath));
            ErrorUtilities.VerifyThrowArgumentLength(path, nameof(path));

            if (basePath.Length == 0)
            {
                return path;
            }

            Uri baseUri = new Uri(FileUtilities.EnsureTrailingSlash(basePath), UriKind.Absolute); // May throw UriFormatException

            Uri pathUri = CreateUriFromPath(path);

            if (!pathUri.IsAbsoluteUri)
            {
                // the path is already a relative url, we will just normalize it...
                pathUri = new Uri(baseUri, pathUri);
            }

            Uri relativeUri = baseUri.MakeRelativeUri(pathUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.IsAbsoluteUri ? relativeUri.LocalPath : relativeUri.ToString());

            string result = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            return result;
        }

        /// <summary>
        /// Helper function to create an Uri object from path.
        /// </summary>
        /// <param name="path">path string</param>
        /// <returns>uri object</returns>
        private static Uri CreateUriFromPath(string path)
        {
            ErrorUtilities.VerifyThrowArgumentLength(path, nameof(path));

            Uri pathUri;

            // Try absolute first, then fall back on relative, otherwise it
            // makes some absolute UNC paths like (\\foo\bar) relative ...
            if (!Uri.TryCreate(path, UriKind.Absolute, out pathUri))
            {
                pathUri = new Uri(path, UriKind.Relative);
            }

            return pathUri;
        }
    }
}
