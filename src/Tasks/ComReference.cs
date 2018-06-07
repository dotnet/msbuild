// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

using Marshal = System.Runtime.InteropServices.Marshal;
using COMException = System.Runtime.InteropServices.COMException;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Abstract base class for COM reference wrappers providing common functionality. 
    /// This class hierarchy is used by the ResolveComReference task.Every class deriving from ComReference
    /// provides functionality for wrapping Com type libraries in a given way(for example AxReference, or PiaReference).
    /// </summary>
    internal abstract class ComReference
    {
        #region Constructors

        /// <summary>
        /// Internal constructor
        /// </summary>
        /// <param name="taskLoggingHelper">task logger instance used for logging</param>
        /// <param name="silent">true if this task should log only errors, no warnings or messages; false otherwise</param>
        /// <param name="referenceInfo">cached reference information (typelib pointer, original task item, typelib name etc.)</param>
        /// <param name="itemName">reference name (for better logging experience)</param>
        internal ComReference(TaskLoggingHelper taskLoggingHelper, bool silent, ComReferenceInfo referenceInfo, string itemName)
        {
            ReferenceInfo = referenceInfo;
            ItemName = itemName;
            Log = taskLoggingHelper;
            Silent = silent;
        }

        #endregion

        #region Properties

        /// <summary>
        /// various data for this reference (type lib attrs, name, path, ITypeLib pointer etc)
        /// </summary>
        internal virtual ComReferenceInfo ReferenceInfo { get; }

        /// <summary>
        /// item name as it appears in the project file
        /// (used for logging purposes, we use the actual typelib name for interesting operations)
        /// </summary>
        internal virtual string ItemName { get; }

        /// <summary>
        /// task used for logging messages
        /// </summary>
        protected internal TaskLoggingHelper Log { get; }

        /// <summary>
        /// True if this class should only log errors, but no messages or warnings.  
        /// </summary>
        protected internal bool Silent { get; }

        /// <summary>
        /// lazy-init property, returns true if ADO 2.7 is installed on the machine
        /// </summary>
        internal static bool Ado27Installed
        {
            get
            {
                // if we already know the answer, return it
                if (ado27PropertyInitialized)
                {
                    return ado27Installed;
                }

                // not initialized? Find out if ADO 2.7 is installed
                ado27Installed = true;
                ado27PropertyInitialized = true;

                ITypeLib ado27 = null;

                try
                {
                    // see if ADO 2.7 is registered.
                    ado27 = (ITypeLib)NativeMethods.LoadRegTypeLib(ref s_guidADO27, 2, 7, 0);
                }
                catch (COMException ex)
                {
                    // it's not registered. 
                    ado27Installed = false;
                    ado27ErrorMessage = ex.Message;
                }
                finally
                {
                    if (ado27 != null)
                    {
                        Marshal.ReleaseComObject(ado27);
                    }
                }

                return ado27Installed;
            }
        }

        internal static bool ado27PropertyInitialized;
        internal static bool ado27Installed;

        /// <summary>
        /// Error message if Ado27 is not installed on the machine (usually something like "type lib not registered")
        /// Only contains valid data if ADO 2.7 is not installed and Ado27Installed was called before
        /// </summary>
        internal static string Ado27ErrorMessage => ado27ErrorMessage;

        internal static string ado27ErrorMessage;

        #endregion

        #region Methods

        /// <summary>
        /// Given a TYPELIBATTR structure, generates a key that can be used in hashtables to identify it.
        /// </summary>
        internal static string UniqueKeyFromTypeLibAttr(TYPELIBATTR attr)
        {
            return $@"{attr.guid}|{attr.wMajorVerNum}.{attr.wMinorVerNum}|{attr.lcid}";
        }

        /// <summary>
        /// Compares two TYPELIBATTR structures
        /// </summary>
        internal static bool AreTypeLibAttrEqual(TYPELIBATTR attr1, TYPELIBATTR attr2)
        {
            return attr1.wMajorVerNum == attr2.wMajorVerNum &&
                attr1.wMinorVerNum == attr2.wMinorVerNum &&
                attr1.lcid == attr2.lcid &&
                attr1.guid == attr2.guid;
        }

        /// <summary>
        /// Helper method for retrieving type lib attributes for the given type lib
        /// </summary>
        internal static void GetTypeLibAttrForTypeLib(ref ITypeLib typeLib, out TYPELIBATTR typeLibAttr)
        {
            typeLib.GetLibAttr(out IntPtr pAttrs);

            // GetLibAttr should never return null, this is just to be safe
            if (pAttrs == IntPtr.Zero)
            {
                throw new COMException(
                    ResourceUtilities.GetResourceString("ResolveComReference.CannotGetTypeLibAttrForTypeLib"));
            }

            try
            {
                typeLibAttr = (TYPELIBATTR)Marshal.PtrToStructure(pAttrs, typeof(TYPELIBATTR));
            }
            finally
            {
                typeLib.ReleaseTLibAttr(pAttrs);
            }
        }

        /// <summary>
        /// Helper method for retrieving type attributes for a given type info
        /// </summary>
        /// <param name="typeInfo"></param>
        /// <param name="typeAttr"></param>
        /// <returns></returns>
        internal static void GetTypeAttrForTypeInfo(ITypeInfo typeInfo, out TYPEATTR typeAttr)
        {
            typeInfo.GetTypeAttr(out IntPtr pAttrs);

            // GetTypeAttr should never return null, this is just to be safe
            if (pAttrs == IntPtr.Zero)
            {
                throw new COMException(
                    ResourceUtilities.GetResourceString("ResolveComReference.CannotRetrieveTypeInformation"));
            }

            try
            {
                typeAttr = (TYPEATTR)Marshal.PtrToStructure(pAttrs, typeof(TYPEATTR));
            }
            finally
            {
                typeInfo.ReleaseTypeAttr(pAttrs);
            }
        }

        /// <summary>
        /// Helper method for retrieving type attributes for a given type info
        /// This method needs to also return the native pointer to be released when we're done with our VARDESC.
        /// It's not really possible to copy everything to a managed struct and then release the ptr immediately
        /// here, since VARDESCs contain other native pointers we may need to access.
        /// </summary>
        internal static void GetVarDescForVarIndex(ITypeInfo typeInfo, int varIndex, out VARDESC varDesc, out IntPtr varDescHandle)
        {
            typeInfo.GetVarDesc(varIndex, out IntPtr pVarDesc);

            // GetVarDesc should never return null, this is just to be safe
            if (pVarDesc == IntPtr.Zero)
            {
                throw new COMException(
                    ResourceUtilities.GetResourceString("ResolveComReference.CannotRetrieveTypeInformation"));
            }

            varDesc = (VARDESC)Marshal.PtrToStructure(pVarDesc, typeof(VARDESC));
            varDescHandle = pVarDesc;
        }

        /// <summary>
        /// Helper method for retrieving the function description structure for the given function index.
        /// This method needs to also return the native pointer to be released when we're done with our FUNCDESC.
        /// It's not really possible to copy everything to a managed struct and then release the ptr immediately
        /// here, since FUNCDESCs contain other native pointers we may need to access.
        /// </summary>
        internal static void GetFuncDescForDescIndex(ITypeInfo typeInfo, int funcIndex, out FUNCDESC funcDesc, out IntPtr funcDescHandle)
        {
            typeInfo.GetFuncDesc(funcIndex, out IntPtr pFuncDesc);

            // GetFuncDesc should never return null, this is just to be safe
            if (pFuncDesc == IntPtr.Zero)
            {
                throw new COMException(
                    ResourceUtilities.GetResourceString("ResolveComReference.CannotRetrieveTypeInformation"));
            }

            funcDesc = (FUNCDESC)Marshal.PtrToStructure(pFuncDesc, typeof(FUNCDESC));
            funcDescHandle = pFuncDesc;
        }

        /// <summary>
        /// Gets the name of given type library. 
        /// </summary>
        internal static bool GetTypeLibNameForITypeLib(TaskLoggingHelper log, bool silent, ITypeLib typeLib, string typeLibId, out string typeLibName)
        {
            typeLibName = "";

            // see if the type library supports ITypeLib2
            if (!(typeLib is ITypeLib2 typeLib2))
            {
                // Looks like the type lib doesn't support it. Let's use the Marshal method.
                typeLibName = Marshal.GetTypeLibName(typeLib);
                return true;
            }

            // Get the custom attribute.  If anything fails then just return the
            // type library name.  
            try
            {
                typeLib2.GetCustData(ref NativeMethods.GUID_TYPELIB_NAMESPACE, out object data);

                // if returned namespace is null or its type is not System.String, fall back to the default 
                // way of getting the type lib name (just to be safe)
                if (data == null || string.Compare(data.GetType().ToString(), "system.string", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    typeLibName = Marshal.GetTypeLibName(typeLib);
                    return true;
                }

                // Strip off the DLL extension if it's there
                typeLibName = (string)data;

                if (typeLibName.Length >= 4)
                {
                    if (string.Compare(typeLibName.Substring(typeLibName.Length - 4), ".dll", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        typeLibName = typeLibName.Substring(0, typeLibName.Length - 4);
                    }
                }
            }
            catch (COMException ex)
            {
                // If anything fails log a warning and just return the type library name.
                if (!silent)
                {
                    log.LogWarningWithCodeFromResources("ResolveComReference.CannotAccessTypeLibName", typeLibId, ex.Message);
                }
                typeLibName = Marshal.GetTypeLibName(typeLib);
                return true;
            }

            return true;
        }

        /// <summary>
        /// Gets the name of given type library.
        /// </summary>
        internal static bool GetTypeLibNameForTypeLibAttrs(TaskLoggingHelper log, bool silent, TYPELIBATTR typeLibAttr, out string typeLibName)
        {
            typeLibName = "";
            ITypeLib typeLib = null;

            try
            {
                // load our type library
                try
                {
                    TYPELIBATTR attr = typeLibAttr;
                    typeLib = (ITypeLib)NativeMethods.LoadRegTypeLib(ref attr.guid, attr.wMajorVerNum, attr.wMinorVerNum, attr.lcid);
                }
                catch (COMException ex)
                {
                    if (!silent)
                    {
                        log.LogWarningWithCodeFromResources("ResolveComReference.CannotLoadTypeLib", typeLibAttr.guid, typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum, ex.Message);
                    }

                    return false;
                }

                string typeLibId = log.FormatResourceString("ResolveComReference.TypeLibAttrId", typeLibAttr.guid.ToString(), typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum);

                return GetTypeLibNameForITypeLib(log, silent, typeLib, typeLibId, out typeLibName);
            }
            finally
            {
                if (typeLib != null)
                {
                    Marshal.ReleaseComObject(typeLib);
                }
            }
        }

        /// <summary>
        /// Strips type library number from a type library path (for example, "ref.dll\2" becomes "ref.dll")
        /// </summary>
        /// <param name="typeLibPath">type library path with possible typelib number appended to it</param>
        /// <returns>proper file path to the type library</returns>
        internal static string StripTypeLibNumberFromPath(string typeLibPath, FileExists fileExists)
        {
            bool lastChance = false;
            if (!string.IsNullOrEmpty(typeLibPath))
            {
                if (!fileExists(typeLibPath))
                {
                    // Strip the type library number
                    int lastSlash = typeLibPath.LastIndexOf('\\');

                    if (lastSlash != -1)
                    {
                        bool allNumbers = true;

                        for (int i = lastSlash + 1; i < typeLibPath.Length; i++)
                        {
                            if (!Char.IsDigit(typeLibPath[i]))
                            {
                                allNumbers = false;
                                break;
                            }
                        }

                        // If we had all numbers past the last slash then we're OK to strip
                        // the type library number
                        if (allNumbers)
                        {
                            typeLibPath = typeLibPath.Substring(0, lastSlash);
                            if (!fileExists(typeLibPath))
                            {
                                lastChance = true;
                            }
                        }
                        else
                        {
                            lastChance = true;
                        }
                    }
                    else
                    {
                        lastChance = true;
                    }
                }
            }

            // If we couldn't find the path directly, we'll use the same mechanism Windows uses to find
            // libraries.  LoadLibrary() will search all of the correct paths to find this module.  We can then
            // use GetModuleFileName() to determine the actual path from which the module was loaded.  This problem
            // was exposed in Vista where certain libraries are registered but are lacking paths in the registry,
            // so the old code would fail to find them on disk using the simplistic checks above.
            if (lastChance)
            {
                IntPtr libraryHandle = NativeMethodsShared.LoadLibrary(typeLibPath);
                if (IntPtr.Zero != libraryHandle)
                {
                    try
                    {
                        var sb = new StringBuilder(NativeMethodsShared.MAX_PATH);
                        System.Runtime.InteropServices.HandleRef handleRef = new System.Runtime.InteropServices.HandleRef(sb, libraryHandle);
                        int len = NativeMethodsShared.GetModuleFileName(handleRef, sb, sb.Capacity);
                        if ((len != 0) &&
                            ((uint)Marshal.GetLastWin32Error() != NativeMethodsShared.ERROR_INSUFFICIENT_BUFFER))
                        {
                            typeLibPath = sb.ToString();
                        }
                        else
                        {
                            typeLibPath = "";
                        }
                    }
                    finally
                    {
                        NativeMethodsShared.FreeLibrary(libraryHandle);
                    }
                }
                else
                {
                    typeLibPath = "";
                }
            }

            return typeLibPath;
        }

        /// <summary>
        /// Gets the type lib path for given type lib attributes(reused almost verbatim from vsdesigner utils code)
        /// NOTE:  If there's a typelib number at the end of the path, does NOT strip it.
        /// </summary>
        internal static bool GetPathOfTypeLib(TaskLoggingHelper log, bool silent, ref TYPELIBATTR typeLibAttr, out string typeLibPath)
        {
            // Get which file the type library resides in.  If the appropriate
            // file cannot be found then a blank string is returned.  
            typeLibPath = "";

            try
            {
                // Get the path from the registry
                // This call has known issues. See http://msdn.microsoft.com/en-us/library/ms221436.aspx for the method and 
                // here for the fix http://support.microsoft.com/kb/982110. Most users from Win7 or Win2008R2 should have already received this post Win7SP1.
                // In Summary: The issue is about calls to The QueryPathOfRegTypeLib function not returning the correct path for a 32-bit version of a
                // registered type library in a 64-bit edition of Windows 7 or in Windows Server 2008 R2. It either returns the 64bit path or null.
                typeLibPath = NativeMethods.QueryPathOfRegTypeLib(ref typeLibAttr.guid, typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum, typeLibAttr.lcid);
                typeLibPath = Environment.ExpandEnvironmentVariables(typeLibPath);
            }
            catch (COMException ex)
            {
                if (!silent)
                {
                    log.LogWarningWithCodeFromResources("ResolveComReference.CannotGetPathForTypeLib", typeLibAttr.guid, typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum, ex.Message);
                }

                return false;
            }

            if (typeLibPath != null && typeLibPath.Length > 0)
            {
                // We have to check for NULL here because QueryPathOfRegTypeLib() returns
                // a BSTR with a NULL character appended to it.
                if (typeLibPath[typeLibPath.Length - 1] == '\0')
                {
                    typeLibPath = typeLibPath.Substring(0, typeLibPath.Length - 1);
                }
            }

            if (!string.IsNullOrEmpty(typeLibPath))
            {
                return true;
            }

            if (!silent)
            {
                log.LogWarningWithCodeFromResources("ResolveComReference.CannotGetPathForTypeLib", typeLibAttr.guid, typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum, "");
            }

            return false;
        }

        #region RemapAdoTypeLib guids

        // guids for RemapAdoTypeLib
        private static readonly Guid s_guidADO20 = new Guid("{00000200-0000-0010-8000-00AA006D2EA4}");
        private static readonly Guid s_guidADO21 = new Guid("{00000201-0000-0010-8000-00AA006D2EA4}");
        private static readonly Guid s_guidADO25 = new Guid("{00000205-0000-0010-8000-00AA006D2EA4}");
        private static readonly Guid s_guidADO26 = new Guid("{00000206-0000-0010-8000-00AA006D2EA4}");
        // unfortunately this cannot be readonly, since it's being passed by reference to LoadRegTypeLib
        private static Guid s_guidADO27 = new Guid("{EF53050B-882E-4776-B643-EDA472E8E3F2}");

        #endregion

        /// <summary>
        /// Tries to remap an ADO type library to ADO 2.7. If the type library passed in is an older ADO tlb,
        /// then remap it to ADO 2.7 if it's registered on the machine (!). Otherwise don't modify the typelib.
        /// Returns true if the type library passed in was successfully remapped.
        /// </summary>
        internal static bool RemapAdoTypeLib(TaskLoggingHelper log, bool silent, ref TYPELIBATTR typeLibAttr)
        {
            // we only care about ADO 2.0, 2.1, 2.5 or 2.6 here.
            if (typeLibAttr.wMajorVerNum == 2)
            {
                if ((typeLibAttr.wMinorVerNum == 0 && typeLibAttr.guid == s_guidADO20) ||
                    (typeLibAttr.wMinorVerNum == 1 && typeLibAttr.guid == s_guidADO21) ||
                    (typeLibAttr.wMinorVerNum == 5 && typeLibAttr.guid == s_guidADO25) ||
                    (typeLibAttr.wMinorVerNum == 6 && typeLibAttr.guid == s_guidADO26))
                {
                    // see if ADO 2.7 is registered.
                    if (!Ado27Installed)
                    {
                        if (!silent)
                        {
                            // it's not registered. Don't change the original typelib then.
                            log.LogWarningWithCodeFromResources("ResolveComReference.FailedToRemapAdoTypeLib", typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum, Ado27ErrorMessage);
                        }

                        return false;
                    }

                    typeLibAttr.guid = s_guidADO27;
                    typeLibAttr.wMajorVerNum = 2;
                    typeLibAttr.wMinorVerNum = 7;
                    typeLibAttr.lcid = 0;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds an existing wrapper for the specified component
        /// </summary>
        internal abstract bool FindExistingWrapper(out ComReferenceWrapperInfo wrapperInfo, DateTime componentTimestamp);

        #endregion
    }
}
