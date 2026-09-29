// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
    /// <summary>
    /// Cache the set of extension Sdk references for a particular combination of inputs.
    /// </summary>
    private static Dictionary<string, string[]> s_cachedExtensionSdkReferences;

    /// <summary>
    /// Get a list of SDK's installed on the machine for a given target platform
    /// </summary>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <returns>IDictionary of installed SDKS and their location. K:SDKName V:SDK installation location</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static IDictionary<string, string> GetPlatformExtensionSDKLocations(string targetPlatformIdentifier, Version targetPlatformVersion)
        => GetPlatformExtensionSDKLocations(null, null, targetPlatformIdentifier, targetPlatformVersion);

    /// <summary>
    /// Get a list of SDK's installed on the machine for a given target platform
    /// </summary>
    /// <param name="diskRoots">Array of disk locations to search for sdks</param>
    /// <param name="registryRoot">Root registry location to look for sdks</param>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <returns>IDictionary of installed SDKS and their location. K:SDKName V:SDK installation location</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static IDictionary<string, string> GetPlatformExtensionSDKLocations(string[] diskRoots, string registryRoot, string targetPlatformIdentifier, Version targetPlatformVersion)
        => GetPlatformExtensionSDKLocations(diskRoots, null, registryRoot, targetPlatformIdentifier, targetPlatformVersion);

    /// <summary>
    /// Get a list of SDK's installed on the machine for a given target platform
    /// </summary>
    /// <param name="diskRoots">Array of disk locations to search for sdks</param>
    /// <param name="extensionDiskRoots">New style extension SDK roots</param>
    /// <param name="registryRoot">Root registry location to look for sdks</param>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <returns>IDictionary of installed SDKS and their location. K:SDKName V:SDK installation location</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static IDictionary<string, string> GetPlatformExtensionSDKLocations(string[] diskRoots, string[] extensionDiskRoots, string registryRoot, string targetPlatformIdentifier, Version targetPlatformVersion)
    {
        var extensionSDKs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<TargetPlatformSDK> targetPlatformMonikers = GetTargetPlatformMonikers(diskRoots, extensionDiskRoots, registryRoot, targetPlatformIdentifier, targetPlatformVersion);
        foreach (TargetPlatformSDK moniker in targetPlatformMonikers)
        {
            foreach (KeyValuePair<string, string> extension in moniker.ExtensionSDKs)
            {
                extensionSDKs[extension.Key] = extension.Value;
            }
        }

        return extensionSDKs;
    }

    /// <summary>
    /// Get a list of SDK's installed on the machine for a given target platform
    /// </summary>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <returns>IDictionary of installed SDKS and their tuples containing (location, platform version).</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Casing kept to maintain consistency with existing APIs")]
    public static IDictionary<string, Tuple<string, string>> GetPlatformExtensionSDKLocationsAndVersions(string targetPlatformIdentifier, Version targetPlatformVersion)
        => GetPlatformExtensionSDKLocationsAndVersions(null, null, targetPlatformIdentifier, targetPlatformVersion);

    /// <summary>
    /// Set of installed SDKs and their location and platform versions
    /// </summary>
    /// <param name="diskRoots">Array of disk locations to search for sdks</param>
    /// <param name="registryRoot">Root registry location to look for sdks</param>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <returns>IDictionary of installed SDKS and their tuples containing (location, platform version).</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Casing kept to maintain consistency with existing APIs")]
    public static IDictionary<string, Tuple<string, string>> GetPlatformExtensionSDKLocationsAndVersions(string[] diskRoots, string registryRoot, string targetPlatformIdentifier, Version targetPlatformVersion)
        => GetPlatformExtensionSDKLocationsAndVersions(diskRoots, null, registryRoot, targetPlatformIdentifier, targetPlatformVersion);

    /// <summary>
    /// Set of installed SDKs and their location and platform versions
    /// </summary>
    /// <param name="diskRoots">Array of disk locations to search for sdks</param>
    /// <param name="multiPlatformDiskRoots">Array of disk locations to search for SDKs that target multiple versions</param>
    /// <param name="registryRoot">Root registry location to look for sdks</param>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <returns>IDictionary of installed SDKS and their tuples containing (location, platform version). Version may be null if the SDK targets multiple versions.</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Casing kept to maintain consistency with existing APIs")]
    public static IDictionary<string, Tuple<string, string>> GetPlatformExtensionSDKLocationsAndVersions(string[] diskRoots, string[] multiPlatformDiskRoots, string registryRoot, string targetPlatformIdentifier, Version targetPlatformVersion)
    {
        var extensionSDKsAndVersions = new Dictionary<string, Tuple<string, string>>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<TargetPlatformSDK> targetPlatformMonikers = GetTargetPlatformMonikers(diskRoots, multiPlatformDiskRoots, registryRoot, targetPlatformIdentifier, targetPlatformVersion);

        foreach (TargetPlatformSDK moniker in targetPlatformMonikers)
        {
            foreach (KeyValuePair<string, string> extension in moniker.ExtensionSDKs)
            {
                extensionSDKsAndVersions[extension.Key] = Tuple.Create(extension.Value, moniker.TargetPlatformVersion.ToString());
            }
        }

        return extensionSDKsAndVersions;
    }

    /// <summary>
    /// Get target platform monikers used to extract ESDK information in the methods GetPlatformExtensionSDKLocationsAndVersions and GetPlatformExtensionSDKLocations
    /// </summary>
    private static IEnumerable<TargetPlatformSDK> GetTargetPlatformMonikers(string[] diskRoots, string[] extensionDiskRoots, string registryRoot, string targetPlatformIdentifier, Version targetPlatformVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformIdentifier);
        ArgumentNullException.ThrowIfNull(targetPlatformVersion);

        string targetPlatformVersionString = targetPlatformVersion.ToString();

        DebugTrace.WriteLine($"Calling with TargetPlatformIdentifier:'{targetPlatformIdentifier}' and TargetPlatformVersion: '{targetPlatformVersionString}'", category: "GetPlatformExtensionSDKLocations");
        IEnumerable<TargetPlatformSDK> targetPlatformSDKs = RetrieveTargetPlatformList(diskRoots, extensionDiskRoots, registryRoot);

        return targetPlatformSDKs
            .Where(platformSDK =>
                string.IsNullOrEmpty(platformSDK.TargetPlatformIdentifier)
                || (platformSDK.TargetPlatformIdentifier.Equals(targetPlatformIdentifier, StringComparison.OrdinalIgnoreCase)
                 && platformSDK.TargetPlatformVersion <= targetPlatformVersion) || platformSDK.ContainsPlatform(targetPlatformIdentifier, targetPlatformVersionString))
            .OrderBy(platform => platform.TargetPlatformVersion);
    }

    /// <summary>
    /// Given an SDKName, targetPlatformIdentifier and TargetPlatformVersion search the default sdk locations for the passed in sdk name.
    /// The format of the sdk moniker is  SDKName, Version=X.X
    /// </summary>
    /// <param name="sdkMoniker">Name of the SDK to determine the installation location for.</param>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <returns>Location of the SDK if it is found, empty string if it could not be found</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformExtensionSDKLocation(string sdkMoniker, string targetPlatformIdentifier, Version targetPlatformVersion)
        => GetPlatformExtensionSDKLocation(sdkMoniker, targetPlatformIdentifier, targetPlatformVersion, null, null);

    /// <summary>
    /// Given an SDKName, targetPlatformIdentifier and TargetPlatformVersion search the default sdk locations for the passed in sdk name.
    /// The format of the sdk moniker is  SDKName, Version=X.X
    /// </summary>
    /// <param name="sdkMoniker">Name of the SDK to determine the installation location for.</param>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <param name="diskRoots">List of disk roots to search for sdks within</param>
    /// <param name="registryRoot">Registry root to look for sdks within</param>
    /// <returns>Location of the SDK if it is found, empty string if it could not be found</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformExtensionSDKLocation(string sdkMoniker, string targetPlatformIdentifier, Version targetPlatformVersion, string[] diskRoots, string registryRoot)
        => GetPlatformExtensionSDKLocation(sdkMoniker, targetPlatformIdentifier, targetPlatformVersion, diskRoots, null, registryRoot);

    /// <summary>
    /// Given an SDKName, targetPlatformIdentifier and TargetPlatformVersion search the default sdk locations for the passed in sdk name.
    /// The format of the sdk moniker is  SDKName, Version=X.X
    /// </summary>
    /// <param name="sdkMoniker">Name of the SDK to determine the installation location for.</param>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <param name="diskRoots">List of disk roots to search for sdks within</param>
    /// <param name="extensionDiskRoots">List of disk roots to look for manifest driven extension sdks</param>
    /// <param name="registryRoot">Registry root to look for sdks within</param>
    /// <returns>Location of the SDK if it is found, empty string if it could not be found</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformExtensionSDKLocation(string sdkMoniker, string targetPlatformIdentifier, Version targetPlatformVersion, string[] diskRoots, string[] extensionDiskRoots, string registryRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformIdentifier);
        ArgumentNullException.ThrowIfNull(targetPlatformVersion);
        ArgumentException.ThrowIfNullOrEmpty(sdkMoniker);

        IEnumerable<TargetPlatformSDK> targetPlatforms = RetrieveTargetPlatformList(diskRoots, extensionDiskRoots, registryRoot);
        var targetPlatformMoniker = targetPlatforms.Where(
            platform =>
                (string.IsNullOrEmpty(platform.TargetPlatformIdentifier)
                || (platform.TargetPlatformIdentifier.Equals(targetPlatformIdentifier, StringComparison.OrdinalIgnoreCase)
                 && platform.TargetPlatformVersion <= targetPlatformVersion))
                 && platform.ExtensionSDKs.ContainsKey(sdkMoniker))
            .OrderByDescending(platform => platform.TargetPlatformVersion)
            .DefaultIfEmpty(null)
            .FirstOrDefault();

        if (targetPlatformMoniker != null)
        {
            return targetPlatformMoniker.ExtensionSDKs[sdkMoniker];
        }
        else
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Given an SDK moniker and the targeted platform get the path to the SDK root if it exists.
    /// </summary>
    /// <param name="sdkMoniker">Moniker for the sdk</param>
    /// <param name="targetPlatformIdentifier">Identifier for the platform</param>
    /// <param name="targetPlatformVersion">Version of the platform</param>
    /// <returns>A full path to the sdk root if the sdk exists in the targeted platform or an empty string if it does not exist.</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformExtensionSDKLocation(string sdkMoniker, string targetPlatformIdentifier, string targetPlatformVersion)
        => GetPlatformExtensionSDKLocation(sdkMoniker, targetPlatformIdentifier, targetPlatformVersion, null, null);

    /// <summary>
    /// Given an SDKName, targetPlatformIdentifier and TargetPlatformVersion search the default sdk locations for the passed in sdk name.
    /// The format of the sdk moniker is  SDKName, Version=X.X
    /// </summary>
    /// <param name="sdkMoniker">Name of the SDK to determine the installation location for.</param>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <param name="diskRoots">List of disk roots to search for sdks within</param>
    /// <param name="registryRoot">Registry root to look for sdks within</param>
    /// <returns>Location of the SDK if it is found, empty string if it could not be found</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformExtensionSDKLocation(string sdkMoniker, string targetPlatformIdentifier, string targetPlatformVersion, string diskRoots, string registryRoot)
        => GetPlatformExtensionSDKLocation(sdkMoniker, targetPlatformIdentifier, targetPlatformVersion, diskRoots, null, registryRoot);

    /// <summary>
    /// Given an SDKName, targetPlatformIdentifier and TargetPlatformVersion search the default sdk locations for the passed in sdk name.
    /// The format of the sdk moniker is  SDKName, Version=X.X
    /// </summary>
    /// <param name="sdkMoniker">Name of the SDK to determine the installation location for.</param>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <param name="diskRoots">List of disk roots to search for sdks within</param>
    /// <param name="extensionDiskRoots">List of disk roots to look for manifest driven extension sdks</param>
    /// <param name="registryRoot">Registry root to look for sdks within</param>
    /// <returns>Location of the SDK if it is found, empty string if it could not be found</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformExtensionSDKLocation(string sdkMoniker, string targetPlatformIdentifier, string targetPlatformVersion, string diskRoots, string extensionDiskRoots, string registryRoot)
    {
        ArgumentNullException.ThrowIfNull(targetPlatformVersion);

        string[] sdkDiskRoots = null;
        if (!string.IsNullOrEmpty(diskRoots))
        {
            sdkDiskRoots = diskRoots.Split(s_diskRootSplitChars, StringSplitOptions.RemoveEmptyEntries);
        }

        string[] extensionSdkDiskRoots = null;
        if (!string.IsNullOrEmpty(extensionDiskRoots))
        {
            extensionSdkDiskRoots = extensionDiskRoots.Split(s_diskRootSplitChars, StringSplitOptions.RemoveEmptyEntries);
        }

        string sdkLocation = string.Empty;
        if (Version.TryParse(targetPlatformVersion, out Version platformVersion))
        {
            sdkLocation = GetPlatformExtensionSDKLocation(sdkMoniker, targetPlatformIdentifier, platformVersion, sdkDiskRoots, extensionSdkDiskRoots, registryRoot);
        }

        return sdkLocation;
    }

    /// <summary>
    /// Gets a dictionary containing a collection of extension SDKs and filter it based on the target platform version
    /// if max platform version isn't set in the extension sdk manifest, add the extension sdk to the filtered list
    /// </summary>
    /// <param name="targetPlatformVersion"></param>
    /// <param name="extensionSdks"></param>
    /// <returns>A IDictionary collection of filtered extension SDKs</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Not worth breaking customers")]
    public static IDictionary<string, string> FilterPlatformExtensionSDKs(Version targetPlatformVersion, IDictionary<string, string> extensionSdks)
    {
        var filteredExtensionSdks = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string> sdk in extensionSdks)
        {
            ExtensionSDK extensionSDK = new ExtensionSDK(sdk.Key, sdk.Value);

            // filter based on platform version - let pass if not in manifest or parameter
            if (extensionSDK.MaxPlatformVersion == null || targetPlatformVersion == null || extensionSDK.MaxPlatformVersion >= targetPlatformVersion)
            {
                filteredExtensionSdks.Add(sdk.Key, sdk.Value);
            }
        }
        return filteredExtensionSdks;
    }

    /// <summary>
    /// Gathers the specified extension SDK references for the given target SDK
    /// </summary>
    /// <param name="extensionSdkMoniker">The moniker is the Name/Version string. Example: "Windows Desktop, Version=10.0.0.1"</param>
    /// <param name="targetSdkIdentifier">The target SDK name.</param>
    /// <param name="targetSdkVersion">The target SDK version.</param>
    /// <param name="diskRoots">The disk roots used to gather installed SDKs.</param>
    /// <param name="extensionDiskRoots">The disk roots used to gather installed extension SDKs.</param>
    /// <param name="registryRoot">The registry root used to gather installed extension SDKs.</param>
    public static string[] GetPlatformOrFrameworkExtensionSdkReferences(string extensionSdkMoniker, string targetSdkIdentifier, string targetSdkVersion, string diskRoots, string extensionDiskRoots, string registryRoot)
        => GetPlatformOrFrameworkExtensionSdkReferences(
            extensionSdkMoniker,
            targetSdkIdentifier,
            targetSdkVersion,
            diskRoots,
            extensionDiskRoots,
            registryRoot,
            targetPlatformIdentifier: null,
            targetPlatformVersion: null);

    /// <summary>
    /// Gathers the specified extension SDK references for the given target SDK
    /// </summary>
    /// <param name="extensionSdkMoniker">The moniker is the Name/Version string. Example: "Windows Desktop, Version=10.0.0.1"</param>
    /// <param name="targetSdkIdentifier">The target SDK name.</param>
    /// <param name="targetSdkVersion">The target SDK version.</param>
    /// <param name="targetPlatformIdentifier">The target platform name.</param>
    /// <param name="targetPlatformVersion">The target platform version.</param>
    /// <param name="diskRoots">The disk roots used to gather installed SDKs.</param>
    /// <param name="extensionDiskRoots">The disk roots used to gather installed extension SDKs.</param>
    /// <param name="registryRoot">The registry root used to gather installed extension SDKs.</param>
    public static string[] GetPlatformOrFrameworkExtensionSdkReferences(
        string extensionSdkMoniker,
        string targetSdkIdentifier,
        string targetSdkVersion,
        string diskRoots,
        string extensionDiskRoots,
        string registryRoot,
        string targetPlatformIdentifier,
        string targetPlatformVersion)
    {
        lock (s_locker)
        {
            s_cachedExtensionSdkReferences ??= new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            string cacheKey = string.Join("|", extensionSdkMoniker, targetSdkIdentifier, targetSdkVersion);

            if (s_cachedExtensionSdkReferences.TryGetValue(cacheKey, out string[] extensionSdkReferences))
            {
                return extensionSdkReferences;
            }

            TargetPlatformSDK matchingSdk = GetMatchingPlatformSDK(targetSdkIdentifier, targetSdkVersion, diskRoots, extensionDiskRoots, registryRoot);

            if (matchingSdk == null)
            {
                DebugTrace.WriteLine($"Could not find root SDK for SDKI = '{targetSdkIdentifier}', SDKV = '{targetSdkVersion}'", category: "GetExtensionSdkReferences");
            }
            else
            {
                string targetSdkPath = matchingSdk.Path;
                string platformVersion = GetPlatformVersion(matchingSdk, targetPlatformIdentifier, targetPlatformVersion);

                if (matchingSdk.ExtensionSDKs.TryGetValue(extensionSdkMoniker, out string extensionSdkPath)
                    || (s_cachedExtensionSdks.TryGetValue(extensionDiskRoots, out matchingSdk)
                     && matchingSdk.ExtensionSDKs.TryGetValue(extensionSdkMoniker, out extensionSdkPath)))
                {
                    ExtensionSDK extensionSdk = new ExtensionSDK(extensionSdkMoniker, extensionSdkPath);
                    if (extensionSdk.SDKType == SDKType.Framework || extensionSdk.SDKType == SDKType.Platform)
                    {
                        // We don't want to attempt to gather ApiContract references if the framework isn't explicitly marked as Framework/Platform
                        extensionSdkReferences = GetApiContractReferences(extensionSdk.ApiContracts, targetSdkPath, platformVersion);
                    }
                }
                else
                {
                    DebugTrace.WriteLine($"Could not find matching extension SDK = '{extensionSdkMoniker}'", category: "GetExtensionSdkReferences");
                }
            }

            s_cachedExtensionSdkReferences.Add(cacheKey, extensionSdkReferences);
            return extensionSdkReferences;
        }
    }

    /// <summary>
    /// Get platform version string which is used to generate versioned path
    /// </summary>
    /// <param name="targetSdk">The target SDK</param>
    /// <param name="targetPlatformIdentifier">The target platform name.</param>
    /// <param name="targetPlatformVersion">The target platform version.</param>
    /// <returns>Return the version string if the platform is versioned, otherwise return empty string</returns>
    private static string GetPlatformVersion(TargetPlatformSDK targetSdk, string targetPlatformIdentifier, string targetPlatformVersion)
    {
        string platformKey = TargetPlatformSDK.GetSdkKey(targetPlatformIdentifier, targetPlatformVersion);
        if (TryGetPlatformManifest(targetSdk, platformKey, out var manifest) && manifest?.VersionedContent == true)
        {
            return manifest.PlatformVersion;
        }
        else
        {
            return string.Empty;
        }
    }
}
