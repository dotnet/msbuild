// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN

using System;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;
using COMException = System.Runtime.InteropServices.COMException;
using Marshal = System.Runtime.InteropServices.Marshal;

#nullable disable

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

                unsafe
                {
                    using ComScope<ITypeLib> ado27 = new(null);

                    // see if ADO 2.7 is registered.
                    HRESULT hr = PInvoke.LoadRegTypeLib(s_guidADO27, 2, 7, 0, ado27);
                    if (hr.Failed)
                    {
                        // it's not registered.
                        ado27Installed = false;
                        ado27ErrorMessage = Marshal.GetExceptionForHR((int)hr)?.Message;
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
        internal static string UniqueKeyFromTypeLibAttr(TLIBATTR attr)
        {
            return $@"{attr.guid}|{attr.wMajorVerNum}.{attr.wMinorVerNum}|{attr.lcid}";
        }

        /// <summary>
        /// Compares two TYPELIBATTR structures
        /// </summary>
        internal static bool AreTypeLibAttrEqual(TLIBATTR attr1, TLIBATTR attr2)
        {
            return attr1.wMajorVerNum == attr2.wMajorVerNum &&
                attr1.wMinorVerNum == attr2.wMinorVerNum &&
                attr1.lcid == attr2.lcid &&
                attr1.guid == attr2.guid;
        }

        /// <summary>
        /// Helper method for retrieving type lib attributes for the given type lib
        /// </summary>
        internal static unsafe void GetTypeLibAttrForTypeLib(ITypeLib* typeLib, out TLIBATTR typeLibAttr)
        {
            TLIBATTR* pAttrs;
            typeLib->GetLibAttr(&pAttrs).ThrowOnFailure();

            // GetLibAttr should never return null, this is just to be safe
            if (pAttrs == null)
            {
                throw new COMException(
                    ResourceUtilities.GetResourceString("ResolveComReference.CannotGetTypeLibAttrForTypeLib"));
            }

            try
            {
                typeLibAttr = *pAttrs;
            }
            finally
            {
                typeLib->ReleaseTLibAttr(pAttrs);
            }
        }

        /// <summary>
        /// Helper method for retrieving type attributes for a given type info
        /// </summary>
        /// <param name="typeInfo"></param>
        /// <param name="typeAttr"></param>
        /// <returns></returns>
        internal static unsafe void GetTypeAttrForTypeInfo(ITypeInfo* typeInfo, out TYPEATTR typeAttr)
        {
            TYPEATTR* pAttrs;
            typeInfo->GetTypeAttr(&pAttrs).ThrowOnFailure();

            // GetTypeAttr should never return null, this is just to be safe
            if (pAttrs == null)
            {
                throw new COMException(
                    ResourceUtilities.GetResourceString("ResolveComReference.CannotRetrieveTypeInformation"));
            }

            try
            {
                typeAttr = *pAttrs;
            }
            finally
            {
                typeInfo->ReleaseTypeAttr(pAttrs);
            }
        }

        /// <summary>
        /// Helper method for retrieving type attributes for a given type info
        /// This method needs to also return the native pointer to be released when we're done with our VARDESC.
        /// It's not really possible to copy everything to a managed struct and then release the ptr immediately
        /// here, since VARDESCs contain other native pointers we may need to access.
        /// </summary>
        internal static unsafe void GetVarDescForVarIndex(ITypeInfo* typeInfo, int varIndex, out VARDESC* varDesc)
        {
            VARDESC* pVarDesc;
            typeInfo->GetVarDesc((uint)varIndex, &pVarDesc).ThrowOnFailure();

            // GetVarDesc should never return null, this is just to be safe
            if (pVarDesc == null)
            {
                throw new COMException(
                    ResourceUtilities.GetResourceString("ResolveComReference.CannotRetrieveTypeInformation"));
            }

            varDesc = pVarDesc;
        }

        /// <summary>
        /// Helper method for retrieving the function description structure for the given function index.
        /// This method needs to also return the native pointer to be released when we're done with our FUNCDESC.
        /// It's not really possible to copy everything to a managed struct and then release the ptr immediately
        /// here, since FUNCDESCs contain other native pointers we may need to access.
        /// </summary>
        internal static unsafe void GetFuncDescForDescIndex(ITypeInfo* typeInfo, int funcIndex, out FUNCDESC* funcDesc)
        {
            FUNCDESC* pFuncDesc;
            typeInfo->GetFuncDesc((uint)funcIndex, &pFuncDesc).ThrowOnFailure();

            // GetFuncDesc should never return null, this is just to be safe
            if (pFuncDesc == null)
            {
                throw new COMException(
                    ResourceUtilities.GetResourceString("ResolveComReference.CannotRetrieveTypeInformation"));
            }

            funcDesc = pFuncDesc;
        }

        /// <summary>
        /// Gets the name of a type library directly from the library's documentation (index -1),
        /// which is the struct-based COM equivalent of the legacy Marshal.GetTypeLibName helper.
        /// </summary>
        private static unsafe string GetTypeLibNameFromDocumentation(ITypeLib* typeLib)
        {
            // All out-params must be supplied: the runtime-callable-wrapper marshaller writes every
            // [out] BSTR even though only the name is needed here.
            using BSTR name = default;
            using BSTR docString = default;
            using BSTR helpFile = default;
            uint helpContext;
            typeLib->GetDocumentation(-1, &name, &docString, &helpContext, &helpFile).ThrowOnFailure();
            return name.ToString();
        }

        /// <summary>
        /// Gets the name of given type library.
        /// </summary>
        internal static unsafe bool GetTypeLibNameForITypeLib(TaskLoggingHelper log, bool silent, ITypeLib* typeLib, string typeLibId, out string typeLibName)
        {
            // see if the type library supports ITypeLib2
            using ComScope<ITypeLib2> typeLib2 = new(null);
            Guid typeLib2Iid = ITypeLib2.IID_Guid;
            if (typeLib->QueryInterface(&typeLib2Iid, typeLib2).Failed)
            {
                // Looks like the type lib doesn't support it. Get the name from the type library directly.
                typeLibName = GetTypeLibNameFromDocumentation(typeLib);
                return true;
            }

            // Get the custom attribute.  If anything fails then just return the
            // type library name.
            try
            {
                Guid namespaceGuid = NativeMethods.GUID_TYPELIB_NAMESPACE;
                VARIANT custData = default;
                typeLib2.Pointer->GetCustData(&namespaceGuid, &custData).ThrowOnFailure();

                object data;
                try
                {
                    data = Marshal.GetObjectForNativeVariant((IntPtr)(&custData));
                }
                finally
                {
                    PInvoke.VariantClear(&custData);
                }

                // if returned namespace is null or its type is not System.String, fall back to the default
                // way of getting the type lib name (just to be safe)
                if (data is not string typeLibNamespace)
                {
                    typeLibName = GetTypeLibNameFromDocumentation(typeLib);
                    return true;
                }

                // Strip off the DLL extension if it's there
                typeLibName = typeLibNamespace;

                if (typeLibName.Length >= 4 &&
                    typeLibName.AsSpan().EndsWith(".dll".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    typeLibName = typeLibName.Substring(0, typeLibName.Length - 4);
                }
            }
            catch (COMException ex)
            {
                // If anything fails log a warning and just return the type library name.
                if (!silent)
                {
                    log.LogWarningWithCodeFromResources("ResolveComReference.CannotAccessTypeLibName", typeLibId, ex.Message);
                }
                typeLibName = GetTypeLibNameFromDocumentation(typeLib);
                return true;
            }

            return true;
        }

        /// <summary>
        /// Gets the name of given type library.
        /// </summary>
        internal static unsafe bool GetTypeLibNameForTypeLibAttrs(TaskLoggingHelper log, bool silent, TLIBATTR typeLibAttr, out string typeLibName)
        {
            typeLibName = "";

            // load our type library
            using ComScope<ITypeLib> typeLib = new(null);
            HRESULT hr = PInvoke.LoadRegTypeLib(typeLibAttr.guid, typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum, typeLibAttr.lcid, typeLib);
            if (hr.Failed)
            {
                if (!silent)
                {
                    log.LogWarningWithCodeFromResources("ResolveComReference.CannotLoadTypeLib", typeLibAttr.guid, typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum, Marshal.GetExceptionForHR((int)hr)?.Message);
                }

                return false;
            }

            string typeLibId = log.FormatResourceString("ResolveComReference.TypeLibAttrId", typeLibAttr.guid.ToString(), typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum);

            return GetTypeLibNameForITypeLib(log, silent, typeLib, typeLibId, out typeLibName);
        }

        /// <summary>
        /// Strips type library number from a type library path (for example, "ref.dll\2" becomes "ref.dll")
        /// </summary>
        /// <param name="typeLibPath">type library path with possible typelib number appended to it</param>
        /// <param name="fileExists">Delegate to check whether the file exists</param>
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
                HMODULE libraryHandle = PInvoke.LoadLibrary(typeLibPath);
                if (!libraryHandle.IsNull)
                {
                    try
                    {
                        typeLibPath = GetModuleFileName(libraryHandle);
                    }
                    finally
                    {
                        PInvoke.FreeLibrary(libraryHandle);
                    }
                }
                else
                {
                    typeLibPath = "";
                }
            }

            return typeLibPath;
        }

        private static string GetModuleFileName(HMODULE handle)
        {
            using BufferScope<char> buffer = new(stackalloc char[(int)PInvoke.MAX_PATH]);

            // Try increased buffer sizes if on longpath-enabled Windows
            for (int bufferSize = (int)PInvoke.MAX_PATH; bufferSize <= NativeMethodsShared.MaxPath; bufferSize *= 2)
            {
                buffer.EnsureCapacity(bufferSize);

                int pathLength = (int)PInvoke.GetModuleFileName(handle, buffer.AsSpan());

                // GetModuleFileName returns bufferSize when truncated; treat that as "buffer too small".
                if (pathLength != 0 && pathLength < bufferSize)
                {
                    return buffer.Slice(0, pathLength).ToString();
                }

                // Double check that the buffer is not insanely big
                Assumed.LessThanOrEqual(bufferSize, int.MaxValue / 2, "Buffer size approaching int.MaxValue");
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the type lib path for given type lib attributes(reused almost verbatim from vsdesigner utils code)
        /// NOTE:  If there's a typelib number at the end of the path, does NOT strip it.
        /// </summary>
        internal static bool GetPathOfTypeLib(TaskLoggingHelper log, bool silent, ref TLIBATTR typeLibAttr, out string typeLibPath)
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
                PInvoke.QueryPathOfRegTypeLib(
                    typeLibAttr.guid,
                    typeLibAttr.wMajorVerNum,
                    typeLibAttr.wMinorVerNum,
                    typeLibAttr.lcid,
                    out BSTR path).ThrowOnFailure();

                // BSTR is IDisposable: SysFreeString runs on scope exit. On failure no path is
                // returned (ThrowOnFailure throws first), so there is nothing to free.
                using (path)
                {
                    typeLibPath = path.ToString();
                }

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

            if (!string.IsNullOrEmpty(typeLibPath))
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
        internal static bool RemapAdoTypeLib(TaskLoggingHelper log, bool silent, ref TLIBATTR typeLibAttr)
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

#endif
