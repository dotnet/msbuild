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
///  Provides methods to access and examine the contents of an assembly manifest.
/// </summary>
/// <remarks>
///  <para>
///   Declared in <c>cor.h</c> as <c>DECLARE_INTERFACE_(IMetaDataAssemblyImport, IUnknown)</c>.
///   IID = <c>{EE62470B-E94B-424E-9B7C-2F00C9249F93}</c>.
///  </para>
///  <para>
///   An instance is obtained by calling <see cref="IMetaDataDispenser.OpenScope"/> with
///   any <c>IMetaData*</c> IID and then performing <c>QueryInterface</c> for this IID
///   (the underlying CLR <c>RegMeta</c> object implements <c>IMetaDataImport</c>,
///   <see cref="IMetaDataImport2"/>, and <c>IMetaDataAssemblyImport</c>).
///  </para>
/// </remarks>
internal unsafe struct IMetaDataAssemblyImport : IComIID
{
    public static readonly Guid IID_IMetaDataAssemblyImport = new(0xEE62470B, 0xE94B, 0x424E, 0x9B, 0x7C, 0x2F, 0x00, 0xC9, 0x24, 0x9F, 0x93);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_IMetaDataAssemblyImport);
    }
#else
    readonly Guid IComIID.Guid => IID_IMetaDataAssemblyImport;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // IMetaDataAssemblyImport vtable layout (indices 3-16):
    //   3  = GetAssemblyProps              <-- Used
    //   4  = GetAssemblyRefProps           <-- Used
    //   5  = GetFileProps                  <-- Used
    //   6  = GetExportedTypeProps
    //   7  = GetManifestResourceProps
    //   8  = EnumAssemblyRefs              <-- Used
    //   9  = EnumFiles                     <-- Used
    //   10 = EnumExportedTypes
    //   11 = EnumManifestResources
    //   12 = GetAssemblyFromScope          <-- Used
    //   13 = FindExportedTypeByName
    //   14 = FindManifestResourceByName
    //   15 = CloseEnum (void return)       <-- Used
    //   16 = FindAssembliesByName

    /// <summary>
    ///  Gets the set of properties for the assembly with the specified metadata token.
    /// </summary>
    /// <param name="mda">Assembly token (from <see cref="GetAssemblyFromScope"/>).</param>
    /// <param name="ppbPublicKey">
    ///  Receives a pointer to the public key blob. The blob lives in the metadata scope and is
    ///  valid only as long as the <see cref="IMetaDataAssemblyImport"/> instance remains alive;
    ///  the caller must not free it. May be null when the public key is not needed.
    /// </param>
    /// <param name="pcbPublicKey">Receives the size of the public key blob in bytes. May be null.</param>
    /// <param name="pulHashAlgId">Receives the hash algorithm (<c>ALG_ID</c>) used for the assembly. May be null.</param>
    /// <param name="szName">Caller-allocated wide-char buffer to fill with the assembly's simple name. May be null to query the required length via <paramref name="pchName"/>.</param>
    /// <param name="cchName">Capacity of <paramref name="szName"/> in wide chars.</param>
    /// <param name="pchName">Receives the actual number of wide chars written to <paramref name="szName"/> (including the terminating null).</param>
    /// <param name="pMetaData">
    ///  Logically <see cref="ASSEMBLYMETADATA"/>*. Receives the assembly's version / culture /
    ///  processor / OS metadata. May be null. When non-null, see
    ///  <see cref="ASSEMBLYMETADATA"/>'s allocation contract — the caller owns every buffer
    ///  referenced from the struct.
    /// </param>
    /// <param name="pdwAssemblyFlags">Receives the <see cref="CorAssemblyFlags"/> bitmask. May be null.</param>
    /// <remarks>Native signature: <c>HRESULT GetAssemblyProps(mdAssembly, const void**, ULONG*, ULONG*, LPWSTR, ULONG, ULONG*, ASSEMBLYMETADATA*, DWORD*)</c>.</remarks>
    public HRESULT GetAssemblyProps(
        MdAssembly mda,
        void** ppbPublicKey,
        uint* pcbPublicKey,
        uint* pulHashAlgId,
        PWSTR szName,
        uint cchName,
        uint* pchName,
        void* pMetaData,
        CorAssemblyFlags* pdwAssemblyFlags)
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, MdAssembly, void**, uint*, uint*, PWSTR, uint, uint*, void*, CorAssemblyFlags*, HRESULT>)_lpVtbl[3])(
                pThis, mda, ppbPublicKey, pcbPublicKey, pulHashAlgId, szName, cchName, pchName, pMetaData, pdwAssemblyFlags);
        }
    }

    /// <summary>
    ///  Gets the set of properties for the assembly reference with the specified metadata token.
    /// </summary>
    /// <param name="mdar">AssemblyRef token (typically from <see cref="EnumAssemblyRefs"/>).</param>
    /// <param name="ppbPublicKeyOrToken">
    ///  Receives a pointer to the public key or public-key-token blob (which one is indicated by
    ///  <see cref="CorAssemblyFlags.afPublicKey"/> in <paramref name="pdwAssemblyRefFlags"/>).
    ///  The blob lives in the metadata scope; the caller must not free it. May be null when not needed.
    /// </param>
    /// <param name="pcbPublicKeyOrToken">Receives the size of the blob in bytes. May be null.</param>
    /// <param name="szName">Caller-allocated wide-char buffer to fill with the reference's simple name. May be null to query length only.</param>
    /// <param name="cchName">Capacity of <paramref name="szName"/> in wide chars.</param>
    /// <param name="pchName">Receives the actual number of wide chars written (including the terminating null).</param>
    /// <param name="pMetaData">
    ///  Logically <see cref="ASSEMBLYMETADATA"/>*. Receives the reference's version / culture /
    ///  processor / OS metadata. May be null. See <see cref="ASSEMBLYMETADATA"/> for the
    ///  buffer-ownership contract.
    /// </param>
    /// <param name="ppbHashValue">Receives a pointer to the hash blob (lives in the metadata scope; do not free). May be null.</param>
    /// <param name="pcbHashValue">Receives the size of the hash blob in bytes. May be null.</param>
    /// <param name="pdwAssemblyRefFlags">Receives the <see cref="CorAssemblyFlags"/> bitmask. May be null.</param>
    /// <remarks>Native signature: <c>HRESULT GetAssemblyRefProps(mdAssemblyRef, const void**, ULONG*, LPWSTR, ULONG, ULONG*, ASSEMBLYMETADATA*, const void**, ULONG*, DWORD*)</c>.</remarks>
    public HRESULT GetAssemblyRefProps(
        MdAssemblyRef mdar,
        void** ppbPublicKeyOrToken,
        uint* pcbPublicKeyOrToken,
        PWSTR szName,
        uint cchName,
        uint* pchName,
        void* pMetaData,
        void** ppbHashValue,
        uint* pcbHashValue,
        CorAssemblyFlags* pdwAssemblyRefFlags)
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, MdAssemblyRef, void**, uint*, PWSTR, uint, uint*, void*, void**, uint*, CorAssemblyFlags*, HRESULT>)_lpVtbl[4])(
                pThis, mdar, ppbPublicKeyOrToken, pcbPublicKeyOrToken, szName, cchName, pchName, pMetaData, ppbHashValue, pcbHashValue, pdwAssemblyRefFlags);
        }
    }

    /// <summary>
    ///  Gets the set of properties for the file (module) with the specified metadata token.
    /// </summary>
    /// <param name="mdf">File token (typically from <see cref="EnumFiles"/>).</param>
    /// <param name="szName">Caller-allocated wide-char buffer for the file name. May be null to query length only.</param>
    /// <param name="cchName">Capacity of <paramref name="szName"/> in wide chars.</param>
    /// <param name="pchName">Receives the actual number of wide chars written (including the terminating null).</param>
    /// <param name="ppbHashValue">Receives a pointer to the hash blob (lives in the metadata scope; do not free). May be null.</param>
    /// <param name="pcbHashValue">Receives the size of the hash blob in bytes. May be null.</param>
    /// <param name="pdwFileFlags">Receives the <c>CorFileFlags</c> bitmask. May be null.</param>
    /// <remarks>Native signature: <c>HRESULT GetFileProps(mdFile, LPWSTR, ULONG, ULONG*, const void**, ULONG*, DWORD*)</c>.</remarks>
    public HRESULT GetFileProps(
        MdFile mdf,
        PWSTR szName,
        uint cchName,
        uint* pchName,
        void** ppbHashValue,
        uint* pcbHashValue,
        uint* pdwFileFlags)
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, MdFile, PWSTR, uint, uint*, void**, uint*, uint*, HRESULT>)_lpVtbl[5])(
                pThis, mdf, szName, cchName, pchName, ppbHashValue, pcbHashValue, pdwFileFlags);
        }
    }

    /// <summary>
    ///  Enumerates the assembly references in the assembly's manifest.
    /// </summary>
    /// <param name="phEnum">[IN/OUT] Enumeration handle. Pass zero on first call.</param>
    /// <param name="rAssemblyRefs">Caller-allocated <c>MdAssemblyRef[]</c> that receives a chunk of AssemblyRef tokens.</param>
    /// <param name="cMax">Capacity of <paramref name="rAssemblyRefs"/>.</param>
    /// <param name="pcTokens">Receives the number of tokens written into <paramref name="rAssemblyRefs"/>. Iteration ends when this is less than <paramref name="cMax"/>.</param>
    /// <remarks>Native signature: <c>HRESULT EnumAssemblyRefs(HCORENUM*, mdAssemblyRef[], ULONG, ULONG*)</c>.</remarks>
    public HRESULT EnumAssemblyRefs(IntPtr* phEnum, MdAssemblyRef* rAssemblyRefs, uint cMax, uint* pcTokens)
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, IntPtr*, MdAssemblyRef*, uint, uint*, HRESULT>)_lpVtbl[8])(
                pThis, phEnum, rAssemblyRefs, cMax, pcTokens);
        }
    }

    /// <summary>
    ///  Enumerates the file (module) tokens in the assembly's manifest. Same enumeration-handle
    ///  semantics as <see cref="EnumAssemblyRefs"/>; the caller must release
    ///  <paramref name="phEnum"/> via <see cref="CloseEnum"/>.
    /// </summary>
    /// <param name="phEnum">[IN/OUT] Enumeration handle. Pass zero on first call. Caller must release via <see cref="CloseEnum"/>.</param>
    /// <param name="rFiles">Caller-allocated <c>MdFile[]</c> that receives a chunk of File tokens.</param>
    /// <param name="cMax">Capacity of <paramref name="rFiles"/>.</param>
    /// <param name="pcTokens">Receives the number of tokens written. Iteration ends when this is less than <paramref name="cMax"/>.</param>
    /// <remarks>Native signature: <c>HRESULT EnumFiles(HCORENUM*, mdFile[], ULONG, ULONG*)</c>.</remarks>
    public HRESULT EnumFiles(IntPtr* phEnum, MdFile* rFiles, uint cMax, uint* pcTokens)
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, IntPtr*, MdFile*, uint, uint*, HRESULT>)_lpVtbl[9])(
                pThis, phEnum, rFiles, cMax, pcTokens);
        }
    }

    /// <summary>
    ///  Returns the metadata token for the assembly contained in the current scope, or
    ///  <see cref="MdAssembly.Nil"/> if the scope is a module rather than an assembly.
    /// </summary>
    /// <param name="ptkAssembly">Receives the assembly token; required (must not be null).</param>
    /// <remarks>Native signature: <c>HRESULT GetAssemblyFromScope(mdAssembly*)</c>.</remarks>
    public HRESULT GetAssemblyFromScope(MdAssembly* ptkAssembly)
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, MdAssembly*, HRESULT>)_lpVtbl[12])(pThis, ptkAssembly);
        }
    }

    /// <summary>
    ///  Releases the native enumeration handle allocated by an <c>Enum*</c> call. Must be invoked
    ///  exactly once per non-null handle, including on exception paths — the metadata engine
    ///  owns the underlying allocation and never frees it implicitly. Safe to call with a
    ///  null/zero handle (no-op).
    /// </summary>
    /// <param name="hEnum">The enumeration handle previously written by an <c>Enum*</c> call.</param>
    /// <remarks>
    ///  Native signature: <c>void CloseEnum(HCORENUM)</c>. This is one of the few
    ///  <c>IMetaData*</c> methods that does not return <c>HRESULT</c>.
    /// </remarks>
    public void CloseEnum(IntPtr hEnum)
    {
        fixed (IMetaDataAssemblyImport* pThis = &this)
        {
            ((delegate* unmanaged[Stdcall]<IMetaDataAssemblyImport*, IntPtr, void>)_lpVtbl[15])(pThis, hEnum);
        }
    }
}
