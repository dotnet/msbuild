// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Factory for <see cref="IFileSystem"/>
    /// </summary>
    internal static class FileSystems
    {
        public static IFileSystem Default = GetFileSystem();

        private static IFileSystem GetFileSystem()
        {
#if CLR2COMPATIBILITY
            return MSBuildTaskHostFileSystem.Singleton();
#else
            if (NativeMethodsShared.IsWindows)
            {
                return MSBuildOnWindowsFileSystem.Singleton();
            }
            else
            {
                return ManagedFileSystem.Singleton();
            }
#endif
        }
    }
}
