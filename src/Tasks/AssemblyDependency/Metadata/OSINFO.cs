// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Metadata;

/// <summary>
///  Describes the target operating system of an assembly. Mirrors the native <c>OSINFO</c>
///  struct from <c>cor.h</c>; the field values use the same constants as the Win32
///  <c>OSVERSIONINFO</c> structure (i.e. what <c>GetVersionEx</c> returns).
/// </summary>
/// <remarks>
///  <para>
///   Layout (from <c>cor.h</c>):
///   <code>
///   typedef struct
///   {
///       DWORD       dwOSPlatformId;         // Operating system platform.
///       DWORD       dwOSMajorVersion;       // OS Major version.
///       DWORD       dwOSMinorVersion;       // OS Minor version.
///   } OSINFO;
///   </code>
///  </para>
///  <para>
///   <b>Allocation:</b> this struct is blittable and has no embedded pointers, so it carries
///   no allocation requirements of its own. A caller that wants the CLR metadata APIs to fill
///   in OS targeting information must allocate an <c>OSINFO[]</c> (typically with
///   <see langword="stackalloc"/>) and point <see cref="ASSEMBLYMETADATA.rOS"/> at the first
///   element, setting <see cref="ASSEMBLYMETADATA.ulOS"/> to the capacity. RAR does not consume
///   OS info, so the array is left null and no allocation is performed.
///  </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct OSINFO
{
    /// <summary>Operating system platform identifier (e.g. <c>VER_PLATFORM_WIN32_NT</c>).</summary>
    public uint dwOSPlatformId;

    /// <summary>Operating system major version.</summary>
    public uint dwOSMajorVersion;

    /// <summary>Operating system minor version.</summary>
    public uint dwOSMinorVersion;
}
