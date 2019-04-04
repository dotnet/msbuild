// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    internal static class FileSystemExtensions
    {
        public static void CreateIfNotExists(this IFileSystem fileSystem, string filePath)
        {
            var parentDirectory = Path.GetDirectoryName(filePath);
            if (!fileSystem.File.Exists(filePath))
            {
                if (!fileSystem.Directory.Exists(parentDirectory))
                {
                    fileSystem.Directory.CreateDirectory(parentDirectory);
                }

                fileSystem.File.CreateEmptyFile(filePath);
            }
        }
    }
}
