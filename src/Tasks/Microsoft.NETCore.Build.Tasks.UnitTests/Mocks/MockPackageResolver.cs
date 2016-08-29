using NuGet.Versioning;
using System.IO;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    public class MockPackageResolver : IPackageResolver
    {
        public string GetPackageDirectory(string packageId, NuGetVersion version)
        {
            return Path.Combine("/root", packageId, version.ToNormalizedString(), "path");
        }
    }
}