// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Common
{
    public static class PathUtility
    {
        public static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), "_._", StringComparison.Ordinal);
        }

        public static bool IsChildOfDirectory(string dir, string candidate)
        {
            if (dir == null)
            {
                throw new ArgumentNullException(nameof(dir));
            }
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }
            dir = Path.GetFullPath(dir);
            dir = EnsureTrailingSlash(dir);
            candidate = Path.GetFullPath(candidate);
            return candidate.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }

        public static string EnsureTrailingSlash(string path)
        {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        public static string EnsureTrailingForwardSlash(string path)
        {
            return EnsureTrailingCharacter(path, '/');
        }

        private static string EnsureTrailingCharacter(string path, char trailingCharacter)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            // if the path is empty, we want to return the original string instead of a single trailing character.
            if (path.Length == 0 || path[path.Length - 1] == trailingCharacter)
            {
                return path;
            }

            return path + trailingCharacter;
        }

        public static string EnsureNoTrailingDirectorySeparator(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                char lastChar = path[path.Length - 1];
                if (lastChar == Path.DirectorySeparatorChar)
                {
                    path = path.Substring(0, path.Length - 1);
                }
            }

            return path;
        }

        public static void EnsureParentDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);

            EnsureDirectoryExists(directory);
        }

        public static void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        public static bool TryDeleteDirectory(string directoryPath)
        {
            try
            {
                Directory.Delete(directoryPath, true);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns childItem relative to directory, with Path.DirectorySeparatorChar as separator
        /// </summary>
        public static string GetRelativePath(DirectoryInfo directory, FileSystemInfo childItem)
        {
            var path1 = EnsureTrailingSlash(directory.FullName);

            var path2 = childItem.FullName;

            return GetRelativePath(path1, path2, Path.DirectorySeparatorChar, true);
        }

        /// <summary>
        /// Returns path2 relative to path1, with Path.DirectorySeparatorChar as separator
        /// </summary>
        public static string GetRelativePath(string path1, string path2)
        {
            if (!Path.IsPathRooted(path1) || !Path.IsPathRooted(path2))
            {
                throw new ArgumentException("both paths need to be rooted/full path");
            }

            return GetRelativePath(path1, path2, Path.DirectorySeparatorChar, true);
        }

        /// <summary>
        /// Returns path2 relative to path1, with Path.DirectorySeparatorChar as separator but ignoring directory
        /// traversals.
        /// </summary>
        public static string GetRelativePathIgnoringDirectoryTraversals(string path1, string path2)
        {
            return GetRelativePath(path1, path2, Path.DirectorySeparatorChar, false);
        }

        /// <summary>
        /// Returns path2 relative to path1, with given path separator
        /// </summary>
        public static string GetRelativePath(string path1, string path2, char separator, bool includeDirectoryTraversals)
        {
            if (string.IsNullOrEmpty(path1))
            {
                throw new ArgumentException("Path must have a value", nameof(path1));
            }

            if (string.IsNullOrEmpty(path2))
            {
                throw new ArgumentException("Path must have a value", nameof(path2));
            }

            StringComparison compare;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                compare = StringComparison.OrdinalIgnoreCase;
                // check if paths are on the same volume
                if (!string.Equals(Path.GetPathRoot(path1), Path.GetPathRoot(path2), compare))
                {
                    // on different volumes, "relative" path is just path2
                    return path2;
                }
            }
            else
            {
                compare = StringComparison.Ordinal;
            }

            var index = 0;
            var path1Segments = path1.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var path2Segments = path2.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // if path1 does not end with / it is assumed the end is not a directory
            // we will assume that is isn't a directory by ignoring the last split
            var len1 = path1Segments.Length - 1;
            var len2 = path2Segments.Length;

            // find largest common absolute path between both paths
            var min = Math.Min(len1, len2);
            while (min > index)
            {
                if (!string.Equals(path1Segments[index], path2Segments[index], compare))
                {
                    break;
                }
                // Handle scenarios where folder and file have same name (only if os supports same name for file and directory)
                // e.g. /file/name /file/name/app
                else if ((len1 == index && len2 > index + 1) || (len1 > index && len2 == index + 1))
                {
                    break;
                }
                ++index;
            }

            var path = "";

            // check if path2 ends with a non-directory separator and if path1 has the same non-directory at the end
            if (len1 + 1 == len2 && !string.IsNullOrEmpty(path1Segments[index]) &&
                string.Equals(path1Segments[index], path2Segments[index], compare))
            {
                return path;
            }

            if (includeDirectoryTraversals)
            {
                for (var i = index; len1 > i; ++i)
                {
                    path += ".." + separator;
                }
            }

            for (var i = index; len2 - 1 > i; ++i)
            {
                path += path2Segments[i] + separator;
            }
            // if path2 doesn't end with an empty string it means it ended with a non-directory name, so we add it back
            if (!string.IsNullOrEmpty(path2Segments[len2 - 1]))
            {
                path += path2Segments[len2 - 1];
            }

            return path;
        }

        [Obsolete("Use System.IO.Path.GetFullPath(string, string) instead, or PathUtility.GetFullPath(string) if the base path is the current working directory.")]
        public static string GetAbsolutePath(string basePath, string relativePath)
        {
            if (basePath == null)
            {
                throw new ArgumentNullException(nameof(basePath));
            }

            if (relativePath == null)
            {
                throw new ArgumentNullException(nameof(relativePath));
            }

            Uri resultUri = new(new Uri(basePath), new Uri(relativePath, UriKind.Relative));
            return resultUri.LocalPath;
        }

        public static string GetDirectoryName(string path)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return path.Substring(Path.GetDirectoryName(path).Length).Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string GetPathWithForwardSlashes(string path)
        {
            return path.Replace('\\', '/');
        }

        public static string GetPathWithBackSlashes(string path)
        {
            return path.Replace('/', '\\');
        }

        public static string GetPathWithDirectorySeparator(string path)
        {
            if (Path.DirectorySeparatorChar == '/')
            {
                return GetPathWithForwardSlashes(path);
            }
            else
            {
                return GetPathWithBackSlashes(path);
            }
        }

        public static string RemoveExtraPathSeparators(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var components = path.Split(Path.DirectorySeparatorChar);
            var result = string.Empty;

            foreach (var component in components)
            {
                if (string.IsNullOrEmpty(component))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(result))
                {
                    result = component;

                    // On Windows, manually append a separator for drive references because Path.Combine won't do so
                    if (result.EndsWith(":") && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        result += Path.DirectorySeparatorChar;
                    }
                }
                else
                {
                    result = Path.Combine(result, component);
                }
            }

            if (path[path.Length - 1] == Path.DirectorySeparatorChar)
            {
                result += Path.DirectorySeparatorChar;
            }

            return result;
        }

        public static bool HasExtension(this string filePath, string extension)
        {
            var comparison = StringComparison.Ordinal;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                comparison = StringComparison.OrdinalIgnoreCase;
            }

            return Path.GetExtension(filePath).Equals(extension, comparison);
        }

        /// <summary>
        /// Gets the fully-qualified path without failing if the
        /// path is empty.
        /// </summary>
        public static string GetFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return Path.GetFullPath(path);
        }

        public static void EnsureAllPathsExist(
            IReadOnlyCollection<string> paths,
            string pathDoesNotExistLocalizedFormatString,
            bool allowDirectories = false)
        {
            var notExisting = new List<string>();

            foreach (var p in paths)
            {
                if (!File.Exists(p) && (!allowDirectories || !Directory.Exists(p)))
                {
                    notExisting.Add(p);
                }
            }

            if (notExisting.Count > 0)
            {
                throw new GracefulException(
                    string.Join(
                        Environment.NewLine,
                        notExisting.Select(p => string.Format(pathDoesNotExistLocalizedFormatString, p))));
            }
        }

        public static bool IsDirectory(this string path) =>
            File.GetAttributes(path).HasFlag(FileAttributes.Directory);
    }
}
