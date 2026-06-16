// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Metadata;

// Strongly-typed wrappers over the CLR metadata token typedefs from corhdr.h:
//
//   typedef ULONG32 mdToken;                // Generic token
//   typedef mdToken mdAssembly;             // Assembly token.     mdAssemblyNil    = mdtAssembly
//   typedef mdToken mdAssemblyRef;          // AssemblyRef token.  mdAssemblyRefNil = mdtAssemblyRef
//   typedef mdToken mdFile;                 // File token.         mdFileNil        = mdtFile
//   typedef mdToken mdTypeDef;              // TypeDef in this scope
//   typedef mdToken mdMethodDef;            // ... etc.
//
// At the C level they're all interchangeable uints; the typedef names exist purely as
// documentation. Mirroring them as distinct readonly structs in C# recovers compile-time
// type discipline: an mdAssemblyRef can't be silently passed where an mdFile is expected.
// Each wrapper is a single ULONG32 field so it's blittable and ABI-compatible with the
// native uint — `delegate*` and array (`MdAssemblyRef[]`) marshalling are zero-cost.
//
// Encoding (from corhdr.h):
//   token = (TableType << 24) | Rid
//   #define RidFromToken(tk)  ((tk) & 0x00ffffff)
//   #define TypeFromToken(tk) ((tk) & 0xff000000)
//   #define IsNilToken(tk)    (RidFromToken(tk) == 0)
//
// The per-type `Nil` constant is NOT zero — it's the table-type tag with row id 0
// (e.g. mdAssemblyNil = mdtAssembly = 0x20000000). `IsNil` matches the CLR's IsNilToken
// macro: it checks the row-id half, not the entire value.
//
// Conversion follows the typedef hierarchy:
//   - **Implicit** widening from a specific token type to the generic MdToken (always safe —
//     every mdAssembly is an mdToken at the C level), so specific tokens flow naturally into
//     APIs that accept the generic mdToken (e.g. IMetaDataImport2.GetCustomAttributeByName).
//   - **Explicit** narrowing from MdToken to a specific type validates the table-type tag
//     and throws ArgumentException if the source isn't of the claimed kind.
//
// Validation only fires when MANAGED code synthesizes a token (`new MdAssembly(...)` or
// `(MdAssembly)mdToken`). Native code writing into an `MdAssembly*` out-param or an
// `MdAssemblyRef[]` array element bypasses the constructor and stores the value directly —
// preserving zero-cost ABI marshalling. The CLR is trusted to produce correctly-tagged
// tokens; managed C# code must demonstrate intent via the constructor or cast.
//
// `default(MdAssembly)` evaluates to the zero token (`Kind == mdtModule`, `IsValid == false`)
// because default-initialization does not invoke the constructor. Use `MdAssembly.Nil`
// instead to get the canonical CLR nil (table-type tag with row id 0).
//
// Only token types currently used by MSBuild are defined here. Add more as needed.

