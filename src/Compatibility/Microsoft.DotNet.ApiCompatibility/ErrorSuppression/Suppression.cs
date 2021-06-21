// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Xml.Serialization;

namespace Microsoft.DotNet.Compatibility.ErrorSuppression
{
    /// <summary>
    /// Represents a Suppression for a validation error.
    /// </summary>
    public class Suppression : IEquatable<Suppression>
    {
        /// <summary>
        /// The DiagnosticId representing the error to be suppressed.
        /// </summary>
        public string? DiagnosticId { get; set; }

        /// <summary>
        /// The target of where to suppress the <see cref="DiagnosticId"/>
        /// </summary>
        public string? Target { get; set; }

        /// <summary>
        /// Left operand of an APICompat comparison.
        /// </summary>
        public string? Left { get; set; }

        /// <summary>
        /// Right operand of an APICompat comparison.
        /// </summary>
        public string? Right { get; set; }

        /// <summary>
        /// <see langword="true"/> if the suppression is to be applied to a baseline validation. <see langword="false"/> otherwise.
        /// </summary>
        public bool IsBaselineSuppression { get; set; }

        // It only makes sense to serialize IsBaselineSuppression when is true, if it is off, no need to have it on the file.
        public bool ShouldSerializeIsBaselineSuppression() => IsBaselineSuppression;

        /// <inheritdoc/>
        public bool Equals(Suppression? other)
        {
            return other != null &&
                   AreEqual(DiagnosticId, other.DiagnosticId) &&
                   AreEqual(Target, other.Target) &&
                   AreEqual(Left, other.Left) &&
                   AreEqual(Right, other.Right) &&
                   IsBaselineSuppression == other.IsBaselineSuppression;

            static bool AreEqual(string? first, string? second)
                => string.IsNullOrEmpty(first?.Trim()) && string.IsNullOrEmpty(second?.Trim()) || StringComparer.InvariantCultureIgnoreCase.Equals(first?.Trim(), second?.Trim());
        }

        public override int GetHashCode() => HashCode.Combine(DiagnosticId?.ToLowerInvariant(), Target?.ToLowerInvariant(), Left?.ToLowerInvariant(), Right?.ToLowerInvariant(), IsBaselineSuppression);
    }
}
