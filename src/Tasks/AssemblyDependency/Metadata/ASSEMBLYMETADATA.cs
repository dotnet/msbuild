// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Microsoft.Build.Tasks.Metadata;

/// <summary>
///  Identity metadata describing the version, culture, processor, and OS targeting of an
///  assembly or assembly reference. Populated as an out-parameter by
///  <see cref="IMetaDataAssemblyImport.GetAssemblyProps"/> and
///  <see cref="IMetaDataAssemblyImport.GetAssemblyRefProps"/>, and supplied as input to the
///  corresponding emit APIs. Mirrors the native <c>ASSEMBLYMETADATA</c> struct from <c>cor.h</c>.
/// </summary>
/// <remarks>
///  <para>
///   Layout (from <c>cor.h</c>):
///   <code>
///   typedef struct
///   {
///       USHORT      usMajorVersion;         // Major Version.
///       USHORT      usMinorVersion;         // Minor Version.
///       USHORT      usBuildNumber;          // Build Number.
///       USHORT      usRevisionNumber;       // Revision Number.
///       LPWSTR      szLocale;               // Locale.
///       ULONG       cbLocale;               // [IN/OUT] Size of the buffer in wide chars/Actual size.
///       DWORD       *rProcessor;            // Processor ID array.
///       ULONG       ulProcessor;            // [IN/OUT] Size of the Processor ID array/Actual # of entries filled in.
///       OSINFO      *rOS;                   // OSINFO array.
///       ULONG       ulOS;                   // [IN/OUT] Size of the OSINFO array/Actual # of entries filled in.
///   } ASSEMBLYMETADATA;
///   </code>
///   Field names mirror <c>cor.h</c> verbatim, including the misleading <c>cb*</c> / <c>ul*</c>
///   prefixes (see the individual field doc-comments).
///  </para>
///  <para>
///   <b>Allocation contract.</b> The CLR metadata APIs never allocate the buffers referenced
///   by this struct — the caller owns every pointer:
///   <list type="bullet">
///    <item>
///     <description>
///      <see cref="szLocale"/> must point at a caller-allocated wide-char buffer of capacity
///      <see cref="cbLocale"/>. The CLR writes the locale string (with terminating NUL) and
///      updates <see cref="cbLocale"/> to the actual count written. Pass a null pointer with
///      <see cref="cbLocale"/> = 0 to query the required size; the CLR returns
///      <c>CLDB_S_TRUNCATION</c> and writes the required count into <see cref="cbLocale"/>.
///     </description>
///    </item>
///    <item>
///     <description>
///      <see cref="rProcessor"/> / <see cref="rOS"/> are parallel arrays of capacity
///      <see cref="ulProcessor"/> / <see cref="ulOS"/>. Pass null pointers with the counts at
///      zero when the caller does not need that data; the CLR will skip writing them.
///     </description>
///    </item>
///   </list>
///  </para>
///  <para>
///   <b>No COM task memory is involved.</b> Because the struct is blittable and the CLR does
///   not allocate behind the pointers, the entire structure can live on the stack:
///   <code>
///   char* localeBuffer = stackalloc char[64];
///   ASSEMBLYMETADATA asmMeta = new() { szLocale = localeBuffer, cbLocale = 64 };
///   asmImport-&gt;GetAssemblyProps(..., &amp;asmMeta, ...).ThrowOnFailure();
///   </code>
///   No <c>Marshal.AllocCoTaskMem</c> / <c>StructureToPtr</c> / <c>DestroyStructure</c> /
///   <c>FreeCoTaskMem</c> plumbing is needed — passing <c>&amp;asmMeta</c> directly into a
///   <c>void* pMetaData</c> parameter is correct and zero-cost.
///  </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ASSEMBLYMETADATA
{
    /// <summary>Assembly major version.</summary>
    public ushort usMajorVersion;

    /// <summary>Assembly minor version.</summary>
    public ushort usMinorVersion;

    /// <summary>Assembly build number.</summary>
    public ushort usBuildNumber;

    /// <summary>Assembly revision number.</summary>
    public ushort usRevisionNumber;

    /// <summary>
    ///  Pointer to a caller-allocated wide-char buffer that receives the assembly's culture
    ///  (locale) string, NUL-terminated. May be null to query the required buffer size — see
    ///  <see cref="cbLocale"/>.
    /// </summary>
    public PWSTR szLocale;

    /// <summary>
    ///  [IN/OUT] Capacity of <see cref="szLocale"/> on entry; number of wide characters
    ///  written (including the terminating NUL) on return. Despite the <c>cb</c> prefix,
    ///  this is a <em>wide character</em> count, not a byte count (see the <c>cor.h</c>
    ///  comment quoted on the type).
    /// </summary>
    public uint cbLocale;

    /// <summary>
    ///  Pointer to a caller-allocated <see langword="uint"/>[] that receives the processor
    ///  ID array. May be null when the caller does not need processor targeting info.
    /// </summary>
    public uint* rProcessor;

    /// <summary>
    ///  [IN/OUT] Capacity of <see cref="rProcessor"/> on entry; number of entries written
    ///  on return. Set to 0 alongside a null <see cref="rProcessor"/> when not requesting
    ///  processor info.
    /// </summary>
    public uint ulProcessor;

    /// <summary>
    ///  Pointer to a caller-allocated <see cref="OSINFO"/>[] that receives the OS targeting
    ///  array. May be null when the caller does not need OS targeting info.
    /// </summary>
    public OSINFO* rOS;

    /// <summary>
    ///  [IN/OUT] Capacity of <see cref="rOS"/> on entry; number of entries written on return.
    ///  Set to 0 alongside a null <see cref="rOS"/> when not requesting OS info.
    /// </summary>
    public uint ulOS;
}
