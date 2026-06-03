// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Manually defined OLE Automation type-library COM struct following CsWin32
// struct-based COM patterns. CsWin32 surfaces ITypeLib / ITypeInfo from the Win32
// metadata, but not ICreateTypeLib (the authoring side of OLE typelibs). The
// declaration is sourced from the Windows SDK header oaidl.h; the interface is
// implemented by oleaut32.dll.

using System;
#if NET
using System.Runtime.CompilerServices;
#endif
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.Build.Tasks.TypeLibInterop;

/// <summary>
///  Provides methods for creating and managing a type library.
/// </summary>
/// <remarks>
///  <para>
///   Declared in <c>oaidl.h</c> as <c>ICreateTypeLib : IUnknown</c>. IID =
///   <c>{00020406-0000-0000-C000-000000000046}</c>.
///  </para>
///  <para>
///   An instance is obtained by <c>QueryInterface</c>'ing an <c>ITypeLib</c> produced
///   by <c>System.Runtime.InteropServices.TypeLibConverter.ConvertAssemblyToTypeLib</c>.
///   Only <see cref="SaveAllChanges"/> is invoked from MSBuild; the remaining slots are
///   documented below for future use.
///  </para>
/// </remarks>
internal unsafe struct ICreateTypeLib : IComIID
{
    public static readonly Guid IID_ICreateTypeLib = new(0x00020406, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_ICreateTypeLib);
    }
#else
    readonly Guid IComIID.Guid => IID_ICreateTypeLib;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ICreateTypeLib* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ICreateTypeLib*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (ICreateTypeLib* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ICreateTypeLib*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (ICreateTypeLib* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ICreateTypeLib*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // ICreateTypeLib vtable layout (indices 3-12):
    //   3  = CreateTypeInfo
    //   4  = SetName
    //   5  = SetVersion
    //   6  = SetGuid
    //   7  = SetDocString
    //   8  = SetHelpFileName
    //   9  = SetHelpContext
    //   10 = SetLcid
    //   11 = SetLibFlags
    //   12 = SaveAllChanges     <-- Used

    /// <summary>
    ///  Saves the <c>ICreateTypeLib</c> instance, persisting any pending changes to
    ///  disk. The path is the one supplied to <c>CreateTypeLib2</c> when the typelib
    ///  was created.
    /// </summary>
    public HRESULT SaveAllChanges()
    {
        fixed (ICreateTypeLib* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ICreateTypeLib*, HRESULT>)_lpVtbl[12])(pThis);
        }
    }
}
