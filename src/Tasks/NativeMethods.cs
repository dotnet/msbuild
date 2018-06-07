// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Tasks
{
#if FEATURE_COM_INTEROP
    /// <summary>
    /// The original ITypeInfo interface in the CLR has incorrect definitions for GetRefTypeOfImplType and GetRefTypeInfo.
    /// It uses ints for marshalling handles which will result in a crash on 64 bit systems. This is a temporary interface
    /// for use until the one in the CLR is fixed. When it is we can go back to using ITypeInfo.
    /// </summary>
    [Guid("00020401-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IFixedTypeInfo
    {
        void GetTypeAttr(out IntPtr ppTypeAttr);
        void GetTypeComp(out System.Runtime.InteropServices.ComTypes.ITypeComp ppTComp);
        void GetFuncDesc(int index, out IntPtr ppFuncDesc);
        void GetVarDesc(int index, out IntPtr ppVarDesc);
        void GetNames(int memid, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2), Out] String[] rgBstrNames, int cMaxNames, out int pcNames);
        void GetRefTypeOfImplType(int index, out IntPtr href);
        void GetImplTypeFlags(int index, out System.Runtime.InteropServices.ComTypes.IMPLTYPEFLAGS pImplTypeFlags);
        void GetIDsOfNames([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1), In] String[] rgszNames, int cNames, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1), Out] int[] pMemId);
        void Invoke([MarshalAs(UnmanagedType.IUnknown)] Object pvInstance, int memid, Int16 wFlags, ref System.Runtime.InteropServices.ComTypes.DISPPARAMS pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, out int puArgErr);
        void GetDocumentation(int index, out String strName, out String strDocString, out int dwHelpContext, out String strHelpFile);
        void GetDllEntry(int memid, System.Runtime.InteropServices.ComTypes.INVOKEKIND invKind, IntPtr pBstrDllName, IntPtr pBstrName, IntPtr pwOrdinal);
        void GetRefTypeInfo(IntPtr hRef, out IFixedTypeInfo ppTI);
        void AddressOfMember(int memid, System.Runtime.InteropServices.ComTypes.INVOKEKIND invKind, out IntPtr ppv);
        void CreateInstance([MarshalAs(UnmanagedType.IUnknown)] Object pUnkOuter, [In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown), Out] out Object ppvObj);
        void GetMops(int memid, out String pBstrMops);
        void GetContainingTypeLib(out System.Runtime.InteropServices.ComTypes.ITypeLib ppTLB, out int pIndex);
        [PreserveSig]
        void ReleaseTypeAttr(IntPtr pTypeAttr);
        [PreserveSig]
        void ReleaseFuncDesc(IntPtr pFuncDesc);
        [PreserveSig]
        void ReleaseVarDesc(IntPtr pVarDesc);
    }

    [GuidAttribute("00020406-0000-0000-C000-000000000046")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface UCOMICreateITypeLib
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
    [TypeLibType(TypeLibTypeFlags.FCanCreate)]
    [ClassInterface(ClassInterfaceType.None)]
    internal class CorMetaDataDispenser
    {
    }

    [ComImport]
    [Guid("809c652e-7396-11d2-9771-00a0c9b4d50c")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown /*0x0001*/)]
    [TypeLibType(TypeLibTypeFlags.FRestricted /*0x0200*/)]
    internal interface IMetaDataDispenser
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        object DefineScope([In] ref Guid rclsid, [In] UInt32 dwCreateFlags, [In] ref Guid riid);

        [return: MarshalAs(UnmanagedType.Interface)]
        object OpenScope([In][MarshalAs(UnmanagedType.LPWStr)]  string szScope, [In] UInt32 dwOpenFlags, [In] ref Guid riid);

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

    [ComImport, Guid("E707DCDE-D1CD-11D2-BAB9-00C04F8ECEAE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
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

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("CD193BC0-B4BC-11d2-9833-00C04FC31D2E")]
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

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("21b8916c-f28e-11d2-a473-00c04f8ef448")]
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

