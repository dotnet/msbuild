// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Win32;

#nullable disable

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
    /// <summary>
    /// Cache the sdk structure as found by enumerating the disk and registry.
    /// </summary>
    private static Dictionary<string, IEnumerable<TargetPlatformSDK>> s_cachedTargetPlatforms;

    /// <summary>
    /// Cache new style extension sdks that we've enumerated
    /// </summary>
    private static Dictionary<string, TargetPlatformSDK> s_cachedExtensionSdks;

    private const string platformsFolderName = "Platforms";
    private const string uapDirectoryName = "Windows Kits";
    private const string uapRegistryName = "Windows";
    private const int uapVersion = 10;

    private static readonly char[] s_diskRootSplitChars = MSBuildConstants.SemicolonChar;

    /// <summary>
    /// Clear out the appdomain wide cache of Platform and Extension SDKs.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDK", Justification = "Shipped this way in Dev11 Beta (go-live)")]
    public static void ClearSDKStaticCache()
    {
        lock (s_locker)
        {
            s_cachedTargetPlatforms?.Clear();
            s_cachedTargetPlatformReferences?.Clear();
            s_cachedExtensionSdks?.Clear();
            s_cachedExtensionSdkReferences?.Clear();
        }
    }

    /// <summary>
    /// Get the list of extension sdks for a given platform and version
    /// </summary>
    private static IEnumerable<TargetPlatformSDK> RetrieveTargetPlatformList(string[] diskRoots, string[] extensionDiskRoots, string registrySearchLocation)
    {
        // Get the disk and registry roots to search for sdks under
        List<string> sdkDiskRoots = GetTargetPlatformMonikerDiskRoots(diskRoots);
        List<string> extensionSdkDiskRoots = GetExtensionSdkDiskRoots(extensionDiskRoots);

        string registryRoot = NativeMethodsShared.IsWindows ? GetTargetPlatformMonikerRegistryRoots(registrySearchLocation) : string.Empty;

        string cachedTargetPlatformsKey = string.Join("|",
            string.Join(";", sdkDiskRoots),
            registryRoot);

        string cachedExtensionSdksKey = extensionDiskRoots == null ? string.Empty : string.Join(";", extensionDiskRoots);

        lock (s_locker)
        {
            s_cachedTargetPlatforms ??= new Dictionary<string, IEnumerable<TargetPlatformSDK>>(StringComparer.OrdinalIgnoreCase);

           s_cachedExtensionSdks ??= new Dictionary<string, TargetPlatformSDK>(StringComparer.OrdinalIgnoreCase);

            if (!s_cachedTargetPlatforms.TryGetValue(cachedTargetPlatformsKey, out IEnumerable<TargetPlatformSDK> collection))
            {
                var monikers = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();
                GatherSDKListFromDirectory(sdkDiskRoots, monikers);

                if (NativeMethodsShared.IsWindows)
                {
                    GatherSDKListFromRegistry(registryRoot, monikers);
                }

                collection = monikers.Keys.ToList();
                s_cachedTargetPlatforms.Add(cachedTargetPlatformsKey, collection);
            }

            if (!string.IsNullOrEmpty(cachedExtensionSdksKey))
            {
                if (!s_cachedExtensionSdks.TryGetValue(cachedExtensionSdksKey, out TargetPlatformSDK extensionSdk))
                {
                    // These extension SDKs can target multiple platforms under the same Target SDK, stash in a null platform key for later filtering
                    extensionSdk = new TargetPlatformSDK(string.Empty, new Version(0, 0), null);

                    GatherExtensionSDKListFromDirectory(extensionSdkDiskRoots, extensionSdk);
                    s_cachedExtensionSdks.Add(cachedExtensionSdksKey, extensionSdk);
                }
                collection = collection.Concat([extensionSdk]);
            }

            return collection;
        }
    }

    /// <summary>
    /// Gets new style extension SDKs (those that are under the target SDK name and version and are driven by manifest, not directory structure).
    /// </summary>
    private static void GatherExtensionSDKListFromDirectory(IEnumerable<string> diskRoots, TargetPlatformSDK extensionSdk)
    {
        // In this case we're passing in roots with the SDK and Version, such as C:\Program Files (x86)\Windows SDKs\1.0
        foreach (string diskRoot in diskRoots)
        {
            DirectoryInfo rootInfo = new DirectoryInfo(diskRoot);
            if (!rootInfo.Exists)
            {
                DebugTrace.WriteLine($"DiskRoot '{diskRoot}' does not exist, skipping it");
                continue;
            }

            // Leave this entry as partners have already started to develop against this path, we will eventually remove this
            DirectoryInfo extensionSdksDirectory = rootInfo.GetDirectories("Extension SDKs", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (extensionSdksDirectory != null)
            {
                GatherExtensionSDKs(extensionSdksDirectory, extensionSdk);
            }

            DirectoryInfo extensionSdksDirectory2 = rootInfo.GetDirectories("ExtensionSDKs", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (extensionSdksDirectory2 != null)
            {
                GatherExtensionSDKs(extensionSdksDirectory2, extensionSdk);
            }
        }
    }

    internal static void GatherExtensionSDKs(DirectoryInfo extensionSdksDirectory, TargetPlatformSDK targetPlatformSDK)
    {
        DebugTrace.WriteLine($"Found ExtensionsSDK folder '{extensionSdksDirectory.FullName}'. ");

        DirectoryInfo[] sdkNameDirectories = extensionSdksDirectory.GetDirectories();
        DebugTrace.WriteLine($"Found '{sdkNameDirectories.Length}' sdkName directories under '{extensionSdksDirectory.FullName}'");

        // For each SDKName under the ExtensionSDKs directory
        foreach (DirectoryInfo sdkNameFolders in sdkNameDirectories)
        {
            DirectoryInfo[] sdkVersionDirectories = sdkNameFolders.GetDirectories();
            DebugTrace.WriteLine($"Found '{sdkVersionDirectories.Length}' sdkVersion directories under '{sdkNameFolders.FullName}'");

            // For each Version directory under the SDK Name
            foreach (DirectoryInfo sdkVersionDirectory in sdkVersionDirectories)
            {
                // Make sure the version folder parses to a version, anything that cannot parse directly to a version is to be ignored.
                DebugTrace.WriteLine($"Parsed sdk version folder '{sdkVersionDirectory.Name}' under '{sdkVersionDirectory.FullName}'");
                if (Version.TryParse(sdkVersionDirectory.Name, out Version _))
                {
                    // Create SDK name based on the folder structure. We could open the manifest here and read the display name, but that would
                    // add complexity and since things are supposed to be in a certain structure I don't think that is needed at this point.
                    string SDKKey = TargetPlatformSDK.GetSdkKey(sdkNameFolders.Name, sdkVersionDirectory.Name);

                    // Make sure we have not added the SDK to the list of found SDKs before.
                    if (!targetPlatformSDK.ExtensionSDKs.ContainsKey(SDKKey))
                    {
                        DebugTrace.WriteLine($"SDKKey '{SDKKey}' was not already found.");
                        string pathToSDKManifest = Path.Combine(sdkVersionDirectory.FullName, "SDKManifest.xml");
                        if (FileUtilities.FileExistsNoThrow(pathToSDKManifest))
                        {
                            targetPlatformSDK.ExtensionSDKs.Add(SDKKey, FileUtilities.EnsureTrailingSlash(sdkVersionDirectory.FullName));
                        }
                        else
                        {
                            DebugTrace.WriteLine($"No SDKManifest.xml files could be found at '{pathToSDKManifest}'. Not adding sdk");
                        }
                    }
                    else
                    {
                        DebugTrace.WriteLine($"SDKKey '{SDKKey}' was already found, not adding sdk under '{sdkVersionDirectory.FullName}'");
                    }
                }
                else
                {
                    DebugTrace.WriteLine($"Failed to parse sdk version folder '{sdkVersionDirectory.Name}' under '{sdkVersionDirectory.FullName}'");
                }
            }
        }
    }

    /// <summary>
    /// Given a root disk location and the target platform properties find all of the SDKs installed in that location.
    /// </summary>
    internal static void GatherSDKListFromDirectory(List<string> diskroots, Dictionary<TargetPlatformSDK, TargetPlatformSDK> platformSDKs)
    {
        foreach (string diskRoot in diskroots)
        {
            DirectoryInfo rootInfo = new DirectoryInfo(diskRoot);
            if (!rootInfo.Exists)
            {
                DebugTrace.WriteLine($"DiskRoot '{diskRoot}' does not exist, skipping it");
                continue;
            }

            foreach (DirectoryInfo rootPathWithIdentifier in rootInfo.GetDirectories())
            {
                // This makes a list of directories under the target framework identifier.
                // This should make something like c:\Program files\Microsoft SDKs\Windows
                if (!rootPathWithIdentifier.Exists)
                {
                    DebugTrace.WriteLine($"Disk root with Identifier: '{rootPathWithIdentifier}' does not exist. ");
                    continue;
                }

                DebugTrace.WriteLine($"Disk root with Identifier: '{rootPathWithIdentifier}' does exist. Enumerating version folders under it. ");

                // Get a list of subdirectories under the root path and identifier, Ie. c:\Program files\Microsoft SDKs\Windows we should see things like, V8.0, 8.0, 9.0 ect.
                // Only grab the folders that have a version number (they can start with a v or not).

                SortedDictionary<Version, List<string>> versionsInRoot = VersionUtilities.GatherVersionStrings(null, rootPathWithIdentifier.GetDirectories().Select(directory => directory.Name));

                DebugTrace.WriteLine($"Found '{versionsInRoot.Count}' version folders under the identifier path '{rootPathWithIdentifier}'.");

                // Go through each of the targetplatform versions under the targetplatform identifier.
                foreach (KeyValuePair<Version, List<string>> directoryUnderRoot in versionsInRoot)
                {
                    TargetPlatformSDK platformSDKKey;

                    if (rootPathWithIdentifier.Name.Equals(uapDirectoryName, StringComparison.OrdinalIgnoreCase) && directoryUnderRoot.Key.Major == uapVersion)
                    {
                        platformSDKKey = new TargetPlatformSDK(uapRegistryName, directoryUnderRoot.Key, null);
                    }
                    else
                    {
                        platformSDKKey = new TargetPlatformSDK(rootPathWithIdentifier.Name, directoryUnderRoot.Key, null);
                    }

                    TargetPlatformSDK targetPlatformSDK = null;

                    // DirectoryUnderRoot.Value will be a list of the raw directory strings under the targetplatform identifier directory that map to the versions specified in directoryUnderRoot.Key.
                    foreach (string version in directoryUnderRoot.Value)
                    {
                        // This should make something like c:\Program files\Microsoft SDKs\Windows\v8.0\
                        string platformSDKDirectory = Path.Combine(rootPathWithIdentifier.FullName, version);
                        string platformSDKManifest = Path.Combine(platformSDKDirectory, "SDKManifest.xml");

                        // If we are gathering the sdk platform manifests then check to see if there is a sdk manifest in the directory if not then skip over it as a platform sdk
                        bool platformSDKManifestExists = FileSystems.Default.FileExists(platformSDKManifest);
                        if (targetPlatformSDK == null && !platformSDKs.TryGetValue(platformSDKKey, out targetPlatformSDK))
                        {
                            targetPlatformSDK = new TargetPlatformSDK(platformSDKKey.TargetPlatformIdentifier, platformSDKKey.TargetPlatformVersion, platformSDKManifestExists ? platformSDKDirectory : null);
                            platformSDKs.Add(targetPlatformSDK, targetPlatformSDK);
                        }

                        if (targetPlatformSDK.Path == null && platformSDKManifestExists)
                        {
                            targetPlatformSDK.Path = platformSDKDirectory;
                        }

                        // Gather the set of platforms supported by this SDK if it's a valid one.
                        if (!string.IsNullOrEmpty(targetPlatformSDK.Path))
                        {
                            GatherPlatformsForSdk(targetPlatformSDK);
                        }

                        // If we are passed an extension sdk dictionary we will continue to look through the extension sdk directories and try and fill it up.
                        // This should make something like c:\Program files\Microsoft SDKs\Windows\v8.0\ExtensionSDKs
                        string sdkFolderPath = Path.Combine(platformSDKDirectory, "ExtensionSDKs");
                        DirectoryInfo extensionSdksDirectory = new DirectoryInfo(sdkFolderPath);

                        if (extensionSdksDirectory.Exists)
                        {
                            GatherExtensionSDKs(extensionSdksDirectory, targetPlatformSDK);
                        }
                        else
                        {
                            DebugTrace.WriteLine($"Could not find ExtensionsSDK folder '{sdkFolderPath}'. ");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Given a registry location enumerate the registry and find the installed SDKs.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static void GatherSDKsFromRegistryImpl(Dictionary<TargetPlatformSDK, TargetPlatformSDK> platformMonikers, string registryKeyRoot, RegistryView registryView, RegistryHive registryHive, GetRegistrySubKeyNames getRegistrySubKeyNames, GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue, OpenBaseKey openBaseKey, FileExists fileExists)
    {
        ArgumentNullException.ThrowIfNull(platformMonikers, "PlatformMonikers");

        if (string.IsNullOrEmpty(registryKeyRoot))
        {
            return;
        }

        // Open the hive for a given view
        using (RegistryKey baseKey = openBaseKey(registryHive, registryView))
        {
            DebugTrace.WriteLine($"Gathering SDKS from registryRoot '{registryKeyRoot}', Hive '{registryHive}', View '{registryView}'");

            // Attach the target platform to the registry root. This should give us something like
            // SOFTWARE\MICROSOFT\Microsoft SDKs\Windows

            // Get all of the platform identifiers
            IEnumerable<string> platformIdentifiers = getRegistrySubKeyNames(baseKey, registryKeyRoot);

            // No identifiers found.
            if (platformIdentifiers == null)
            {
                DebugTrace.WriteLine($"No sub keys found under registryKeyRoot {registryKeyRoot}");
                return;
            }

            foreach (string platformIdentifier in platformIdentifiers)
            {
                string platformIdentifierKey = registryKeyRoot + @"\" + platformIdentifier;

                // Get all of the version folders under the targetplatform identifier key
                IEnumerable<string> versions = getRegistrySubKeyNames(baseKey, platformIdentifierKey);

                // No versions found.
                if (versions == null)
                {
                    DebugTrace.WriteLine($"No sub keys found under platformIdentifierKey {platformIdentifierKey}");
                    return;
                }

                // Returns a a sorted set of versions and their associated registry strings. The reason we need the original strings is that
                // they may contain a v where as a version does not support a v.
                SortedDictionary<Version, List<string>> sortedVersions = VersionUtilities.GatherVersionStrings(null, versions);

                foreach (KeyValuePair<Version, List<string>> registryVersions in sortedVersions)
                {
                    TargetPlatformSDK platformSDKKey = new TargetPlatformSDK(platformIdentifier, registryVersions.Key, null);
                    TargetPlatformSDK targetPlatformSDK = null;

                    // Go through each of the raw version strings which were found in the registry
                    foreach (string version in registryVersions.Value)
                    {
                        // Attach the version and extensionSDKs strings to the platformIdentifier key we built up above.
                        // Make something like SOFTWARE\MICROSOFT\Microsoft SDKs\Windows\8.0\
                        string platformSDKsRegistryKey = platformIdentifierKey + @"\" + version;

                        string platformSDKDirectory = getRegistrySubKeyDefaultValue(baseKey, platformSDKsRegistryKey);

                        // May be null because some use installationfolder instead
                        if (platformSDKDirectory == null)
                        {
                            using (RegistryKey versionKey = baseKey.OpenSubKey(platformSDKsRegistryKey))
                            {
                                if (versionKey != null)
                                {
                                    platformSDKDirectory = versionKey.GetValue("InstallationFolder") as string;
                                }
                            }
                        }

                        bool platformSDKmanifestExists = false;

                        if (platformSDKDirectory != null)
                        {
                            string platformSDKManifest = Path.Combine(platformSDKDirectory, "SDKManifest.xml");
                            // Windows kits is special because they do not have an sdk manifest yet, this is for the windows sdk. We will accept them as they are. For others
                            // we will require that an sdkmanifest exists.
                            platformSDKmanifestExists = fileExists(platformSDKManifest) || platformSDKDirectory.IndexOf("Windows Kits", StringComparison.OrdinalIgnoreCase) >= 0;
                        }

                        if (targetPlatformSDK == null && !platformMonikers.TryGetValue(platformSDKKey, out targetPlatformSDK))
                        {
                            targetPlatformSDK = new TargetPlatformSDK(platformSDKKey.TargetPlatformIdentifier, platformSDKKey.TargetPlatformVersion, platformSDKmanifestExists ? platformSDKDirectory : null);
                            platformMonikers.Add(targetPlatformSDK, targetPlatformSDK);
                        }

                        if (targetPlatformSDK.Path == null && platformSDKmanifestExists)
                        {
                            targetPlatformSDK.Path = platformSDKDirectory;
                        }

                        // Gather the set of platforms supported by this SDK if it's a valid one.
                        if (!string.IsNullOrEmpty(targetPlatformSDK.Path))
                        {
                            GatherPlatformsForSdk(targetPlatformSDK);
                        }

                        // Make something like SOFTWARE\MICROSOFT\Microsoft SDKs\Windows\8.0\ExtensionSdks
                        string extensionSDKsKey = platformSDKsRegistryKey + @"\ExtensionSDKs";
                        DebugTrace.WriteLine($"Getting subkeys of '{extensionSDKsKey}'");

                        // Get all of the SDK name folders under the ExtensionSDKs registry key
                        IEnumerable<string> sdkNames = getRegistrySubKeyNames(baseKey, extensionSDKsKey);
                        if (sdkNames == null)
                        {
                            DebugTrace.WriteLine($"Could not find subkeys of '{extensionSDKsKey}'");
                            continue;
                        }

                        DebugTrace.WriteLine($"Found subkeys of '{extensionSDKsKey}'");

                        // For each SDK folder under ExtensionSDKs
                        foreach (string sdkName in sdkNames)
                        {
                            // Combine the SDK Name with the ExtensionSDKs key we have built up above.
                            // Make something like SOFTWARE\MICROSOFT\Windows SDKs\Windows\8.0\ExtensionSDKs\XNA
                            string sdkNameKey = extensionSDKsKey + @"\" + sdkName;

                            // Get all of the version registry keys under the SDK Name Key.
                            IEnumerable<string> sdkVersions = getRegistrySubKeyNames(baseKey, sdkNameKey);

                            DebugTrace.WriteLine($"Getting subkeys of '{sdkNameKey}'");
                            if (sdkVersions == null)
                            {
                                DebugTrace.WriteLine($"Could not find subkeys of '{sdkNameKey}'");
                                continue;
                            }

                            DebugTrace.WriteLine($"Found subkeys of '{sdkNameKey}'");

                            // For each version registry entry under the SDK Name registry key
                            foreach (string sdkVersion in sdkVersions)
                            {
                                // We only want registry keys which parse directly to versions
                                Version tempVersion;
                                if (Version.TryParse(sdkVersion, out tempVersion))
                                {
                                    string sdkDirectoryKey = sdkNameKey + @"\" + sdkVersion;
                                    DebugTrace.WriteLine($"Getting default key for '{sdkDirectoryKey}'");

                                    // Now that we found the registry key we need to get its default value which points to the directory this SDK is in.
                                    string directoryName = getRegistrySubKeyDefaultValue(baseKey, sdkDirectoryKey);
                                    string sdkKey = TargetPlatformSDK.GetSdkKey(sdkName, sdkVersion);
                                    if (directoryName != null)
                                    {
                                        DebugTrace.WriteLine($"SDK installation location = '{directoryName}'");

                                        // Make sure the directory exists and that it has not been added before.
                                        if (!targetPlatformSDK.ExtensionSDKs.ContainsKey(sdkKey))
                                        {
                                            if (FileUtilities.DirectoryExistsNoThrow(directoryName))
                                            {
                                                string sdkManifestFileLocation = Path.Combine(directoryName, "SDKManifest.xml");
                                                if (fileExists(sdkManifestFileLocation))
                                                {
                                                    DebugTrace.WriteLine($"Adding SDK '{sdkKey}'  at '{directoryName}' to the list of found sdks.");
                                                    targetPlatformSDK.ExtensionSDKs.Add(sdkKey, FileUtilities.EnsureTrailingSlash(directoryName));
                                                }
                                                else
                                                {
                                                    DebugTrace.WriteLine($"No SDKManifest.xml file found at '{sdkManifestFileLocation}'.");
                                                }
                                            }
                                            else
                                            {
                                                DebugTrace.WriteLine($"SDK directory '{directoryName}' does not exist");
                                            }
                                        }
                                        else
                                        {
                                            DebugTrace.WriteLine($"SDK key was previously added. '{sdkKey}'");
                                        }
                                    }
                                    else
                                    {
                                        DebugTrace.WriteLine($"Default key is null for '{sdkDirectoryKey}'");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    ///  Gather the list of SDKs installed on the machine from the registry.
    ///  Do not parallelize the getting of these entries, order is important, we want the first ones in to win.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void GatherSDKListFromRegistry(string registryRoot, Dictionary<TargetPlatformSDK, TargetPlatformSDK> platformMonikers)
    {
        // Setup some delegates because the methods we call use them during unit testing.
        GetRegistrySubKeyNames getSubkeyNames = new GetRegistrySubKeyNames(RegistryHelper.GetSubKeyNames);
        GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue = new GetRegistrySubKeyDefaultValue(RegistryHelper.GetDefaultValue);
        OpenBaseKey openBaseKey = new OpenBaseKey(RegistryHelper.OpenBaseKey);
        FileExists fileExists = new FileExists(File.Exists);

        bool is64bitOS = Environment.Is64BitOperatingSystem;

        // Under WOW64 the HKEY_CURRENT_USER\SOFTWARE key is shared. This means the values are the same in the 64 bit and 32 bit views. This means we only need to get one view of this key.
        GatherSDKsFromRegistryImpl(platformMonikers, registryRoot, RegistryView.Default, RegistryHive.CurrentUser, getSubkeyNames, getRegistrySubKeyDefaultValue, openBaseKey, fileExists);

        // Since SDKS can contain multiple architecture it makes sense to register both 32 bit and 64 bit in one location, but if for some reason that
        // is not possible then we need to look at both hives. Choosing the 32 bit one first because is where we expect to find them usually.
        if (is64bitOS)
        {
            GatherSDKsFromRegistryImpl(platformMonikers, registryRoot, RegistryView.Registry32, RegistryHive.LocalMachine, getSubkeyNames, getRegistrySubKeyDefaultValue, openBaseKey, fileExists);
            GatherSDKsFromRegistryImpl(platformMonikers, registryRoot, RegistryView.Registry64, RegistryHive.LocalMachine, getSubkeyNames, getRegistrySubKeyDefaultValue, openBaseKey, fileExists);
        }
        else
        {
            GatherSDKsFromRegistryImpl(platformMonikers, registryRoot, RegistryView.Default, RegistryHive.LocalMachine, getSubkeyNames, getRegistrySubKeyDefaultValue, openBaseKey, fileExists);
        }
    }

    /// <summary>
    /// Get the disk locations to search for sdks under. This can be overridden by an environment variable
    /// </summary>
    private static void GetDefaultSDKDiskRoots(List<string> diskRoots)
    {
        if (NativeMethodsShared.IsWindows)
        {
            // The order is important here because we want to look in the users location first before the non privileged location.

            // We need this so that a user can also have an sdk installed in a non privileged location
            string userLocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (userLocalAppData.Length > 0)
            {
                string localAppdataFolder = Path.Combine(userLocalAppData, "Microsoft SDKs");
                if (FileSystems.Default.DirectoryExists(localAppdataFolder))
                {
                    diskRoots.Add(localAppdataFolder);
                }
            }

            string defaultProgramFilesLocation = Path.Combine(
                FrameworkLocationHelper.programFiles32,
                "Microsoft SDKs");
            diskRoots.Add(defaultProgramFilesLocation);
        }
        else
        {
            diskRoots.Add(NativeMethodsShared.FrameworkBasePath);
        }
    }

    /// <summary>
    /// Extract the disk roots from the environment
    /// </summary>
    private static void ExtractSdkDiskRootsFromEnvironment(List<string> diskRoots, string directoryRoots)
    {
        if (diskRoots != null && !string.IsNullOrEmpty(directoryRoots))
        {
            string[] splitRoots = directoryRoots.Split(s_diskRootSplitChars, StringSplitOptions.RemoveEmptyEntries);
            DebugTrace.WriteLine($"DiskRoots from Registry '{string.Join(";", splitRoots)}'");
            diskRoots.AddRange(splitRoots);
        }

        if (diskRoots != null)
        {
            diskRoots.ForEach(x => x = x.Trim());
            diskRoots.RemoveAll(x => !FileUtilities.DirectoryExistsNoThrow(x));
        }
    }

    /// <summary>
    /// Get the disk roots to search for both platform and extension sdks in. The environment variable can
    /// override the defaults.
    /// </summary>
    /// <returns></returns>
    private static List<string> GetTargetPlatformMonikerDiskRoots(string[] diskRoots)
    {
        var sdkDiskRoots = new List<string>();
        string sdkDirectoryRootsFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY");
        ExtractSdkDiskRootsFromEnvironment(sdkDiskRoots, sdkDirectoryRootsFromEnvironment);
        if (sdkDiskRoots.Count == 0)
        {
            if (diskRoots?.Length > 0)
            {
                DebugTrace.WriteLine($"Passed in DiskRoots '{string.Join(";", diskRoots)}'");
                sdkDiskRoots.AddRange(diskRoots);
            }
            else
            {
                DebugTrace.WriteLine("Getting default disk roots");
                GetDefaultSDKDiskRoots(sdkDiskRoots);
            }
        }

        DebugTrace.WriteLine($"Diskroots being used '{string.Join(";", sdkDiskRoots)}'");
        return sdkDiskRoots;
    }

    /// <summary>
    /// Get the disk roots to search for multi platform extension sdks in. The environment variable can
    /// override the defaults.
    /// </summary>
    private static List<string> GetExtensionSdkDiskRoots(string[] diskRoots)
    {
        var sdkDiskRoots = new List<string>();
        string sdkDirectoryRootsFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDMULTIPLATFORMSDKREFERENCEDIRECTORY");
        ExtractSdkDiskRootsFromEnvironment(sdkDiskRoots, sdkDirectoryRootsFromEnvironment);
        if (sdkDiskRoots.Count == 0 && diskRoots?.Length > 0)
        {
            DebugTrace.WriteLine($"Passed in DiskRoots '{string.Join(";", diskRoots)}'", category: "GetMultiPlatformSdkDiskRoots");
            sdkDiskRoots.AddRange(diskRoots);
        }

        DebugTrace.WriteLine($"Diskroots being used '{string.Join(";", sdkDiskRoots)}'", category: "GetMultiPlatformSdkDiskRoots");
        return sdkDiskRoots;
    }

    /// <summary>
    /// Get the registry root to find sdks under. The registry can be disabled if we are in a checked in scenario
    /// </summary>
    /// <returns></returns>
    private static string GetTargetPlatformMonikerRegistryRoots(string registryRootLocation)
    {
        DebugTrace.WriteLine($"RegistryRoot passed in '{registryRootLocation ?? string.Empty}'");

        string disableRegistryForSDKLookup = Environment.GetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP");

        // If we are not disabling the registry for platform sdk lookups then lets look in the default location.
        string registryRoot = string.Empty;

        if (disableRegistryForSDKLookup == null)
        {
            if (!string.IsNullOrEmpty(registryRootLocation))
            {
                registryRoot = registryRootLocation;
            }
            else
            {
                registryRoot = @"SOFTWARE\MICROSOFT\Microsoft SDKs\";
            }

            DebugTrace.WriteLine($"RegistryRoot to be looked under '{registryRoot}'");
        }
        else
        {
            DebugTrace.WriteLine("MSBUILDDISABLEREGISTRYFORSDKLOOKUP is set registry sdk lookup is disabled");
        }

        return registryRoot;
    }

    /// <summary>
    /// Given a platform SDK object, populate its supported platforms.
    /// </summary>
    private static void GatherPlatformsForSdk(TargetPlatformSDK sdk)
    {
        Assumed.NotNullOrEmpty(sdk.Path, "SDK path must be set");

        try
        {
            string platformsRoot = Path.Combine(sdk.Path, platformsFolderName);
            DirectoryInfo platformsRootInfo = new DirectoryInfo(platformsRoot);

            if (platformsRootInfo.Exists)
            {
                DirectoryInfo[] platformIdentifiers = platformsRootInfo.GetDirectories();
                DebugTrace.WriteLine($"Found '{platformIdentifiers.Length}' platform identifier directories under '{platformsRoot}'");

                // Iterate through all identifiers
                foreach (DirectoryInfo platformIdentifier in platformIdentifiers)
                {
                    DirectoryInfo[] platformVersions = platformIdentifier.GetDirectories();
                    DebugTrace.WriteLine($"Found '{platformVersions.Length}' platform version directories under '{platformIdentifier.FullName}'");

                    // and all versions under each of those identifiers
                    foreach (DirectoryInfo platformVersion in platformVersions)
                    {
                        // If this version directory is not actually a proper version format, ignore it.
                        Version tempVersion;
                        if (Version.TryParse(platformVersion.Name, out tempVersion))
                        {
                            string sdkKey = TargetPlatformSDK.GetSdkKey(platformIdentifier.Name, platformVersion.Name);

                            // make sure we haven't already seen this one somehow
                            if (!sdk.Platforms.ContainsKey(sdkKey))
                            {
                                DebugTrace.WriteLine($"SDKKey '{sdkKey}' was not already found.");

                                string pathToPlatformManifest = Path.Combine(platformVersion.FullName, "Platform.xml");
                                if (FileUtilities.FileExistsNoThrow(pathToPlatformManifest))
                                {
                                    sdk.Platforms.Add(sdkKey, FileUtilities.EnsureTrailingSlash(platformVersion.FullName));
                                }
                                else
                                {
                                    DebugTrace.WriteLine($"No Platform.xml could be found at '{pathToPlatformManifest}'. Not adding this platform");
                                }
                            }
                            else
                            {
                                DebugTrace.WriteLine($"SDKKey '{sdkKey}' was already found, not adding platform under '{platformVersion.FullName}'");
                            }
                        }
                        else
                        {
                            DebugTrace.WriteLine($"Failed to parse platform version folder '{platformVersion.Name}' under '{platformVersion.FullName}'");
                        }
                    }
                }
            }
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            DebugTrace.WriteLine($"Encountered exception trying to gather platform-specific data: {e.Message}");
        }
    }
}
