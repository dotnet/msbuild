// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Files
{
    public class ContentFiles
    {
        private readonly ProjectContext _context;

        public ContentFiles(ProjectContext context)
        {
            _context = context;
        }

        public void StructuredCopyTo(string targetDirectory)
        {
            var sourceFiles = _context
                .ProjectFile
                .Files
                .GetContentFiles();

            var sourceDirectory = _context.ProjectDirectory;

            if (sourceFiles == null)
            {
                throw new ArgumentNullException(nameof(sourceFiles));
            }

            sourceDirectory = EnsureTrailingSlash(sourceDirectory);
            targetDirectory = EnsureTrailingSlash(targetDirectory);

            var pathMap = sourceFiles
                .ToDictionary(s => s,
                    s => Path.Combine(targetDirectory,
                        PathUtility.GetRelativePathIgnoringDirectoryTraversals(sourceDirectory, s)));

            foreach (var targetDir in pathMap.Values
                .Select(Path.GetDirectoryName)
                .Distinct()
                .Where(t => !Directory.Exists(t)))
            {
                Directory.CreateDirectory(targetDir);
            }

            foreach (var sourceFilePath in pathMap.Keys)
            {
                File.Copy(
                    sourceFilePath,
                    pathMap[sourceFilePath],
                    overwrite: true);
            }

            RemoveAttributeFromFiles(pathMap.Values, FileAttributes.ReadOnly);
        }

        public void StructuredCopyTo(string targetDirectory, IEnumerable<IncludeEntry> includeEntries)
        {
            if (includeEntries == null)
            {
                return;
            }

            foreach (var targetDir in includeEntries
                .Select(f => Path.GetDirectoryName(f.TargetPath))
                .Distinct()
                .Where(t => !Directory.Exists(t)))
            {
                Directory.CreateDirectory(targetDir);
            }

            foreach (var file in includeEntries)
            {
                File.Copy(file.SourcePath, file.TargetPath, overwrite: true);
            }

            RemoveAttributeFromFiles(includeEntries.Select(f => f.TargetPath), FileAttributes.ReadOnly);
        }

        private static void RemoveAttributeFromFiles(IEnumerable<string> files, FileAttributes attribute)
        {
            foreach (var file in files)
            {
                var fileAttributes = File.GetAttributes(file);
                if ((fileAttributes & attribute) == attribute)
                {
                    File.SetAttributes(file, fileAttributes & ~attribute);
                }
            }
        }

        private static string EnsureTrailingSlash(string path)
        {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
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
    }
}
