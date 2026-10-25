// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  A normalized path key used for item-specification matching and lazy item evaluation, with a cached hash code.
/// </summary>
/// <remarks>
///  <see cref="LazyItemEvaluator{P, I, M, D}"/> caches these keys on item records and uses them in its
///   normalized-path index and literal Update batches. <see cref="ItemSpecMatcher{P, I}.IntersectsWith"/> matches
///  Remove specifications against that index and returns keys for removing the matching items.
///  Keeping the hash with the path avoids hashing its characters again on each membership lookup.
///  <para>
///   Equality and hashing use <see cref="StringComparer.OrdinalIgnoreCase"/> on every platform, matching
///   the comparison policy of those item-operation lookups rather than filesystem case sensitivity.
///   Construction does not normalize the input; callers supply a path already normalized for comparison.
///  </para>
///  <para>
///   A default-initialized key is invalid; check <see cref="IsDefault"/> before accessing its path to avoid
///   an exception. Item records use a nullable key to represent a path that has not yet been computed.
///  </para>
/// </remarks>
internal readonly struct NormalizedItemPath : IEquatable<NormalizedItemPath>
{
    private readonly int _hashCode;
    private readonly string? _value;

    /// <summary>
    ///  Gets the normalized path.
    /// </summary>
    /// <exception cref="InvalidOperationException">The key was default-initialized rather than constructed with a path.</exception>
    public string Value => _value ?? throw new InvalidOperationException();

    /// <summary>
    ///  Gets whether this key was default-initialized and has no path.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_value))]
    public bool IsDefault => _value is null;

    /// <summary>
    ///  Captures a normalized path and computes its hash once.
    /// </summary>
    /// <param name="value">The path already normalized for comparison.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public NormalizedItemPath(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
        _hashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(value);
    }

    /// <summary>
    ///  Compares normalized paths using ordinal-ignore-case equality.
    /// </summary>
    /// <param name="other">The key to compare.</param>
    /// <returns>
    ///  <see langword="true"/> when both paths are equal ignoring case.
    /// </returns>
    public bool Equals(NormalizedItemPath other)
        => StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);

    public override bool Equals(object? obj)
        => obj is NormalizedItemPath other && Equals(other);

    /// <summary>
    ///  Returns the cached hash without inspecting the path's characters.
    /// </summary>
    /// <returns>
    ///  The ordinal-ignore-case path hash computed during construction.
    /// </returns>
    public override int GetHashCode()
        => _hashCode;

    public static bool operator ==(NormalizedItemPath left, NormalizedItemPath right)
        => left.Equals(right);

    public static bool operator !=(NormalizedItemPath left, NormalizedItemPath right)
        => !left.Equals(right);
}
