// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Manually defined Fusion COM struct following CsWin32 struct-based COM patterns.
// Fusion is not in Win32 metadata; declarations from CLR\src\inc\fusion.idl.

using System;
#if NET
using System.Runtime.CompilerServices;
#endif
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.Build.Tasks.Fusion;

/// <summary>
///  Represents a fusion assembly name.
/// </summary>
/// <remarks>
///  <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/iassemblyname-interface"/>.
///  IID = {CD193BC0-B4BC-11D2-9833-00C04FC31D2E}
/// </remarks>
internal unsafe struct IAssemblyName : IComIID
{
    public static readonly Guid IID_IAssemblyName = new(0xCD193BC0, 0xB4BC, 0x11D2, 0x98, 0x33, 0x00, 0xC0, 0x4F, 0xC3, 0x1D, 0x2E);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_IAssemblyName);
    }
#else
    readonly Guid IComIID.Guid => IID_IAssemblyName;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (IAssemblyName* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyName*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (IAssemblyName* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyName*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (IAssemblyName* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyName*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // IAssemblyName vtable layout (indices 3-13):
    //   3 = SetProperty
    //   4 = GetProperty
    //   5 = Finalize
    //   6 = GetDisplayName       <-- Used
    //   7 = Reserved
    //   8 = GetName
    //   9 = GetVersion
    //   10 = IsEqual
    //   11 = Clone

    /// <summary>
    ///  Returns a string representation of the assembly name.
    /// </summary>
    public HRESULT GetDisplayName(char* szDisplayName, int* pccDisplayName, AssemblyNameDisplayFlags dwDisplayFlags)
    {
        fixed (IAssemblyName* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyName*, char*, int*, AssemblyNameDisplayFlags, HRESULT>)_lpVtbl[6])(
                pThis, szDisplayName, pccDisplayName, dwDisplayFlags);
        }
    }
}
