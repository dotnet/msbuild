// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;

using Microsoft.Build.Utilities;
#endif

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks
{
#if NETFRAMEWORK

    /// <summary>
    /// Returns paths to the frameworks SDK.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class GetFrameworkSdkPath : TaskExtension, IGetFrameworkSdkPathTaskContract
    {
        #region Properties

        private static volatile string s_path;
        private static volatile string s_version20Path;
        private static volatile string s_version35Path;
        private static volatile string s_version40Path;
        private static volatile string s_version45Path;
        private static volatile string s_version451Path;
        private static volatile string s_version46Path;
        private static volatile string s_version461Path;
        private static readonly LockType s_lockObject = new();

        /// <summary>
        /// The path to the latest .NET SDK if it could be found. It will be String.Empty if the SDK was not found.
        /// </summary>
        [Output]
        public string Path
        {
            get
            {
                string path = s_path;
                if (path == null)
                {
                    lock (s_lockObject)
                    {
                        path = s_path;
                        if (path == null)
                        {
                            path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Latest, VisualStudioVersion.VersionLatest);

                            if (String.IsNullOrEmpty(path))
                            {
                                Log.LogMessageFromResources(
                                    MessageImportance.High,
                                    "GetFrameworkSdkPath.CouldNotFindSDK",
                                    ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Latest, VisualStudioVersion.VersionLatest),
                                    ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Latest, VisualStudioVersion.VersionLatest));

                                path = String.Empty;
                            }
                            else
                            {
                                path = FrameworkFileUtilities.EnsureTrailingSlash(path);
                                Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", path);
                            }
                            
                            s_path = path;
                        }
                    }
                }

                return path;
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
                string path = s_version20Path;
                if (path == null)
                {
                    lock (s_lockObject)
                    {
                        path = s_version20Path;
                        if (path == null)
                        {
                            path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version20);

                            if (String.IsNullOrEmpty(path))
                            {
                                Log.LogMessageFromResources(
                                    MessageImportance.High,
                                    "GetFrameworkSdkPath.CouldNotFindSDK",
                                    ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version20),
                                    ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version20));

                                path = String.Empty;
                            }
                            else
                            {
                                path = FrameworkFileUtilities.EnsureTrailingSlash(path);
                                Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", path);
                            }
                            
                            s_version20Path = path;
                        }
                    }
                }

                return path;
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
                string path = s_version35Path;
                if (path == null)
                {
                    lock (s_lockObject)
                    {
                        path = s_version35Path;
                        if (path == null)
                        {
                            path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest);

                            if (String.IsNullOrEmpty(path))
                            {
                                Log.LogMessageFromResources(
                                    MessageImportance.High,
                                    "GetFrameworkSdkPath.CouldNotFindSDK",
                                    ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest),
                                    ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest));

                                path = String.Empty;
                            }
                            else
                            {
                                path = FrameworkFileUtilities.EnsureTrailingSlash(path);
                                Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", path);
                            }
                            
                            s_version35Path = path;
                        }
                    }
                }

                return path;
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
                string path = s_version40Path;
                if (path == null)
                {
                    lock (s_lockObject)
                    {
                        path = s_version40Path;
                        if (path == null)
                        {
                            path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.VersionLatest);

                            if (String.IsNullOrEmpty(path))
                            {
                                Log.LogMessageFromResources(
                                    MessageImportance.High,
                                    "GetFrameworkSdkPath.CouldNotFindSDK",
                                    ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.VersionLatest),
                                    ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.VersionLatest));

                                path = String.Empty;
                            }
                            else
                            {
                                path = FrameworkFileUtilities.EnsureTrailingSlash(path);
                                Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", path);
                            }
                            
                            s_version40Path = path;
                        }
                    }
                }

                return path;
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
                string path = s_version45Path;
                if (path == null)
                {
                    lock (s_lockObject)
                    {
                        path = s_version45Path;
                        if (path == null)
                        {
                            path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.VersionLatest);

                            if (String.IsNullOrEmpty(path))
                            {
                                Log.LogMessageFromResources(
                                    MessageImportance.High,
                                    "GetFrameworkSdkPath.CouldNotFindSDK",
                                    ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.VersionLatest),
                                    ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.VersionLatest));

                                path = String.Empty;
                            }
                            else
                            {
                                path = FrameworkFileUtilities.EnsureTrailingSlash(path);
                                Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", path);
                            }
                            
                            s_version45Path = path;
                        }
                    }
                }

                return path;
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
                string path = s_version451Path;
                if (path == null)
                {
                    lock (s_lockObject)
                    {
                        path = s_version451Path;
                        if (path == null)
                        {
                            path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.VersionLatest);

                            if (String.IsNullOrEmpty(path))
                            {
                                Log.LogMessageFromResources(
                                    MessageImportance.High,
                                    "GetFrameworkSdkPath.CouldNotFindSDK",
                                    ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.VersionLatest),
                                    ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.VersionLatest));

                                path = String.Empty;
                            }
                            else
                            {
                                path = FrameworkFileUtilities.EnsureTrailingSlash(path);
                                Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", path);
                            }
                            
                            s_version451Path = path;
                        }
                    }
                }

                return path;
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
                string path = s_version46Path;
                if (path == null)
                {
                    lock (s_lockObject)
                    {
                        path = s_version46Path;
                        if (path == null)
                        {
                            path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.VersionLatest);

                            if (String.IsNullOrEmpty(path))
                            {
                                Log.LogMessageFromResources(
                                    MessageImportance.High,
                                    "GetFrameworkSdkPath.CouldNotFindSDK",
                                    ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.VersionLatest),
                                    ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.VersionLatest));

                                path = String.Empty;
                            }
                            else
                            {
                                path = FrameworkFileUtilities.EnsureTrailingSlash(path);
                                Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", path);
                            }
                            
                            s_version46Path = path;
                        }
                    }
                }

                return path;
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
                string path = s_version461Path;
                if (path == null)
                {
                    lock (s_lockObject)
                    {
                        path = s_version461Path;
                        if (path == null)
                        {
                            path = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.VersionLatest);

                            if (String.IsNullOrEmpty(path))
                            {
                                Log.LogMessageFromResources(
                                    MessageImportance.High,
                                    "GetFrameworkSdkPath.CouldNotFindSDK",
                                    ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.VersionLatest),
                                    ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.VersionLatest));

                                path = String.Empty;
                            }
                            else
                            {
                                path = FrameworkFileUtilities.EnsureTrailingSlash(path);
                                Log.LogMessageFromResources(MessageImportance.Low, "GetFrameworkSdkPath.FoundSDK", path);
                            }
                            
                            s_version461Path = path;
                        }
                    }
                }

                return path;
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
            // Does Nothing: getters do all the work

            return true;
        }

        #endregion
    }
