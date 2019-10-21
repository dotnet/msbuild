// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal class PackageLocation
    {
        public PackageLocation(
            FilePath? nugetConfig = null,
            DirectoryPath? rootConfigDirectory = null, 
            string[] additionalFeeds = null)
        {
            NugetConfig = nugetConfig;
            RootConfigDirectory = rootConfigDirectory;
            AdditionalFeeds = additionalFeeds ?? Array.Empty<string>();
        }

        public FilePath? NugetConfig { get; }
        public DirectoryPath? RootConfigDirectory { get; }
        public string[] AdditionalFeeds { get; }
    }
}
