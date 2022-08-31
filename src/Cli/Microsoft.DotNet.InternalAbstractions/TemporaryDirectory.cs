// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.EnvironmentAbstractions;
using System.IO;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.InternalAbstractions
{
    internal class TemporaryDirectory : ITemporaryDirectory
    {
        public string DirectoryPath { get; }

        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(FileUtilities.CreateTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(DirectoryPath);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(DirectoryPath, true);
            }
            catch
            {
                // Ignore failures here.
            }
        }
    }
}
