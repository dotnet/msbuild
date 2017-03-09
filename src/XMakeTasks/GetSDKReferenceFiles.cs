// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Gathers the reference assemblies from the SDK based on what configuration and architecture a SDK references.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolves an SDKReference to a full path on disk
    /// </summary>
    public class GetSDKReferenceFiles : TaskExtension
    {
        /// <summary>
        /// Set of resolvedSDK references which we will use to find the reference assemblies.
        /// </summary>
        private ITaskItem[] _resolvedSDKReferences = new TaskItem[0];

        /// <summary>
        /// Set of the redist files for the resolved sdks
        /// </summary>
        private ITaskItem[] _sdkRedistFiles = new TaskItem[0];

        /// <summary>
        /// Resolved reference assemblies from the SDK
        /// </summary>
        private ITaskItem[] _references = new TaskItem[0];

        /// <summary>
        /// Redist files from the SDKs
        /// </summary>
        private ITaskItem[] _redistFiles = new TaskItem[0];

        /// <summary>
        /// Set of resolved reference assemblies. This removes any duplicate ones between sdks.
        /// </summary>
        private HashSet<ResolvedReferenceAssembly> _resolvedReferences = new HashSet<ResolvedReferenceAssembly>();

        /// <summary>
        /// Set of resolved reference assemblies. This removes any duplicate ones between sdks.
        /// </summary>
        private HashSet<ResolvedRedistFile> _resolveRedistFiles = new HashSet<ResolvedRedistFile>();

        /// <summary>
        /// Files to be copied locally
        /// </summary>
        private ITaskItem[] _copyLocalFiles = new TaskItem[0];

        /// <summary>
        /// Set of reference assembly extensions to look for.
        /// </summary>
        private string[] _referenceExtensions = new string[] { ".winmd", ".dll" };

        /// <summary>
        /// Dictionary of SDK Identity to the cache file that contains the file information for it.
        /// </summary>
        private ConcurrentDictionary<string, SDKInfo> _cacheFileForSDKs = new ConcurrentDictionary<string, SDKInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Set of exceptions which were thrown while reading or writing to the cache file, this needs to be thread safe since TPL code will add exceptions into this structure at the same time.
        /// </summary>
        private ConcurrentQueue<string> _exceptions = new ConcurrentQueue<string>();

        /// <summary>
        /// Delegate to get the assembly name
        /// </summary>
        private GetAssemblyName _getAssemblyName;

        /// <summary>
        /// Get the image runtime version from a file
        /// </summary>
        private GetAssemblyRuntimeVersion _getRuntimeVersion;

        /// <summary>
        /// File exists delegate
        /// </summary>
        private FileExists _fileExists;

        /// <summary>
        /// Folder where the cache files are written to
        /// </summary>
        private string _cacheFilePath = Path.GetTempPath();

        /// <summary>
        /// Constructor
        /// </summary>
        public GetSDKReferenceFiles()
        {
            CacheFileFolderPath = Path.GetTempPath();
            LogReferencesList = true;
            LogRedistFilesList = true;
            LogReferenceConflictBetweenSDKsAsWarning = true;
            LogReferenceConflictWithinSDKAsWarning = false;
            LogRedistConflictBetweenSDKsAsWarning = true;
            LogRedistConflictWithinSDKAsWarning = false;
        }

        #region Properties

        /// <summary>
        /// Path where the cache files should be stored
        /// </summary>
        public string CacheFileFolderPath
        {
            get
            {
                return _cacheFilePath;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "CacheFileFolderPath");
                _cacheFilePath = value;
            }
        }

        /// <summary>
        /// Resolved SDK references which we will get the reference assemblies from.
        /// </summary>
        public ITaskItem[] ResolvedSDKReferences
        {
            get
            {
                return _resolvedSDKReferences;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "ResolvedSDKReferences");
                _resolvedSDKReferences = value;
            }
        }

        /// <summary>
        /// Extensions which should be considered reference files, we will look for 
        /// the files in the order they are specified in the array.
        /// </summary>
        public string[] ReferenceExtensions
        {
            get
            {
                return _referenceExtensions;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "ReferenceExtensions");
                _referenceExtensions = value;
            }
        }

        /// <summary>
        /// Should the references found as part of resolving the sdk be logged.
        /// The default is true
        /// </summary>
        public bool LogReferencesList
        {
            get;
            set;
        }

        /// <summary>
        /// Should the redist files found as part of resolving the sdk be logged.
        /// The default is true
        /// </summary>
        public bool LogRedistFilesList
        {
            get;
            set;
        }

        /// <summary>
        /// The targetted SDK identifier.
        /// </summary>
        public string TargetSDKIdentifier
        {
            get;
            set;
        }

        /// <summary>
        /// The targeted SDK version.
        /// </summary>
        public string TargetSDKVersion
        {
            get;
            set;
        }

        /// <summary>
        /// The targetted platform identifier.
        /// </summary>
        public string TargetPlatformIdentifier
        {
            get;
            set;
        }

        /// <summary>
        /// The targeted platform version.
        /// </summary>
        public string TargetPlatformVersion
        {
            get;
            set;
        }

        /// <summary>
        /// Resolved reference items.
        /// </summary>
        [Output]
        public ITaskItem[] References
        {
            get { return _references; }
        }

        /// <summary>
        /// Resolved redist files.
        /// </summary>
        [Output]
        public ITaskItem[] RedistFiles
        {
            get { return _redistFiles; }
        }

        /// <summary>
        /// Files that need to be copied locally, this is the reference assemblies and the xml intellisense files.
        /// </summary>
        [Output]
        public ITaskItem[] CopyLocalFiles
        {
            get { return _copyLocalFiles; }
        }

        /// <summary>
        /// Should conflicts between redist files within an SDK be logged as a message or a warning.
        /// The default is to log them as a message.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDKAs", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "SDKAs", Justification = "SDK and As are two different words")]
        public bool LogRedistConflictWithinSDKAsWarning
        {
            get;
            set;
        }

        /// <summary>
        /// Should conflicts between redist files across different referenced SDKs be logged as a message or a warning.
        /// The default is to log them as a warning.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDKs", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        public bool LogRedistConflictBetweenSDKsAsWarning
        {
            get;
            set;
        }

        /// <summary>
        /// Should conflicts between reference files within an SDK be logged as a message or a warning.
        /// The default is to log them as a message.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDKAs", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "SDKAs", Justification = "SDK and As are two different words")]
        public bool LogReferenceConflictWithinSDKAsWarning
        {
            get;
            set;
        }

        /// <summary>
        /// Should conflicts between reference files across different referenced SDKs be logged as a message or a warning.
        /// The default is to log them as a warning.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SDKs", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        public bool LogReferenceConflictBetweenSDKsAsWarning
        {
            get;
            set;
        }

        /// <summary>
        /// Should we log exceptions which were hit when the cache file is being read and written to
        /// </summary>
        public bool LogCacheFileExceptions
        {
            get;
            set;
        }
        #endregion

        /// <summary>
        /// Execute the task
        /// </summary>
        public override bool Execute()
        {
            return Execute(new GetAssemblyName(AssemblyNameExtension.GetAssemblyNameEx), new GetAssemblyRuntimeVersion(AssemblyInformation.GetRuntimeVersion), new FileExists(FileUtilities.FileExistsNoThrow));
        }

        /// <summary>
        /// Execute the task
        /// </summary>
        internal bool Execute(GetAssemblyName getAssemblyName, GetAssemblyRuntimeVersion getRuntimeVersion, FileExists fileExists)
        {
            _getAssemblyName = getAssemblyName;
            _getRuntimeVersion = getRuntimeVersion;
            _fileExists = fileExists;

            try
            {
                // Filter out all references tagged as RuntimeReferenceOnly 
                IEnumerable<ITaskItem> filteredResolvedSDKReferences = ResolvedSDKReferences.Where(
                    sdkReference => !(MetadataConversionUtilities.TryConvertItemMetadataToBool(sdkReference, "RuntimeReferenceOnly"))
                );

                PopulateReferencesForSDK(filteredResolvedSDKReferences);

                foreach (ITaskItem resolvedSDKReference in filteredResolvedSDKReferences)
                {
                    string sdkName = resolvedSDKReference.GetMetadata("SDKName");
                    string sdkIdentity = resolvedSDKReference.GetMetadata("OriginalItemSpec");
                    string rootDirectory = resolvedSDKReference.ItemSpec;
                    string targetedConfiguration = resolvedSDKReference.GetMetadata("TargetedSDKConfiguration");
                    string targetedArchitecture = resolvedSDKReference.GetMetadata("TargetedSDKArchitecture");

                    if (targetedConfiguration.Length == 0)
                    {
                        Log.LogErrorWithCodeFromResources("GetSDKReferenceFiles.CannotHaveEmptyTargetConfiguration", resolvedSDKReference.ItemSpec);
                        return false;
                    }

                    if (targetedArchitecture.Length == 0)
                    {
                        Log.LogErrorWithCodeFromResources("GetSDKReferenceFiles.CannotHaveEmptyTargetArchitecture", resolvedSDKReference.ItemSpec);
                        return false;
                    }

                    FindReferences(resolvedSDKReference, sdkIdentity, sdkName, rootDirectory, targetedConfiguration, targetedArchitecture);
                    FindRedistFiles(resolvedSDKReference, sdkIdentity, targetedConfiguration, targetedArchitecture);
                }

                GenerateOutputItems();

                if (_exceptions.Count > 0 && LogCacheFileExceptions)
                {
                    foreach (string exceptionMessage in _exceptions)
                    {
                        Log.LogMessageFromText(exceptionMessage, MessageImportance.High);
                    }
                }
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                Log.LogErrorWithCodeFromResources("GetSDKReferenceFiles.CouldNotGetSDKReferenceFiles", e.Message);
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Find the redist files
        /// </summary>
        private void FindRedistFiles(ITaskItem resolvedSDKReference, string sdkIdentity, string targetedConfiguration, string targetedArchitecture)
        {
            // Gather the redist files, order is important because we want the most specific match of config and architecture to be the file that returns if there is a collision in destination paths
            HashSet<ResolvedRedistFile> resolvedRedistFileSet = new HashSet<ResolvedRedistFile>();
            IList<string> redistPaths = new List<string>();

            if (targetedConfiguration.Length > 0 && targetedArchitecture.Length > 0)
            {
                redistPaths = ToolLocationHelper.GetSDKRedistFolders(resolvedSDKReference.ItemSpec, targetedConfiguration, targetedArchitecture);
            }

            if (LogRedistFilesList)
            {
                foreach (string path in redistPaths)
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "GetSDKReferenceFiles.ExpandRedistFrom", path.Replace(resolvedSDKReference.ItemSpec, String.Empty));
                }
            }

            SDKInfo sdkCacheInfo = null;
            if (_cacheFileForSDKs.TryGetValue(sdkIdentity, out sdkCacheInfo) && sdkCacheInfo != null)
            {
                foreach (string path in redistPaths)
                {
                    GatherRedistFiles(resolvedRedistFileSet, resolvedSDKReference, path, sdkCacheInfo);
                }
            }

            // Add the resolved redist files to the master list of resolved redist files also log the fact we have found them.
            foreach (ResolvedRedistFile redist in resolvedRedistFileSet)
            {
                bool success = _resolveRedistFiles.Add(redist);

                if (success)
                {
                    if (LogRedistFilesList)
                    {
                        Log.LogMessageFromResources("GetSDKReferenceFiles.AddingRedistFile", redist.RedistFile.Replace(redist.SDKReferenceItem.ItemSpec, String.Empty), redist.TargetPath);
                    }
                }
                else
                {
                    ResolvedRedistFile winner = _resolveRedistFiles.First<ResolvedRedistFile>(x => x.Equals(redist));

                    if (!LogRedistConflictBetweenSDKsAsWarning)
                    {
                        Log.LogMessageFromResources("GetSDKReferenceFiles.ConflictRedistDifferentSDK", winner.TargetPath, winner.SDKReferenceItem.GetMetadata("OriginalItemSpec"), redist.SDKReferenceItem.GetMetadata("OriginalItemSpec"), winner.RedistFile, redist.RedistFile);
                    }
                    else
                    {
                        string message = ResourceUtilities.FormatResourceString("GetSDKReferenceFiles.ConflictRedistDifferentSDK", winner.TargetPath, winner.SDKReferenceItem.GetMetadata("OriginalItemSpec"), redist.SDKReferenceItem.GetMetadata("OriginalItemSpec"), winner.RedistFile, redist.RedistFile);
                        Log.LogWarningWithCodeFromResources("GetSDKReferenceFiles.ConflictBetweenFiles", message);
                    }
                }
            }
        }

        /// <summary>
        /// Find references for the sdk
        /// </summary>
        private void FindReferences(ITaskItem resolvedSDKReference, string sdkIdentity, string sdkName, string rootDirectory, string targetedConfiguration, string targetedArchitecture)
        {
            bool expandSDK = false;

            if (bool.TryParse(resolvedSDKReference.GetMetadata("ExpandReferenceAssemblies"), out expandSDK) && expandSDK)
            {
                Log.LogMessageFromResources("GetSDKReferenceFiles.GetSDKReferences", sdkName, rootDirectory);

                // Gather the reference assemblies, order is important because we want the most specific match of config and architecture to be searched for last
                // so it can overwrite any less specific matches.
                HashSet<ResolvedReferenceAssembly> resolvedReferenceAssemblies = new HashSet<ResolvedReferenceAssembly>();

                // If the SDK is manifest driven we want to grab them from the ApiContracts in the manifest if possible- will only happen if TargetSdk is identified
                string[] manifestReferencePaths = this.GetReferencePathsFromManifest(resolvedSDKReference);

                if (manifestReferencePaths != null && manifestReferencePaths.Length > 0)
                {
                    // Found ApiContract references, use those
                    foreach (string manifestReferencePath in manifestReferencePaths)
                    {
                        resolvedReferenceAssemblies.Add(new ResolvedReferenceAssembly(resolvedSDKReference, manifestReferencePath));
                    }
                }
                else if (targetedConfiguration.Length > 0 && targetedArchitecture.Length > 0)
                {
                    // Couldn't find any valid ApiContracts, look up references the traditional way
                    IList<string> referencePaths = new List<string>();
                    referencePaths = ToolLocationHelper.GetSDKReferenceFolders(resolvedSDKReference.ItemSpec, targetedConfiguration, targetedArchitecture);

                    if (LogReferencesList)
                    {
                        foreach (string path in referencePaths)
                        {
                            Log.LogMessageFromResources(MessageImportance.Low, "GetSDKReferenceFiles.ExpandReferencesFrom", path.Replace(resolvedSDKReference.ItemSpec, String.Empty));
                        }
                    }

                    SDKInfo sdkCacheInfo = null;
                    if (_cacheFileForSDKs.TryGetValue(sdkIdentity, out sdkCacheInfo) && sdkCacheInfo != null)
                    {
                        foreach (string path in referencePaths)
                        {
                            GatherReferenceAssemblies(resolvedReferenceAssemblies, resolvedSDKReference, path, sdkCacheInfo);
                        }
                    }
                }

                // Add the resolved references to the master list of resolved assemblies also log the fact we have found them.
                foreach (ResolvedReferenceAssembly reference in resolvedReferenceAssemblies)
                {
                    bool success = _resolvedReferences.Add(reference);
                    if (success)
                    {
                        if (LogReferencesList)
                        {
                            Log.LogMessageFromResources("GetSDKReferenceFiles.AddingReference", reference.AssemblyLocation.Replace(reference.SDKReferenceItem.ItemSpec, String.Empty));
                        }
                    }
                    else
                    {
                        // Multiple extension SDKs can reference the exact same WinMD now. If the assembly path is exactly the same go ahead and keep the first silently.
                        // (The normal matching is by filename ONLY)
                        if (_resolvedReferences.Any(x => String.Equals(x.AssemblyLocation, reference.AssemblyLocation, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        ResolvedReferenceAssembly winner = _resolvedReferences.First<ResolvedReferenceAssembly>(x => x.Equals(reference));

                        if (!LogReferenceConflictBetweenSDKsAsWarning)
                        {
                            Log.LogMessageFromResources("GetSDKReferenceFiles.ConflictReferenceDifferentSDK", winner.SDKReferenceItem.GetMetadata("OriginalItemSpec"), reference.SDKReferenceItem.GetMetadata("OriginalItemSpec"), winner.AssemblyLocation, reference.AssemblyLocation);
                        }
                        else
                        {
                            string message = ResourceUtilities.FormatResourceString("GetSDKReferenceFiles.ConflictReferenceDifferentSDK", winner.SDKReferenceItem.GetMetadata("OriginalItemSpec"), reference.SDKReferenceItem.GetMetadata("OriginalItemSpec"), winner.AssemblyLocation, reference.AssemblyLocation);
                            Log.LogWarningWithCodeFromResources("GetSDKReferenceFiles.ConflictBetweenFiles", message);
                        }
                    }
                }
            }

            if (!expandSDK)
            {
                Log.LogMessageFromResources("GetSDKReferenceFiles.NotExpanding", sdkName);
            }
        }

        /// <summary>
        /// Generate the output groups
        /// </summary>
        private void GenerateOutputItems()
        {
            List<ITaskItem> resolvedReferenceAssemblies = new List<ITaskItem>();
            List<ITaskItem> copyLocalReferenceAssemblies = new List<ITaskItem>();
            List<ITaskItem> redistReferenceItems = new List<ITaskItem>();

            foreach (ResolvedReferenceAssembly reference in _resolvedReferences)
            {
                ITaskItem outputItem = new TaskItem(reference.AssemblyLocation);
                resolvedReferenceAssemblies.Add(outputItem);

                if (outputItem.GetMetadata(ItemMetadataNames.msbuildReferenceSourceTarget).Length == 0)
                {
                    outputItem.SetMetadata(ItemMetadataNames.msbuildReferenceSourceTarget, "ExpandSDKReference");
                }

                // Mark the two pieces of metadata with the SDK name
                outputItem.SetMetadata(ItemMetadataNames.msbuildReferenceFromSDK, reference.SDKReferenceItem.GetMetadata("OriginalItemSpec"));
                outputItem.SetMetadata(ItemMetadataNames.msbuildReferenceGrouping, reference.SDKReferenceItem.GetMetadata("OriginalItemSpec"));
                outputItem.SetMetadata(ItemMetadataNames.msbuildReferenceGroupingDisplayName, reference.SDKReferenceItem.GetMetadata("DisplayName"));

                string sdkIdentity = reference.SDKReferenceItem.GetMetadata("OriginalItemSpec");
                outputItem.SetMetadata("OriginalItemSpec", sdkIdentity);
                outputItem.SetMetadata("SDKRootPath", reference.SDKReferenceItem.ItemSpec);
                outputItem.SetMetadata("ResolvedFrom", "GetSDKReferenceFiles");

                SDKInfo sdkInfo = null;
                if (_cacheFileForSDKs.TryGetValue(sdkIdentity, out sdkInfo) && sdkInfo != null)
                {
                    SdkReferenceInfo referenceInfo = null;
                    if (sdkInfo.PathToReferenceMetadata != null && sdkInfo.PathToReferenceMetadata.TryGetValue(reference.AssemblyLocation, out referenceInfo))
                    {
                        if (referenceInfo != null && referenceInfo.FusionName != null)
                        {
                            outputItem.SetMetadata(ItemMetadataNames.fusionName, referenceInfo.FusionName);
                        }

                        if (referenceInfo != null && referenceInfo.ImageRuntime != null)
                        {
                            outputItem.SetMetadata(ItemMetadataNames.imageRuntime, referenceInfo.ImageRuntime);
                        }

                        if (referenceInfo != null && referenceInfo.IsWinMD)
                        {
                            outputItem.SetMetadata(ItemMetadataNames.winMDFile, "true");

                            if (referenceInfo.IsManagedWinmd)
                            {
                                outputItem.SetMetadata(ItemMetadataNames.winMDFileType, "Managed");
                            }
                            else
                            {
                                outputItem.SetMetadata(ItemMetadataNames.winMDFileType, "Native");
                            }
                        }
                        else
                        {
                            outputItem.SetMetadata("WinMDFile", "false");
                        }
                    }
                }

                if (reference.CopyLocal)
                {
                    outputItem.SetMetadata("CopyLocal", "true");
                    copyLocalReferenceAssemblies.Add(outputItem);

                    string directory = Path.GetDirectoryName(reference.AssemblyLocation);
                    string fileNameNoExtension = Path.GetFileNameWithoutExtension(reference.AssemblyLocation);
                    string xmlFile = Path.Combine(directory, fileNameNoExtension + ".xml");

                    if (FileUtilities.FileExistsNoThrow(xmlFile))
                    {
                        ITaskItem item = new TaskItem(xmlFile);

                        // Add the related item.
                        copyLocalReferenceAssemblies.Add(item);
                    }
                }
                else
                {
                    outputItem.SetMetadata("CopyLocal", "false");
                }
            }

            resolvedReferenceAssemblies.Sort(TaskItemSpecFilenameComparer.genericComparer);
            copyLocalReferenceAssemblies.Sort(TaskItemSpecFilenameComparer.genericComparer);

            _references = resolvedReferenceAssemblies.ToArray();
            _copyLocalFiles = copyLocalReferenceAssemblies.ToArray();

            foreach (ResolvedRedistFile file in _resolveRedistFiles)
            {
                ITaskItem outputItem = new TaskItem(file.RedistFile);

                if (outputItem.GetMetadata(ItemMetadataNames.msbuildReferenceSourceTarget).Length == 0)
                {
                    outputItem.SetMetadata(ItemMetadataNames.msbuildReferenceSourceTarget, "ExpandSDKReference");
                }

                outputItem.SetMetadata("OriginalItemSpec", file.SDKReferenceItem.GetMetadata("OriginalItemSpec"));
                outputItem.SetMetadata("SDKRootPath", file.SDKReferenceItem.ItemSpec);
                outputItem.SetMetadata("ResolvedFrom", "GetSDKReferenceFiles");

                // Target path for the file
                outputItem.SetMetadata("TargetPath", file.TargetPath);

                // Pri files need to know the root directory of the target path
                if (Path.GetExtension(file.RedistFile).Equals(".PRI", StringComparison.OrdinalIgnoreCase))
                {
                    outputItem.SetMetadata("Root", file.TargetRoot);
                }

                redistReferenceItems.Add(outputItem);
            }

            redistReferenceItems.Sort(TaskItemSpecFilenameComparer.genericComparer);
            _redistFiles = redistReferenceItems.ToArray();
        }

        /// <summary>
        /// Gather the reference assemblies from the referenceassembly directory.
        /// </summary>
        private void GatherReferenceAssemblies(HashSet<ResolvedReferenceAssembly> resolvedFiles, ITaskItem sdkReference, string path, SDKInfo info)
        {
            List<string> referenceFiles = null;
            if (info.DirectoryToFileList != null && info.DirectoryToFileList.TryGetValue(FileUtilities.EnsureNoTrailingSlash(path), out referenceFiles) && referenceFiles != null)
            {
                foreach (var file in referenceFiles)
                {
                    // We only want to find files which match the extensions the user has asked for, this will usually be dll or winmd.
                    bool matchesExtension = false;
                    foreach (var extension in _referenceExtensions)
                    {
                        string fileExtension = Path.GetExtension(file);
                        if (fileExtension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                        {
                            matchesExtension = true;
                            break;
                        }
                    }

                    if (!matchesExtension)
                    {
                        continue;
                    }

                    ResolvedReferenceAssembly resolvedReference = new ResolvedReferenceAssembly(sdkReference, file);
                    bool success = resolvedFiles.Add(resolvedReference);
                    if (!success)
                    {
                        ResolvedReferenceAssembly winner = resolvedFiles.First<ResolvedReferenceAssembly>(x => x.Equals(resolvedReference));

                        if (!LogReferenceConflictWithinSDKAsWarning)
                        {
                            Log.LogMessageFromResources("GetSDKReferenceFiles.ConflictReferenceSameSDK", winner.SDKReferenceItem.GetMetadata("OriginalItemSpec"), winner.AssemblyLocation.Replace(winner.SDKReferenceItem.ItemSpec, String.Empty), resolvedReference.AssemblyLocation.Replace(resolvedReference.SDKReferenceItem.ItemSpec, String.Empty));
                        }
                        else
                        {
                            string message = ResourceUtilities.FormatResourceString("GetSDKReferenceFiles.ConflictReferenceSameSDK", winner.SDKReferenceItem.GetMetadata("OriginalItemSpec"), winner.AssemblyLocation.Replace(winner.SDKReferenceItem.ItemSpec, String.Empty), resolvedReference.AssemblyLocation.Replace(resolvedReference.SDKReferenceItem.ItemSpec, String.Empty));
                            Log.LogWarningWithCodeFromResources("GetSDKReferenceFiles.ConflictBetweenFiles", message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gather the redist files from from the redist directory.
        /// </summary>
        private void GatherRedistFiles(HashSet<ResolvedRedistFile> resolvedRedistFiles, ITaskItem sdkReference, string redistFilePath, SDKInfo info)
        {
            bool copyRedist = MetadataConversionUtilities.TryConvertItemMetadataToBool(sdkReference, "CopyRedist");
            if (copyRedist)
            {
                foreach (KeyValuePair<string, List<string>> directoryToFileList in info.DirectoryToFileList)
                {
                    // Add a trailing slash to ensure we don't match the start of a platform (e.g. ...\ARM matching ...\ARM64)
                    if (FileUtilities.EnsureTrailingSlash(directoryToFileList.Key).StartsWith(FileUtilities.EnsureTrailingSlash(redistFilePath), StringComparison.OrdinalIgnoreCase))
                    {
                        List<string> redistFiles = directoryToFileList.Value;
                        string targetPathRoot = sdkReference.GetMetadata("CopyRedistToSubDirectory");

                        foreach (var file in redistFiles)
                        {
                            string relativeToBase = FileUtilities.MakeRelative(redistFilePath, file);
                            string targetPath = Path.Combine(targetPathRoot, relativeToBase);

                            ResolvedRedistFile redistFile = new ResolvedRedistFile(sdkReference, file, targetPath, targetPathRoot);
                            if (!resolvedRedistFiles.Add(redistFile))
                            {
                                ResolvedRedistFile winner = resolvedRedistFiles.First<ResolvedRedistFile>(x => x.Equals(redistFile));

                                if (!LogRedistConflictWithinSDKAsWarning)
                                {
                                    Log.LogMessageFromResources("GetSDKReferenceFiles.ConflictRedistSameSDK", redistFile.TargetPath, redistFile.SDKReferenceItem.GetMetadata("OriginalItemSpec"), winner.RedistFile.Replace(redistFile.SDKReferenceItem.ItemSpec, String.Empty), redistFile.RedistFile.Replace(redistFile.SDKReferenceItem.ItemSpec, String.Empty));
                                }
                                else
                                {
                                    string message = ResourceUtilities.FormatResourceString("GetSDKReferenceFiles.ConflictRedistSameSDK", redistFile.TargetPath, redistFile.SDKReferenceItem.GetMetadata("OriginalItemSpec"), winner.RedistFile.Replace(redistFile.SDKReferenceItem.ItemSpec, String.Empty), redistFile.RedistFile.Replace(redistFile.SDKReferenceItem.ItemSpec, String.Empty));
                                    Log.LogWarningWithCodeFromResources("GetSDKReferenceFiles.ConflictBetweenFiles", message);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gather the contents of all of the SDK into a cache file and save it to disk.
        /// </summary>
        private void PopulateReferencesForSDK(IEnumerable<ITaskItem> sdks)
        {
            SDKFilesCache sdkFilesCache = new SDKFilesCache(_exceptions, _cacheFilePath, _getAssemblyName, _getRuntimeVersion, _fileExists);

            // Go through each sdk which has been resolved in this project
            foreach (ITaskItem sdk in sdks)
            {
                string sdkIdentity = sdk.GetMetadata("OriginalItemSpec");
                ErrorUtilities.VerifyThrowArgument(sdkIdentity.Length != 0, "GetSDKReferenceFiles.NoOriginalItemSpec", sdk.ItemSpec);
                string sdkRoot = sdk.ItemSpec;

                // Try and get the cache file for this SDK if it already exists
                SDKInfo info = sdkFilesCache.LoadAssemblyListFromCacheFile(sdkIdentity, sdkRoot);

                if (info == null || !sdkFilesCache.IsAssemblyListCacheFileUpToDate(sdkIdentity, sdkRoot, _cacheFilePath))
                {
                    info = sdkFilesCache.GetCacheFileInfoFromSDK(sdkIdentity, sdkRoot, this.GetReferencePathsFromManifest(sdk));

                    // On a background thread save the file to disk
                    SaveContext saveContext = new SaveContext(sdkIdentity, sdkRoot, info);
                    ThreadPool.QueueUserWorkItem(new WaitCallback(sdkFilesCache.SaveAssemblyListToCacheFile), saveContext);
                }

                _cacheFileForSDKs.TryAdd(sdkIdentity, info);
            }
        }

        /// <summary>
        /// Get the referenced file names from the SDK's manifest if applicable- may return null.
        /// </summary>
        private string[] GetReferencePathsFromManifest(ITaskItem sdk)
        {
            string[] manifestReferencePaths = null;

            // It is only useful to look if we have a target SDK specified
            if (!String.IsNullOrEmpty(TargetSDKIdentifier) && !String.IsNullOrEmpty(TargetSDKVersion))
            {
                manifestReferencePaths = ToolLocationHelper.GetPlatformOrFrameworkExtensionSdkReferences(
                    sdk.GetMetadata(GetInstalledSDKLocations.SDKNameMetadataName),
                    TargetSDKIdentifier,
                    TargetSDKVersion,
                    sdk.GetMetadata(GetInstalledSDKLocations.DirectoryRootsMetadataName),
                    sdk.GetMetadata(GetInstalledSDKLocations.ExtensionDirectoryRootsMetadataName),
                    sdk.GetMetadata(GetInstalledSDKLocations.RegistryRootMetadataName),
                    TargetPlatformIdentifier,
                    TargetPlatformVersion);
            }

            return manifestReferencePaths;
        }

        /// <summary>
        /// Class which represents a resolved reference assembly
        /// </summary>
        private class ResolvedReferenceAssembly : IEquatable<ResolvedReferenceAssembly>
        {
            /// <summary>
            ///  Is the reference copy local
            /// </summary>
            private bool _copyLocal = false;

            /// <summary>
            /// Constructor
            /// </summary>
            public ResolvedReferenceAssembly(ITaskItem sdkReferenceItem, string assemblyLocation)
            {
                FileName = Path.GetFileNameWithoutExtension(assemblyLocation);
                AssemblyLocation = assemblyLocation;
                bool.TryParse(sdkReferenceItem.GetMetadata("CopyLocalExpandedReferenceAssemblies"), out _copyLocal);
                SDKReferenceItem = sdkReferenceItem;
            }

            /// <summary>
            ///  What is the file name
            /// </summary>
            public string FileName
            {
                get;
                private set;
            }

            /// <summary>
            /// What is the location of the assembly on disk.
            /// </summary>
            public string AssemblyLocation
            {
                get;
                private set;
            }

            /// <summary>
            /// Is the assembly copy local or not.
            /// </summary>
            public bool CopyLocal
            {
                get
                {
                    return _copyLocal;
                }
            }

            /// <summary>
            /// Original resolved SDK reference item passed in.
            /// </summary>
            public ITaskItem SDKReferenceItem
            {
                get;
                private set;
            }

            /// <summary>
            /// Override object equals to use the equals redist in this object.
            /// </summary>
            public override bool Equals(object obj)
            {
                ResolvedReferenceAssembly reference = obj as ResolvedReferenceAssembly;
                if (reference == null)
                {
                    return false;
                }

                return Equals(reference);
            }

            /// <summary>
            /// Override get hash code
            /// </summary>
            public override int GetHashCode()
            {
                return FileName.GetHashCode();
            }

            /// <summary>
            /// Are two resolved references items Equal
            /// </summary>
            public bool Equals(ResolvedReferenceAssembly other)
            {
                if (other == null)
                {
                    return false;
                }

                if (Object.ReferenceEquals(other, this))
                {
                    return true;
                }

                // We only care about the file name and not the path because if they have the same file name but different paths then they will likely contain
                // the same namespaces and the compiler does not like to have two references with the same namespace passed at once without aliasing and 
                // we have no way to do aliasing per assembly since we are grabbing a bunch of files at once.)
                return String.Equals(this.FileName, other.FileName, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Class which represents a resolved redist file
        /// </summary>
        private class ResolvedRedistFile : IEquatable<ResolvedRedistFile>
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public ResolvedRedistFile(ITaskItem sdkReferenceItem, string redistFile, string targetPath, string targetRoot)
            {
                RedistFile = redistFile;
                TargetPath = targetPath;
                TargetRoot = targetRoot;
                SDKReferenceItem = sdkReferenceItem;
            }

            /// <summary>
            ///  What is the file name
            /// </summary>
            public string RedistFile
            {
                get;
                private set;
            }

            /// <summary>
            /// What is the targetPath for the redist file.
            /// </summary>
            public string TargetPath
            {
                get;
                private set;
            }

            /// <summary>
            /// What is the root directory of the target path
            /// </summary>
            public string TargetRoot
            {
                get;
                private set;
            }

            /// <summary>
            /// Original resolved SDK reference item passed in.
            /// </summary>
            public ITaskItem SDKReferenceItem
            {
                get;
                private set;
            }

            /// <summary>
            /// Override object equals to use the equals redist in this object.
            /// </summary>
            public override bool Equals(object obj)
            {
                ResolvedReferenceAssembly reference = obj as ResolvedReferenceAssembly;
                if (reference == null)
                {
                    return false;
                }

                return Equals(reference);
            }

            /// <summary>
            /// Override get hash code
            /// </summary>
            public override int GetHashCode()
            {
                return TargetPath.GetHashCode();
            }

            /// <summary>
            /// Are two resolved references items Equal
            /// </summary>
            public bool Equals(ResolvedRedistFile other)
            {
                if (other == null)
                {
                    return false;
                }

                if (Object.ReferenceEquals(other, this))
                {
                    return true;
                }

                // We only care about the target path since that is the location relative to the package root where the redist file
                // will be copied.
                return String.Equals(this.TargetPath, other.TargetPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        #region Cache Serialization

        /// <summary>
        /// Methods which are used to save and read the cache files per sdk from and to disk.
        /// </summary>
        private class SDKFilesCache
        {
            /// <summary>
            ///  Thread-safe queue which contains exceptions throws during cache file reading and writing.
            /// </summary>
            private ConcurrentQueue<string> _exceptionMessages;

            /// <summary>
            /// Delegate to get the assembly name
            /// </summary>
            private GetAssemblyName _getAssemblyName;

            /// <summary>
            /// Get the image runtime version from a afile
            /// </summary>
            private GetAssemblyRuntimeVersion _getRuntimeVersion;

            /// <summary>
            /// File exists delegate
            /// </summary>
            private FileExists _fileExists;

            /// <summary>
            /// Location for the cache files to be written to
            /// </summary>
            private string _cacheFileDirectory;

            /// <summary>
            /// Constructor
            /// </summary>
            internal SDKFilesCache(ConcurrentQueue<string> exceptionQueue, string cacheFileDirectory, GetAssemblyName getAssemblyName, GetAssemblyRuntimeVersion getRuntimeVersion, FileExists fileExists)
            {
                _exceptionMessages = exceptionQueue;
                _cacheFileDirectory = cacheFileDirectory;
                _getAssemblyName = getAssemblyName;
                _getRuntimeVersion = getRuntimeVersion;
                _fileExists = fileExists;
            }

            /// <summary>
            /// Load reference assembly information from the cache file
            /// </summary>
            internal SDKInfo LoadAssemblyListFromCacheFile(string sdkIdentity, string sdkRoot)
            {
                var cacheFile = Directory.GetFiles(_cacheFileDirectory, GetCacheFileName(sdkIdentity, sdkRoot, "*")).FirstOrDefault();

                try
                {
                    if (!string.IsNullOrEmpty(cacheFile))
                    {
                        return SDKInfo.Deserialize(cacheFile);
                    }
                }
                catch (Exception e)
                {
                    if (ExceptionHandling.IsCriticalException(e))
                    {
                        throw;
                    }

                    // Queue up for later logging, does not matter if the file is deleted or not
                    _exceptionMessages.Enqueue(ResourceUtilities.FormatResourceString("GetSDKReferenceFiles.ProblemReadingCacheFile", cacheFile, e.Message));
                }

                return null;
            }

            /// <summary>
            /// Save assembly reference information to the cache file
            /// </summary>
            internal void SaveAssemblyListToCacheFile(object data)
            {
                string referencesCacheFile = String.Empty;
                try
                {
                    SaveContext saveContext = data as SaveContext;
                    SDKInfo cacheFileInfo = saveContext.Assemblies;

                    referencesCacheFile = Path.Combine(_cacheFileDirectory, GetCacheFileName(saveContext.SdkIdentity, saveContext.SdkRoot, cacheFileInfo.Hash.ToString("X", CultureInfo.InvariantCulture)));
                    string[] existingCacheFiles = Directory.GetFiles(_cacheFileDirectory, GetCacheFileName(saveContext.SdkIdentity, saveContext.SdkRoot, "*"));

                    // First delete any existing cache files
                    foreach (string existingCacheFile in existingCacheFiles)
                    {
                        try
                        {
                            File.Delete(existingCacheFile);
                        }
                        catch (Exception e)
                        {
                            if (ExceptionHandling.IsCriticalException(e))
                            {
                                throw;
                            }

                            // Queue up for later logging, does not matter if the file is deleted or not
                            _exceptionMessages.Enqueue(ResourceUtilities.FormatResourceString("GetSDKReferenceFiles.ProblemDeletingCacheFile", existingCacheFile, e.Message));
                        }
                    }

                    BinaryFormatter formatter = new BinaryFormatter();
                    using (FileStream fs = new FileStream(referencesCacheFile, FileMode.Create))
                    {
                        formatter.Serialize(fs, cacheFileInfo);
                    }
                }
                catch (Exception e)
                {
                    if (ExceptionHandling.IsCriticalException(e))
                    {
                        throw;
                    }

                    // Queue up for later logging, does not matter if the cache got written
                    _exceptionMessages.Enqueue(ResourceUtilities.FormatResourceString("GetSDKReferenceFiles.ProblemWritingCacheFile", referencesCacheFile, e.Message));
                }
            }

            /// <summary>
            /// Get references from the paths provided, and populate the provided cache
            /// </summary>
            internal SDKInfo GetCacheFileInfoFromSDK(string sdkIdentity, string sdkRootDirectory, string[] sdkManifestReferences)
            {
                ConcurrentDictionary<string, SdkReferenceInfo> references = new ConcurrentDictionary<string, SdkReferenceInfo>(StringComparer.OrdinalIgnoreCase);
                ConcurrentDictionary<string, List<string>> directoryToFileList = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                List<string> directoriesToHash = new List<string>();

                var referenceDirectories = GetAllReferenceDirectories(sdkRootDirectory);
                var redistDirectories = GetAllRedistDirectories(sdkRootDirectory);

                directoriesToHash.AddRange(referenceDirectories);
                directoriesToHash.AddRange(redistDirectories);

                if (sdkManifestReferences != null && sdkManifestReferences.Length > 0)
                {
                    // Manifest driven- get the info from the known list
                    PopulateReferencesDictionaryFromManifestPaths(directoryToFileList, references, sdkManifestReferences);
                }
                else
                {
                    PopulateReferencesDictionaryFromPaths(directoryToFileList, references, referenceDirectories);
                }

                PopulateRedistDictionaryFromPaths(directoryToFileList, redistDirectories);

                SDKInfo cacheInfo = new SDKInfo(references, directoryToFileList, FileUtilities.GetHexHash(sdkIdentity), FileUtilities.GetPathsHash(directoriesToHash));
                return cacheInfo;
            }

            /// <summary>
            /// Populate an existing assembly dictionary for the given framework moniker utilizing provided manifest reference information
            /// </summary>
            internal void PopulateReferencesDictionaryFromManifestPaths(ConcurrentDictionary<string, List<string>> referencesByDirectory, ConcurrentDictionary<string, SdkReferenceInfo> references, string[] sdkManifestReferences)
            {
                // Sort by directory
                var groupedByDirectory =
                    from reference in sdkManifestReferences
                    group reference by Path.GetDirectoryName(reference);

                foreach (var group in groupedByDirectory)
                {
                    referencesByDirectory.TryAdd(group.Key, group.ToList());
                }

                Parallel.ForEach<string>(sdkManifestReferences, reference => { references.TryAdd(reference, GetSDKReferenceInfo(reference)); });
            }

            /// <summary>
            /// Populate an existing assembly dictionary for the given framework moniker
            /// </summary>
            internal void PopulateReferencesDictionaryFromPaths(ConcurrentDictionary<string, List<string>> referencesByDirectory, ConcurrentDictionary<string, SdkReferenceInfo> references, IEnumerable<string> referenceDirectories)
            {
                // Add each folder to the dictionary along with a list of all of files inside of it
                Parallel.ForEach<string>(
                referenceDirectories,
                path =>
                {
                    List<string> files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).ToList<string>();
                    referencesByDirectory.TryAdd(path, files);

                    Parallel.ForEach<string>(files, filePath => { references.TryAdd(filePath, GetSDKReferenceInfo(filePath)); });
                });
            }

            /// <summary>
            /// Populate an existing assembly dictionary for the given framework moniker
            /// </summary>
            internal void PopulateRedistDictionaryFromPaths(ConcurrentDictionary<string, List<string>> redistFilesByDirectory, IEnumerable<string> redistDirectories)
            {
                // Add each folder to the dictionary along with a list of all of files inside of it
                Parallel.ForEach<string>(
                redistDirectories,
                path =>
                {
                    List<string> files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).ToList<string>();
                    redistFilesByDirectory.TryAdd(path, files);
                });
            }

            /// <summary>
            /// Is the assembly list cache file up to date. 
            /// This is done by comparing the last write time of the cache file to the last write time of the code.
            /// If our code is newer than the last write time of the cache file then there may be some different serialization used so we should say it is out of date and just regenerate it.
            /// </summary>
            internal bool IsAssemblyListCacheFileUpToDate(string sdkIdentity, string sdkRoot, string cacheFileFolder)
            {
                // The hash is the hash of last modified times for the passed in reference paths. A directory gets modified if a file is added, deleted, or modified  inside of the inside of the directory itself (modifications to child folders are not seen however).
                List<string> directoriesToHash = new List<string>();
                directoriesToHash.AddRange(GetAllReferenceDirectories(sdkRoot));
                directoriesToHash.AddRange(GetAllRedistDirectories(sdkRoot));

                int hash = FileUtilities.GetPathsHash(directoriesToHash);
                string referencesCacheFile = Path.Combine(cacheFileFolder, GetCacheFileName(sdkIdentity, sdkRoot, hash.ToString("X", CultureInfo.InvariantCulture)));

                bool upToDate = false;
                DateTime referencesCacheFileLastWriteTimeUtc = File.GetLastWriteTimeUtc(referencesCacheFile);

                string currentAssembly = String.Empty;
                try
                {
                    currentAssembly = Assembly.GetExecutingAssembly().CodeBase;
                    Uri codeBase = new Uri(currentAssembly);
                    DateTime currentCodeLastWriteTime = File.GetLastWriteTimeUtc(codeBase.LocalPath);
                    if (File.Exists(referencesCacheFile) && currentCodeLastWriteTime < referencesCacheFileLastWriteTimeUtc)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    if (ExceptionHandling.IsCriticalException(ex))
                    {
                        throw;
                    }

                    // Queue up for later logging, does not matter if the cache got written
                    _exceptionMessages.Enqueue(ResourceUtilities.FormatResourceString("GetSDKReferenceFiles.ProblemGeneratingHash", currentAssembly, ex.Message));

                    // Don't care why the check failed we will just say the cache is not up to date.
                }

                return upToDate;
            }

            /// <summary>
            /// Generate an SDKReferenceInfo object
            /// </summary>
            internal SdkReferenceInfo GetSDKReferenceInfo(string referencePath)
            {
                string imageRuntimeVersion = null;
                bool isManagedWinMD = false;
                bool isWinMDFile = false;
                string fusionName = null;

                try
                {
                    AssemblyNameExtension assemblyNameExtension = _getAssemblyName(referencePath);
                    if (assemblyNameExtension != null)
                    {
                        AssemblyName assembly = assemblyNameExtension.AssemblyName;
                        isWinMDFile = AssemblyInformation.IsWinMDFile(referencePath, _getRuntimeVersion, _fileExists, out imageRuntimeVersion, out isManagedWinMD);
                        if (assembly != null)
                        {
                            fusionName = assembly.FullName;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (ExceptionHandling.IsCriticalException(e))
                    {
                        throw;
                    }

                    // Queue up for later logging, does not matter if the cache got written
                    _exceptionMessages.Enqueue(ResourceUtilities.FormatResourceString("GetSDKReferenceFiles.ProblemGettingAssemblyMetadata", referencePath, e.Message));
                }

                SdkReferenceInfo referenceInfo = new SdkReferenceInfo(fusionName, imageRuntimeVersion, isWinMDFile, isManagedWinMD);
                return referenceInfo;
            }

            /// <summary>
            /// Generate cache file name from sdkIdentity, sdkRoot and suffixHash.
            /// </summary>
            private static string GetCacheFileName(string sdkIdentity, string sdkRoot, string suffixHash)
            {
                string identityHash = FileUtilities.GetHexHash(sdkIdentity);
                string rootHash = FileUtilities.GetHexHash(sdkRoot);

                return sdkIdentity + ",Set=" + identityHash + "-" + rootHash + ",Hash=" + suffixHash + ".dat";
            }

            /// <summary>
            /// Get all redist subdirectories under the given path
            /// </summary>
            private IEnumerable<string> GetAllRedistDirectories(string sdkRoot)
            {
                string redistPath = Path.Combine(sdkRoot, "Redist");
                if (FileUtilities.DirectoryExistsNoThrow(redistPath))
                {
                    return Directory.GetDirectories(redistPath, "*", SearchOption.AllDirectories).ToList<string>();
                }

                return new string[0];
            }

            /// <summary>
            /// Get all reference subdirectories under the given path
            /// </summary>
            private IEnumerable<string> GetAllReferenceDirectories(string sdkRoot)
            {
                string referencesPath = Path.Combine(sdkRoot, "References");
                if (FileUtilities.DirectoryExistsNoThrow(referencesPath))
                {
                    return Directory.GetDirectories(referencesPath, "*", SearchOption.AllDirectories).ToList<string>();
                }

                return new string[0];
            }
        }

        /// <summary>
        /// Class to contain some identity information about a file in an sdk
        /// </summary>
        [Serializable]
        private class SdkReferenceInfo
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public SdkReferenceInfo(string fusionName, string imageRuntime, bool isWinMD, bool isManagedWinmd)
            {
                this.FusionName = fusionName;
                this.ImageRuntime = imageRuntime;
                this.IsWinMD = isWinMD;
                this.IsManagedWinmd = isManagedWinmd;
            }

            #region Properties
            /// <summary>
            /// The fusionName
            /// </summary>
            public string FusionName
            {
                get;
                private set;
            }

            /// <summary>
            /// Is the file a winmd or not
            /// </summary>
            public bool IsWinMD
            {
                get;
                private set;
            }

            /// <summary>
            /// Is the file a managed winmd or not
            /// </summary>
            public bool IsManagedWinmd
            {
                get;
                private set;
            }

            /// <summary>
            /// What is the imageruntime information on it.
            /// </summary>
            public string ImageRuntime
            {
                get;
                private set;
            }

            #endregion
        }

        /// <summary>
        /// Structure that contains the on disk representation of the SDK in memory
        /// </summary>
        [Serializable]
        private class SDKInfo
        {
            // Current version for serialization. This should be changed when breaking changes
            // are made to this class.
            private const byte CurrentSerializationVersion = 1;

            // Version this instance is serialized with.
            private byte _serializedVersion = CurrentSerializationVersion;

            /// <summary>
            /// Constructor
            /// </summary>
            public SDKInfo(ConcurrentDictionary<string, SdkReferenceInfo> pathToReferenceMetadata, ConcurrentDictionary<string, List<string>> directoryToFileList, string cacheFileSuffix, int cacheHash)
            {
                PathToReferenceMetadata = pathToReferenceMetadata;
                DirectoryToFileList = directoryToFileList;
                Suffix = cacheFileSuffix;
                Hash = cacheHash;
            }

            /// <summary>
            /// A dictionary which maps a file path to a structure that contain some metadata information about that file.
            /// </summary>
            public ConcurrentDictionary<string, SdkReferenceInfo> PathToReferenceMetadata { get; private set; }

            /// <summary>
            /// Dictionary which maps a directory to a list of file names within that directory. This is used to shortcut hitting the disk for the list of files inside of it.
            /// </summary>
            public ConcurrentDictionary<string, List<string>> DirectoryToFileList { get; private set; }

            /// <summary>
            /// Suffix for the cache file
            /// </summary>
            public string Suffix { get; private set; }

            /// <summary>
            /// Hashset
            /// </summary>
            public int Hash { get; private set; }

            public static SDKInfo Deserialize(string cacheFile)
            {
                using (FileStream fs = new FileStream(cacheFile, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    var info = (SDKInfo)formatter.Deserialize(fs);

                    // If the serialization versions don't match, don't use the cache
                    if (info != null && info._serializedVersion != CurrentSerializationVersion)
                        return null;

                    return info;
                }
            }
        }

        /// <summary>
        /// This class represents the context information used by the background cache serialization thread.
        /// </summary>
        private class SaveContext
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public SaveContext(string sdkIdentity, string sdkRoot, SDKInfo assemblies)
            {
                this.SdkIdentity = sdkIdentity;
                this.SdkRoot = sdkRoot;
                this.Assemblies = assemblies;
            }

            /// <summary>
            /// Identity of the sdk
            /// </summary>
            public string SdkIdentity { get; private set; }

            /// <summary>
            /// Root path of the sdk
            /// </summary>
            public string SdkRoot { get; private set; }

            /// <summary>
            /// Assembly metadata information
            /// </summary>
            public SDKInfo Assemblies { get; private set; }
        }
        #endregion
    }
}
