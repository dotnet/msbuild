// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.AssemblyFoldersFromConfig;

#nullable disable

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
    /// <summary>
    /// Get a sorted list of AssemblyFoldersExInfo which contain information about what directories the 3rd party assemblies are registered under for use during build and design time.
    ///
    /// This method will enumerate the AssemblyFoldersEx registry location and return a list of AssemblyFoldersExInfo in the same order in which
    /// they will be searched during both design and build time for reference assemblies.
    /// </summary>
    /// <param name="registryRoot">The root registry location for the targeted framework. For .NET this is SOFTWARE\MICROSOFT\.NETFramework</param>
    /// <param name="targetFrameworkVersion">The targeted framework version (2.0, 3.0, 3.5, 4.0, etc)</param>
    /// <param name="registryKeySuffix">The name of the folder (AssemblyFoldersEx) could also be PocketPC\AssemblyFoldersEx, or others</param>
    /// <param name="osVersion">Components may declare Min and Max OSVersions in the registry this value can be used filter directories returned based on whether or not the osversion is bounded by the Min  and Max versions declared by the component. If this value is blank or null no filtering is done</param>
    /// <param name="platform">Components may declare platform guids in the registry this can be used to return only directories which have a certain platform guid. If this value is blank or null no filtering is done</param>
    /// <param name="targetProcessorArchitecture">What processor architecture is being targeted. This determines which registry hives are searched in what order.
    /// On a 64 bit operating system we do the following
    ///         If you are targeting 64 bit (target x64 or ia64)
    ///             Add in the 64 bit hive first
    ///             Add in the 32 bit hive second
    ///         If you are not targeting a 64 bit
    ///            Add in the 32 bit hive first
    ///            Add in the 64 bit hive second
    /// On a 32 bit machine we only add in the 32 bit hive.
    /// </param>
    /// <returns>List of AssemblyFoldersExInfo</returns>
    [SupportedOSPlatform("windows")]
    public static IList<AssemblyFoldersExInfo> GetAssemblyFoldersExInfo(string registryRoot, string targetFrameworkVersion, string registryKeySuffix, string osVersion, string platform, System.Reflection.ProcessorArchitecture targetProcessorArchitecture)
    {
        ArgumentException.ThrowIfNullOrEmpty(registryRoot);
        ArgumentException.ThrowIfNullOrEmpty(registryKeySuffix);
        ArgumentException.ThrowIfNullOrEmpty(targetFrameworkVersion);

        AssemblyFoldersEx assemblyFoldersEx = new AssemblyFoldersEx(
            registryRoot,
            targetFrameworkVersion,
            registryKeySuffix,
            osVersion,
            platform,
            new GetRegistrySubKeyNames(RegistryHelper.GetSubKeyNames),
            new GetRegistrySubKeyDefaultValue(RegistryHelper.GetDefaultValue),
            targetProcessorArchitecture,
            new OpenBaseKey(RegistryHelper.OpenBaseKey));

        var assemblyFolders = new List<AssemblyFoldersExInfo>();
        assemblyFolders.AddRange(assemblyFoldersEx);
        return assemblyFolders;
    }

    /// <summary>
    /// Get a sorted list of AssemblyFoldersFromConfigInfo which contain information about what directories the 3rd party assemblies are registered under for use during build and design time.
    ///
    /// This method will read the specified configuration file and enumerate the and return a list of AssemblyFoldersFromConfigInfo in the same order in which
    /// they will be searched during both design and build time for reference assemblies.
    /// </summary>
    /// <param name="configFile">Full path to the Assembly Folders config file.</param>
    /// <param name="targetFrameworkVersion">The targeted framework version (2.0, 3.0, 3.5, 4.0, etc).</param>
    /// <param name="targetProcessorArchitecture">What processor architecture is being targeted. This determines which registry hives are searched in what order.
    /// On a 64 bit operating system we do the following
    ///         If you are targeting 64 bit (target x64 or ia64)
    ///             Add in the 64 bit assembly folders first
    ///             Add in the 32 bit assembly folders second
    ///         If you are not targeting a 64 bit
    ///            Add in the 32 bit assembly folders first
    ///            Add in the 64 bit assembly folders second
    /// On a 32 bit machine we only add in the 32 bit assembly folders.
    /// </param>
    /// <returns>List of AssemblyFoldersFromConfigInfo</returns>
    public static IList<AssemblyFoldersFromConfigInfo> GetAssemblyFoldersFromConfigInfo(string configFile, string targetFrameworkVersion, System.Reflection.ProcessorArchitecture targetProcessorArchitecture)
    {
        ArgumentException.ThrowIfNullOrEmpty(configFile);
        ArgumentException.ThrowIfNullOrEmpty(targetFrameworkVersion);

        var assemblyFoldersInfos = new AssemblyFoldersFromConfig(configFile, targetFrameworkVersion, targetProcessorArchitecture);

        return assemblyFoldersInfos.ToList();
    }
}
