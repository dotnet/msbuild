// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// 1. Publish PublishRelativePath is a relative <strong>file</strong> path. This need to convert to relative path.
    /// 2. Due to DotnetTools package format, "tools/{TargetFramework}/any/" is needed in addition to the relative path.
    /// </summary>
    public sealed class ResolveToolPackagePaths : TaskBase
    {
        public string AppHostIntermediatePath { get; set; }

        [Required]
        public ITaskItem[] ResolvedFileToPublish { get; set; }

        [Required]
        public string PublishDir { get; set; }

        [Required]
        public string TargetFrameworkMoniker { get; set; }

        public string TargetPlatformMoniker { get; set; }

        [Output]
        public ITaskItem[] ResolvedFileToPublishWithPackagePath { get; private set; }

        private const char ForwardSeparatorChar = '\\';
        private const char BackwardSeparatorChar = '/';

        protected override void ExecuteCore()
        {
            var result = new List<TaskItem>();
            foreach (ITaskItem r in ResolvedFileToPublish)
            {
                // skip packing apphost since a dotnet tool will get a generated shim executable once installed
                if (r.ItemSpec.Equals(AppHostIntermediatePath, StringComparison.Ordinal))
                {
                    continue;
                }

                string relativePath = r.GetMetadata("RelativePath");
                var fullpath = Path.GetFullPath(
                    Path.Combine(PublishDir,
                    relativePath));
                var i = new TaskItem(fullpath);

                var shortFrameworkName = NuGetFramework
                    .ParseComponents(TargetFrameworkMoniker, TargetPlatformMoniker)
                    .GetShortFolderName();

                i.SetMetadata("PackagePath", $"tools/{shortFrameworkName}/any/{GetDirectoryPathInRelativePath(relativePath)}");
                result.Add(i);
            }

            ResolvedFileToPublishWithPackagePath = result.ToArray();
        }

        /// <summary>
        /// Change "dir/file.exe" to "dir"
        /// </summary>
        internal static string GetDirectoryPathInRelativePath(string publishRelativePath)
        {
            publishRelativePath = NormalizeDirectorySeparatorsToUnixStyle(publishRelativePath);
            var index = publishRelativePath.LastIndexOf(BackwardSeparatorChar);
            return index == -1 ? string.Empty : publishRelativePath.Substring(0, index);
        }

        // https://github.com/dotnet/corefx/issues/4208
        // Basic copy paste from corefx. But Normalize to "/" instead of \
        private static string NormalizeDirectorySeparatorsToUnixStyle(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            char current;

            StringBuilder builder = new StringBuilder(path.Length);

            int start = 0;
            if (IsDirectorySeparator(path[start]))
            {
                start++;
                builder.Append(BackwardSeparatorChar);
            }

            for (int i = start; i < path.Length; i++)
            {
                current = path[i];

                // If we have a separator
                if (IsDirectorySeparator(current))
                {
                    // If the next is a separator, skip adding this
                    if (i + 1 < path.Length && IsDirectorySeparator(path[i + 1]))
                    {
                        continue;
                    }

                    // Ensure it is the primary separator
                    current = BackwardSeparatorChar;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool IsDirectorySeparator(char c)
        {
            return c == ForwardSeparatorChar || c == BackwardSeparatorChar;
        }

    }
}
