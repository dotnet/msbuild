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
///  v2 of <see cref="ISetupInstance"/>. Derives from <see cref="ISetupInstance"/>; adds
///  installer-state queries. MSBuild only needs <see cref="GetState"/> here to filter out
///  partial installations.
/// </summary>
/// <remarks>
///  Declared in <c>Setup.Configuration.idl</c>. IID = <c>{89143C9A-05AF-49B0-B717-72E218A2185C}</c>.
/// </remarks>
internal unsafe struct ISetupInstance2 : IComIID
{
    public static readonly Guid IID_ISetupInstance2 = new(0x89143C9A, 0x05AF, 0x49B0, 0xB7, 0x17, 0x72, 0xE2, 0x18, 0xA2, 0x18, 0x5C);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_ISetupInstance2);
    }
#else
    readonly Guid IComIID.Guid => IID_ISetupInstance2;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupInstance2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (ISetupInstance2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (ISetupInstance2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // ISetupInstance vtable layout (indices 3-10): see ISetupInstance.cs.
    // ISetupInstance2 vtable layout (added at indices 11+):
    //   11 = GetState                    (out InstanceState)              <-- Used
    //   12 = GetPackages                 (out SAFEARRAY of ISetupPackageReference*)
    //   13 = GetProduct                  (out ISetupPackageReference**)
    //   14 = GetProductPath              (out BSTR)
    //   15 = GetErrors                   (out ISetupErrorState**)
    //   16 = IsLaunchable                (out VARIANT_BOOL)
    //   17 = IsComplete                  (out VARIANT_BOOL)
    //   18 = GetProperties               (out ISetupPropertyStore**)
    //   19 = GetEnginePath               (out BSTR)

    /// <summary>
    ///  Get the installer-state bitfield. Compare against <see cref="InstanceState.Complete"/>
    ///  to filter out partial installations that the user should not be invoking against.
    /// </summary>
    public HRESULT GetState(InstanceState* pState)
    {
        fixed (ISetupInstance2* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, InstanceState*, HRESULT>)_lpVtbl[11])(pThis, pState);
        }
    }
}

#endif
