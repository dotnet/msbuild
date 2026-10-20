// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
    private const string retailConfigurationName = "Retail";
    private const string neutralArchitectureName = "Neutral";
    private const string commonConfigurationFolderName = "CommonConfiguration";
    private const string redistFolderName = "Redist";
    private const string referencesFolderName = "References";
    private const string designTimeFolderName = "DesignTime";

    /// <summary>
    /// Get the list of SDK folders which contains the references for the sdk at the sdkRoot provided
    /// in the order in which they should be searched for references.
    /// </summary>
    /// <param name="sdkRoot">Root folder for the SDK</param>
    /// <returns>A list of folders in the order which they should be used when looking for references in the SDK</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static IList<string> GetSDKReferenceFolders(string sdkRoot)
        => GetSDKReferenceFolders(sdkRoot, retailConfigurationName, neutralArchitectureName);

    /// <summary>
    /// Get the list of SDK folders which contains the references for the sdk at the sdkRoot provided
    /// in the order in which they should be searched for references.
    /// </summary>
    /// <param name="sdkRoot">Root folder for the SDK</param>
    /// <param name="targetConfiguration">The configuration the SDK is targeting. This should be Debug or Retail</param>
    /// <param name="targetArchitecture">The architecture the SDK is targeting</param>
    /// <returns>A list of folders in the order which they should be used when looking for references in the SDK</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static IList<string> GetSDKReferenceFolders(string sdkRoot, string targetConfiguration, string targetArchitecture)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkRoot);
        ArgumentException.ThrowIfNullOrEmpty(targetConfiguration);
        ArgumentException.ThrowIfNullOrEmpty(targetArchitecture);

        var referenceDirectories = new List<string>(4);

        string legacyWindowsMetadataLocation = Path.Combine(sdkRoot, "Windows Metadata");
        if (FileUtilities.DirectoryExistsNoThrow(legacyWindowsMetadataLocation))
        {
            legacyWindowsMetadataLocation = FileUtilities.EnsureTrailingSlash(legacyWindowsMetadataLocation);
            referenceDirectories.Add(legacyWindowsMetadataLocation);
        }

        AddSDKPaths(sdkRoot, referencesFolderName, targetConfiguration, targetArchitecture, referenceDirectories);

        return referenceDirectories;
    }

    /// <summary>
    /// Add the set of paths for where sdk files should be found. Where &lt;folderType&gt; is redist, references, designtime
    /// </summary>
    private static void AddSDKPaths(string sdkRoot, string folderName, string targetConfiguration, string targetArchitecture, List<string> directories)
    {
        targetArchitecture = RemapSdkArchitecture(targetArchitecture);

        // <SDKROOT>\<folderType>\Debug\X86
        AddSDKPath(sdkRoot, folderName, targetConfiguration, targetArchitecture, directories);

        if (!neutralArchitectureName.Equals(targetArchitecture, StringComparison.OrdinalIgnoreCase))
        {
            // <SDKROOT>\<folderType>\Debug\Neutral
            AddSDKPath(sdkRoot, folderName, targetConfiguration, neutralArchitectureName, directories);
        }

        // <SDKROOT>\<folderType>\CommonConfiguration\x86
        AddSDKPath(sdkRoot, folderName, commonConfigurationFolderName, targetArchitecture, directories);

        if (!neutralArchitectureName.Equals(targetArchitecture, StringComparison.OrdinalIgnoreCase))
        {
            // <SDKROOT>\<folderType>\CommonConfiguration\Neutral
            AddSDKPath(sdkRoot, folderName, commonConfigurationFolderName, neutralArchitectureName, directories);
        }
    }

    /// <summary>
    /// Get the list of SDK folders which contains the redist files for the sdk at the sdkRoot provided
    /// in the order in which they should be searched for references.
    /// </summary>
    /// <param name="sdkRoot">Root folder for the SDK must contain a redist folder</param>
    /// <returns>A list of folders in the order which they should be used when looking for redist files in the SDK</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static IList<string> GetSDKRedistFolders(string sdkRoot)
        => GetSDKRedistFolders(sdkRoot, retailConfigurationName, neutralArchitectureName);

    /// <summary>
    /// Get the list of SDK folders which contains the redist files for the sdk at the sdkRoot provided
    /// in the order in which they should be searched for references.
    /// </summary>
    /// <param name="sdkRoot">Root folder for the SDK must contain a redist folder</param>
    /// <param name="targetConfiguration">The configuration the SDK is targeting. This should be Debug or Retail</param>
    /// <param name="targetArchitecture">The architecture the SDK is targeting</param>
    /// <returns>A list of folders in the order which they should be used when looking for redist files in the SDK</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static IList<string> GetSDKRedistFolders(string sdkRoot, string targetConfiguration, string targetArchitecture)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkRoot);
        ArgumentException.ThrowIfNullOrEmpty(targetConfiguration);
        ArgumentException.ThrowIfNullOrEmpty(targetArchitecture);

        var redistDirectories = new List<string>(4);

        AddSDKPaths(sdkRoot, redistFolderName, targetConfiguration, targetArchitecture, redistDirectories);
        return redistDirectories;
    }

    /// <summary>
    /// Get the list of SDK folders which contains the designtime files for the sdk at the sdkRoot provided
    /// in the order in which they should be searched for references.
    /// </summary>
    /// <param name="sdkRoot">Root folder for the SDK must contain a Designtime folder</param>
    /// <returns>A list of folders in the order which they should be used when looking for DesignTime files in the SDK</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static IList<string> GetSDKDesignTimeFolders(string sdkRoot)
        => GetSDKDesignTimeFolders(sdkRoot, retailConfigurationName, neutralArchitectureName);

    /// <summary>
    /// Get the list of SDK folders which contains the DesignTime files for the sdk at the sdkRoot provided
    /// in the order in which they should be searched for references.
    /// </summary>
    /// <param name="sdkRoot">Root folder for the SDK must contain a DesignTime folder</param>
    /// <param name="targetConfiguration">The configuration the SDK is targeting. This should be Debug or Retail</param>
    /// <param name="targetArchitecture">The architecture the SDK is targeting</param>
    /// <returns>A list of folders in the order which they should be used when looking for DesignTime files in the SDK</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static IList<string> GetSDKDesignTimeFolders(string sdkRoot, string targetConfiguration, string targetArchitecture)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkRoot);
        ArgumentException.ThrowIfNullOrEmpty(targetConfiguration);
        ArgumentException.ThrowIfNullOrEmpty(targetArchitecture);

        var designTimeDirectories = new List<string>(4);

        AddSDKPaths(sdkRoot, designTimeFolderName, targetConfiguration, targetArchitecture, designTimeDirectories);
        return designTimeDirectories;
    }
    /// <summary>
    /// Return the versioned/unversioned SDK content folder path
    /// </summary>
    /// <param name="sdkIdentifier">The identifier of the SDK</param>
    /// <param name="sdkVersion">The verision of the SDK</param>
    /// <param name="targetPlatformIdentifier">The identifier of the targeted platform</param>
    /// <param name="targetPlatformMinVersion">The min version of the targeted platform</param>
    /// <param name="targetPlatformVersion">The version of the targeted platform</param>
    /// <param name="folderName">The content folder name under SDK path</param>
    /// <param name="diskRoot">An optional disk root to search.  A value should only be passed from a unit test.</param>
    /// <returns>The SDK content folder path</returns>
    public static string GetSDKContentFolderPath(
        string sdkIdentifier,
        string sdkVersion,
        string targetPlatformIdentifier,
        string targetPlatformMinVersion,
        string targetPlatformVersion,
        string folderName,
        string diskRoot = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkIdentifier);
        ArgumentException.ThrowIfNullOrEmpty(sdkVersion);

        // Avoid exception in Path.Combine
        folderName ??= string.Empty;

        // If no folder name is input or it isn't UWP SDK, return the root SDK path.
        if (string.IsNullOrWhiteSpace(folderName) || sdkVersion != "10.0" || !string.Equals(sdkIdentifier, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            string sdkLocation = GetPlatformSDKLocation(sdkIdentifier, sdkVersion);
            return Path.Combine(sdkLocation, folderName);
        }

        ArgumentException.ThrowIfNullOrEmpty(targetPlatformIdentifier);
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformVersion);

        string sdkContentFolderPath = null;

        TargetPlatformSDK matchingSdk = GetMatchingPlatformSDK(targetPlatformIdentifier, targetPlatformVersion, diskRoot, null, null);
        string platformKey = TargetPlatformSDK.GetSdkKey(targetPlatformIdentifier, targetPlatformVersion);
        if (TryGetPlatformManifest(matchingSdk, platformKey, out PlatformManifest manifest))
        {
            sdkContentFolderPath = manifest.VersionedContent
                ? Path.Combine(matchingSdk.Path, folderName, targetPlatformVersion)
                : Path.Combine(matchingSdk.Path, folderName);
        }

        return sdkContentFolderPath;
    }

    /// <summary>
    /// Remap some common architectures to a single one that will be in the SDK.
    /// </summary>
    private static string RemapSdkArchitecture(string targetArchitecture)
    {
        if (targetArchitecture.Equals("msil", StringComparison.OrdinalIgnoreCase) ||
           targetArchitecture.Equals("AnyCPU", StringComparison.OrdinalIgnoreCase) ||
           targetArchitecture.Equals("Any CPU", StringComparison.OrdinalIgnoreCase))
        {
            targetArchitecture = "Neutral";
        }
        else if (targetArchitecture.Equals("Amd64", StringComparison.OrdinalIgnoreCase))
        {
            targetArchitecture = "x64";
        }

        return targetArchitecture;
    }

    /// <summary>
    /// Add the reference folder to the list of reference directories if it exists.
    /// </summary>
    private static void AddSDKPath(string sdkRoot, string contentFolderName, string targetConfiguration, string targetArchitecture, List<string> contentDirectories)
    {
        string referenceAssemblyPath = Path.Combine(sdkRoot, contentFolderName, targetConfiguration, targetArchitecture);

        if (FileUtilities.DirectoryExistsNoThrow(referenceAssemblyPath))
        {
            referenceAssemblyPath = FileUtilities.EnsureTrailingSlash(referenceAssemblyPath);
            contentDirectories.Add(referenceAssemblyPath);
        }
    }
}