#else

    [MSBuildMultiThreadableTask]
    public sealed class GetFrameworkSdkPath : TaskRequiresFramework, IGetFrameworkSdkPathTaskContract
    {
        public GetFrameworkSdkPath()
            : base(nameof(GetFrameworkSdkPath))
        {
        }

        #region Properties

        [Output]
        public string Path { get; set; }

        [Output]
        public string FrameworkSdkVersion20Path { get; }

        [Output]
        public string FrameworkSdkVersion35Path { get; }

        [Output]
        public string FrameworkSdkVersion40Path { get; }

        [Output]
        public string FrameworkSdkVersion45Path { get; }

        [Output]
        public string FrameworkSdkVersion451Path { get; }

        [Output]
        public string FrameworkSdkVersion46Path { get; }

        [Output]
        public string FrameworkSdkVersion461Path { get; }

        #endregion
    }

#endif

#pragma warning disable SA1201 // Elements should appear in the correct order
    internal interface IGetFrameworkSdkPathTaskContract
    {
        #region Properties

        string Path { get; set; }
        string FrameworkSdkVersion20Path { get; }
        string FrameworkSdkVersion35Path { get; }
        string FrameworkSdkVersion40Path { get; }
        string FrameworkSdkVersion45Path { get; }
        string FrameworkSdkVersion451Path { get; }
        string FrameworkSdkVersion46Path { get; }
        string FrameworkSdkVersion461Path { get; }

        #endregion
    }
#pragma warning restore SA1201 // Elements should appear in the correct order
}
