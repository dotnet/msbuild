// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Factory for <see cref="IFileSystemAbstraction"/>
    /// </summary>
    internal static class FileSystems
    {
        public static IFileSystemAbstraction Default = GetFileSystem();

        private static IFileSystemAbstraction GetFileSystem()
        {
            if (NativeMethodsShared.IsWindows)
            {
                return MSBuildOnWindowsFileSystem.Singleton();
            }
            else
            {
                return ManagedFileSystem.Singleton();
            }
        }
    }
}
