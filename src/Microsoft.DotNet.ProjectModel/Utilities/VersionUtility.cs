using System;
using System.Runtime.Loader;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Utilities
{
    internal static class VersionUtility
    {
        public static NuGetVersion GetAssemblyVersion(string path)
        {
            return new NuGetVersion(AssemblyLoadContext.GetAssemblyName(path).Version);
        }
    }
}
