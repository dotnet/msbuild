// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// CLR metadata (Cor*) interop. The Cor metadata APIs are declared in
// CLR\src\inc\cor.h (`IMetaDataDispenser`, `IMetaDataImport2`,
// `IMetaDataAssemblyImport`) and implemented inside `mscoree.dll`/`clr.dll`
// (the CLR metadata engine). They are not part of Win32 and are therefore not
// surfaced by CsWin32.
//
// Declarations are authored manually here following the CsWin32 struct-based
// COM pattern (blittable raw pointers + function-pointer vtables) used
// elsewhere in this repository (see `src/Framework/Utilities/Wmi/` and
// `src/Tasks/AssemblyDependency/Fusion/`). Only the vtable slots actually
// invoked by MSBuild are exposed; the remaining slots are documented in
// each interface file so future additions land on the correct index.

using System;

namespace Microsoft.Build.Tasks.Metadata;

/// <summary>
///  Class identifiers and helpers for CLR metadata COM activation.
/// </summary>
internal static class CorMetadata
{
    /// <summary>
    ///  CLSID of <c>CorMetaDataDispenser</c>, the CLR metadata dispenser coclass.
    /// </summary>
    /// <remarks>
    ///  Defined in <c>cor.h</c>:
    ///  <code>EXTERN_GUID(CLSID_CorMetaDataDispenser, 0xe5cb7a31, 0x7512, 0x11d2, 0x89, 0xce, 0x00, 0x80, 0xc7, 0x92, 0xe5, 0xd8);</code>
    ///  CoCreate this CLSID against <see cref="IMetaDataDispenser"/> to obtain the
    ///  dispenser; the returned object also implements <c>IMetaDataDispenserEx</c>.
    /// </remarks>
    public static readonly Guid CLSID_CorMetaDataDispenser = new(0xE5CB7A31, 0x7512, 0x11d2, 0x89, 0xCE, 0x00, 0x80, 0xC7, 0x92, 0xE5, 0xD8);
}
