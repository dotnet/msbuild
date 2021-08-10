// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if BUILD_ENGINE
using Microsoft.Build.FileSystem;
#endif

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Factory for <see cref="IFileSystem"/>
    /// </summary>
    internal static class FileSystems
    {
        public static IFileSystem Default = GetFileSystem();

#if !CLR2COMPATIBILITY
        public static IDirectoryCache DefaultDirectoryCache = new DirectoryCacheOverFileSystem(Default);
#endif

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
