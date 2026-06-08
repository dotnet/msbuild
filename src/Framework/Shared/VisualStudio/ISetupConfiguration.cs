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
///  Top-level Visual Studio Setup Configuration query interface (v1). MSBuild only needs
///  the IUnknown surface here so it can QI to <see cref="ISetupConfiguration2"/>; the
///  v1-specific vtable slots (3–5) are intentionally not exposed.
/// </summary>
/// <remarks>
///  Declared in <c>Setup.Configuration.idl</c>. IID = <c>{42843719-DB4C-46C2-8E7C-64F1816EFD5B}</c>.
/// </remarks>
internal unsafe struct ISetupConfiguration : IComIID
{
    public static readonly Guid IID_ISetupConfiguration = new(0x42843719, 0xDB4C, 0x46C2, 0x8E, 0x7C, 0x64, 0xF1, 0x81, 0x6E, 0xFD, 0x5B);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_ISetupConfiguration);
    }
#else
    readonly Guid IComIID.Guid => IID_ISetupConfiguration;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupConfiguration* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (ISetupConfiguration* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (ISetupConfiguration* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration*, uint>)_lpVtbl[2])(pThis);
        }
    }
}

#endif
