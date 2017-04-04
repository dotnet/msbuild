// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Archive;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    public class NuGetPackagesArchiver : INuGetPackagesArchiver
    {
        public string NuGetPackagesArchive =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "nuGetPackagesArchive.lzma"));

        public void ExtractArchive(string archiveDestination)
        {
            var progress = new ConsoleProgressReport();
            var archive = new IndexedArchive();

            archive.Extract(NuGetPackagesArchive, archiveDestination, progress);
        }
    }
}
