// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Globbing
{
    /// <summary>
    /// Implementation of IMSBuildGlob that uses Microsoft.Extensions.FileSystemGlobbing
    /// for compatibility evaluation with the existing MSBuildGlob implementation.
    /// </summary>
    public class FileSystemGlobbingMSBuildGlob : IMSBuildGlob
    {
        private readonly Matcher _matcher;
        private readonly string _globRoot;

        /// <summary>
        /// Constructor for FileSystemGlobbingMSBuildGlob
        /// </summary>
        /// <param name="globRoot">The root directory for the glob</param>
        /// <param name="fileSpec">The file specification pattern</param>
        internal FileSystemGlobbingMSBuildGlob(string globRoot, string fileSpec)
        {
            ErrorUtilities.VerifyThrowArgumentNull(globRoot);
            ErrorUtilities.VerifyThrowArgumentNull(fileSpec);

            _globRoot = globRoot;

            _matcher = new Matcher();
            _matcher.AddInclude(fileSpec);
        }

        /// <summary>
        /// See <see cref="IMSBuildGlob.IsMatch"/>.
        /// </summary>
        /// <param name="stringToMatch">The string to match against the glob pattern</param>
        /// <returns>True if the string matches the pattern, false otherwise</returns>
        public bool IsMatch(string stringToMatch)
        {
            ErrorUtilities.VerifyThrowArgumentNull(stringToMatch);

            try
            {
                // Normalize the path for comparison
                string normalizedPath = FileUtilities.NormalizePath(stringToMatch);

                // Handle relative vs absolute paths similar to MSBuildGlob
                if (!Path.IsPathRooted(normalizedPath))
                {
                    // Make it relative to the glob root
                    string fullPath = Path.GetFullPath(Path.Combine(_globRoot, normalizedPath));
                    
                    // Check if the resolved path is under the glob root
                    string normalizedGlobRoot = FileUtilities.NormalizePath(_globRoot);
                    if (!fullPath.StartsWith(normalizedGlobRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    // Get the relative path from the glob root using a compatible method
                    normalizedPath = FileUtilities.MakeRelative(normalizedGlobRoot, fullPath);
                }
                else
                {
                    // For absolute paths, make them relative to the glob root
                    string normalizedGlobRoot = FileUtilities.NormalizePath(_globRoot);
                    if (!normalizedPath.StartsWith(normalizedGlobRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    normalizedPath = FileUtilities.MakeRelative(normalizedGlobRoot, normalizedPath);
                }

                // Use the matcher to test the pattern
                return _matcher.Match(normalizedPath).HasMatches;
            }
            catch (Exception)
            {
                // If there are any path-related exceptions, return false
                // This matches the behavior of MSBuildGlob which returns false for invalid paths
                return false;
            }
        }

        /// <summary>
        /// Creates a new FileSystemGlobbingMSBuildGlob from the given parameters
        /// </summary>
        /// <param name="globRoot">The root directory for the glob</param>
        /// <param name="fileSpec">The file specification pattern</param>
        /// <returns>A new FileSystemGlobbingMSBuildGlob instance</returns>
        public static FileSystemGlobbingMSBuildGlob Parse(string globRoot, string fileSpec)
        {
            ErrorUtilities.VerifyThrowArgumentNull(globRoot);
            ErrorUtilities.VerifyThrowArgumentNull(fileSpec);
            ErrorUtilities.VerifyThrowArgumentInvalidPath(globRoot, nameof(globRoot));

            if (string.IsNullOrEmpty(globRoot))
            {
                globRoot = Directory.GetCurrentDirectory();
            }

            globRoot = FileUtilities.NormalizePath(globRoot).WithTrailingSlash();

            return new FileSystemGlobbingMSBuildGlob(globRoot, fileSpec);
        }

        /// <summary>
        /// Creates a new FileSystemGlobbingMSBuildGlob using the current directory as the root
        /// </summary>
        /// <param name="fileSpec">The file specification pattern</param>
        /// <returns>A new FileSystemGlobbingMSBuildGlob instance</returns>
        public static FileSystemGlobbingMSBuildGlob Parse(string fileSpec)
        {
            return Parse(string.Empty, fileSpec);
        }
    }
}