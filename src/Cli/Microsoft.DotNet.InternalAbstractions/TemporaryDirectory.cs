// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet;

namespace Microsoft.DotNet.InternalAbstractions
{
    internal class TemporaryDirectory : ITemporaryDirectory
    {
        public string DirectoryPath { get; }

        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(PathUtilities.CreateTempSubdirectory());
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
