using System.Reflection;
using System.Runtime.Versioning;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli
{
    internal static class BundledTargetFramework
    {
        public static string GetTargetFrameworkMoniker()
        {
            TargetFrameworkAttribute targetFrameworkAttribute = typeof(BundledTargetFramework)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute<TargetFrameworkAttribute>();

            return NuGetFramework
                .Parse(targetFrameworkAttribute.FrameworkName)
                .GetShortFolderName();
        }
    }
}
