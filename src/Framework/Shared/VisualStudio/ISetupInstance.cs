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
///  A single installed Visual Studio instance. MSBuild only reads three identity fields
///  (installation path, display name, installation version); the rest of the v1 vtable
///  is intentionally not exposed.
/// </summary>
/// <remarks>
///  Declared in <c>Setup.Configuration.idl</c>. IID = <c>{B41463C3-8866-43B5-BC33-2B0676F7F42E}</c>.
///  String returns are <see cref="BSTR"/>; callers own the BSTR and must release it via
///  <c>SysFreeString</c>.
/// </remarks>
internal unsafe struct ISetupInstance : IComIID
{
    public static readonly Guid IID_ISetupInstance = new(0xB41463C3, 0x8866, 0x43B5, 0xBC, 0x33, 0x2B, 0x06, 0x76, 0xF7, 0xF4, 0x2E);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_ISetupInstance);
    }
#else
    readonly Guid IComIID.Guid => IID_ISetupInstance;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupInstance* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (ISetupInstance* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (ISetupInstance* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // ISetupInstance vtable layout (indices 3-10):
    //    3 = GetInstanceId          (out BSTR)
    //    4 = GetInstallDate         (out FILETIME)
    //    5 = GetInstallationName    (out BSTR)
    //    6 = GetInstallationPath    (out BSTR)        <-- Used
    //    7 = GetInstallationVersion (out BSTR)        <-- Used
    //    8 = GetDisplayName         (LCID, out BSTR)  <-- Used
    //    9 = GetDescription         (LCID, out BSTR)
    //   10 = ResolvePath            (LPCWSTR, out BSTR)

    /// <summary>Get the absolute installation path of this instance (typically <c>...\Microsoft Visual Studio\YYYY\Edition</c>).</summary>
    public HRESULT GetInstallationPath(BSTR* pbstrInstallationPath)
    {
        fixed (ISetupInstance* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrInstallationPath);
        }
    }

    /// <summary>Get the installation version (e.g. <c>"17.8.0.0"</c>); parseable by <see cref="System.Version"/>.</summary>
    public HRESULT GetInstallationVersion(BSTR* pbstrInstallationVersion)
    {
        fixed (ISetupInstance* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, BSTR*, HRESULT>)_lpVtbl[7])(pThis, pbstrInstallationVersion);
        }
    }

    /// <summary>Get the localized display name (e.g. <c>"Visual Studio Enterprise 2022"</c>).</summary>
    /// <param name="lcid">LCID for localization; <c>0</c> for the current user UI culture.</param>
    /// <param name="pbstrDisplayName">Receives the localized display name as a freshly-allocated BSTR; the caller owns it and must release it via <c>SysFreeString</c>.</param>
    public HRESULT GetDisplayName(uint lcid, BSTR* pbstrDisplayName)
    {
        fixed (ISetupInstance* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, uint, BSTR*, HRESULT>)_lpVtbl[8])(pThis, lcid, pbstrDisplayName);
        }
    }
}

#endif
