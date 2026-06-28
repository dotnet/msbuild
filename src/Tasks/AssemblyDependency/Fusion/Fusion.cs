// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Fusion (GAC) interop. The Fusion APIs are declared in CLR\src\inc\fusion.idl and
// implemented in CLR\src\fusion\. They are shipped in fusion.dll (a forwarder to
// mscoreei.dll) and are Windows + .NET Framework only.
//
// CsWin32 does not surface the Fusion APIs, so they are declared manually here
// following the CsWin32 struct-based COM pattern (blittable raw pointers +
// function-pointer vtables) used elsewhere in this repository.

using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Microsoft.Build.Tasks.Fusion;

/// <summary>
///  Identifies an assembly cache. Native name <c>ASM_CACHE_FLAGS</c>.
/// </summary>
/// <remarks>
///  <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/asm-cache-flags-enumeration"/>.
///  <c>ASM_CACHE_ROOT</c> (0x8) and <c>ASM_CACHE_ROOT_EX</c> (0x80) are only meaningful
///  with <see cref="NativeMethods.GetCachePath"/> and are intentionally omitted here.
/// </remarks>
[Flags]
internal enum AssemblyCacheFlags
{
    /// <summary>The native image cache (NGen).</summary>
    ZAP = 1,

    /// <summary>The global assembly cache.</summary>
    GAC = 2,

    /// <summary>The download cache (assemblies downloaded on demand or shadow-copied).</summary>
    DOWNLOAD = 4,
}

/// <summary>
///  Flags for <see cref="NativeMethods.CreateAssemblyNameObject"/>. Native name
///  <c>CREATE_ASM_NAME_OBJ_FLAGS</c>.
/// </summary>
/// <remarks>
///  <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/createassemblynameobject-function"/>.
/// </remarks>
internal enum CreateAssemblyNameObjectFlags
{
    /// <summary>Default: create an empty assembly name to be populated via <c>IAssemblyName.SetProperty</c>.</summary>
    CANOF_DEFAULT = 0,

    /// <summary>Parse the supplied display name (e.g. <c>System, Version=4.0.0.0, ...</c>) into the name object.</summary>
    CANOF_PARSE_DISPLAY_NAME = 1,
}

/// <summary>
///  Flags for <see cref="IAssemblyName.GetDisplayName"/>. Native name <c>ASM_DISPLAY_FLAGS</c>
///  (constants are <c>ASM_DISPLAYF_*</c>).
/// </summary>
/// <remarks>
///  <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/asm-display-flags-enumeration"/>.
///  Only the subset historically used by MSBuild is exposed here.
/// </remarks>
[Flags]
internal enum AssemblyNameDisplayFlags
{
    /// <summary>Include the version component (<c>Version=...</c>).</summary>
    VERSION = 0x01,

    /// <summary>Include the culture component (<c>Culture=...</c>).</summary>
    CULTURE = 0x02,

    /// <summary>Include the public key token component (<c>PublicKeyToken=...</c>).</summary>
    PUBLIC_KEY_TOKEN = 0x04,

    /// <summary>Include the processor architecture component (<c>ProcessorArchitecture=...</c>).</summary>
    PROCESSORARCHITECTURE = 0x20,

    /// <summary>Include the retargetable flag (<c>Retargetable=Yes</c>) when set on the assembly.</summary>
    RETARGETABLE = 0x80,

    /// <summary>
    ///  Combined set of components used by MSBuild to obtain a full fusion display name.
    /// </summary>
    /// <remarks>
    ///  Note: this value pre-dates <c>ASM_DISPLAYF_CONTENT_TYPE</c> (0x400) and intentionally
    ///  excludes it to preserve MSBuild's historical display-name format.
    /// </remarks>
    ALL = VERSION | CULTURE | PUBLIC_KEY_TOKEN | PROCESSORARCHITECTURE | RETARGETABLE,
}

/// <summary>
///  Flags for the <c>dwFlags</c> parameter of <see cref="IAssemblyCache.QueryAssemblyInfo"/>.
///  Native name <c>QUERYASMINFO_FLAG_*</c>.
/// </summary>
internal enum ASSEMBLYINFO_FLAG
{
    /// <summary>Validate the assembly bits in the cache (hash check).</summary>
    VALIDATE = 1,

    /// <summary>Return the cached assembly size in <see cref="ASSEMBLY_INFO.uliAssemblySizeInKB"/>.</summary>
    GETSIZE = 2,
}

/// <summary>
///  Returned by <see cref="IAssemblyCache.QueryAssemblyInfo"/> with information about a
///  cached assembly. Native name <c>ASSEMBLY_INFO</c>, defined in fusion.idl.
/// </summary>
/// <remarks>
///  The two-pass calling pattern is:
///  <list type="number">
///    <item>Set <see cref="cbAssemblyInfo"/> to <c>sizeof(ASSEMBLY_INFO)</c>, leave the rest zero, and call.</item>
///    <item><see cref="cchBuf"/> is filled in with the required path buffer size (in chars, including NUL).</item>
///    <item>Allocate the buffer, set <see cref="pszCurrentAssemblyPathBuf"/>, and call again.</item>
///  </list>
///  See <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/assembly-info-structure"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ASSEMBLY_INFO
{
    /// <summary>Size of this structure in bytes; must be initialized by the caller before the first call.</summary>
    public uint cbAssemblyInfo;

    /// <summary>
    ///  Flags describing the cached assembly. <c>ASSEMBLYINFO_FLAG_INSTALLED</c> (0x1) when
    ///  installed, <c>ASSEMBLYINFO_FLAG_PAYLOADRESIDENT</c> (0x2) when payload is resident.
    /// </summary>
    public uint dwAssemblyFlags;

    /// <summary>Total size in KB of all files belonging to the assembly (when <c>GETSIZE</c> is requested).</summary>
    public ulong uliAssemblySizeInKB;

    /// <summary>Caller-provided buffer to receive the path to the cached assembly manifest.</summary>
    public char* pszCurrentAssemblyPathBuf;

    /// <summary>
    ///  In: size (in chars) of <see cref="pszCurrentAssemblyPathBuf"/>. Out: required size,
    ///  including the trailing NUL.
    /// </summary>
    public uint cchBuf;
}

