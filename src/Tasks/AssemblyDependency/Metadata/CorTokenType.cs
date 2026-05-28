// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Tasks.Metadata;

/// <summary>
///  Metadata table type stored in the high byte of an <c>mdToken</c>. Mirrors
///  <c>CorTokenType</c> from <c>corhdr.h</c>.
/// </summary>
/// <remarks>
///  <para>
///   An <c>mdToken</c> is a 32-bit value packed as <c>(TableType &lt;&lt; 24) | Rid</c>, where the
///   high byte selects which metadata table the token indexes and the low 24 bits are the row
///   id. Use <c>token &amp; 0xFF000000</c> to extract the table type (and compare against this
///   enum) and <c>token &amp; 0x00FFFFFF</c> to extract the row id.
///  </para>
///  <para>
///   Most token typedefs in <c>corhdr.h</c> (e.g. <c>mdAssembly</c>, <c>mdAssemblyRef</c>,
///   <c>mdFile</c>) carry their corresponding <c>CorTokenType</c> in the high byte. The
///   per-type nil constants are <em>not</em> zero — they are the table-type tag with row id 0
///   (e.g. <c>mdAssemblyNil == mdtAssembly == 0x20000000</c>). Use
///   <see cref="MdToken.IsNil"/> (or the per-type equivalent) which matches the CLR's
///   <c>IsNilToken</c> macro: it checks the row-id half, not the entire value.
///  </para>
/// </remarks>
internal enum CorTokenType : uint
{
    mdtModule = 0x00000000,
    mdtTypeRef = 0x01000000,
    mdtTypeDef = 0x02000000,
    mdtFieldDef = 0x04000000,
    mdtMethodDef = 0x06000000,
    mdtParamDef = 0x08000000,
    mdtInterfaceImpl = 0x09000000,
    mdtMemberRef = 0x0A000000,
    mdtCustomAttribute = 0x0C000000,
    mdtPermission = 0x0E000000,
    mdtSignature = 0x11000000,
    mdtEvent = 0x14000000,
    mdtProperty = 0x17000000,
    mdtMethodImpl = 0x19000000,
    mdtModuleRef = 0x1A000000,
    mdtTypeSpec = 0x1B000000,
    mdtAssembly = 0x20000000,
    mdtAssemblyRef = 0x23000000,
    mdtFile = 0x26000000,
    mdtExportedType = 0x27000000,
    mdtManifestResource = 0x28000000,
    mdtGenericParam = 0x2A000000,
    mdtMethodSpec = 0x2B000000,
    mdtGenericParamConstraint = 0x2C000000,

    mdtString = 0x70000000,
    mdtName = 0x71000000,

    /// <summary>Not an actual table type — sentinel for "the value is not a table token".</summary>
    mdtBaseType = 0x72000000,
}
