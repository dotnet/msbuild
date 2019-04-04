// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    internal static class FileSystemExtensions
    {
        public static void CreateIfNotExists(this IFileSystem fileSystem, string filePath)
        {
            // retry if there is 2 CLI process trying to create file (for example sentinel file)
            // at the same time
            FileAccessRetrier.RetryOnIOException(() =>
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
            });
        }
    }
}
