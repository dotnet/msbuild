using System;
using System.Runtime.Loader;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Utilities
{
    internal static class VersionUtility
    {
        public static NuGetVersion GetAssemblyVersion(string path)
        {
//#if NET451
//            return new NuGetVersion(AssemblyName.GetAssemblyName(path).Version);
//#else
            return new NuGetVersion(AssemblyLoadContext.GetAssemblyName(path).Version);
//#endif
        }
    }
}
