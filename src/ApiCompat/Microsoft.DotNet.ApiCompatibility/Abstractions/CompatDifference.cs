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
    public class CompatDifference : IDiagnostic, IEquatable<CompatDifference>
    {
        /// <summary>
        /// The Diagnostic ID for this difference.
        /// </summary>
        public string DiagnosticId { get; }

        /// <summary>
        /// The <see cref="DifferenceType"/>.
        /// </summary>
        public DifferenceType Type { get; }

        /// <summary>
        /// A diagnostic message for the difference.
        /// </summary>
        public virtual string Message { get; }

        /// <summary>
        /// A unique ID in order to identify the API that the difference was raised for.
        /// </summary>
        public string? ReferenceId { get; }

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

        public bool Equals(CompatDifference? other) => other != null &&
            DiagnosticId.Equals(other.DiagnosticId, StringComparison.InvariantCultureIgnoreCase) &&
            Type.Equals(other.Type) &&
            string.Equals(ReferenceId, other.ReferenceId, StringComparison.InvariantCultureIgnoreCase);

        public override bool Equals(object? obj) => obj is CompatDifference difference && Equals(difference);

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
    }
}
