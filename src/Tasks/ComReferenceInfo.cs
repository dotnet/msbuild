// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

// TYPELIBATTR clashes with the one in InteropServices.
using TYPELIBATTR = System.Runtime.InteropServices.ComTypes.TYPELIBATTR;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Internal class representing information about a COM reference.
    /// </summary>
    internal class ComReferenceInfo
    {
        #region Properties

        /// <summary>
        /// ITypeLib pointer 
        /// </summary>
        internal ITypeLib typeLibPointer;

        /// <summary>
        /// type library attributes for the reference. Taken from the task item itself or type library if
        /// reference is specified as file on disk.
        /// </summary>
        internal TYPELIBATTR attr;

        /// <summary>
        /// type library name
        /// </summary>
        internal string typeLibName;

        /// <summary>
        /// path to the reference, with typelibrary number stripped, if any (so ref1.dll\2 becomes ref1.dll).
        /// The full path is only used for loading the type library, and it's not necessary 
        /// to do it after the interface pointer is cached in this object.
        /// </summary>
        internal string strippedTypeLibPath;

        /// <summary>
        /// When using TlbImp.exe, we need to make sure that we keep track of the non-stripped typelib path, 
        /// because that's what we need to pass to TlbImp.  
        /// </summary>
        internal string fullTypeLibPath;

        /// <summary>
        /// reference to the original ITaskItem, if any
        /// </summary>
        internal ITaskItem taskItem;

        /// <summary>
        /// Path to the resolved reference.
        /// </summary>
        internal ComReferenceInfo primaryOfAxImpRef;

        /// <summary>
        /// The wrapper that resulted from resolving the COM reference.
        /// </summary>
        internal ComReferenceWrapperInfo resolvedWrapper;

        /// <summary>
        /// List of the paths to COM wrapper assemblies that this reference is dependent upon. 
        /// </summary>
        internal List<string> dependentWrapperPaths;

        /// <summary>
        /// Reference to the ITaskItem generated from the resolved reference, if any. 
        /// </summary>
        internal ITaskItem referencePathItem;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        internal ComReferenceInfo()
        {
            this.dependentWrapperPaths = new List<string>();
        }

        /// <summary>
        /// Construct a new ComReferenceInfo copying all state from the given ComReferenceInfo instance
        /// </summary>
        internal ComReferenceInfo(ComReferenceInfo copyFrom)
        {
            this.attr = copyFrom.attr;
            this.typeLibName = copyFrom.typeLibName;
            this.strippedTypeLibPath = copyFrom.strippedTypeLibPath;
            this.fullTypeLibPath = copyFrom.fullTypeLibPath;
            this.typeLibPointer = copyFrom.typeLibPointer;
            this.primaryOfAxImpRef = copyFrom.primaryOfAxImpRef;
            this.resolvedWrapper = copyFrom.resolvedWrapper;
            this.taskItem = new TaskItem(copyFrom.taskItem);
            this.dependentWrapperPaths = copyFrom.dependentWrapperPaths;
            this.referencePathItem = copyFrom.referencePathItem;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initialize the object with type library attributes
        /// </summary>
        internal bool InitializeWithTypeLibAttrs(TaskLoggingHelper log, bool silent, TYPELIBATTR tlbAttr, ITaskItem originalTaskItem, string targetProcessorArchitecture)
        {
            TYPELIBATTR remappableTlbAttr = tlbAttr;

            ComReference.RemapAdoTypeLib(log, silent, ref remappableTlbAttr);

            // for attribute references, the path is not specified, so we need to get it from the registry
            if (!ComReference.GetPathOfTypeLib(log, silent, ref remappableTlbAttr, out this.fullTypeLibPath))
            {
                return false;
            }

            // Now that we have the path, we can call InitializeWithPath to get the correct TYPELIBATTR set up
            // and the correct ITypeLib pointer. 
            return InitializeWithPath(log, silent, this.fullTypeLibPath, originalTaskItem, targetProcessorArchitecture);
        }

        /// <summary>
        /// Initialize the object with a type library path 
        /// </summary>
        internal bool InitializeWithPath(TaskLoggingHelper log, bool silent, string path, ITaskItem originalTaskItem, string targetProcessorArchitecture)
        {
            ErrorUtilities.VerifyThrowArgumentNull(path, "path");

            this.taskItem = originalTaskItem;

            // Note that currently we DO NOT remap file ADO references. This is because when pointing to a file on disk,
            // it seems unnatural to remap it to something else - a file reference means "use THIS component". 
            // This is still under debate though, and may be revised later.

            // save both the stripped and full path in our object -- for the most part we just need the stripped path, but if
            // we're using tlbimp.exe, we need to pass the full path w/ type lib number to it, or it won't generate the interop 
            // assembly correctly. 
            this.fullTypeLibPath = path;
            this.strippedTypeLibPath = ComReference.StripTypeLibNumberFromPath(path, File.Exists);

            // use the unstripped path to actually load the library
            switch (targetProcessorArchitecture)
            {
                case ProcessorArchitecture.AMD64:
                case ProcessorArchitecture.IA64:
                    this.typeLibPointer = (ITypeLib)NativeMethods.LoadTypeLibEx(path, (int)NativeMethods.REGKIND.REGKIND_LOAD_TLB_AS_64BIT);
                    break;
                case ProcessorArchitecture.X86:
                    this.typeLibPointer = (ITypeLib)NativeMethods.LoadTypeLibEx(path, (int)NativeMethods.REGKIND.REGKIND_LOAD_TLB_AS_32BIT);
                    break;
                case ProcessorArchitecture.ARM:
                case ProcessorArchitecture.MSIL:
                default:
                    // Transmit the flag directly from the .targets files and rely on tlbimp.exe to produce a good error message.
                    this.typeLibPointer = (ITypeLib)NativeMethods.LoadTypeLibEx(path, (int)NativeMethods.REGKIND.REGKIND_NONE);
                    break;
            }

            try
            {
                // get the type lib attributes from the retrieved interface pointer.
                // do NOT remap file ADO references, since we'd end up with a totally different reference than specified.
                ComReference.GetTypeLibAttrForTypeLib(ref this.typeLibPointer, out this.attr);

                // get the type lib name from the retrieved interface pointer
                if (!ComReference.GetTypeLibNameForITypeLib(
                    log,
                    silent,
                    this.typeLibPointer,
                    GetTypeLibId(log),
                    out this.typeLibName))
                {
                    ReleaseTypeLibPtr();
                    return false;
                }
            }
            catch (COMException)
            {
                ReleaseTypeLibPtr();
                throw;
            }

            return true;
        }

        /// <summary>
        /// A unique id string of this reference, it's either the item spec or (in the case of a dependency ref)
        /// guid and version from typelib attributes
        /// </summary>
        private string GetTypeLibId(TaskLoggingHelper log)
        {
            if (taskItem != null)
            {
                return taskItem.ItemSpec;
            }
            else
            {
                return log.FormatResourceString("ResolveComReference.TypeLibAttrId", attr.guid, attr.wMajorVerNum, attr.wMinorVerNum);
            }
        }

        /// <summary>
        /// Get the source item, if available. Null otherwise.
        /// </summary>
        internal string SourceItemSpec => taskItem?.ItemSpec;

        /// <summary>
        /// Release the COM ITypeLib pointer for this reference
        /// </summary>
        internal void ReleaseTypeLibPtr()
        {
            if (typeLibPointer != null)
            {
                Marshal.ReleaseComObject(typeLibPointer);
                typeLibPointer = null;
            }
        }
        #endregion
    }
}
