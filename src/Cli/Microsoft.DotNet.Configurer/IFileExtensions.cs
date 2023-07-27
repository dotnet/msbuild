// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
