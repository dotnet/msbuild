// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.ProjectModel;

namespace Microsoft.DotNet.ToolPackage
{
    internal class LockFileMatcher
    {
        /// <summary>
        /// Check if LockFileItem matches the targetRelativeFilePath.
        /// The path in LockFileItem is in pattern tools/TFM/RID/my/tool.dll. Tools/TFM/RID is selected by NuGet.
        /// And there will be only one TFM/RID combination.
        /// When "my/tools.dll" part matches exactly with the targetRelativeFilePath, return true.
        /// </summary>
        /// <param name="lockFileItem">LockFileItem from asset.json restored from temp project</param>
        /// <param name="targetRelativeFilePath">file path relative to tools/TFM/RID</param>
        internal static bool MatchesFile(LockFileItem lockFileItem, string targetRelativeFilePath)
        {
            string[] pathInLockFilePathInArray = SplitPathByDirectorySeparator(lockFileItem.Path);
            string[] entryPointPathInArray = SplitPathByDirectorySeparator(targetRelativeFilePath);

            return entryPointPathInArray.Length >= 1
                   && PathInLockFileDirectoriesStartWithToolsAndFollowsTwoSubFolder(
                       pathInLockFilePathInArray,
                       entryPointPathInArray)
                   && SubPathMatchesTargetFilePath(pathInLockFilePathInArray, entryPointPathInArray);
        }

        /// <summary>
        /// Check if LockFileItem is under targetRelativePath directory.
        /// The path in LockFileItem is in pattern tools/TFM/RID/my/tool.dll. Tools/TFM/RID is selected by NuGet.
        /// And there will be only one TFM/RID combination.
        /// When "my/folder/of/tool/tools.dll" part under targetRelativePath "my/folder/of" or "my/folder", return true.
        /// </summary>
        internal static bool MatchesDirectoryPath(LockFileItem lockFileItem, string targetRelativePath)
        {
            string[] pathInLockFilePathInArray = SplitPathByDirectorySeparator(lockFileItem.Path);
            string[] targetDirectoryPathInArray = SplitPathByDirectorySeparator(targetRelativePath);

            return pathInLockFilePathInArray[0] == "tools"
                   && SubPathMatchesTargetFilePath(pathInLockFilePathInArray, targetDirectoryPathInArray);
        }

        private static bool SubPathMatchesTargetFilePath(string[] pathInLockFilePathInArray, string[] targetInArray)
        {
            string[] pathAfterToolsTfmRid = pathInLockFilePathInArray.Skip(3).ToArray();
            return !targetInArray
                .Where((directoryOnEveryLevel, i) => directoryOnEveryLevel != pathAfterToolsTfmRid[i])
                .Any();
        }

        private static bool PathInLockFileDirectoriesStartWithToolsAndFollowsTwoSubFolder(
            string[] pathInLockFilePathInArray,
            string[] targetInArray)
        {
            if (pathInLockFilePathInArray.Length - targetInArray.Length != 3)
            {
                return false;
            }

            if (pathInLockFilePathInArray[0] != "tools")
            {
                return false;
            }

            return true;
        }

        private static string[] SplitPathByDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new string[0];
            }

            return path.Split('\\', '/');
        }
    }
}
