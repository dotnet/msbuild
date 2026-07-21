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
///  Provides access to the contents of the global assembly cache.
/// </summary>
/// <remarks>
///  <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/iassemblycache-interface"/>.
///  IID = {E707DCDE-D1CD-11D2-BAB9-00C04F8ECEAE}
/// </remarks>
internal unsafe struct IAssemblyCache : IComIID
{
    public static readonly Guid IID_IAssemblyCache = new(0xE707DCDE, 0xD1CD, 0x11D2, 0xBA, 0xB9, 0x00, 0xC0, 0x4F, 0x8E, 0xCE, 0xAE);

#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in IID_IAssemblyCache);
    }
#else
    readonly Guid IComIID.Guid => IID_IAssemblyCache;
#endif

    private readonly void** _lpVtbl;

    // IUnknown methods (vtable indices 0-2)

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (IAssemblyCache* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyCache*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
        }
    }

    public uint AddRef()
    {
        fixed (IAssemblyCache* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyCache*, uint>)_lpVtbl[1])(pThis);
        }
    }

    public uint Release()
    {
        fixed (IAssemblyCache* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyCache*, uint>)_lpVtbl[2])(pThis);
        }
    }

    // IAssemblyCache vtable layout (indices 3-7):
    //   3 = UninstallAssembly
    //   4 = QueryAssemblyInfo   <-- Used
    //   5 = CreateAssemblyCacheItem
    //   6 = Reserved (was CreateAssemblyScavenger)
    //   7 = InstallAssembly

    /// <summary>
    ///  Retrieves info about an assembly from the cache.
    /// </summary>
    public HRESULT QueryAssemblyInfo(uint dwFlags, char* pszAssemblyName, ASSEMBLY_INFO* pAsmInfo)
    {
        fixed (IAssemblyCache* pThis = &this)
        {
            return ((delegate* unmanaged[Stdcall]<IAssemblyCache*, uint, char*, ASSEMBLY_INFO*, HRESULT>)_lpVtbl[4])(
                pThis, dwFlags, pszAssemblyName, pAsmInfo);
        }
    }
}
