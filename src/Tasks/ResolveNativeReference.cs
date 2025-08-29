﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
#if DEBUG
using System.Diagnostics;
#endif
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

#nullable disable

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
                ErrorUtilities.VerifyThrowArgumentNull(_nativeReferences, nameof(NativeReferences));
                return _nativeReferences;
            }
            set => _nativeReferences = value;
        }

        [Required]
        public string[] AdditionalSearchPaths
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_additionalSearchPaths, nameof(AdditionalSearchPaths));
                return _additionalSearchPaths;
            }
            set => _additionalSearchPaths = value;
        }

        [Output]
        public ITaskItem[] ContainingReferenceFiles { get; set; }

        [Output]
        public ITaskItem[] ContainedPrerequisiteAssemblies { get; set; }

        [Output]
        public ITaskItem[] ContainedComComponents { get; set; }

        [Output]
        public ITaskItem[] ContainedTypeLibraries { get; set; }

        [Output]
        public ITaskItem[] ContainedLooseTlbFiles { get; set; }

        [Output]
        public ITaskItem[] ContainedLooseEtcFiles { get; set; }

        private ITaskItem[] _nativeReferences;
        private string[] _additionalSearchPaths = Array.Empty<string>();
        #endregion

        #region Nested classes
        private class ItemSpecComparerClass : IComparer
        {
            int IComparer.Compare(Object taskItem1, Object taskItem2)
            {
                // simply calls string.Compare on the item specs of the items
                return string.Compare(((ITaskItem)taskItem1).ItemSpec, ((ITaskItem)taskItem2).ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }
        #endregion

        #region ITask members

        /// <summary>
        /// Task entry point.
        /// </summary>
        public override bool Execute()
        {
            // Process each task item. If one of them fails we still process the
            // rest of them, but remember that the task should return failure.
            bool retValue = true;
            int reference;

            var containingReferenceFilesTable = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
            var containedPrerequisiteAssembliesTable = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
            var containedComComponentsTable = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
            var containedTypeLibrariesTable = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
            var containedLooseTlbFilesTable = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
            var containedLooseEtcFilesTable = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);

            for (reference = 0; reference < NativeReferences.GetLength(0); reference++)
            {
                ITaskItem item = NativeReferences[reference];
                string path = item.GetMetadata("HintPath");
                // If no HintPath then fallback to trying to resolve from the assembly identity...
                if (String.IsNullOrEmpty(path) || !FileSystems.Default.FileExists(path))
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
#if DEBUG
                    try
                    {
#endif
#pragma warning disable format //invalid formatting in Release when try-catch is skipped
                        if (!ExtractFromManifest(NativeReferences[reference], path, containingReferenceFilesTable, containedPrerequisiteAssembliesTable, containedComComponentsTable, containedTypeLibrariesTable, containedLooseTlbFilesTable, containedLooseEtcFilesTable))
                        {
                            retValue = false;
                        }
#pragma warning restore format
#if DEBUG
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

            ContainingReferenceFiles = containingReferenceFilesTable.Values.ToArray();
            Array.Sort(ContainingReferenceFiles, itemSpecComparer);

            ContainedPrerequisiteAssemblies = containedPrerequisiteAssembliesTable.Values.ToArray();
            Array.Sort(ContainedPrerequisiteAssemblies, itemSpecComparer);

            ContainedComComponents = containedComComponentsTable.Values.ToArray();
            Array.Sort(ContainedComComponents, itemSpecComparer);

            ContainedTypeLibraries = containedTypeLibrariesTable.Values.ToArray();
            Array.Sort(ContainedTypeLibraries, itemSpecComparer);

            ContainedLooseTlbFiles = containedLooseTlbFilesTable.Values.ToArray();
            Array.Sort(ContainedLooseTlbFiles, itemSpecComparer);

            ContainedLooseEtcFiles = containedLooseEtcFilesTable.Values.ToArray();
            Array.Sort(ContainedLooseEtcFiles, itemSpecComparer);

            return retValue;
        }
        #endregion

        #region Methods

        /// <summary>
        /// Helper manifest resolution method. Cracks the manifest and extracts the different elements from it.
        /// </summary>
        internal bool ExtractFromManifest(
            ITaskItem taskItem,
            string path,
            Dictionary<string, ITaskItem> containingReferenceFilesTable,
            Dictionary<string, ITaskItem> containedPrerequisiteAssembliesTable,
            Dictionary<string, ITaskItem> containedComComponentsTable,
            Dictionary<string, ITaskItem> containedTypeLibrariesTable,
            Dictionary<string, ITaskItem> containedLooseTlbFilesTable,
            Dictionary<string, ITaskItem> containedLooseEtcFilesTable)
        {
            Log.LogMessageFromResources(MessageImportance.Low, "ResolveNativeReference.Comment", path);

            Manifest manifest;

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
                {
                    return false;
                }

                bool isClickOnceApp = manifest is ApplicationManifest applicationManifest && applicationManifest.IsClickOnceManifest;
                // ClickOnce application manifest should not be added as native reference, but we should open and process it.        
                if (!containingReferenceFilesTable.ContainsKey(path) && !isClickOnceApp)
                {
                    ITaskItem itemNativeReferenceFile = new TaskItem();
                    itemNativeReferenceFile.ItemSpec = path;
                    if (manifest.AssemblyIdentity.Name != null)
                    {
                        itemNativeReferenceFile.SetMetadata(ItemMetadataNames.fusionName, manifest.AssemblyIdentity.Name);
                    }
                    taskItem?.CopyMetadataTo(itemNativeReferenceFile);
                    containingReferenceFilesTable.Add(path, itemNativeReferenceFile);
                }

                if (manifest.AssemblyReferences != null)
                {
                    foreach (AssemblyReference assemblyref in manifest.AssemblyReferences)
                    {
                        if (assemblyref.IsVirtual)
                        {
                            // It is a CLR virtual reference, not a real reference.
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
                            if (!containedPrerequisiteAssembliesTable.ContainsKey(id))
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
                        {
                            continue;
                        }

                        // add the loose file to the outputs list, if it's not already there
                        if (!containedLooseEtcFilesTable.ContainsKey(fileref.ResolvedPath))
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
                                if (!containedComComponentsTable.ContainsKey(comclass.ClsId))
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
                                if (!containedTypeLibrariesTable.ContainsKey(typelib.TlbId))
                                {
                                    ITaskItem itemTypeLib = new TaskItem();
                                    itemTypeLib.ItemSpec = typelib.TlbId;
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.wrapperTool, ComReferenceTypes.tlbimp);
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.guid, typelib.TlbId);
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.lcid, "0");
                                    string[] verMajorAndMinor = typelib.Version.Split(MSBuildConstants.DotChar);
                                    // UNDONE: are major and minor version numbers in base 10 or 16?
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.versionMajor, verMajorAndMinor[0]);
                                    itemTypeLib.SetMetadata(ComReferenceItemMetadataNames.versionMinor, verMajorAndMinor[1]);
                                    containedTypeLibrariesTable.Add(typelib.TlbId, itemTypeLib);
                                }
                            }

                            // add the loose TLB file to the outputs list, if it's not already there
                            if (!containedLooseTlbFilesTable.ContainsKey(fileref.ResolvedPath))
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
