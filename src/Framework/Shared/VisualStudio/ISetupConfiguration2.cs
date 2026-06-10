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
///  v2 of the Visual Studio Setup Configuration top-level query interface. Derives from
///  <see cref="ISetupConfiguration"/>; adds <see cref="EnumAllInstances"/> for enumerating
///  every installed instance (including incomplete / partial installations).
/// </summary>
/// <remarks>
///  Declared in <c>Setup.Configuration.idl</c>. IID = <c>{26AAB78C-4A60-49D6-AF3B-3C35BC93365D}</c>.
/// </remarks>
internal unsafe struct ISetupConfiguration2 : IComIID
{
    public static readonly Guid IID_ISetupConfiguration2 = new(0x26AAB78C, 0x4A60, 0x49D6, 0xAF, 0x3B, 0x3C, 0x35, 0xBC, 0x93, 0x36, 0x5D);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_ISetupConfiguration2);
    }
#else
    readonly Guid IComIID.Guid => IID_ISetupConfiguration2;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupConfiguration2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (ISetupConfiguration2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (ISetupConfiguration2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // ISetupConfiguration vtable layout (indices 3-5):
    //   3 = EnumInstances              (out IEnumSetupInstances**)
    //   4 = GetInstanceForCurrentProcess(out ISetupInstance**)
    //   5 = GetInstanceForPath          (LPCWSTR, out ISetupInstance**)

    // ISetupConfiguration2 vtable layout (added at index 6):
    //   6 = EnumAllInstances            (out IEnumSetupInstances**)        <-- Used

    /// <summary>
    ///  Enumerate every installed Visual Studio instance the installer knows about,
    ///  including ones whose <c>InstanceState</c> is incomplete. Use this in preference
    ///  to <c>EnumInstances</c> when the caller wants to surface partial installs.
    /// </summary>
    public HRESULT EnumAllInstances(IEnumSetupInstances** ppEnumInstances)
    {
        fixed (ISetupConfiguration2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, IEnumSetupInstances**, HRESULT>)_lpVtbl[6])(pThis, ppEnumInstances);
        }
    }
}

#endif
