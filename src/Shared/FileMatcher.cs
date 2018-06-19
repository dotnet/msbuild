// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Functions for matching file names with patterns.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Functions for matching file names with patterns. 
    /// </summary>
    internal class FileMatcher
    {
        private const string recursiveDirectoryMatch = "**";
        private const string dotdot = "..";

        private static readonly string s_directorySeparator = new string(Path.DirectorySeparatorChar, 1);

        private static readonly string s_thisDirectory = "." + s_directorySeparator;

        private static readonly char[] s_wildcardCharacters = { '*', '?' };
        private static readonly char[] s_wildcardAndSemicolonCharacters = { '*', '?', ';' };

        // on OSX both System.IO.Path separators are '/', so we have to use the literals
        internal static readonly char[] directorySeparatorCharacters = { '/', '\\' };
        internal static readonly string[] directorySeparatorStrings = directorySeparatorCharacters.Select(c => c.ToString()).ToArray();

        // until Cloudbuild switches to EvaluationContext, we need to keep their dependence on global glob caching via an environment variable
        private static readonly Lazy<ConcurrentDictionary<string, ImmutableArray<string>>> s_cachedGlobExpansions = new Lazy<ConcurrentDictionary<string, ImmutableArray<string>>>(() => new ConcurrentDictionary<string, ImmutableArray<string>>(StringComparer.OrdinalIgnoreCase));
        private static readonly Lazy<ConcurrentDictionary<string, object>> s_cachedGlobExpansionsLock = new Lazy<ConcurrentDictionary<string, object>>(() => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));

        private readonly ConcurrentDictionary<string, ImmutableArray<string>> _cachedGlobExpansions;
        private readonly Lazy<ConcurrentDictionary<string, object>> _cachedGlobExpansionsLock = new Lazy<ConcurrentDictionary<string, object>>(() => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));

        /// <summary>
        /// Cache of the list of invalid path characters, because this method returns a clone (for security reasons)
        /// which can cause significant transient allocations
        /// </summary>
        private static readonly char[] s_invalidPathChars = Path.GetInvalidPathChars();

        public const RegexOptions DefaultRegexOptions = RegexOptions.IgnoreCase;

        private readonly GetFileSystemEntries _getFileSystemEntries;
        private readonly DirectoryExists _directoryExists;

        /// <summary>
        /// The Default FileMatcher does not cache directory enumeration.
        /// </summary>
        public static FileMatcher Default = new FileMatcher(FileSystemFactory.GetFileSystem(), null);

        public FileMatcher(IFileSystemAbstraction fileSystem, ConcurrentDictionary<string, ImmutableArray<string>> fileEntryExpansionCache = null) : this(
            (entityType, path, pattern, projectDirectory, stripProjectDirectory) => GetAccessibleFileSystemEntries(
                fileSystem,
                entityType,
                path,
                pattern,
                projectDirectory,
                stripProjectDirectory),
            fileSystem.DirectoryExists,
            fileEntryExpansionCache)
        {
        }

        public FileMatcher(GetFileSystemEntries getFileSystemEntries, DirectoryExists directoryExists, ConcurrentDictionary<string, ImmutableArray<string>> getFileSystemDirectoryEntriesCache = null)
        {
            if (Traits.Instance.MSBuildCacheFileEnumerations)
            {
                _cachedGlobExpansions = s_cachedGlobExpansions.Value;
                _cachedGlobExpansionsLock = s_cachedGlobExpansionsLock;
            }
            else
            {
                _cachedGlobExpansions = getFileSystemDirectoryEntriesCache;
            }

            _getFileSystemEntries = getFileSystemDirectoryEntriesCache == null
                ? getFileSystemEntries
                : (type, path, pattern, directory, projectDirectory) =>
                {
                    // Cache only directories, for files we won't hit the cache because the file name patterns tend to be unique
                    if (type == FileSystemEntity.Directories)
                    {
                        return getFileSystemDirectoryEntriesCache.GetOrAdd(
                            $"{path};{pattern ?? "*"}",
                            s => getFileSystemEntries(
                                type,
                                path,
                                pattern,
                                directory,
                                projectDirectory));
                    }
                    return getFileSystemEntries(type, path, pattern, directory, projectDirectory);
                };

            _directoryExists = directoryExists;
        }

        /// <summary>
        /// The type of entity that GetFileSystemEntries should return.
        /// </summary>
        internal enum FileSystemEntity
        {
            Files,
            Directories,
            FilesAndDirectories
        };

        /// <summary>
        /// Delegate defines the GetFileSystemEntries signature that GetLongPathName uses
        /// to enumerate directories on the file system.
        /// </summary>
        /// <param name="entityType">Files, Directories, or Files and Directories</param>
        /// <param name="path">The path to search.</param>
        /// <param name="pattern">The file pattern.</param>
        /// <param name="projectDirectory"></param>
        /// <param name="stripProjectDirectory"></param>
        /// <returns>An immutable array of filesystem entries.</returns>
        internal delegate ImmutableArray<string> GetFileSystemEntries(FileSystemEntity entityType, string path, string pattern, string projectDirectory, bool stripProjectDirectory);

        internal static void ClearFileEnumerationsCache()
        {
            if (s_cachedGlobExpansions.IsValueCreated)
            {
                s_cachedGlobExpansions.Value.Clear();
            }

            if (s_cachedGlobExpansionsLock.IsValueCreated)
            {
                s_cachedGlobExpansionsLock.Value.Clear();
            }
        }

        /// <summary>
        /// Determines whether the given path has any wild card characters.
        /// </summary>
        /// <param name="filespec"></param>
        /// <returns></returns>
        internal static bool HasWildcards(string filespec)
        {
            return -1 != filespec.IndexOfAny(s_wildcardCharacters);
        }

        /// <summary>
        /// Determines whether the given path has any wild card characters or any semicolons.
        /// </summary>
        internal static bool HasWildcardsSemicolonItemOrPropertyReferences(string filespec)
        {
            return
                (
                (-1 != filespec.IndexOfAny(s_wildcardAndSemicolonCharacters)) ||
                filespec.Contains("$(") ||
                filespec.Contains("@(")
                );
        }

        /// <summary>
        /// Get the files and\or folders specified by the given path and pattern.
        /// </summary>
        /// <param name="entityType">Whether Files, Directories or both.</param>
        /// <param name="path">The path to search.</param>
        /// <param name="pattern">The pattern to search.</param>
        /// <param name="projectDirectory">The directory for the project within which the call is made</param>
        /// <param name="stripProjectDirectory">If true the project directory should be stripped</param>
        /// <param name="fileSystem">The file system abstraction to use that implements file system operations</param>
        /// <returns></returns>
        private static ImmutableArray<string> GetAccessibleFileSystemEntries(IFileSystemAbstraction fileSystem, FileSystemEntity entityType, string path, string pattern, string projectDirectory, bool stripProjectDirectory)
        {
            path = FileUtilities.FixFilePath(path);
            switch (entityType)
            {
                case FileSystemEntity.Files: return GetAccessibleFiles(fileSystem, path, pattern, projectDirectory, stripProjectDirectory);
                case FileSystemEntity.Directories: return GetAccessibleDirectories(fileSystem, path, pattern);
                case FileSystemEntity.FilesAndDirectories: return GetAccessibleFilesAndDirectories(fileSystem,path, pattern);
                default:
                    ErrorUtilities.VerifyThrow(false, "Unexpected filesystem entity type.");
                    break;
            }
            return ImmutableArray<string>.Empty;
        }

        /// <summary>
        /// Returns an immutable array of file system entries matching the specified search criteria. Inaccessible or non-existent file
        /// system entries are skipped.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <param name="fileSystem">The file system abstraction to use that implements file system operations</param>
        /// <returns>An immutable array of matching file system entries (can be empty).</returns>
        private static ImmutableArray<string> GetAccessibleFilesAndDirectories(IFileSystemAbstraction fileSystem, string path, string pattern)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    return (ShouldEnforceMatching(pattern)
                        ? fileSystem.EnumerateFileSystemEntries(path, pattern)
                            .Where(o => IsMatch(Path.GetFileName(o), pattern, true))
                        : fileSystem.EnumerateFileSystemEntries(path, pattern)
                    ).ToImmutableArray();
                }
                // for OS security
                catch (UnauthorizedAccessException)
                {
                    // do nothing
                }
                // for code access security
                catch (System.Security.SecurityException)
                {
                    // do nothing
                }
            }
            return ImmutableArray<string>.Empty;
        }

        /// <summary>
        /// Determine if the given search pattern will match loosely on Windows
        /// </summary>
        /// <param name="searchPattern">The search pattern to check</param>
        /// <returns></returns>
        private static bool ShouldEnforceMatching(string searchPattern)
        {
            if (searchPattern == null)
            {
                return false;
            }
            // https://github.com/Microsoft/msbuild/issues/3060
            // NOTE: Corefx matches loosely in three cases (in the absence of the * wildcard in the extension):
            // 1) if the extension ends with the ? wildcard, it matches files with shorter extensions also e.g. "file.tx?" would
            //    match both "file.txt" and "file.tx"
            // 2) if the extension is three characters, and the filename contains the * wildcard, it matches files with longer
            //    extensions that start with the same three characters e.g. "*.htm" would match both "file.htm" and "file.html"
            // 3) if the ? wildcard is to the left of a period, it matches files with shorter name e.g. ???.txt would match
            //    foo.txt, fo.txt and also f.txt
            return searchPattern.IndexOf("?.", StringComparison.Ordinal) != -1 ||
                   (
                       Path.GetExtension(searchPattern).Length == (3 + 1 /* +1 for the period */) &&
                       searchPattern.IndexOf('*') != -1
                   ) ||
                   searchPattern.EndsWith("?", StringComparison.Ordinal);
        }

        /// <summary>
        /// Same as Directory.EnumerateFiles(...) except that files that
        /// aren't accessible are skipped instead of throwing an exception.
        /// 
        /// Other exceptions are passed through.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="filespec">The pattern.</param>
        /// <param name="projectDirectory">The project directory</param>
        /// <param name="stripProjectDirectory"></param>
        /// <param name="fileSystem">The file system abstraction to use that implements file system operations</param>
        /// <returns>Files that can be accessed.</returns>
        private static ImmutableArray<string> GetAccessibleFiles
        (
            IFileSystemAbstraction fileSystem,
            string path,
            string filespec,     // can be null
            string projectDirectory,
            bool stripProjectDirectory
        )
        {
            try
            {
                // look in current directory if no path specified
                string dir = ((path.Length == 0) ? s_thisDirectory : path);

                // get all files in specified directory, unless a file-spec has been provided
                IEnumerable<string> files;
                if (filespec == null)
                {
                    files = fileSystem.EnumerateFiles(dir);
                }
                else
                {
                    files = fileSystem.EnumerateFiles(dir, filespec);
                    if (ShouldEnforceMatching(filespec))
                    {
                        files = files.Where(o => IsMatch(Path.GetFileName(o), filespec, true));
                    }
                }
                // If the Item is based on a relative path we need to strip
                // the current directory from the front
                if (stripProjectDirectory)
                {
                    files = RemoveProjectDirectory(files, projectDirectory);
                }
                // Files in the current directory are coming back with a ".\"
                // prepended to them.  We need to remove this; it breaks the
                // IDE, which expects just the filename if it is in the current
                // directory.  But only do this if the original path requested
                // didn't itself contain a ".\".
                else if (!path.StartsWith(s_thisDirectory, StringComparison.Ordinal))
                {
                    files = RemoveInitialDotSlash(files);
                }

                return files.ToImmutableArray();
            }
            catch (System.Security.SecurityException)
            {
                // For code access security.
                return ImmutableArray<string>.Empty;
            }
            catch (System.UnauthorizedAccessException)
            {
                // For OS security.
                return ImmutableArray<string>.Empty;
            }
        }

        /// <summary>
        /// Same as Directory.EnumerateDirectories(...) except that files that
        /// aren't accessible are skipped instead of throwing an exception.
        /// 
        /// Other exceptions are passed through.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pattern">Pattern to match</param>
        /// <param name="fileSystem">The file system abstraction to use that implements file system operations</param>
        /// <returns>Accessible directories.</returns>
        private static ImmutableArray<string> GetAccessibleDirectories
        (
            IFileSystemAbstraction fileSystem,
            string path,
            string pattern
        )
        {
            try
            {
                IEnumerable<string> directories = null;

                if (pattern == null)
                {
                    directories = fileSystem.EnumerateDirectories((path.Length == 0) ? s_thisDirectory : path);
                }
                else
                {
                    directories = fileSystem.EnumerateDirectories((path.Length == 0) ? s_thisDirectory : path, pattern);
                    if (ShouldEnforceMatching(pattern))
                    {
                        directories = directories.Where(o => IsMatch(Path.GetFileName(o), pattern, true));
                    }
                }

                // Subdirectories in the current directory are coming back with a ".\"
                // prepended to them.  We need to remove this; it breaks the
                // IDE, which expects just the filename if it is in the current
                // directory.  But only do this if the original path requested
                // didn't itself contain a ".\".
                if (!path.StartsWith(s_thisDirectory, StringComparison.Ordinal))
                {
                    directories = RemoveInitialDotSlash(directories);
                }

                return directories.ToImmutableArray();
            }
            catch (System.Security.SecurityException)
            {
                // For code access security.
                return ImmutableArray<string>.Empty;
            }
            catch (System.UnauthorizedAccessException)
            {
                // For OS security.
                return ImmutableArray<string>.Empty;
            }
        }

        /// <summary>
        /// Given a path name, get its long version.
        /// </summary>
        /// <param name="path">The short path.</param>
        /// <returns>The long path.</returns>
        internal string GetLongPathName
        (
            string path
        )
        {
            return GetLongPathName(path, _getFileSystemEntries);
        }

        /// <summary>
        /// Given a path name, get its long version.
        /// </summary>
        /// <param name="path">The short path.</param>
        /// <param name="getFileSystemEntries">Delegate.</param>
        /// <returns>The long path.</returns>
        internal static string GetLongPathName
        (
            string path,
            GetFileSystemEntries getFileSystemEntries
        )
        {
            if (path.IndexOf("~", StringComparison.Ordinal) == -1)
            {
                // A path with no '~' must not be a short name.
                return path;
            }

            ErrorUtilities.VerifyThrow(!HasWildcards(path),
                "GetLongPathName does not handle wildcards and was passed '{0}'.", path);

            string[] parts = path.Split(directorySeparatorCharacters);
            string pathRoot;
            int startingElement = 0;

            bool isUnc = path.StartsWith(s_directorySeparator + s_directorySeparator, StringComparison.Ordinal);
            if (isUnc)
            {
                pathRoot = s_directorySeparator + s_directorySeparator;
                pathRoot += parts[2];
                pathRoot += s_directorySeparator;
                pathRoot += parts[3];
                pathRoot += s_directorySeparator;
                startingElement = 4;
            }
            else
            {
                // Is it relative?
                if (path.Length > 2 && path[1] == ':')
                {
                    // Not relative
                    pathRoot = parts[0] + s_directorySeparator;
                    startingElement = 1;
                }
                else
                {
                    // Relative
                    pathRoot = String.Empty;
                    startingElement = 0;
                }
            }

            // Build up an array of parts. These elements may be "" if there are
            // extra slashes.
            string[] longParts = new string[parts.Length - startingElement];

            string longPath = pathRoot;
            for (int i = startingElement; i < parts.Length; ++i)
            {
                // If there is a zero-length part, then that means there was an extra slash.
                if (parts[i].Length == 0)
                {
                    longParts[i - startingElement] = String.Empty;
                }
                else
                {
                    if (parts[i].IndexOf("~", StringComparison.Ordinal) == -1)
                    {
                        // If there's no ~, don't hit the disk.
                        longParts[i - startingElement] = parts[i];
                        longPath = Path.Combine(longPath, parts[i]);
                    }
                    else
                    {
                        // getFileSystemEntries(...) returns an empty array if longPath doesn't exist.
                        ImmutableArray<string> entries = getFileSystemEntries(FileSystemEntity.FilesAndDirectories, longPath, parts[i], null, false);

                        if (0 == entries.Length)
                        {
                            // The next part doesn't exist. Therefore, no more of the path will exist.
                            // Just return the rest.
                            for (int j = i; j < parts.Length; ++j)
                            {
                                longParts[j - startingElement] = parts[j];
                            }
                            break;
                        }

                        // Since we know there are no wild cards, this should be length one.
                        ErrorUtilities.VerifyThrow(entries.Length == 1,
                            "Unexpected number of entries ({3}) found when enumerating '{0}' under '{1}'. Original path was '{2}'",
                            parts[i], longPath, path, entries.Length);

                        // Entries[0] contains the full path.
                        longPath = entries[0];

                        // We just want the trailing node.
                        longParts[i - startingElement] = Path.GetFileName(longPath);
                    }
                }
            }

            return pathRoot + String.Join(s_directorySeparator, longParts);
        }

        /// <summary>
        /// Given a filespec, split it into left-most 'fixed' dir part, middle 'wildcard' dir part, and filename part.
        /// The filename part may have wildcard characters in it.
        /// </summary>
        /// <param name="filespec">The filespec to be decomposed.</param>
        /// <param name="fixedDirectoryPart">Receives the fixed directory part.</param>
        /// <param name="wildcardDirectoryPart">The wildcard directory part.</param>
        /// <param name="filenamePart">The filename part.</param>
        internal void SplitFileSpec(
            string filespec,
            out string fixedDirectoryPart,
            out string wildcardDirectoryPart,
            out string filenamePart)
        {
            PreprocessFileSpecForSplitting
            (
                filespec,
                out fixedDirectoryPart,
                out wildcardDirectoryPart,
                out filenamePart
            );

            /* 
             * Handle the special case in which filenamePart is '**'.
             * In this case, filenamePart becomes '*.*' and the '**' is appended
             * to the end of the wildcardDirectory part.
             * This is so that later regular expression matching can accurately
             * pull out the different parts (fixed, wildcard, filename) of given
             * file specs.
             */
            if (recursiveDirectoryMatch == filenamePart)
            {
                wildcardDirectoryPart += recursiveDirectoryMatch;
                wildcardDirectoryPart += s_directorySeparator;
                filenamePart = "*.*";
            }

            fixedDirectoryPart = FileMatcher.GetLongPathName(fixedDirectoryPart, _getFileSystemEntries);
        }

        /// <summary>
        /// Do most of the grunt work of splitting the filespec into parts.
        /// Does not handle post-processing common to the different matching
        /// paths.
        /// </summary>
        /// <param name="filespec">The filespec to be decomposed.</param>
        /// <param name="fixedDirectoryPart">Receives the fixed directory part.</param>
        /// <param name="wildcardDirectoryPart">The wildcard directory part.</param>
        /// <param name="filenamePart">The filename part.</param>
        private static void PreprocessFileSpecForSplitting
        (
            string filespec,
            out string fixedDirectoryPart,
            out string wildcardDirectoryPart,
            out string filenamePart
        )
        {
            filespec = FileUtilities.FixFilePath(filespec);
            int indexOfLastDirectorySeparator = filespec.LastIndexOfAny(directorySeparatorCharacters);
            if (-1 == indexOfLastDirectorySeparator)
            {
                /*
                 * No dir separator found. This is either this form,
                 * 
                 *      Source.cs
                 *      *.cs
                 * 
                 *  or this form,
                 * 
                 *     **
                 */
                fixedDirectoryPart = String.Empty;
                wildcardDirectoryPart = String.Empty;
                filenamePart = filespec;
                return;
            }

            int indexOfFirstWildcard = filespec.IndexOfAny(s_wildcardCharacters);
            if
            (
                -1 == indexOfFirstWildcard
                || indexOfFirstWildcard > indexOfLastDirectorySeparator
            )
            {
                /*
                 * There is at least one dir separator, but either there is no wild card or the
                 * wildcard is after the dir separator.
                 *
                 * The form is one of these:
                 * 
                 *      dir1\Source.cs
                 *      dir1\*.cs
                 * 
                 * Where the trailing spec is meant to be a filename. Or,
                 * 
                 *      dir1\**
                 * 
                 * Where the trailing spec is meant to be any file recursively.
                 */

                // We know the fixed director part now.
                fixedDirectoryPart = filespec.Substring(0, indexOfLastDirectorySeparator + 1);
                wildcardDirectoryPart = String.Empty;
                filenamePart = filespec.Substring(indexOfLastDirectorySeparator + 1);
                return;
            }

            /*
             * Find the separator right before the first wildcard.
             */
            string filespecLeftOfWildcard = filespec.Substring(0, indexOfFirstWildcard);
            int indexOfSeparatorBeforeWildCard = filespecLeftOfWildcard.LastIndexOfAny(directorySeparatorCharacters);
            if (-1 == indexOfSeparatorBeforeWildCard)
            {
                /*
                 * There is no separator before the wildcard, so the form is like this:
                 * 
                 *      dir?\Source.cs
                 * 
                 * or this,
                 * 
                 *      dir?\**
                 */
                fixedDirectoryPart = String.Empty;
                wildcardDirectoryPart = filespec.Substring(0, indexOfLastDirectorySeparator + 1);
                filenamePart = filespec.Substring(indexOfLastDirectorySeparator + 1);
                return;
            }

            /*
             * There is at least one wildcard and one dir separator, split parts out.
             */
            fixedDirectoryPart = filespec.Substring(0, indexOfSeparatorBeforeWildCard + 1);
            wildcardDirectoryPart = filespec.Substring(indexOfSeparatorBeforeWildCard + 1, indexOfLastDirectorySeparator - indexOfSeparatorBeforeWildCard);
            filenamePart = filespec.Substring(indexOfLastDirectorySeparator + 1);
        }

        /// <summary>
        /// Removes the leading ".\" from all of the paths in the array. 
        /// </summary>
        /// <param name="paths">Paths to remove .\ from.</param>
        private static IEnumerable<string> RemoveInitialDotSlash
        (
            IEnumerable<string> paths
        )
        {
            foreach (string path in paths)
            {
                if (path.StartsWith(s_thisDirectory, StringComparison.Ordinal))
                {
                    yield return path.Substring(2);
                }
                else
                {
                    yield return path;
                }
            }
        }


        /// <summary>
        /// Checks if the char is a DirectorySeparatorChar or a AltDirectorySeparatorChar
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        internal static bool IsDirectorySeparator(char c)
        {
            return (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
        }
        /// <summary>
        /// Removes the current directory converting the file back to relative path 
        /// </summary>
        /// <param name="paths">Paths to remove current directory from.</param>
        /// <param name="projectDirectory"></param>
        internal static IEnumerable<string> RemoveProjectDirectory
        (
            IEnumerable<string> paths,
            string projectDirectory
        )
        {
            bool directoryLastCharIsSeparator = IsDirectorySeparator(projectDirectory[projectDirectory.Length - 1]);
            foreach (string path in paths)
            {
                if (path.StartsWith(projectDirectory, StringComparison.Ordinal))
                {
                    // If the project directory did not end in a slash we need to check to see if the next char in the path is a slash
                    if (!directoryLastCharIsSeparator)
                    {
                        //If the next char after the project directory is not a slash, skip this path
                        if (path.Length <= projectDirectory.Length || !IsDirectorySeparator(path[projectDirectory.Length]))
                        {
                            yield return path;
                            continue;
                        }
                        yield return path.Substring(projectDirectory.Length + 1);
                    }
                    else
                    {
                        yield return path.Substring(projectDirectory.Length);
                    }
                }
                else
                {
                    yield return path;
                }
            }
        }

        struct RecursiveStepResult
        {
            public string RemainingWildcardDirectory;
            public bool ConsiderFiles;
            public bool NeedsToProcessEachFile;
            public string DirectoryPattern;
            public bool NeedsDirectoryRecursion;
        }

        class FilesSearchData
        {
            public FilesSearchData(
                string filespec,                // can be null
                Regex regexFileMatch,           // can be null
                bool needsRecursion
                )
            {
                Filespec = filespec;
                RegexFileMatch = regexFileMatch;
                NeedsRecursion = needsRecursion;
            }

            /// <summary>
            /// The filespec.
            /// </summary>
            public string Filespec { get; }
            /// <summary>
            /// Wild-card matching.
            /// </summary>
            public Regex RegexFileMatch { get; }
            /// <summary>
            /// If true, then recursion is required.
            /// </summary>
            public bool NeedsRecursion { get; }
        }

        struct RecursionState
        {
            /// <summary>
            /// The directory to search in
            /// </summary>
            public string BaseDirectory;
            /// <summary>
            /// The remaining, wildcard part of the directory.
            /// </summary>
            public string RemainingWildcardDirectory;
            /// <summary>
            /// Data about a search that does not change as the search recursively traverses directories
            /// </summary>
            public FilesSearchData SearchData;
        }

        /// <summary>
        /// Get all files that match either the file-spec or the regular expression. 
        /// </summary>
        /// <param name="listOfFiles">List of files that gets populated.</param>
        /// <param name="recursionState">Information about the search</param>
        /// <param name="projectDirectory"></param>
        /// <param name="stripProjectDirectory"></param>
        /// <param name="searchesToExclude">Patterns to exclude from the results</param>
        /// <param name="searchesToExcludeInSubdirs">exclude patterns that might activate farther down the directory tree. Keys assume paths are normalized with forward slashes and no trailing slashes</param>
        /// <param name="taskOptions">Options for tuning the parallelization of subdirectories</param>
        private void GetFilesRecursive(
            ConcurrentStack<List<string>> listOfFiles,
            RecursionState recursionState,
            string projectDirectory,
            bool stripProjectDirectory,
            IList<RecursionState> searchesToExclude,
            Dictionary<string, List<RecursionState>> searchesToExcludeInSubdirs,
            TaskOptions taskOptions)
        {
            ErrorUtilities.VerifyThrow((recursionState.SearchData.Filespec== null) || (recursionState.SearchData.RegexFileMatch == null),
                "File-spec overrides the regular expression -- pass null for file-spec if you want to use the regular expression.");

            ErrorUtilities.VerifyThrow((recursionState.SearchData.Filespec != null) || (recursionState.SearchData.RegexFileMatch != null),
                "Need either a file-spec or a regular expression to match files.");

            ErrorUtilities.VerifyThrow(recursionState.RemainingWildcardDirectory != null, "Expected non-null remaning wildcard directory.");

            RecursiveStepResult[] excludeNextSteps = null;
            //  Determine if any of searchesToExclude is necessarily a superset of the results that will be returned.
            //  This means all results will be excluded and we should bail out now.
            if (searchesToExclude != null)
            {
                excludeNextSteps = new RecursiveStepResult[searchesToExclude.Count];
                for (int i = 0; i < searchesToExclude.Count; i++)
                {
                    RecursionState searchToExclude = searchesToExclude[i];
                    //  The BaseDirectory of all the exclude searches should be the same as the include one
                    Debug.Assert(FileUtilities.PathsEqual(searchToExclude.BaseDirectory, recursionState.BaseDirectory), "Expected exclude search base directory to match include search base directory");

                    excludeNextSteps[i] = GetFilesRecursiveStep(searchesToExclude[i]);

                    //  We can exclude all results in this folder if:
                    if (
                        //  We are matching files based on a filespec and not a regular expression
                        searchToExclude.SearchData.Filespec != null &&
                        //  The wildcard path portion of the excluded search matches the include search
                        searchToExclude.RemainingWildcardDirectory == recursionState.RemainingWildcardDirectory &&
                        //  The exclude search will match ALL filenames OR
                        (searchToExclude.SearchData.Filespec == "*" || searchToExclude.SearchData.Filespec == "*.*" ||
                            //  The exclude search filename pattern matches the include search's pattern
                            searchToExclude.SearchData.Filespec == recursionState.SearchData.Filespec))
                    {
                        //  We won't get any results from this search that we would end up keeping
                        return;
                    }
                }
            }

            RecursiveStepResult nextStep = GetFilesRecursiveStep(recursionState);

            List<string> files = null;
            foreach (string file in GetFilesForStep(nextStep, recursionState, projectDirectory,
                stripProjectDirectory))
            {
                if (excludeNextSteps != null)
                {
                    bool exclude = false;
                    for (int i = 0; i < excludeNextSteps.Length; i++)
                    {
                        RecursiveStepResult excludeNextStep = excludeNextSteps[i];
                        if (excludeNextStep.ConsiderFiles && MatchFileRecursionStep(searchesToExclude[i], file))
                        {
                            exclude = true;
                            break;
                        }
                    }
                    if (exclude)
                    {
                        continue;
                    }
                }
                files = files ?? new List<string>();
                files.Add(file);
            }
            // Add all matched files at once to reduce thread contention
            if (files?.Count > 0)
            {
                listOfFiles.Push(files);
            }

            if (!nextStep.NeedsDirectoryRecursion)
            {
                return;
            }

            Action<string> processSubdirectory = subdir =>
            {
                //  RecursionState is a struct so this copies it
                var newRecursionState = recursionState;

                newRecursionState.BaseDirectory = subdir;
                newRecursionState.RemainingWildcardDirectory = nextStep.RemainingWildcardDirectory;

                List<RecursionState> newSearchesToExclude = null;

                if (excludeNextSteps != null)
                {
                    newSearchesToExclude = new List<RecursionState>();

                    for (int i = 0; i < excludeNextSteps.Length; i++)
                    {
                        if (excludeNextSteps[i].NeedsDirectoryRecursion &&
                            (excludeNextSteps[i].DirectoryPattern == null || IsMatch(Path.GetFileName(subdir), excludeNextSteps[i].DirectoryPattern, true)))
                        {
                            RecursionState thisExcludeStep = searchesToExclude[i];
                            thisExcludeStep.BaseDirectory = subdir;
                            thisExcludeStep.RemainingWildcardDirectory = excludeNextSteps[i].RemainingWildcardDirectory;
                            newSearchesToExclude.Add(thisExcludeStep);
                        }
                    }
                }

                if (searchesToExcludeInSubdirs != null)
                {
                    List<RecursionState> searchesForSubdir;

                    if (searchesToExcludeInSubdirs.TryGetValue(subdir, out searchesForSubdir))
                    {
                        //  We've found the base directory that these exclusions apply to.  So now add them as normal searches
                        if (newSearchesToExclude == null)
                        {
                            newSearchesToExclude = new List<RecursionState>();
                        }
                        newSearchesToExclude.AddRange(searchesForSubdir);
                    }
                }

                // We never want to strip the project directory from the leaves, because the current 
                // process directory maybe different
                GetFilesRecursive(
                    listOfFiles,
                    newRecursionState,
                    projectDirectory,
                    stripProjectDirectory,
                    newSearchesToExclude,
                    searchesToExcludeInSubdirs,
                    taskOptions);
            };

            // Calcuate the MaxDegreeOfParallelism value in order to prevent too much tasks being running concurrently.
            int dop = 0;
            // Lock only when we may be dealing with multiple threads
            if (taskOptions.MaxTasks > 1 && taskOptions.MaxTasksPerIteration > 1)
            {
                // We don't need to lock when there will be only one Parallel.ForEach running
                // If the condition is true, means that we are going to iterate though the project root folder
                // by using only one Parallel.ForEach
                if (taskOptions.MaxTasks == taskOptions.MaxTasksPerIteration)
                {
                    dop = taskOptions.AvailableTasks;
                    taskOptions.AvailableTasks = 0;
                }
                else
                {
                    lock (taskOptions)
                    {
                        dop = Math.Min(taskOptions.MaxTasksPerIteration, taskOptions.AvailableTasks);
                        taskOptions.AvailableTasks -= dop;
                    }
                }
            }
            // Use a foreach to reduce the overhead of Parallel.ForEach when we are not running in parallel
            if (dop < 2)
            {
                foreach (var subdir in _getFileSystemEntries(FileSystemEntity.Directories, recursionState.BaseDirectory, nextStep.DirectoryPattern, null, false))
                {
                    processSubdirectory(subdir);
                }
            }
            else
            {
                Parallel.ForEach(
                    _getFileSystemEntries(FileSystemEntity.Directories, recursionState.BaseDirectory, nextStep.DirectoryPattern, null, false),
                    new ParallelOptions {MaxDegreeOfParallelism = dop},
                    processSubdirectory);
            }
            if (dop <= 0)
            {
                return;
            }
            // We don't need to lock if there was only one Parallel.ForEach running
            // If the condition is true, means that we finished the iteration though the project root folder and
            // all its subdirectories
            if (taskOptions.MaxTasks == taskOptions.MaxTasksPerIteration)
            {
                taskOptions.AvailableTasks = taskOptions.MaxTasks;
                return;
            }
            lock (taskOptions)
            {
                taskOptions.AvailableTasks += dop;
            }
        }

        private IEnumerable<string> GetFilesForStep(
            RecursiveStepResult stepResult,
            RecursionState recursionState,
            string projectDirectory,
            bool stripProjectDirectory)
        {
            if (!stepResult.ConsiderFiles)
            {
                return Enumerable.Empty<string>();
            }
            IEnumerable<string> files = _getFileSystemEntries(FileSystemEntity.Files, recursionState.BaseDirectory,
                recursionState.SearchData.Filespec, projectDirectory, stripProjectDirectory);

            if (!stepResult.NeedsToProcessEachFile)
            {
                return files;
            }
            return files.Where(o => MatchFileRecursionStep(recursionState, o));
        }

        private static bool MatchFileRecursionStep(RecursionState recursionState, string file)
        {
            if (recursionState.SearchData.Filespec != null)
            {
                return IsMatch(Path.GetFileName(file), recursionState.SearchData.Filespec, true);
            }

            // if no file-spec provided, match the file to the regular expression
            // PERF NOTE: Regex.IsMatch() is an expensive operation, so we avoid it whenever possible
            return recursionState.SearchData.RegexFileMatch.IsMatch(file);
        }

        private static RecursiveStepResult GetFilesRecursiveStep
        (
            RecursionState recursionState
        )
        {
            RecursiveStepResult ret = new RecursiveStepResult();

            /*
             * Get the matching files.
             */
            bool considerFiles = false;

            // Only consider files if...
            if (recursionState.RemainingWildcardDirectory.Length == 0)
            {
                // We've reached the end of the wildcard directory elements.
                considerFiles = true;
            }
            else if (recursionState.RemainingWildcardDirectory.IndexOf(recursiveDirectoryMatch, StringComparison.Ordinal) == 0)
            {
                // or, we've reached a "**" so everything else is matched recursively.
                considerFiles = true;
            }
            ret.ConsiderFiles = considerFiles;
            if (considerFiles)
            {
                ret.NeedsToProcessEachFile = recursionState.SearchData.Filespec == null;
            }

            /*
             * Recurse into subdirectories.
             */
            if (recursionState.SearchData.NeedsRecursion && recursionState.RemainingWildcardDirectory.Length > 0)
            {
                // Find the next directory piece.
                string pattern = null;

                if (!IsRecursiveDirectoryMatch(recursionState.RemainingWildcardDirectory))
                {
                    int indexOfNextSlash = recursionState.RemainingWildcardDirectory.IndexOfAny(directorySeparatorCharacters);

                    pattern = indexOfNextSlash != -1 ? recursionState.RemainingWildcardDirectory.Substring(0, indexOfNextSlash) : recursionState.RemainingWildcardDirectory;

                    if (pattern == recursiveDirectoryMatch)
                    {
                        // If pattern turned into **, then there's no choice but to enumerate everything.
                        pattern = null;
                        recursionState.RemainingWildcardDirectory = recursiveDirectoryMatch;
                    }
                    else
                    {
                        // Peel off the leftmost directory piece. So for example, if remainingWildcardDirectory
                        // contains:
                        //
                        //        ?emp\foo\**\bar
                        //
                        // then put '?emp' into pattern. Then put the remaining part,
                        //
                        //        foo\**\bar
                        //
                        // back into remainingWildcardDirectory.
                        // This is a performance optimization. We don't want to enumerate everything if we 
                        // don't have to.
                        recursionState.RemainingWildcardDirectory = indexOfNextSlash != -1 ? recursionState.RemainingWildcardDirectory.Substring(indexOfNextSlash + 1) : string.Empty;
                    }
                }

                ret.NeedsDirectoryRecursion = true;
                ret.RemainingWildcardDirectory = recursionState.RemainingWildcardDirectory;
                ret.DirectoryPattern = pattern;
            }

            return ret;
        }

        /// <summary>
        /// Given a file spec, create a regular expression that will match that
        /// file spec.
        /// 
        /// PERF WARNING: this method is called in performance-critical
        /// scenarios, so keep it fast and cheap
        /// </summary>
        /// <param name="fixedDirectoryPart">The fixed directory part.</param>
        /// <param name="wildcardDirectoryPart">The wildcard directory part.</param>
        /// <param name="filenamePart">The filename part.</param>
        /// <param name="isLegalFileSpec">Receives whether this pattern is legal or not.</param>
        /// <returns>The regular expression string.</returns>
        private static string RegularExpressionFromFileSpec
        (
            string fixedDirectoryPart,
            string wildcardDirectoryPart,
            string filenamePart,
            out bool isLegalFileSpec
        )
        {
            isLegalFileSpec = true;

            /*
             * The code below uses tags in the form <:tag:> to encode special information
             * while building the regular expression.
             * 
             * This format was chosen because it's not a legal form for filespecs. If the
             * filespec comes in with either "<:" or ":>", return isLegalFileSpec=false to
             * prevent intrusion into the special processing.
             */
            if ((fixedDirectoryPart.IndexOf("<:", StringComparison.Ordinal) != -1) ||
                (fixedDirectoryPart.IndexOf(":>", StringComparison.Ordinal) != -1) ||
                (wildcardDirectoryPart.IndexOf("<:", StringComparison.Ordinal) != -1) ||
                (wildcardDirectoryPart.IndexOf(":>", StringComparison.Ordinal) != -1) ||
                (filenamePart.IndexOf("<:", StringComparison.Ordinal) != -1) ||
                (filenamePart.IndexOf(":>", StringComparison.Ordinal) != -1))
            {
                isLegalFileSpec = false;
                return String.Empty;
            }

            /*
             * Its not legal for there to be a ".." after a wildcard.
             */
            if (wildcardDirectoryPart.Contains(dotdot))
            {
                isLegalFileSpec = false;
                return String.Empty;
            }

            /* 
             * Trailing dots in file names have to be treated specially.
             * We want:
             * 
             *     *. to match foo
             * 
             * but 'foo' doesn't have a trailing '.' so we need to handle this while still being careful 
             * not to match 'foo.txt'
             */
            if (filenamePart.EndsWith(".", StringComparison.Ordinal))
            {
                filenamePart = filenamePart.Replace("*", "<:anythingbutdot:>");
                filenamePart = filenamePart.Replace("?", "<:anysinglecharacterbutdot:>");
                filenamePart = filenamePart.Substring(0, filenamePart.Length - 1);
            }

            /*
             * Now, build up the starting filespec but put tags in to identify where the fixedDirectory,
             * wildcardDirectory and filenamePart are. Also tag the beginning of the line and the end of
             * the line, so that we can identify patterns by whether they're on one end or the other.
             */
            StringBuilder matchFileExpression = new StringBuilder();
            matchFileExpression.Append("<:bol:>");
            matchFileExpression.Append("<:fixeddir:>").Append(fixedDirectoryPart).Append("<:endfixeddir:>");
            matchFileExpression.Append("<:wildcarddir:>").Append(wildcardDirectoryPart).Append("<:endwildcarddir:>");
            matchFileExpression.Append("<:filename:>").Append(filenamePart).Append("<:endfilename:>");
            matchFileExpression.Append("<:eol:>");

            /*
             *  Call out our special matching characters.
             */
            foreach (var separator in directorySeparatorStrings)
            {
                matchFileExpression.Replace(separator, "<:dirseparator:>");
            }

            /*
             * Capture the leading \\ in UNC paths, so that the doubled slash isn't
             * reduced in a later step.
             */
            matchFileExpression.Replace("<:fixeddir:><:dirseparator:><:dirseparator:>", "<:fixeddir:><:uncslashslash:>");

            /*
             * Iteratively reduce four cases involving directory separators
             * 
             *  (1) <:dirseparator:>.<:dirseparator:> -> <:dirseparator:>
             *        This is an identity, so for example, these two are equivalent,
             * 
             *            dir1\.\dir2 == dir1\dir2
             * 
             *    (2) <:dirseparator:><:dirseparator:> -> <:dirseparator:>
             *      Double directory separators are treated as a single directory separator,
             *      so, for example, this is an identity:
             * 
             *          f:\dir1\\dir2 == f:\dir1\dir2
             * 
             *      The single exemption is for UNC path names, like this:
             * 
             *          \\server\share != \server\share
             * 
             *      This case is handled by the <:uncslashslash:> which was substituted in
             *      a prior step.
             * 
             *  (3) <:fixeddir:>.<:dirseparator:>.<:dirseparator:> -> <:fixeddir:>.<:dirseparator:>
             *      A ".\" at the beginning of a line is equivalent to nothing, so:
             * 
             *          .\.\dir1\file.txt == .\dir1\file.txt
             * 
             *  (4) <:dirseparator:>.<:eol:> -> <:eol:>
             *      A "\." at the end of a line is equivalent to nothing, so:
             * 
             *          dir1\dir2\. == dir1\dir2             *
             */
            int sizeBefore;
            do
            {
                sizeBefore = matchFileExpression.Length;

                // NOTE: all these replacements will necessarily reduce the expression length i.e. length will either reduce or
                // stay the same through this loop
                matchFileExpression.Replace("<:dirseparator:>.<:dirseparator:>", "<:dirseparator:>");
                matchFileExpression.Replace("<:dirseparator:><:dirseparator:>", "<:dirseparator:>");
                matchFileExpression.Replace("<:fixeddir:>.<:dirseparator:>.<:dirseparator:>", "<:fixeddir:>.<:dirseparator:>");
                matchFileExpression.Replace("<:dirseparator:>.<:endfilename:>", "<:endfilename:>");
                matchFileExpression.Replace("<:filename:>.<:endfilename:>", "<:filename:><:endfilename:>");

                ErrorUtilities.VerifyThrow(matchFileExpression.Length <= sizeBefore,
                    "Expression reductions cannot increase the length of the expression.");
            } while (matchFileExpression.Length < sizeBefore);

            /*
             * Collapse **\** into **.
             */
            do
            {
                sizeBefore = matchFileExpression.Length;
                matchFileExpression.Replace(recursiveDirectoryMatch + "<:dirseparator:>" + recursiveDirectoryMatch, recursiveDirectoryMatch);

                ErrorUtilities.VerifyThrow(matchFileExpression.Length <= sizeBefore,
                    "Expression reductions cannot increase the length of the expression.");
            } while (matchFileExpression.Length < sizeBefore);

            /*
             * Call out legal recursion operators:
             * 
             *        fixed-directory + **\
             *        \**\
             *        **\**
             * 
             */
            do
            {
                sizeBefore = matchFileExpression.Length;
                matchFileExpression.Replace("<:dirseparator:>" + recursiveDirectoryMatch + "<:dirseparator:>", "<:middledirs:>");
                matchFileExpression.Replace("<:wildcarddir:>" + recursiveDirectoryMatch + "<:dirseparator:>", "<:wildcarddir:><:leftdirs:>");

                ErrorUtilities.VerifyThrow(matchFileExpression.Length <= sizeBefore,
                    "Expression reductions cannot increase the length of the expression.");
            } while (matchFileExpression.Length < sizeBefore);


            /*
             * By definition, "**" must appear alone between directory slashes. If there is any remaining "**" then this is not
             * a valid filespec.
             */
            // NOTE: this condition is evaluated left-to-right -- this is important because we want the length BEFORE stripping
            // any "**"s remaining in the expression
            if (matchFileExpression.Length > matchFileExpression.Replace(recursiveDirectoryMatch, null).Length)
            {
                isLegalFileSpec = false;
                return String.Empty;
            }

            /*
             * Remaining call-outs not involving "**"
             */
            matchFileExpression.Replace("*.*", "<:anynonseparator:>");
            matchFileExpression.Replace("*", "<:anynonseparator:>");
            matchFileExpression.Replace("?", "<:singlecharacter:>");

            /*
             *  Escape all special characters defined for regular expresssions.
             */
            matchFileExpression.Replace("\\", "\\\\"); // Must be first.
            matchFileExpression.Replace("$", "\\$");
            matchFileExpression.Replace("(", "\\(");
            matchFileExpression.Replace(")", "\\)");
            matchFileExpression.Replace("*", "\\*");
            matchFileExpression.Replace("+", "\\+");
            matchFileExpression.Replace(".", "\\.");
            matchFileExpression.Replace("[", "\\[");
            matchFileExpression.Replace("?", "\\?");
            matchFileExpression.Replace("^", "\\^");
            matchFileExpression.Replace("{", "\\{");
            matchFileExpression.Replace("|", "\\|");

            /*
             *  Now, replace call-outs with their regex equivalents.
             */
            matchFileExpression.Replace("<:middledirs:>", "((/)|(\\\\)|(/.*/)|(/.*\\\\)|(\\\\.*\\\\)|(\\\\.*/))");
            matchFileExpression.Replace("<:leftdirs:>", "((.*/)|(.*\\\\)|())");
            matchFileExpression.Replace("<:rightdirs:>", ".*");
            matchFileExpression.Replace("<:anything:>", ".*");
            matchFileExpression.Replace("<:anythingbutdot:>", "[^\\.]*");
            matchFileExpression.Replace("<:anysinglecharacterbutdot:>", "[^\\.].");
            matchFileExpression.Replace("<:anynonseparator:>", "[^/\\\\]*");
            matchFileExpression.Replace("<:singlecharacter:>", ".");
            matchFileExpression.Replace("<:dirseparator:>", "[/\\\\]+");
            matchFileExpression.Replace("<:uncslashslash:>", @"\\\\");
            matchFileExpression.Replace("<:bol:>", "^");
            matchFileExpression.Replace("<:eol:>", "$");
            matchFileExpression.Replace("<:fixeddir:>", "(?<FIXEDDIR>");
            matchFileExpression.Replace("<:endfixeddir:>", ")");
            matchFileExpression.Replace("<:wildcarddir:>", "(?<WILDCARDDIR>");
            matchFileExpression.Replace("<:endwildcarddir:>", ")");
            matchFileExpression.Replace("<:filename:>", "(?<FILENAME>");
            matchFileExpression.Replace("<:endfilename:>", ")");

            return matchFileExpression.ToString();
        }

        /// <summary>
        /// Given a filespec, get the information needed for file matching. 
        /// </summary>
        /// <param name="filespec">The filespec.</param>
        /// <param name="regexFileMatch">Receives the regular expression.</param>
        /// <param name="needsRecursion">Receives the flag that is true if recursion is required.</param>
        /// <param name="isLegalFileSpec">Receives the flag that is true if the filespec is legal.</param>
        internal void GetFileSpecInfoWithRegexObject(
            string filespec,
            out Regex regexFileMatch,
            out bool needsRecursion,
            out bool isLegalFileSpec)
        {
            string fixedDirectoryPart;
            string wildcardDirectoryPart;
            string filenamePart;
            string matchFileExpression;

            GetFileSpecInfo(filespec,
                out fixedDirectoryPart, out wildcardDirectoryPart, out filenamePart,
                out matchFileExpression, out needsRecursion, out isLegalFileSpec);

            
            regexFileMatch = isLegalFileSpec
                ? new Regex(matchFileExpression, DefaultRegexOptions)
                : null;
        }

        internal delegate Tuple<string, string, string> FixupParts(
            string fixedDirectoryPart,
            string recursiveDirectoryPart,
            string filenamePart);

        /// <summary>
        /// Given a filespec, parse it and construct the regular expression string.
        /// </summary>
        /// <param name="filespec">The filespec.</param>
        /// <param name="fixedDirectoryPart">Receives the fixed directory part.</param>
        /// <param name="wildcardDirectoryPart">Receives the wildcard directory part.</param>
        /// <param name="filenamePart">Receives the filename part.</param>
        /// <param name="matchFileExpression">Receives the regular expression.</param>
        /// <param name="needsRecursion">Receives the flag that is true if recursion is required.</param>
        /// <param name="isLegalFileSpec">Receives the flag that is true if the filespec is legal.</param>
        /// <param name="fixupParts">hook method to further change the parts</param>
        internal void GetFileSpecInfo(
            string filespec,
            out string fixedDirectoryPart,
            out string wildcardDirectoryPart,
            out string filenamePart,
            out string matchFileExpression,
            out bool needsRecursion,
            out bool isLegalFileSpec,
            FixupParts fixupParts = null)
        {
            isLegalFileSpec = true;
            needsRecursion = false;
            fixedDirectoryPart = String.Empty;
            wildcardDirectoryPart = String.Empty;
            filenamePart = String.Empty;
            matchFileExpression = null;

            if (!RawFileSpecIsValid(filespec))
            {
                isLegalFileSpec = false;
                return;
            }

            /*
             * Now break up the filespec into constituent parts--fixed, wildcard and filename.
             */
            SplitFileSpec(filespec, out fixedDirectoryPart, out wildcardDirectoryPart, out filenamePart);

            if (fixupParts != null)
            {
                var newParts = fixupParts(fixedDirectoryPart, wildcardDirectoryPart, filenamePart);

                // todo use named tuples when they'll be available
                fixedDirectoryPart = newParts.Item1;
                wildcardDirectoryPart = newParts.Item2;
                filenamePart = newParts.Item3;
            }

            /*
             *  Get a regular expression for matching files that will be found.
             */
            matchFileExpression = RegularExpressionFromFileSpec(fixedDirectoryPart, wildcardDirectoryPart, filenamePart, out isLegalFileSpec);

            /*
             * Was the filespec valid? If not, then just return now.
             */
            if (!isLegalFileSpec)
            {
                return;
            }

            /*
             * Determine whether recursion will be required.
             */
            needsRecursion = (wildcardDirectoryPart.Length != 0);
        }

        internal static bool RawFileSpecIsValid(string filespec)
        {
            // filespec cannot contain illegal characters
            if (-1 != filespec.IndexOfAny(s_invalidPathChars))
            {
                return false;
            }

            /*
             * Check for patterns in the filespec that are explicitly illegal.
             * 
             * Any path with "..." in it is illegal.
             */
            if (-1 != filespec.IndexOf("...", StringComparison.Ordinal))
            {
                return false;
            }

            /*
             * If there is a ':' anywhere but the second character, this is an illegal pattern.
             * Catches this case among others,
             * 
             *        http://www.website.com
             * 
             */
            int rightmostColon = filespec.LastIndexOf(":", StringComparison.Ordinal);

            if
            (
                -1 != rightmostColon
                && 1 != rightmostColon
            )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// The results of a match between a filespec and a file name.
        /// </summary>
        internal sealed class Result
        {
            /// <summary>
            /// Default constructor.
            /// </summary>
            internal Result()
            {
                // do nothing
            }

            internal bool isLegalFileSpec; // initially false
            internal bool isMatch; // initially false
            internal bool isFileSpecRecursive; // initially false
            internal string fixedDirectoryPart = String.Empty;
            internal string wildcardDirectoryPart = String.Empty;
            internal string filenamePart = String.Empty;
        }

        /// <summary>
        /// A wildcard (* and ?) matching algorithm that tests whether the input string matches against the pattern.
        /// </summary>
        /// <param name="input">String which is matched against the pattern.</param>
        /// <param name="pattern">Pattern against which string is matched.</param>
        /// <param name="ignoreCase">Determines whether ignoring case when comparing two characters</param>
        internal static bool IsMatch(string input, string pattern, bool ignoreCase)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            // Parameter lengths
            int patternLength = pattern.Length;
            int inputLength = input.Length;

            // Used to save the location when a * wildcard is found in the input string
            int patternTmpIndex = -1;
            int inputTmpIndex = -1;

            // Current indexes
            int patternIndex = 0;
            int inputIndex = 0;

            // Store the information whether the tail was checked when a pattern "*?" occurred
            bool tailChecked = false;

#if MONO    // MONO doesn't support local functions
            Func<char, char, int, int, bool> CompareIgnoreCase = (inputChar, patternChar, iIndex, pIndex) =>
#else
            // Function for comparing two characters, ignoring case
            // PERF NOTE:
            // Having a local function instead of a variable increases the speed by approx. 2 times.
            // Passing inputChar and patternChar increases the speed by approx. 10%, when comparing
            // to using the string indexer. The iIndex and pIndex parameters are only used
            // when we have to compare two non ASCII characters. Using just string.Compare for
            // character comparison, would reduce the speed by approx. 5 times.
            bool CompareIgnoreCase(char inputChar, char patternChar, int iIndex, int pIndex)
#endif
            {
                // We will mostly be comparing ASCII characters, check this first
                if (inputChar < 128 && patternChar < 128)
                {
                    if (inputChar >= 'A' && inputChar <= 'Z' && patternChar >= 'a' && patternChar <= 'z')
                    {
                        return inputChar + 32 == patternChar;
                    }
                    if (inputChar >= 'a' && inputChar <= 'z' && patternChar >= 'A' && patternChar <= 'Z')
                    {
                        return inputChar == patternChar + 32;
                    }
                    return inputChar == patternChar;
                }
                if (inputChar > 128 && patternChar > 128)
                {
                    return string.Compare(input, iIndex, pattern, pIndex, 1, StringComparison.OrdinalIgnoreCase) == 0;
                }
                // We don't need to compare, an ASCII character cannot have its lowercase/uppercase outside the ASCII table
                // and a non ASCII character cannot have its lowercase/uppercase inside the ASCII table
                return false;
            }
#if MONO
            ; // The end of the CompareIgnoreCase anonymous function
#endif

            while (inputIndex < inputLength)
            {
                if (patternIndex < patternLength)
                {
                    // Check if there is a * wildcard first as we can have it also in the input string
                    if (pattern[patternIndex] == '*')
                    {
                        // Skip all * wildcards if there are more than one
                        while (++patternIndex < patternLength && pattern[patternIndex] == '*') { }

                        // Return if the last character is a * wildcard
                        if (patternIndex >= patternLength)
                        {
                            return true;
                        }

                        // Mostly, we will be dealing with a file extension pattern e.g. "*.ext", so try to check the tail first
                        if (!tailChecked)
                        {
                            // Iterate from the end of the pattern to the current pattern index
                            // and hope that there is no * wildcard in order to return earlier
                            int inputTailIndex = inputLength;
                            int patternTailIndex = patternLength;
                            while (patternIndex < patternTailIndex && inputTailIndex > inputIndex)
                            {
                                patternTailIndex--;
                                inputTailIndex--;
                                // If we encountered a * wildcard we are not sure if it matches as there can be zero or more than one characters
                                // so we have to fallback to the standard procedure e.g. ("aaaabaaad", "*?b*d")
                                if (pattern[patternTailIndex] == '*')
                                {
                                    break;
                                }
                                // If the tail doesn't match, we can safely return e.g. ("aaa", "*b")
                                if ((
                                        (!ignoreCase && input[inputTailIndex] != pattern[patternTailIndex]) ||
                                        (ignoreCase && !CompareIgnoreCase(input[inputTailIndex], pattern[patternTailIndex], patternTailIndex, inputTailIndex))
                                    ) &&
                                    pattern[patternTailIndex] != '?')
                                {
                                    return false;
                                }
                                if (patternIndex == patternTailIndex)
                                {
                                    return true;
                                }
                            }
                            // Alter the lengths to the last valid match so that we don't need to match them again
                            inputLength = inputTailIndex + 1;
                            patternLength = patternTailIndex + 1;
                            tailChecked = true; // Make sure that the tail is checked only once
                        }

                        // Skip to the first character that matches after the *, e.g. ("abcd", "*d")
                        // The ? wildcard cannot be skipped as we will have a wrong result for e.g. ("aab" "*?b")
                        if (pattern[patternIndex] != '?')
                        {
                            while (
                                (!ignoreCase && input[inputIndex] != pattern[patternIndex]) ||
                                (ignoreCase && !CompareIgnoreCase(input[inputIndex], pattern[patternIndex], inputIndex, patternIndex)))
                            {
                                // Return if there is no character that match e.g. ("aa", "*b")
                                if (++inputIndex >= inputLength)
                                {
                                    return false;
                                }
                            }
                        }
                        patternTmpIndex = patternIndex;
                        inputTmpIndex = inputIndex;
                        continue;
                    }

                    // If we have a match, step to the next character
                    if (
                        (!ignoreCase && input[inputIndex] == pattern[patternIndex]) ||
                        (ignoreCase && CompareIgnoreCase(input[inputIndex], pattern[patternIndex], inputIndex, patternIndex)) ||
                        pattern[patternIndex] == '?')
                    {
                        patternIndex++;
                        inputIndex++;
                        continue;
                    }
                }
                // No match found, if we didn't found a location of a * wildcard, return false e.g. ("ab", "?ab")
                // otherwise set the location after the previous * wildcard and try again with the next character in the input
                if (patternTmpIndex < 0)
                {
                    return false;
                }
                patternIndex = patternTmpIndex;
                inputIndex = inputTmpIndex++;
            }
            // When we reach the end of the input we have to skip all * wildcards as they match also zero characters
            while (patternIndex < patternLength && pattern[patternIndex] == '*')
            {
                patternIndex++;
            }
            return patternIndex >= patternLength;
        }

        /// <summary>
        /// Given a pattern (filespec) and a candidate filename (fileToMatch)
        /// return matching information.
        /// </summary>
        /// <param name="filespec">The filespec.</param>
        /// <param name="fileToMatch">The candidate to match against.</param>
        /// <returns>The result class.</returns>
        internal Result FileMatch
        (
            string filespec,
            string fileToMatch
        )
        {
            Result matchResult = new Result();

            fileToMatch = GetLongPathName(fileToMatch, _getFileSystemEntries);

            Regex regexFileMatch;
            GetFileSpecInfoWithRegexObject
            (
                filespec,
                out regexFileMatch,
                out matchResult.isFileSpecRecursive,
                out matchResult.isLegalFileSpec
            );

            if (matchResult.isLegalFileSpec)
            {
                GetRegexMatchInfo(
                    fileToMatch,
                    regexFileMatch,
                    out matchResult.isMatch,
                    out matchResult.fixedDirectoryPart,
                    out matchResult.wildcardDirectoryPart,
                    out matchResult.filenamePart);
            }

            return matchResult;
        }

        internal static void GetRegexMatchInfo(
            string fileToMatch,
            Regex fileSpecRegex,
            out bool isMatch,
            out string fixedDirectoryPart,
            out string wildcardDirectoryPart,
            out string filenamePart)
        {
            Match match = fileSpecRegex.Match(fileToMatch);

            isMatch = match.Success;
            fixedDirectoryPart = string.Empty;
            wildcardDirectoryPart = String.Empty;
            filenamePart = string.Empty;

            if (isMatch)
            {
                fixedDirectoryPart = match.Groups["FIXEDDIR"].Value;
                wildcardDirectoryPart = match.Groups["WILDCARDDIR"].Value;
                filenamePart = match.Groups["FILENAME"].Value;
            }
        }

        class TaskOptions
        {
            public TaskOptions(int maxTasks)
            {
                MaxTasks = maxTasks;
            }
            /// <summary>
            /// The maximum number of tasks that are allowed to run concurrently
            /// </summary>
            public readonly int MaxTasks;

            /// <summary>
            /// The number of currently available tasks
            /// </summary>
            public int AvailableTasks;

            /// <summary>
            /// The maximum number of tasks that Parallel.ForEach may use
            /// </summary>
            public int MaxTasksPerIteration;
        }

        /// <summary>
        /// Given a filespec, find the files that match. 
        /// Will never throw IO exceptions: if there is no match, returns the input verbatim.
        /// </summary>
        /// <param name="projectDirectoryUnescaped">The project directory.</param>
        /// <param name="filespecUnescaped">Get files that match the given file spec.</param>
        /// <param name="excludeSpecsUnescaped">Exclude files that match this file spec.</param>
        /// <returns>The array of files.</returns>
        internal string[] GetFiles
        (
            string projectDirectoryUnescaped,
            string filespecUnescaped,
            IEnumerable<string> excludeSpecsUnescaped = null
        )
        {
            // For performance. Short-circuit iff there is no wildcard.
            // Perf Note: Doing a [Last]IndexOfAny(...) is much faster than compiling a
            // regular expression that does the same thing, regardless of whether
            // filespec contains one of the characters.
            // Choose LastIndexOfAny instead of IndexOfAny because it seems more likely
            // that wildcards will tend to be towards the right side.
            if (!HasWildcards(filespecUnescaped))
            {
                return CreateArrayWithSingleItemIfNotExcluded(filespecUnescaped, excludeSpecsUnescaped);
            }

            if (_cachedGlobExpansions == null)
            {
                return GetFilesImplementation(
                    projectDirectoryUnescaped,
                    filespecUnescaped,
                    excludeSpecsUnescaped);
            }

            var filesKey = ComputeFileEnumerationCacheKey(projectDirectoryUnescaped, filespecUnescaped, excludeSpecsUnescaped);

            ImmutableArray<string> files;
            if (!_cachedGlobExpansions.TryGetValue(filesKey, out files))
            {
                // avoid parallel evaluations of the same wildcard by using a unique lock for each wildcard
                object locks = _cachedGlobExpansionsLock.Value.GetOrAdd(filesKey, _ => new object());
                lock (locks)
                {
                    if (!_cachedGlobExpansions.TryGetValue(filesKey, out files))
                    {
                        files =
                            _cachedGlobExpansions.GetOrAdd(
                                filesKey,
                                (_) =>
                                    GetFilesImplementation(
                                        projectDirectoryUnescaped,
                                        filespecUnescaped,
                                        excludeSpecsUnescaped)
                                        .ToImmutableArray());
                    }
                }
            }

            // Copy the file enumerations to prevent outside modifications of the cache (e.g. sorting, escaping) and to maintain the original method contract that a new array is created on each call.
            var filesToReturn = files.ToArray();

            return filesToReturn;
        }

        private static string ComputeFileEnumerationCacheKey(string projectDirectoryUnescaped, string filespecUnescaped, IEnumerable<string> excludes)
        {
            var sb = new StringBuilder();

            sb.Append(projectDirectoryUnescaped);
            sb.Append(filespecUnescaped);

            if (excludes != null)
            {
                foreach (var exclude in excludes)
                {
                    sb.Append(exclude);
                }
            }

            return sb.ToString();
        }

        enum SearchAction
        {
            RunSearch,
            ReturnFileSpec,
            ReturnEmptyList,
        }

        private SearchAction GetFileSearchData(
            string projectDirectoryUnescaped,
            string filespecUnescaped,
            out bool stripProjectDirectory,
            out RecursionState result)
        {
            stripProjectDirectory = false;
            result = new RecursionState();

            string fixedDirectoryPart;
            string wildcardDirectoryPart;
            string filenamePart;
            string matchFileExpression;
            bool needsRecursion;
            bool isLegalFileSpec;
            GetFileSpecInfo
            (
                filespecUnescaped,
                out fixedDirectoryPart,
                out wildcardDirectoryPart,
                out filenamePart,
                out matchFileExpression,
                out needsRecursion,
                out isLegalFileSpec
            );

            /*
             * If the filespec is invalid, then just return now.
             */
            if (!isLegalFileSpec)
            {
                return SearchAction.ReturnFileSpec;
            }

            // The projectDirectory is not null only if we are running the evaluation from
            // inside the engine (i.e. not from a task)
            if (projectDirectoryUnescaped != null)
            {
                if (fixedDirectoryPart != null)
                {
                    string oldFixedDirectoryPart = fixedDirectoryPart;
                    try
                    {
                        fixedDirectoryPart = Path.Combine(projectDirectoryUnescaped, fixedDirectoryPart);
                    }
                    catch (ArgumentException)
                    {
                        return SearchAction.ReturnEmptyList;
                    }

                    stripProjectDirectory = !String.Equals(fixedDirectoryPart, oldFixedDirectoryPart, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    fixedDirectoryPart = projectDirectoryUnescaped;
                    stripProjectDirectory = true;
                }
            }

            /*
             * If the fixed directory part doesn't exist, then this means no files should be
             * returned.
             */
            if (fixedDirectoryPart.Length > 0 && !_directoryExists(fixedDirectoryPart))
            {
                return SearchAction.ReturnEmptyList;
            }

            // determine if we need to use the regular expression to match the files
            // PERF NOTE: Constructing a Regex object is expensive, so we avoid it whenever possible
            bool matchWithRegex =
                // if we have a directory specification that uses wildcards, and
                (wildcardDirectoryPart.Length > 0) &&
                // the specification is not a simple "**"
                !IsRecursiveDirectoryMatch(wildcardDirectoryPart);
            // then we need to use the regular expression

            var searchData = new FilesSearchData(
                // if using the regular expression, ignore the file pattern
                (matchWithRegex ? null : filenamePart),
                // if using the file pattern, ignore the regular expression
                (matchWithRegex ? new Regex(matchFileExpression, RegexOptions.IgnoreCase) : null),
                needsRecursion);

            result.SearchData = searchData;
            result.BaseDirectory = Normalize(fixedDirectoryPart);
            result.RemainingWildcardDirectory = Normalize(wildcardDirectoryPart);

            return SearchAction.RunSearch;
        }

        /// <summary>
        /// Replace all slashes to the OS slash, collapse multiple slashes into one, trim trailing slashes
        /// </summary>
        /// <param name="aString">A string</param>
        /// <returns>The normalized string</returns>
        internal static string Normalize(string aString)
        {
            if (string.IsNullOrEmpty(aString))
            {
                return aString;
            }

            var sb = new StringBuilder(aString.Length);
            var index = 0;

            // preserve meaningful roots and their slashes
            if (aString.Length >= 2 && IsValidDriveChar(aString[0]) && aString[1] == ':')
            {
                sb.Append(aString[0]);
                sb.Append(aString[1]);

                var i = SkipCharacters(aString, 2, c => IsSlash(c));

                if (index != i)
                {
                    sb.Append('\\');
                }

                index = i;
            }
            else if (aString.StartsWith("/", StringComparison.Ordinal))
            {
                sb.Append('/');
                index = SkipCharacters(aString, 1, c => IsSlash(c));
            }
            else if (aString.StartsWith(@"\\", StringComparison.Ordinal))
            {
                sb.Append(@"\\");
                index = SkipCharacters(aString, 2, c => IsSlash(c));
            }
            else if (aString.StartsWith(@"\", StringComparison.Ordinal))
            {
                sb.Append(@"\");
                index = SkipCharacters(aString, 1, c => IsSlash(c));
            }

            while (index < aString.Length)
            {
                var afterSlashesIndex = SkipCharacters(aString, index, c => IsSlash(c));

                // do not append separator at the end of the string
                if (afterSlashesIndex >= aString.Length)
                {
                    break;
                }
                // replace multiple slashes with the OS separator
                else if (afterSlashesIndex > index)
                {
                    sb.Append(s_directorySeparator);
                }

                var afterNonSlashIndex = SkipCharacters(aString, afterSlashesIndex, c => !IsSlash(c));

                sb.Append(aString, afterSlashesIndex, afterNonSlashIndex - afterSlashesIndex);

                index = afterNonSlashIndex;
            }

            return sb.ToString();
        }

        private static bool IsSlash(char c) => c == '/' || c == '\\';

        /// <summary>
        /// Skips characters that satisfy the condition <param name="jumpOverCharacter"></param>
        /// </summary>
        /// <param name="aString">The working string</param>
        /// <param name="startingIndex">Offset in string to start the search in</param>
        /// <returns>First index that does not satisfy the condition. Returns the string's length if end of string is reached</returns>
        private static int SkipCharacters(string aString, int startingIndex, Func<char, bool> jumpOverCharacter)
        {
            var index = startingIndex;

            while (index < aString.Length && jumpOverCharacter(aString[index]))
            {
                index++;
            }

            return index;
        }

        // copied from https://github.com/dotnet/corefx/blob/master/src/Common/src/System/IO/PathInternal.Windows.cs#L77-L83
        /// <summary>
        /// Returns true if the given character is a valid drive letter
        /// </summary>
        internal static bool IsValidDriveChar(char value)
        {
            return ((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z'));
        }

        static string[] CreateArrayWithSingleItemIfNotExcluded(string filespecUnescaped, IEnumerable<string> excludeSpecsUnescaped)
        {
            if (excludeSpecsUnescaped != null)
            {
                foreach (string excludeSpec in excludeSpecsUnescaped)
                {

                    // Try a path equality check first to:
                    // - avoid the expensive regex
                    // - maintain legacy behaviour where an illegal filespec is treated as a normal string
                    if (FileUtilities.PathsEqual(filespecUnescaped, excludeSpec))
                    {
                        return Array.Empty<string>();
                    }

                    var match = Default.FileMatch(excludeSpec, filespecUnescaped);

                    if (match.isLegalFileSpec && match.isMatch)
                    {
                        return Array.Empty<string>();
                    }
                }
            }
            return new[] { filespecUnescaped };
        }

        /// <summary>
        /// Given a filespec, find the files that match. 
        /// Will never throw IO exceptions: if there is no match, returns the input verbatim.
        /// </summary>
        /// <param name="projectDirectoryUnescaped">The project directory.</param>
        /// <param name="filespecUnescaped">Get files that match the given file spec.</param>
        /// <param name="excludeSpecsUnescaped">Exclude files that match this file spec.</param>
        /// <returns>The array of files.</returns>
        private string[] GetFilesImplementation(
            string projectDirectoryUnescaped,
            string filespecUnescaped,
            IEnumerable<string> excludeSpecsUnescaped)
        {
            // UNDONE (perf): Short circuit the complex processing when we only have a path and a wildcarded filename

            /*
             * Analyze the file spec and get the information we need to do the matching.
             */
            bool stripProjectDirectory;
            RecursionState state;
            var action = GetFileSearchData(projectDirectoryUnescaped, filespecUnescaped,
                out stripProjectDirectory, out state);

            if (action == SearchAction.ReturnEmptyList)
            {
                return Array.Empty<string>();
            }
            else if (action == SearchAction.ReturnFileSpec)
            {
                return CreateArrayWithSingleItemIfNotExcluded(filespecUnescaped, excludeSpecsUnescaped);
            }
            else if (action != SearchAction.RunSearch)
            {
                //  This means the enum value wasn't valid (or a new one was added without updating code correctly)
                throw new NotSupportedException(action.ToString());
            }

            List<RecursionState> searchesToExclude = null;

            //  Exclude searches which will become active when the recursive search reaches their BaseDirectory.
            //  The BaseDirectory of the exclude search is the key for this dictionary.
            Dictionary<string, List<RecursionState>> searchesToExcludeInSubdirs = null;

            HashSet<string> resultsToExclude = null;
            if (excludeSpecsUnescaped != null)
            {
                searchesToExclude = new List<RecursionState>();
                foreach (string excludeSpec in excludeSpecsUnescaped)
                {
                    //  This is ignored, we always use the include pattern's value for stripProjectDirectory
                    bool excludeStripProjectDirectory;

                    RecursionState excludeState;
                    var excludeAction = GetFileSearchData(projectDirectoryUnescaped, excludeSpec,
                        out excludeStripProjectDirectory, out excludeState);

                    if (excludeAction == SearchAction.ReturnFileSpec)
                    {
                        if (resultsToExclude == null)
                        {
                            resultsToExclude = new HashSet<string>();
                        }
                        resultsToExclude.Add(excludeSpec);

                        continue;
                    }
                    else if (excludeAction == SearchAction.ReturnEmptyList)
                    {
                        //  Nothing to do
                        continue;
                    }
                    else if (excludeAction != SearchAction.RunSearch)
                    {
                        //  This means the enum value wasn't valid (or a new one was added without updating code correctly)
                        throw new NotSupportedException(excludeAction.ToString());
                    }

                    var excludeBaseDirectory = excludeState.BaseDirectory;
                    var includeBaseDirectory = state.BaseDirectory;

                    if (string.Compare(excludeBaseDirectory, includeBaseDirectory, StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        //  What to do if the BaseDirectory for the exclude search doesn't match the one for inclusion?
                        //  - If paths don't match (one isn't a prefix of the other), then ignore the exclude search.  Examples:
                        //      - c:\Foo\ - c:\Bar\
                        //      - c:\Foo\Bar\ - C:\Foo\Baz\
                        //      - c:\Foo\ - c:\Foo2\
                        if (excludeBaseDirectory.Length == includeBaseDirectory.Length)
                        {
                            //  Same length, but different paths.  Ignore this exclude search
                            continue;
                        }
                        else if (excludeBaseDirectory.Length > includeBaseDirectory.Length)
                        {
                            if (!excludeBaseDirectory.StartsWith(includeBaseDirectory, StringComparison.OrdinalIgnoreCase))
                            {
                                //  Exclude path is longer, but doesn't start with include path.  So ignore it.
                                continue;
                            }

                            //  - The exclude BaseDirectory is somewhere under the include BaseDirectory. So
                            //    keep the exclude search, but don't do any processing on it while recursing until the baseDirectory
                            //    in the recursion matches the exclude BaseDirectory.  Examples:
                            //      - Include - Exclude
                            //      - C:\git\msbuild\ - c:\git\msbuild\obj\
                            //      - C:\git\msbuild\ - c:\git\msbuild\src\Common\

                            if (searchesToExcludeInSubdirs == null)
                            {
                                searchesToExcludeInSubdirs = new Dictionary<string, List<RecursionState>>(StringComparer.OrdinalIgnoreCase);
                            }
                            List<RecursionState> listForSubdir;
                            if (!searchesToExcludeInSubdirs.TryGetValue(excludeBaseDirectory, out listForSubdir))
                            {
                                listForSubdir = new List<RecursionState>();

                                searchesToExcludeInSubdirs[excludeBaseDirectory] = listForSubdir;
                            }
                            listForSubdir.Add(excludeState);
                        }
                        else
                        {
                            //  Exclude base directory length is less than include base directory length.
                            if (!state.BaseDirectory.StartsWith(excludeState.BaseDirectory, StringComparison.OrdinalIgnoreCase))
                            {
                                //  Include path is longer, but doesn't start with the exclude path.  So ignore exclude path
                                //  (since it won't match anything under the include path)
                                continue;
                            }

                            //  Now check the wildcard part
                            if (excludeState.RemainingWildcardDirectory.Length == 0)
                            {
                                //  The wildcard part is empty, so ignore the exclude search, as it's looking for files non-recursively
                                //  in a folder higher up than the include baseDirectory.
                                //  Example: include="c:\git\msbuild\src\Framework\**\*.cs" exclude="c:\git\msbuild\*.cs"
                                continue;
                            }
                            else if (IsRecursiveDirectoryMatch(excludeState.RemainingWildcardDirectory))
                            {
                                //  The wildcard part is exactly "**\", so the exclude pattern will apply to everything in the include
                                //  pattern, so simply update the exclude's BaseDirectory to be the same as the include baseDirectory
                                //  Example: include="c:\git\msbuild\src\Framework\**\*.*" exclude="c:\git\msbuild\**\*.bak"
                                excludeState.BaseDirectory = state.BaseDirectory;
                                searchesToExclude.Add(excludeState);
                            }
                            else
                            {
                                //  The wildcard part is non-empty and not "**\", so we will need to match it with a Regex.  Fortunately
                                //  these conditions mean that it needs to be matched with a Regex anyway, so here we will update the
                                //  BaseDirectory to be the same as the exclude BaseDirectory, and change the wildcard part to be "**\"
                                //  because we don't know where the different parts of the exclude wildcard part would be matched.
                                //  Example: include="c:\git\msbuild\src\Framework\**\*.*" exclude="c:\git\msbuild\**\bin\**\*.*"
                                Debug.Assert(excludeState.SearchData.RegexFileMatch != null, "Expected Regex to be used for exclude file matching");
                                excludeState.BaseDirectory = state.BaseDirectory;
                                excludeState.RemainingWildcardDirectory = recursiveDirectoryMatch + s_directorySeparator;
                                searchesToExclude.Add(excludeState);
                            }
                        }
                    }
                    else
                    {
                        searchesToExclude.Add(excludeState);
                    }
                }
            }

            if (searchesToExclude != null && searchesToExclude.Count == 0)
            {
                searchesToExclude = null;
            }

            /*
             * Even though we return a string[] we work internally with a ConcurrentStack.
             * This is because it's cheaper to add items to a ConcurrentStack and this code
             * might potentially do a lot of that.
             */
            var listOfFiles = new ConcurrentStack<List<string>>();

            /*
             * Now get the files that match, starting at the lowest fixed directory.
             */
            try
            {
                // Setup the values for calculating the MaxDegreeOfParallelism option of Parallel.ForEach
                // Set to use only half processors when we have 4 or more of them, in order to not be too aggresive
                // By setting MaxTasksPerIteration to the maximum amount of tasks, which means that only one
                // Parallel.ForEach will run at once, we get a stable number of threads being created.
                var maxTasks = Math.Max(1, Environment.ProcessorCount / 2);
                var taskOptions = new TaskOptions(maxTasks)
                {
                    AvailableTasks = maxTasks,
                    MaxTasksPerIteration = maxTasks
                };
                GetFilesRecursive(
                    listOfFiles,
                    state,
                    projectDirectoryUnescaped,
                    stripProjectDirectory,
                    searchesToExclude,
                    searchesToExcludeInSubdirs,
                    taskOptions);
            }
            // Catch exceptions that are thrown inside the Parallel.ForEach
            catch (AggregateException ex)
            {
                // Flatten to get exceptions than are thrown inside a nested Parallel.ForEach
                if (ex.Flatten().InnerExceptions.All(ExceptionHandling.IsIoRelatedException))
                {
                    return CreateArrayWithSingleItemIfNotExcluded(filespecUnescaped, excludeSpecsUnescaped);
                }
                throw;
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                // Assume it's not meant to be a path
                return CreateArrayWithSingleItemIfNotExcluded(filespecUnescaped, excludeSpecsUnescaped);
            }

            /*
             * Build the return array.
             */
            var files = resultsToExclude != null
                ? listOfFiles.SelectMany(list => list).Where(f => !resultsToExclude.Contains(f)).ToArray()
                : listOfFiles.SelectMany(list => list).ToArray();

            return files;
        }

        private static bool IsRecursiveDirectoryMatch(string path) => path.TrimTrailingSlashes() == recursiveDirectoryMatch;
    }
}
