// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Internal.ProjectModel.Utilities;
using Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing;
using Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Abstractions;

namespace Microsoft.DotNet.Internal.ProjectModel.Files
{
    internal class IncludeFilesResolver
    {
        public static IEnumerable<IncludeEntry> GetIncludeFiles(IncludeContext context, string targetBasePath, IList<DiagnosticMessage> diagnostics)
        {
            return GetIncludeFiles(context, targetBasePath, diagnostics, flatten: false);
        }

        public static IEnumerable<IncludeEntry> GetIncludeFiles(
            IncludeContext context,
            string targetBasePath,
            IList<DiagnosticMessage> diagnostics,
            bool flatten)
        {
            var sourceBasePath = PathUtility.EnsureTrailingSlash(context.SourceBasePath);
            targetBasePath = PathUtility.GetPathWithDirectorySeparator(targetBasePath);

            var includeEntries = new HashSet<IncludeEntry>();

            // Check for illegal characters in target path
            if (string.IsNullOrEmpty(targetBasePath))
            {
                diagnostics?.Add(new DiagnosticMessage(
                    ErrorCodes.NU1003,
                    $"Invalid '{context.Option}' section. The target '{targetBasePath}' is invalid, " +
                    "targets must either be a file name or a directory suffixed with '/'. " +
                    "The root directory of the package can be specified by using a single '/' character.",
                    sourceBasePath,
                    DiagnosticMessageSeverity.Error));
            }
            else if (targetBasePath.Split(Path.DirectorySeparatorChar).Any(s => s.Equals(".") || s.Equals("..")))
            {
                diagnostics?.Add(new DiagnosticMessage(
                    ErrorCodes.NU1004,
                    $"Invalid '{context.Option}' section. " +
                    $"The target '{targetBasePath}' contains path-traversal characters ('.' or '..'). " +
                    "These characters are not permitted in target paths.",
                    sourceBasePath,
                    DiagnosticMessageSeverity.Error));
            }
            else
            {
                var files = GetIncludeFilesCore(
                    sourceBasePath,
                    context.IncludePatterns,
                    context.ExcludePatterns,
                    context.IncludeFiles,
                    context.BuiltInsInclude,
                    context.BuiltInsExclude).ToList();

                var isFile = targetBasePath[targetBasePath.Length - 1] != Path.DirectorySeparatorChar;
                if (isFile && files.Count > 1)
                {
                    // It's a file. But the glob matched multiple things
                    diagnostics?.Add(new DiagnosticMessage(
                        ErrorCodes.NU1005,
                        $"Invalid '{ProjectFilesCollection.PackIncludePropertyName}' section. " +
                        $"The target '{targetBasePath}' refers to a single file, but the corresponding pattern " +
                        "produces multiple files. To mark the target as a directory, suffix it with '/'.",
                        sourceBasePath,
                        DiagnosticMessageSeverity.Error));
                }
                else if (isFile && files.Count > 0)
                {
                    var filePath = Path.GetFullPath(
                        Path.Combine(sourceBasePath, PathUtility.GetPathWithDirectorySeparator(files[0].Path)));

                    includeEntries.Add(new IncludeEntry(targetBasePath, filePath));
                }
                else if (!isFile)
                {
                    targetBasePath = targetBasePath.Substring(0, targetBasePath.Length - 1);

                    foreach (var file in files)
                    {
                        var fullPath = Path.GetFullPath(
                            Path.Combine(sourceBasePath, PathUtility.GetPathWithDirectorySeparator(file.Path)));
                        string targetPath;

                        if (flatten)
                        {
                            targetPath = Path.Combine(targetBasePath, PathUtility.GetPathWithDirectorySeparator(file.Stem));
                        }
                        else
                        {
                            targetPath = Path.Combine(
                                targetBasePath,
                                PathUtility.GetRelativePathIgnoringDirectoryTraversals(sourceBasePath, fullPath));
                        }

                        includeEntries.Add(new IncludeEntry(targetPath, fullPath));
                    }
                }

                if (context.IncludeFiles != null)
                {
                    foreach (var literalRelativePath in context.IncludeFiles)
                    {
                        var fullPath = Path.GetFullPath(Path.Combine(sourceBasePath, literalRelativePath));
                        string targetPath;

                        if (isFile)
                        {
                            targetPath = targetBasePath;
                        }
                        else if (flatten)
                        {
                            targetPath =  Path.Combine(targetBasePath, Path.GetFileName(fullPath));
                        }
                        else
                        {
                            targetPath = Path.Combine(targetBasePath, PathUtility.GetRelativePath(sourceBasePath, fullPath));
                        }

                        includeEntries.Add(new IncludeEntry(targetPath, fullPath));
                    }
                }

                if (context.ExcludeFiles != null)
                {
                    var literalExcludedFiles = new HashSet<string>(
                        context.ExcludeFiles.Select(file => Path.GetFullPath(Path.Combine(sourceBasePath, file))),
                        StringComparer.Ordinal);

                    includeEntries.RemoveWhere(entry => literalExcludedFiles.Contains(entry.SourcePath));
                }
            }
            
            if (context.Mappings != null)
            {
                // Finally add all the mappings
                foreach (var map in context.Mappings)
                {
                    var targetPath = Path.Combine(targetBasePath, PathUtility.GetPathWithDirectorySeparator(map.Key));

                    foreach (var file in GetIncludeFiles(map.Value, targetPath, diagnostics, flatten: true))
                    {
                        file.IsCustomTarget = true;

                        // Prefer named targets over default ones
                        includeEntries.RemoveWhere(f => string.Equals(f.SourcePath, file.SourcePath) && !f.IsCustomTarget);
                        includeEntries.Add(file);
                    }
                }
            }

            return includeEntries;
        }

        private static IEnumerable<FilePatternMatch> GetIncludeFilesCore(
            string sourceBasePath,
            List<string> includePatterns,
            List<string> excludePatterns,
            List<string> includeFiles,
            List<string> builtInsInclude,
            List<string> builtInsExclude)
        {
            var literalIncludedFiles = new List<string>();

            if (includeFiles != null)
            {
                // literal included files are added at the last, but the search happens early
                // so as to make the process fail early in case there is missing file. fail early
                // helps to avoid unnecessary globing for performance optimization
                foreach (var literalRelativePath in includeFiles)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(sourceBasePath, literalRelativePath));

                    if (!File.Exists(fullPath))
                    {
                        throw new InvalidOperationException(string.Format("Can't find file {0}", literalRelativePath));
                    }

                    literalIncludedFiles.Add(fullPath);
                }
            }

            // Globbing
            var matcher = new Matcher();
            if (builtInsInclude != null)
            {
                matcher.AddIncludePatterns(builtInsInclude);
            }
            if (includePatterns != null)
            {
                matcher.AddIncludePatterns(includePatterns);
            }
            if (builtInsExclude != null)
            {
                matcher.AddExcludePatterns(builtInsExclude);
            }
            if (excludePatterns != null)
            {
                matcher.AddExcludePatterns(excludePatterns);
            }

            return matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(sourceBasePath))).Files;
        }
    }
}
