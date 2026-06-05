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
///  Entry point to the CLR metadata engine; creates new scopes or opens existing scopes
///  (.NET assemblies / modules) so callers can read or emit metadata through other
///  <c>IMetaData*</c> interfaces.
/// </summary>
/// <remarks>
///  <para>
///   Declared in <c>cor.h</c> as <c>DECLARE_INTERFACE_(IMetaDataDispenser, IUnknown)</c>.
///   IID = <c>{809C652E-7396-11D2-9771-00A0C9B4D50C}</c>.
///  </para>
///  <para>
///   The implementation lives in <c>mscoree.dll</c> / <c>clr.dll</c>; activate it via
///   <see cref="CorMetadata.CLSID_CorMetaDataDispenser"/> and <c>CoCreateInstance</c>.
///  </para>
/// </remarks>
internal unsafe struct IMetaDataDispenser : IComIID
{
    public static readonly Guid IID_IMetaDataDispenser = new(0x809C652E, 0x7396, 0x11D2, 0x97, 0x71, 0x00, 0xA0, 0xC9, 0xB4, 0xD5, 0x0C);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_IMetaDataDispenser);
    }
#else
    readonly Guid IComIID.Guid => IID_IMetaDataDispenser;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (IMetaDataDispenser* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataDispenser*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (IMetaDataDispenser* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataDispenser*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (IMetaDataDispenser* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataDispenser*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // IMetaDataDispenser vtable layout (indices 3-5):
    //   3 = DefineScope         (REFCLSID, DWORD, REFIID, IUnknown**)
    //   4 = OpenScope           (LPCWSTR, DWORD, REFIID, IUnknown**)        <-- Used
    //   5 = OpenScopeOnMemory   (LPCVOID, ULONG, DWORD, REFIID, IUnknown**)

    /// <summary>
    ///  Opens an existing on-disk scope (typically a managed PE) and returns the
    ///  interface identified by <paramref name="riid"/>.
    /// </summary>
    /// <param name="szScope">Null-terminated path of the file to open.</param>
    /// <param name="dwOpenFlags">
    ///  Combination of <see cref="CorOpenFlags"/>. Pass <see cref="CorOpenFlags.ofRead"/>
    ///  (or <c>default</c>) for default read-only access.
    /// </param>
    /// <param name="riid">
    ///  IID of the interface to return. The underlying CLR <c>RegMeta</c> coclass implements
    ///  every <c>IMetaData*</c> interface, so callers can request <see cref="IMetaDataImport2"/>
    ///  or <see cref="IMetaDataAssemblyImport"/> directly and save a <c>QueryInterface</c>
    ///  round-trip. Internally <c>OpenScope</c> activates the scope and immediately QIs for
    ///  <paramref name="riid"/> (see <c>CLR\src\md\compiler\disp.cpp</c> <c>Disp::OpenScope</c>
    ///  -&gt; <c>DeliverScope</c>).
    /// </param>
    /// <param name="ppIUnk">
    ///  Receives the requested interface pointer. The CLR has already <c>AddRef</c>'d it; the
    ///  caller owns the reference and is responsible for releasing it (typically via
    ///  <c>ComScope&lt;T&gt;</c> / <c>AgileComPointer&lt;T&gt;</c>).
    /// </param>
    /// <remarks>Native signature: <c>HRESULT OpenScope(LPCWSTR, DWORD, REFIID, IUnknown**)</c>.</remarks>
    public HRESULT OpenScope(PCWSTR szScope, CorOpenFlags dwOpenFlags, Guid* riid, void** ppIUnk)
    {
        fixed (IMetaDataDispenser* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IMetaDataDispenser*, PCWSTR, CorOpenFlags, Guid*, void**, HRESULT>)_lpVtbl[4])(
                pThis, szScope, dwOpenFlags, riid, ppIUnk);
        }
    }
}
