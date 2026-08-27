// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copied from dotnet/winforms (System.Private.Windows.Core) and adapted for
// MSBuild's CsWin32 layout. See:
// https://github.com/dotnet/winforms/blob/main/src/System.Private.Windows.Core/src/Windows/Win32/System/Com/GlobalInterfaceTable.cs

using System;
using System.Runtime.Versioning;
using Windows.Win32.Foundation;

namespace Windows.Win32.System.Com;

/// <summary>
///  Wrapper for the COM global interface table (StdGlobalInterfaceTable).
///  Used to obtain thread-agile access to COM pointers held across thread
///  boundaries (and held in managed class fields).
/// </summary>
internal static unsafe class GlobalInterfaceTable
{
    // CLSID_StdGlobalInterfaceTable = {00000323-0000-0000-C000-000000000046}
    private static readonly Guid s_clsidStdGIT = new(0x00000323, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);

    [SupportedOSPlatform("windows5.0")]
    private static readonly IGlobalInterfaceTable* s_globalInterfaceTable = CreateGlobalInterfaceTable();

    [SupportedOSPlatform("windows5.0")]
    private static IGlobalInterfaceTable* CreateGlobalInterfaceTable()
    {
        IGlobalInterfaceTable* git;
        Guid clsid = s_clsidStdGIT;
        Guid iid = IID.Get<IGlobalInterfaceTable>();
        PInvoke.CoCreateInstance(
            &clsid,
            pUnkOuter: null,
            CLSCTX.CLSCTX_INPROC_SERVER,
            &iid,
            (void**)&git).ThrowOnFailure();
        return git;
    }

    /// <summary>
    ///  Registers <paramref name="interface"/> in the GIT. The GIT adds its own ref
    ///  count, so callers can release their reference if they want the GIT entry to own it.
    /// </summary>
    /// <returns>The cookie used to refer to the interface in the table.</returns>
    [SupportedOSPlatform("windows5.0")]
    public static uint RegisterInterface<TInterface>(TInterface* @interface)
        where TInterface : unmanaged, IComIID
    {
        uint cookie;
        Guid iid = IID.Get<TInterface>();
        s_globalInterfaceTable->RegisterInterfaceInGlobal(
            (IUnknown*)@interface,
            &iid,
            &cookie).ThrowOnFailure();
        return cookie;
    }

    /// <summary>
    ///  Gets an agile interface for <paramref name="cookie"/> returned by
    ///  <see cref="RegisterInterface{TInterface}(TInterface*)"/>.
    /// </summary>
    [SupportedOSPlatform("windows5.0")]
    public static ComScope<TInterface> GetInterface<TInterface>(uint cookie, out HRESULT result)
        where TInterface : unmanaged, IComIID
    {
        Guid iid = IID.Get<TInterface>();
        ComScope<TInterface> @interface = new(null);
        result = s_globalInterfaceTable->GetInterfaceFromGlobal(cookie, &iid, (void**)&@interface);
        return @interface;
    }

    /// <summary>
    ///  Revokes a registered interface, decrementing the GIT's ref count on it.
    /// </summary>
    [SupportedOSPlatform("windows5.0")]
    public static HRESULT RevokeInterface(uint cookie)
        => s_globalInterfaceTable->RevokeInterfaceFromGlobal(cookie);
}
