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

        public IEnumerable<IToolPackage> GetInstalledPackages(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

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
