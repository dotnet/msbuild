// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;

#nullable disable

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
    /// <summary>
    /// Cache the frameworkName of the highest version of a framework given its root path and identifier.
    /// This is to optimize calls to GetHighestVersionOfTargetFramework
    /// </summary>
    private static Dictionary<string, FrameworkNameVersioning> s_cachedHighestFrameworkNameForTargetFrameworkIdentifier;

    /// <summary>
    /// Cache the list of supported frameworks
    /// </summary>
    private static List<string> s_targetFrameworkMonikers = null;

    /// <summary>
    /// Gets a IList of supported target framework monikers.
    /// </summary>
    /// <returns>list of supported target framework monikers</returns>
    public static IList<string> GetSupportedTargetFrameworks()
    {
        lock (s_locker)
        {
            if (s_targetFrameworkMonikers == null)
            {
                s_targetFrameworkMonikers = new List<string>();
                IList<string> frameworkIdentifiers = GetFrameworkIdentifiers(FrameworkLocationHelper.programFilesReferenceAssemblyLocation);
                foreach (string frameworkIdentifier in frameworkIdentifiers)
                {
                    IList<string> frameworkVersions = GetFrameworkVersions(FrameworkLocationHelper.programFilesReferenceAssemblyLocation, frameworkIdentifier);
                    foreach (string frameworkVersion in frameworkVersions)
                    {
                        Version version = VersionUtilities.ConvertToVersion(frameworkVersion);
                        s_targetFrameworkMonikers.Add(new FrameworkNameVersioning(frameworkIdentifier, version, null).FullName);

                        IList<string> frameworkProfile = GetFrameworkProfiles(FrameworkLocationHelper.programFilesReferenceAssemblyLocation, frameworkIdentifier, frameworkVersion);
                        foreach (string profile in frameworkProfile)
                        {
                            s_targetFrameworkMonikers.Add(new FrameworkNameVersioning(frameworkIdentifier, version, profile).FullName);
                        }
                    }
                }
            }
        }

        return s_targetFrameworkMonikers;
    }

    /// <summary>
    /// This method will return the highest version of a target framework moniker based on the identifier. This method will only
    /// find full frameworks, this means no profiles will be returned.
    /// </summary>
    public static FrameworkNameVersioning HighestVersionOfTargetFrameworkIdentifier(string targetFrameworkRootDirectory, string frameworkIdentifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetFrameworkRootDirectory);
        ArgumentException.ThrowIfNullOrEmpty(frameworkIdentifier);

        string key = targetFrameworkRootDirectory + ";" + frameworkIdentifier;
        FrameworkNameVersioning highestFrameworkName = null;
        bool foundInCache = false;

        lock (s_locker)
        {
            if (s_cachedHighestFrameworkNameForTargetFrameworkIdentifier == null)
            {
                s_cachedHighestFrameworkNameForTargetFrameworkIdentifier = new Dictionary<string, FrameworkNameVersioning>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                foundInCache = s_cachedHighestFrameworkNameForTargetFrameworkIdentifier.TryGetValue(key, out highestFrameworkName);
            }

            if (!foundInCache)
            {
                IList<string> frameworkVersions = GetFrameworkVersions(targetFrameworkRootDirectory, frameworkIdentifier);
                if (frameworkVersions.Count > 0)
                {
                    Version targetFrameworkVersion = ConvertTargetFrameworkVersionToVersion(frameworkVersions[frameworkVersions.Count - 1]);
                    highestFrameworkName = new FrameworkNameVersioning(frameworkIdentifier, targetFrameworkVersion);
                }

                s_cachedHighestFrameworkNameForTargetFrameworkIdentifier.Add(key, highestFrameworkName);
            }
        }

        return highestFrameworkName;
    }

    /// <summary>
    /// Given a string which may start with a "v" convert the string to a version object.
    /// </summary>
    private static Version ConvertTargetFrameworkVersionToVersion(string targetFrameworkVersion)
    {
        // Trim off the v if is is there.
        if (!string.IsNullOrEmpty(targetFrameworkVersion) && targetFrameworkVersion[0] is 'v' or 'V')
        {
#if NET
            return Version.Parse(targetFrameworkVersion.AsSpan(1));
#else
            return new Version(targetFrameworkVersion.Substring(1));
#endif
        }

        return new Version(targetFrameworkVersion);
    }

    /// <summary>
    /// Gets the installed framework identifiers
    /// </summary>
    /// <param name="frameworkReferenceRoot"></param>
    /// <returns></returns>
    internal static IList<string> GetFrameworkIdentifiers(string frameworkReferenceRoot)
    {
        if (string.IsNullOrEmpty(frameworkReferenceRoot))
        {
            throw new ArgumentException("Invalid frameworkReferenceRoot", nameof(frameworkReferenceRoot));
        }

        var frameworkIdentifiers = new List<string>();

        bool bAddDotNetFrameworkIdentifier = false;
        bool bFoundDotNetFrameworkIdentifier = false;
        bool programFilesReferenceAssemblyLocationFound = false;

        DirectoryInfo di = new DirectoryInfo(frameworkReferenceRoot);
        if (di.Exists)
        {
            if (frameworkReferenceRoot.Equals(FrameworkLocationHelper.programFilesReferenceAssemblyLocation, StringComparison.OrdinalIgnoreCase))
            {
                programFilesReferenceAssemblyLocationFound = true;
            }

            foreach (DirectoryInfo folder in di.GetDirectories())
            {
                if (programFilesReferenceAssemblyLocationFound &&
                    (
                        string.Equals(folder.Name, FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV30, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(folder.Name, FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV35, StringComparison.OrdinalIgnoreCase)))
                {
                    bAddDotNetFrameworkIdentifier = true;
                    continue;
                }

                if (string.Equals(folder.Name, FrameworkLocationHelper.dotNetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    bFoundDotNetFrameworkIdentifier = true;
                }

                frameworkIdentifiers.Add(folder.Name);
            }
        }

        if (programFilesReferenceAssemblyLocationFound && !bFoundDotNetFrameworkIdentifier)
        {
            if (!bAddDotNetFrameworkIdentifier)
            {
                // special case for .NETFramework v2.0 - check also in the framework path because v20 does not have reference
                // assembly folders
                string dotNetFx20Path = GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20);
                if (dotNetFx20Path != null)
                {
                    if (FileSystems.Default.DirectoryExists(dotNetFx20Path))
                    {
                        frameworkIdentifiers.Add(FrameworkLocationHelper.dotNetFrameworkIdentifier);
                    }
                }
            }
            else
            {
                frameworkIdentifiers.Add(FrameworkLocationHelper.dotNetFrameworkIdentifier);
            }
        }

        return frameworkIdentifiers;
    }

    /// <summary>
    /// Gets the installed versions for a given framework
    /// </summary>
    private static IList<string> GetFrameworkVersions(string frameworkReferenceRoot, string frameworkIdentifier)
    {
        if (string.IsNullOrEmpty(frameworkReferenceRoot))
        {
            throw new ArgumentException("Invalid frameworkReferenceRoot", nameof(frameworkReferenceRoot));
        }

        if (string.IsNullOrEmpty(frameworkIdentifier))
        {
            throw new ArgumentException("Invalid frameworkIdentifier", nameof(frameworkIdentifier));
        }

        var frameworkVersions = new List<string>();

        // backward compatibility with orcas
        // In case of orcas .NETFramework v3.0, v3.5 - the version folders are directly under the frameworkReferenceRoot
        // first check here
        if (string.Equals(frameworkIdentifier, FrameworkLocationHelper.dotNetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            IList<string> versions = GetFx35AndEarlierVersions(frameworkReferenceRoot);
            if (versions.Count > 0)
            {
                frameworkVersions.AddRange(versions);
            }
        }

        // then look under the extensible multi-targeting layout - even for .NETFramework because future .NETFramework
        // versions would be at the right place
        string frameworkIdentifierPath = Path.Combine(frameworkReferenceRoot, frameworkIdentifier);

        DirectoryInfo dirInfoFxIdentifierPath = new DirectoryInfo(frameworkIdentifierPath);
        if (dirInfoFxIdentifierPath.Exists)
        {
            foreach (DirectoryInfo folder in dirInfoFxIdentifierPath.GetDirectories())
            {
                // the expected version folder name is of the format v<MajorVersion>.<MinorVersion> e.g. v3.5
                // only add if the version folder name is of the right format
                if (folder.Name.Length >= 4 && folder.Name.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                {
                    if (Version.TryParse(
#if NET
                        folder.Name.AsSpan(1),
#else
                        folder.Name.Substring(1),
#endif
                        out _))
                    {
                        frameworkVersions.Add(folder.Name);
                    }
                }
            }
        }

        // sort in ascending order of the version numbers, this is important as later when we search for assemblies in other methods
        // we should be looking in ascending order of the framework version folders on disk
        frameworkVersions.Sort(VersionComparer.Instance);

        return frameworkVersions;
    }

    /// <summary>
    /// Get installed framework profiles
    /// </summary>
    /// <param name="frameworkReferenceRoot"></param>
    /// <param name="frameworkIdentifier"></param>
    /// <param name="frameworkVersion"></param>
    /// <returns></returns>
    private static IList<string> GetFrameworkProfiles(string frameworkReferenceRoot, string frameworkIdentifier, string frameworkVersion)
    {
        if (string.IsNullOrEmpty(frameworkReferenceRoot))
        {
            throw new ArgumentException("Invalid frameworkReferenceRoot", nameof(frameworkReferenceRoot));
        }

        if (string.IsNullOrEmpty(frameworkIdentifier))
        {
            throw new ArgumentException("Invalid frameworkIdentifier", nameof(frameworkIdentifier));
        }

        if (string.IsNullOrEmpty(frameworkVersion))
        {
            throw new ArgumentException("Invalid frameworkVersion", nameof(frameworkVersion));
        }

        var frameworkProfiles = new List<string>();

        string frameworkProfilePath = Path.Combine(frameworkReferenceRoot, frameworkIdentifier);
        frameworkProfilePath = Path.Combine(frameworkProfilePath, frameworkVersion);
        frameworkProfilePath = Path.Combine(frameworkProfilePath, "Profiles");

        DirectoryInfo dirInfoFxProfilePath = new DirectoryInfo(frameworkProfilePath);
        if (dirInfoFxProfilePath.Exists)
        {
            foreach (DirectoryInfo subType in dirInfoFxProfilePath.GetDirectories())
            {
                Version ver = VersionUtilities.ConvertToVersion(frameworkVersion);
                // check if profile is installed correctly
                IList<string> refAssemblyPaths = GetPathToReferenceAssemblies(new FrameworkNameVersioning(frameworkIdentifier, ver, subType.Name));
                if (refAssemblyPaths?.Count > 0)
                {
                    frameworkProfiles.Add(subType.Name);
                }
            }
        }

        return frameworkProfiles;
    }

    /// <summary>
    /// returns the .NETFramework versions lessthanOrEqualTo 3.5 installed in the machine
    /// Only returns Fx versions lessthanOrEqualTo 3.5 if DNFx3.5 is installed
    /// </summary>
    /// <param name="frameworkReferenceRoot"></param>
    /// <returns></returns>
    private static IList<string> GetFx35AndEarlierVersions(string frameworkReferenceRoot)
    {
        IList<string> versions = new List<string>();

        // only return v35 and earlier versions if .NetFx35 is installed
        string dotNetFx35Path = GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35);

        if (dotNetFx35Path != null)
        {
            // .NetFx35 is installed

            // check v20
            string dotNetFx20Path = GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20);
            if (dotNetFx20Path != null)
            {
                versions.Add("v2.0");
            }

            // check v30
            string dotNextFx30RefPath = Path.Combine(frameworkReferenceRoot, FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV30);
            if (FileSystems.Default.DirectoryExists(dotNextFx30RefPath))
            {
                versions.Add(FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV30);
            }

            // check v35
            string dotNextFx35RefPath = Path.Combine(frameworkReferenceRoot, FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV35);
            if (FileSystems.Default.DirectoryExists(dotNextFx35RefPath))
            {
                versions.Add(FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV35);
            }
        }

        return versions;
    }
}
