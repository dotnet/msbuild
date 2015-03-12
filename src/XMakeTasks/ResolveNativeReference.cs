// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Resources;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Main class for the native reference resolution task.
    /// </summary>
    public class ResolveNativeReference : TaskExtension
    {
        #region Constructors

        /// <summary>
        ///  ResolveNativeReference constructor
        /// </summary>
        public ResolveNativeReference()
        {
            // do nothing.
        }
        #endregion

        #region Properties
        [Required]
        public ITaskItem[] NativeReferences
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_nativeReferences, "nativeReferences");
                return _nativeReferences;
            }
            set
            {
                _nativeReferences = value;
            }
        }

        [Required]
        public string[] AdditionalSearchPaths
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_additionalSearchPaths, "additionalSearchPaths");
                return _additionalSearchPaths;
            }
            set
            {
                _additionalSearchPaths = value;
            }
        }

        [Output]
        public ITaskItem[] ContainingReferenceFiles
        {
            get
            {
                return _containingReferenceFiles;
            }
            set
            {
                _containingReferenceFiles = value;
            }
        }

        [Output]
        public ITaskItem[] ContainedPrerequisiteAssemblies
        {
            get
            {
                return _containedPrerequisiteAssemblies;
            }
            set
            {
                _containedPrerequisiteAssemblies = value;
            }
        }

        [Output]
        public ITaskItem[] ContainedComComponents
        {
            get
            {
                return _containedComComponents;
            }
            set
            {
                _containedComComponents = value;
            }
        }

        [Output]
        public ITaskItem[] ContainedTypeLibraries
        {
            get
            {
                return _containedTypeLibraries;
            }
            set
            {
                _containedTypeLibraries = value;
            }
        }

        [Output]
        public ITaskItem[] ContainedLooseTlbFiles
        {
            get
            {
                return _containedLooseTlbFiles;
            }
            set
            {
                _containedLooseTlbFiles = value;
            }
        }

        [Output]
        public ITaskItem[] ContainedLooseEtcFiles
        {
            get
            {
                return _containedLooseEtcFiles;
            }
            set
            {
                _containedLooseEtcFiles = value;
            }
        }

        private ITaskItem[] _nativeReferences = null;
        private ITaskItem[] _containingReferenceFiles = null;
        private ITaskItem[] _containedPrerequisiteAssemblies = null;
        private ITaskItem[] _containedComComponents = null;
        private ITaskItem[] _containedTypeLibraries = null;
        private ITaskItem[] _containedLooseTlbFiles = null;
        private ITaskItem[] _containedLooseEtcFiles = null;
        private string[] _additionalSearchPaths = new string[0];
        #endregion

        #region Nested classes
        private class ItemSpecComparerClass : IComparer
        {
            int IComparer.Compare(Object taskItem1, Object taskItem2)
            {
                // simply calls string.Compare on the item specs of the items
                return (string.Compare(((ITaskItem)taskItem1).ItemSpec, ((ITaskItem)taskItem2).ItemSpec, StringComparison.OrdinalIgnoreCase));
            }
        }
        #endregion

        #region ITask members

        /// <summary>
        /// Task entry point.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            // Process each task item. If one of them fails we still process the
            // rest of them, but remember that the task should return failure.
            bool retValue = true;
            int reference = 0;

            Hashtable containingReferenceFilesTable = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Hashtable containedPrerequisiteAssembliesTable = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Hashtable containedComComponentsTable = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Hashtable containedTypeLibrariesTable = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Hashtable containedLooseTlbFilesTable = new Hashtable(StringComparer.OrdinalIgnoreCase);
            Hashtable containedLooseEtcFilesTable = new Hashtable(StringComparer.OrdinalIgnoreCase);

            for (reference = 0; reference < NativeReferences.GetLength(0); reference++)
            {
                ITaskItem item = NativeReferences[reference];
                string path = item.GetMetadata("HintPath");
                // If no HintPath then fallback to trying to resolve from the assembly identity...
                if (String.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    AssemblyIdentity ai = AssemblyIdentity.FromAssemblyName(item.ItemSpec);
                    if (ai != null)
                    {
                        Log.LogMessageFromResources(MessageImportance.Low, "ResolveNativeReference.ResolveReference", item.ItemSpec);
                        foreach (string searchPath in AdditionalSearchPaths)
                        {
                            Log.LogMessageFromResources(MessageImportance.Low, "ResolveAssemblyReference.FourSpaceIndent", searchPath);
                        }
                        path = ai.Resolve(AdditionalSearchPaths);
                    }
                }
                else
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "ResolveNativeReference.ResolveReference", item.ItemSpec);
                    Log.LogMessageFromResources(MessageImportance.Low, "ResolveAssemblyReference.FourSpaceIndent", path);
                }

                if (!String.IsNullOrEmpty(path))
                {
#if _DEBUG
                    try
                    {
#endif
                    if (!ExtractFromManifest(NativeReferences[reference], path, containingReferenceFilesTable, containedPrerequisiteAssembliesTable, containedComComponentsTable, containedTypeLibrariesTable, containedLooseTlbFilesTable, containedLooseEtcFilesTable))
                    {
                        retValue = false;
                    }
#if _DEBUG
                    }
                    catch (Exception)
                    {
                        Debug.Assert(false, "Unexpected exception in ResolveNativeReference.Execute. " + 
                            "Please log a MSBuild bug specifying the steps to reproduce the problem.");
                        throw;
                    }
#endif
                }
                else
                {
                    Log.LogWarningWithCodeFromResources("ResolveNativeReference.FailedToResolveReference", item.ItemSpec);
                }
            }

            IComparer itemSpecComparer = new ItemSpecComparerClass();

            _containingReferenceFiles = new ITaskItem[containingReferenceFilesTable.Count];
            containingReferenceFilesTable.Values.CopyTo(_containingReferenceFiles, 0);
            Array.Sort(_containingReferenceFiles, itemSpecComparer);

            _containedPrerequisiteAssemblies = new ITaskItem[containedPrerequisiteAssembliesTable.Count];
            containedPrerequisiteAssembliesTable.Values.CopyTo(_containedPrerequisiteAssemblies, 0);
            Array.Sort(_containedPrerequisiteAssemblies, itemSpecComparer);

            _containedComComponents = new ITaskItem[containedComComponentsTable.Count];
            containedComComponentsTable.Values.CopyTo(_containedComComponents, 0);
            Array.Sort(_containedComComponents, itemSpecComparer);

            _containedTypeLibraries = new ITaskItem[containedTypeLibrariesTable.Count];
            containedTypeLibrariesTable.Values.CopyTo(_containedTypeLibraries, 0);
            Array.Sort(_containedTypeLibraries, itemSpecComparer);

            _containedLooseTlbFiles = new ITaskItem[containedLooseTlbFilesTable.Count];
            containedLooseTlbFilesTable.Values.CopyTo(_containedLooseTlbFiles, 0);
            Array.Sort(_containedLooseTlbFiles, itemSpecComparer);

            _containedLooseEtcFiles = new ITaskItem[containedLooseEtcFilesTable.Count];
            containedLooseEtcFilesTable.Values.CopyTo(_containedLooseEtcFiles, 0);
            Array.Sort(_containedLooseEtcFiles, itemSpecComparer);

            return retValue;
        }
        #endregion

        #region Methods

        /// <summary>
        /// Helper manifest resolution method. Cracks the manifest and extracts the different elements from it.
        /// </summary>
        internal bool ExtractFromManifest(ITaskItem taskItem, string path, Hashtable containingReferenceFilesTable, Hashtable containedPrerequisiteAssembliesTable, Hashtable containedComComponentsTable, Hashtable containedTypeLibrariesTable, Hashtable containedLooseTlbFilesTable, Hashtable containedLooseEtcFilesTable)
        {
            Log.LogMessageFromResources(MessageImportance.Low, "ResolveNativeReference.Comment", path);

            Manifest manifest = null;

            try
            {
                manifest = ManifestReader.ReadManifest(path, false);
            }
            catch (System.Xml.XmlException ex)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.ReadInputManifestFailed", path, ex.Message);
                return false;
            }

            if (manifest != null)
            {
                manifest.TreatUnfoundNativeAssembliesAsPrerequisites = true;
                manifest.ReadOnly = true; // only reading a manifest, set flag so we get GenerateManifest.ResolveFailedInReadOnlyMode instead of GenerateManifest.ResolveFailedInReadWriteMode messages
                manifest.ResolveFiles();
                if (!manifest.OutputMessages.LogTaskMessages(this))
                    return false;

                ApplicationManifest applicationManifest = manifest as ApplicationManifest;
                bool isClickOnceApp = applicationManifest != null && applicationManifest.IsClickOnceManifest;
                // ClickOnce application manifest should not be added as native reference, but we should open and process it.        
                if (containingReferenceFilesTable.ContainsKey(path) == false && !isClickOnceApp)
                {
                    ITaskItem itemNativeReferenceFile = new TaskItem();
                    itemNativeReferenceFile.ItemSpec = path;
                    if (manifest.AssemblyIdentity.Name != null)
                        itemNativeReferenceFile.SetMetadata(ItemMetadataNames.fusionName, manifest.AssemblyIdentity.Name);
                    if (taskItem != null)
                        taskItem.CopyMetadataTo(itemNativeReferenceFile);
                    containingReferenceFilesTable.Add(path, itemNativeReferenceFile);
                }

                if (manifest.AssemblyReferences != null)
                {
                    foreach (AssemblyReference assemblyref in manifest.AssemblyReferences)
                    {
                        if (assemblyref.IsVirtual)
                        {
                            //It is a CLR virtual reference, not a real reference.
                            continue;
                        }

                        if (!assemblyref.IsPrerequisite)
                        {
                            // recurse and call ExtractFromManifest for this assembly--if it has a manifest it will be cracked.
                            ExtractFromManifest(null, assemblyref.ResolvedPath, containingReferenceFilesTable, containedPrerequisiteAssembliesTable, containedComComponentsTable, containedTypeLibrariesTable, containedLooseTlbFilesTable, containedLooseEtcFilesTable);
                        }
                        else
                        {
                            string id = assemblyref.AssemblyIdentity.GetFullName(AssemblyIdentity.FullNameFlags.All);
                            // add the assembly to the prerequisites list, if it's not already there
                            if (containedPrerequisiteAssembliesTable.ContainsKey(id) == false)
                            {
                                ITaskItem item = new TaskItem();
                                item.ItemSpec = id;
                                item.SetMetadata("DependencyType", "Prerequisite");
                                containedPrerequisiteAssembliesTable.Add(id, item);
                            }
                        }
                    }
                }

                if (manifest.FileReferences != null)
                {
                    foreach (FileReference fileref in manifest.FileReferences)
                    {
                        if (fileref.ResolvedPath == null)
                            continue;

                        // add the loose file to the outputs list, if it's not already there
                        if (containedLooseEtcFilesTable.ContainsKey(fileref.ResolvedPath) == false)
                        {
                            ITaskItem itemLooseEtcFile = new TaskItem();
                            itemLooseEtcFile.ItemSpec = fileref.ResolvedPath;
                            // The ParentFile attribute (visible thru Project Outputs) relates the loose
                            // file to the parent assembly of which it is a part. This is important so we can
                            // group those files together with their parent assembly in the deployment tool
                            // (i.e. ClickOnce application files dialog).
                            itemLooseEtcFile.SetMetadata(ItemMetadataNames.parentFile, Path.GetFileName(path));
                            containedLooseEtcFilesTable.Add(fileref.ResolvedPath, itemLooseEtcFile);
                        }

                        if (fileref.ComClasses != null)
                        {
                            foreach (ComClass comclass in fileref.ComClasses)
                            {
                                // add the comclass to the outputs list, if it's not already there
                                if (containedComComponentsTable.ContainsKey(comclass.ClsId) == false)
                                {
                                    ITaskItem itemComClass = new TaskItem();
                                    itemComClass.ItemSpec = comclass.ClsId;
                                    containedComComponentsTable.Add(comclass.ClsId, itemComClass);
                                }
                            }
                        }

                        if (fileref.TypeLibs != null)
                        {
                            foreach (TypeLib typelib in fileref.TypeLibs)
                            {
                                // add the typelib to the outputs list, if it's not already there
                                if (containedTypeLibrariesTable.ContainsKey(typelib.TlbId) == false)
                                {
                                    ITaskItem itemTypeLib = new TaskItem();
                                    itemTypeLib.ItemSpec = typelib.TlbId;
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.wrapperTool, ComReferenceTypes.tlbimp);
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.guid, typelib.TlbId);
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.lcid, "0");
                                    string delimStr = ".";
                                    char[] delimiter = delimStr.ToCharArray();
                                    string[] verMajorAndMinor = null;
                                    verMajorAndMinor = typelib.Version.Split(delimiter);
                                    // UNDONE: are major and minor version numbers in base 10 or 16?
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.versionMajor, verMajorAndMinor[0]);
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.versionMinor, verMajorAndMinor[1]);
                                    containedTypeLibrariesTable.Add(typelib.TlbId, itemTypeLib);
                                }
                            }

                            // add the loose TLB file to the outputs list, if it's not already there
                            if (containedLooseTlbFilesTable.Contains(fileref.ResolvedPath) == false)
                            {
                                ITaskItem itemLooseTlbFile = new TaskItem();
                                itemLooseTlbFile.ItemSpec = fileref.ResolvedPath;
                                containedLooseTlbFilesTable.Add(fileref.ResolvedPath, itemLooseTlbFile);
                            }
                        }
                    }
                }
            }
            return true;
        }
        #endregion
    }
}
