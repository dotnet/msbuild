// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This task resolves items in the build process (built, dependencies, satellites,
    /// content, debug symbols, documentation, etc.) to files for manifest generation.
    /// </Summary>
    /// <comment>
    /// This task executes following steps:
    ///   (1) Filter out Framework assemblies
    ///   (2) Filter out non-existant files
    ///   (3) Build list of Dependencies from built items with CopyLocal=True
    ///   (4) Build list of Prerequisites from built items with CopyLocal=False
    ///   (5) Build list of Satellites from built items based on TargetCulture
    ///   (6) Build list of Files from Files and ExtraFiles inputs, using next step
    ///   (7) For each PublishFile item...
    ///    If item is on Dependencies list then move it to Prerequisites list
    ///    If item is on Content list then add it to File list unless it is excluded
    ///    If item is on Extra list then add it to File list only if it is included
    ///    Apply Group and Optional attributes from PublishFile items to built items
    ///    (8) Insure all output items have a TargetPath, and if in a Group that IsOptional is set
    /// </comment>
    public sealed class ResolveManifestFiles : TaskExtension
    {
        #region Fields

        private ITaskItem[] _extraFiles;
        private ITaskItem[] _files;
        private ITaskItem[] _managedAssemblies;
        private ITaskItem[] _nativeAssemblies;
        private ITaskItem[] _publishFiles;
        private ITaskItem[] _satelliteAssemblies;
        private CultureInfo _targetCulture;
        private bool _includeAllSatellites;

        private string _targetFrameworkVersion;
        // if signing manifests is on and not all app files are included, then the project can't be published.
        private bool _canPublish;
        #endregion

        #region Properties

        public ITaskItem DeploymentManifestEntryPoint { get; set; }

        public ITaskItem EntryPoint { get; set; }

        public ITaskItem[] ExtraFiles
        {
            get => _extraFiles;
            set => _extraFiles = Util.SortItems(value);
        }

        public ITaskItem[] Files
        {
            get => _files;
            set => _files = Util.SortItems(value);
        }

        public ITaskItem[] ManagedAssemblies
        {
            get => _managedAssemblies;
            set => _managedAssemblies = Util.SortItems(value);
        }

        public ITaskItem[] NativeAssemblies
        {
            get => _nativeAssemblies;
            set => _nativeAssemblies = Util.SortItems(value);
        }

        [Output]
        public ITaskItem[] OutputAssemblies { get; set; }

        [Output]
        public ITaskItem OutputDeploymentManifestEntryPoint { get; set; }

        [Output]
        public ITaskItem OutputEntryPoint { get; set; }

        [Output]
        public ITaskItem[] OutputFiles { get; set; }

        public ITaskItem[] PublishFiles
        {
            get => _publishFiles;
            set => _publishFiles = Util.SortItems(value);
        }

        public ITaskItem[] SatelliteAssemblies
        {
            get => _satelliteAssemblies;
            set => _satelliteAssemblies = Util.SortItems(value);
        }

        public string TargetCulture { get; set; }

        public bool SigningManifests { get; set; }

        public string TargetFrameworkVersion
        {
            get
            {
                if (string.IsNullOrEmpty(_targetFrameworkVersion))
                {
                    return Constants.TargetFrameworkVersion35;
                }
                return _targetFrameworkVersion;
            }
            set => _targetFrameworkVersion = value;
        }

        #endregion

        public override bool Execute()
        {
            if (!ValidateInputs())
            {
                return false;
            }

            // if signing manifests is on and not all app files are included, then the project can't be published.
            _canPublish = true;
            bool is35Project = (CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) >= 0);

            GetPublishInfo(out List<PublishInfo> assemblyPublishInfoList, out List<PublishInfo> filePublishInfoList, out List<PublishInfo> satellitePublishInfoList, out List<PublishInfo> manifestEntryPointList);

            OutputAssemblies = GetOutputAssembliesAndSatellites(assemblyPublishInfoList, satellitePublishInfoList);

            if (!_canPublish && is35Project)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.ManifestsSignedHashExcluded");
                return false;
            }

            OutputFiles = GetOutputFiles(filePublishInfoList);

            if (!_canPublish && is35Project)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.ManifestsSignedHashExcluded");
                return false;
            }

            OutputEntryPoint = GetOutputEntryPoint(EntryPoint, manifestEntryPointList);

            if (!_canPublish && is35Project)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.ManifestsSignedHashExcluded");
                return false;
            }

            OutputDeploymentManifestEntryPoint = GetOutputEntryPoint(DeploymentManifestEntryPoint, manifestEntryPointList);

            if (!_canPublish && is35Project)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.ManifestsSignedHashExcluded");
                return false;
            }

            return true;
        }

        private static Version ConvertFrameworkVersionToString(string version)
        {
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return new Version(version.Substring(1));
            }
            return new Version(version);
        }

        private static int CompareFrameworkVersions(string versionA, string versionB)
        {
            Version version1 = ConvertFrameworkVersionToString(versionA);
            Version version2 = ConvertFrameworkVersionToString(versionB);
            return version1.CompareTo(version2);
        }

        private bool ValidateInputs()
        {
            if (!String.IsNullOrEmpty(TargetCulture))
            {
                if (String.Equals(TargetCulture, "*", StringComparison.Ordinal))
                {
                    _includeAllSatellites = true;
                }
                else if (!String.Equals(TargetCulture, "neutral", StringComparison.Ordinal))
                {
                    try
                    {
                        _targetCulture = new CultureInfo(TargetCulture);
                    }
                    catch (ArgumentException)
                    {
                        Log.LogErrorWithCodeFromResources("General.InvalidValue", "TargetCulture", "ResolveManifestFiles");
                        return false;
                    }
                }
            }
            return true;
        }

        #region Helpers

        // Creates an output item for a an assembly, with optional Group attribute.
        private static ITaskItem CreateAssemblyItem(ITaskItem item, string group, string targetPath, string includeHash)
        {
            ITaskItem outputItem = new TaskItem(item.ItemSpec);
            item.CopyMetadataTo(outputItem);
            outputItem.SetMetadata("DependencyType", "Install");
            if (String.IsNullOrEmpty(targetPath))
            {
                targetPath = GetItemTargetPath(outputItem);
            }
            outputItem.SetMetadata(ItemMetadataNames.targetPath, targetPath);
            if (!String.IsNullOrEmpty(group))
            {
                outputItem.SetMetadata("Group", group);
            }

            if (!String.IsNullOrEmpty(includeHash))
            {
                outputItem.SetMetadata("IncludeHash", includeHash);
            }
            return outputItem;
        }

        // Creates an output item for a file, with optional Group and IsData attributes.
        private static ITaskItem CreateFileItem(ITaskItem item, string group, string targetPath, string includeHash, bool isDataFile)
        {
            ITaskItem outputItem = new TaskItem(item.ItemSpec);
            item.CopyMetadataTo(outputItem);
            if (String.IsNullOrEmpty(targetPath))
            {
                targetPath = GetItemTargetPath(outputItem);
            }
            outputItem.SetMetadata(ItemMetadataNames.targetPath, targetPath);
            if (!String.IsNullOrEmpty(group) && !isDataFile)
            {
                outputItem.SetMetadata("Group", group);
            }

            if (!String.IsNullOrEmpty(includeHash))
            {
                outputItem.SetMetadata("IncludeHash", includeHash);
            }

            outputItem.SetMetadata("IsDataFile", isDataFile.ToString().ToLowerInvariant());
            return outputItem;
        }

        // Creates an output item for a prerequisite.
        private static ITaskItem CreatePrerequisiteItem(ITaskItem item)
        {
            ITaskItem outputItem = new TaskItem(item.ItemSpec);
            item.CopyMetadataTo(outputItem);
            outputItem.SetMetadata("DependencyType", "Prerequisite");
            return outputItem;
        }

        private static bool GetItemCopyLocal(ITaskItem item)
        {
            string copyLocal = item.GetMetadata(ItemMetadataNames.copyLocal);
            if (!String.IsNullOrEmpty(copyLocal))
            {
                return ConvertUtil.ToBoolean(copyLocal);
            }
            return true; // always return true if item does not have a CopyLocal attribute
        }

        // Returns the culture for the specified item, first by looking for an attribute and if not found
        // attempts to infer from the disk path.
        private static CultureInfo GetItemCulture(ITaskItem item)
        {
            string itemCulture = item.GetMetadata("Culture");
            if (String.IsNullOrEmpty(itemCulture))
            {
                // Infer culture from path (i.e. "obj\debug\fr\WindowsApplication1.resources.dll" -> "fr")
                string[] pathSegments = PathUtil.GetPathSegments(item.ItemSpec);
                itemCulture = pathSegments.Length > 1 ? pathSegments[pathSegments.Length - 2] : null;
                Debug.Assert(!String.IsNullOrEmpty(itemCulture), String.Format(CultureInfo.CurrentCulture, "Satellite item '{0}' is missing expected attribute '{1}'", item.ItemSpec, "Culture"));
                item.SetMetadata("Culture", itemCulture);
            }
            return new CultureInfo(itemCulture);
        }

        private static string GetItemTargetPath(ITaskItem item)
        {
            string targetPath = item.GetMetadata(ItemMetadataNames.targetPath);
            if (String.IsNullOrEmpty(targetPath))
            {
                targetPath = Path.GetFileName(item.ItemSpec);
                // If item is a satellite then make sure the culture is part of the path...
                string assemblyType = item.GetMetadata("AssemblyType");
                if (String.Equals(assemblyType, "Satellite", StringComparison.Ordinal))
                {
                    CultureInfo itemCulture = GetItemCulture(item);
                    if (itemCulture != null)
                    {
                        targetPath = Path.Combine(itemCulture.ToString(), targetPath);
                    }
                }
            }
            return targetPath;
        }

        private void GetOutputAssemblies(List<PublishInfo> publishInfos, ref List<ITaskItem> assemblyList)
        {
            var assemblyMap = new AssemblyMap();

            // Add all managed assemblies to the AssemblyMap, except assemblies that are part of the .NET Framework...
            if (_managedAssemblies != null)
            {
                foreach (ITaskItem item in _managedAssemblies)
                {
                    if (!IsFiltered(item))
                    {
                        item.SetMetadata("AssemblyType", "Managed");
                        assemblyMap.Add(item);
                    }
                }
            }

            if (_nativeAssemblies != null)
            {
                foreach (ITaskItem item in _nativeAssemblies)
                {
                    if (!IsFiltered(item))
                    {
                        item.SetMetadata("AssemblyType", "Native");
                        assemblyMap.Add(item);
                    }
                }
            }

            // Apply PublishInfo state from PublishFile items...
            foreach (PublishInfo publishInfo in publishInfos)
            {
                MapEntry entry = assemblyMap[publishInfo.key];
                if (entry != null)
                {
                    entry.publishInfo = publishInfo;
                }
                else
                {
                    Log.LogWarningWithCodeFromResources("ResolveManifestFiles.PublishFileNotFound", publishInfo.key);
                }
            }

            // Go through the AssemblyMap and determine which items get added to ouput AssemblyList based
            // on computed PublishFlags for each item...
            foreach (MapEntry entry in assemblyMap)
            {
                // If PublishInfo didn't come from a PublishFile item, then construct PublishInfo from the item
                if (entry.publishInfo == null)
                {
                    entry.publishInfo = new PublishInfo();
                }

                // If state is auto then also need to look on the item to see whether the dependency type
                // has alread been specified upstream (i.e. from ResolveNativeReference task)...
                if (entry.publishInfo.state == PublishState.Auto)
                {
                    string dependencyType = entry.item.GetMetadata("DependencyType");
                    if (String.Equals(dependencyType, "Prerequisite", StringComparison.Ordinal))
                    {
                        entry.publishInfo.state = PublishState.Prerequisite;
                    }
                    else if (String.Equals(dependencyType, "Install", StringComparison.Ordinal))
                    {
                        entry.publishInfo.state = PublishState.Include;
                    }
                }

                bool copyLocal = GetItemCopyLocal(entry.item);
                PublishFlags flags = PublishFlags.GetAssemblyFlags(entry.publishInfo.state, copyLocal);

                if (flags.IsPublished &&
                    string.Equals(entry.publishInfo.includeHash, "false", StringComparison.OrdinalIgnoreCase) &&
                    SigningManifests)
                {
                    _canPublish = false;
                }

                if (flags.IsPublished)
                {
                    assemblyList.Add(CreateAssemblyItem(entry.item, entry.publishInfo.group, entry.publishInfo.targetPath, entry.publishInfo.includeHash));
                }
                else if (flags.IsPrerequisite)
                {
                    assemblyList.Add(CreatePrerequisiteItem(entry.item));
                }
            }
        }

        private ITaskItem[] GetOutputAssembliesAndSatellites(List<PublishInfo> assemblyPublishInfos, List<PublishInfo> satellitePublishInfos)
        {
            var assemblyList = new List<ITaskItem>();
            GetOutputAssemblies(assemblyPublishInfos, ref assemblyList);
            GetOutputSatellites(satellitePublishInfos, ref assemblyList);
            return assemblyList.ToArray();
        }

        private ITaskItem[] GetOutputFiles(List<PublishInfo> publishInfos)
        {
            var fileList = new List<ITaskItem>();
            var fileMap = new FileMap();

            // Add all input Files to the FileMap, flagging them to be published by default...
            if (Files != null)
            {
                foreach (ITaskItem item in Files)
                {
                    fileMap.Add(item, true);
                }
            }

            // Add all input ExtraFiles to the FileMap, flagging them to NOT be published by default...
            if (ExtraFiles != null)
            {
                foreach (ITaskItem item in ExtraFiles)
                {
                    fileMap.Add(item, false);
                }
            }

            // Apply PublishInfo state from PublishFile items...
            foreach (PublishInfo publishInfo in publishInfos)
            {
                MapEntry entry = fileMap[publishInfo.key];
                if (entry != null)
                {
                    entry.publishInfo = publishInfo;
                }
                else
                {
                    Log.LogWarningWithCodeFromResources("ResolveManifestFiles.PublishFileNotFound", publishInfo.key);
                }
            }

            // Go through the FileMap and determine which items get added to ouput FileList based
            // on computed PublishFlags for each item...
            foreach (MapEntry entry in fileMap)
            {
                // If PublishInfo didn't come from a PublishFile item, then construct PublishInfo from the item
                if (entry.publishInfo == null)
                {
                    entry.publishInfo = new PublishInfo();
                }

                string fileExtension = Path.GetExtension(entry.item.ItemSpec);
                PublishFlags flags = PublishFlags.GetFileFlags(entry.publishInfo.state, fileExtension, entry.includedByDefault);

                if (flags.IsPublished &&
                    string.Equals(entry.publishInfo.includeHash, "false", StringComparison.OrdinalIgnoreCase) &&
                    SigningManifests)
                {
                    _canPublish = false;
                }

                if (flags.IsPublished)
                {
                    fileList.Add(CreateFileItem(entry.item, entry.publishInfo.group, entry.publishInfo.targetPath, entry.publishInfo.includeHash, flags.IsDataFile));
                }
            }

            return fileList.ToArray();
        }

        private void GetOutputSatellites(List<PublishInfo> publishInfos, ref List<ITaskItem> assemblyList)
        {
            var satelliteMap = new FileMap();

            if (_satelliteAssemblies != null)
            {
                foreach (ITaskItem item in _satelliteAssemblies)
                {
                    item.SetMetadata("AssemblyType", "Satellite");
                    satelliteMap.Add(item, true);
                }
            }

            // Apply PublishInfo state from PublishFile items...
            foreach (PublishInfo publishInfo in publishInfos)
            {
                string key = publishInfo.key + ".dll";
                MapEntry entry = satelliteMap[key];
                if (entry != null)
                {
                    entry.publishInfo = publishInfo;
                }
                else
                {
                    Log.LogWarningWithCodeFromResources("ResolveManifestFiles.PublishFileNotFound", publishInfo.key);
                }
            }

            // Go through the AssemblyMap and determine which items get added to ouput SatelliteList based
            // on computed PublishFlags for each item...
            foreach (MapEntry entry in satelliteMap)
            {
                // If PublishInfo didn't come from a PublishFile item, then construct PublishInfo from the item
                if (entry.publishInfo == null)
                {
                    entry.publishInfo = new PublishInfo();
                }

                CultureInfo satelliteCulture = GetItemCulture(entry.item);
                PublishFlags flags = PublishFlags.GetSatelliteFlags(entry.publishInfo.state, satelliteCulture, _targetCulture, _includeAllSatellites);

                if (flags.IsPublished &&
                    string.Equals(entry.publishInfo.includeHash, "false", StringComparison.OrdinalIgnoreCase) &&
                    SigningManifests)
                {
                    _canPublish = false;
                }

                if (flags.IsPublished)
                {
                    assemblyList.Add(CreateAssemblyItem(entry.item, entry.publishInfo.group, entry.publishInfo.targetPath, entry.publishInfo.includeHash));
                }
                else if (flags.IsPrerequisite)
                {
                    assemblyList.Add(CreatePrerequisiteItem(entry.item));
                }
            }
        }

        private ITaskItem GetOutputEntryPoint(ITaskItem entryPoint, List<PublishInfo> manifestEntryPointList)
        {
            if (entryPoint == null)
            {
                return null;
            }
            var outputEntryPoint = new TaskItem(entryPoint.ItemSpec);
            entryPoint.CopyMetadataTo(outputEntryPoint);
            string targetPath = entryPoint.GetMetadata("TargetPath");
            if (!string.IsNullOrEmpty(targetPath))
            {
                for (int i = 0; i < manifestEntryPointList.Count; i++)
                {
                    if (String.Equals(targetPath, manifestEntryPointList[i].key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(manifestEntryPointList[i].includeHash))
                        {
                            if (manifestEntryPointList[i].state != PublishState.Exclude &&
                                string.Equals(
                                    manifestEntryPointList[i].includeHash,
                                    "false",
                                    StringComparison.OrdinalIgnoreCase) &&
                                SigningManifests)
                            {
                                _canPublish = false;
                            }
                            outputEntryPoint.SetMetadata("IncludeHash", manifestEntryPointList[i].includeHash);
                        }
                        return outputEntryPoint;
                    }
                }
            }

            return outputEntryPoint;
        }

        // Returns PublishFile items separated into separate arrays by FileType attribute.
        private void GetPublishInfo(
            out List<PublishInfo> assemblyPublishInfos,
            out List<PublishInfo> filePublishInfos,
            out List<PublishInfo> satellitePublishInfos,
            out List<PublishInfo> manifestEntryPointPublishInfos)
        {
            assemblyPublishInfos = new List<PublishInfo>();
            filePublishInfos = new List<PublishInfo>();
            satellitePublishInfos = new List<PublishInfo>();
            manifestEntryPointPublishInfos = new List<PublishInfo>();

            if (PublishFiles != null)
            {
                foreach (ITaskItem item in PublishFiles)
                {
                    var publishInfo = new PublishInfo(item);
                    string fileType = item.GetMetadata("FileType");
                    switch (fileType)
                    {
                        case "Assembly":
                            assemblyPublishInfos.Add(publishInfo);
                            break;
                        case "File":
                            filePublishInfos.Add(publishInfo);
                            break;
                        case "Satellite":
                            satellitePublishInfos.Add(publishInfo);
                            break;
                        case "ManifestEntryPoint":
                            manifestEntryPointPublishInfos.Add(publishInfo);
                            break;
                        default:
                            Log.LogWarningWithCodeFromResources("GenerateManifest.InvalidItemValue", "FileType", item.ItemSpec);
                            continue;
                    }
                }
            }
        }

        private bool IsFiltered(ITaskItem item)
        {
            // If assembly is part of the FX then it should be filtered out...
            // System.Reflection.AssemblyName.GetAssemblyName throws if file is not an assembly.
            // We're using AssemblyIdentity.FromManagedAssembly here because it just does an
            // OpenScope and returns null if not an assembly, which is much faster.

            AssemblyIdentity identity = AssemblyIdentity.FromManagedAssembly(item.ItemSpec);
            if (identity != null && identity.IsInFramework(Constants.DotNetFrameworkIdentifier, TargetFrameworkVersion))
            {
                return true;
            }

            // If assembly is not a "Redist Root" then it should be filtered out...
            string str = item.GetMetadata("IsRedistRoot");
            if (!String.IsNullOrEmpty(str))
            {
                if (Boolean.TryParse(str, out bool isRedistRoot))
                {
                    return !isRedistRoot;
                }
            }
            return false;
        }

        #endregion

        #region PublishInfo
        private class PublishInfo
        {
            public readonly string key;
            public readonly string group;
            public readonly string targetPath;
            public readonly string includeHash;
            public PublishState state = PublishState.Auto;
            public PublishInfo()
            {
            }
            public PublishInfo(ITaskItem item)
            {
                this.key = item.ItemSpec?.ToLowerInvariant();
                this.group = item.GetMetadata("Group");
                this.state = StringToPublishState(item.GetMetadata("PublishState"));
                this.includeHash = item.GetMetadata("IncludeHash");
                this.targetPath = item.GetMetadata(ItemMetadataNames.targetPath);
            }
        }
        #endregion

        #region MapEntry
        private class MapEntry
        {
            public readonly ITaskItem item;
            public readonly bool includedByDefault;
            public PublishInfo publishInfo;
            public MapEntry(ITaskItem item, bool includedByDefault)
            {
                this.item = item;
                this.includedByDefault = includedByDefault;
            }
        }
        #endregion

        #region AssemblyMap
        private class AssemblyMap : IEnumerable
        {
            private readonly Dictionary<string, MapEntry> _dictionary = new Dictionary<string, MapEntry>();
            private readonly Dictionary<string, MapEntry> _simpleNameDictionary = new Dictionary<string, MapEntry>();

            public MapEntry this[string fusionName]
            {
                get
                {
                    string key = fusionName.ToLowerInvariant();
                    if (!_dictionary.TryGetValue(key, out MapEntry entry))
                    {
                        _simpleNameDictionary.TryGetValue(key, out entry);
                    }
                    return entry;
                }
            }

            public void Add(ITaskItem item)
            {
                var entry = new MapEntry(item, true);
                string fusionName = item.GetMetadata(ItemMetadataNames.fusionName);
                if (String.IsNullOrEmpty(fusionName))
                {
                    fusionName = Path.GetFileNameWithoutExtension(item.ItemSpec);
                }

                // Add to map with full name, for SpecificVersion=true case
                string key = fusionName.ToLowerInvariant();
                Debug.Assert(!_dictionary.ContainsKey(key), String.Format(CultureInfo.CurrentCulture, "Two or more items with same key '{0}' detected", key));
                if (!_dictionary.ContainsKey(key))
                {
                    _dictionary.Add(key, entry);
                }

                // Also add to map with simple name, for SpecificVersion=false case
                int i = fusionName.IndexOf(',');
                if (i > 0)
                {
                    string simpleName = fusionName.Substring(0, i); //example: "ClassLibrary1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" -> "ClassLibrary1"
                    key = simpleName.ToLowerInvariant();
                    // If there are multiple with same simple name then we'll take the first one and ignore the rest, which is not an unreasonable thing to do
                    if (!_simpleNameDictionary.ContainsKey(key))
                    {
                        _simpleNameDictionary.Add(key, entry);
                    }
                }
            }
            
            IEnumerator IEnumerable.GetEnumerator()
            {
                return _dictionary.Values.GetEnumerator();
            }
        }
        #endregion

        #region FileMap
        private class FileMap : IEnumerable
        {
            private readonly Dictionary<string, MapEntry> _dictionary = new Dictionary<string, MapEntry>();

            public MapEntry this[string targetPath]
            {
                get
                {
                    string key = targetPath.ToLowerInvariant();
                    _dictionary.TryGetValue(key, out MapEntry entry);
                    return entry;
                }
            }

            public void Add(ITaskItem item, bool includedByDefault)
            {
                string targetPath = GetItemTargetPath(item);
                Debug.Assert(!String.IsNullOrEmpty(targetPath));
                if (String.IsNullOrEmpty(targetPath)) return;
                string key = targetPath.ToLowerInvariant();
                Debug.Assert(!_dictionary.ContainsKey(key), String.Format(CultureInfo.CurrentCulture, "Two or more items with same '{0}' attribute detected", ItemMetadataNames.targetPath));
                var entry = new MapEntry(item, includedByDefault);
                if (!_dictionary.ContainsKey(key))
                {
                    _dictionary.Add(key, entry);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _dictionary.Values.GetEnumerator();
            }
        }
        #endregion

        #region PublishFlags
        private enum PublishState
        {
            Auto,
            Include,
            Exclude,
            DataFile,
            Prerequisite
        }

        private static PublishState StringToPublishState(string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                try
                {
                    return (PublishState)Enum.Parse(typeof(PublishState), value, false);
                }
                catch (FormatException)
                {
                    Debug.Fail(String.Format(CultureInfo.CurrentCulture, "Invalid value '{0}' for {1}", value, "PublishState"));
                }
                catch (ArgumentException)
                {
                    Debug.Fail(String.Format(CultureInfo.CurrentCulture, "Invalid value '{0}' for {1}", value, "PublishState"));
                }
            }
            return PublishState.Auto;
        }

        private class PublishFlags
        {
            private PublishFlags(bool isDataFile, bool isPrerequisite, bool isPublished)
            {
                IsDataFile = isDataFile;
                IsPrerequisite = isPrerequisite;
                IsPublished = isPublished;
            }

            public static PublishFlags GetAssemblyFlags(PublishState state, bool copyLocal)
            {
                const bool isDataFile = false;
                bool isPrerequisite = false;
                bool isPublished = false;
                switch (state)
                {
                    case PublishState.Auto:
                        isPrerequisite = !copyLocal;
                        isPublished = copyLocal;
                        break;
                    case PublishState.Include:
                        isPrerequisite = false;
                        isPublished = true;
                        break;
                    case PublishState.Exclude:
                        isPrerequisite = false;
                        isPublished = false;
                        break;
                    case PublishState.DataFile:
                        Debug.Fail(String.Format(CultureInfo.CurrentCulture, "PublishState.DataFile is invalid for an assembly"));
                        break;
                    case PublishState.Prerequisite:
                        isPrerequisite = true;
                        isPublished = false;
                        break;
                    default:
                        Debug.Fail(String.Format(CultureInfo.CurrentCulture, "Unhandled value PublishFlags.{0}", state.ToString()));
                        break;
                }
                return new PublishFlags(isDataFile, isPrerequisite, isPublished);
            }

            public static PublishFlags GetFileFlags(PublishState state, string fileExtension, bool includedByDefault)
            {
                bool isDataFile = false;
                const bool isPrerequisite = false;
                bool isPublished = false;
                switch (state)
                {
                    case PublishState.Auto:
                        isDataFile = includedByDefault && PathUtil.IsDataFile(fileExtension);
                        isPublished = includedByDefault;
                        break;
                    case PublishState.Include:
                        isDataFile = false;
                        isPublished = true;
                        break;
                    case PublishState.Exclude:
                        isDataFile = false;
                        isPublished = false;
                        break;
                    case PublishState.DataFile:
                        isDataFile = true;
                        isPublished = true;
                        break;
                    case PublishState.Prerequisite:
                        Debug.Fail(String.Format(CultureInfo.CurrentCulture, "PublishState.Prerequisite is invalid for a file"));
                        break;
                    default:
                        Debug.Fail(String.Format(CultureInfo.CurrentCulture, "Unhandled value PublishFlags.{0}", state.ToString()));
                        break;
                }
                return new PublishFlags(isDataFile, isPrerequisite, isPublished);
            }

            public static PublishFlags GetSatelliteFlags(PublishState state, CultureInfo satelliteCulture, CultureInfo targetCulture, bool includeAllSatellites)
            {
                bool includedByDefault = IsSatelliteIncludedByDefault(satelliteCulture, targetCulture, includeAllSatellites);
                const bool isDataFile = false;
                bool isPrerequisite = false;
                bool isPublished = false;
                switch (state)
                {
                    case PublishState.Auto:
                        isPrerequisite = false;
                        isPublished = includedByDefault;
                        break;
                    case PublishState.Include:
                        isPrerequisite = false;
                        isPublished = true;
                        break;
                    case PublishState.Exclude:
                        isPrerequisite = false;
                        isPublished = false;
                        break;
                    case PublishState.DataFile:
                        Debug.Fail(String.Format(CultureInfo.CurrentCulture, "PublishState.DataFile is invalid for an assembly"));
                        break;
                    case PublishState.Prerequisite:
                        isPrerequisite = true;
                        isPublished = false;
                        break;
                    default:
                        Debug.Fail(String.Format(CultureInfo.CurrentCulture, "Unhandled value PublishFlags.{0}", state.ToString()));
                        break;
                }
                return new PublishFlags(isDataFile, isPrerequisite, isPublished);
            }

            public bool IsDataFile { get; }

            public bool IsPrerequisite { get; }

            public bool IsPublished { get; }

            private static bool IsSatelliteIncludedByDefault(CultureInfo satelliteCulture, CultureInfo targetCulture, bool includeAllSatellites)
            {
                // If target culture not specified then satellite is not included by default...
                if (targetCulture == null)
                {
                    return includeAllSatellites;
                }

                // If satellite culture matches target culture then satellite is included by default...
                if (targetCulture.Equals(satelliteCulture))
                {
                    return true;
                }

                // If satellite culture matches target culture's neutral culture then satellite is included by default...
                // For example, if target culture is "fr-FR" then target culture's neutral culture is "fr",
                // so if satellite culture is also "fr" then it will be included as well.
                if (!targetCulture.IsNeutralCulture && targetCulture.Parent.Equals(satelliteCulture))
                {
                    return true;
                }

                // Otherwise satellite is not included by default...
                return includeAllSatellites;
            }
        }
        #endregion
    }
}
