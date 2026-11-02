// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;

#nullable disable

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
    /// <summary>
    /// Cache the set of target platform references for a particular combination of inputs.  For legacy
    /// target platforms, this is just grabbing all winmds from the References\CommonConfiguration\Neutral
    /// folder; for OneCore-based platforms, this involves reading the list from Platform.xml and synthesizing
    /// the locations.
    /// </summary>
    private static Dictionary<string, string[]> s_cachedTargetPlatformReferences;
    /// <summary>
    /// Get a list target platform sdks on the machine.
    /// </summary>
    /// <returns>List of Target Platform SDKs, Item1: TargetPlatformName Item2: Version of SDK Item3: Path to sdk root</returns>
    public static IList<TargetPlatformSDK> GetTargetPlatformSdks()
        => GetTargetPlatformSdks(null, null);

    /// <summary>
    /// Get a list target platform sdks on the machine.
    /// </summary>
    /// <param name="diskRoots">List of disk locations to search for platform sdks</param>
    /// <param name="registryRoot">Registry root location to look for platform sdks</param>
    /// <returns>List of Target Platform SDKs</returns>
    public static IList<TargetPlatformSDK> GetTargetPlatformSdks(string[] diskRoots, string registryRoot)
    {
        IEnumerable<TargetPlatformSDK> targetPlatforms = RetrieveTargetPlatformList(diskRoots, null, registryRoot);
        return targetPlatforms.Where(platform => platform.Path != null).ToList();
    }

    /// <summary>
    /// Filter list of platform sdks based on minimum OS and VS versions
    /// </summary>
    /// <param name="targetPlatformSdkList">List of platform sdks</param>
    /// <param name="osVersion">Operating System version. Pass null to not filter based on this parameter</param>
    /// <param name="vsVersion">Visual Studio version. Pass null not to filter based on this parameter</param>
    /// <returns>List of Target Platform SDKs</returns>
    public static IList<TargetPlatformSDK> FilterTargetPlatformSdks(IList<TargetPlatformSDK> targetPlatformSdkList, Version osVersion, Version vsVersion)
    {
        var filteredTargetPlatformSdkList = new List<TargetPlatformSDK>();

        foreach (TargetPlatformSDK targetPlatformSdk in targetPlatformSdkList)
        {
            if ((targetPlatformSdk.MinOSVersion == null || osVersion == null || targetPlatformSdk.MinOSVersion <= osVersion) && // filter based on OS version - let pass if not in manifest or parameter
                (targetPlatformSdk.MinVSVersion == null || vsVersion == null || targetPlatformSdk.MinVSVersion <= vsVersion))    // filter based on VS version - let pass if not in manifest or parameter
            {
                filteredTargetPlatformSdkList.Add(targetPlatformSdk);
            }
        }

        return filteredTargetPlatformSdkList;
    }

    /// <summary>
    /// Get the location of the target platform SDK props file for a given {SDKI, SDKV, TPI, TPMinV, TPV} combination.
    /// </summary>
    /// <param name="sdkIdentifier">The OneCore SDK identifier that defines OnceCore SDK root</param>
    /// <param name="sdkVersion">The verision of the OneCore SDK</param>
    /// <param name="targetPlatformIdentifier">Identifier for the targeted platform</param>
    /// <param name="targetPlatformMinVersion">The min version of the targeted platform</param>
    /// <param name="targetPlatformVersion">The version of the targeted platform</param>
    /// <returns>Location of the target platform SDK props file without .props filename</returns>
    public static string GetPlatformSDKPropsFileLocation(string sdkIdentifier, string sdkVersion, string targetPlatformIdentifier, string targetPlatformMinVersion, string targetPlatformVersion)
        => GetPlatformSDKPropsFileLocation(sdkIdentifier, sdkVersion, targetPlatformIdentifier, targetPlatformMinVersion, targetPlatformVersion, null, null);

    /// <summary>
    /// Get the location of the target platform SDK props file for a given {SDKI, SDKV, TPI, TPMinV, TPV} combination.
    /// </summary>
    /// <param name="sdkIdentifier">The OneCore SDK identifier that defines OnceCore SDK root</param>
    /// <param name="sdkVersion">The verision of the OneCore SDK</param>
    /// <param name="targetPlatformIdentifier">Identifier for the targeted platform</param>
    /// <param name="targetPlatformMinVersion">The min version of the targeted platform</param>
    /// <param name="targetPlatformVersion">The version of the targeted platform</param>
    /// <param name="diskRoots">List of disk roots to search for sdks within</param>
    /// <param name="registryRoot">Registry root to look for sdks within</param>
    /// <returns>Location of the target platform SDK props file without .props filename</returns>
    public static string GetPlatformSDKPropsFileLocation(string sdkIdentifier, string sdkVersion, string targetPlatformIdentifier, string targetPlatformMinVersion, string targetPlatformVersion, string diskRoots, string registryRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformIdentifier);
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformVersion);

        string propsFileLocation;
        try
        {
            // e.g. C:\Program Files (x86)\Windows Kits\8.2
            string sdkRoot = GetPlatformSDKLocation(targetPlatformIdentifier, targetPlatformVersion, diskRoots, registryRoot);

            if (!string.IsNullOrEmpty(sdkRoot))
            {
                // In the old SDK world, it is e.g. C:\Program Files (x86)\Windows Kits\8.2\DesignTime\CommonConfiguration\Neutral
                // In OneCore SDK world, it is e.g. C:\Program Files (x86)\Windows Kits\10.0\DesignTime\CommonConfiguration\Neutral\UAP\0.8.0.0
                if (string.IsNullOrEmpty(sdkIdentifier))
                {
                    propsFileLocation = Path.Combine(sdkRoot, designTimeFolderName, commonConfigurationFolderName, neutralArchitectureName);
                }
                else
                {
                    propsFileLocation = Path.Combine(sdkRoot, designTimeFolderName, commonConfigurationFolderName, neutralArchitectureName, targetPlatformIdentifier, targetPlatformVersion);
                }

                if (FileSystems.Default.DirectoryExists(propsFileLocation))
                {
                    return propsFileLocation;
                }
                else
                {
                    DebugTrace.WriteLine($"Target platform props file location '{propsFileLocation}' did not exist.");
                }
            }
            else
            {
                DebugTrace.WriteLine($"Could not find root SDK location for SDKI = '{sdkIdentifier}', SDKV = '{sdkVersion}'");
            }
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            DebugTrace.WriteLine($"Encountered exception trying to get the SDK props file Location : {e.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gathers the set of platform winmds for a particular {SDKI, SDKV, TPI, TPMinV, TPV} combination
    /// </summary>
    public static string[] GetTargetPlatformReferences(string sdkIdentifier, string sdkVersion, string targetPlatformIdentifier, string targetPlatformMinVersion, string targetPlatformVersion)
        => GetTargetPlatformReferences(sdkIdentifier, sdkVersion, targetPlatformIdentifier, targetPlatformMinVersion, targetPlatformVersion, null, null);

    /// <summary>
    /// Gathers the set of platform winmds for a particular {SDKI, SDKV, TPI, TPMinV, TPV} combination
    /// </summary>
    public static string[] GetTargetPlatformReferences(string sdkIdentifier, string sdkVersion, string targetPlatformIdentifier, string targetPlatformMinVersion, string targetPlatformVersion, string diskRoots, string registryRoot)
    {
        lock (s_locker)
        {
            s_cachedTargetPlatformReferences ??= new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            string cacheKey = string.Join("|", sdkIdentifier, sdkVersion, targetPlatformIdentifier, targetPlatformMinVersion, targetPlatformVersion, diskRoots, registryRoot);

            if (s_cachedTargetPlatformReferences.TryGetValue(cacheKey, out string[] targetPlatformReferences))
            {
                return targetPlatformReferences;
            }

            if (string.IsNullOrEmpty(sdkIdentifier) && string.IsNullOrEmpty(sdkVersion))
            {
                targetPlatformReferences = GetLegacyTargetPlatformReferences(targetPlatformIdentifier, targetPlatformVersion, diskRoots, registryRoot);
            }
            else
            {
                targetPlatformReferences = GetTargetPlatformReferencesFromManifest(sdkIdentifier, sdkVersion, targetPlatformIdentifier, targetPlatformMinVersion, targetPlatformVersion, diskRoots, registryRoot);
            }

            s_cachedTargetPlatformReferences.Add(cacheKey, targetPlatformReferences);
            return targetPlatformReferences;
        }
    }

    /// <summary>
    /// Gathers the set of platform winmds based on the assumption that they come from
    /// an SDK that is specified solely by TPI / TPV.
    /// </summary>
    private static string[] GetLegacyTargetPlatformReferences(string targetPlatformIdentifier, string targetPlatformVersion, string diskRoots, string registryRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformIdentifier);
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformVersion);

        try
        {
            // TODO: Add caching so that we only have to read all this stuff in once.
            string sdkRoot = GetPlatformSDKLocation(targetPlatformIdentifier, targetPlatformVersion, diskRoots, registryRoot);
            string winmdLocation = null;

            if (!string.IsNullOrEmpty(sdkRoot))
            {
                winmdLocation = Path.Combine(sdkRoot, referencesFolderName, commonConfigurationFolderName, neutralArchitectureName);

                if (!FileSystems.Default.DirectoryExists(winmdLocation))
                {
                    DebugTrace.WriteLine($"Target platform location '{winmdLocation}' did not exist");
                    winmdLocation = null;
                }
            }
            else
            {
                DebugTrace.WriteLine($"Could not find root SDK location for TPI = '{targetPlatformIdentifier}', TPV = '{targetPlatformVersion}'");
            }

            if (!string.IsNullOrEmpty(winmdLocation))
            {
                string[] winmdPaths = Directory.GetFiles(winmdLocation, "*.winmd");

                if (winmdPaths.Length > 0)
                {
                    DebugTrace.WriteLine($"Found {winmdPaths.Length} contract winmds in '{winmdLocation}'");
                    return winmdPaths;
                }
            }
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            DebugTrace.WriteLine($"Encountered exception trying to gather the platform references: {e.Message}");
        }

        return [];
    }

    /// <summary>
    /// Gathers the set of platform winmds for a particular {SDKI, SDKV, TPI, TPMinV, TPV} combination,
    /// based on the assumption that it is an SDK that has both {SDKI, SDKV} and TP* specifiers.
    /// </summary>
    private static string[] GetTargetPlatformReferencesFromManifest(
        string sdkIdentifier,
        string sdkVersion,
        string targetPlatformIdentifier,
        string targetPlatformMinVersion,
        string targetPlatformVersion,
        string diskRoots,
        string registryRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(sdkIdentifier);
        ArgumentException.ThrowIfNullOrEmpty(sdkVersion);
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformIdentifier);
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformVersion);

        string[] contractWinMDs = [];

        TargetPlatformSDK matchingSdk = GetMatchingPlatformSDK(targetPlatformIdentifier, targetPlatformVersion, diskRoots, null, registryRoot);
        string platformKey = TargetPlatformSDK.GetSdkKey(targetPlatformIdentifier, targetPlatformVersion);
        if (TryGetPlatformManifest(matchingSdk, platformKey, out PlatformManifest manifest))
        {
            if (manifest.VersionedContent)
            {
                contractWinMDs = GetApiContractReferences(manifest.ApiContracts, matchingSdk.Path, manifest.PlatformVersion);
            }
            else
            {
                contractWinMDs = GetApiContractReferences(manifest.ApiContracts, matchingSdk.Path);
            }
        }

        return contractWinMDs;
    }

    /// <summary>
    /// Return the WinMD paths referenced by the given api contracts and target sdk root
    /// </summary>
    /// <param name="apiContracts">The API contract definitions</param>
    /// <param name="targetPlatformSdkRoot">The root of the target platform SDK</param>
    /// <returns>List of matching WinMDs</returns>
    internal static string[] GetApiContractReferences(IEnumerable<ApiContract> apiContracts, string targetPlatformSdkRoot)
        => GetApiContractReferences(apiContracts, targetPlatformSdkRoot, string.Empty);

    /// <summary>
    /// Return the WinMD paths referenced by the given api contracts and target sdk root
    /// </summary>
    /// <param name="apiContracts">The API contract definitions</param>
    /// <param name="targetPlatformSdkRoot">The root of the target platform SDK</param>
    /// <param name="targetPlatformSdkVersion">The version of the target platform SDK</param>
    /// <returns>List of matching WinMDs</returns>
    internal static string[] GetApiContractReferences(IEnumerable<ApiContract> apiContracts, string targetPlatformSdkRoot, string targetPlatformSdkVersion)
    {
        if (apiContracts == null)
        {
            return [];
        }

        var contractWinMDs = new List<string>();

        string referencesRoot = Path.Combine(targetPlatformSdkRoot, referencesFolderName, targetPlatformSdkVersion);

        foreach (ApiContract contract in apiContracts)
        {
            DebugTrace.WriteLine($"Gathering contract references for contract with name '{contract.Name}' and version '{contract.Version}'");
            string contractPath = Path.Combine(referencesRoot, contract.Name, contract.Version);

            if (FileSystems.Default.DirectoryExists(contractPath))
            {
                string[] winmdPaths = Directory.GetFiles(contractPath, "*.winmd");

                if (winmdPaths.Length > 0)
                {
                    DebugTrace.WriteLine($"Found {winmdPaths.Length} contract winmds in '{contractPath}'");
                    contractWinMDs.AddRange(winmdPaths);
                }
            }
        }

        return contractWinMDs.ToArray();
    }

    private static bool TryGetPlatformManifest(TargetPlatformSDK matchingSdk, string platformKey, out PlatformManifest manifest)
    {
        manifest = null;
        try
        {
            string platformManifestLocation = null;

            if (matchingSdk != null)
            {
                if (!matchingSdk.Platforms.TryGetValue(platformKey, out platformManifestLocation))
                {
                    DebugTrace.WriteLine($"Target platform location '{platformManifestLocation}' did not exist or did not contain Platform.xml", category: "GetPlatformManifest");
                }
            }
            else
            {
                DebugTrace.WriteLine($"Could not find root SDK for '{platformKey}'", category: "GetPlatformManifest");
            }

            if (!string.IsNullOrEmpty(platformManifestLocation))
            {
                manifest = new PlatformManifest(platformManifestLocation);

                if (!manifest.ReadError)
                {
                    return true;
                }
            }
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            DebugTrace.WriteLine($"Encountered exception trying to check if SDK is versioned: {e.Message}", category: "GetValueUsingMatchingSDKManifest");
        }

        return false;
    }

    /// <summary>
    /// Given a target platform identifier and a target platform version search the default sdk locations for the platform sdk for the target platform.
    /// </summary>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <returns>Location of the SDK if it is found, empty string if it could not be found</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformSDKLocation(string targetPlatformIdentifier, Version targetPlatformVersion)
        => GetPlatformSDKLocation(targetPlatformIdentifier, targetPlatformVersion, null, null);

    /// <summary>
    /// Given a target platform identifier and a target platform version search the default sdk locations for the platform sdk for the target platform.
    /// </summary>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <param name="diskRoots">List of disk roots to search for sdks within</param>
    /// <param name="registryRoot">Registry root to look for sdks within</param>
    /// <returns>Location of the SDK if it is found, empty string if it could not be found</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformSDKLocation(string targetPlatformIdentifier, Version targetPlatformVersion, string[] diskRoots, string registryRoot)
    {
        var targetPlatform = GetMatchingPlatformSDK(targetPlatformIdentifier, targetPlatformVersion, diskRoots, null, registryRoot);
        return targetPlatform?.Path ?? string.Empty;
    }

    /// <summary>
    /// Given a target platform identifier and a target platform version search the default sdk locations for the platform sdk for the target platform.
    /// </summary>
    /// <param name="targetPlatformIdentifier">Identifier for the platform</param>
    /// <param name="targetPlatformVersion">Version of the platform</param>
    /// <returns>A full path to the sdk root if the sdk exists in the targeted platform or an empty string if it does not exist.</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformSDKLocation(string targetPlatformIdentifier, string targetPlatformVersion)
        => GetPlatformSDKLocation(targetPlatformIdentifier, targetPlatformVersion, null, null);

    /// <summary>
    /// Given a target platform identifier and a target platform version search the default sdk locations for the platform sdk for the target platform.
    /// </summary>
    /// <param name="targetPlatformIdentifier">Targeted platform to find SDKs for</param>
    /// <param name="targetPlatformVersion">Targeted platform version to find SDKs for</param>
    /// <param name="diskRoots">List of disk roots to search for sdks within</param>
    /// <param name="registryRoot">Registry root to look for sdks within</param>
    /// <returns>Location of the platform SDK if it is found, empty string if it could not be found</returns>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static string GetPlatformSDKLocation(string targetPlatformIdentifier, string targetPlatformVersion, string diskRoots, string registryRoot)
    {
        ArgumentNullException.ThrowIfNull(targetPlatformVersion);

        string[] sdkDiskRoots = null;
        if (!string.IsNullOrEmpty(diskRoots))
        {
            sdkDiskRoots = diskRoots.Split(s_diskRootSplitChars, StringSplitOptions.RemoveEmptyEntries);
        }

        string sdkLocation = string.Empty;
        if (Version.TryParse(targetPlatformVersion, out Version platformVersion))
        {
            sdkLocation = GetPlatformSDKLocation(targetPlatformIdentifier, platformVersion, sdkDiskRoots, registryRoot);
        }

        return sdkLocation;
    }

    /// <summary>
    /// Given a target platform identifier and version, get the display name for that platform SDK.
    /// </summary>
    public static string GetPlatformSDKDisplayName(string targetPlatformIdentifier, string targetPlatformVersion)
        => GetPlatformSDKDisplayName(targetPlatformIdentifier, targetPlatformVersion, null, null);

    /// <summary>
    /// Given a target platform identifier and version, get the display name for that platform SDK.
    /// </summary>
    public static string GetPlatformSDKDisplayName(string targetPlatformIdentifier, string targetPlatformVersion, string diskRoots, string registryRoot)
    {
        TargetPlatformSDK targetPlatform = GetMatchingPlatformSDK(targetPlatformIdentifier, targetPlatformVersion, diskRoots, null, registryRoot);
        return targetPlatform?.DisplayName ?? GenerateDefaultSDKDisplayName(targetPlatformIdentifier, targetPlatformVersion);
    }

    /// <summary>
    /// Given an SDK identifier and an SDK version, return a list of installed platforms.
    /// </summary>
    /// <param name="sdkIdentifier">SDK for which to find the installed platforms</param>
    /// <param name="sdkVersion">SDK version for which to find the installed platforms</param>
    /// <returns>A list of keys for the installed platforms for the given SDK</returns>
    public static IEnumerable<string> GetPlatformsForSDK(string sdkIdentifier, Version sdkVersion)
        => GetPlatformsForSDK(sdkIdentifier, sdkVersion, null, null);

    /// <summary>
    /// Given an SDK identifier and an SDK version, return a list of installed platforms.
    /// </summary>
    /// <param name="sdkIdentifier">SDK for which to find the installed platforms</param>
    /// <param name="sdkVersion">SDK version for which to find the installed platforms</param>
    /// <param name="diskRoots">List of disk roots to search for sdks within</param>
    /// <param name="registryRoot">Registry root to look for sdks within</param>
    /// <returns>A list of keys for the installed platforms for the given SDK</returns>
    public static IEnumerable<string> GetPlatformsForSDK(string sdkIdentifier, Version sdkVersion, string[] diskRoots, string registryRoot)
    {
        ArgumentNullException.ThrowIfNull(sdkIdentifier);
        ArgumentNullException.ThrowIfNull(sdkVersion);

        IEnumerable<TargetPlatformSDK> targetPlatformSDKs = RetrieveTargetPlatformList(diskRoots, null, registryRoot);

        var platforms = new List<string>();
        foreach (TargetPlatformSDK sdk in targetPlatformSDKs)
        {
            bool isSDKMatch = string.Equals(sdk.TargetPlatformIdentifier, sdkIdentifier, StringComparison.OrdinalIgnoreCase) && Equals(sdk.TargetPlatformVersion, sdkVersion);
            if (!isSDKMatch || sdk.Platforms == null)
            {
                continue;
            }

            foreach (string platform in sdk.Platforms.Keys)
            {
                platforms.Add(platform);
            }
        }

        return platforms;
    }

    /// <summary>
    /// Given an SDK Identifier and SDK version, return the latest installed platform.
    /// </summary>
    /// <param name="sdkIdentifier">SDK for which to find the latest installed platform</param>
    /// <param name="sdkVersion">SDK version for which to find the latest installed platform</param>
    /// <returns>The latest installed version for the given SDK</returns>
    public static string GetLatestSDKTargetPlatformVersion(string sdkIdentifier, string sdkVersion)
        => GetLatestSDKTargetPlatformVersion(sdkIdentifier, sdkVersion, null);

    /// <summary>
    /// Given an SDK Identifier and SDK version, return the latest installed platform.
    /// </summary>
    /// <param name="sdkIdentifier">SDK for which to find the latest installed platform</param>
    /// <param name="sdkVersion">SDK version for which to find the latest installed platform</param>
    /// <param name="sdkRoots">SDK Root folders</param>
    /// <returns>The latest installed version for the given SDK</returns>
    public static string GetLatestSDKTargetPlatformVersion(string sdkIdentifier, string sdkVersion, string[] sdkRoots)
    {
        ArgumentNullException.ThrowIfNull(sdkIdentifier);
        ArgumentNullException.ThrowIfNull(sdkVersion);

        var availablePlatformVersions = new List<Version>();
        IEnumerable<string> platformMonikerList = GetPlatformsForSDK(sdkIdentifier, new Version(sdkVersion), sdkRoots, null);

        Version platformVersion;
        foreach (string platformMoniker in platformMonikerList)
        {
            if (TryParsePlatformVersion(platformMoniker, out platformVersion))
            {
                availablePlatformVersions.Add(platformVersion);
            }
        }

        if (availablePlatformVersions?.Count > 0)
        {
            return availablePlatformVersions.OrderByDescending(x => x).FirstOrDefault().ToString();
        }
        else
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Tries to parse the "version" out of a platformMoniker.
    /// </summary>
    /// <param name="platformMoniker">PlatformMoniker, in the form "PlatformName, Version=version"</param>
    /// <param name="platformVersion">The version of the platform, if the parse was successful - Else set to null</param>
    /// <returns>True if parse was successful, false otherwise </returns>
    private static bool TryParsePlatformVersion(string platformMoniker, out Version platformVersion)
    {
        platformVersion = null;
        FrameworkNameVersioning framework = null;
        try
        {
            framework = new FrameworkNameVersioning(platformMoniker);
        }
        catch (ArgumentException e)
        {
            DebugTrace.WriteLine($"Cannot create FrameworkName object, Exception:{e.Message}");
        }

        if (framework != null)
        {
            platformVersion = framework.Version;
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Given a target platform identifier and version and locations in which to search, find the TargetPlatformSDK
    /// object that matches.
    /// </summary>
    private static TargetPlatformSDK GetMatchingPlatformSDK(string targetPlatformIdentifier, string targetPlatformVersion, string diskRoots, string multiPlatformDiskRoots, string registryRoot)
    {
        ArgumentNullException.ThrowIfNull(targetPlatformVersion);

        string[] sdkDiskRoots = null;
        if (!string.IsNullOrEmpty(diskRoots))
        {
            sdkDiskRoots = diskRoots.Split(s_diskRootSplitChars, StringSplitOptions.RemoveEmptyEntries);
        }

        string[] sdkmultiPlatformDiskRoots = null;
        if (!string.IsNullOrEmpty(multiPlatformDiskRoots))
        {
            sdkmultiPlatformDiskRoots = multiPlatformDiskRoots.Split(s_diskRootSplitChars, StringSplitOptions.RemoveEmptyEntries);
        }

        if (Version.TryParse(targetPlatformVersion, out Version platformVersion))
        {
            return GetMatchingPlatformSDK(targetPlatformIdentifier, platformVersion, sdkDiskRoots, sdkmultiPlatformDiskRoots, registryRoot);
        }

        return null;
    }

    /// <summary>
    /// Given a target platform identifier and version and locations in which to search, find the TargetPlatformSDK
    /// object that matches.
    /// </summary>
    private static TargetPlatformSDK GetMatchingPlatformSDK(string targetPlatformIdentifier, Version targetPlatformVersion, string[] diskRoots, string[] multiPlatformDiskRoots, string registryRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPlatformIdentifier);
        ArgumentNullException.ThrowIfNull(targetPlatformVersion);

        IEnumerable<TargetPlatformSDK> targetPlatforms = RetrieveTargetPlatformList(diskRoots, multiPlatformDiskRoots, registryRoot);

        TargetPlatformSDK matchingSdk = targetPlatforms
            .FirstOrDefault(platform =>
                string.Equals(platform.TargetPlatformIdentifier, targetPlatformIdentifier, StringComparison.OrdinalIgnoreCase)
                && Equals(platform.TargetPlatformVersion, targetPlatformVersion));

        // For UAP platforms match against registered platforms...
        // Logic is same as used for managed UAP projects
        // vsproject\flavors\ProjectFlavoring\Microsoft.VisualStudio.ProjectFlavoring\Microsoft\VisualStudio\ProjectFlavoring\Retargeting\Management\VsMultiTargetingPlatformProvider.cs:FindPlatformSdk
        if (matchingSdk == null)
        {
            string versionString = targetPlatformVersion.ToString();
            matchingSdk = targetPlatforms.FirstOrDefault(platform => platform.ContainsPlatform(targetPlatformIdentifier, versionString));
        }

        return matchingSdk;
    }

    /// <summary>
    /// Given a target platform identifier and version, generate a reasonable default display name.
    /// </summary>
    /// <param name="targetPlatformIdentifier"></param>
    /// <param name="targetPlatformVersion"></param>
    private static string GenerateDefaultSDKDisplayName(string targetPlatformIdentifier, string targetPlatformVersion)
        => targetPlatformIdentifier + " " + targetPlatformVersion;
}
