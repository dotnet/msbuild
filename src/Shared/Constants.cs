// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;

namespace Microsoft.Build.Shared
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
        /// The most current Visual Studio Version known to this version of MSBuild. 
        /// </summary>
#if STANDALONEBUILD
        internal const string CurrentVisualStudioVersion = "15.0";
#else
        internal const string CurrentVisualStudioVersion = Microsoft.VisualStudio.Internal.BrandNames.VSGeneralVersion;
#endif

        /// <summary>
        /// The most current ToolsVersion known to this version of MSBuild. 
        /// </summary>
        internal const string CurrentToolsVersion = CurrentVisualStudioVersion;

        /// <summary>
        /// The most current ToolsVersion known to this version of MSBuild as a Version object. 
        /// </summary>
        internal static Version CurrentToolsVersionAsVersion = new Version(CurrentToolsVersion);

        /// <summary>
        /// The most current VSGeneralAssemblyVersion known to this version of MSBuild. 
        /// </summary>
#if STANDALONEBUILD
        internal const string CurrentAssemblyVersion = "15.1.0.0";
#else
        internal const string CurrentAssemblyVersion = Microsoft.VisualStudio.Internal.BrandNames.VSGeneralAssemblyVersion;
#endif

        /// <summary>
        /// Current version of this MSBuild Engine assembly in the form, e.g, "12.0"
        /// </summary>
        internal static string CurrentProductVersion
        {
            get
            {
#if STANDALONEBUILD
                return "15.0";
#else
                Version thisAssemblyVersion = new Version(ThisAssembly.Version);
                // "12.0.0.0" --> "12.0"
                return thisAssemblyVersion.Major + "." + thisAssemblyVersion.Minor;
#endif
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
