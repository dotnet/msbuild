// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Returns paths to the frameworks SDK.
    /// </summary>
    public class GetFrameworkSdkPath : TaskExtension
    {
        #region Properties

        private static string s_path;
        private static string s_version20Path;
        private static string s_version35Path;
        private static string s_version40Path;
        private static string s_version45Path;
        private static string s_version451Path;
        private static string s_version46Path;
        private static string s_version461Path;

        /// <summary>
        /// The path to the latest .NET SDK if it could be found. It will be String.Empty if the SDK was not found.
        /// </summary>
        [Output]
        public string Path
        {
            get
            {
                if (s_path == null)
                {
                    s_path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Latest, VisualStudioVersion.VersionLatest);

                    if (String.IsNullOrEmpty(s_path))
                    {
                        Log.LogMessageFromResources(
                            MessageImportance.High,
                            "GetFrameworkSdkPath.CouldNotFindSDK",
                            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Latest, VisualStudioVersion.VersionLatest),
                            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Latest, VisualStudioVersion.VersionLatest)
                        );

                        s_path = String.Empty;
                    }
                    else
                    {
                        s_path = FileUtilities.EnsureTrailingSlash(s_path);
                        Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", s_path);
                    }
                }

                return s_path;
            }
            set
            {
                // Does nothing; for backwards compatibility only
            }
        }

        /// <summary>
        /// The path to the v2.0 .NET SDK if it could be found. It will be String.Empty if the SDK was not found.
        /// </summary>
        [Output]
        public string FrameworkSdkVersion20Path
        {
            get
            {
                if (s_version20Path == null)
                {
                    s_version20Path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version20);

                    if (String.IsNullOrEmpty(s_version20Path))
                    {
                        Log.LogMessageFromResources(
                            MessageImportance.High,
                            "GetFrameworkSdkPath.CouldNotFindSDK",
                            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version20),
                            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version20)
                        );

                        s_version20Path = String.Empty;
                    }
                    else
                    {
                        s_version20Path = FileUtilities.EnsureTrailingSlash(s_version20Path);
                        Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", s_version20Path);
                    }
                }

                return s_version20Path;
            }
        }

        /// <summary>
        /// The path to the v3.5 .NET SDK if it could be found. It will be String.Empty if the SDK was not found.
        /// </summary>
        [Output]
        public string FrameworkSdkVersion35Path
        {
            get
            {
                if (s_version35Path == null)
                {
                    s_version35Path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest);

                    if (String.IsNullOrEmpty(s_version35Path))
                    {
                        Log.LogMessageFromResources(
                            MessageImportance.High,
                            "GetFrameworkSdkPath.CouldNotFindSDK",
                            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest),
                            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest)
                        );

                        s_version35Path = String.Empty;
                    }
                    else
                    {
                        s_version35Path = FileUtilities.EnsureTrailingSlash(s_version35Path);
                        Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", s_version35Path);
                    }
                }

                return s_version35Path;
            }
        }

        /// <summary>
        /// The path to the v4.0 .NET SDK if it could be found. It will be String.Empty if the SDK was not found.
        /// </summary>
        [Output]
        public string FrameworkSdkVersion40Path
        {
            get
            {
                if (s_version40Path == null)
                {
                    s_version40Path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.VersionLatest);

                    if (String.IsNullOrEmpty(s_version40Path))
                    {
                        Log.LogMessageFromResources(
                            MessageImportance.High,
                            "GetFrameworkSdkPath.CouldNotFindSDK",
                            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.VersionLatest),
                            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.VersionLatest)
                        );

                        s_version40Path = String.Empty;
                    }
                    else
                    {
                        s_version40Path = FileUtilities.EnsureTrailingSlash(s_version40Path);
                        Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", s_version40Path);
                    }
                }

                return s_version40Path;
            }
        }

        /// <summary>
        /// The path to the v4.5 .NET SDK if it could be found. It will be String.Empty if the SDK was not found.
        /// </summary>
        [Output]
        public string FrameworkSdkVersion45Path
        {
            get
            {
                if (s_version45Path == null)
                {
                    s_version45Path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.VersionLatest);

                    if (String.IsNullOrEmpty(s_version45Path))
                    {
                        Log.LogMessageFromResources(
                            MessageImportance.High,
                            "GetFrameworkSdkPath.CouldNotFindSDK",
                            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.VersionLatest),
                            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.VersionLatest)
                        );

                        s_version45Path = String.Empty;
                    }
                    else
                    {
                        s_version45Path = FileUtilities.EnsureTrailingSlash(s_version45Path);
                        Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", s_version45Path);
                    }
                }

                return s_version45Path;
            }
        }

        /// <summary>
        /// The path to the v4.5.1 .NET SDK if it could be found. It will be String.Empty if the SDK was not found.
        /// </summary>
        [Output]
        public string FrameworkSdkVersion451Path
        {
            get
            {
                if (s_version451Path == null)
                {
                    s_version451Path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.VersionLatest);

                    if (String.IsNullOrEmpty(s_version451Path))
                    {
                        Log.LogMessageFromResources(
                            MessageImportance.High,
                            "GetFrameworkSdkPath.CouldNotFindSDK",
                            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.VersionLatest),
                            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.VersionLatest)
                        );

                        s_version451Path = String.Empty;
                    }
                    else
                    {
                        s_version451Path = FileUtilities.EnsureTrailingSlash(s_version451Path);
                        Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", s_version451Path);
                    }
                }

                return s_version451Path;
            }
        }

        /// <summary>
        /// The path to the v4.6 .NET SDK if it could be found. It will be String.Empty if the SDK was not found.
        /// </summary>
        [Output]
        public string FrameworkSdkVersion46Path
        {
            get
            {
                if (s_version46Path == null)
                {
                    s_version46Path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.VersionLatest);

                    if (String.IsNullOrEmpty(s_version46Path))
                    {
                        Log.LogMessageFromResources(
                            MessageImportance.High,
                            "GetFrameworkSdkPath.CouldNotFindSDK",
                            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.VersionLatest),
                            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.VersionLatest)
                        );

                        s_version46Path = String.Empty;
                    }
                    else
                    {
                        s_version46Path = FileUtilities.EnsureTrailingSlash(s_version46Path);
                        Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", s_version46Path);
                    }
                }

                return s_version46Path;
            }
        }

        /// <summary>
        /// The path to the v4.6.1 .NET SDK if it could be found. It will be String.Empty if the SDK was not found.
        /// </summary>
        [Output]
        public string FrameworkSdkVersion461Path
        {
            get
            {
                if (s_version461Path == null)
                {
                    s_version461Path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.VersionLatest);

                    if (String.IsNullOrEmpty(s_version461Path))
                    {
                        Log.LogMessageFromResources(
                            MessageImportance.High,
                            "GetFrameworkSdkPath.CouldNotFindSDK",
                            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.VersionLatest),
                            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.VersionLatest)
                        );

                        s_version461Path = String.Empty;
                    }
                    else
                    {
                        s_version461Path = FileUtilities.EnsureTrailingSlash(s_version461Path);
                        Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", s_version461Path);
                    }
                }

                return s_version461Path;
            }
        }

        #endregion

        #region ITask Members

        /// <summary>
        /// Get the SDK.
        /// </summary>
        /// <returns>true</returns>
        public override bool Execute()
        {
            //Does Nothing: getters do all the work

            return true;
        }

        #endregion
    }
}
