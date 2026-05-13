// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

using System.Text;
using System.Reflection;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
#if !NET
using System.Linq;
#endif
#if FEATURE_HANDLEPROCESSCORRUPTEDSTATEEXCEPTIONS
using System.Runtime.ExceptionServices;
#endif
using System.Text.RegularExpressions;
using System.Runtime.Versioning;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    [GuidAttribute("00020406-0000-0000-C000-000000000046")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface ICreateTypeLib
    {
        void CreateTypeInfo();
        void SetName();
        void SetVersion();
        void SetGuid();
        void SetDocString();
        void SetHelpFileName();
        void SetHelpContext();
        void SetLcid();
        void SetLibFlags();
        void SaveAllChanges();
    }

    [ComImport]
    [Guid("E5CB7A31-7512-11d2-89CE-0080C792E5D8")]
#if !NETSTANDARD2_0_OR_GREATER // NS2.0 doesn't have COM so this can't appear in the ref assembly
    [TypeLibType(TypeLibTypeFlags.FCanCreate)]
#endif
    [ClassInterface(ClassInterfaceType.None)]
    internal class CorMetaDataDispenser
    {
    }

    [ComImport]
    [Guid("809c652e-7396-11d2-9771-00a0c9b4d50c")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown /*0x0001*/)]
#if !NETSTANDARD2_0_OR_GREATER // NS2.0 doesn't have COM so this can't appear in the ref assembly
    [TypeLibType(TypeLibTypeFlags.FRestricted /*0x0200*/)]
