using System.Collections.Generic;
using Microsoft.Extensions.ProjectModel.Graph;

namespace Microsoft.Extensions.ProjectModel
{
    public class PackageDescription : LibraryDescription
    {
        public PackageDescription(
            LibraryRange requestedRange, 
            string path,
            LockFilePackageLibrary package, 
            LockFileTargetLibrary lockFileLibrary, 
            IEnumerable<LibraryRange> dependencies,
            bool compatible)
            : base(
                  requestedRange,
                  new LibraryIdentity(package.Name, package.Version, LibraryType.Package),
                  path,
                  dependencies: dependencies,
                  framework: null,
                  resolved: true,
                  compatible: compatible)
        {
            Library = package;
            Target = lockFileLibrary;
        }

        public LockFileTargetLibrary Target { get; set; }
        public LockFilePackageLibrary Library { get; set; }
    }
}
