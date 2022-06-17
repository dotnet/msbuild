// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

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
        public string ReferenceId { get; }

        /// <summary>
        /// Instantiate a new object representing the compatibility difference.
        /// </summary>
        /// <param name="id"><see cref="string"/> representing the diagnostic ID.</param>
        /// <param name="message"><see cref="string"/> message describing the difference.</param>
        /// <param name="type"><see cref="DifferenceType"/> to describe the type of the difference.</param>
        /// <param name="member"><see cref="ISymbol"/> for which the difference is associated to.</param>
        public CompatDifference(string diagnosticId, string message, DifferenceType type, ISymbol member)
            : this(diagnosticId, message, type, member?.GetDocumentationCommentId())
        {
        }

        /// <summary>
        /// Instantiate a new object representing the compatibility difference.
        /// </summary>
        /// <param name="id"><see cref="string"/> representing the diagnostic ID.</param>
        /// <param name="message"><see cref="string"/> message describing the difference.</param>
        /// <param name="type"><see cref="DifferenceType"/> to describe the type of the difference.</param>
        /// <param name="memberId"><see cref="string"/> containing the member ID for which the difference is associated to.</param>
        public CompatDifference(string diagnosticId, string message, DifferenceType type, string memberId)
        {
            DiagnosticId = diagnosticId ?? throw new ArgumentNullException(nameof(diagnosticId));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Type = type;
            ReferenceId = memberId ?? throw new ArgumentNullException(nameof(memberId));
        }

        /// <summary>
        /// Gets a <see cref="string"/> representation of the difference.
        /// </summary>
        /// <returns><see cref="string"/> describing the difference.</returns>
        public override string ToString() => $"{DiagnosticId} : {Message}";

        public bool Equals(CompatDifference other) => other != null &&
                                                      (object.ReferenceEquals(this, other) ||
                                                      (DiagnosticId.Equals(other.DiagnosticId, StringComparison.InvariantCultureIgnoreCase) &
                                                      Type.Equals(other.Type) &
                                                      ReferenceId.Equals(other.ReferenceId, StringComparison.InvariantCultureIgnoreCase)));

        public override bool Equals(object obj) => Equals(obj as CompatDifference);

        public override int GetHashCode()
        {
            int hashCode = 1447485498;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(DiagnosticId?.ToLowerInvariant() ?? string.Empty);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Type.ToString().ToLowerInvariant() ?? string.Empty);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ReferenceId?.ToLowerInvariant() ?? string.Empty);
            return hashCode;
        }
    }
}
