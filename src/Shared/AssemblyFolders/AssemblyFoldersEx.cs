// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if FEATURE_WIN32_REGISTRY

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using ProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Implements the rules for finding component directories using the AssemblyFoldersEx scheme.
    ///
    /// This is the normal schema:
    ///
    ///  [HKLM | HKCU]\SOFTWARE\MICROSOFT\.NetFramework\ 
    ///    v1.0.3705 
    ///      AssemblyFoldersEx 
    ///          Infragistics.GridControl.1.0:  
    ///              @Default = c:\program files\infragistics\grid control\1.0\bin 
    ///              @Description = Infragistics Grid Control for .NET version 1.0 
    ///              9466 
    ///                  @Default = c:\program files\infragistics\grid control\1.0sp1\bin 
    ///                  @Description = SP1 for Infragistics Grid Control for .NET version 1.0 
    ///
    /// 
    /// The root registry path is the following:
    ///
    ///     [HKLM | HKCU]\{AssemblyFoldersBase}\{RuntimeVersion}\{AssemblyFoldersSuffix}
    ///
    /// Where:
    ///
    ///     {AssemblyFoldersBase} = Software\Microsoft\[.NetFramework | .NetCompactFramework]
    ///     {RuntimeVersion} = the runtime version property from the project file
    ///     {AssemblyFoldersSuffix} = [ PocketPC | SmartPhone | WindowsCE]\AssemblyFoldersEx
    ///
    /// </summary>
    internal class AssemblyFoldersEx : IEnumerable<AssemblyFoldersExInfo>
    {
        /// <summary>
        /// The list of directory names found from the registry.
        /// </summary>
        private List<AssemblyFoldersExInfo> _directoryNames = new List<AssemblyFoldersExInfo>();

        /// <summary>
        /// Set of unique paths to directories found from the registry
        /// </summary>
        private HashSet<string> _uniqueDirectoryPaths = new HashSet<string>();

        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="registryKeyRoot">Like Software\Microsoft\[.NetFramework | .NetCompactFramework]</param>
        /// <param name="targetRuntimeVersion">The runtime version property from the project file.</param>
        /// <param name="registryKeySuffix">Like [ PocketPC | SmartPhone | WindowsCE]\AssemblyFoldersEx</param>
        /// <param name="osVersion">Operating system version</param>
        /// <param name="platform">Current platform</param>
        /// <param name="getRegistrySubKeyNames">Used to find registry subkey names.</param>
        /// <param name="getRegistrySubKeyDefaultValue">Used to find registry key default values.</param>
        /// <param name="targetProcessorArchitecture">Architecture to seek.</param>
        /// <param name="openBaseKey">Key object to open.</param>
        internal AssemblyFoldersEx
        (
            string registryKeyRoot,
            string targetRuntimeVersion,
            string registryKeySuffix,
            string osVersion,
            string platform,
            GetRegistrySubKeyNames getRegistrySubKeyNames,
            GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue,
            ProcessorArchitecture targetProcessorArchitecture,
            OpenBaseKey openBaseKey
        )
        {
            // No extensions are supported, except on Windows
            if (!NativeMethodsShared.IsWindows)
            {
                return;
            }

            bool is64bitOS = EnvironmentUtilities.Is64BitOperatingSystem;
            bool targeting64bit = targetProcessorArchitecture == ProcessorArchitecture.Amd64 || targetProcessorArchitecture == ProcessorArchitecture.IA64;

            // The registry lookup should be as follows:
            /* 64 bit OS:
            *            Targeting 64 bit:
            *                First, look in 64 bit registry location
            *                Second, look in 32 bit registry location
            *            Targeting X86 or MSIL:
            *                First,  look in the 32 bit hive 
            *                Second, look in 64 bit hive
            *
            *  32 bit OS:           
            *        32 bit process:
            *            Targeting 64 bit, or X86, or MSIL:
            *                Look in the default registry which is the 32 bit hive
            */

            // Under WOW64 the HKEY_CURRENT_USER\SOFTWARE key is shared. This means the values are the same in the 64 bit and 32 bit views. This means we only need to get one view of this key.
            FindDirectories(RegistryView.Default, RegistryHive.CurrentUser, registryKeyRoot, targetRuntimeVersion, registryKeySuffix, osVersion, platform, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, openBaseKey);

            if (is64bitOS)
            {
                // Under WOW64 the HKEY_LOCAL_MACHINE\SOFTWARE key is redirected. This means the values can be different in the 64 bit and 32 bit views. This means we only need to get look at both keys.

                if (targeting64bit)
                {
                    FindDirectories(RegistryView.Registry64, RegistryHive.LocalMachine, registryKeyRoot, targetRuntimeVersion, registryKeySuffix, osVersion, platform, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, openBaseKey);
                    FindDirectories(RegistryView.Registry32, RegistryHive.LocalMachine, registryKeyRoot, targetRuntimeVersion, registryKeySuffix, osVersion, platform, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, openBaseKey);
                }
                else
                {
                    FindDirectories(RegistryView.Registry32, RegistryHive.LocalMachine, registryKeyRoot, targetRuntimeVersion, registryKeySuffix, osVersion, platform, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, openBaseKey);
                    FindDirectories(RegistryView.Registry64, RegistryHive.LocalMachine, registryKeyRoot, targetRuntimeVersion, registryKeySuffix, osVersion, platform, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, openBaseKey);
                }
            }
            else
            {
                FindDirectories(RegistryView.Default, RegistryHive.LocalMachine, registryKeyRoot, targetRuntimeVersion, registryKeySuffix, osVersion, platform, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, openBaseKey);
            }
        }

        /// <summary>
        /// Finds directories for a specific registry key.
        /// </summary>
        /// <param name="view">The registry view to examine.</param>
        /// <param name="hive">The registry hive to examine.</param>
        /// <param name="registryKeyRoot">Like Software\Microsoft\[.NetFramework | .NetCompactFramework]</param>
        /// <param name="targetRuntimeVersion">The runtime version property from the project file.</param>
        /// <param name="registryKeySuffix">Like [ PocketPC | SmartPhone | WindowsCE]\AssemblyFoldersEx</param>
        /// <param name="osVersion">Operating system version</param>
        /// <param name="platform">Current platform</param>
        /// <param name="getRegistrySubKeyNames">Used to find registry subkey names.</param>
        /// <param name="getRegistrySubKeyDefaultValue">Used to find registry key default values.</param>
        /// <param name="openBaseKey">Key object to open.</param>
        private void FindDirectories
        (
            RegistryView view,
            RegistryHive hive,
            string registryKeyRoot,
            string targetRuntimeVersion,
            string registryKeySuffix,
            string osVersion,
            string platform,
            GetRegistrySubKeyNames getRegistrySubKeyNames,
            GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue,
            OpenBaseKey openBaseKey
        )
        {
            // Open the hive for a given view
            using (RegistryKey baseKey = openBaseKey(hive, view))
            {
                IEnumerable<string> versions = getRegistrySubKeyNames(baseKey, registryKeyRoot);

                // No versions found.
                if (versions == null)
                {
                    return;
                }

                List<ExtensionFoldersRegistryKey> versionStrings = GatherVersionStrings(targetRuntimeVersion, versions);

                // Loop the versions, looking for component keys.
                List<ExtensionFoldersRegistryKey> componentKeys = new List<ExtensionFoldersRegistryKey>();

                foreach (ExtensionFoldersRegistryKey versionString in versionStrings)
                {
                    // Make like SOFTWARE\MICROSOFT\.NetFramework\v2.0.x86chk\AssemblyFoldersEx
                    string fullVersionKey = registryKeyRoot + @"\" + versionString.RegistryKey + @"\" + registryKeySuffix;
                    IEnumerable<string> components = getRegistrySubKeyNames(baseKey, fullVersionKey);

                    if (components != null)
                    {
                        // Sort the components in reverse alphabetical order so values with higher alphabetical names are earlier in the array.
                        // This is to try and get newer versioned components based on the fact they should have higher versioned names.
                        List<string> sortedComponents = new List<string>();

                        foreach (string component in components)
                        {
                            sortedComponents.Add(component);
                        }

                        // The reason we sort here rather than on the component keys is that we do not want to sort using the FullVersionKey
                        // the versions have already been sorted (with things that look like raw drops being tacked onto the bottom of the list after sorting)
                        // By sorting the versions again we will get these raw drop numbers possibly being somewhere other than at the bottom and thereby cause the resolver
                        // to find the assembly in the wrong location.
                        sortedComponents.Sort(ReverseStringGenericComparer.Comparer);

                        foreach (string component in sortedComponents)
                        {
                            // ComponentKeys are like SOFTWARE\MICROSOFT\.NetFramework\v1.0.x86chk\AssemblyFoldersEx\Infragistics.GridControl.1.0
                            componentKeys.Add(new ExtensionFoldersRegistryKey(fullVersionKey + @"\" + component, versionString.TargetFrameworkVersion));
                        }
                    }
                }

                // Loop the component keys, looking for servicing keys.
                List<ExtensionFoldersRegistryKey> directoryKeys = new List<ExtensionFoldersRegistryKey>();

                foreach (ExtensionFoldersRegistryKey componentKey in componentKeys)
                {
                    IEnumerable<string> servicingKeys = getRegistrySubKeyNames(baseKey, componentKey.RegistryKey);

                    if (servicingKeys != null)
                    {
                        List<string> fullServicingKeys = new List<string>();

                        foreach (string servicingKey in servicingKeys)
                        {
                            // ServicingKeys are like SOFTWARE\MICROSOFT\.NetFramework\v1.0.3705\AssemblyFoldersEx\Infragistics.GridControl.1.0\9120
                            fullServicingKeys.Add(componentKey.RegistryKey + @"\" + servicingKey);
                        }

                        // Alphabetize to put them in version order.
                        fullServicingKeys.Sort(ReverseStringGenericComparer.Comparer);
                        foreach (string key in fullServicingKeys)
                        {
                            directoryKeys.Add(new ExtensionFoldersRegistryKey(key, componentKey.TargetFrameworkVersion));
                        }

                        directoryKeys.Add(componentKey);
                    }
                }

                // Now, we have a properly ordered collection of registry keys, each of which
                // should point to a default value with a file path. Get those files paths.
                foreach (ExtensionFoldersRegistryKey directoryKey in directoryKeys)
                {
                    if (!(String.IsNullOrEmpty(platform) && String.IsNullOrEmpty(osVersion)))
                    {
                        using (RegistryKey keyPlatform = baseKey.OpenSubKey(directoryKey.RegistryKey, false))
                        {
                            if (keyPlatform != null && keyPlatform.ValueCount > 0)
                            {
                                if (platform != null && platform.Length > 0)
                                {
                                    string platformValue = keyPlatform.GetValue("Platform", null) as string;

                                    if (!String.IsNullOrEmpty(platformValue) && !MatchingPlatformExists(platform, platformValue))
                                    {
                                        continue;
                                    }
                                }

                                if (osVersion != null && osVersion.Length > 0)
                                {
                                    Version ver = VersionUtilities.ConvertToVersion(osVersion);

                                    if (!IsVersionInsideRange(ver, keyPlatform))
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                    }

                    string directoryName = getRegistrySubKeyDefaultValue(baseKey, directoryKey.RegistryKey);

                    if (null != directoryName)
                    {
                        _uniqueDirectoryPaths.Add(directoryName);
                        _directoryNames.Add(new AssemblyFoldersExInfo(hive, view, directoryKey.RegistryKey, directoryName, directoryKey.TargetFrameworkVersion));
                    }
                }
            }
        }

        private bool MatchingPlatformExists(string platform, string platformValue)
        {
            bool match = false;

            if (platformValue != null && platformValue.Length > 0)
            {
                string[] platforms = platformValue.Split(MSBuildConstants.SemicolonChar);
                foreach (string p in platforms)
                {
                    if (String.Compare(p, platform, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        match = true;
                        break;
                    }
                }
            }

            return match;
        }

        private bool IsVersionInsideRange(Version v, RegistryKey keyPlatform)
        {
            bool insideRange = true;

            if (v != null)
            {
                string minVersionAsString = keyPlatform.GetValue("MinOSVersion", null) as string;
                Version minVersion = minVersionAsString == null ? null : VersionUtilities.ConvertToVersion(minVersionAsString);
                if (minVersion != null && minVersion > v)
                {
                    // Filter keys with MinOSVersion > OSVersion
                    insideRange = false;
                }

                string maxVersionAsString = keyPlatform.GetValue("MaxOSVersion", null) as string;
                Version maxVersion = maxVersionAsString == null ? null : VersionUtilities.ConvertToVersion(maxVersionAsString);
                if (maxVersion != null && maxVersion < v)
                {
                    // Filter keys with MaxOSVersion < OSVersion
                    insideRange = false;
                }
            }

            return insideRange;
        }

        /// <summary>
        ///  The algorithm for gathering versions from the registry is as follows:
        ///     1) targetRuntimeVersion is the target framework version you are targeting
        ///     2) versions is a string list from reading the registry, this list is in what ever order the registry returns 
        ///        the keys to us in, this is usually alphabetical.
        ///     
        ///     We will go through each version string and do the following:
        ///         1) Check to see if the string is a version
        ///             If the string is not a version we will check to see if the string starts with the framework we are targeting,
        ///             if it does we will add it to a list which will be added at the end 
        ///             of the versions list, if not it gets ignored. We do this to stay compatible to what we have been doing since whidbey.
        ///             
        ///             If the string is a version
        ///                 We check to see if the version is a valid target framework version. Meaning.  It has a Maj.Minor version and may have 
        ///                 build, Build is less than or equal to 255 and there is no revision. The reason the build number needs to be less than 255 is because
        ///                 255 is the largest build number for a target framework version that visual studio 2010 supports. The build number is supposed to 
        ///                 represent a service pack on the 4.0 framework.
        ///                 
        ///                 If the string is a valid target framework version we check to see we already have a dictionary entry and if not we 
        ///                 add one. 
        ///                 If the string is not a valid target framework then we will ignore the part of the version which makes it invalid
        ///                 (either the build or the revision, or both) and see where that version would fit in the dictionary as a key and
        ///                 then put the original version string into the list for that entry.
        ///                 
        ///         Since the dictionary is sorted in reverse order to generate the list to return we do the following:
        ///         Go through the list of dictionary entries 
        ///             For each entry sort the list in reverse alphabetical order and add the entries in their internal list to the listToreturn.
        ///
        ///         This way we have a reverse sorted list of all of the version keys.
        /// </summary>
        internal static List<ExtensionFoldersRegistryKey> GatherVersionStrings(string targetRuntimeVersion, IEnumerable<string> versions)
        {
            List<string> additionalToleratedKeys = new List<string>();
            Version targetVersion = VersionUtilities.ConvertToVersion(targetRuntimeVersion);
            List<ExtensionFoldersRegistryKey> versionStrings = new List<ExtensionFoldersRegistryKey>();

            // This dictionary will contain a set of target framework versions and a list of strings read from the registry which are supposed to be treated like the 
            // target framework version stored as the key. 
            // For example:
            //  If the target framework version is 4.0  but the registry string is v4.0.2116 then we want to treat v4.0.2116 as if it was v4.0 during the sort, 
            // but when reading out of the registry
            //  we need to know the original value so we can open the correct key.
            //
            //  The reason there needs to be a list for each target framework version is that there could be multiple keys in the registry which should be treated 
            // like v4.0 for sorting.
            //  for example lets say we had the following entries in the registry:
            //       4.0.2116  and 4.0.2116.87  both of these are supposed to be treated like v4.0 because they are not valid target framework versions but 
            // are valid version numbers and should be searched when we are targeting 4.0.
            SortedDictionary<Version, List<string>> targetFrameworkVersionToRegistryVersions = new SortedDictionary<Version, List<string>>(ReverseVersionGenericComparer.Comparer);

            // Loop over versions from registry.
            foreach (string version in versions)
            {
                if ((version.Length > 0) && (String.Compare(version.Substring(0, 1), "v", StringComparison.OrdinalIgnoreCase) == 0))
                {
                    Version candidateVersion = VersionUtilities.ConvertToVersion(version);

                    if (candidateVersion == null)
                    {
                        // If it wasn't a true version number, we may still want to tolerate it because raw drops have
                        // the form 'v2.0.x86chk'
                        if (String.Compare(version, 0, targetRuntimeVersion, 0, targetRuntimeVersion.Length, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            additionalToleratedKeys.Add(version);
                        }
                    }
                    else
                    {
                        // To be added to our dictionary our candidate version from the registry must be a valid target framework version which is less than or equal 
                        // to the target version. Therefore if the candidate version is not a valid target framework version we will pretend it is and sort it in its correct form.

                        Version replacementVersion = null;
                        if (candidateVersion.Build > 255)
                        {
                            // Pretend the candidate version is really Maj.Minor ignore the build and revision
                            replacementVersion = new Version(candidateVersion.Major, candidateVersion.Minor);
                        }
                        else if (candidateVersion.Revision != -1)
                        {
                            // Pretend the version is Maj.Minor.Build ignore the revision
                            replacementVersion = new Version(candidateVersion.Major, candidateVersion.Minor, candidateVersion.Build);
                        }
                        else
                        {
                            // Was not replaced just use as is since it is a good version
                            replacementVersion = candidateVersion;
                        }

                        // If the target version is null then we need to do a partial version match 
                        bool addToListDueToPartialNameMatch = false;
                        if (targetVersion == null)
                        {
                            if (String.Compare(version, 0, targetRuntimeVersion, 0, targetRuntimeVersion.Length, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                addToListDueToPartialNameMatch = true;
                            }
                        }

                        // If we have a target framework version as a version object is the version we are going to add to our dictionary in the correct range.
                        bool replacementVersionWithinRange = (targetVersion != null && targetVersion >= replacementVersion);

                        // Add the version to our dictionary if we are within the correct range or we had no target framework version but partially matched on the version string.
                        if (replacementVersion != null && (replacementVersionWithinRange || addToListDueToPartialNameMatch))
                        {
                            AddCandidateVersion(targetFrameworkVersionToRegistryVersions, version, replacementVersion);
                        }
                    }
                }
            }

            // Go through the target framework versions in reverse version order
            foreach (KeyValuePair<Version, List<string>> entry in targetFrameworkVersionToRegistryVersions)
            {
                List<string> frameworkList = entry.Value;

                // Sort the list in reverse alphabetical order since these are the version strings from the registry
                frameworkList.Sort(ReverseStringGenericComparer.Comparer);

                foreach (string s in frameworkList)
                {
                    // The string in this case already contains the v
                    versionStrings.Add(new ExtensionFoldersRegistryKey(s, entry.Key));
                }
            }

            // The additional tolerated keys are added onto the end of the versions list in what ever order they came from the 
            // registry in.
            foreach (string key in additionalToleratedKeys)
            {
                versionStrings.Add(new ExtensionFoldersRegistryKey(key, targetVersion ?? new Version(0, 0)));
            }

            return versionStrings;
        }

        /// <summary>
        /// Given a candidate version we need to add it to the dictionary of targetFrameworkToRegistry versions. This involves determining if we need to add it to
        /// an existing entry or create a new one.
        /// </summary>
        private static void AddCandidateVersion(SortedDictionary<Version, List<string>> targetFrameworkVersionToRegistryVersions, string version, Version candidateVersion)
        {
            List<string> listOfFrameworks = null;
            if (targetFrameworkVersionToRegistryVersions.TryGetValue(candidateVersion, out listOfFrameworks))
            {
                listOfFrameworks.Add(version);
            }
            else
            {
                // The version is not in our dictionary yet, lets add it
                // We need a new list since one has not been added yet
                listOfFrameworks = new List<string>();
                // Make sure we add ourselves to the list
                listOfFrameworks.Add(version);

                targetFrameworkVersionToRegistryVersions.Add(candidateVersion, listOfFrameworks);
            }
        }

        /// <summary>
        /// Get Enumerator
        /// </summary>
        IEnumerator<AssemblyFoldersExInfo> IEnumerable<AssemblyFoldersExInfo>.GetEnumerator()
        {
            return _directoryNames.GetEnumerator();
        }

        /// <summary>
        /// Get enumerator
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<AssemblyFoldersExInfo>)this).GetEnumerator();
        }

        internal IEnumerable<string> UniqueDirectoryPaths
        {
            get => _uniqueDirectoryPaths;
        }
    }
}
#endif
