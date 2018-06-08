// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Defines list of redistributable assemblies for use in dependency analysis.
    /// The input is a set of XML files in a well known format consisting of
    /// File elements. Each File element defines the assembly name of an assembly
    /// that is part of a redistributable unit, such as the .NET Framework
    /// (i.e. dotnetfx.exe) or the J# Framework. For the .NET Framework, these
    /// data files are specified in a sub-folder of the .NET Framework named
    /// "RedistList". This list is used by the build system to unify previous
    /// Framework version dependencies to the current Framework version.
    /// This list is also used by the deployment system to exclude Framework
    /// dependencies from customer deployment packages.
    /// </summary>    
    internal sealed class RedistList
    {
        // List of cached RedistList objects, the key is a semi-colon delimited list of data file paths
        private static readonly Dictionary<string, RedistList> s_cachedRedistList = new Dictionary<string, RedistList>(StringComparer.OrdinalIgnoreCase);

        // Process wide cache of redist lists found on disk under fx directories.
        // K: target framework directory, V: redist lists found on disk underneath K
        private static Dictionary<string, string[]> s_redistListPathCache;

        // Lock object
        private static readonly Object s_locker = new Object();

        /// <summary>
        /// When we check to see if an assembly is in this redist list we want to cache it so that if we ask again we do not
        /// have to re-scan bits of the redist list and do the assemblynameExtension comparisons.
        /// </summary>
        private readonly ConcurrentDictionary<AssemblyNameExtension, NGen<bool>> _assemblyNameInRedist = new ConcurrentDictionary<AssemblyNameExtension, NGen<bool>>(AssemblyNameComparer.GenericComparer);

        /// <summary>
        /// AssemblyName to unified assemblyName. We make this kind of call a lot and also will ask for the same name multiple times.
        /// </summary>
        private readonly ConcurrentDictionary<string, AssemblyEntry> _assemblyNameToUnifiedAssemblyName = new ConcurrentDictionary<string, AssemblyEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// AssemblyName to AssemblyNameExtension object. We make this kind of call a lot and also will ask for the same name multiple times.
        /// </summary>
        private readonly ConcurrentDictionary<string, AssemblyNameExtension> _assemblyNameToAssemblyNameExtension = new ConcurrentDictionary<string, AssemblyNameExtension>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// When we check to see if an assembly is remapped we should cache the result because we may get asked the same question a number of times.
        /// Since the remapping list does not change between builds neither would the results of the remapping for a given fusion name.
        /// </summary>
        private readonly ConcurrentDictionary<AssemblyNameExtension, AssemblyNameExtension> _remappingCache = new ConcurrentDictionary<AssemblyNameExtension, AssemblyNameExtension>(AssemblyNameComparer.GenericComparerConsiderRetargetable);

        // List of cached BlackList RedistList objects, the key is a semi-colon delimited list of data file paths
        private readonly ConcurrentDictionary<string, Hashtable> _cachedBlackList = new ConcurrentDictionary<string, Hashtable>(StringComparer.OrdinalIgnoreCase);
        
        /***************Fields which are only set in the constructor and should not be modified by the class. **********************/
        // Array of errors encountered while reading files.
        private readonly ReadOnlyCollection<Exception> _errors;
        // Array of files corresponding to the errors above.
        private readonly ReadOnlyCollection<String> _errorFilenames;

        // List of assembly entries loaded from the XML data files, one entry for each valid File element
        private readonly ReadOnlyCollection<AssemblyEntry> _assemblyList;

        // Maps simple names to assembly entries, the key is a simple name and the value is an index into assemblyList
        private readonly ReadOnlyDictionary<string, int> _simpleNameMap;

        // Remapping entries read from xml files in the RedistList directory.
        private readonly ReadOnlyCollection<AssemblyRemapping> _remapEntries;

        // Constants for locating redist lists under an fx directory.
        private const string MatchPattern = "*.xml";
        internal const string RedistListFolder = "RedistList";

        private RedistList(AssemblyTableInfo[] assemblyTableInfos)
        {
            var errors = new List<Exception>();
            var errorFilenames = new List<string>();
            var assemblyList = new List<AssemblyEntry>();
            var remappingEntries = new List<AssemblyRemapping>();

            if (assemblyTableInfos == null) throw new ArgumentNullException(nameof(assemblyTableInfos));
            foreach (AssemblyTableInfo assemblyTableInfo in assemblyTableInfos)
            {
                ReadFile(assemblyTableInfo, assemblyList, errors, errorFilenames, remappingEntries);
            }

            _errors = new ReadOnlyCollection<Exception>(errors);
            _errorFilenames = new ReadOnlyCollection<string>(errorFilenames);
            _remapEntries = new ReadOnlyCollection<AssemblyRemapping>(remappingEntries);

            // With the same simple name and then the version so that for each simple name we want the assemblies to also be sorted by version.
            assemblyList.Sort(s_sortByVersionDescending);
            _assemblyList = new ReadOnlyCollection<AssemblyEntry>(assemblyList);

            var simpleNameMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < assemblyList.Count; ++i)
            {
                AssemblyEntry entry = assemblyList[i];
                if (!simpleNameMap.ContainsKey(entry.SimpleName))
                {
                    simpleNameMap.Add(entry.SimpleName, i);
                }
            }

            _simpleNameMap = new ReadOnlyDictionary<string, int>(simpleNameMap);
        }

        /// <summary>
        /// Returns any exceptions encountered while reading\parsing the XML.
        /// </summary>
        internal Exception[] Errors => _errors.ToArray();

        /// <summary>
        /// Returns any exceptions encountered while reading\parsing the XML.
        /// </summary>
        internal string[] ErrorFileNames => _errorFilenames.ToArray();

        /// <summary>
        /// Returns the number of entries in the redist list
        /// </summary>
        internal int Count => _assemblyList.Count;

        /// <summary>
        /// Determines whether or not the specified assembly is part of the Framework.
        /// Assemblies from a previous version of the Framework will be
        /// correctly identified.
        /// </summary>
        public bool IsFrameworkAssembly(string assemblyName)
        {
            AssemblyEntry entry = GetUnifiedAssemblyEntry(assemblyName);
            if (!String.IsNullOrEmpty(entry?.RedistName))
            {
                AssemblyNameExtension assembly = GetAssemblyNameExtension(assemblyName);

                // The version of the checking assembly should be lower than the one of the unified assembly
                if (assembly.Version <= entry.AssemblyNameExtension.Version)
                {
                    return entry.RedistName.StartsWith("Microsoft-Windows-CLRCoreComp", StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// Determines whether or not the specified assembly is a Prerequisite assembly.
        /// A prerequisite assembly is any assembly that is flagged as being installed in the GAC
        /// in a redist data file.
        /// </summary>
        public bool IsPrerequisiteAssembly(string assemblyName)
        {
            AssemblyEntry entry = GetUnifiedAssemblyEntry(assemblyName);
            return entry != null && entry.InGAC;
        }

        /// <summary>
        /// If there was a remapping entry in the redist list list then remap the passed in assemblynameextension 
        /// if not just return the original one. 
        /// </summary>
        public AssemblyNameExtension RemapAssembly(AssemblyNameExtension extensionToRemap)
        {
            if (!_remappingCache.TryGetValue(extensionToRemap, out AssemblyNameExtension remappedExtension))
            {
                // We do not expect there to be more than a handfull of entries
                foreach (AssemblyRemapping remapEntry in _remapEntries)
                {
                    if (remapEntry.From.PartialNameCompare(extensionToRemap, true/* consider retargetable flag*/))
                    {
                        remappedExtension = remapEntry.To;
                        break;
                    }
                }
                _remappingCache.TryAdd(extensionToRemap, remappedExtension);
            }

            // Important to clone since we tend to mutate assemblyNameExtensions in RAR
            return remappedExtension?.Clone();
        }

        /// <summary>
        /// Determines whether or not the specified assembly is a redist root.
        /// </summary>
        internal bool? IsRedistRoot(string assemblyName)
        {
            AssemblyEntry entry = GetUnifiedAssemblyEntry(assemblyName);
            return entry?.IsRedistRoot;
        }

        /// <summary>
        /// Returns an instance of RedistList initialized from the framework folder for v2.0
        /// This function returns a statically cached object, so all calls will return the
        /// same instance.
        /// </summary>
        public static RedistList GetFrameworkList20()
        {
            string frameworkVersion20Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20);
            string[] redistListPaths = Array.Empty<string>();
            if (frameworkVersion20Path != null)
            {
                redistListPaths = RedistList.GetRedistListPathsFromDisk(frameworkVersion20Path);
            }

            var assemblyTableInfos = new AssemblyTableInfo[redistListPaths.Length];
            for (int i = 0; i < redistListPaths.Length; ++i)
            {
                assemblyTableInfos[i] = new AssemblyTableInfo(redistListPaths[i], frameworkVersion20Path);
            }

            return GetRedistList(assemblyTableInfos);
        }

        /// <summary>
        /// Returns an instance of RedistList initialized from the framework folder for v3.0
        /// This function returns a statically cached object, so all calls will return the
        /// same instance.
        /// </summary>
        public static RedistList GetFrameworkList30()
        {
            return GetFrameworkListFromReferenceAssembliesPath(TargetDotNetFrameworkVersion.Version30);
        }

        /// <summary>
        /// Returns an instance of RedistList initialized from the framework folder for v3.5
        /// This function returns a statically cached object, so all calls will return the
        /// same instance.
        /// </summary>
        public static RedistList GetFrameworkList35()
        {
            return GetFrameworkListFromReferenceAssembliesPath(TargetDotNetFrameworkVersion.Version35);
        }

        /// <summary>
        /// This is owned by chris mann
        /// </summary>
        public static RedistList GetRedistListFromPath(string path)
        {
            string[] redistListPaths = (path == null) ? Array.Empty<string>(): GetRedistListPathsFromDisk(path);

            var assemblyTableInfos = new AssemblyTableInfo[redistListPaths.Length];
            for (int i = 0; i < redistListPaths.Length; ++i)
            {
                assemblyTableInfos[i] = new AssemblyTableInfo(redistListPaths[i], path);
            }

            return GetRedistList(assemblyTableInfos);
        }

        private static RedistList GetFrameworkListFromReferenceAssembliesPath(TargetDotNetFrameworkVersion version)
        {
            string referenceAssembliesPath = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(version);

            // On dogfood build machines, v3.5 is not formally installed, so this returns null.
            // We don't use redist lists in this case.            
            string[] redistListPaths = (referenceAssembliesPath == null) ? Array.Empty<string>() : GetRedistListPathsFromDisk(referenceAssembliesPath);

            var assemblyTableInfos = new AssemblyTableInfo[redistListPaths.Length];
            for (int i = 0; i < redistListPaths.Length; ++i)
            {
                assemblyTableInfos[i] = new AssemblyTableInfo(redistListPaths[i], referenceAssembliesPath);
            }

            return GetRedistList(assemblyTableInfos);
        }

        /// <summary>
        /// Given a framework directory path, this static method will find matching
        /// redist list files underneath that path.  A process-wide cache is used to
        /// avoid hitting the disk multiple times for the same framework directory.
        /// </summary>
        /// <returns>Array of paths to redist lists under given framework directory.</returns>
        public static string[] GetRedistListPathsFromDisk(string frameworkDirectory)
        {
            ErrorUtilities.VerifyThrowArgumentNull(frameworkDirectory, nameof(frameworkDirectory));

            lock (s_locker)
            {
                if (s_redistListPathCache == null)
                {
                    s_redistListPathCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                }

                if (!s_redistListPathCache.TryGetValue(frameworkDirectory, out string[] results))
                {
                    string redistDirectory = Path.Combine(frameworkDirectory, RedistListFolder);

                    if (Directory.Exists(redistDirectory))
                    {
                        results = Directory.GetFiles(redistDirectory, MatchPattern);
                        s_redistListPathCache.Add(frameworkDirectory, results);
                        return results;
                    }
                }
                else
                {
                    return results;
                }
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// The name of this redist.
        /// </summary>
        internal string RedistName(string assemblyName)
        {
            AssemblyEntry entry = GetUnifiedAssemblyEntry(assemblyName);
            return entry?.RedistName;
        }

        /// <summary>
        /// Returns an instance of RedistList initialized from the specified set of files.
        /// This function returns a statically cached object, so subsequent calls with the same set
        /// of files will return the same instance.
        /// </summary>
        public static RedistList GetRedistList(AssemblyTableInfo[] assemblyTables)
        {
            if (assemblyTables == null) throw new ArgumentNullException(nameof(assemblyTables));
            Array.Sort(assemblyTables);

            var keyBuilder = assemblyTables.Length > 0 ? new StringBuilder(assemblyTables[0].Descriptor) : new StringBuilder();
            for (int i = 1; i < assemblyTables.Length; ++i)
            {
                keyBuilder.Append(';');
                keyBuilder.Append(assemblyTables[i].Descriptor);
            }

            string key = keyBuilder.ToString();
            lock (s_locker)
            {
                if (s_cachedRedistList.TryGetValue(key, out RedistList redistList))
                {
                    return redistList;
                }

                redistList = new RedistList(assemblyTables);
                s_cachedRedistList.Add(key, redistList);

                return redistList;
            }
        }

        private static string GetSimpleName(string assemblyName)
        {
            if (assemblyName == null) throw new ArgumentNullException(nameof(assemblyName));
            int i = assemblyName.IndexOf(",", StringComparison.Ordinal);
            return i > 0 ? assemblyName.Substring(0, i) : assemblyName;
        }

        private AssemblyEntry GetUnifiedAssemblyEntry(string assemblyName)
        {
            if (assemblyName == null) throw new ArgumentNullException(nameof(assemblyName));
            if (!_assemblyNameToUnifiedAssemblyName.TryGetValue(assemblyName, out AssemblyEntry unifiedEntry))
            {
                string simpleName = GetSimpleName(assemblyName);
                if (_simpleNameMap.TryGetValue(simpleName, out int index))
                {
                    // Provides the starting index into assemblyList of the simpleName
                    var highestVersionInRedist = new AssemblyNameExtension(_assemblyList[index].FullName);
                    for (int i = index; i < _assemblyList.Count; ++i)
                    {
                        AssemblyEntry entry = _assemblyList[i];
                        if (!string.Equals(simpleName, entry.SimpleName, StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        AssemblyNameExtension firstAssembly = GetAssemblyNameExtension(assemblyName);
                        AssemblyNameExtension secondAssembly = entry.AssemblyNameExtension;

                        bool matchNotConsideringVersion = firstAssembly.EqualsIgnoreVersion(secondAssembly);

                        // Do not want to downgrade a version which would be the case where two assemblies match even if one has a version greater than the highest in the redist list.
                        if (matchNotConsideringVersion && highestVersionInRedist.Version <= secondAssembly.Version)
                        {
                            unifiedEntry = entry;
                            break;
                        }
                    }
                }

                // unified entry can be null but this is used to keep us from trying to generate the unified name when one does not exist in the redist list.
                _assemblyNameToUnifiedAssemblyName.TryAdd(assemblyName, unifiedEntry);
            }

            return unifiedEntry;
        }

        private AssemblyNameExtension GetAssemblyNameExtension(string assemblyName)
        {
            return _assemblyNameToAssemblyNameExtension.GetOrAdd(assemblyName, key => new AssemblyNameExtension(key));
        }

        /// <summary>
        /// Given an assemblyNameExtension, is that assembly name in the redist list or not. This will use partial matching and match as much of the fusion name as exists in the assemblyName passed in.
        /// </summary>
        public bool FrameworkAssemblyEntryInRedist(AssemblyNameExtension assemblyName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(assemblyName, nameof(assemblyName));

            if (!_assemblyNameInRedist.TryGetValue(assemblyName, out NGen<bool> isAssemblyNameInRedist))
            {
                string simpleName = GetSimpleName(assemblyName.Name);
                if (_simpleNameMap.TryGetValue(simpleName, out int index))
                {
                    // Provides the starting index into assemblyList of the simpleName
                    for (int i = index; i < _assemblyList.Count; ++i)
                    {
                        AssemblyEntry entry = _assemblyList[i];
                        if (!string.Equals(simpleName, entry.SimpleName, StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        // Make sure the redist name starts with Microsoft-Windows-CLRCoreComp or else it could be a third party redist list.
                        if (!entry.RedistName.StartsWith("Microsoft-Windows-CLRCoreComp", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        AssemblyNameExtension firstAssembly = assemblyName;
                        AssemblyNameExtension secondAssembly = entry.AssemblyNameExtension;
                        if (firstAssembly.PartialNameCompare(secondAssembly, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken | PartialComparisonFlags.Culture))
                        {
                            isAssemblyNameInRedist = true;
                            break;
                        }
                    }
                }

                // We need to make the assemblyname immutable before we add it to the dictionary because the original object may be mutated afterward
                _assemblyNameInRedist.TryAdd(assemblyName.CloneImmutable(), isAssemblyNameInRedist);
            }

            return isAssemblyNameInRedist;
        }

        /// <summary>
        /// Returns the unified version of the specified assembly.
        /// Assemblies from a previous version of the Framework will be
        /// returned with the current runtime version.
        /// </summary>
        public string GetUnifiedAssemblyName(string assemblyName)
        {
            AssemblyEntry entry = GetUnifiedAssemblyEntry(assemblyName);
            return entry?.FullName ?? assemblyName;
        }

        /// <summary>
        /// Find every assembly full name that matches the given simple name.
        /// </summary>
        /// <param name="simpleName"></param>
        /// <returns>The array of assembly names.</returns>
        internal AssemblyEntry[] FindAssemblyNameFromSimpleName
        (
            string simpleName
        )
        {
            var candidateNames = new List<AssemblyEntry>();

            if (_simpleNameMap.TryGetValue(simpleName, out int index))
            {
                for (int i = index; i < _assemblyList.Count; ++i)
                {
                    AssemblyEntry entry = _assemblyList[i];
                    if (!String.Equals(simpleName, entry.SimpleName, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    candidateNames.Add(entry);
                }
            }

            return candidateNames.ToArray();
        }

        /// <summary>
        /// This method will take a list of AssemblyTableInfo and generate a black list by subtracting the 
        /// assemblies listed in the WhiteList from the RedistList. 
        /// 
        /// 1) If there are assemblies in the redist list and one or more client subset files are read in with matching names then
        ///    the subtraction will take place. If there were no matching redist lists read in the black list will be empty.
        ///    
        /// 2) If the subset has a matching name but there are no files inside of it then the black list will contain ALL files in the redist list.
        /// 
        /// 3) If the redist list assembly has a null or empty redist name or the subset list has a null or empty subset name they will not be used for black list generation.
        ///
        /// When generating the blacklist, we will first see if the black list is in the appdomain wide cache
        /// so that we do not regenerate one for multiple calls using the same whiteListAssemblyTableInfo.
        /// 
        /// </summary>
        /// <param name="whiteListAssemblyTableInfo">List of paths to white list xml files</param>
        /// <returns>A hashtable containing the full assembly names of black listed assemblies as the key, and null as the value. 
        ///          If there is no assemblies in the redist list null is returned.
        /// </returns> 
        internal Hashtable GenerateBlackList(AssemblyTableInfo[] whiteListAssemblyTableInfo, List<Exception> whiteListErrors, List<string> whiteListErrorFileNames)
        {
            // Return null if there are no assemblies in the redist list.
            if (_assemblyList.Count == 0)
            {
                return null;
            }

            // Sort so that the same set of whiteListAssemblyTableInfo will generate the same key for the cache
            Array.Sort(whiteListAssemblyTableInfo);

            var keyBuilder = whiteListAssemblyTableInfo.Length > 0 ? new StringBuilder(whiteListAssemblyTableInfo[0].Descriptor) : new StringBuilder();

            // Concatenate the paths to the whitelist xml files together to get the key into the blacklist cache.
            for (int i = 1; i < whiteListAssemblyTableInfo.Length; ++i)
            {
                keyBuilder.Append(';');
                keyBuilder.Append(whiteListAssemblyTableInfo[i].Descriptor);
            }

            string key = keyBuilder.ToString();

            if (!_cachedBlackList.TryGetValue(key, out Hashtable returnTable))
            {
                var whiteListAssemblies = new List<AssemblyEntry>();

                // Unique list of redist names in the subset files read in. We use this to make sure we are subtracting from the correct framework list.
                var uniqueClientListNames = new Hashtable(StringComparer.OrdinalIgnoreCase);

                // Get the assembly entries for the white list
                foreach (AssemblyTableInfo info in whiteListAssemblyTableInfo)
                {
                    var whiteListAssembliesReadIn = new List<AssemblyEntry>();

                    // Need to know how many errors are in the list before the read file call so that if the redist name is null due to an error
                    // we do not get a "redist name is null or empty" error when in actual fact it was a file not found error.
                    int errorsBeforeReadCall = whiteListErrors.Count;

                    // Read in the subset list file. 
                    string redistName = ReadFile(info, whiteListAssembliesReadIn, whiteListErrors, whiteListErrorFileNames, null);

                    // Get the client subset name which has been read in.
                    if (!String.IsNullOrEmpty(redistName))
                    {
                        // Populate the list of assemblies which are to be used as white list assemblies.
                        whiteListAssemblies.AddRange(whiteListAssembliesReadIn);

                        // We may have the same redist name for multiple files, we only want to get the set of unique names.
                        if (!uniqueClientListNames.ContainsKey(redistName))
                        {
                            uniqueClientListNames[redistName] = null;
                        }
                    }
                    else
                    {
                        // There are no extra errors reading in the subset list file which would have caused the redist list name to be null or empty.
                        // This means the redist name read in must be null or empty
                        if (whiteListErrors.Count == errorsBeforeReadCall)
                        {
                            // The whiteList errors passes back problems reading the redist file through the use of an array containing exceptions
                            whiteListErrors.Add(new Exception(ResourceUtilities.FormatResourceString("ResolveAssemblyReference.NoSubSetRedistListName", info.Path)));
                            whiteListErrorFileNames.Add(info.Path);
                        }
                    }
                }

                // Dont care about the case of the assembly name
                var blackList = new Hashtable(StringComparer.OrdinalIgnoreCase);

                // Do we have any subset names?
                bool uniqueClientNamesExist = uniqueClientListNames.Count > 0;

                // Fill the hashtable with the entries, if there are no white list assemblies the black list will contain all assemblies in the redist list
                foreach (AssemblyEntry entry in _assemblyList)
                {
                    string entryFullName = entry.FullName;
                    string redistName = entry.RedistName;
                    if (String.IsNullOrEmpty(redistName))
                    {
                        // Ignore null or empty redist entries as we cannot match these up with any client subset lists.
                        continue;
                    }

                    string hashKey = entryFullName + "," + redistName;

                    // If there were no subset list names read in we cannot generate a black list. (warnings will be logged as part of the reading of the subset list).
                    if (uniqueClientNamesExist)
                    {
                        if (!blackList.ContainsKey(hashKey) && uniqueClientListNames.ContainsKey(redistName))
                        {
                            blackList[hashKey] = entryFullName;
                        }
                    }
                }

                // Go through each of the white list assemblies and remove it from the black list. Do this based on the assembly name and the redist name
                foreach (AssemblyEntry whiteListEntry in whiteListAssemblies)
                {
                    blackList.Remove(whiteListEntry.FullName + "," + whiteListEntry.RedistName);
                }

                // The output hashtable needs to be just the full names and not the names + redist name
                var blackListOfAssemblyNames = new Hashtable(StringComparer.OrdinalIgnoreCase);
                foreach (string name in blackList.Values)
                {
                    blackListOfAssemblyNames[name] = null;
                }

                _cachedBlackList.TryAdd(key, blackListOfAssemblyNames);

                return blackListOfAssemblyNames;
            }

            return returnTable;
        }

        /// <summary>
        /// Read the redist list from disk.
        /// XML formatting issues will recorded in the 'errors' collection.
        /// </summary>
        /// <param name="assemblyTableInfo">Information about the redistlist file.</param>
        /// <returns>Redist name of the redist list just read in</returns>
        internal static string ReadFile(AssemblyTableInfo assemblyTableInfo, List<AssemblyEntry> assembliesList, List<Exception> errorsList, List<string> errorFilenamesList, List<AssemblyRemapping> remapEntries)
        {
            string path = assemblyTableInfo.Path;
            string redistName = null;
            XmlReader reader = null;

            // Keep track of what assembly entries we have read in from the redist list, we want to track this because we need to know if there are duplicate entries
            // if there are duplicate entries one with ingac = true and one with InGac=false we want to choose the one with ingac true.
            // The reason we want to take the ingac True over ingac false is that this indicates the assembly IS in the gac.
            var assemblyEntries = new Dictionary<string, AssemblyEntry>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                reader = XmlReader.Create(path, readerSettings);

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (string.Equals(reader.Name, "FileList", StringComparison.OrdinalIgnoreCase))
                        {
                            reader.MoveToFirstAttribute();
                            do
                            {
                                if (string.Equals(reader.Name, "Redist", StringComparison.OrdinalIgnoreCase))
                                {
                                    redistName = reader.Value;
                                    break;
                                }
                            }
                            while (reader.MoveToNextAttribute());
                            reader.MoveToElement();

                            ParseFileListSection(assemblyTableInfo, path, redistName, reader, assemblyEntries, remapEntries);
                        }

                        if (string.Equals(reader.Name, "Remap", StringComparison.OrdinalIgnoreCase))
                        {
                            if (remapEntries != null)
                            {
                                ParseRemapSection(assemblyTableInfo, path, redistName, reader, remapEntries);
                            }
                        }
                    }
                }
            }
            catch (XmlException ex)
            {
                // Log the error and continue on.
                errorsList.Add(ex);
                errorFilenamesList.Add(path);
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                // If there was a problem writing the file (like it's read-only or locked on disk, for
                // example), then eat the exception and log a warning.  Otherwise, rethrow.
                errorsList.Add(ex);
                errorFilenamesList.Add(path);
            }
            finally
            {
                reader?.Dispose();
            }

            foreach (AssemblyEntry entry in assemblyEntries.Values)
            {
                assembliesList.Add(entry);
            }

            return redistName;
        }

        /// <summary>
        /// Parse the remapping xml element in the redist list
        /// </summary>
        private static void ParseRemapSection(AssemblyTableInfo assemblyTableInfo, string path, string redistName, XmlReader reader, List<AssemblyRemapping> mapping)
        {
            AssemblyNameExtension fromEntry = null;
            AssemblyNameExtension toEntry = null;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (string.Equals(reader.Name, "From", StringComparison.OrdinalIgnoreCase) && !reader.IsEmptyElement && fromEntry == null)
                    {
                        AssemblyEntry newEntry = ReadFileListEntry(assemblyTableInfo, path, redistName, reader, false);
                        if (newEntry != null)
                        {
                            fromEntry = newEntry.AssemblyNameExtension;
                        }
                    }

                    if (string.Equals(reader.Name, "To", StringComparison.OrdinalIgnoreCase) && fromEntry != null && toEntry == null)
                    {
                        AssemblyEntry newEntry = ReadFileListEntry(assemblyTableInfo, path, redistName, reader, false);
                        if (newEntry != null)
                        {
                            toEntry = newEntry.AssemblyNameExtension;
                        }
                    }

                    if (fromEntry != null && toEntry != null)
                    {
                        var pair = new AssemblyRemapping(fromEntry, toEntry);

                        if (!mapping.Any(x => x.From.Equals(pair.From)))
                        {
                            mapping.Add(pair);
                        }

                        fromEntry = null;
                        toEntry = null;
                    }
                }

                if (reader.NodeType == XmlNodeType.EndElement && string.Equals(reader.Name, "From", StringComparison.OrdinalIgnoreCase))
                {
                    fromEntry = null;
                    toEntry = null;
                }

                if (reader.NodeType == XmlNodeType.EndElement && string.Equals(reader.Name, "Remap", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Parse the FileList section in the redist list.
        /// </summary>
        private static void ParseFileListSection(AssemblyTableInfo assemblyTableInfo, string path, string redistName, XmlReader reader, Dictionary<string, AssemblyEntry> assemblyEntries, List<AssemblyRemapping> remapEntries)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (string.Equals(reader.Name, "File", StringComparison.OrdinalIgnoreCase))
                    {
                        AssemblyEntry newEntry = ReadFileListEntry(assemblyTableInfo, path, redistName, reader, true);
                        if (newEntry != null)
                        {
                            // When comparing the assembly entries we want to compare the FullName which is a formatted as name, version, publicKeyToken and culture and whether the entry is a redistroot flag
                            // We do not need to add the redistName and the framework directory because this will be the same for all entries in the current redist list being read.
                            string hashIndex = String.Format(CultureInfo.InvariantCulture, "{0},{1}", newEntry.FullName, newEntry.IsRedistRoot == null ? "null" : newEntry.IsRedistRoot.ToString());

                            assemblyEntries.TryGetValue(hashIndex, out AssemblyEntry dictionaryEntry);
                            // If the entry is not in the hashtable or the entry is in the hashtable but the new entry has the ingac flag true, make sure the hashtable contains the entry with the ingac true.
                            if (dictionaryEntry == null || newEntry.InGAC)
                            {
                                assemblyEntries[hashIndex] = newEntry;
                            }
                        }
                    }

                    if (string.Equals(reader.Name, "Remap", StringComparison.OrdinalIgnoreCase))
                    {
                        if (remapEntries != null)
                        {
                            ParseRemapSection(assemblyTableInfo, path, redistName, reader, remapEntries);
                        }
                    }
                }

                // We are at the end of the fileList lets bail out and see if we can find other sections
                if (reader.NodeType == XmlNodeType.EndElement && string.Equals(reader.Name, "FileList", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Parse an individual FileListEntry in the redist list
        /// </summary>
        private static AssemblyEntry ReadFileListEntry(AssemblyTableInfo assemblyTableInfo, string path, string redistName, XmlReader reader, bool fullFusionNameRequired)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            reader.MoveToFirstAttribute();
            do
            {
                attributes.Add(reader.Name, reader.Value);
            } while (reader.MoveToNextAttribute());

            reader.MoveToElement();

            attributes.TryGetValue("AssemblyName", out string name);
            attributes.TryGetValue("Version", out string version);
            attributes.TryGetValue("PublicKeyToken", out string publicKeyToken);
            attributes.TryGetValue("Culture", out string culture);
            attributes.TryGetValue("InGAC", out string inGAC);
            attributes.TryGetValue("Retargetable", out string retargetable);
            attributes.TryGetValue("IsRedistRoot", out string isRedistRoot);
            if (!bool.TryParse(inGAC, out bool inGACFlag))
            {
                inGACFlag = true;                           // true by default 
            }

            // The retargetable flag is Yes or No for some reason
            bool retargetableFlag = "Yes".Equals(retargetable, StringComparison.OrdinalIgnoreCase);

            bool? isRedistRootFlag = null;                  // null by default.
            if (bool.TryParse(isRedistRoot, out bool isRedistRootAsBoolean))
            {
                isRedistRootFlag = isRedistRootAsBoolean;
            }

            bool isValidEntry = !string.IsNullOrEmpty(name) && (!fullFusionNameRequired || (fullFusionNameRequired && !string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(publicKeyToken) && !string.IsNullOrEmpty(culture)));
            Debug.Assert(isValidEntry, string.Format(CultureInfo.InvariantCulture, "Missing attribute in redist file: {0}, line #{1}", path, 
                reader is IXmlLineInfo ? ((IXmlLineInfo)reader).LineNumber : 0));
            AssemblyEntry newEntry = null;
            if (isValidEntry)
            {
                // Get the new entry from the redist list
                newEntry = new AssemblyEntry(name, version, publicKeyToken, culture, inGACFlag, isRedistRootFlag, redistName, assemblyTableInfo.FrameworkDirectory, retargetableFlag);
            }

            return newEntry;
        }

        #region Comparers
        private static readonly IComparer<AssemblyEntry> s_sortByVersionDescending = new SortByVersionDescending();

        /// <summary>
        /// The redist list is a collection of AssemblyEntry. We would like to have the redist list sorted on two keys.
        /// The first key is simple name, the simple names should be sorted alphabetically in ascending order (a,b,c,d,e).
        /// When the simple names are the same the sorting shouldbe done by version number rather than the alphabetical representation of the version.
        /// A numerical comparison is required because the alphabetical sort does not place the versions in numerical order. For example 1, 10, 2, 3, 4
        /// This sort should be done descending ( 10,9,8,7,6,5) so that if the resdist list is read from top to bottom the newest version is seen first.
        /// </summary>
        internal class SortByVersionDescending : IComparer, IComparer<AssemblyEntry>
        {
            public int Compare(object a, object b)
            {
                AssemblyEntry firstEntry = a as AssemblyEntry;
                AssemblyEntry secondEntry = b as AssemblyEntry;
                return Compare(firstEntry, secondEntry);
            }

            public int Compare(AssemblyEntry firstEntry, AssemblyEntry secondEntry)
            {
                Debug.Assert(firstEntry != null && secondEntry != null);
                if (firstEntry == null || secondEntry == null) return 0;

                AssemblyNameExtension firstAssemblyName = firstEntry.AssemblyNameExtension;
                AssemblyNameExtension secondAssemblyName = secondEntry.AssemblyNameExtension;

                // We want to sort first on the assembly name.
                int stringResult = string.Compare(firstAssemblyName.Name, secondAssemblyName.Name, StringComparison.OrdinalIgnoreCase);

                // If the simple names do not match then we do not need to sort based on version.
                if (stringResult != 0)
                {
                    return stringResult;
                }

                // We now want to sort based on the version number
                // The compare method is expected to return the following values:
                // Less than zero = right instance is less than left. 
                // Zero  = right instance is equal to left. 
                // Greater than zero  = right instance is greater than left. 

                // Want the greater version number to be on top in a list so we need to reverse the comparison
                int returnValue = firstAssemblyName.Version.CompareTo(secondAssemblyName.Version);
                if (returnValue == 0)
                {
                    return 0;
                }

                // The firstAssemblyName has a lower version than secondAssemblyName, we want to reverse them.
                return -returnValue;
            }
        }
        #endregion
    }

    /// <summary>
    /// Internal class representing a redist list or whitelist and its corresponding framework directory.
    /// </summary>
    internal class AssemblyTableInfo : IComparable
    {
        private string _descriptor;

        internal AssemblyTableInfo(string path, string frameworkDirectory)
        {
            Path = path;
            FrameworkDirectory = frameworkDirectory;
        }

        internal string Path { get; }

        internal string FrameworkDirectory { get; }

        internal string Descriptor => _descriptor ?? (_descriptor = Path + FrameworkDirectory);

        public int CompareTo(object obj)
        {
            var that = (AssemblyTableInfo)obj;
            return String.Compare(Descriptor, that.Descriptor, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Provide a mechanism to determine where the subset white lists are located by searching the target framework folders 
    /// for a list of provided subset list names.
    /// </summary>
    internal class SubsetListFinder
    {
        #region Data

        // Process wide cache of subset lists found on disk under fx directories.
        // K: target framework directory + subsetNames, V: subset list paths found on disk underneath the subsetList folder
        private static Dictionary<string, string[]> s_subsetListPathCache;

        // Locl for subsetListPathCache
        private static readonly Object s_subsetListPathCacheLock = new Object();

        // Folder to look for the subset lists in under the target framework directories
        private const string subsetListFolder = "SubsetList";

        /// <summary>
        /// The subset names to search for.
        /// </summary>
        private readonly string[] _subsetToSearchFor;

        #endregion

        #region Constructor

        /// <summary>
        /// This class takes in a list of subset names to look for and provides a method to search the target framework directories to see if those
        /// files exist.
        /// </summary>
        /// <param name="subsetToSearchFor">String array of subset names, ie  Client, Net, MySubset. This may be null or empty if no subsets were requested to be 
        /// found in the target framework directories. This can happen if the the subsets are instead passed in as InstalledDefaultSubsetTables</param>
        internal SubsetListFinder(string[] subsetToSearchFor)
        {
            ErrorUtilities.VerifyThrowArgumentNull(subsetToSearchFor, nameof(subsetToSearchFor));
            _subsetToSearchFor = subsetToSearchFor;
        }

        #endregion

        #region Properties
        /// <summary>
        ///  Folder to look for the subset lists under the target framework directories
        /// </summary>
        public static string SubsetListFolder => subsetListFolder;

        #endregion

        #region Methods
        /// <summary>
        /// Given a framework directory path, this method will find matching
        /// subset list files underneath that path.  An appdomain-wide cache is used to
        /// avoid hitting the disk multiple times for the same framework directory and set of requested subset names.
        /// </summary>
        /// <param name="frameworkDirectory">Framework directory to look for set of subset files under</param>
        /// <returns>Array of paths locations to subset lists under the given framework directory.</returns>
        public string[] GetSubsetListPathsFromDisk(string frameworkDirectory)
        {
            ErrorUtilities.VerifyThrowArgumentNull(frameworkDirectory, nameof(frameworkDirectory));

            // Make sure we have some subset names to search for it is possible that no subsets are asked for
            // so we should return as quickly as possible in that case.
            if (_subsetToSearchFor.Length > 0)
            {
                lock (s_subsetListPathCacheLock)
                {
                    // We want to cache the paths to the subset files so that we do not have to hit the disk and check for the files 
                    // each time RAR is called within the appdomain.
                    if (s_subsetListPathCache == null)
                    {
                        s_subsetListPathCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                    }

                    // TargetFrameworkDirectory is not unique enough because a different invocation could ask for a different 
                    // set of subset files from the same TargetFrameworkDirectory
                    string concatenatedSubsetListNames = String.Join(";", _subsetToSearchFor);

                    string key = frameworkDirectory + ":" + concatenatedSubsetListNames;

                    s_subsetListPathCache.TryGetValue(key, out string[] subsetLists);
                    if (subsetLists == null)
                    {
                        // Get the path to the subset folder under the target framework directory
                        string subsetDirectory = Path.Combine(frameworkDirectory, subsetListFolder);

                        var subsetFilesForFrameworkDirectory = new List<string>();

                        // Go through each of the subsets and see if it is in the target framework subset directory 
                        foreach (string subsetName in _subsetToSearchFor)
                        {
                            string subsetFilePath = Path.Combine(subsetDirectory, subsetName + ".xml");
                            if (File.Exists(subsetFilePath))
                            {
                                subsetFilesForFrameworkDirectory.Add(subsetFilePath);
                            }
                        }

                        // Note, even if the array is empty we still want to add it to the cache, because some 
                        // target framework directories may never contain a subset file (for example 2.05727 and 3.0)
                        // for this reason we should not check them everytime if the files are not found.
                        s_subsetListPathCache[key] = subsetFilesForFrameworkDirectory.ToArray();
                        return s_subsetListPathCache[key];
                    }
                    else
                    {
                        return subsetLists;
                    }
                }
            }

            return Array.Empty<string>();
        }
        #endregion
    }

    /// <summary>
    /// Describes an assembly entry found in an installed assembly table.
    /// </summary>
    internal class AssemblyEntry
    {
        private AssemblyNameExtension _assemblyName;

        public AssemblyEntry(string name, string version, string publicKeyToken, string culture, bool inGAC, bool? isRedistRoot, string redistName, string frameworkDirectory, bool retargetable)
        {
            Debug.Assert(name != null && frameworkDirectory != null);
            SimpleName = name;
            if (name != null && version != null && publicKeyToken != null && culture != null)
            {
                FullName = $"{name}, Version={version}, Culture={culture}, PublicKeyToken={publicKeyToken}";
            }
            else if (name != null && version != null && publicKeyToken != null)
            {
                FullName = $"{name}, Version={version}, PublicKeyToken={publicKeyToken}";
            }
            else if (name != null && version != null && culture != null)
            {
                FullName = $"{name}, Version={version}, Culture={culture}";
            }
            else if (name != null && version != null)
            {
                FullName = $"{name}, Version={version}";
            }
            else if (name != null && publicKeyToken != null)
            {
                FullName = $"{name}, PublicKeyToken={version}";
            }
            else if (name != null && culture != null)
            {
                FullName = $"{name}, Culture={culture}";
            }
            else if (name != null)
            {
                FullName = $"{name}";
            }

            if (retargetable)
            {
                FullName += ", Retargetable=Yes";
            }

            InGAC = inGAC;
            IsRedistRoot = isRedistRoot;
            RedistName = redistName;
            FrameworkDirectory = frameworkDirectory;
            Retargetable = retargetable;
        }

        public string FullName { get; }
        public bool InGAC { get; }
        public bool? IsRedistRoot { get;  }
        public string RedistName { get; }
        public string SimpleName { get; }
        public string FrameworkDirectory { get; }
        public bool Retargetable { get; }

        public AssemblyNameExtension AssemblyNameExtension
        {
            get
            {
                if (_assemblyName == null)
                {
                    _assemblyName = new AssemblyNameExtension(FullName, true);
                    _assemblyName.MarkImmutable();
                }

                return _assemblyName;
            }
        }
    }
}
