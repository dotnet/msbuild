// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Tasks.Metadata;

/// <summary>
///  Assembly attribute bits used by <c>DefineAssembly</c> / returned from
///  <c>GetAssemblyProps</c> / <c>GetAssemblyRefProps</c>. Mirrors the native
///  <c>CorAssemblyFlags</c> enum from <c>corhdr.h</c>.
/// </summary>
/// <remarks>
///  <para>
///   Layout (from <c>corhdr.h</c>):
///   <code>
///   typedef enum CorAssemblyFlags
///   {
///       afPublicKey             =   0x0001,
///       afPA_None               =   0x0000,
///       afPA_MSIL               =   0x0010,
///       afPA_x86                =   0x0020,
///       afPA_IA64               =   0x0030,
///       afPA_AMD64              =   0x0040,
///       afPA_ARM                =   0x0050,
///       afPA_ARM64              =   0x0060,
///       afPA_NoPlatform         =   0x0070,
///       afPA_Specified          =   0x0080,
///       afPA_Mask               =   0x0070,
///       afPA_FullMask           =   0x00F0,
///       afPA_Shift              =   0x0004,   // NOT A FLAG; shift count
///       afEnableJITcompileTracking   =  0x8000,
///       afDisableJITcompileOptimizer =  0x4000,
///       afRetargetable          =   0x0100,
///       afContentType_Default         = 0x0000,
///       afContentType_WindowsRuntime  = 0x0200,
///       afContentType_Mask            = 0x0E00,
///   } CorAssemblyFlags;
///   </code>
///  </para>
/// </remarks>
[Flags]
internal enum CorAssemblyFlags : uint
{
    /// <summary>The assembly ref holds the full (unhashed) public key.</summary>
    afPublicKey = 0x0001,

    /// <summary>Processor Architecture unspecified.</summary>
    afPA_None = 0x0000,

    /// <summary>Processor Architecture: neutral (PE32).</summary>
    afPA_MSIL = 0x0010,

    /// <summary>Processor Architecture: x86 (PE32).</summary>
    afPA_x86 = 0x0020,

    /// <summary>Processor Architecture: Itanium (PE32+).</summary>
    afPA_IA64 = 0x0030,

    /// <summary>Processor Architecture: AMD x64 (PE32+).</summary>
    afPA_AMD64 = 0x0040,

    /// <summary>Processor Architecture: ARM (PE32).</summary>
    afPA_ARM = 0x0050,

    /// <summary>Processor Architecture: ARM64 (PE32+).</summary>
    afPA_ARM64 = 0x0060,

    /// <summary>
    ///  Applies to any platform but cannot run on any (e.g. reference assembly). Should not
    ///  have <see cref="afPA_Specified"/> set.
    /// </summary>
    afPA_NoPlatform = 0x0070,

    /// <summary>Propagate PA flags to AssemblyRef record.</summary>
    afPA_Specified = 0x0080,

    /// <summary>Bits describing the processor architecture.</summary>
    afPA_Mask = 0x0070,

    /// <summary>Bits describing the PA including <see cref="afPA_Specified"/>.</summary>
    afPA_FullMask = 0x00F0,

    /// <summary>Shift count for PA flag &lt;-&gt; index conversion. <em>Not</em> a flag bit.</summary>
    afPA_Shift = 0x0004,

    /// <summary>From <c>DebuggableAttribute</c>.</summary>
    afEnableJITcompileTracking = 0x8000,

    /// <summary>From <c>DebuggableAttribute</c>.</summary>
    afDisableJITcompileOptimizer = 0x4000,

    /// <summary>The assembly can be retargeted (at runtime) to an assembly from a different publisher.</summary>
    afRetargetable = 0x0100,

    /// <summary>Default content type (managed assembly).</summary>
    afContentType_Default = 0x0000,

    /// <summary>Windows Runtime content type.</summary>
    afContentType_WindowsRuntime = 0x0200,

    /// <summary>Bits describing the content type.</summary>
    afContentType_Mask = 0x0E00,
}
