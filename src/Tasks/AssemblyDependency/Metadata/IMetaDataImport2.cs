// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Manually defined CLR metadata COM struct following CsWin32 struct-based COM patterns.
// CLR metadata interfaces are not in Win32 metadata; declarations from CLR\src\inc\cor.h.

using System;
#if NET
using System.Runtime.CompilerServices;
#endif
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.Build.Tasks.Metadata;

/// <summary>
///  CLR metadata import interface that extends the base <c>IMetaDataImport</c> with
///  generics-aware metadata import plus PE/runtime version inspection.
/// </summary>
/// <remarks>
///  <para>
///   Declared in <c>cor.h</c> as <c>DECLARE_INTERFACE_(IMetaDataImport2, IMetaDataImport)</c>.
///   IID = <c>{FCE5EFA0-8BBA-4F8E-A036-8F2022B08466}</c>.
///  </para>
///  <para>
///   MSBuild never holds a base <c>IMetaDataImport</c> pointer directly — the base interface
///   has no dedicated wrapper because every CLR <c>RegMeta</c> coclass implements
///   <c>IMetaDataImport2</c> anyway. The 62 inherited slots are documented inline in the
///   vtable map below.
///  </para>
/// </remarks>
internal unsafe struct IMetaDataImport2 : IComIID
{
    public static readonly Guid IID_IMetaDataImport2 = new(0xFCE5EFA0, 0x8BBA, 0x4F8E, 0xA0, 0x36, 0x8F, 0x20, 0x22, 0xB0, 0x84, 0x66);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_IMetaDataImport2);
    }
#else
    readonly Guid IComIID.Guid => IID_IMetaDataImport2;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (IMetaDataImport2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataImport2*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (IMetaDataImport2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataImport2*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (IMetaDataImport2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataImport2*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // IMetaDataImport2 vtable. Slots 3-64 are inherited from IMetaDataImport; slots 65-72 are
    // the IMetaDataImport2 additions. MSBuild never holds a base IMetaDataImport pointer at
    // runtime — callers ask IMetaDataDispenser::OpenScope for IID_IMetaDataImport2 directly,
    // since the underlying CLR RegMeta coclass implements every IMetaData* interface.
    //
    // Used slots:
    //   60 = GetCustomAttributeByName  <-- Used (inherited from IMetaDataImport)
    //   70 = GetPEKind                 <-- Used (IMetaDataImport2 addition)
    //
    // Full slot map (source: cor.h). Used here as the canonical reference when adding new
    // methods so they land on the correct index.
    //
    // Inherited IMetaDataImport (indices 3-64):
    //   3  = CloseEnum (void return)            34 = GetEventProps
    //   4  = CountEnum                          35 = EnumMethodSemantics
    //   5  = ResetEnum                          36 = GetMethodSemantics
    //   6  = EnumTypeDefs                       37 = GetClassLayout
    //   7  = EnumInterfaceImpls                 38 = GetFieldMarshal
    //   8  = EnumTypeRefs                       39 = GetRVA
    //   9  = FindTypeDefByName                  40 = GetPermissionSetProps
    //   10 = GetScopeProps                      41 = GetSigFromToken
    //   11 = GetModuleFromScope                 42 = GetModuleRefProps
    //   12 = GetTypeDefProps                    43 = EnumModuleRefs
    //   13 = GetInterfaceImplProps              44 = GetTypeSpecFromToken
    //   14 = GetTypeRefProps                    45 = GetNameFromToken
    //   15 = ResolveTypeRef                     46 = EnumUnresolvedMethods
    //   16 = EnumMembers                        47 = GetUserString
    //   17 = EnumMembersWithName                48 = GetPinvokeMap
    //   18 = EnumMethods                        49 = EnumSignatures
    //   19 = EnumMethodsWithName                50 = EnumTypeSpecs
    //   20 = EnumFields                         51 = EnumUserStrings
    //   21 = EnumFieldsWithName                 52 = GetParamForMethodIndex
    //   22 = EnumParams                         53 = EnumCustomAttributes
    //   23 = EnumMemberRefs                     54 = GetCustomAttributeProps
    //   24 = EnumMethodImpls                    55 = FindTypeRef
    //   25 = EnumPermissionSets                 56 = GetMemberProps
    //   26 = FindMember                         57 = GetFieldProps
    //   27 = FindMethod                         58 = GetPropertyProps
    //   28 = FindField                          59 = GetParamProps
    //   29 = FindMemberRef                      60 = GetCustomAttributeByName  <-- Used
    //   30 = GetMethodProps                     61 = IsValidToken
    //   31 = GetMemberRefProps                  62 = GetNestedClassProps
    //   32 = EnumProperties                     63 = GetNativeCallConvFromSig
    //   33 = EnumEvents                         64 = IsGlobal
    //
    // IMetaDataImport2 additions (indices 65-72):
    //   65 = EnumGenericParams
    //   66 = GetGenericParamProps
    //   67 = GetMethodSpecProps
    //   68 = EnumGenericParamConstraints
    //   69 = GetGenericParamConstraintProps
    //   70 = GetPEKind                  <-- Used
    //   71 = GetVersionString
    //   72 = EnumMethodSpecs

    /// <summary>
    ///  Gets the value of the custom attribute, given its name and owner.
    /// </summary>
    /// <param name="tkObj">Token for the scope of the lookup (typically the assembly token).</param>
    /// <param name="szName">Name of the desired custom attribute (null-terminated).</param>
    /// <param name="ppData">Receives a pointer to the attribute blob (caller must not free).</param>
    /// <param name="pcbData">Receives the size of the attribute blob in bytes.</param>
    /// <returns><c>S_OK</c> if the attribute exists; <c>S_FALSE</c> if not found.</returns>
    /// <remarks>Native signature: <c>HRESULT GetCustomAttributeByName(mdToken, LPCWSTR, const void**, ULONG*)</c>.</remarks>
    public HRESULT GetCustomAttributeByName(MdToken tkObj, PCWSTR szName, void** ppData, uint* pcbData)
    {
        fixed (IMetaDataImport2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataImport2*, MdToken, PCWSTR, void**, uint*, HRESULT>)_lpVtbl[60])(
                pThis, tkObj, szName, ppData, pcbData);
        }
    }

    /// <summary>
    ///  Gets a value identifying the nature of the code in the portable executable (PE)
    ///  file and the target platform that the code was compiled for.
    /// </summary>
    /// <param name="pdwPEKind">Receives a <c>CorPEKind</c> bitmask (0 = not a PE).</param>
    /// <param name="pdwMachine">Receives the machine value from the NT PE header (e.g. <c>IMAGE_FILE_MACHINE_I386</c>).</param>
    /// <remarks>Native signature: <c>HRESULT GetPEKind(DWORD*, DWORD*)</c>.</remarks>
    public HRESULT GetPEKind(uint* pdwPEKind, uint* pdwMachine)
    {
        fixed (IMetaDataImport2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataImport2*, uint*, uint*, HRESULT>)_lpVtbl[70])(
                pThis, pdwPEKind, pdwMachine);
        }
    }
}
