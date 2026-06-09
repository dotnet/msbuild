// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WINDOWSINTEROP && FEATURE_VISUALSTUDIOSETUP

using System;
#if NET
using System.Runtime.CompilerServices;
#endif
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.Build.Shared.VisualStudio;

/// <summary>
///  Standard COM enumerator over <see cref="ISetupInstance"/> pointers.
/// </summary>
/// <remarks>
///  Declared in <c>Setup.Configuration.idl</c>. IID = <c>{6380BCFF-41D3-4B2E-8B2E-BF8A6810C848}</c>.
/// </remarks>
internal unsafe struct IEnumSetupInstances : IComIID
{
    public static readonly Guid IID_IEnumSetupInstances = new(0x6380BCFF, 0x41D3, 0x4B2E, 0x8B, 0x2E, 0xBF, 0x8A, 0x68, 0x10, 0xC8, 0x48);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_IEnumSetupInstances);
    }
#else
    readonly Guid IComIID.Guid => IID_IEnumSetupInstances;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (IEnumSetupInstances* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (IEnumSetupInstances* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (IEnumSetupInstances* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // IEnumSetupInstances vtable layout (indices 3-6):
    //   3 = Next   (ULONG celt, ISetupInstance** rgelt, ULONG* pceltFetched)  <-- Used
    //   4 = Skip   (ULONG celt)
    //   5 = Reset  ()
    //   6 = Clone  (out IEnumSetupInstances**)

    /// <summary>
    ///  Retrieve up to <paramref name="celt"/> instances from the enumerator. Returns
    ///  <see cref="HRESULT.S_OK"/> when exactly <paramref name="celt"/> items were
    ///  produced and <see cref="HRESULT.S_FALSE"/> when fewer (including zero) were
    ///  available. <paramref name="pceltFetched"/> always reflects the actual count.
    /// </summary>
    public HRESULT Next(uint celt, ISetupInstance** rgelt, uint* pceltFetched)
    {
        fixed (IEnumSetupInstances* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, uint, ISetupInstance**, uint*, HRESULT>)_lpVtbl[3])(
                pThis, celt, rgelt, pceltFetched);
        }
    }
}

#endif