/// <summary>
///  P/Invokes into fusion.dll. Signatures match <c>CLR\src\inc\fusion.idl</c>;
///  implementations live in <c>CLR\src\fusion\</c>. fusion.dll is a forwarder shipped
///  with the .NET Framework, so these APIs are Windows + .NET Framework only.
/// </summary>
internal static unsafe class NativeMethods
{
    /// <summary>
    ///  Returns an <see cref="IAssemblyCache"/> pointer that grants access to the GAC.
    /// </summary>
    /// <param name="ppAsmCache">Out: the newly-created cache pointer.</param>
    /// <param name="dwReserved">Reserved. Must be 0.</param>
    /// <remarks>
    ///  Native signature: <c>STDAPI CreateAssemblyCache(IAssemblyCache** ppAsmCache, DWORD dwReserved)</c>.
    ///  See <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/createassemblycache-function"/>.
    /// </remarks>
    [DllImport("fusion.dll")]
    internal static extern HRESULT CreateAssemblyCache(IAssemblyCache** ppAsmCache, uint dwReserved);

    /// <summary>
    ///  Returns an <see cref="IAssemblyEnum"/> that enumerates the assemblies in the GAC matching
    ///  <paramref name="pName"/> (or all assemblies if <paramref name="pName"/> is <see langword="null"/>).
    /// </summary>
    /// <param name="ppEnum">Out: the newly-created enumerator.</param>
    /// <param name="pUnkReserved">Reserved. Must be <see langword="null"/>.</param>
    /// <param name="pName">Optional partial or full fusion name to filter the enumeration; <see langword="null"/> for "all".</param>
    /// <param name="flags">Which cache to enumerate (only <see cref="AssemblyCacheFlags.GAC"/> is meaningful here in practice).</param>
    /// <param name="pvReserved">Reserved. Must be <see langword="null"/>.</param>
    /// <remarks>
    ///  Native signature: <c>STDAPI CreateAssemblyEnum(IAssemblyEnum** pEnum, IUnknown* pUnkReserved,
    ///  IAssemblyName* pName, DWORD dwFlags, LPVOID pvReserved)</c>.
    ///  See <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/createassemblyenum-function"/>.
    /// </remarks>
    [DllImport("fusion.dll")]
    internal static extern HRESULT CreateAssemblyEnum(
        IAssemblyEnum** ppEnum,
        void* pUnkReserved,
        IAssemblyName* pName,
        AssemblyCacheFlags flags,
        void* pvReserved);

    /// <summary>
    ///  Creates an <see cref="IAssemblyName"/> by parsing <paramref name="szAssemblyName"/>.
    /// </summary>
    /// <param name="ppAssemblyNameObj">Out: the newly-created name object.</param>
    /// <param name="szAssemblyName">A display name (partial or full) such as
    ///  <c>System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</c>.</param>
    /// <param name="flags">Parsing flags. Pass <see cref="CreateAssemblyNameObjectFlags.CANOF_PARSE_DISPLAY_NAME"/>
    ///  to parse a display string; pass <see cref="CreateAssemblyNameObjectFlags.CANOF_DEFAULT"/>
    ///  to receive an empty object to populate manually.</param>
    /// <param name="pvReserved">Reserved. Must be <see langword="null"/>.</param>
    /// <remarks>
    ///  Native signature: <c>STDAPI CreateAssemblyNameObject(LPASSEMBLYNAME* ppAssemblyNameObj,
    ///  LPCWSTR szAssemblyName, DWORD dwFlags, LPVOID pvReserved)</c>.
    ///  See <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/createassemblynameobject-function"/>.
    /// </remarks>
    [DllImport("fusion.dll")]
    internal static extern HRESULT CreateAssemblyNameObject(
        IAssemblyName** ppAssemblyNameObj,
        PCWSTR szAssemblyName,
        CreateAssemblyNameObjectFlags flags,
        void* pvReserved);

    /// <summary>
    ///  Retrieves the root path of the specified assembly cache. Follows the standard
    ///  two-pass Win32 buffer-sizing pattern: call once with <paramref name="cachePath"/> =
    ///  <see langword="null"/> to learn the required size, then call again with a buffer.
    /// </summary>
    /// <param name="cacheFlags">Which cache to query. Typically <see cref="AssemblyCacheFlags.GAC"/>.</param>
    /// <param name="cachePath">Out: caller-allocated buffer of at least <c>*pcchPath</c> chars; may be <see langword="null"/> to query size.</param>
    /// <param name="pcchPath">In/Out: in, the buffer size in chars (including NUL); out, the
    ///  required size (when called with a <see langword="null"/> buffer) or the length written
    ///  (when called with a non-null buffer).</param>
    /// <remarks>
    ///  Native signature: <c>STDAPI GetCachePath(ASM_CACHE_FLAGS dwCacheFlags, LPWSTR pwzCachePath, PDWORD pcchPath)</c>.
    ///  See <see href="https://learn.microsoft.com/dotnet/framework/unmanaged-api/fusion/getcachepath-function"/>.
    /// </remarks>
    [DllImport("fusion.dll")]
    internal static extern HRESULT GetCachePath(AssemblyCacheFlags cacheFlags, PWSTR cachePath, uint* pcchPath);
}
