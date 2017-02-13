// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Globalization;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// Functions for matching file names with patterns. 
    /// </summary>
    /// <owner>JomoF</owner>
    internal static class FileMatcher
    {
        private const string recursiveDirectoryMatch = "**";
        private const string dotdot = "..";
        private static readonly string directorySeparator = new string(Path.DirectorySeparatorChar,1);
        private static readonly string altDirectorySeparator = new string(Path.AltDirectorySeparatorChar,1);

        private static readonly char[] wildcardCharacters = { '*', '?' };
        internal static readonly char[] directorySeparatorCharacters = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private static readonly GetFileSystemEntries defaultGetFileSystemEntries = new GetFileSystemEntries(GetAccessibleFileSystemEntries);
        private static readonly DirectoryExists defaultDirectoryExists = new DirectoryExists(Directory.Exists);

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
        /// <returns>The array of filesystem entries.</returns>
        internal delegate string[] GetFileSystemEntries(FileSystemEntity entityType, string path, string pattern, string projectDirectory, bool stripProjectDirectory);

        /// <summary>
        /// Returns true if the directory exists and is not a file, otherwise false.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the directory exists.</returns>
        internal delegate bool DirectoryExists(string path);


        /// <summary>
        /// Determines whether the given path has any wild card characters.
        /// </summary>
        /// <param name="filespec"></param>
        /// <returns></returns>
        internal static bool HasWildcards(string filespec)
        {
            return -1 != filespec.IndexOfAny(wildcardCharacters);
        }

        /// <summary>
        /// Get the files and\or folders specified by the given path and pattern.
        /// </summary>
        /// <param name="entityType">Whether Files, Directories or both.</param>
        /// <param name="path">The path to search.</param>
        /// <param name="pattern">The pattern to search.</param>
        /// <param name="projectDirectory">The directory for the project within which the call is made</param>
        /// <param name="stripProjectDirectory">If true the project directory should be stripped</param>
        /// <returns></returns>
        private static string[] GetAccessibleFileSystemEntries(FileSystemEntity entityType, string path, string pattern, string projectDirectory, bool stripProjectDirectory)
        {
            string[] files = null;
            switch (entityType)
            {
                case FileSystemEntity.Files: files = GetAccessibleFiles(path, pattern, projectDirectory, stripProjectDirectory); break;
                case FileSystemEntity.Directories: files = GetAccessibleDirectories(path, pattern); break;
                case FileSystemEntity.FilesAndDirectories: files = GetAccessibleFilesAndDirectories(path, pattern); break;
                default:
                    ErrorUtilities.VerifyThrow(false, "Unexpected filesystem entity type.");
                    break;
            }

            return files;
        }

        /// <summary>
        /// Returns an array of file system entries matching the specified search criteria. Inaccessible or non-existent file
        /// system entries are skipped.
        /// </summary>
        /// <owner>SumedhK,JomoF</owner>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <returns>Array of matching file system entries (can be empty).</returns>
        private static string[] GetAccessibleFilesAndDirectories(string path, string pattern)
        {
            string[] entries = null;

            if (Directory.Exists(path))
            {
                try
                {
                    entries = Directory.GetFileSystemEntries(path, pattern);
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

            if (entries == null)
            {
                entries = new string[0];
            }

            return entries;
        }

        /// <summary>
        /// Same as Directory.GetFiles(...) except that files that
        /// aren't accessible are skipped instead of throwing an exception.
        /// 
        /// Other exceptions are passed through.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="filespec">The pattern.</param>
        /// <param name="projectDirectory">The project directory</param>
        /// <param name="stripProjectDirectory"></param>
        /// <returns>Files that can be accessed.</returns>
        private static string[] GetAccessibleFiles
        (
            string path,
            string filespec,     // can be null
            string projectDirectory,
            bool   stripProjectDirectory
        )
        {
            try
            {
                // look in current directory if no path specified
                string dir = ((path.Length == 0) ? ".\\" : path);

                // get all files in specified directory, unless a file-spec has been provided
                string[] files = (filespec == null)
                    ? Directory.GetFiles(dir)
                    : Directory.GetFiles(dir, filespec);

                // If the Item is based on a relative path we need to strip
                // the current directory from the front
                if (stripProjectDirectory)
                {
                    RemoveProjectDirectory(files, projectDirectory);
                }
                // Files in the current directory are coming back with a ".\"
                // prepended to them.  We need to remove this; it breaks the
                // IDE, which expects just the filename if it is in the current
                // directory.  But only do this if the original path requested
                // didn't itself contain a ".\".
                else if (!path.StartsWith(".\\", StringComparison.Ordinal))
                {
                    RemoveInitialDotSlash(files);
                }

                return files;
            }
            catch (System.Security.SecurityException)
            {
                // For code access security.
                return new string[0];
            }
            catch (System.UnauthorizedAccessException)
            {
                // For OS security.
                return new string[0];
            }
        }

        /// <summary>
        /// Same as Directory.GetDirectories(...) except that files that
        /// aren't accessible are skipped instead of throwing an exception.
        /// 
        /// Other exceptions are passed through.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pattern">Pattern to match</param>
        /// <returns>Accessible directories.</returns>
        private static string[] GetAccessibleDirectories
        (
            string path,
            string pattern
        )
        {
            try
            {
                string[] directories = null;

                if (pattern == null)
                {
                    directories = Directory.GetDirectories((path.Length == 0) ? ".\\" : path);
                }
                else
                {
                    directories = Directory.GetDirectories((path.Length == 0) ? ".\\" : path, pattern);
                }

                // Subdirectories in the current directory are coming back with a ".\"
                // prepended to them.  We need to remove this; it breaks the
                // IDE, which expects just the filename if it is in the current
                // directory.  But only do this if the original path requested
                // didn't itself contain a ".\".
                if (!path.StartsWith(".\\", StringComparison.Ordinal))
                {
                    RemoveInitialDotSlash(directories);
                }

                return directories;
            }
            catch (System.Security.SecurityException)
            {
                // For code access security.
                return new string[0];
            }
            catch (System.UnauthorizedAccessException)
            {
                // For OS security.
                return new string[0];
            }
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
            int startingElement=0;

            bool isUnc = path.StartsWith(directorySeparator + directorySeparator, StringComparison.Ordinal);
            if (isUnc)
            {
                pathRoot = directorySeparator + directorySeparator;
                pathRoot += parts[2];
                pathRoot += directorySeparator;
                pathRoot += parts[3];
                pathRoot += directorySeparator;
                startingElement = 4;
            }
            else
            {
                // Is it relative?
                if (path.Length>2 && path[1] == ':')
                {
                    // Not relative
                    pathRoot = parts[0] + directorySeparator;
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
                    longParts[i-startingElement] = String.Empty;
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
                        string[] entries = getFileSystemEntries(FileSystemEntity.FilesAndDirectories, longPath, parts[i], null, false);

                        if (0 == entries.Length)
                        {
                            // The next part doesn't exist. Therefore, no more of the path will exist.
                            // Just return the rest.
                            for (int j = i; j<parts.Length; ++j)
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

            return pathRoot + String.Join (directorySeparator, longParts);
        }

        /// <summary>
        /// Given a filespec, split it into left-most 'fixed' dir part, middle 'wildcard' dir part, and filename part.
        /// The filename part may have wildcard characters in it.
        /// </summary>
        /// <param name="filespec">The filespec to be decomposed.</param>
        /// <param name="fixedDirectoryPart">Receives the fixed directory part.</param>
        /// <param name="wildcardDirectoryPart">The wildcard directory part.</param>
        /// <param name="filenamePart">The filename part.</param>
        /// <param name="getFileSystemEntries">Delegate.</param>
        internal static void SplitFileSpec
        (
            string filespec,
            out string fixedDirectoryPart,
            out string wildcardDirectoryPart,
            out string filenamePart,
            GetFileSystemEntries getFileSystemEntries
        )
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
                wildcardDirectoryPart += directorySeparator;
                filenamePart = "*.*";
            }

            fixedDirectoryPart = FileMatcher.GetLongPathName(fixedDirectoryPart, getFileSystemEntries);
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

            int indexOfFirstWildcard = filespec.IndexOfAny(wildcardCharacters);
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
                fixedDirectoryPart = filespec.Substring (0, indexOfLastDirectorySeparator + 1);
                wildcardDirectoryPart = String.Empty;
                filenamePart = filespec.Substring (indexOfLastDirectorySeparator + 1);
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
                wildcardDirectoryPart = filespec.Substring (0, indexOfLastDirectorySeparator + 1);
                filenamePart = filespec.Substring (indexOfLastDirectorySeparator + 1);
                return;
            }

            /*
             * There is at least one wildcard and one dir separator, split parts out.
             */
            fixedDirectoryPart = filespec.Substring(0, indexOfSeparatorBeforeWildCard+1);
            wildcardDirectoryPart = filespec.Substring(indexOfSeparatorBeforeWildCard+1, indexOfLastDirectorySeparator-indexOfSeparatorBeforeWildCard);
            filenamePart = filespec.Substring(indexOfLastDirectorySeparator+1);
        }

        /// <summary>
        /// Removes the leading ".\" from all of the paths in the array. 
        /// </summary>
        /// <param name="paths">Paths to remove .\ from.</param>
        private static void RemoveInitialDotSlash
        (
            string[] paths
        )
        {
            for (int i=0; i < paths.Length; i++)
            {
                if (paths[i].StartsWith(".\\", StringComparison.Ordinal))
                {
                    paths[i] = paths[i].Substring(2);
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
        internal static void RemoveProjectDirectory
        (
            string[] paths,
            string projectDirectory
        )
        {
            bool directoryLastCharIsSeparator = IsDirectorySeparator(projectDirectory[projectDirectory.Length - 1]);
             for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].StartsWith(projectDirectory, StringComparison.Ordinal))
                {
                    // If the project directory did not end in a slash we need to check to see if the next char in the path is a slash
                    if (!directoryLastCharIsSeparator)
                    {
                        //If the next char after the project directory is not a slash, skip this path
                        if (paths[i].Length <= projectDirectory.Length || !IsDirectorySeparator(paths[i][projectDirectory.Length]))
                        {
                            continue;
                        }
                        paths[i] = paths[i].Substring(projectDirectory.Length + 1);
                    }
                    else
                    {
                        paths[i] = paths[i].Substring(projectDirectory.Length);
                    }
                }
            }
        }

        /// <summary>
        /// Get all files that match either the file-spec or the regular expression. 
        /// </summary>
        /// <param name="listOfFiles">List of files that gets populated.</param>
        /// <param name="baseDirectory">The path to enumerate</param>
        /// <param name="remainingWildcardDirectory">The remaining, wildcard part of the directory.</param>
        /// <param name="filespec">The filespec.</param>
        /// <param name="extensionLengthToEnforce"></param>
        /// <param name="regexFileMatch">Wild-card matching.</param>
        /// <param name="needsRecursion">If true, then recursion is required.</param>
        /// <param name="projectDirectory"></param>
        /// <param name="stripProjectDirectory"></param>
        /// <param name="getFileSystemEntries">Delegate.</param>
        private static void GetFilesRecursive
        (
            System.Collections.IList listOfFiles,
            string baseDirectory,
            string remainingWildcardDirectory,
            string filespec,                // can be null
            int extensionLengthToEnforce,   // only relevant when filespec is not null
            Regex regexFileMatch,           // can be null
            bool needsRecursion,
            string projectDirectory,
            bool   stripProjectDirectory,
            GetFileSystemEntries getFileSystemEntries
        )
        {
            Debug.Assert((filespec == null) || (regexFileMatch == null),
                "File-spec overrides the regular expression -- pass null for file-spec if you want to use the regular expression.");

            ErrorUtilities.VerifyThrow((filespec != null) || (regexFileMatch != null),
                "Need either a file-spec or a regular expression to match files.");

            ErrorUtilities.VerifyThrow(remainingWildcardDirectory!=null, "Expected non-null remaning wildcard directory.");

            /*
             * Get the matching files.
             */
            bool considerFiles = false;

            // Only consider files if...
            if (remainingWildcardDirectory.Length == 0)
            {
                // We've reached the end of the wildcard directory elements.
                considerFiles = true;
            }
            else if (remainingWildcardDirectory.IndexOf(recursiveDirectoryMatch, StringComparison.Ordinal) == 0)
            {
                // or, we've reached a "**" so everything else is matched recursively.
                considerFiles = true;
            }

            if (considerFiles)
            {
                string[] files = getFileSystemEntries(FileSystemEntity.Files, baseDirectory, filespec, projectDirectory, stripProjectDirectory);
                foreach (string file in files)
                {
                    if ((filespec != null) ||
                        // if no file-spec provided, match the file to the regular expression
                        // PERF NOTE: Regex.IsMatch() is an expensive operation, so we avoid it whenever possible
                        regexFileMatch.IsMatch(file))
                    {
                        if ((filespec == null) ||
                            // if we used a file-spec with a "loosely" defined extension
                            (extensionLengthToEnforce == 0) ||
                            // discard all files that do not have extensions of the desired length
                            (Path.GetExtension(file).Length == extensionLengthToEnforce))
                        {
                            listOfFiles.Add((object)file);
                        }
                    }
                }
            }

            /*
             * Recurse into subdirectories.
             */
            if (needsRecursion && remainingWildcardDirectory.Length>0)
            {
                // Find the next directory piece.
                string pattern = null;

                if (remainingWildcardDirectory != recursiveDirectoryMatch)
                {
                    int indexOfNextSlash = remainingWildcardDirectory.IndexOfAny(directorySeparatorCharacters);
                    ErrorUtilities.VerifyThrow(indexOfNextSlash != -1, "Slash should be guaranteed.");

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
                    pattern = remainingWildcardDirectory.Substring(0, indexOfNextSlash);
                    remainingWildcardDirectory = remainingWildcardDirectory.Substring(indexOfNextSlash + 1);

                    // If pattern turned into **, then there's no choice but to enumerate everything.
                    if (pattern == recursiveDirectoryMatch)
                    {
                        pattern = null;
                        remainingWildcardDirectory = recursiveDirectoryMatch;
                    }
                }

                // We never want to strip the project directory from the leaves, because the current 
                // process directory maybe different
                string[] subdirs = getFileSystemEntries(FileSystemEntity.Directories, baseDirectory, pattern, null, false);
                foreach (string subdir in subdirs)
                {
                    GetFilesRecursive(listOfFiles, subdir, remainingWildcardDirectory, filespec, extensionLengthToEnforce, regexFileMatch, true, projectDirectory, stripProjectDirectory, getFileSystemEntries);
                }
            }
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
            matchFileExpression.Replace(directorySeparator, "<:dirseparator:>");
            matchFileExpression.Replace(altDirectorySeparator, "<:dirseparator:>");

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
        /// <param name="getFileSystemEntries">Delegate.</param>
        internal static void GetFileSpecInfo
        (
            string filespec,
            out Regex regexFileMatch,
            out bool needsRecursion,
            out bool isLegalFileSpec,
            GetFileSystemEntries getFileSystemEntries

        )
        {
            string fixedDirectoryPart;
            string wildcardDirectoryPart;
            string filenamePart;
            string matchFileExpression;

            GetFileSpecInfo(filespec,
                out fixedDirectoryPart, out wildcardDirectoryPart, out filenamePart,
                out matchFileExpression, out needsRecursion, out isLegalFileSpec,
                getFileSystemEntries);

            if (isLegalFileSpec)
            {
                regexFileMatch = new Regex(matchFileExpression, RegexOptions.IgnoreCase);
            }
            else
            {
                regexFileMatch = null;
            }
        }

        /// <summary>
        /// Given a filespec, get the information needed for file matching.
        /// </summary>
        /// <param name="filespec">The filespec.</param>
        /// <param name="fixedDirectoryPart">Receives the fixed directory part.</param>
        /// <param name="wildcardDirectoryPart">Receives the wildcard directory part.</param>
        /// <param name="filenamePart">Receives the filename part.</param>
        /// <param name="matchFileExpression">Receives the regular expression.</param>
        /// <param name="needsRecursion">Receives the flag that is true if recursion is required.</param>
        /// <param name="isLegalFileSpec">Receives the flag that is true if the filespec is legal.</param>
        /// <param name="getFileSystemEntries">Delegate.</param>
        private static void GetFileSpecInfo
        (
            string filespec,
            out string fixedDirectoryPart,
            out string wildcardDirectoryPart,
            out string filenamePart,
            out string matchFileExpression,
            out bool needsRecursion,
            out bool isLegalFileSpec,
            GetFileSystemEntries getFileSystemEntries
        )
        {
            isLegalFileSpec = true;
            needsRecursion = false;
            fixedDirectoryPart = String.Empty;
            wildcardDirectoryPart = String.Empty;
            filenamePart = String.Empty;
            matchFileExpression = null;

            // bail out if filespec contains illegal characters
            if (-1 != filespec.IndexOfAny(Path.GetInvalidPathChars()))
            {
                isLegalFileSpec = false;
                return;
            }

            /*
             * Check for patterns in the filespec that are explicitly illegal.
             * 
             * Any path with "..." in it is illegal.
             */
            if (-1 != filespec.IndexOf("...", StringComparison.Ordinal))
            {
                isLegalFileSpec = false;
                return;
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
                isLegalFileSpec = false;
                return;
            }

            /*
             * Now break up the filespec into constituent parts--fixed, wildcard and filename.
             */
            SplitFileSpec(filespec, out fixedDirectoryPart, out wildcardDirectoryPart, out filenamePart, getFileSystemEntries);

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

            internal bool isLegalFileSpec = false;
            internal bool isMatch = false;
            internal bool isFileSpecRecursive = false;
            internal string fixedDirectoryPart = String.Empty;
            internal string wildcardDirectoryPart = String.Empty;
            internal string filenamePart = String.Empty;
        }

        /// <summary>
        /// Given a pattern (filespec) and a candidate filename (fileToMatch)
        /// return matching information.
        /// </summary>
        /// <param name="filespec">The filespec.</param>
        /// <param name="fileToMatch">The candidate to match against.</param>
        /// <returns>The result class.</returns>
        internal static Result FileMatch
        (
            string filespec,
            string fileToMatch
        )
        {
            return FileMatch(filespec, fileToMatch, defaultGetFileSystemEntries);
        }

        /// <summary>
        /// Given a pattern (filespec) and a candidate filename (fileToMatch)
        /// return matching information.
        /// </summary>
        /// <param name="filespec">The filespec.</param>
        /// <param name="fileToMatch">The candidate to match against.</param>
        /// <param name="getFileSystemEntries">Delegate.</param>
        /// <returns>The result class.</returns>
        internal static Result FileMatch
        (
            string filespec,
            string fileToMatch,
            GetFileSystemEntries getFileSystemEntries
        )
        {
            Result matchResult = new Result();

            fileToMatch = GetLongPathName(fileToMatch, getFileSystemEntries);

            Regex regexFileMatch;
            GetFileSpecInfo
            (
                filespec,
                out regexFileMatch,
                out matchResult.isFileSpecRecursive,
                out matchResult.isLegalFileSpec,
                getFileSystemEntries
            );

            if (matchResult.isLegalFileSpec)
            {
                Match match = regexFileMatch.Match(fileToMatch);
                matchResult.isMatch = match.Success;

                if (matchResult.isMatch)
                {
                    matchResult.fixedDirectoryPart = match.Groups["FIXEDDIR"].Value;
                    matchResult.wildcardDirectoryPart = match.Groups["WILDCARDDIR"].Value;
                    matchResult.filenamePart = match.Groups["FILENAME"].Value;
                }
            }

            return matchResult;
        }

        /// <summary>
        /// Given a filespec, find the files that match. 
        /// </summary>
        /// <param name="filespec">Get files that match the given file spec.</param>
        /// <returns>The array of files.</returns>
        internal static string[] GetFiles
        (
            string projectDirectory,
            string filespec
        )
        {
            string[] files = GetFiles(projectDirectory, filespec, defaultGetFileSystemEntries, defaultDirectoryExists);
            return files;
        }

        /// <summary>
        /// Given a filespec, find the files that match. 
        /// </summary>
        /// <param name="filespec">Get files that match the given file spec.</param>
        /// <param name="getFileSystemEntries">Get files that match the given file spec.</param>
        /// <param name="directoryExists">Determine whether a directory exists.</param>
        /// <returns>The array of files.</returns>
        internal static string[] GetFiles
        (
            string projectDirectory,
            string filespec,
            GetFileSystemEntries getFileSystemEntries,
            DirectoryExists directoryExists
        )
        {
            // For performance. Short-circuit iff there is no wildcard.
            // Perf Note: Doing a [Last]IndexOfAny(...) is much faster than compiling a
            // regular expression that does the same thing, regardless of whether
            // filespec contains one of the characters.
            // Choose LastIndexOfAny instead of IndexOfAny because it seems more likely
            // that wildcards will tend to be towards the right side.
            if (!HasWildcards(filespec))
            {
                return new string[] { filespec };
            }

            /*
             * Even though we return a string[] we work internally with an IList.
             * This is because it's cheaper to add items to an IList and this code
             * might potentially do a lot of that.
             */
            System.Collections.ArrayList arrayListOfFiles = new System.Collections.ArrayList();
            System.Collections.IList listOfFiles = (System.Collections.IList) arrayListOfFiles;

            /*
             * Analyze the file spec and get the information we need to do the matching.
             */
            string fixedDirectoryPart;
            string wildcardDirectoryPart;
            string filenamePart;
            string matchFileExpression;
            bool needsRecursion;
            bool isLegalFileSpec;
            GetFileSpecInfo
            (
                filespec,
                out fixedDirectoryPart,
                out wildcardDirectoryPart,
                out filenamePart,
                out matchFileExpression,
                out needsRecursion,
                out isLegalFileSpec,
                getFileSystemEntries
            );

            /*
             * If the filespec is invalid, then just return now.
             */
            if (!isLegalFileSpec)
            {
                return new string[] { filespec };
            }

            // The projectDirectory is not null only if we are running the evaluation from
            // inside the engine (i.e. not from a task)
            bool stripProjectDirectory = false;
            if (projectDirectory != null)
            {
                if (fixedDirectoryPart != null)
                {
                    string oldFixedDirectoryPart = fixedDirectoryPart;
                    try
                    {
                        fixedDirectoryPart = Path.Combine(projectDirectory, fixedDirectoryPart);
                    }
                    catch (ArgumentException)
                    {
                        return new string[0];
                    }

                    stripProjectDirectory = !String.Equals(fixedDirectoryPart, oldFixedDirectoryPart, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    fixedDirectoryPart = projectDirectory;
                    stripProjectDirectory = true;
                }
            }

            /*
             * If the fixed directory part doesn't exist, then this means no files should be
             * returned.
             */
            if (fixedDirectoryPart.Length > 0 && !directoryExists(fixedDirectoryPart))
            {
                return new string[0];
            }

            // determine if we need to use the regular expression to match the files
            // PERF NOTE: Constructing a Regex object is expensive, so we avoid it whenever possible
            bool matchWithRegex =
                // if we have a directory specification that uses wildcards, and
                (wildcardDirectoryPart.Length > 0) &&
                // the specification is not a simple "**"
                (wildcardDirectoryPart != (recursiveDirectoryMatch + directorySeparator));
                // then we need to use the regular expression

            // if we're not using the regular expression, get the file pattern extension
            string extensionPart = matchWithRegex
                ? null
                : Path.GetExtension(filenamePart);

            // check if the file pattern would cause Windows to match more loosely on the extension
            // NOTE: Windows matches loosely in two cases (in the absence of the * wildcard in the extension):
            // 1) if the extension ends with the ? wildcard, it matches files with shorter extensions also e.g. "file.tx?" would
            //    match both "file.txt" and "file.tx"
            // 2) if the extension is three characters, and the filename contains the * wildcard, it matches files with longer
            //    extensions that start with the same three characters e.g. "*.htm" would match both "file.htm" and "file.html"
            bool needToEnforceExtensionLength =
                    (extensionPart != null) &&
                    (extensionPart.IndexOf('*') == -1)
                &&
                    (extensionPart.EndsWith("?", StringComparison.Ordinal)
                ||
                    ((extensionPart.Length == (3 + 1 /* +1 for the period */)) &&
                    (filenamePart.IndexOf('*') != -1)));

            /*
             * Now get the files that match, starting at the lowest fixed directory.
             */
            GetFilesRecursive(listOfFiles, fixedDirectoryPart, wildcardDirectoryPart,
                // if using the regular expression, ignore the file pattern
                (matchWithRegex ? null : filenamePart), (needToEnforceExtensionLength ? extensionPart.Length : 0),
                // if using the file pattern, ignore the regular expression
                (matchWithRegex ? new Regex(matchFileExpression, RegexOptions.IgnoreCase) : null),
                needsRecursion, projectDirectory, stripProjectDirectory, getFileSystemEntries);

            /*
             * Build the return array.
             */
            string[] files = (string[])arrayListOfFiles.ToArray(typeof(string));
            return files;
        }
    }
}
