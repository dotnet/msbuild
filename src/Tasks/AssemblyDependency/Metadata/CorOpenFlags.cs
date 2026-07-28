// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Tasks.Metadata;

/// <summary>
///  Flags controlling <see cref="IMetaDataDispenser.OpenScope"/> behavior. Mirrors
///  the native <c>CorOpenFlags</c> enum from <c>corhdr.h</c>.
/// </summary>
/// <remarks>
///  <para>
///   Layout (from <c>corhdr.h</c>):
///   <code>
///   typedef enum CorOpenFlags
///   {
///       ofRead              =   0x00000000,
///       ofWrite             =   0x00000001,
///       ofReadWriteMask     =   0x00000001,
///       ofCopyMemory        =   0x00000002,
///       ofReadOnly          =   0x00000010,
///       ofTakeOwnership     =   0x00000020,
///       ofNoTypeLib         =   0x00000080,
///       ofCheckIntegrity    =   0x00000800,
///       ofNoTransform       =   0x00001000,
///       ofReserved1         =   0x00000100,
///       ofReserved2         =   0x00000200,
///       ofReserved3         =   0x00000400,
///       ofReserved          =   0xffffe740
///   } CorOpenFlags;
///   </code>
///  </para>
///  <para>
///   <c>ofCacheImage</c> (0x4) and <c>ofManifestMetadata</c> (0x8) are obsolete and
///   ignored by the CLR; they are intentionally not exposed here. Backing type is
///   <see cref="uint"/> so the high-bit <see cref="ofReserved"/> mask round-trips.
///  </para>
/// </remarks>
[Flags]
internal enum CorOpenFlags : uint
{
    /// <summary>Open scope for read (default; value 0).</summary>
    ofRead = 0x00000000,

    /// <summary>Open scope for write.</summary>
    ofWrite = 0x00000001,

    /// <summary>Mask covering the read/write bit.</summary>
    ofReadWriteMask = 0x00000001,

    /// <summary>Open scope with memory; metadata maintains its own copy of the buffer.</summary>
    ofCopyMemory = 0x00000002,

    /// <summary>Open scope read-only. The returned object cannot be QI'd for <c>IMetaDataEmit*</c>.</summary>
    ofReadOnly = 0x00000010,

    /// <summary>The memory was allocated with <c>CoTaskMemAlloc</c> and will be freed by the metadata engine.</summary>
    ofTakeOwnership = 0x00000020,

    /// <summary>Reserved for internal use.</summary>
    ofReserved1 = 0x00000100,

    /// <summary>Reserved for internal use.</summary>
    ofReserved2 = 0x00000200,

    /// <summary>Reserved for internal use.</summary>
    ofReserved3 = 0x00000400,

    /// <summary>
    ///  Only open the scope if it passes a code-integrity check. Honored only on machines with
    ///  Device Guard enabled and OS versions that support it.
    /// </summary>
    ofCheckIntegrity = 0x00000800,

    /// <summary>Disable automatic transforms of <c>.winmd</c> files.</summary>
    ofNoTransform = 0x00001000,

    /// <summary>Do not OpenScope on a typelib.</summary>
    ofNoTypeLib = 0x00000080,

    /// <summary>Mask covering all reserved bits.</summary>
    ofReserved = 0xFFFFE740,
}
