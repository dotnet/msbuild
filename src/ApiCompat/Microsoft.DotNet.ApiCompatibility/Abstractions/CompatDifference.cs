// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Class representing a difference of compatibility, containing detailed information about it.
    /// </summary>
    public readonly struct CompatDifference : IDiagnostic, IEquatable<CompatDifference>
    {
        /// <inheritdoc />
        public readonly string DiagnosticId { get; }

        /// <summary>
        /// The <see cref="DifferenceType"/>.
        /// </summary>
        public readonly DifferenceType Type { get; }

        /// <inheritdoc />
        public readonly string Message { get; }

        /// <inheritdoc />
        public readonly string? ReferenceId { get; }

        /// <summary>
        /// Instantiate a new object representing the compatibility difference.
        /// </summary>
        /// <param name="id"><see cref="string"/> representing the diagnostic ID.</param>
        /// <param name="message"><see cref="string"/> message describing the difference.</param>
        /// <param name="type"><see cref="DifferenceType"/> to describe the type of the difference.</param>
        /// <param name="member"><see cref="ISymbol"/> for which the difference is associated to.</param>
        public CompatDifference(string diagnosticId, string message, DifferenceType type, ISymbol member)
            : this(diagnosticId, message, type, member.GetDocumentationCommentId())
        {
        }

        /// <summary>
        /// Instantiate a new object representing the compatibility difference.
        /// </summary>
        /// <param name="id"><see cref="string"/> representing the diagnostic ID.</param>
        /// <param name="message"><see cref="string"/> message describing the difference.</param>
        /// <param name="type"><see cref="DifferenceType"/> to describe the type of the difference.</param>
        /// <param name="memberId"><see cref="string"/> containing the member ID for which the difference is associated to.</param>
        public CompatDifference(string diagnosticId, string message, DifferenceType type, string? memberId)
        {
            DiagnosticId = diagnosticId;
            Message = message;
            Type = type;
            ReferenceId = memberId;
        }

        /// <summary>
        /// Gets a <see cref="string"/> representation of the difference.
        /// </summary>
        /// <returns><see cref="string"/> describing the difference.</returns>
        public override string ToString() => $"{DiagnosticId} : {Message}";

        /// <inheritdoc />
        public bool Equals(CompatDifference other) =>
            DiagnosticId.Equals(other.DiagnosticId, StringComparison.InvariantCultureIgnoreCase) &&
            Type.Equals(other.Type) &&
            string.Equals(ReferenceId, other.ReferenceId, StringComparison.InvariantCultureIgnoreCase);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is CompatDifference difference && Equals(difference);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hashCode = 1447485498;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(DiagnosticId.ToLowerInvariant());
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Type.ToString().ToLowerInvariant());
            if (ReferenceId != null)
            {
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ReferenceId.ToLowerInvariant());
            }

            return hashCode;
        }

        /// <inheritdoc />
        public static bool operator ==(CompatDifference left, CompatDifference right) => left.Equals(right);
        
        /// <inheritdoc />
        public static bool operator !=(CompatDifference left, CompatDifference right) => !(left == right);
    }
}