/// <summary>
///  Generic CLR metadata token. Mirrors <c>mdToken</c> (<c>ULONG32</c>) from <c>corhdr.h</c>.
///  Specific token types (<see cref="MdAssembly"/>, <see cref="MdAssemblyRef"/>,
///  <see cref="MdFile"/>) implicitly convert to this type so they can be passed to APIs
///  that take a generic <c>mdToken</c> argument.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct MdToken : IEquatable<MdToken>
{
    /// <summary>The raw 32-bit token value.</summary>
    public readonly uint Value;

    public MdToken(uint value) => Value = value;

    /// <summary><c>mdTokenNil</c> (0).</summary>
    public static MdToken Nil => default;

    /// <summary>Table-type tag from the high byte (CLR macro: <c>TypeFromToken</c>).</summary>
    public CorTokenType Kind => (CorTokenType)(Value & 0xFF000000u);

    /// <summary>Row id from the low 24 bits (CLR macro: <c>RidFromToken</c>).</summary>
    public uint Rid => Value & 0x00FFFFFFu;

    /// <summary><see langword="true"/> if the row-id half is zero (CLR macro: <c>IsNilToken</c>).</summary>
    public bool IsNil => Rid == 0;

    public bool Equals(MdToken other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is MdToken t && Equals(t);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"0x{Value:x8}";

    public static bool operator ==(MdToken left, MdToken right) => left.Equals(right);
    public static bool operator !=(MdToken left, MdToken right) => !left.Equals(right);
}

/// <summary>
///  Assembly metadata token. Mirrors <c>mdAssembly</c> from <c>corhdr.h</c>
///  (<c>typedef mdToken mdAssembly</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct MdAssembly : IEquatable<MdAssembly>
{
    /// <summary>The raw 32-bit token value.</summary>
    public readonly uint Value;

    public MdAssembly(uint value)
    {
        CorTokenType kind = (CorTokenType)(value & 0xFF000000u);
        if (kind != CorTokenType.mdtAssembly)
        {
            throw new ArgumentException(
                $"Token 0x{value:x8} has table-type tag {kind} (expected {CorTokenType.mdtAssembly}).",
                nameof(value));
        }
        Value = value;
    }

    /// <summary><c>mdAssemblyNil</c> — table-type tag with row id 0 (<c>0x20000000</c>, not 0).</summary>
    public static MdAssembly Nil => new((uint)CorTokenType.mdtAssembly);

    /// <summary>Table-type tag from the high byte. Should equal <see cref="CorTokenType.mdtAssembly"/> for a well-formed token.</summary>
    public CorTokenType Kind => (CorTokenType)(Value & 0xFF000000u);

    /// <summary>Row id from the low 24 bits.</summary>
    public uint Rid => Value & 0x00FFFFFFu;

    /// <summary><see langword="true"/> if the row-id half is zero (the canonical CLR nil check).</summary>
    public bool IsNil => Rid == 0;

    /// <summary><see langword="true"/> if the table-type tag really is <see cref="CorTokenType.mdtAssembly"/>.</summary>
    public bool IsValid => Kind == CorTokenType.mdtAssembly;

    public bool Equals(MdAssembly other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is MdAssembly t && Equals(t);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"0x{Value:x8}";

    public static bool operator ==(MdAssembly left, MdAssembly right) => left.Equals(right);
    public static bool operator !=(MdAssembly left, MdAssembly right) => !left.Equals(right);

    /// <summary>Widens to the generic <see cref="MdToken"/>. Always safe — every <c>mdAssembly</c> is an <c>mdToken</c>.</summary>
    public static implicit operator MdToken(MdAssembly token) => new(token.Value);

    /// <summary>
    ///  Narrows from the generic <see cref="MdToken"/>. Validates that the source token's
    ///  table-type tag is <see cref="CorTokenType.mdtAssembly"/>; throws
    ///  <see cref="ArgumentException"/> otherwise.
    /// </summary>
    public static explicit operator MdAssembly(MdToken token) => new(token.Value);
}

/// <summary>
///  AssemblyRef metadata token. Mirrors <c>mdAssemblyRef</c> from <c>corhdr.h</c>
///  (<c>typedef mdToken mdAssemblyRef</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct MdAssemblyRef : IEquatable<MdAssemblyRef>
{
    /// <summary>The raw 32-bit token value.</summary>
    public readonly uint Value;

    public MdAssemblyRef(uint value)
    {
        CorTokenType kind = (CorTokenType)(value & 0xFF000000u);
        if (kind != CorTokenType.mdtAssemblyRef)
        {
            throw new ArgumentException(
                $"Token 0x{value:x8} has table-type tag {kind} (expected {CorTokenType.mdtAssemblyRef}).",
                nameof(value));
        }
        Value = value;
    }

    /// <summary><c>mdAssemblyRefNil</c> — table-type tag with row id 0 (<c>0x23000000</c>, not 0).</summary>
    public static MdAssemblyRef Nil => new((uint)CorTokenType.mdtAssemblyRef);

    /// <summary>Table-type tag from the high byte. Should equal <see cref="CorTokenType.mdtAssemblyRef"/> for a well-formed token.</summary>
    public CorTokenType Kind => (CorTokenType)(Value & 0xFF000000u);

    /// <summary>Row id from the low 24 bits.</summary>
    public uint Rid => Value & 0x00FFFFFFu;

    /// <summary><see langword="true"/> if the row-id half is zero (the canonical CLR nil check).</summary>
    public bool IsNil => Rid == 0;

    /// <summary><see langword="true"/> if the table-type tag really is <see cref="CorTokenType.mdtAssemblyRef"/>.</summary>
    public bool IsValid => Kind == CorTokenType.mdtAssemblyRef;

    public bool Equals(MdAssemblyRef other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is MdAssemblyRef t && Equals(t);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"0x{Value:x8}";

    public static bool operator ==(MdAssemblyRef left, MdAssemblyRef right) => left.Equals(right);
    public static bool operator !=(MdAssemblyRef left, MdAssemblyRef right) => !left.Equals(right);

    /// <summary>Widens to the generic <see cref="MdToken"/>. Always safe — every <c>mdAssemblyRef</c> is an <c>mdToken</c>.</summary>
    public static implicit operator MdToken(MdAssemblyRef token) => new(token.Value);

    /// <summary>
    ///  Narrows from the generic <see cref="MdToken"/>. Validates that the source token's
    ///  table-type tag is <see cref="CorTokenType.mdtAssemblyRef"/>; throws
    ///  <see cref="ArgumentException"/> otherwise.
    /// </summary>
    public static explicit operator MdAssemblyRef(MdToken token) => new(token.Value);
}

/// <summary>
///  File metadata token. Mirrors <c>mdFile</c> from <c>corhdr.h</c>
///  (<c>typedef mdToken mdFile</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct MdFile : IEquatable<MdFile>
{
    /// <summary>The raw 32-bit token value.</summary>
    public readonly uint Value;

    public MdFile(uint value)
    {
        CorTokenType kind = (CorTokenType)(value & 0xFF000000u);
        if (kind != CorTokenType.mdtFile)
        {
            throw new ArgumentException(
                $"Token 0x{value:x8} has table-type tag {kind} (expected {CorTokenType.mdtFile}).",
                nameof(value));
        }
        Value = value;
    }

    /// <summary><c>mdFileNil</c> — table-type tag with row id 0 (<c>0x26000000</c>, not 0).</summary>
    public static MdFile Nil => new((uint)CorTokenType.mdtFile);

    /// <summary>Table-type tag from the high byte. Should equal <see cref="CorTokenType.mdtFile"/> for a well-formed token.</summary>
    public CorTokenType Kind => (CorTokenType)(Value & 0xFF000000u);

    /// <summary>Row id from the low 24 bits.</summary>
    public uint Rid => Value & 0x00FFFFFFu;

    /// <summary><see langword="true"/> if the row-id half is zero (the canonical CLR nil check).</summary>
    public bool IsNil => Rid == 0;

    /// <summary><see langword="true"/> if the table-type tag really is <see cref="CorTokenType.mdtFile"/>.</summary>
    public bool IsValid => Kind == CorTokenType.mdtFile;

    public bool Equals(MdFile other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is MdFile t && Equals(t);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"0x{Value:x8}";

    public static bool operator ==(MdFile left, MdFile right) => left.Equals(right);
    public static bool operator !=(MdFile left, MdFile right) => !left.Equals(right);

    /// <summary>Widens to the generic <see cref="MdToken"/>. Always safe — every <c>mdFile</c> is an <c>mdToken</c>.</summary>
    public static implicit operator MdToken(MdFile token) => new(token.Value);

    /// <summary>
    ///  Narrows from the generic <see cref="MdToken"/>. Validates that the source token's
    ///  table-type tag is <see cref="CorTokenType.mdtFile"/>; throws
    ///  <see cref="ArgumentException"/> otherwise.
    /// </summary>
    public static explicit operator MdFile(MdToken token) => new(token.Value);
}
