// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WINDOWSINTEROP
using Microsoft.Build.Framework;
#endif

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Factory for <see cref="IFileSystem"/>
    /// </summary>
    internal static class FileSystems
    {
        public static IFileSystem Default = GetFileSystem();

        internal static bool SupportsDirectoryCache(IFileSystem fileSystem)
        {
            while (fileSystem is CachingFileSystemWrapper wrapper)
            {
                fileSystem = wrapper.UnderlyingFileSystem;
            }

#if FEATURE_WINDOWSINTEROP
            if (NativeMethods.IsWindows && fileSystem is MSBuildOnWindowsFileSystem or WindowsFileSystem)
            {
                return true;
            }
#endif
            return fileSystem.GetType() == typeof(ManagedFileSystem);
        }

        private static IFileSystem GetFileSystem()
        {
#if FEATURE_WINDOWSINTEROP
            if (NativeMethods.IsWindows)
            {
                return MSBuildOnWindowsFileSystem.Singleton();
            }
            else
#endif
            {
                return ManagedFileSystem.Singleton();
            }
        }
    }
}
