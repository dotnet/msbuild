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
///  Enumerates the assemblies in the global assembly cache.
/// </summary>
/// <remarks>
///  <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/iassemblyenum-interface"/>.
///  IID = {21B8916C-F28E-11D2-A473-00C04F8EF448}
/// </remarks>
internal unsafe struct IAssemblyEnum : IComIID
{
    public static readonly Guid IID_IAssemblyEnum = new(0x21B8916C, 0xF28E, 0x11D2, 0xA4, 0x73, 0x00, 0xC0, 0x4F, 0x8E, 0xF4, 0x48);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_IAssemblyEnum);
    }
#else
    readonly Guid IComIID.Guid => IID_IAssemblyEnum;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (IAssemblyEnum* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyEnum*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (IAssemblyEnum* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyEnum*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (IAssemblyEnum* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyEnum*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // IAssemblyEnum vtable layout (indices 3-5):
    //   3 = GetNextAssembly   <-- Used
    //   4 = Reset
    //   5 = Clone

    /// <summary>
    ///  Gets a pointer to the next <see cref="IAssemblyName"/> in this enum.
    /// </summary>
    public HRESULT GetNextAssembly(void* pvReserved, IAssemblyName** ppName, uint dwFlags)
    {
        fixed (IAssemblyEnum* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyEnum*, void*, IAssemblyName**, uint, HRESULT>)_lpVtbl[3])(
                pThis, pvReserved, ppName, dwFlags);
        }
    }
}
