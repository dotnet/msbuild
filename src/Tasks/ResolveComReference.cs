// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if FEATURE_APPDOMAIN

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

// TYPELIBATTR clashes with the one in InteropServices.
using TYPELIBATTR = System.Runtime.InteropServices.ComTypes.TYPELIBATTR;
using UtilitiesProcessorArchitecture = Microsoft.Build.Utilities.ProcessorArchitecture;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Main class for the COM reference resolution task
    /// </summary>
    public sealed partial class ResolveComReference : AppDomainIsolatedTaskExtension, IComReferenceResolver
    {
        #region Properties

        /// <summary>
        /// COM references specified by guid/version/lcid
        /// </summary>
        public ITaskItem[] TypeLibNames { get; set; }

        /// <summary>
        /// COM references specified by type library file path
        /// </summary>
        public ITaskItem[] TypeLibFiles { get; set; }

        /// <summary>
        /// Array of equals-separated pairs of environment
        /// variables that should be passed to the spawned tlbimp.exe and aximp.exe,
        /// in addition to (or selectively overriding) the regular environment block.
        /// </summary>
        public string[] EnvironmentVariables { get; set; }

        /// <summary>
        /// merged array containing typeLibNames and typeLibFiles (internal for unit testing)
        /// </summary>
        internal List<ComReferenceInfo> allProjectRefs;

        /// <summary>
        /// array containing all dependency references
        /// </summary>
        internal List<ComReferenceInfo> allDependencyRefs;

        /// <summary>
        /// the directory wrapper files get generated into
        /// </summary>
        public string WrapperOutputDirectory { get; set; }

        /// <summary>
        /// When set to true, the typelib version will be included in the wrapper name.  Default is false.
        /// </summary>
        public bool IncludeVersionInInteropName { get; set; }

        /// <summary>
        /// source of resolved .NET assemblies - we need this for ActiveX wrappers, since we can't resolve .NET assembly
        /// references ourselves
        /// </summary>
        public ITaskItem[] ResolvedAssemblyReferences { get; set; }

        /// <summary>
        /// container name for public/private keys
        /// </summary>
        public string KeyContainer { get; set; } 

        /// <summary>
        /// file containing public/private keys
        /// </summary>
        public string KeyFile { get; set; }

        /// <summary>
        /// delay sign wrappers?
        /// </summary>
        public bool DelaySign { get; set; }

        /// <summary>
        /// Passes the TypeLibImporterFlags.PreventClassMembers flag to tlb wrapper generation
        /// </summary>
        public bool NoClassMembers { get; set; } 

        /// <summary>
        /// If true, do not log messages or warnings.  Default is false. 
        /// </summary>
        public bool Silent { get; set; }

        /// <summary>
        /// The preferred target processor architecture. Passed to tlbimp.exe /machine flag after translation. 
        /// Should be a member of Microsoft.Build.Utilities.ProcessorArchitecture.
        /// </summary>
        public string TargetProcessorArchitecture
        {
            get => _targetProcessorArchitecture;

            set
            {
                if (UtilitiesProcessorArchitecture.X86.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.X86;
                }
                else if (UtilitiesProcessorArchitecture.MSIL.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.MSIL;
                }
                else if (UtilitiesProcessorArchitecture.AMD64.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.AMD64;
                }
                else if (UtilitiesProcessorArchitecture.IA64.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.IA64;
                }
                else if (UtilitiesProcessorArchitecture.ARM.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _targetProcessorArchitecture = UtilitiesProcessorArchitecture.ARM;
                }
                else
                {
                    _targetProcessorArchitecture = value;
                }
            }
        }

        private string _targetProcessorArchitecture;

        /// <summary>
        /// Property to allow multitargeting of ResolveComReferences:  If true, tlbimp.exe 
        /// from the appropriate target framework will be run out-of-proc to generate
        /// the necessary wrapper assemblies. Aximp is always run out of proc.
        /// </summary>
        public bool ExecuteAsTool { get; set; } = true;

        /// <summary>
        /// paths to found/generated reference wrappers
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedFiles { get; set; }

        /// <summary>
        /// paths to found modules (needed for isolation)
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedModules { get; set; }

        /// <summary>
        /// If ExecuteAsTool is true, this must be set to the SDK 
        /// tools path for the framework version being targeted. 
        /// </summary>
        public string SdkToolsPath { get; set; }

        /// <summary>
        /// Cache file for COM component timestamps. If not present, every run will regenerate all the wrappers.
        /// </summary>
        public string StateFile { get; set; }

        /// <summary>
        /// The project target framework version.
        ///
        /// Default is empty. which means there will be no filtering for the reference based on their target framework.
        /// </summary>
        /// <value></value>
        public string TargetFrameworkVersion { get; set; } = String.Empty;

        private Version _projectTargetFramework;
        
        /// <summary>version 4.0</summary>
        private static readonly Version s_targetFrameworkVersion_40 = new Version("4.0");

        private ResolveComReferenceCache _timestampCache;

        // Cache hashtables for different wrapper types
        private readonly Dictionary<string, ComReferenceWrapperInfo> _cachePia =
            new Dictionary<string, ComReferenceWrapperInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ComReferenceWrapperInfo> _cacheTlb =
            new Dictionary<string, ComReferenceWrapperInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ComReferenceWrapperInfo> _cacheAx =
            new Dictionary<string, ComReferenceWrapperInfo>(StringComparer.OrdinalIgnoreCase);

        // Paths for the out-of-proc tools being used
        private string _aximpPath;
        private string _tlbimpPath;

        #endregion

        #region ITask members

        /// <summary>
        /// Task entry point.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            if (!VerifyAndInitializeInputs())
            {
                return false;
            }

            if (!ComputePathToAxImp() || !ComputePathToTlbImp())
            {
                // unable to compute the path to tlbimp.exe, aximp.exe, or both and that is necessary to 
                // continue forward, so return now.
                return false;
            }

            allProjectRefs = new List<ComReferenceInfo>();
            allDependencyRefs = new List<ComReferenceInfo>();

            _timestampCache = (ResolveComReferenceCache)StateFileBase.DeserializeCache(StateFile, Log, typeof(ResolveComReferenceCache));

            if (_timestampCache == null || (_timestampCache != null && !_timestampCache.ToolPathsMatchCachePaths(_tlbimpPath, _aximpPath)))
            {
                if (!Silent)
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.NotUsingCacheFile", StateFile ?? String.Empty);
                }

                _timestampCache = new ResolveComReferenceCache(_tlbimpPath, _aximpPath);
            }
            else if (!Silent)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.UsingCacheFile", StateFile ?? String.Empty);
            }

            try
            {
                ConvertAttrReferencesToComReferenceInfo(allProjectRefs, TypeLibNames);
                ConvertFileReferencesToComReferenceInfo(allProjectRefs, TypeLibFiles);

                // add missing tlbimp references for aximp ones
                AddMissingTlbReferences();

                // see if we have any typelib name clashes. Ignore the return value - we now remove the conflicting refs
                // and continue (first one wins)
                CheckForConflictingReferences();

                SetFrameworkVersionFromString(TargetFrameworkVersion);

                // Process each task item. If one of them fails we still process the rest of them, but
                // remember that the task should return failure.
                // DESIGN CHANGE: we no longer fail the task when one or more references fail to resolve. 
                // Unless we experience a catastrophic failure, we'll log warnings for those refs and proceed
                // (and return success)
                var moduleList = new List<ITaskItem>();
                var resolvedReferenceList = new List<ITaskItem>();

                var dependencyWalker = new ComDependencyWalker(Marshal.ReleaseComObject);
                bool allReferencesResolvedSuccessfully = true;
                for (int pass = 0; pass < 4; pass++)
                {
                    foreach (ComReferenceInfo projectRefInfo in allProjectRefs)
                    {
                        string wrapperType = projectRefInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.wrapperTool);

                        // first resolve all PIA refs, then regular tlb refs and finally ActiveX refs
                        if ((pass == 0 && ComReferenceTypes.IsPia(wrapperType)) ||
                            (pass == 1 && ComReferenceTypes.IsTlbImp(wrapperType)) ||
                            (pass == 2 && ComReferenceTypes.IsPiaOrTlbImp(wrapperType)) ||
                            (pass == 3 && ComReferenceTypes.IsAxImp(wrapperType)))
                        {
                            try
                            {
                                if (!ResolveReferenceAndAddToList(dependencyWalker, projectRefInfo, resolvedReferenceList, moduleList))
                                {
                                    allReferencesResolvedSuccessfully = false;
                                }
                            }
                            catch (ComReferenceResolutionException)
                            {
                                // problem resolving this reference? continue so that we can display all error messages
                            }
                            catch (StrongNameException)
                            {
                                // key extraction problem? No point in continuing, since all wrappers will hit the same problem.
                                // error message has already been logged
                                return false;
                            }
                            catch (FileLoadException ex)
                            {
                                // This exception is thrown when we try to load a delay signed assembly without disabling
                                // strong name verification first. So print a nice information if we're generating 
                                // delay signed wrappers, otherwise rethrow, since it's an unexpected exception.
                                if (DelaySign)
                                {
                                    Log.LogErrorWithCodeFromResources(null, projectRefInfo.SourceItemSpec, 0, 0, 0, 0, "ResolveComReference.LoadingDelaySignedAssemblyWithStrongNameVerificationEnabled", ex.Message);

                                    // no point in printing the same thing multiple times...
                                    return false;
                                }
                                else
                                {
                                    Debug.Assert(false, "Unexpected exception in ResolveComReference.Execute. " +
                                        "Please log a MSBuild bug specifying the steps to reproduce the problem.");
                                    throw;
                                }
                            }
                            catch (ArgumentException ex)
                            {
                                // This exception is thrown when we try to convert some of the Metadata from the project
                                // file and the conversion fails.  Most likely, the user needs to correct a type in the 
                                // project file.
                                Log.LogErrorWithCodeFromResources("General.InvalidArgument", ex.Message);
                                return false;
                            }
                            catch (SystemException ex)
                            {
                                Log.LogErrorWithCodeFromResources("ResolveComReference.FailedToResolveComReference",
                                    projectRefInfo.attr.guid, projectRefInfo.attr.wMajorVerNum, projectRefInfo.attr.wMinorVerNum,
                                    ex.Message);
                            }
                        }
                    }
                }

                SetCopyLocalToFalseOnGacOrNoPIAAssemblies(resolvedReferenceList, GlobalAssemblyCache.GetGacPath());

                ResolvedModules = moduleList.ToArray();
                ResolvedFiles = resolvedReferenceList.ToArray();

                // The Logs from AxImp and TlbImp aren't part of our log, but if the task failed, it will return false from 
                // GenerateWrapper, which should get passed all the way back up here.  
                return allReferencesResolvedSuccessfully && !Log.HasLoggedErrors;
            }
            finally
            {
                if ((_timestampCache != null) && _timestampCache.Dirty)
                {
                    _timestampCache.SerializeCache(StateFile, Log);
                }

                Cleanup();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Converts the string target framework value to a number.
        /// Accepts both "v" prefixed and no "v" prefixed formats
        /// if format is bad will log a message and return 0.
        /// </summary>
        /// <returns>Target framework version value</returns>
        internal void SetFrameworkVersionFromString(string version)
        {
            Version parsedVersion = null;
            if (!String.IsNullOrEmpty(version))
            {
                parsedVersion = VersionUtilities.ConvertToVersion(version);

                if (parsedVersion == null && !Silent)
                {
                    Log.LogMessageFromResources(MessageImportance.Normal, "ResolveComReference.BadTargetFrameworkFormat", version);
                }
            }

            _projectTargetFramework = parsedVersion;
        }

        /// <summary>
        /// Computes the path to TlbImp.exe for use in logging and for passing to the 
        /// nested TlbImp task.
        /// </summary>
        /// <returns>True if the path is found (or it doesn't matter because we're executing in memory), false otherwise</returns>
        private bool ComputePathToTlbImp()
        {
            _tlbimpPath = null;

            if (String.IsNullOrEmpty(SdkToolsPath))
            {
                _tlbimpPath = GetPathToSDKFileWithCurrentlyTargetedArchitecture("TlbImp.exe", TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest);

                if (null == _tlbimpPath && ExecuteAsTool)
                {
                    Log.LogErrorWithCodeFromResources("General.PlatformSDKFileNotFound", "TlbImp.exe",
                        ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest),
                        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest));
                }
            }
            else
            {
                _tlbimpPath = SdkToolsPathUtility.GeneratePathToTool(SdkToolsPathUtility.FileInfoExists, TargetProcessorArchitecture, SdkToolsPath, "TlbImp.exe", Log, ExecuteAsTool);
            }

            if (null == _tlbimpPath && !ExecuteAsTool)
            {
                // if TlbImp.exe is not installed, just use the filename
                _tlbimpPath = "TlbImp.exe";
                return true;
            }

            if (_tlbimpPath != null)
            {
                _tlbimpPath = Path.GetDirectoryName(_tlbimpPath);
            }

            return _tlbimpPath != null;
        }

        /// <summary>
        /// Computes the path to AxImp.exe for use in logging and for passing to the 
        /// nested AxImp task.
        /// </summary>
        /// <returns>True if the path is found, false otherwise</returns>
        private bool ComputePathToAxImp()
        {
            // We always execute AxImp.exe out of proc
            _aximpPath = null;

            if (String.IsNullOrEmpty(SdkToolsPath))
            {
                // In certain cases -- such as trying to build a Dev10 project on a machine that only has Dev11 installed --
                // it's possible to have ExecuteAsTool set to false (e.g. "use the current CLR") but still have SDKToolsPath
                // be empty (because it's referencing the 7.0A SDK in the registry, which doesn't exist).  In that case, we 
                // want to look for VersionLatest.  However, if ExecuteAsTool is true (default value) and SDKToolsPath is 
                // empty, then we can safely assume that we want to get the 3.5 version of the tool.
                TargetDotNetFrameworkVersion targetAxImpVersion = ExecuteAsTool ? TargetDotNetFrameworkVersion.Version35 : TargetDotNetFrameworkVersion.Latest;

                // We want to use the copy of AxImp corresponding to our targeted architecture if possible.  
                _aximpPath = GetPathToSDKFileWithCurrentlyTargetedArchitecture("AxImp.exe", targetAxImpVersion, VisualStudioVersion.VersionLatest);

                if (null == _aximpPath)
                {
                    Log.LogErrorWithCodeFromResources("General.PlatformSDKFileNotFound", "AxImp.exe",
                        ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(targetAxImpVersion, VisualStudioVersion.VersionLatest),
                        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(targetAxImpVersion, VisualStudioVersion.VersionLatest));
                }
            }
            else
            {
                _aximpPath = SdkToolsPathUtility.GeneratePathToTool(SdkToolsPathUtility.FileInfoExists, TargetProcessorArchitecture, SdkToolsPath, "AxImp.exe", Log, true /* log errors */);
            }

            if (_aximpPath != null)
            {
                _aximpPath = Path.GetDirectoryName(_aximpPath);
            }

            return _aximpPath != null;
        }

        /// <summary>
        /// Try to get the path to the tool in the Windows SDK with the given .NET Framework version and 
        /// of the same architecture as we were currently given for TargetProcessorArchitecture.
        /// </summary>
        private string GetPathToSDKFileWithCurrentlyTargetedArchitecture(string file, TargetDotNetFrameworkVersion targetFrameworkVersion, VisualStudioVersion visualStudioVersion)
        {
            string path = null;

            switch (TargetProcessorArchitecture)
            {
                case UtilitiesProcessorArchitecture.ARM:
                case UtilitiesProcessorArchitecture.X86:
                    path = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile(file, targetFrameworkVersion, visualStudioVersion, DotNetFrameworkArchitecture.Bitness32);
                    break;
                case UtilitiesProcessorArchitecture.AMD64:
                case UtilitiesProcessorArchitecture.IA64:
                    path = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile(file, targetFrameworkVersion, visualStudioVersion, DotNetFrameworkArchitecture.Bitness64);
                    break;
                case UtilitiesProcessorArchitecture.MSIL:
                default:
                    // just go with the default lookup
                    break;
            }

            if (path == null)
            {
                // fall back to the default lookup (current architecture / x86) just in case it's found there ...
                path = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile(file, targetFrameworkVersion, visualStudioVersion);
            }

            return path;
        }

        /// <summary>
        /// Clean various caches and other state that should not be preserved between subsequent runs
        /// </summary>
        private void Cleanup()
        {
            // clear the wrapper caches - since references can change between runs, wrapper objects should not be reused
            _cacheAx.Clear();
            _cachePia.Clear();
            _cacheTlb.Clear();

            // release COM interface pointers for dependency references
            foreach (ComReferenceInfo dependencyRefInfo in allDependencyRefs)
            {
                dependencyRefInfo.ReleaseTypeLibPtr();
            }

            // release COM interface pointers for project references
            foreach (ComReferenceInfo projectRefInfo in allProjectRefs)
            {
                projectRefInfo.ReleaseTypeLibPtr();
            }
        }

        /*
         * Method:  VerifyAndInitializeInputs
         * 
         * Helper method. Verifies the input task items have correct metadata and initializes optional ones with
         * default values if they're not present.
         */
        private bool VerifyAndInitializeInputs()
        {
            if (!string.IsNullOrEmpty(KeyContainer) && !string.IsNullOrEmpty(KeyFile))
            {
                Log.LogErrorWithCodeFromResources("ResolveComReference.CannotSpecifyBothKeyFileAndKeyContainer");
                return false;
            }

            if (DelaySign)
            {
                if (string.IsNullOrEmpty(KeyContainer) && string.IsNullOrEmpty(KeyFile))
                {
                    Log.LogErrorWithCodeFromResources("ResolveComReference.CannotSpecifyDelaySignWithoutEitherKeyFileOrKeyContainer");
                    return false;
                }
            }

            // if no output directory specified, default to the project directory
            if (WrapperOutputDirectory == null)
            {
                WrapperOutputDirectory = String.Empty;
            }

            int typeLibNamesLength = (TypeLibNames == null) ? 0 : TypeLibNames.GetLength(0);
            int typeLibFilesLength = (TypeLibFiles == null) ? 0 : TypeLibFiles.GetLength(0);

            // nothing to do? we cannot tell the difference between not passing in anything and passing in empty list,
            // so let's just exit.
            if (typeLibFilesLength + typeLibNamesLength == 0)
            {
                Log.LogErrorWithCodeFromResources("ResolveComReference.NoComReferencesSpecified");
                return false;
            }

            bool metadataValid = true;

            for (int i = 0; i < typeLibNamesLength; i++)
            {
                // verify the COM reference item contains all the required attributes

                if (!VerifyReferenceMetadataForNameItem(TypeLibNames[i], out string missingMetadata))
                {
                    Log.LogErrorWithCodeFromResources(null, TypeLibNames[i].ItemSpec, 0, 0, 0, 0, "ResolveComReference.MissingOrUnknownComReferenceAttribute", missingMetadata, TypeLibNames[i].ItemSpec);

                    // don't exit immediately... check all the refs and display all errors
                    metadataValid = false;
                }
                else
                {
                    // Initialize optional attributes with default values if they're missing
                    InitializeDefaultMetadataForNameItem(TypeLibNames[i]);
                }
            }

            for (int i = 0; i < typeLibFilesLength; i++)
            {
                // File COM references don't have any required metadata, so no verification necessary here
                // Initialize optional metadata with default values if they're missing
                InitializeDefaultMetadataForFileItem(TypeLibFiles[i]);
            }

            return metadataValid;
        }

        /*
         * Method:  ConvertAttrReferencesToComReferenceInfo
         * 
         * Helper method. Converts TypeLibAttr references to ComReferenceInfo objects.
         * This method cannot fail, since we want to proceed with the task even if some references won't load.
         */
        private void ConvertAttrReferencesToComReferenceInfo(List<ComReferenceInfo> projectRefs, ITaskItem[] typeLibAttrs)
        {
            int typeLibAttrsLength = (typeLibAttrs == null) ? 0 : typeLibAttrs.GetLength(0);

            for (int i = 0; i < typeLibAttrsLength; i++)
            {
                var projectRefInfo = new ComReferenceInfo();

                try
                {
                    if (projectRefInfo.InitializeWithTypeLibAttrs(Log, Silent, TaskItemToTypeLibAttr(typeLibAttrs[i]), typeLibAttrs[i], TargetProcessorArchitecture))
                    {
                        projectRefs.Add(projectRefInfo);
                    }
                    else
                    {
                        projectRefInfo.ReleaseTypeLibPtr();
                    }
                }
                catch (COMException ex)
                {
                    if (!Silent)
                    {
                        Log.LogWarningWithCodeFromResources("ResolveComReference.CannotLoadTypeLibItemSpec", typeLibAttrs[i].ItemSpec, ex.Message);
                    }

                    projectRefInfo.ReleaseTypeLibPtr();
                    // we don't want to fail the task if one of the references is not registered, so just continue
                }
            }
        }

        /*
         * Method:  ConvertFileReferencesToComReferenceInfo
         * 
         * Helper method. Converts TypeLibFiles references to ComReferenceInfo objects
         * This method cannot fail, since we want to proceed with the task even if some references won't load.
         */
        private void ConvertFileReferencesToComReferenceInfo(List<ComReferenceInfo> projectRefs, ITaskItem[] tlbFiles)
        {
            int tlbFilesLength = (tlbFiles == null) ? 0 : tlbFiles.GetLength(0);

            for (int i = 0; i < tlbFilesLength; i++)
            {
                string refPath = tlbFiles[i].ItemSpec;
                if (!Path.IsPathRooted(refPath))
                {
                    refPath = Path.Combine(Directory.GetCurrentDirectory(), refPath);
                }

                var projectRefInfo = new ComReferenceInfo();

                try
                {
                    if (projectRefInfo.InitializeWithPath(Log, Silent, refPath, tlbFiles[i], TargetProcessorArchitecture))
                    {
                        projectRefs.Add(projectRefInfo);
                    }
                    else
                    {
                        projectRefInfo.ReleaseTypeLibPtr();
                    }
                }
                catch (COMException ex)
                {
                    if (!Silent)
                    {
                        Log.LogWarningWithCodeFromResources("ResolveComReference.CannotLoadTypeLibItemSpec", tlbFiles[i].ItemSpec, ex.Message);
                    }

                    projectRefInfo.ReleaseTypeLibPtr();
                    // we don't want to fail the task if one of the references is not registered, so just continue
                }
            }
        }

        /// <summary>
        /// Every ActiveX reference (aximp) requires a corresponding tlbimp reference. If the tlbimp reference is
        /// missing from the project file we pretend it's there to save the user some useless typing.
        /// </summary>
        internal void AddMissingTlbReferences()
        {
            var newProjectRefs = new List<ComReferenceInfo>();

            foreach (ComReferenceInfo axRefInfo in allProjectRefs)
            {
                // Try to find the matching tlbimp/pia reference for each aximp reference
                // There is an obscured case in this algorithm: there may be more than one match. Arbitrarily chooses the first.
                if (ComReferenceTypes.IsAxImp(axRefInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.wrapperTool)))
                {
                    bool matchingTlbRefPresent = false;

                    foreach (ComReferenceInfo tlbRefInfo in allProjectRefs)
                    {
                        string tlbWrapperType = tlbRefInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.wrapperTool);

                        if (ComReferenceTypes.IsTlbImp(tlbWrapperType) || ComReferenceTypes.IsPia(tlbWrapperType) || ComReferenceTypes.IsPiaOrTlbImp(tlbWrapperType))
                        {
                            if (ComReference.AreTypeLibAttrEqual(axRefInfo.attr, tlbRefInfo.attr))
                            {
                                axRefInfo.taskItem.SetMetadata(ComReferenceItemMetadataNames.tlbReferenceName, tlbRefInfo.typeLibName);

                                // Check and demote EmbedInteropTypes to "false" for wrappers of ActiveX controls. The compilers won't embed
                                // the ActiveX control and so will transitively turn this wrapper into a reference as well. We need to know to
                                // make the wrapper CopyLocal=true later so switch to EmbedInteropTypes=false now.
                                string embedInteropTypes = tlbRefInfo.taskItem.GetMetadata(ItemMetadataNames.embedInteropTypes);
                                if (ConversionUtilities.CanConvertStringToBool(embedInteropTypes))
                                {
                                    if (ConversionUtilities.ConvertStringToBool(embedInteropTypes))
                                    {
                                        if (!Silent)
                                        {
                                            Log.LogMessageFromResources(MessageImportance.High, "ResolveComReference.TreatingTlbOfActiveXAsNonEmbedded", tlbRefInfo.taskItem.ItemSpec, axRefInfo.taskItem.ItemSpec);
                                        }

                                        tlbRefInfo.taskItem.SetMetadata(ItemMetadataNames.embedInteropTypes, "false");
                                    }
                                }
                                axRefInfo.primaryOfAxImpRef = tlbRefInfo;
                                matchingTlbRefPresent = true;
                                break;
                            }
                        }
                    }

                    // add the matching tlbimp ref if not already there
                    if (!matchingTlbRefPresent)
                    {
                        if (!Silent)
                        {
                            Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.AddingMissingTlbReference", axRefInfo.taskItem.ItemSpec);
                        }

                        var newTlbRef = new ComReferenceInfo(axRefInfo);
                        newTlbRef.taskItem.SetMetadata(ComReferenceItemMetadataNames.wrapperTool, ComReferenceTypes.primaryortlbimp);
                        newTlbRef.taskItem.SetMetadata(ItemMetadataNames.embedInteropTypes, "false");
                        axRefInfo.primaryOfAxImpRef = newTlbRef;

                        newProjectRefs.Add(newTlbRef);
                        axRefInfo.taskItem.SetMetadata(ComReferenceItemMetadataNames.tlbReferenceName, newTlbRef.typeLibName);
                    }
                }
            }

            foreach (ComReferenceInfo refInfo in newProjectRefs)
            {
                allProjectRefs.Add(refInfo);
            }
        }

        /// <summary>
        /// Resolves the COM reference, and adds it to the appropriate item list.
        /// </summary>
        private bool ResolveReferenceAndAddToList
            (
            ComDependencyWalker dependencyWalker,
            ComReferenceInfo projectRefInfo,
            List<ITaskItem> resolvedReferenceList,
            List<ITaskItem> moduleList
            )
        {
            if (ResolveReference(dependencyWalker, projectRefInfo, WrapperOutputDirectory, out ITaskItem referencePath))
            {
                resolvedReferenceList.Add(referencePath);

                bool isolated = MetadataConversionUtilities.TryConvertItemMetadataToBool(projectRefInfo.taskItem, "Isolated", out bool metadataFound);

                if (metadataFound && isolated)
                {
                    string modulePath = projectRefInfo.strippedTypeLibPath;
                    if (modulePath != null)
                    {
                        ITaskItem moduleItem = new TaskItem(modulePath);
                        moduleItem.SetMetadata("Name", projectRefInfo.taskItem.ItemSpec);
                        moduleList.Add(moduleItem);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        /*
         * Method:  ResolveReference
         * 
         * Helper COM resolution method. Creates an appropriate helper class for the given tool and calls 
         * the Resolve method on it.
         */
        internal bool ResolveReference(ComDependencyWalker dependencyWalker, ComReferenceInfo referenceInfo, string outputDirectory, out ITaskItem referencePathItem)
        {
            if (referenceInfo.referencePathItem == null)
            {
                if (!Silent)
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.Resolving", referenceInfo.taskItem.ItemSpec, referenceInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.wrapperTool));
                }

                List<string> dependencyPaths = ScanAndResolveAllDependencies(dependencyWalker, referenceInfo);

                referenceInfo.dependentWrapperPaths = dependencyPaths;
                referencePathItem = new TaskItem();
                referenceInfo.referencePathItem = referencePathItem;

                if (ResolveComClassicReference(referenceInfo, outputDirectory,
                    referenceInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.wrapperTool),
                    referenceInfo.taskItem.ItemSpec, true, referenceInfo.dependentWrapperPaths, out ComReferenceWrapperInfo wrapperInfo))
                {
                    referencePathItem.ItemSpec = wrapperInfo.path;
                    referenceInfo.taskItem.CopyMetadataTo(referencePathItem);

                    string fusionName = AssemblyName.GetAssemblyName(wrapperInfo.path).FullName;
                    referencePathItem.SetMetadata(ItemMetadataNames.fusionName, fusionName);

                    if (!Silent)
                    {
                        Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.ResolvedReference", referenceInfo.taskItem.ItemSpec, wrapperInfo.path);
                    }

                    return true;
                }

                if (!Silent)
                {
                    Log.LogWarningWithCodeFromResources("ResolveComReference.CannotFindWrapperForTypeLib", referenceInfo.taskItem.ItemSpec);
                }

                return false;
            }
            else
            {
                bool successfullyResolved = !String.IsNullOrEmpty(referenceInfo.referencePathItem.ItemSpec);
                referencePathItem = referenceInfo.referencePathItem;

                return successfullyResolved;
            }
        }

        /*
         * Method:  IsExistingProjectReference
         * 
         * If given typelib attributes are already a project reference, return that reference.
         */
        internal bool IsExistingProjectReference(TYPELIBATTR typeLibAttr, string neededRefType, out ComReferenceInfo referenceInfo)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                // First PIAs, then tlbimps, then aximp
                // Only execute each pass if the needed ref type matches or is null
                // Important: the condition for Ax wrapper is different, since we don't want to find Ax references
                // for unknown wrapper types - "unknown" wrapper means we're only looking for a tlbimp or a primary reference
                if ((pass == 0 && (ComReferenceTypes.IsPia(neededRefType) || neededRefType == null)) ||
                    (pass == 1 && (ComReferenceTypes.IsTlbImp(neededRefType) || neededRefType == null)) ||
                    (pass == 2 && (ComReferenceTypes.IsAxImp(neededRefType))))
                {
                    foreach (ComReferenceInfo projectRefInfo in allProjectRefs)
                    {
                        string wrapperType = projectRefInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.wrapperTool);

                        // First PIAs, then tlbimps, then aximp
                        if ((pass == 0 && ComReferenceTypes.IsPia(wrapperType)) ||
                            (pass == 1 && ComReferenceTypes.IsTlbImp(wrapperType)) ||
                            (pass == 2 && ComReferenceTypes.IsAxImp(wrapperType)))
                        {
                            // found it? return the existing reference
                            if (ComReference.AreTypeLibAttrEqual(projectRefInfo.attr, typeLibAttr))
                            {
                                referenceInfo = projectRefInfo;
                                return true;
                            }
                        }
                    }
                }
            }

            referenceInfo = null;
            return false;
        }

        /*
         * Method:  IsExistingDependencyReference
         * 
         * If given typelib attributes are already a dependency reference (that is, was already
         * processed) return that reference.
         */
        internal bool IsExistingDependencyReference(TYPELIBATTR typeLibAttr, out ComReferenceInfo referenceInfo)
        {
            foreach (ComReferenceInfo dependencyRefInfo in allDependencyRefs)
            {
                // found it? return the existing reference
                if (ComReference.AreTypeLibAttrEqual(dependencyRefInfo.attr, typeLibAttr))
                {
                    referenceInfo = dependencyRefInfo;
                    return true;
                }
            }

            referenceInfo = null;
            return false;
        }

        /*
         * Method:  ResolveComClassicReference
         * 
         * Resolves a COM classic reference given the type library attributes and the type of wrapper to use.
         * If wrapper type is not specified, this method will first look for an existing reference in the project,
         * fall back to looking for a PIA and finally try to generate a regular tlbimp wrapper.
         */
        internal bool ResolveComClassicReference(ComReferenceInfo referenceInfo, string outputDirectory, string wrapperType, string refName, bool topLevelRef, List<string> dependencyPaths, out ComReferenceWrapperInfo wrapperInfo)
        {
            wrapperInfo = null;

            bool retVal = false;

            // only look for an existing PIA
            if (ComReferenceTypes.IsPia(wrapperType))
            {
                retVal = ResolveComReferencePia(referenceInfo, refName, out wrapperInfo);
            }
            // find/generate a tlb wrapper
            else if (ComReferenceTypes.IsTlbImp(wrapperType))
            {
                retVal = ResolveComReferenceTlb(referenceInfo, outputDirectory, refName, topLevelRef, dependencyPaths, out wrapperInfo);
            }
            // find/generate an Ax wrapper
            else if (ComReferenceTypes.IsAxImp(wrapperType))
            {
                retVal = ResolveComReferenceAx(referenceInfo, outputDirectory, refName, out wrapperInfo);
            }
            // find/generate a pia/tlb wrapper (it's only possible to get here via a callback)
            else if (wrapperType == null || ComReferenceTypes.IsPiaOrTlbImp(wrapperType))
            {
                // if this reference does not exist in the project, try looking for a PIA first
                retVal = ResolveComReferencePia(referenceInfo, refName, out wrapperInfo);
                if (!retVal)
                {
                    // failing that, try a regular tlb wrapper
                    retVal = ResolveComReferenceTlb(referenceInfo, outputDirectory, refName, false /* dependency */, dependencyPaths, out wrapperInfo);
                }
            }
            else
            {
                ErrorUtilities.VerifyThrow(false, "Unknown wrapper type!");
            }
            referenceInfo.resolvedWrapper = wrapperInfo;

            // update the timestamp cache with the timestamp of the component we just processed
            _timestampCache[referenceInfo.strippedTypeLibPath] = File.GetLastWriteTime(referenceInfo.strippedTypeLibPath);

            return retVal;
        }

        /*
         * Method:  ResolveComClassicReference
         * 
         * Resolves a COM classic reference given the type library attributes and the type of wrapper to use.
         * If wrapper type is not specified, this method will first look for an existing reference in the project,
         * fall back to looking for a PIA and finally try to generate a regular tlbimp wrapper.
         *
         * This is the method available for references to call back to resolve their dependencies
         */
        bool IComReferenceResolver.ResolveComClassicReference(TYPELIBATTR typeLibAttr, string outputDirectory, string wrapperType, string refName, out ComReferenceWrapperInfo wrapperInfo)
        {
            // does this reference exist in the project or is it a dependency?
            bool topLevelRef = false;

            wrapperInfo = null;

            // remap the type lib to ADO 2.7 if necessary
            TYPELIBATTR oldAttr = typeLibAttr;

            if (ComReference.RemapAdoTypeLib(Log, Silent, ref typeLibAttr) && !Silent)
            {
                // if successfully remapped the reference to ADO 2.7, notify the user
                Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.RemappingAdoTypeLib", oldAttr.wMajorVerNum, oldAttr.wMinorVerNum);
            }

            // find an existing ref in the project (taking the desired wrapperType into account, if any)
            if (IsExistingProjectReference(typeLibAttr, wrapperType, out ComReferenceInfo referenceInfo))
            {
                // IsExistingProjectReference should not return null... 
                Debug.Assert(referenceInfo != null, "IsExistingProjectReference should not return null");
                topLevelRef = true;
                wrapperType = referenceInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.wrapperTool);
            }
            // was this dependency already processed?
            else if (IsExistingDependencyReference(typeLibAttr, out referenceInfo))
            {
                Debug.Assert(referenceInfo != null, "IsExistingDependencyReference should not return null");

                // we've seen this dependency before, so we should know what its wrapper type is.
                if (wrapperType == null || ComReferenceTypes.IsPiaOrTlbImp(wrapperType))
                {
                    string typeLibKey = ComReference.UniqueKeyFromTypeLibAttr(typeLibAttr);
                    if (_cachePia.ContainsKey(typeLibKey))
                    {
                        wrapperType = ComReferenceTypes.primary;
                    }
                    else if (_cacheTlb.ContainsKey(typeLibKey))
                    {
                        wrapperType = ComReferenceTypes.tlbimp;
                    }
                }
            }
            // if not found anywhere, create a new ComReferenceInfo object and resolve it.
            else
            {
                try
                {
                    referenceInfo = new ComReferenceInfo();

                    if (referenceInfo.InitializeWithTypeLibAttrs(Log, Silent, typeLibAttr, null, TargetProcessorArchitecture))
                    {
                        allDependencyRefs.Add(referenceInfo);
                    }
                    else
                    {
                        referenceInfo.ReleaseTypeLibPtr();
                        return false;
                    }
                }
                catch (COMException ex)
                {
                    if (!Silent)
                    {
                        Log.LogWarningWithCodeFromResources("ResolveComReference.CannotLoadTypeLib", typeLibAttr.guid,
                            typeLibAttr.wMajorVerNum.ToString(CultureInfo.InvariantCulture),
                            typeLibAttr.wMinorVerNum.ToString(CultureInfo.InvariantCulture),
                            ex.Message);
                    }

                    referenceInfo.ReleaseTypeLibPtr();

                    // can't resolve an unregistered and unknown dependency, so return false
                    return false;
                }
            }

            // if we don't have the reference name, use the typelib name
            if (refName == null)
            {
                refName = referenceInfo.typeLibName;
            }

            return ResolveComClassicReference(referenceInfo, outputDirectory, wrapperType, refName, topLevelRef, referenceInfo.dependentWrapperPaths, out wrapperInfo);
        }

        /*
         * Method:  ResolveNetAssemblyReference
         * 
         * Resolves a .NET assembly reference using the list of resolved managed references supplied to the task.
         *
         * This is the method available for references to call back to resolve their dependencies
         */
        bool IComReferenceResolver.ResolveNetAssemblyReference(string assemblyName, out string assemblyPath)
        {
            int commaIndex = assemblyName.IndexOf(',');

            // if we have a strong name, strip off everything but the assembly name
            if (commaIndex != -1)
                assemblyName = assemblyName.Substring(0, commaIndex);

            assemblyName += ".dll";

            for (int i = 0; i < ResolvedAssemblyReferences.GetLength(0); i++)
            {
                if (String.Compare(Path.GetFileName(ResolvedAssemblyReferences[i].ItemSpec), assemblyName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    assemblyPath = ResolvedAssemblyReferences[i].ItemSpec;
                    return true;
                }
            }

            assemblyPath = null;
            return false;
        }

        /*
         * Method:  ResolveComAssemblyReference
         * 
         * Resolves a COM wrapper assembly reference based on the COM references resolved so far. This method is necessary
         * for Ax wrappers only, so all necessary references will be resolved by then (since we resolve them in 
         * the following order: pia, tlbimp, aximp)
         *
         * This is the method available for references to call back to resolve their dependencies
         */
        bool IComReferenceResolver.ResolveComAssemblyReference(string fullAssemblyName, out string assemblyPath)
        {
            var fullAssemblyNameEx = new AssemblyNameExtension(fullAssemblyName);

            foreach (ComReferenceWrapperInfo wrapperInfo in _cachePia.Values)
            {
                // this should not happen, but it would be a non fatal error
                Debug.Assert(wrapperInfo.path != null);
                if (wrapperInfo.path == null)
                {
                    continue;
                }

                // we have already verified all cached wrappers, so we don't expect this methods to throw anything
                var wrapperAssemblyNameEx = new AssemblyNameExtension(AssemblyName.GetAssemblyName(wrapperInfo.path));

                if (fullAssemblyNameEx.Equals(wrapperAssemblyNameEx))
                {
                    assemblyPath = wrapperInfo.path;
                    return true;
                }
                // The PIA might have been redirected, so check its original assembly name too
                else if (fullAssemblyNameEx.Equals(wrapperInfo.originalPiaName))
                {
                    assemblyPath = wrapperInfo.path;
                    return true;
                }
            }

            foreach (ComReferenceWrapperInfo wrapperInfo in _cacheTlb.Values)
            {
                // temporary wrapper? skip it.
                if (wrapperInfo.path == null)
                {
                    continue;
                }

                // we have already verified all cached wrappers, so we don't expect this methods to throw anything
                var wrapperAssemblyNameEx = new AssemblyNameExtension(AssemblyName.GetAssemblyName(wrapperInfo.path));
                if (fullAssemblyNameEx.Equals(wrapperAssemblyNameEx))
                {
                    assemblyPath = wrapperInfo.path;
                    return true;
                }
            }

            foreach (ComReferenceWrapperInfo wrapperInfo in _cacheAx.Values)
            {
                // this should not happen, but it would be a non fatal error
                Debug.Assert(wrapperInfo.path != null);
                if (wrapperInfo.path == null)
                {
                    continue;
                }

                // we have already verified all cached wrappers, so we don't expect this methods to throw anything
                var wrapperAssemblyNameEx = new AssemblyNameExtension(AssemblyName.GetAssemblyName(wrapperInfo.path));
                if (fullAssemblyNameEx.Equals(wrapperAssemblyNameEx))
                {
                    assemblyPath = wrapperInfo.path;
                    return true;
                }
            }

            assemblyPath = null;
            return false;
        }

        /// <summary>
        /// Helper function - resolves a PIA COM classic reference given the type library attributes.
        /// </summary>
        /// <param name="referenceInfo">Information about the reference to be resolved</param>
        /// <param name="refName">Name of reference</param>
        /// <param name="wrapperInfo">Information about wrapper locations</param>
        /// <returns>True if the reference was already found or successfully generated, false otherwise.</returns>
        internal bool ResolveComReferencePia(ComReferenceInfo referenceInfo, string refName, out ComReferenceWrapperInfo wrapperInfo)
        {
            string typeLibKey = ComReference.UniqueKeyFromTypeLibAttr(referenceInfo.attr);

            // look in the PIA cache first
            if (_cachePia.TryGetValue(typeLibKey, out wrapperInfo))
            {
                return true;
            }

            try
            {
                // if not in the cache, we have no choice but to go looking for the PIA
                var reference = new PiaReference(Log, Silent, referenceInfo, refName);

                // if not found, fail (we do not fall back to tlbimp wrappers if we're looking specifically for a PIA)
                if (!reference.FindExistingWrapper(out wrapperInfo, _timestampCache[referenceInfo.strippedTypeLibPath]))
                {
                    return false;
                }

                // if found, add it to the PIA cache
                _cachePia.Add(typeLibKey, wrapperInfo);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Return the set of item specs for the resolved assembly references. 
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<string> GetResolvedAssemblyReferenceItemSpecs()
        {
            return (ResolvedAssemblyReferences == null) ? Array.Empty<string>(): ResolvedAssemblyReferences.Select(rar => rar.ItemSpec);
        }

        /// <summary>
        /// Helper function - resolves a regular tlb COM classic reference given the type library attributes.
        /// </summary>
        /// <param name="referenceInfo">Information about the reference to be resolved</param>
        /// <param name="outputDirectory">Directory the interop DLL should be written to</param>
        /// <param name="refName">Name of reference</param>
        /// <param name="topLevelRef">True if this is a top-level reference</param>
        /// <param name="wrapperInfo">Information about wrapper locations</param>
        /// <returns>True if the reference was already found or successfully generated, false otherwise.</returns>
        internal bool ResolveComReferenceTlb(ComReferenceInfo referenceInfo, string outputDirectory, string refName, bool topLevelRef, List<string> dependencyPaths, out ComReferenceWrapperInfo wrapperInfo)
        {
            string typeLibKey = ComReference.UniqueKeyFromTypeLibAttr(referenceInfo.attr);

            // look in the TLB cache first
            if (_cacheTlb.TryGetValue(typeLibKey, out wrapperInfo))
            {
                return true;
            }

            // is it a temporary wrapper?
            bool isTemporary = false;

            // no top level (included in the project) refs can have temporary wrappers
            if (!topLevelRef)
            {
                // wrapper is temporary if there's a top level tlb reference with the same typelib name, but different attributes
                foreach (ComReferenceInfo projectRefInfo in allProjectRefs)
                {
                    if (ComReferenceTypes.IsTlbImp(projectRefInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.wrapperTool)))
                    {
                        // conflicting typelib names for different typelibs? generate a temporary wrapper
                        if (!ComReference.AreTypeLibAttrEqual(referenceInfo.attr, projectRefInfo.attr) &&
                            String.Compare(referenceInfo.typeLibName, projectRefInfo.typeLibName, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            isTemporary = true;
                        }
                    }
                }
            }

            try
            {
                var referencePaths = new List<string>(GetResolvedAssemblyReferenceItemSpecs());

                if (dependencyPaths != null)
                {
                    referencePaths.AddRange(dependencyPaths);
                }

                // not in the cache? see if anyone was kind enough to generate it for us
                var reference = new TlbReference(Log, Silent, this, referencePaths, referenceInfo, refName,
                    outputDirectory, isTemporary, DelaySign, KeyFile, KeyContainer, NoClassMembers,
                    TargetProcessorArchitecture, IncludeVersionInInteropName, ExecuteAsTool, _tlbimpPath,
                    BuildEngine, EnvironmentVariables);

                // wrapper doesn't exist or needs regeneration? generate it then
                if (!reference.FindExistingWrapper(out wrapperInfo, _timestampCache[referenceInfo.strippedTypeLibPath]))
                {
                    if (!reference.GenerateWrapper(out wrapperInfo))
                    {
                        return false;
                    }
                }

                // if found or successfully generated, cache it.
                _cacheTlb.Add(typeLibKey, wrapperInfo);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Helper function - resolves an ActiveX reference given the type library attributes.
        /// </summary>
        /// <param name="referenceInfo">Information about the reference to be resolved</param>
        /// <param name="outputDirectory">Directory the interop DLL should be written to</param>
        /// <param name="refName">Name of reference</param>
        /// <param name="wrapperInfo">Information about wrapper locations</param>
        /// <returns>True if the reference was already found or successfully generated, false otherwise.</returns>
        internal bool ResolveComReferenceAx(ComReferenceInfo referenceInfo, string outputDirectory, string refName, out ComReferenceWrapperInfo wrapperInfo)
        {
            string typeLibKey = ComReference.UniqueKeyFromTypeLibAttr(referenceInfo.attr);

            // look in the Ax cache first
            if (_cacheAx.TryGetValue(typeLibKey, out wrapperInfo))
            {
                return true;
            }

            try
            {
                // not in the cache? see if anyone was kind enough to generate it for us

                var reference = new AxReference(Log, Silent, this, referenceInfo, refName, outputDirectory, DelaySign, KeyFile, KeyContainer, IncludeVersionInInteropName, _aximpPath, BuildEngine, EnvironmentVariables);

                // wrapper doesn't exist or needs regeneration? generate it then
                if (!reference.FindExistingWrapper(out wrapperInfo, _timestampCache[referenceInfo.strippedTypeLibPath]))
                {
                    if (!reference.GenerateWrapper(out wrapperInfo))
                    {
                        return false;
                    }
                }

                // if found or successfully generated, cache it.
                _cacheAx.Add(typeLibKey, wrapperInfo);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                return false;
            }

            return true;
        }

        #region VerifyReferenceMetadataForNameItem required metadata

        // Metadata required on a valid Com reference item
        private static readonly string[] s_requiredMetadataForNameItem = {
            ComReferenceItemMetadataNames.guid,
            ComReferenceItemMetadataNames.versionMajor,
            ComReferenceItemMetadataNames.versionMinor
        };

        #endregion

        /*
         * Method:  VerifyReferenceMetadataForNameItem
         * 
         * Verifies that all required metadata on the COM reference item are there.
         */
        internal static bool VerifyReferenceMetadataForNameItem(ITaskItem reference, out string missingOrInvalidMetadata)
        {
            missingOrInvalidMetadata = "";

            // go through the list of required metadata and fail if one of them is not found
            foreach (string metadataName in s_requiredMetadataForNameItem)
            {
                if (reference.GetMetadata(metadataName).Length == 0)
                {
                    missingOrInvalidMetadata = metadataName;
                    return false;
                }
            }

            // now verify they contain valid data
            if (!Guid.TryParse(reference.GetMetadata(ComReferenceItemMetadataNames.guid), out _))
            {
                // invalid guid format
                missingOrInvalidMetadata = ComReferenceItemMetadataNames.guid;
                return false;
            }

            try
            {
                // invalid versionMajor format
                missingOrInvalidMetadata = ComReferenceItemMetadataNames.versionMajor;
                short.Parse(reference.GetMetadata(ComReferenceItemMetadataNames.versionMajor), NumberStyles.Integer, CultureInfo.InvariantCulture);

                // invalid versionMinor format
                missingOrInvalidMetadata = ComReferenceItemMetadataNames.versionMinor;
                short.Parse(reference.GetMetadata(ComReferenceItemMetadataNames.versionMinor), NumberStyles.Integer, CultureInfo.InvariantCulture);

                // only check lcid if specified
                if (reference.GetMetadata(ComReferenceItemMetadataNames.lcid).Length > 0)
                {
                    // invalid lcid format
                    missingOrInvalidMetadata = ComReferenceItemMetadataNames.lcid;
                    int.Parse(reference.GetMetadata(ComReferenceItemMetadataNames.lcid), NumberStyles.Integer, CultureInfo.InvariantCulture);
                }

                // only check wrapperTool if specified
                if (reference.GetMetadata(ComReferenceItemMetadataNames.wrapperTool).Length > 0)
                {
                    // invalid wrapperTool type
                    missingOrInvalidMetadata = ComReferenceItemMetadataNames.wrapperTool;
                    string wrapperTool = reference.GetMetadata(ComReferenceItemMetadataNames.wrapperTool);

                    if ((!ComReferenceTypes.IsAxImp(wrapperTool)) &&
                        (!ComReferenceTypes.IsTlbImp(wrapperTool)) &&
                        (!ComReferenceTypes.IsPia(wrapperTool)))
                    {
                        return false;
                    }
                }
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }

            // all metadata were found
            missingOrInvalidMetadata = String.Empty;
            return true;
        }

        /*
         * Method:  InitializeDefaultMetadataForNameItem
         * 
         * Initializes optional metadata on given name item to their default values if they're not present
         */
        internal static void InitializeDefaultMetadataForNameItem(ITaskItem reference)
        {
            // default value for lcid is 0
            if (reference.GetMetadata(ComReferenceItemMetadataNames.lcid).Length == 0)
            {
                reference.SetMetadata(ComReferenceItemMetadataNames.lcid, "0");
            }

            // default value for wrapperTool is tlbimp
            if (reference.GetMetadata(ComReferenceItemMetadataNames.wrapperTool).Length == 0)
            {
                reference.SetMetadata(ComReferenceItemMetadataNames.wrapperTool, ComReferenceTypes.tlbimp);
            }
        }

        /*
         * Method:  InitializeDefaultMetadataForFileItem
         * 
         * Initializes optional metadata on given file item to their default values if they're not present
         */
        internal static void InitializeDefaultMetadataForFileItem(ITaskItem reference)
        {
            // default value for wrapperTool is tlbimp
            if (reference.GetMetadata(ComReferenceItemMetadataNames.wrapperTool).Length == 0)
            {
                reference.SetMetadata(ComReferenceItemMetadataNames.wrapperTool, ComReferenceTypes.tlbimp);
            }
        }

        /*
         * Method:  CheckForConflictingReferences
         * 
         * Checks if we have any conflicting references.
         */
        internal bool CheckForConflictingReferences()
        {
            var namesForReferences = new Dictionary<string, ComReferenceInfo>(StringComparer.OrdinalIgnoreCase);
            var refsToBeRemoved = new List<ComReferenceInfo>();
            bool noConflictsFound = true;

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (ComReferenceInfo projectRefInfo in allProjectRefs)
                {
                    string wrapperType = projectRefInfo.taskItem.GetMetadata(ComReferenceItemMetadataNames.wrapperTool);

                    // only check aximp and tlbimp references
                    if ((pass == 0 && ComReferenceTypes.IsAxImp(wrapperType)) ||
                        (pass == 1 && ComReferenceTypes.IsTlbImp(wrapperType)))
                    {
                        // if we already have a reference with this name, compare attributes
                        if (namesForReferences.TryGetValue(projectRefInfo.typeLibName, out ComReferenceInfo conflictingRef))
                        {
                            // if different type lib attributes, we have a conflict, remove the conflicting reference
                            // and continue processing
                            if (!ComReference.AreTypeLibAttrEqual(projectRefInfo.attr, conflictingRef.attr))
                            {
                                if (!Silent)
                                {
                                    Log.LogWarningWithCodeFromResources("ResolveComReference.ConflictingReferences", projectRefInfo.taskItem.ItemSpec, conflictingRef.taskItem.ItemSpec);
                                }

                                // mark the reference for removal, can't do it here because we're iterating through the ref's container
                                refsToBeRemoved.Add(projectRefInfo);
                                noConflictsFound = false;
                            }
                        }
                        else
                        {
                            // store the current reference
                            namesForReferences.Add(projectRefInfo.typeLibName, projectRefInfo);
                        }
                    }
                }

                // use a new hashtable for different passes - refs to the same typelib with different wrapper types are OK
                namesForReferences.Clear();
            }

            // now that we're outside the loop, we can safely remove the marked references
            foreach (ComReferenceInfo projectRefInfo in refsToBeRemoved)
            {
                // remove and cleanup
                allProjectRefs.Remove(projectRefInfo);
                projectRefInfo.ReleaseTypeLibPtr();
            }

            return noConflictsFound;
        }

        /// <summary>
        /// Set the CopyLocal metadata to false on all assemblies that are located in the GAC.
        /// </summary>
        /// <param name="outputTaskItems">ArrayList of ITaskItems that will be outputted from the task</param>
        /// <param name="gacPath">The GAC root path</param>
        internal void SetCopyLocalToFalseOnGacOrNoPIAAssemblies(List<ITaskItem> outputTaskItems, string gacPath)
        {
            foreach (ITaskItem taskItem in outputTaskItems)
            {
                if (taskItem.GetMetadata(ItemMetadataNames.msbuildReferenceSourceTarget).Length == 0)
                {
                    taskItem.SetMetadata(ItemMetadataNames.msbuildReferenceSourceTarget, "ResolveComReference");
                }

                string embedInteropTypesMetadata = taskItem.GetMetadata(ItemMetadataNames.embedInteropTypes);

                if (_projectTargetFramework != null && (_projectTargetFramework >= s_targetFrameworkVersion_40))
                {
                    if ((embedInteropTypesMetadata != null) &&
                        (String.Compare(embedInteropTypesMetadata, "true", StringComparison.OrdinalIgnoreCase) == 0))
                    {
                        // Embed Interop Types forces CopyLocal to false
                        taskItem.SetMetadata(ItemMetadataNames.copyLocal, "false");
                        continue;
                    }
                }

                string privateMetadata = taskItem.GetMetadata(ItemMetadataNames.privateMetadata);

                // if Private is not set on the original item, we set CopyLocal to false for GAC items 
                // and true for non-GAC items
                if ((privateMetadata == null) || (privateMetadata.Length == 0))
                {
                    if (String.Compare(taskItem.ItemSpec, 0, gacPath, 0, gacPath.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        taskItem.SetMetadata(ItemMetadataNames.copyLocal, "false");
                    }
                    else
                    {
                        taskItem.SetMetadata(ItemMetadataNames.copyLocal, "true");
                    }
                }
                // if Private is set, it always takes precedence
                else
                {
                    taskItem.SetMetadata(ItemMetadataNames.copyLocal, privateMetadata);
                }
            }
        }

        /// <summary>
        /// Scan all the dependencies of the main project references and preresolve them
        /// so that when we get asked about a previously unknown dependency in the form of a .NET assembly 
        /// we know what to do with it.
        /// </summary>
        private List<string> ScanAndResolveAllDependencies(ComDependencyWalker dependencyWalker, ComReferenceInfo reference)
        {
            dependencyWalker.ClearDependencyList();

            if (!Silent)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.ScanningDependencies", reference.SourceItemSpec);
            }

            dependencyWalker.AnalyzeTypeLibrary(reference.typeLibPointer);

            if (!Silent)
            {
                foreach (Exception ex in dependencyWalker.EncounteredProblems)
                {
                    // A failure to resolve a reference due to something possibly being missing from disk is not
                    // an error; the user may not be actually consuming types from it
                    Log.LogWarningWithCodeFromResources("ResolveComReference.FailedToScanDependencies",
                        reference.SourceItemSpec, ex.Message);
                }
            }

            dependencyWalker.EncounteredProblems.Clear();

            var dependentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TYPELIBATTR[] dependentAttrs = dependencyWalker.GetDependencies();

            foreach (TYPELIBATTR dependencyTypeLibAttr in dependentAttrs)
            {
                // We don't need to even try to resolve if the dependency reference is ourselves. 
                if (!ComReference.AreTypeLibAttrEqual(dependencyTypeLibAttr, reference.attr))
                {
                    if (IsExistingProjectReference(dependencyTypeLibAttr, null, out ComReferenceInfo existingReference))
                    {
                        // If we're resolving another project reference, empty out the type cache -- if the dependencies are buried,
                        // caching the analyzed types can make it so that we don't recognize our dependencies' dependencies. 
                        dependencyWalker.ClearAnalyzedTypeCache();

                        if (ResolveReference(dependencyWalker, existingReference, WrapperOutputDirectory, out ITaskItem resolvedItem))
                        {
                            // Add the resolved dependency
                            dependentPaths.Add(resolvedItem.ItemSpec);

                            // and anything it depends on 
                            foreach (string dependentPath in existingReference.dependentWrapperPaths)
                            {
                                dependentPaths.Add(dependentPath);
                            }
                        }
                    }
                    else
                    {
                        if (!Silent)
                        {
                            Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.ResolvingDependency",
                                dependencyTypeLibAttr.guid, dependencyTypeLibAttr.wMajorVerNum, dependencyTypeLibAttr.wMinorVerNum);
                        }

                        ((IComReferenceResolver)this).ResolveComClassicReference(dependencyTypeLibAttr, WrapperOutputDirectory,
                            null /* unknown wrapper type */, null /* unknown name */, out ComReferenceWrapperInfo wrapperInfo);

                        if (!Silent)
                        {
                            Log.LogMessageFromResources(MessageImportance.Low, "ResolveComReference.ResolvedDependentComReference",
                                dependencyTypeLibAttr.guid, dependencyTypeLibAttr.wMajorVerNum, dependencyTypeLibAttr.wMinorVerNum,
                                wrapperInfo.path);
                        }

                        dependentPaths.Add(wrapperInfo.path);
                    }
                }
            }

            return dependentPaths.ToList();
        }

        /*
         * Method:  TaskItemToTypeLibAttr
         * 
         * Gets the TLIBATTR structure based on the reference we have.
         * Sets guid, versions major & minor, lcid.
         */
        internal static TYPELIBATTR TaskItemToTypeLibAttr(ITaskItem taskItem)
        {
            // copy metadata from Reference to our TYPELIBATTR
            var attr = new TYPELIBATTR
            {
                guid = new Guid(taskItem.GetMetadata(ComReferenceItemMetadataNames.guid)),
                wMajorVerNum = short.Parse(
                    taskItem.GetMetadata(ComReferenceItemMetadataNames.versionMajor),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture),
                wMinorVerNum = short.Parse(
                    taskItem.GetMetadata(ComReferenceItemMetadataNames.versionMinor),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture),
                lcid = int.Parse(
                    taskItem.GetMetadata(ComReferenceItemMetadataNames.lcid),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture)
            };

            return attr;
        }

        #endregion
    }
}

#endif
