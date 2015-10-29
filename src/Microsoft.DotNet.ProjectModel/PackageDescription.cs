using System.Collections.Generic;
using Microsoft.Extensions.ProjectModel.Graph;

namespace Microsoft.Extensions.ProjectModel
{
    public class PackageDescription : LibraryDescription
    {
        public PackageDescription(
            string path,
            LockFilePackageLibrary package, 
            LockFileTargetLibrary lockFileLibrary, 
            IEnumerable<LibraryRange> dependencies,
            bool compatible)
            : base(
                  new LibraryIdentity(package.Name, package.Version, LibraryType.Package),
                  path,
                  dependencies: dependencies,
                  framework: null,
                  resolved: compatible,
                  compatible: compatible)
        {
            Library = package;
            Target = lockFileLibrary;
        }

        public LockFileTargetLibrary Target { get; set; }
        public LockFilePackageLibrary Library { get; set; }
    }
}