#endif
    internal interface IMetaDataDispenser
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        object DefineScope([In] ref Guid rclsid, [In] UInt32 dwCreateFlags, [In] ref Guid riid);

        [return: MarshalAs(UnmanagedType.Interface)]
        object OpenScope([In][MarshalAs(UnmanagedType.LPWStr)] string szScope, [In] UInt32 dwOpenFlags, [In] ref Guid riid);

        [return: MarshalAs(UnmanagedType.Interface)]
        object OpenScopeOnMemory([In] IntPtr pData, [In] UInt32 cbData, [In] UInt32 dwOpenFlags, [In] ref Guid riid);
    }

    [ComImport]
    [Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMetaDataImport
    {
        // PreserveSig because this method is an exception that
        // actually returns void, not HRESULT.
        [PreserveSig]
        void CloseEnum();
        void CountEnum(IntPtr iRef, ref UInt32 ulCount);
        void ResetEnum();
        void EnumTypeDefs();
        void EnumInterfaceImpls();
        void EnumTypeRefs();
        void FindTypeDefByName();
        void GetScopeProps();
        void GetModuleFromScope();
        void GetTypeDefProps();
        void GetInterfaceImplProps();
        void GetTypeRefProps();
        void ResolveTypeRef();
        void EnumMembers();
        void EnumMembersWithName();
        void EnumMethods();
        void EnumMethodsWithName();
        void EnumFields();
        void EnumFieldsWithName();
        void EnumParams();
        void EnumMemberRefs();
        void EnumMethodImpls();
        void EnumPermissionSets();
        void FindMember();
        void FindMethod();
        void FindField();
        void FindMemberRef();
        void GetMethodProps();
        void GetMemberRefProps();
        void EnumProperties();
        void EnumEvents();
        void GetEventProps();
        void EnumMethodSemantics();
        void GetMethodSemantics();
        void GetClassLayout();
        void GetFieldMarshal();
        void GetRVA();
        void GetPermissionSetProps();
        void GetSigFromToken();
        void GetModuleRefProps();
        void EnumModuleRefs();
        void GetTypeSpecFromToken();
        void GetNameFromToken();
        void EnumUnresolvedMethods();
        void GetUserString();
        void GetPinvokeMap();
        void EnumSignatures();
        void EnumTypeSpecs();
        void EnumUserStrings();
        void GetParamForMethodIndex();
        void EnumCustomAttributes();
        void GetCustomAttributeProps();
        void FindTypeRef();
        void GetMemberProps();
        void GetFieldProps();
        void GetPropertyProps();
        void GetParamProps();
        void GetCustomAttributeByName();
        void IsValidToken();  // Note: Need preservesig for this if ever going to be used.
        void GetNestedClassProps();
        void GetNativeCallConvFromSig();
        void IsGlobal();
    }

    [ComImport]
    [Guid("FCE5EFA0-8BBA-4f8e-A036-8F2022B08466")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMetaDataImport2
    {
        void CloseEnum();
        void CountEnum();
        void ResetEnum();
        void EnumTypeDefs();
        void EnumInterfaceImpls();
        void EnumTypeRefs();
        void FindTypeDefByName();
        void GetScopeProps();
        void GetModuleFromScope();
        void GetTypeDefProps();
        void GetInterfaceImplProps();
        void GetTypeRefProps();
        void ResolveTypeRef();
        void EnumMembers();
        void EnumMembersWithName();
        void EnumMethods();
        void EnumMethodsWithName();
        void EnumFields();
        void EnumFieldsWithName();
        void EnumParams();
        void EnumMemberRefs();
        void EnumMethodImpls();
        void EnumPermissionSets();
        void FindMember();
        void FindMethod();
        void FindField();
        void FindMemberRef();
        void GetMethodProps();
        void GetMemberRefProps();
        void EnumProperties();
        void EnumEvents();
        void GetEventProps();
        void EnumMethodSemantics();
        void GetMethodSemantics();
        void GetClassLayout();
        void GetFieldMarshal();
        void GetRVA();
        void GetPermissionSetProps();
        void GetSigFromToken();
        void GetModuleRefProps();
        void EnumModuleRefs();
        void GetTypeSpecFromToken();
        void GetNameFromToken();
        void EnumUnresolvedMethods();
        void GetUserString();
        void GetPinvokeMap();
        void EnumSignatures();
        void EnumTypeSpecs();
        void EnumUserStrings();
        void GetParamForMethodIndex();
        void EnumCustomAttributes();
        void GetCustomAttributeProps();
        void FindTypeRef();
        void GetMemberProps();
        void GetFieldProps();
        void GetPropertyProps();
        void GetParamProps();
        [PreserveSig]
        int GetCustomAttributeByName(UInt32 mdTokenObj, [MarshalAs(UnmanagedType.LPWStr)] string szName, out IntPtr ppData, out uint pDataSize);
        void IsValidToken();
        void GetNestedClassProps();
        void GetNativeCallConvFromSig();
        void IsGlobal();
        void EnumGenericParams();
        void GetGenericParamProps();
        void GetMethodSpecProps();
        void EnumGenericParamConstraints();
        void GetGenericParamConstraintProps();
        void GetPEKind(out UInt32 pdwPEKind, out UInt32 pdwMachine);
        void GetVersionString([MarshalAs(UnmanagedType.LPArray)] char[] pwzBuf, UInt32 ccBufSize, out UInt32 pccBufSize);
    }

    // Flags for OpenScope
    internal enum CorOpenFlags
    {
        ofRead = 0x00000000,     // Open scope for read
        ofWrite = 0x00000001,     // Open scope for write.
        ofCopyMemory = 0x00000002,     // Open scope with memory. Ask metadata to maintain its own copy of memory.
        ofCacheImage = 0x00000004,     // EE maps but does not do relocations or verify image
        ofNoTypeLib = 0x00000080,     // Don't OpenScope on a typelib.
    };

    [ComImport]
    [Guid("EE62470B-E94B-424e-9B7C-2F00C9249F93")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMetaDataAssemblyImport
    {
        void GetAssemblyProps(UInt32 mdAsm, out IntPtr pPublicKeyPtr, out UInt32 ucbPublicKeyPtr, out UInt32 uHashAlg, [MarshalAs(UnmanagedType.LPArray)] char[] strName, UInt32 cchNameIn, out UInt32 cchNameRequired, IntPtr amdInfo, out UInt32 dwFlags);
        void GetAssemblyRefProps(UInt32 mdAsmRef, out IntPtr ppbPublicKeyOrToken, out UInt32 pcbPublicKeyOrToken, [MarshalAs(UnmanagedType.LPArray)] char[] strName, UInt32 cchNameIn, out UInt32 pchNameOut, IntPtr amdInfo, out IntPtr ppbHashValue, out UInt32 pcbHashValue, out UInt32 pdwAssemblyRefFlags);
        void GetFileProps([In] UInt32 mdFile, [MarshalAs(UnmanagedType.LPArray)] char[] strName, UInt32 cchName, out UInt32 cchNameRequired, out IntPtr bHashData, out UInt32 cchHashBytes, out UInt32 dwFileFlags);
        void GetExportedTypeProps();
        void GetManifestResourceProps();
        void EnumAssemblyRefs([In, Out] ref IntPtr phEnum, [MarshalAs(UnmanagedType.LPArray), Out] UInt32[] asmRefs, UInt32 asmRefCount, out UInt32 iFetched);
        void EnumFiles([In, Out] ref IntPtr phEnum, [MarshalAs(UnmanagedType.LPArray), Out] UInt32[] fileRefs, UInt32 fileRefCount, out UInt32 iFetched);
        void EnumExportedTypes();
        void EnumManifestResources();
        void GetAssemblyFromScope(out UInt32 mdAsm);
        void FindExportedTypeByName();
        void FindManifestResourceByName();
        // PreserveSig because this method is an exception that
        // actually returns void, not HRESULT.
        [PreserveSig]
        void CloseEnum([In] IntPtr phEnum);
        void FindAssembliesByName();
    }

    [ComImport]
    [Guid("00000001-0000-0000-c000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IClassFactory
    {
        void CreateInstance([MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown), Out] out object ppvObject);
        void LockServer(bool fLock);
    }

    // Subset of CorAssemblyFlags from corhdr.h
    internal enum CorAssemblyFlags : uint
    {
        afPublicKey = 0x0001,            // The assembly ref holds the full (unhashed) public key.
        afRetargetable = 0x0100            // The assembly can be retargeted (at runtime) to an
                                           //  assembly from a different publisher.
    };

    /*
    From cor.h:
        typedef struct
        {
            USHORT      usMajorVersion;         // Major Version.
            USHORT      usMinorVersion;         // Minor Version.
            USHORT      usBuildNumber;          // Build Number.
            USHORT      usRevisionNumber;       // Revision Number.
            LPWSTR      szLocale;               // Locale.
            ULONG       cbLocale;               // [IN/OUT] Size of the buffer in wide chars/Actual size.
            DWORD       *rProcessor;            // Processor ID array.
            ULONG       ulProcessor;            // [IN/OUT] Size of the Processor ID array/Actual # of entries filled in.
            OSINFO      *rOS;                   // OSINFO array.
            ULONG       ulOS;                   // [IN/OUT]Size of the OSINFO array/Actual # of entries filled in.
        } ASSEMBLYMETADATA;
    */
    [StructLayout(LayoutKind.Sequential)]
    internal struct ASSEMBLYMETADATA
    {
        public UInt16 usMajorVersion;
        public UInt16 usMinorVersion;
        public UInt16 usBuildNumber;
        public UInt16 usRevisionNumber;
        public IntPtr rpLocale;
        public UInt32 cchLocale;
        public IntPtr rpProcessors;
        public UInt32 cProcessors;
        public IntPtr rOses;
        public UInt32 cOses;
    }

    internal enum ASSEMBLYINFO_FLAG
    {
        VALIDATE = 1,
        GETSIZE = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ASSEMBLY_INFO
    {
        public uint cbAssemblyInfo;
        public uint dwAssemblyFlags;
        public ulong uliAssemblySizeInKB;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pszCurrentAssemblyPathBuf;
        public uint cchBuf;
    }

    [ComImport]
    [Guid("E707DCDE-D1CD-11D2-BAB9-00C04F8ECEAE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAssemblyCache
    {
        /* Unused.
        [PreserveSig]
        int UninstallAssembly(uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] string pszAssemblyName, IntPtr pvReserved, int pulDisposition);
         */
        int UninstallAssembly();

        [PreserveSig]
        uint QueryAssemblyInfo(uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] string pszAssemblyName, ref ASSEMBLY_INFO pAsmInfo);

        /* Unused.
        [PreserveSig]
        int CreateAssemblyCacheItem(uint dwFlags, IntPtr pvReserved, out object ppAsmItem, [MarshalAs(UnmanagedType.LPWStr)] string pszAssemblyName);
         */
        int CreateAssemblyCacheItem();

        /* Unused.
        [PreserveSig]
        int CreateAssemblyScavenger(out object ppAsmScavenger);
         */
        int CreateAssemblyScavenger();

        /* Unused.
        [PreserveSig]
        int InstallAssembly(uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] string pszManifestFilePath, IntPtr pvReserved);
         */
        int InstallAssembly();
    }

    [Flags]
    internal enum AssemblyCacheFlags
    {
        ZAP = 1,
        GAC = 2,
        DOWNLOAD = 4
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("CD193BC0-B4BC-11d2-9833-00C04FC31D2E")]
    internal interface IAssemblyName
    {
        [PreserveSig]
        int SetProperty(
                int PropertyId,
                IntPtr pvProperty,
                int cbProperty);

        [PreserveSig]
        int GetProperty(
                int PropertyId,
                IntPtr pvProperty,
                ref int pcbProperty);

        [PreserveSig]
        int Finalize();

        [PreserveSig]
        int GetDisplayName(
                StringBuilder pDisplayName,
                ref int pccDisplayName,
                int displayFlags);

        [PreserveSig]
        int Reserved(ref Guid guid,
            Object obj1,
            Object obj2,
            String string1,
            Int64 llFlags,
            IntPtr pvReserved,
            int cbReserved,
            out IntPtr ppv);

        [PreserveSig]
        int GetName(
                ref int pccBuffer,
                StringBuilder pwzName);

        [PreserveSig]
        int GetVersion(
                out int versionHi,
                out int versionLow);
        [PreserveSig]
        int IsEqual(
                IAssemblyName pAsmName,
                int cmpFlags);

        [PreserveSig]
        int Clone(out IAssemblyName pAsmName);
    }// IAssemblyName

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("21b8916c-f28e-11d2-a473-00c04f8ef448")]
    internal interface IAssemblyEnum
    {
        [PreserveSig]
        int GetNextAssembly(
                IntPtr pvReserved,
                out IAssemblyName ppName,
                int flags);
        [PreserveSig]
        int Reset();
        [PreserveSig]
        int Clone(out IAssemblyEnum ppEnum);
    }// IAssemblyEnum

    internal enum CreateAssemblyNameObjectFlags
    {
        CANOF_DEFAULT = 0,
        CANOF_PARSE_DISPLAY_NAME = 1,
    }

    [Flags]
    internal enum AssemblyNameDisplayFlags
    {
        VERSION = 0x01,
        CULTURE = 0x02,
        PUBLIC_KEY_TOKEN = 0x04,
        PROCESSORARCHITECTURE = 0x20,
        RETARGETABLE = 0x80,
        // This enum will change in the future to include
        // more attributes.
        ALL = VERSION
                                    | CULTURE
                                    | PUBLIC_KEY_TOKEN
                                    | PROCESSORARCHITECTURE
                                    | RETARGETABLE
    }

    /// <summary>
    /// Interop methods.
    /// </summary>
    internal static partial class NativeMethods
    {
        #region Constants

        internal static readonly IntPtr NullPtr = IntPtr.Zero;

        internal const int ERROR_SUCCESS = 0;

        internal const int TYPE_E_REGISTRYACCESS = -2147319780;
        internal const int TYPE_E_CANTLOADLIBRARY = -2147312566;

        internal const int HRESULT_E_CLASSNOTREGISTERED = -2147221164;

        internal const int ERROR_INVALID_FILENAME = -2147024773; // Illegal characters in name
        internal const int ERROR_ACCESS_DENIED = -2147024891; // ACL'd or r/o
        internal const int ERROR_SHARING_VIOLATION = -2147024864; // File locked by another use

        internal static Guid GUID_TYPELIB_NAMESPACE = new Guid("{0F21F359-AB84-41E8-9A78-36D110E6D2F9}");
        internal static Guid GUID_ExportedFromComPlus = new Guid("{90883f05-3d28-11d2-8f17-00a0c9a6186d}");

        internal static Guid IID_IUnknown = new Guid("{00000000-0000-0000-C000-000000000046}");
        internal static Guid IID_IDispatch = new Guid("{00020400-0000-0000-C000-000000000046}");
        internal static Guid IID_ITypeInfo = new Guid("{00020401-0000-0000-C000-000000000046}");
        internal static Guid IID_IEnumVariant = new Guid("{00020404-0000-0000-C000-000000000046}");
        internal static Guid IID_IDispatchEx = new Guid("{A6EF9860-C720-11D0-9337-00A0C90DCAA9}");

        internal static Guid IID_StdOle = new Guid("{00020430-0000-0000-C000-000000000046}");

        // used in LoadTypeLibEx
        internal enum REGKIND
        {
            REGKIND_DEFAULT = 0,
            REGKIND_REGISTER = 1,
            REGKIND_NONE = 2,
            REGKIND_LOAD_TLB_AS_32BIT = 0x20,
            REGKIND_LOAD_TLB_AS_64BIT = 0x40,
        }

        // Set of IMAGE_FILE constants which represent the processor architectures for native assemblies.
        internal const UInt16 IMAGE_FILE_MACHINE_UNKNOWN = 0x0; // The contents of this field are assumed to be applicable to any machine type
        internal const UInt16 IMAGE_FILE_MACHINE_INVALID = UInt16.MaxValue; // Invalid value for the machine type.
        internal const UInt16 IMAGE_FILE_MACHINE_AMD64 = 0x8664; // x64
        internal const UInt16 IMAGE_FILE_MACHINE_ARM = 0x1c0; // ARM little endian
        internal const UInt16 IMAGE_FILE_MACHINE_ARMV7 = 0x1c4; // ARMv7 (or higher) Thumb mode only
        internal const UInt16 IMAGE_FILE_MACHINE_I386 = 0x14c; // Intel 386 or later processors and compatible processors
        internal const UInt16 IMAGE_FILE_MACHINE_IA64 = 0x200; // Intel Itanium processor family
        internal const UInt16 IMAGE_FILE_MACHINE_ARM64 = 0xAA64; // ARM64 Little-Endian
        internal const UInt16 IMAGE_FILE_MACHINE_R4000 = 0x166; // Used to test a architecture we do not expect to reference

        internal const int SE_ERR_ACCESSDENIED = 5;

        [Flags]
        internal enum MoveFileFlags
        {
            MOVEFILE_REPLACE_EXISTING = 0x00000001,
            MOVEFILE_COPY_ALLOWED = 0x00000002,
            MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004,
            MOVEFILE_WRITE_THROUGH = 0x00000008,
            MOVEFILE_CREATE_HARDLINK = 0x00000010,
            MOVEFILE_FAIL_IF_NOT_TRACKABLE = 0x00000020
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CRYPTOAPI_BLOB
        {
            internal uint cbData;
            internal IntPtr pbData;
        }

        #endregion

        #region PInvoke

        //------------------------------------------------------------------------------
        // CreateHardLink
        //------------------------------------------------------------------------------
        [DllImport("libc", SetLastError = true)]
        internal static extern int link(string oldpath, string newpath);

        internal static bool MakeHardLink(string newFileName, string exitingFileName, ref string errorMessage, TaskLoggingHelper log)
        {
            bool hardLinkCreated;
            if (NativeMethodsShared.IsWindows)
            {
#if FEATURE_WINDOWSINTEROP
                hardLinkCreated = Windows.Win32.PInvoke.CreateHardLink(newFileName, exitingFileName);
                errorMessage = hardLinkCreated ? null : Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
#else
                hardLinkCreated = false;
                errorMessage = "CreateHardLink is not supported in this build (FEATURE_WINDOWSINTEROP is disabled).";
#endif
            }
            else
            {
                hardLinkCreated = link(exitingFileName, newFileName) == 0;
                errorMessage = hardLinkCreated ? null : log.FormatResourceString("Copy.NonWindowsLinkErrorMessage", "link()", Marshal.GetLastWin32Error());
            }

            return hardLinkCreated;
        }

        //------------------------------------------------------------------------------
        // MoveFileEx
        //------------------------------------------------------------------------------
#if FEATURE_WINDOWSINTEROP
        [SupportedOSPlatform("windows5.1.2600")]
        internal static bool MoveFileExWindows(string existingFileName, string newFileName, MoveFileFlags flags)
            => Windows.Win32.PInvoke.MoveFileEx(existingFileName, newFileName, (Windows.Win32.Storage.FileSystem.MOVE_FILE_FLAGS)flags);
#else
        internal static bool MoveFileExWindows(string existingFileName, string newFileName, MoveFileFlags flags) => false;
#endif

        /// <summary>
        /// Add implementation of this function when not running on windows. The implementation is
        /// not complete, of course, but should work for most common cases.
        /// </summary>
        /// <param name="existingFileName"></param>
        /// <param name="newFileName"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        internal static bool MoveFileEx(AbsolutePath existingFileName, AbsolutePath newFileName, MoveFileFlags flags)
        {
            if (NativeMethodsShared.IsWindows)
            {
                return MoveFileExWindows(existingFileName, newFileName, flags);
            }

            if (!FileSystems.Default.FileExists(existingFileName))
            {
                return false;
            }

            var targetExists = FileSystems.Default.FileExists(newFileName);

            if (targetExists
                && ((flags & MoveFileFlags.MOVEFILE_REPLACE_EXISTING) != MoveFileFlags.MOVEFILE_REPLACE_EXISTING))
            {
                return false;
            }

            if (targetExists && (File.GetAttributes(newFileName) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                throw new IOException("Moving target is read-only");
            }

            if (existingFileName == newFileName)
            {
                return true;
            }

            if (targetExists)
            {
                File.Delete(newFileName);
            }

            File.Move(existingFileName, newFileName);
            return true;
        }

        //------------------------------------------------------------------------------
        // RegisterTypeLib
        //------------------------------------------------------------------------------
        [DllImport("oleaut32", PreserveSig = false, EntryPoint = "RegisterTypeLib")]
        internal static extern void RegisterTypeLib([In, MarshalAs(UnmanagedType.Interface)] object pTypeLib, [In, MarshalAs(UnmanagedType.LPWStr)] string szFullPath, [In, MarshalAs(UnmanagedType.LPWStr)] string szHelpDir);

        //------------------------------------------------------------------------------
        // UnRegisterTypeLib
        //------------------------------------------------------------------------------
        [DllImport("oleaut32", PreserveSig = false, EntryPoint = "UnRegisterTypeLib")]
        internal static extern void UnregisterTypeLib(
            [In] ref Guid guid,
            [In] short wMajorVerNum,
            [In] short wMinorVerNum,
            [In] int lcid,
            [In] System.Runtime.InteropServices.ComTypes.SYSKIND syskind);

        //------------------------------------------------------------------------------
        // LoadTypeLib
        //------------------------------------------------------------------------------
        [DllImport("oleaut32", PreserveSig = false, EntryPoint = "LoadTypeLibEx")]
        [return: MarshalAs(UnmanagedType.Interface)]
        internal static extern object LoadTypeLibEx([In, MarshalAs(UnmanagedType.LPWStr)] string szFullPath, [In] int regKind);

        //------------------------------------------------------------------------------
        // LoadRegTypeLib
        //------------------------------------------------------------------------------
        [DllImport("oleaut32", PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        internal static extern object LoadRegTypeLib([In] ref Guid clsid, [In] short majorVersion, [In] short minorVersion, [In] int lcid);

        //------------------------------------------------------------------------------
        // QueryPathOfRegTypeLib
        //------------------------------------------------------------------------------
        [DllImport("oleaut32", PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.BStr)]
        internal static extern string QueryPathOfRegTypeLib([In] ref Guid clsid, [In] short majorVersion, [In] short minorVersion, [In] int lcid);

        internal static bool AllDrivesMapped()
        {
#if FEATURE_WINDOWSINTEROP
            const uint AllDriveMask = 0x0cffffff;
            if (NativeMethodsShared.IsWindows)
            {
                var driveMask = Windows.Win32.PInvoke.GetLogicalDrives();
                // All drives are taken if the value has all 26 bits set
                return driveMask >= AllDriveMask;
            }
#endif

            return false;
        }

        //------------------------------------------------------------------------------
        // CreateAssemblyCache
        //------------------------------------------------------------------------------
        [DllImport("fusion.dll")]
        [SupportedOSPlatform("windows")]
        internal static extern uint CreateAssemblyCache(out IAssemblyCache ppAsmCache, uint dwReserved);

        [DllImport("fusion.dll")]
        internal static extern int CreateAssemblyEnum(
                out IAssemblyEnum ppEnum,
                IntPtr pUnkReserved,
                IAssemblyName pName,
                AssemblyCacheFlags flags,
                IntPtr pvReserved);

        [DllImport("fusion.dll")]
        [SupportedOSPlatform("windows")]
        internal static extern int CreateAssemblyNameObject(
                out IAssemblyName ppAssemblyNameObj,
                [MarshalAs(UnmanagedType.LPWStr)]
                String szAssemblyName,
                CreateAssemblyNameObjectFlags flags,
                IntPtr pvReserved);

        /// <summary>
        /// GetCachePath from fusion.dll.
        /// A common design pattern in unmanaged C++ is calling a function twice, once to determine the length of the string
        /// and then again to pass the client-allocated character buffer.
        /// </summary>
        /// <param name="cacheFlags">Value that indicates the source of the cached assembly.</param>
        /// <param name="cachePath">The returned pointer to the path.</param>
        /// <param name="pcchPath">The requested maximum length of CachePath, and upon return, the actual length of CachePath.</param>
        ///
        [DllImport("fusion.dll", CharSet = CharSet.Unicode)]
        [SupportedOSPlatform("windows")]
        internal static extern unsafe int GetCachePath(AssemblyCacheFlags cacheFlags, [Out] char* cachePath, ref int pcchPath);

        //------------------------------------------------------------------------------
        // PFXImportCertStore
        //------------------------------------------------------------------------------
        // (Removed: dead crypt32/advapi32 P/Invokes had no call sites in the repo.)

        #endregion

        #region Methods
#if FEATURE_HANDLEPROCESSCORRUPTEDSTATEEXCEPTIONS
        /// <summary>
        /// Given a pointer to a metadata blob, read the string parameter from it.  Returns true if
        /// a valid string was constructed and false otherwise.
        ///
        /// Adapted from bizapps\server\designers\models\packagemodel\nativemethods.cs (TryReadStringArgument) and
        /// the original ARD implementation in vsproject\compsvcspkg\enumcomplus.cpp (GetStringCustomAttribute)
        /// This code was taken from the vsproject\ReferenceManager\Providers\NativeMethods.cs
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        internal static unsafe bool TryReadMetadataString(string fullPath, IntPtr attrData, uint attrDataSize, out string strValue)
        {
            int attrDataOffset = 0;
            strValue = null;

            try
            {
                // Blob structure for an attribute with a constructor receiving one string
                // and no named parameters:
                //
                //     [2 bytes] Prolog: unsigned int16 with value 0x0001
                //     [1, 2 or 4 bytes] PackedLen: Number of bytes of string parameter
                //     [PackedLen bytes] String parameter encoded as UTF8
                //     [1 byte] Name Parameter Count: Named parameter count equal to 0

                // Minimum size is 4-bytes (Prolog + PackedLen).  Prolog must be 0x0001.
                if ((attrDataSize >= 4) && (Marshal.ReadInt16(attrData, attrDataOffset) == 1))
                {
                    int preReadOffset = 2; // pass the prolog
                    IntPtr attrDataPostProlog = attrData + preReadOffset;

                    int strLen;
                    // Get the offset at which the uncompressed data starts, and the
                    // length of the uncompressed data.
                    attrDataOffset = CorSigUncompressData(attrDataPostProlog, out strLen);

                    if (strLen != -1)
                    {
                        // the full size of the blob we were passed in should be sufficient to
                        // cover the prolog, compressed string length, and actual string.
                        if (attrDataSize >= preReadOffset + attrDataOffset + strLen)
                        {
                            // Read in the uncompressed data
                            byte[] bytes = new byte[(int)strLen];
                            int i;
                            for (i = 0; i < strLen; i++)
                            {
                                bytes[i] = Marshal.ReadByte(attrDataPostProlog, attrDataOffset + i);
                            }

                            // And convert it to the output string.
                            strValue = Encoding.UTF8.GetString(bytes);
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
            }
            catch (AccessViolationException)
            {
                // The Marshal.ReadXXXX functions throw AVs when they're fed an invalid pointer, and very occasionally,
                // for some reason, on what seem to be otherwise perfectly valid assemblies (it must be
                // intermittent given that otherwise the user would be completely unable to use the reference
                // manager), the pointer that we generate to look up the AssemblyTitle is apparently invalid,
                // or for some reason Marshal.ReadByte thinks it is.
                //
                return false;
            }

            return strValue != null;
        }
#endif
        /// <summary>
        /// Returns the number of bytes that compressed data -- the length of the uncompressed
        /// data -- takes up, and has an out value of the length of the string.
        ///
        /// Decompression algorithm stolen from ndp\clr\src\toolbox\mdbg\corapi\metadata\cormetadata.cs, which
        /// was translated from the base implementation in ndp\clr\src\inc\cor.h
        /// This code was taken from the vsproject\ReferenceManager\Providers\NativeMethods.cs
        /// </summary>
        /// <param name="data">Pointer to the beginning of the data block</param>
        /// <param name="uncompressedDataLength">Length of the uncompressed data block</param>
        internal static unsafe int CorSigUncompressData(IntPtr data, out int uncompressedDataLength)
        {
            // As described in bizapps\server\designers\models\packagemodel\nativemethods.cs:
            // The maximum encodable integer is 29 bits long, 0x1FFFFFFF. The compression algorithm used is as follows (bit 0 is the least significant bit):
            // - If the value lies between 0 (0x00) and 127 (0x7F), inclusive, encode as a one-byte integer (bit 7 is clear, value held in bits 6 through 0)
            // - If the value lies between 2^8 (0x80) and 2^14 - 1 (0x3FFF), inclusive, encode as a 2-byte integer with bit 15 set, bit 14 clear (value held in bits 13 through 0)
            // - Otherwise, encode as a 4-byte integer, with bit 31 set, bit 30 set, bit 29 clear (value held in bits 28 through 0)
            // - A null string should be represented with the reserved single byte 0xFF, and no following data
            int count = -1;
            byte* bytes = (byte*)(data);
            uncompressedDataLength = 0;

            // Smallest.
            if ((*bytes & 0x80) == 0x00)       // 0??? ????
            {
                uncompressedDataLength = *bytes;
                count = 1;
            }
            // Medium.
            else if ((*bytes & 0xC0) == 0x80)  // 10?? ????
            {
                uncompressedDataLength = (int)((*bytes & 0x3f) << 8 | *(bytes + 1));
                count = 2;
            }
            else if ((*bytes & 0xE0) == 0xC0)      // 110? ????
            {
                uncompressedDataLength = (int)((*bytes & 0x1f) << 24 | *(bytes + 1) << 16 | *(bytes + 2) << 8 | *(bytes + 3));
                count = 4;
            }

            return count;
        }
        #endregion
        #region InternalClass
        /// <summary>
        /// This class is a wrapper over the native GAC enumeration API.
        /// </summary>
        [ComVisible(false)]
        internal partial class AssemblyCacheEnum : IEnumerable<AssemblyNameExtension>
        {
            /// <summary>
            /// Path to the gac
            /// </summary>
            private static readonly string s_gacPath = Path.Combine(NativeMethodsShared.FrameworkBasePath, "gac");

            private const string AssemblyVersionPattern = @"^([.\d]+)_([^_]*)_([a-fA-F\d]{16})$";

            /// <summary>
            /// Regex for directory version parsing
            /// </summary>
#if NET
            [GeneratedRegex(AssemblyVersionPattern, RegexOptions.CultureInvariant)]
            private static partial Regex AssemblyVersionRegex { get; }
#else
            private static Regex AssemblyVersionRegex { get; } = new Regex(AssemblyVersionPattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
#endif

            /// <summary>
            /// The IAssemblyEnum interface which allows us to ask for the next assembly from the GAC enumeration.
            /// </summary>
            private IAssemblyEnum _assemblyEnum;

            /// <summary>
            /// For non-Windows implementation, we need assembly name
            /// </summary>
            private AssemblyName _assemblyNameVersion;

            /// <summary>
            /// For non-Windows implementation, we need assembly name
            /// </summary>
            private IEnumerable<string> _gacDirectories;

            /// <summary>
            /// Are we done going through the enumeration.
            /// </summary>
            private bool _done;

            // null means enumerate all the assemblies
            internal AssemblyCacheEnum(String assemblyName)
            {
                InitializeEnum(assemblyName);
            }

            /// <summary>
            /// Initialize the GAC Enum
            /// </summary>
            /// <param name="assemblyName"></param>
            private void InitializeEnum(String assemblyName)
            {
                if (NativeMethodsShared.IsWindows)
                {
                    IAssemblyName fusionName = null;

                    int hr = 0;
                    try
                    {
                        if (assemblyName != null)
                        {
                            hr = CreateAssemblyNameObject(
                                out fusionName,
                                assemblyName,
                                CreateAssemblyNameObjectFlags.CANOF_PARSE_DISPLAY_NAME
                                /* parse components assuming the assemblyName is a fusion name, this does not have to be a full fusion name*/,
                                IntPtr.Zero);
                        }

                        if (hr >= 0)
                        {
                            hr = CreateAssemblyEnum(
                                out _assemblyEnum,
                                IntPtr.Zero,
                                fusionName,
                                AssemblyCacheFlags.GAC,
                                IntPtr.Zero);
                        }
                    }
                    catch (Exception e)
                    {
                        hr = e.HResult;
                    }

                    if (hr < 0)
                    {
                        _assemblyEnum = null;
                    }
                }
                else
                {
                    if (FileSystems.Default.DirectoryExists(s_gacPath))
                    {
                        if (!string.IsNullOrWhiteSpace(assemblyName))
                        {
                            _assemblyNameVersion = new AssemblyName(assemblyName);
                            _gacDirectories = Directory.EnumerateDirectories(s_gacPath, _assemblyNameVersion.Name);
                        }
                        else
                        {
                            _gacDirectories = Directory.EnumerateDirectories(s_gacPath);
                        }
                    }
                    else
                    {
                        _gacDirectories = [];
                    }
                }
            }

            public IEnumerator<AssemblyNameExtension> GetEnumerator()
            {
                if (NativeMethodsShared.IsWindows)
                {
                    if (_assemblyEnum == null)
                    {
                        yield break;
                    }

                    if (_done)
                    {
                        yield break;
                    }

                    while (!_done)
                    {
                        // Now get next IAssemblyName from m_AssemblyEnum
                        int hr = _assemblyEnum.GetNextAssembly((IntPtr)0, out IAssemblyName fusionName, 0);

                        if (hr < 0)
                        {
                            Marshal.ThrowExceptionForHR(hr);
                        }

                        if (fusionName != null)
                        {
                            string assemblyFusionName = GetFullName(fusionName);
                            yield return new AssemblyNameExtension(assemblyFusionName);
                        }
                        else
                        {
                            _done = true;
                            yield break;
                        }
                    }
                }
                else
                {
                    foreach (var dir in _gacDirectories)
                    {
                        var assemblyName = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(assemblyName))
                        {
                            foreach (var version in Directory.EnumerateDirectories(dir))
                            {
                                var versionString = Path.GetFileName(version);
                                if (!string.IsNullOrWhiteSpace(versionString))
                                {
                                    var match = AssemblyVersionRegex.Match(versionString);
                                    if (match.Success)
                                    {
                                        var name = new AssemblyName
                                        {
                                            Name = assemblyName,
                                            CultureInfo =
                                                               !string.IsNullOrWhiteSpace(
                                                                   match.Groups[2].Value)
                                                                   ? new CultureInfo(
                                                                         match.Groups[2].Value)
                                                                   : CultureInfo.InvariantCulture
                                        };
                                        if (!string.IsNullOrEmpty(match.Groups[1].Value))
                                        {
                                            name.Version = new Version(match.Groups[1].Value);
                                        }
                                        if (!string.IsNullOrWhiteSpace(match.Groups[3].Value))
                                        {
                                            var value = match.Groups[3].Value;
                                            byte[] key =
#if NET
                                                Convert.FromHexString(value.AsSpan(0, 16));
#else
                                                Enumerable.Range(0, 16)
                                                .Where(x => x % 2 == 0)
                                                .Select(x => Convert.ToByte(value.Substring(x, 2), 16))
                                                .ToArray();
#endif
                                            name.SetPublicKeyToken(key);
                                        }

                                        yield return new AssemblyNameExtension(name);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            private static string GetFullName(IAssemblyName fusionAsmName)
            {
                int ilen = 1024;
                StringBuilder sDisplayName = new StringBuilder(ilen);
                int hr = fusionAsmName.GetDisplayName(sDisplayName, ref ilen, (int)AssemblyNameDisplayFlags.ALL);
                if (hr < 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                return sDisplayName.ToString();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public static string AssemblyPathFromStrongName(string strongName)
            {
                var assemblyNameVersion = new AssemblyName(strongName);
                var path = Path.Combine(s_gacPath, assemblyNameVersion.Name);

                // See if we can find the name as a directory in the GAC
                if (FileSystems.Default.DirectoryExists(path))
                {
                    // Since we have a strong name, create the path to the dll
                    path = Path.Combine(
                        path,
                        string.Format(
                            "{0}_{1}_{2}",
                            assemblyNameVersion.Version.ToString(4),
                            assemblyNameVersion.CultureName != "neutral" ? assemblyNameVersion.CultureName : string.Empty,
#if NET
                            Convert.ToHexStringLower(assemblyNameVersion.GetPublicKeyToken())),
#else
                            assemblyNameVersion.GetPublicKeyToken()
                                .Aggregate(new StringBuilder(), (builder, v) => builder.Append(v.ToString("x2")))),
#endif
                        assemblyNameVersion.Name + ".dll");

                    if (FileSystems.Default.FileExists(path))
                    {
                        return path;
                    }
                }

                return null;
            }
        }
        #endregion
    }
}
