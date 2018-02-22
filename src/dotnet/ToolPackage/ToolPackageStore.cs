using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal class ToolPackageStore : IToolPackageStore
    {
        public ToolPackageStore(DirectoryPath root)
        {
            Root = root;
        }

        public DirectoryPath Root { get; private set; }

        public IEnumerable<IToolPackage> GetInstalledPackages(string packageId = null)
        {
            if (packageId != null)
            {
                return EnumerateVersions(packageId);
            }

            return EnumerateAllPackages().SelectMany(p => p);
        }

        private IEnumerable<IEnumerable<IToolPackage>> EnumerateAllPackages()
        {
            if (!Directory.Exists(Root.Value))
            {
                yield break;
            }

            foreach (var subdirectory in Directory.EnumerateDirectories(Root.Value))
            {
                var packageId = Path.GetFileName(subdirectory);
                if (packageId == ToolPackageInstaller.StagingDirectory)
                {
                    continue;
                }

                yield return EnumerateVersions(packageId);
            }
        }

        private IEnumerable<IToolPackage> EnumerateVersions(string packageId)
        {
            var packageRootDirectory = Root.WithSubDirectories(packageId);
            if (!Directory.Exists(packageRootDirectory.Value))
            {
                yield break;
            }

            foreach (var subdirectory in Directory.EnumerateDirectories(packageRootDirectory.Value))
            {
                var version = Path.GetFileName(subdirectory);
                yield return new ToolPackageInstance(
                    this,
                    packageId,
                    version,
                    packageRootDirectory.WithSubDirectories(version));
            }
        }
    }
}
