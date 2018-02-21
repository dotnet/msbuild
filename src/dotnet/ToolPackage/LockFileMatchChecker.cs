// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
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
                   && PathInLockFileDirectoriesStartWithToolsAndFollowsTwoSubFolder()
                   && SubPathMatchesTargetFilePath();

            bool SubPathMatchesTargetFilePath()
            {
                string[] pathAfterToolsTfmRid = pathInLockFilePathInArray.Skip(3).ToArray();
                return !pathAfterToolsTfmRid
                    .Where((directoryOnEveryLevel, i) => directoryOnEveryLevel != entryPointPathInArray[i])
                    .Any();
            }

            bool PathInLockFileDirectoriesStartWithToolsAndFollowsTwoSubFolder()
            {
                if (pathInLockFilePathInArray.Length - entryPointPathInArray.Length != 3)
                {
                    return false;
                }

                if (pathInLockFilePathInArray[0] != "tools")
                {
                    return false;
                }

                return true;
            }

            string[] SplitPathByDirectorySeparator(string path)
            {
                return path.Split('\\', '/');
            }
        }
    }
}
