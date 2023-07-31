// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    using PathAndPropertiesTuple = Tuple<string, IDictionary<string, string>>;

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
        private static readonly IDictionary<string, string> _emptyProperties = new Dictionary<string, string>();

        /// <summary>
        /// Return Type metadata that should be applied to files in the target library group 
        /// </summary>
        public static string GetTypeMetadata(this FileGroup fileGroup)
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
        public static IEnumerable<PathAndPropertiesTuple> GetFilePathAndProperties(
            this FileGroup fileGroup, LockFileTargetLibrary package)
        {
            switch (fileGroup)
            {
                case FileGroup.CompileTimeAssembly:
                    return SelectPath(package.CompileTimeAssemblies);

                case FileGroup.RuntimeAssembly:
                    return SelectPath(package.RuntimeAssemblies);

                case FileGroup.ContentFile:
                    return SelectPath(package.ContentFiles);

                case FileGroup.NativeLibrary:
                    return SelectPath(package.NativeLibraries);

                case FileGroup.ResourceAssembly:
                    return SelectPath(package.ResourceAssemblies);

                case FileGroup.RuntimeTarget:
                    return SelectPath(package.RuntimeTargets);

                case FileGroup.FrameworkAssembly:
                    return package.FrameworkAssemblies.Select(c => Tuple.Create(c, _emptyProperties));

                default:
                    throw new ArgumentOutOfRangeException(nameof(fileGroup));
            }
        }

        private static IEnumerable<PathAndPropertiesTuple> SelectPath<T>(IList<T> fileItemList) 
            where T : LockFileItem
            => fileItemList.Select(c => Tuple.Create(c.Path, c.Properties));
    }
}