#endif

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct STARTUPINFO
    {
        internal Int32 cb;
        internal string lpReserved;
        internal string lpDesktop;
        internal string lpTitle;
        internal Int32 dwX;
        internal Int32 dwY;
        internal Int32 dwXSize;
        internal Int32 dwYSize;
        internal Int32 dwXCountChars;
        internal Int32 dwYCountChars;
        internal Int32 dwFillAttribute;
        internal Int32 dwFlags;
        internal Int16 wShowWindow;
        internal Int16 cbReserved2;
        internal IntPtr lpReserved2;
        internal IntPtr hStdInput;
        internal IntPtr hStdOutput;
        internal IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    internal enum SymbolicLink
    {
        File = 0,
        Directory = 1
    }

    /// <summary>
    /// Interop methods.
    /// </summary>
    internal static class NativeMethods
    {
        #region Constants

        internal static readonly IntPtr NullPtr = IntPtr.Zero;
        internal static readonly IntPtr InvalidIntPtr = new IntPtr(-1);

        internal const uint NORMAL_PRIORITY_CLASS = 0x0020;
        internal const uint CREATE_NO_WINDOW = 0x08000000;
        internal const Int32 STARTF_USESTDHANDLES = 0x00000100;
        internal const int ERROR_SUCCESS = 0;

        internal const int TYPE_E_REGISTRYACCESS = -2147319780;
        internal const int TYPE_E_CANTLOADLIBRARY = -2147312566;

        internal const int HRESULT_E_CLASSNOTREGISTERED = -2147221164;

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
        internal const UInt16 IMAGE_FILE_MACHINE_UNKNOWN = 0x0; //	The contents of this field are assumed to be applicable to any machine type
        internal const UInt16 IMAGE_FILE_MACHINE_INVALID = UInt16.MaxValue; // Invalid value for the machine type.
        internal const UInt16 IMAGE_FILE_MACHINE_AMD64 = 0x8664; //	x64
        internal const UInt16 IMAGE_FILE_MACHINE_ARM = 0x1c0; // ARM little endian
        internal const UInt16 IMAGE_FILE_MACHINE_ARMV7 = 0x1c4; // ARMv7 (or higher) Thumb mode only
        internal const UInt16 IMAGE_FILE_MACHINE_I386 = 0x14c; // Intel 386 or later processors and compatible processors
        internal const UInt16 IMAGE_FILE_MACHINE_IA64 = 0x200; // Intel Itanium processor family
        internal const UInt16 IMAGE_FILE_MACHINE_R4000 = 0x166; // Used to test a architecture we do not expect to reference

        internal const uint GENERIC_READ = 0x80000000;

        internal const uint PAGE_READONLY = 0x02;

        internal const uint FILE_MAP_READ = 0x04;

        internal const uint FILE_TYPE_DISK = 0x01;

        internal const int SE_ERR_ACCESSDENIED = 5;

        // CryptoApi flags and constants
        [Flags]
        internal enum CryptFlags
        {
            Exportable = 0x1,
            UserProtected = 0x2,
            MachineKeySet = 0x20,
            UserKeySet = 0x1000
        }

        internal enum KeySpec
        {
            AT_KEYEXCHANGE = 1,
            AT_SIGNATURE = 2
        }

        internal enum BlobType
        {
            SIMPLEBLOB = 0x1,
            PUBLICKEYBLOB = 0x6,
            PRIVATEKEYBLOB = 0x7,
            PLAINTEXTKEYBLOB = 0x8,
            OPAQUEKEYBLOB = 0x9,
            PUBLICKEYBLOBEX = 0xA,
            SYMMETRICWRAPKEYBLOB = 0xB,
        }

        [Flags]
        internal enum CertStoreClose
        {
            CERT_CLOSE_STORE_FORCE_FLAG = 0x00000001,
            CERT_CLOSE_STORE_CHECK_FLAG = 0x00000002,
        }

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

        #endregion

        #region NT header stuff

        internal const uint IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10b;
        internal const uint IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20b;

        internal const uint IMAGE_DIRECTORY_ENTRY_COMHEADER = 14;

        internal const uint COMIMAGE_FLAGS_STRONGNAMESIGNED = 0x08;

        [StructLayout(LayoutKind.Sequential)]
        internal struct IMAGE_FILE_HEADER
        {
            internal ushort Machine;
            internal ushort NumberOfSections;
            internal uint TimeDateStamp;
            internal uint PointerToSymbolTable;
            internal uint NumberOfSymbols;
            internal ushort SizeOfOptionalHeader;
            internal ushort Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IMAGE_DATA_DIRECTORY
        {
            internal uint VirtualAddress;
            internal uint Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IMAGE_OPTIONAL_HEADER32
        {
            internal ushort Magic;
            internal byte MajorLinkerVersion;
            internal byte MinorLinkerVersion;
            internal uint SizeOfCode;
            internal uint SizeOfInitializedData;
            internal uint SizeOfUninitializedData;
            internal uint AddressOfEntryPoint;
            internal uint BaseOfCode;
            internal uint BaseOfData;
            internal uint ImageBase;
            internal uint SectionAlignment;
            internal uint FileAlignment;
            internal ushort MajorOperatingSystemVersion;
            internal ushort MinorOperatingSystemVersion;
            internal ushort MajorImageVersion;
            internal ushort MinorImageVersion;
            internal ushort MajorSubsystemVersion;
            internal ushort MinorSubsystemVersion;
            internal uint Win32VersionValue;
            internal uint SizeOfImage;
            internal uint SizeOfHeaders;
            internal uint CheckSum;
            internal ushort Subsystem;
            internal ushort DllCharacteristics;
            internal uint SizeOfStackReserve;
            internal uint SizeOfStackCommit;
            internal uint SizeOfHeapReserve;
            internal uint SizeOfHeapCommit;
            internal uint LoaderFlags;
            internal uint NumberOfRvaAndSizes;

            // should be:
            // [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] internal IMAGE_DATA_DIRECTORY[] DataDirectory;
            // but fixed size arrays only work with simple types, so I have to use ulongs and convert them to IMAGE_DATA_DIRECTORY structs
            // Fortunately, IMAGE_DATA_DIRECTORY is only 8 bytes long... (whew)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            internal ulong[] DataDirectory;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IMAGE_OPTIONAL_HEADER64
        {
            internal ushort Magic;
            internal byte MajorLinkerVersion;
            internal byte MinorLinkerVersion;
            internal uint SizeOfCode;
            internal uint SizeOfInitializedData;
            internal uint SizeOfUninitializedData;
            internal uint AddressOfEntryPoint;
            internal uint BaseOfCode;
            internal ulong ImageBase;
            internal uint SectionAlignment;
            internal uint FileAlignment;
            internal ushort MajorOperatingSystemVersion;
            internal ushort MinorOperatingSystemVersion;
            internal ushort MajorImageVersion;
            internal ushort MinorImageVersion;
            internal ushort MajorSubsystemVersion;
            internal ushort MinorSubsystemVersion;
            internal uint Win32VersionValue;
            internal uint SizeOfImage;
            internal uint SizeOfHeaders;
            internal uint CheckSum;
            internal ushort Subsystem;
            internal ushort DllCharacteristics;
            internal ulong SizeOfStackReserve;
            internal ulong SizeOfStackCommit;
            internal ulong SizeOfHeapReserve;
            internal ulong SizeOfHeapCommit;
            internal uint LoaderFlags;
            internal uint NumberOfRvaAndSizes;

            // should be:
            // [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] internal IMAGE_DATA_DIRECTORY[] DataDirectory;
            // but fixed size arrays only work with simple types, so I have to use ulongs and convert them to IMAGE_DATA_DIRECTORY structs
            // Fortunately, IMAGE_DATA_DIRECTORY is only 8 bytes long... (whew)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            internal ulong[] DataDirectory;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IMAGE_NT_HEADERS32
        {
            internal uint signature;
            internal IMAGE_FILE_HEADER fileHeader;
            internal IMAGE_OPTIONAL_HEADER32 optionalHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IMAGE_NT_HEADERS64
        {
            internal uint signature;
            internal IMAGE_FILE_HEADER fileHeader;
            internal IMAGE_OPTIONAL_HEADER64 optionalHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IMAGE_COR20_HEADER
        {
            internal uint cb;
            internal ushort MajorRuntimeVersion;
            internal ushort MinorRuntimeVersion;
            internal IMAGE_DATA_DIRECTORY MetaData;
            internal uint Flags;
            internal uint EntryPointTokenOrEntryPointRVA;
            internal IMAGE_DATA_DIRECTORY Resources;
            internal IMAGE_DATA_DIRECTORY StrongNameSignature;
            internal IMAGE_DATA_DIRECTORY CodeManagerTable;
            internal IMAGE_DATA_DIRECTORY VTableFixups;
            internal IMAGE_DATA_DIRECTORY ExportAddressTableJumps;
            internal IMAGE_DATA_DIRECTORY ManagedNativeHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CRYPTOAPI_BLOB
        {
            internal uint cbData;
            internal IntPtr pbData;
        }

        #endregion

        #region PInvoke
        private const string Crypt32DLL = "crypt32.dll";
        private const string Advapi32DLL = "advapi32.dll";
        private const string MscoreeDLL = "mscoree.dll";

        //------------------------------------------------------------------------------
        // CreateHardLink
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateHardLink(string newFileName, string exitingFileName, IntPtr securityAttributes);

        [DllImport("libc", SetLastError = true)]
        internal static extern int link(string oldpath, string newpath);

        internal static bool MakeHardLink(string newFileName, string exitingFileName, ref string errorMessage)
        {
            bool hardLinkCreated;
            if (NativeMethodsShared.IsWindows)
            {
                hardLinkCreated = CreateHardLink(newFileName, exitingFileName, IntPtr.Zero /* reserved, must be NULL */);
                errorMessage = hardLinkCreated ? null : Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
            }
            else
            {
                hardLinkCreated = link(exitingFileName, newFileName) == 0;
                errorMessage = hardLinkCreated ? null : "The link() library call failed with the following error code: " + Marshal.GetLastWin32Error();
            }

            return hardLinkCreated;
        }

        //------------------------------------------------------------------------------
        // CreateSymbolicLink
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool CreateSymbolicLink(string symLinkFileName, string targetFileName, SymbolicLink dwFlags);

        [DllImport("libc", SetLastError = true)]
        internal static extern int symlink(string oldpath, string newpath);

        internal static bool MakeSymbolicLink(string newFileName, string exitingFileName, ref string errorMessage)
        {
            bool symbolicLinkCreated;
            if (NativeMethodsShared.IsWindows)
            {
                symbolicLinkCreated = CreateSymbolicLink(newFileName, exitingFileName, SymbolicLink.File);
                errorMessage = symbolicLinkCreated ? null : Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
            }
            else
            {
                symbolicLinkCreated = symlink(exitingFileName, newFileName) == 0;
                errorMessage = symbolicLinkCreated ? null : "The link() library call failed with the following error code: " + Marshal.GetLastWin32Error();
            }

            return symbolicLinkCreated;
        }

        //------------------------------------------------------------------------------
        // MoveFileEx
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "MoveFileEx")]
        internal static extern bool MoveFileExWindows
        (
            [In] string existingFileName,
            [In] string newFileName,
            [In] MoveFileFlags flags
        );

        /// <summary>
        /// Add implementation of this function when not running on windows. The implementation is
        /// not complete, of course, but should work for most common cases.
        /// </summary>
        /// <param name="existingFileName"></param>
        /// <param name="newFileName"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        internal static bool MoveFileEx(string existingFileName, string newFileName, MoveFileFlags flags)
        {
            if (NativeMethodsShared.IsWindows)
            {
                return MoveFileExWindows(existingFileName, newFileName, flags);
            }

            if (!File.Exists(existingFileName))
            {
                return false;
            }

            var targetExists = File.Exists(newFileName);

            if (targetExists
                && ((flags & MoveFileFlags.MOVEFILE_REPLACE_EXISTING) != MoveFileFlags.MOVEFILE_REPLACE_EXISTING))
            {
                return false;
            }

            if (targetExists && (File.GetAttributes(newFileName) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                throw new IOException("Moving target is read-only");
            }

            if (string.Equals(existingFileName, newFileName, StringComparison.Ordinal))
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
        internal static extern void UnregisterTypeLib
        (
            [In] ref Guid guid,
            [In] short wMajorVerNum,
            [In] short wMinorVerNum,
            [In] int lcid,
            [In] System.Runtime.InteropServices.ComTypes.SYSKIND syskind
        );

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

        //------------------------------------------------------------------------------
        // CreateFile
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, FileShare dwShareMode,
            IntPtr lpSecurityAttributes, FileMode dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        //------------------------------------------------------------------------------
        // GetFileType
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetFileType(IntPtr hFile);

        //------------------------------------------------------------------------------
        // CloseHandle
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        //------------------------------------------------------------------------------
        // CreateFileMapping
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect,
            uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

        //------------------------------------------------------------------------------
        // MapViewOfFile
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr MapViewOfFile(IntPtr hFileMapping, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, IntPtr dwNumberOfBytesToMap);

        //------------------------------------------------------------------------------
        // UnmapViewOfFile
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        //------------------------------------------------------------------------------
        // CreateProcess
        //------------------------------------------------------------------------------
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcess
        (
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [In, MarshalAs(UnmanagedType.Bool)]
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        //------------------------------------------------------------------------------
        // ImageNtHeader
        //------------------------------------------------------------------------------
        [DllImport("dbghelp.dll", SetLastError = true)]
        internal static extern IntPtr ImageNtHeader(IntPtr imageBase);

        //------------------------------------------------------------------------------
        // ImageRvaToVa
        //------------------------------------------------------------------------------
        [DllImport("dbghelp.dll", SetLastError = true)]
        internal static extern IntPtr ImageRvaToVa(IntPtr ntHeaders, IntPtr imageBase, uint Rva, out IntPtr LastRvaSection);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetLogicalDrives();

        internal static bool AllDrivesMapped()
        {
            const uint AllDriveMask = 0x0cffffff;
            if (NativeMethodsShared.IsWindows)
            {
                var driveMask = GetLogicalDrives();
                // All drives are taken if the value has all 26 bits set
                return driveMask >= AllDriveMask;
            }

            return false;
        }

#if FEATURE_COM_INTEROP
        //------------------------------------------------------------------------------
        // CreateAssemblyCache
        //------------------------------------------------------------------------------
        [DllImport("fusion.dll")]
        internal static extern uint CreateAssemblyCache(out IAssemblyCache ppAsmCache, uint dwReserved);

        [DllImport("fusion.dll")]
        internal static extern int CreateAssemblyEnum(
                out IAssemblyEnum ppEnum,
                IntPtr pUnkReserved,
                IAssemblyName pName,
                AssemblyCacheFlags flags,
                IntPtr pvReserved);

        [DllImport("fusion.dll")]
        internal static extern int CreateAssemblyNameObject(
                out IAssemblyName ppAssemblyNameObj,
                [MarshalAs(UnmanagedType.LPWStr)]
                String szAssemblyName,
                CreateAssemblyNameObjectFlags flags,
                IntPtr pvReserved);

        /// <summary>
        /// GetCachePath from fusion.dll.
        /// Using StringBuilder here is a way to pass a preallocated buffer of characters to (native) functions that require it.
        /// A common design pattern in unmanaged C++ is calling a function twice, once to determine the length of the string
        /// and then again to pass the client-allocated character buffer. StringBuilder is the most straightforward way
        /// to allocate a mutable buffer of characters and pass it around.
        /// </summary>
        [DllImport("fusion.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetCachePath(AssemblyCacheFlags cacheFlags, StringBuilder cachePath, ref int pcchPath);
#endif

        /*------------------------------------------------------------------------------
        CompareAssemblyIdentity
        The Fusion API to compare two assembly identities to determine whether or not they are equivalent is now available. This new API is exported from mscorwks.dll, which you can access via mscoree's GetRealProcAddress. The function prototype is defined in fusion.h as follows:

        STDAPI CompareAssemblyIdentity(LPCWSTR pwzAssemblyIdentity1,
                                    BOOL fUnified1,
                                    LPCWSTR pwzAssemblyIdentity2,
                                    BOOL fUnified2,
                                    BOOL *pfEquivalent,
                                    AssemblyComparisonResult *pResult);

typedef enum _tagAssemblyComparisonResult 
{
    ACR_Unknown,                    // Unknown 
    ACR_EquivalentFullMatch,        // all fields match
    ACR_EquivalentWeakNamed,        // match based on weak-name, version numbers ignored
    ACR_EquivalentFXUnified,        // match based on FX-unification of version numbers
    ACR_EquivalentUnified,          // match based on legacy-unification of version numbers
    ACR_NonEquivalentVersion,       // all fields match except version field
    ACR_NonEquivalent,              // no match

    ACR_EquivalentPartialMatch,
    ACR_EquivalentPartialWeakNamed,  
    ACR_EquivalentPartialUnified,
    ACR_EquivalentPartialFXUnified,
    ACR_NonEquivalentPartialVersion     
} AssemblyComparisonResult;

        Parameters:
            [in] LPCWSTR pwzAssemblyIdentity1 : Textual identity of the first assembly to be compared
            [in] BOOL fUnified1               : Flag to indicate user-specified unification for pwzAssemblyIdentity1 (see below)
            [in] LPCWSTR pwzAssemblyIdentity2 : Textual identity of the second assembly to be compared
            [in] BOOL fUnified2               : Flag to inidcate user-specified unification for pwzAssemblyIdentity2 (see below)
            [out] BOOL *pfEquivalent          : Boolean indicating whether the identities are equivalent
            [out] AssemblyComparisonResult *pResult : Contains detailed information about the comparison result

        This API will check whether or not pwzAssemblyIdentity1 and pwzAssemblyIdentity2 are equivalent. Both of these identities must be full-specified (name, version, pkt, culture). The pfEquivalent parameter will be set to TRUE if one (or more) of the following conditions is true:

        a) The assembly identities are equivalent. For strongly-named assemblies this means full match on (name, version, pkt, culture); for simply-named assemblies this means a match on (name, culture)

        b) The assemblies being compared are FX assemblies (even if the version numbers are not the same, these will compare as equivalent by way of unification)

        c) The assemblies are not FX assemblies but are equivalent because fUnified1 and/or fUnified2 were set.

        The fUnified flag is used to indicate that all versions up to the version number of the strongly-named assembly are considered equivalent to itself. For example, if pwzAssemblyIdentity1 is "foo, version=5.0.0.0, culture=neutral, publicKeyToken=...." and fUnified1==TRUE, then this means to treat all versions of the assembly in the range 0.0.0.0-5.0.0.0 to be equivalent to "foo, version=5.0.0.0, culture=neutral, publicKeyToken=...". If pwzAssemblyIdentity2 is the same as pwzAssemblyIdentity1, except has a lower version number (e.g. version range 0.0.0.0-5.0.0.0), then the API will return that the identities are equivalent. If pwzAssemblyIdentity2 is the same as pwzAssemblyIdentity1, but has a greater version number than 5.0.0.0 then the two identities will only be equivalent if fUnified2 is set.

        The AssemblyComparisonResult gives you information about why the identities compared as equal or not equal. The description of the meaning of each ACR_* return value is described in the declaration above.
        ------------------------------------------------------------------------------*/
        [DllImport("fusion.dll", CharSet = CharSet.Unicode, EntryPoint = "CompareAssemblyIdentity")]
        internal static extern int CompareAssemblyIdentityWindows
            (
                string pwzAssemblyIdentity1,
                [MarshalAs(UnmanagedType.Bool)] bool fUnified1,
                string pwzAssemblyIdentity2,
                [MarshalAs(UnmanagedType.Bool)] bool fUnified2,
                [MarshalAs(UnmanagedType.Bool)] out bool pfEquivalent,
                out AssemblyComparisonResult pResult
            );

        // TODO: Verify correctness of this implementation and
        // extend to more cases.
        internal static void CompareAssemblyIdentity(
            string assemblyIdentity1,
            bool fUnified1,
            string assemblyIdentity2,
            bool fUnified2,
            out bool pfEquivalent,
            out AssemblyComparisonResult pResult)
        {
#if FEATURE_FUSION_COMPAREASSEMBLYIDENTITY
            if (NativeMethodsShared.IsWindows)
            {
                CompareAssemblyIdentityWindows(
                    assemblyIdentity1,
                    fUnified1,
                    assemblyIdentity2,
                    fUnified2,
                    out pfEquivalent,
                    out pResult);
            }
#endif

            AssemblyName an1 = new AssemblyName(assemblyIdentity1);
            AssemblyName an2 = new AssemblyName(assemblyIdentity2);

            //pfEquivalent = AssemblyName.ReferenceMatchesDefinition(an1, an2);
            pfEquivalent = RefMatchesDef(an1, an2);
            if (pfEquivalent)
            {
                pResult = AssemblyComparisonResult.ACR_EquivalentFullMatch;
                return;
            }

            if (!an1.Name.Equals(an2.Name, StringComparison.OrdinalIgnoreCase))
            {
                pResult = AssemblyComparisonResult.ACR_NonEquivalent;
                pfEquivalent = false;
                return;
            }

            var versionCompare = an1.Version.CompareTo(an2.Version);

            if ((versionCompare < 0 && fUnified2) || (versionCompare > 0 && fUnified1))
            {
                pResult = AssemblyComparisonResult.ACR_NonEquivalentVersion;
                pfEquivalent = true;
                return;
            }

            if (versionCompare == 0)
            {
                pResult = AssemblyComparisonResult.ACR_EquivalentFullMatch;
                pfEquivalent = true;
                return;
            }

            pResult = pfEquivalent ? AssemblyComparisonResult.ACR_EquivalentFullMatch : AssemblyComparisonResult.ACR_NonEquivalent;
        }

        //  Based on coreclr baseassemblyspec.cpp (https://github.com/dotnet/coreclr/blob/4cf8a6b082d9bb1789facd996d8265d3908757b2/src/vm/baseassemblyspec.cpp#L330)
        private static bool RefMatchesDef(AssemblyName @ref, AssemblyName def)
        {
            if (IsStrongNamed(@ref))
            {
                return IsStrongNamed(def) && CompareRefToDef(@ref, def);
            }
            else
            {
                return @ref.Name.Equals(def.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Based on coreclr baseassemblyspec.inl (https://github.com/dotnet/coreclr/blob/32f0f9721afb584b4a14d69135bea7ddc129f755/src/vm/baseassemblyspec.inl#L679-L683)
        private static bool IsStrongNamed(AssemblyName assembly)
        {
            var refPkt = assembly.GetPublicKeyToken();
            return refPkt != null && refPkt.Length != 0;
        }

        //  Based on https://github.com/dotnet/coreclr/blob/4cf8a6b082d9bb1789facd996d8265d3908757b2/src/vm/baseassemblyspec.cpp#L241
        private static bool CompareRefToDef(AssemblyName @ref, AssemblyName def)
        {
            if (!@ref.Name.Equals(def.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            byte[] rpkt = @ref.GetPublicKeyToken();
            byte[] dpkt = def.GetPublicKeyToken();

            if (rpkt.Length != dpkt.Length)
            {
                return false;
            }

            for (int i = 0; i < rpkt.Length; i++)
            {
                if (rpkt[i] != dpkt[i])
                {
                    return false;
                }
            }

            if (@ref.Version != def.Version)
            {
                return false;
            }

            if (@ref.CultureName != null &&
                @ref.CultureName != def.CultureName)
            {
                return false;
            }

            return true;
        }

        internal enum AssemblyComparisonResult
        {
            ACR_Unknown,                    // Unknown 
            ACR_EquivalentFullMatch,        // all fields match
            ACR_EquivalentWeakNamed,        // match based on weak-name, version numbers ignored
            ACR_EquivalentFXUnified,        // match based on FX-unification of version numbers
            ACR_EquivalentUnified,          // match based on legacy-unification of version numbers
            ACR_NonEquivalentVersion,       // all fields match except version field
            ACR_NonEquivalent,              // no match

            ACR_EquivalentPartialMatch,
            ACR_EquivalentPartialWeakNamed,
            ACR_EquivalentPartialUnified,
            ACR_EquivalentPartialFXUnified,
            ACR_NonEquivalentPartialVersion
        }

        //------------------------------------------------------------------------------
        // PFXImportCertStore
        //------------------------------------------------------------------------------
        [DllImport(Crypt32DLL, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr PFXImportCertStore([In] IntPtr blob, [In] string password, [In] CryptFlags flags);

        //------------------------------------------------------------------------------
        // CertCloseStore
        //------------------------------------------------------------------------------
        [DllImport(Crypt32DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CertCloseStore([In]   IntPtr CertStore, CertStoreClose Flags);

        //------------------------------------------------------------------------------
        // CertEnumCertificatesInStore
        //------------------------------------------------------------------------------
        [DllImport(Crypt32DLL, SetLastError = true)]
        internal static extern IntPtr CertEnumCertificatesInStore([In]   IntPtr CertStore, [In]   IntPtr PrevCertContext);

        //------------------------------------------------------------------------------
        // CryptAcquireCertificatePrivateKey
        //------------------------------------------------------------------------------
        [DllImport(Crypt32DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptAcquireCertificatePrivateKey([In] IntPtr CertContext, [In] uint flags, [In] IntPtr reserved, [In, Out] ref IntPtr CryptProv, [In, Out] ref KeySpec KeySpec, [In, Out, MarshalAs(UnmanagedType.Bool)] ref bool CallerFreeProv);

        //------------------------------------------------------------------------------
        // CryptGetUserKey
        //------------------------------------------------------------------------------
        [DllImport(Advapi32DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptGetUserKey([In] IntPtr CryptProv, [In] KeySpec KeySpec, [In, Out] ref IntPtr Key);

        //------------------------------------------------------------------------------
        // CryptExportKey
        //------------------------------------------------------------------------------
        [DllImport(Advapi32DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptExportKey([In] IntPtr Key, [In] IntPtr ExpKey, [In] BlobType type, [In] uint Flags, [In] IntPtr Data, [In, Out] ref uint DataLen);

        //------------------------------------------------------------------------------
        // CryptDestroyKey
        //------------------------------------------------------------------------------
        [DllImport(Advapi32DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptDestroyKey(IntPtr hKey);

        //------------------------------------------------------------------------------
        // CryptReleaseContext
        //------------------------------------------------------------------------------
        [DllImport(Advapi32DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptReleaseContext([In] IntPtr Prov, [In] uint Flags);

        //------------------------------------------------------------------------------
        // CertFreeCertificateContext
        //------------------------------------------------------------------------------
        [DllImport(Crypt32DLL, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CertFreeCertificateContext(IntPtr CertContext);

#if FEATURE_MSCOREE
        /// <summary>
        /// Get the runtime version for a given file
        /// </summary>
        /// <param name="szFullPath">The path of the file to be examined</param>
        /// <param name="szBuffer">The buffer allocated for the version information that is returned.</param>
        /// <param name="cchBuffer">The size, in wide characters, of szBuffer</param>
        /// <param name="dwLength">The size, in bytes, of the returned szBuffer.</param>
        /// <returns>HResult</returns>
        [DllImport(MscoreeDLL, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint GetFileVersion(String szFullPath, StringBuilder szBuffer, int cchBuffer, out uint dwLength);
#endif
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
            IntPtr attrDataPostProlog = IntPtr.Zero;
            int attrDataOffset = 0;
            int strLen = 0;
            int i = 0;
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
                    attrDataPostProlog = attrData + preReadOffset;

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
                            for (i = 0; i < strLen; i++)
                            {
                                bytes[i] = Marshal.ReadByte(attrDataPostProlog, attrDataOffset + i);
                            }

                            // And convert it to the output string. 
                            strValue = new String(Encoding.UTF8.GetChars(bytes));
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

            return (strValue != null);
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
                uncompressedDataLength = (int)(((*bytes & 0x3f) << 8 | *(bytes + 1)));
                count = 2;
            }
            else if ((*bytes & 0xE0) == 0xC0)      // 110? ????    
            {
                uncompressedDataLength = (int)(((*bytes & 0x1f) << 24 | *(bytes + 1) << 16 | *(bytes + 2) << 8 | *(bytes + 3)));
                count = 4;
            }

            return count;
        }
        #endregion
        #region InternalClass
#if FEATURE_COM_INTEROP
        /// <summary>
        /// This class is a wrapper over the native GAC enumeration API.
        /// </summary>
        [ComVisible(false)]
        internal class AssemblyCacheEnum : IEnumerable<AssemblyNameExtension>
        {
            /// <summary>
            /// Path to the gac
            /// </summary>
            private static readonly string s_gacPath = Path.Combine(NativeMethodsShared.FrameworkBasePath, "gac");

            /// <summary>
            /// Regex for directory version parsing
            /// </summary>
            private static readonly Regex s_assemblyVersionRegex = new Regex(
                @"^([.\d]+)_([^_]*)_([a-fA-F\d]{16})$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
                    if (Directory.Exists(s_gacPath))
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
                        _gacDirectories = Array.Empty<string>();
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
                                    var match = s_assemblyVersionRegex.Match(versionString);
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
                                            name.SetPublicKeyToken(
                                                Enumerable.Range(0, 16)
                                                    .Where(x => x % 2 == 0)
                                                    .Select(x => Convert.ToByte(value.Substring(x, 2), 16)).ToArray());
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
                if (Directory.Exists(path))
                {
                    // Since we have a strong name, create the path to the dll
                    path = Path.Combine(
                        path,
                        string.Format(
                            "{0}_{1}_{2}",
                            assemblyNameVersion.Version.ToString(4),
                            assemblyNameVersion.CultureName != "neutral" ? assemblyNameVersion.CultureName : string.Empty,
                            assemblyNameVersion.GetPublicKeyToken()
                                .Aggregate(new StringBuilder(), (builder, v) => builder.Append(v.ToString("x2")))),
                        assemblyNameVersion.Name + ".dll");

                    if (File.Exists(path))
                    {
                        return path;
                    }
                }

                return null;
            }
        }
#endif
        #endregion
    }
}
