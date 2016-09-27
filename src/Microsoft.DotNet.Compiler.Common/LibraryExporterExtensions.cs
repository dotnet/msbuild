// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectModel.Compilation;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public static class LibraryExporterExtensions
    {
        public static IEnumerable<LibraryExport> GetAllProjectTypeDependencies(this LibraryExporter exporter)
        {
            return
                exporter.GetDependencies(LibraryType.Project);
        }

        public static void CopyTo(this IEnumerable<LibraryAsset> assets, string destinationPath)
        {
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            foreach (var asset in assets)
            {
                var file = Path.Combine(destinationPath, Path.GetFileName(asset.ResolvedPath));
                File.Copy(asset.ResolvedPath, file, overwrite: true);
                RemoveFileAttribute(file, FileAttributes.ReadOnly);
            }
        }

        public static void StructuredCopyTo(this IEnumerable<LibraryAsset> assets, string destinationPath, string tempLocation)
        {
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            foreach (var asset in assets)
            {
                var targetName = ResolveTargetName(destinationPath, asset);
                var transformedFile = asset.GetTransformedFile(tempLocation);

                File.Copy(transformedFile, targetName, overwrite: true);
                RemoveFileAttribute(targetName, FileAttributes.ReadOnly);
            }
        }
        
        private static void RemoveFileAttribute(String file, FileAttributes attribute)
        {
            if (File.Exists(file))
            {
                var fileAttributes = File.GetAttributes(file);
                if ((fileAttributes & attribute) == attribute)
                {
                    File.SetAttributes(file, fileAttributes & ~attribute);
                }
            }
        }

        private static string ResolveTargetName(string destinationPath, LibraryAsset asset)
        {
            string targetName;
            if (!string.IsNullOrEmpty(asset.RelativePath))
            {
                targetName = Path.Combine(destinationPath, asset.RelativePath);
                var destinationAssetPath = Path.GetDirectoryName(targetName);

                if (!Directory.Exists(destinationAssetPath))
                {
                    Directory.CreateDirectory(destinationAssetPath);
                }
            }
            else
            {
                targetName = Path.Combine(destinationPath, Path.GetFileName(asset.ResolvedPath));
            }
            return targetName;
        }
    }
}
