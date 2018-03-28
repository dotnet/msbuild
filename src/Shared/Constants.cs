// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;

// This file is compiled into both Microsoft.Build.Framework and Microsoft.Build.Tasks which can cause collisions.
#if MICROSOFT_BUILD_TASKS
namespace Microsoft.Build.Tasks
#else
namespace Microsoft.Build.Shared
#endif
{
    /// <summary>
    /// Constants that we want to be shareable across all our assemblies.
    /// </summary>
    internal static class MSBuildConstants
    {
        /// <summary>
        /// The name of the property that indicates the tools path
        /// </summary>
        internal const string ToolsPath = "MSBuildToolsPath";

        /// <summary>
        /// Name of the property that indicates the X64 tools path
        /// </summary>
        internal const string ToolsPath64 = "MSBuildToolsPath64";

        /// <summary>
        /// Name of the property that indicates the root of the SDKs folder
        /// </summary>
        internal const string SdksPath = "MSBuildSDKsPath";

        /// <summary>
        /// Name of the property that indicates that all warnings should be treated as errors.
        /// </summary>
        internal const string TreatWarningsAsErrors = "MSBuildTreatWarningsAsErrors";

        /// <summary>
        /// Name of the property that indicates a list of warnings to treat as errors.
        /// </summary>
        internal const string WarningsAsErrors = "MSBuildWarningsAsErrors";

        /// <summary>
        /// Name of the property that indicates the list of warnings to treat as messages.
        /// </summary>
        internal const string WarningsAsMessages = "MSBuildWarningsAsMessages";

        /// <summary>
        /// The name of the environment variable that users can specify to override where NuGet assemblies are loaded from in the NuGetSdkResolver.
        /// </summary>
        internal const string NuGetAssemblyPathEnvironmentVariableName = "MSBUILD_NUGET_PATH";

        /// <summary>
        /// The name of the target to run when a user specifies the /restore command-line argument.
        /// </summary>
        internal const string RestoreTargetName = "Restore";
        /// <summary>
        /// The most current Visual Studio Version known to this version of MSBuild.
        /// </summary>
        internal const string CurrentVisualStudioVersion = "15.0";

        /// <summary>
        /// The most current ToolsVersion known to this version of MSBuild.
        /// </summary>
        internal const string CurrentToolsVersion = CurrentVisualStudioVersion;

        // if you change the key also change the following clones
        // Microsoft.Build.OpportunisticIntern.BucketedPrioritizedStringList.TryIntern
        internal const string MSBuildDummyGlobalPropertyHeader = "MSBuildProjectInstance";

        /// <summary>
        /// The most current ToolsVersion known to this version of MSBuild as a Version object.
        /// </summary>
        internal static Version CurrentToolsVersionAsVersion = new Version(CurrentToolsVersion);

        /// <summary>
        /// The most current VSGeneralAssemblyVersion known to this version of MSBuild.
        /// </summary>
        internal const string CurrentAssemblyVersion = "15.1.0.0";

        /// <summary>
        /// Current version of this MSBuild Engine assembly in the form, e.g, "12.0"
        /// </summary>
        internal static string CurrentProductVersion
        {
            get
            {
                return "15.0";
            }
        }
    }

    /// <summary>
    /// Constants naming well-known item metadata.
    /// </summary>
    internal static class ItemMetadataNames
    {
        internal const string fusionName = "FusionName";
        internal const string hintPath = "HintPath";
        internal const string assemblyFolderKey = "AssemblyFolderKey";
        internal const string alias = "Alias";
        internal const string aliases = "Aliases";
        internal const string parentFile = "ParentFile";
        internal const string privateMetadata = "Private";
        internal const string copyLocal = "CopyLocal";
        internal const string isRedistRoot = "IsRedistRoot";
        internal const string redist = "Redist";
        internal const string resolvedFrom = "ResolvedFrom";
        internal const string destinationSubDirectory = "DestinationSubDirectory";
        internal const string specificVersion = "SpecificVersion";
        internal const string link = "Link";
        internal const string subType = "SubType";
        internal const string executableExtension = "ExecutableExtension";
        internal const string embedInteropTypes = "EmbedInteropTypes";
        internal const string targetPath = "TargetPath";
        internal const string dependentUpon = "DependentUpon";
        internal const string msbuildSourceProjectFile = "MSBuildSourceProjectFile";
        internal const string msbuildSourceTargetName = "MSBuildSourceTargetName";
        internal const string isPrimary = "IsPrimary";
        internal const string targetFramework = "RequiredTargetFramework";
        internal const string frameworkDirectory = "FrameworkDirectory";
        internal const string version = "Version";
        internal const string imageRuntime = "ImageRuntime";
        internal const string winMDFile = "WinMDFile";
        internal const string winMDFileType = "WinMDFileType";
        internal const string msbuildReferenceSourceTarget = "ReferenceSourceTarget";
        internal const string msbuildReferenceGrouping = "ReferenceGrouping";
        internal const string msbuildReferenceGroupingDisplayName = "ReferenceGroupingDisplayName";
        internal const string msbuildReferenceFromSDK = "ReferenceFromSDK";
        internal const string winmdImplmentationFile = "Implementation";
        internal const string projectReferenceOriginalItemSpec = "ProjectReferenceOriginalItemSpec";
        internal const string IgnoreVersionForFrameworkReference = "IgnoreVersionForFrameworkReference";
        internal const string frameworkFile = "FrameworkFile";
    }
}
