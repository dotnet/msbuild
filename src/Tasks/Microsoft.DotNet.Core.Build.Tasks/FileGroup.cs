// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Core.Build.Tasks
{
    /// <summary>
    /// Values for File Group Metadata corresponding to the groups in a target library
    /// </summary>
    internal enum FileGroup
    {
        CompileTimeAssembly,
        RuntimeAssembly,
        ContentFile,
        NativeLibrary,
        ResourceAssembly,
        RuntimeTarget,
        FrameworkAssembly
    }

    internal static class FileGroupExtensions
    {
        /// <summary>
        /// Return Type metadata that should be applied to files in the target library group 
        /// </summary>
        internal static string GetTypeMetadata(this FileGroup fileGroup)
        {
            switch (fileGroup)
            {
                case FileGroup.CompileTimeAssembly:
                case FileGroup.RuntimeAssembly:
                case FileGroup.NativeLibrary:
                case FileGroup.ResourceAssembly:
                case FileGroup.RuntimeTarget:
                    return "assembly";

                case FileGroup.FrameworkAssembly:
                    return "frameworkAssembly";

                case FileGroup.ContentFile:
                    return "content";

                default:
                    return null;
            }
        }

        /// <summary>
        /// Return a list of file paths from the corresponding group in the target library
        /// </summary>
        internal static IEnumerable<string> GetFilePathListFor(this FileGroup fileGroup, LockFileTargetLibrary package)
        {
            switch (fileGroup)
            {
                case FileGroup.CompileTimeAssembly:
                    return SelectPath(package.CompileTimeAssemblies);

                case FileGroup.RuntimeAssembly:
                    return SelectPath(package.RuntimeAssemblies);

                case FileGroup.ContentFile:
                    return package.ContentFiles.Select(c => c.Path);

                case FileGroup.NativeLibrary:
                    return SelectPath(package.NativeLibraries);

                case FileGroup.ResourceAssembly:
                    return SelectPath(package.ResourceAssemblies);

                case FileGroup.RuntimeTarget:
                    return package.RuntimeTargets.Select(c => c.Path);

                case FileGroup.FrameworkAssembly:
                    return package.FrameworkAssemblies;

                default:
                    throw new Exception($"Unexpected file group in project.lock.json target library {package.Name}");
            }
        }

        private static IEnumerable<string> SelectPath(IList<LockFileItem> fileItemList)
            => fileItemList.Select(c => c.Path);
    }
}
