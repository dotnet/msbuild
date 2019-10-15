
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
