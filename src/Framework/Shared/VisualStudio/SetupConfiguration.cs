// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Manually-defined Visual Studio Setup Configuration COM structs following CsWin32
// struct-based COM patterns. The Setup Configuration COM API is not in Win32 metadata
// (it ships with the VS installer redistributable as
// Microsoft.VisualStudio.Setup.Configuration.Native.dll); declarations from the
// Setup.Configuration type library.

#if FEATURE_WINDOWSINTEROP && FEATURE_VISUALSTUDIOSETUP

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Shared.VisualStudio;

/// <summary>
///  Activation entry points and well-known IIDs for the Visual Studio Setup Configuration
///  COM API exposed by <c>Microsoft.VisualStudio.Setup.Configuration.Native.dll</c>.
/// </summary>
internal static unsafe class SetupConfiguration
{
    /// <summary>
    ///  CLSID of the <c>SetupConfiguration</c> coclass (the entry-point activatable object).
    ///  <c>CoCreate</c> this CLSID against <see cref="ISetupConfiguration2"/> /
    ///  <see cref="ISetupConfiguration"/> to obtain the top-level query interface.
    /// </summary>
    public static readonly Guid CLSID_SetupConfiguration = new(0x177F0C4A, 0x1CD3, 0x4DE7, 0xA3, 0x2C, 0x71, 0xDB, 0xBB, 0x9F, 0xA3, 0x6D);

    /// <summary>
    ///  Fallback activation entry point that lives next to the coclass when the CLSID is
    ///  not registered with COM (which happens when the helper DLL has not been registered
    ///  on the machine — e.g. fresh CI agent without the VS installer). Returns a raw
    ///  <see cref="ISetupConfiguration"/> pointer suitable for QI'ing to
    ///  <see cref="ISetupConfiguration2"/>.
    /// </summary>
    /// <remarks>
    ///  Signature from <c>Setup.Configuration.idl</c>:
    ///  <c>STDAPI GetSetupConfiguration(_Outptr_ ISetupConfiguration **ppConfiguration, _Reserved_ LPVOID lpReserved);</c>.
    ///  The <c>ISetupConfiguration**</c> parameter accepts <c>ComScope&lt;ISetupConfiguration&gt;</c>
    ///  directly via its implicit <c>T**</c> operator.
    /// </remarks>
    [DllImport("Microsoft.VisualStudio.Setup.Configuration.Native.dll", ExactSpelling = true, PreserveSig = true)]
    public static extern int GetSetupConfiguration(ISetupConfiguration** ppConfiguration, IntPtr lpReserved);
}

#endif
