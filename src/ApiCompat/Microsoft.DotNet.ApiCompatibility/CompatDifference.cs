// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class representing a difference of compatibility, containing detailed information about it.
    /// </summary>
    public readonly struct CompatDifference : IDiagnostic, IEquatable<CompatDifference>
    {
        /// <inheritdoc />
        public string DiagnosticId { get; }

        /// <summary>
        /// The <see cref="DifferenceType"/>.
        /// </summary>
        public DifferenceType Type { get; }

        /// <inheritdoc />
        public string Message { get; }

        /// <inheritdoc />
        public string? ReferenceId { get; }

        /// <summary>
        ///  The left's metadata information.
        /// </summary>
        public MetadataInformation Left { get; }

        /// <summary>
        /// The right's metadata information.
        /// </summary>
        public MetadataInformation Right { get; }

        /// <summary>
        /// Instantiate a new object representing the compatibility difference.
        /// </summary>
        /// <param name="left">The metadata information of the left comparison side.</param>
        /// <param name="right">The metadata information of the right comparison side.</param>
        /// <param name="diagnosticId"><see cref="string"/> representing the diagnostic ID.</param>
        /// <param name="message"><see cref="string"/> message describing the difference.</param>
        /// <param name="type"><see cref="DifferenceType"/> to describe the type of the difference.</param>
        /// <param name="member"><see cref="ISymbol"/> for which the difference is associated to.</param>
        public CompatDifference(MetadataInformation left, MetadataInformation right, string diagnosticId, string message, DifferenceType type, ISymbol member)
            : this(left, right, diagnosticId, message, type, member.GetDocumentationCommentId())
        {
        }

        /// <summary>
        /// Instantiate a new object representing the compatibility difference.
        /// </summary>
        /// <param name="left">The metadata information of the left comparison side.</param>
        /// <param name="right">The metadata information of the right comparison side.</param>
        /// <param name="diagnosticId"><see cref="string"/> representing the diagnostic ID.</param>
        /// <param name="message"><see cref="string"/> message describing the difference.</param>
        /// <param name="type"><see cref="DifferenceType"/> to describe the type of the difference.</param>
        /// <param name="memberId"><see cref="string"/> containing the member ID for which the difference is associated to.</param>
        public CompatDifference(MetadataInformation left, MetadataInformation right, string diagnosticId, string message, DifferenceType type, string? memberId)
        {
            Left = left;
            Right = right;
            DiagnosticId = diagnosticId;
            Message = message;
            Type = type;
            ReferenceId = memberId;
        }

        /// <summary>
        /// Create a compatibility difference object with default left and right metadata for which the difference occurred.
        /// </summary>
        public static CompatDifference CreateWithDefaultMetadata(string diagnosticId, string message, DifferenceType type, string? memberId) =>
            new(MetadataInformation.DefaultLeft,
                MetadataInformation.DefaultRight,
                diagnosticId,
                message,
                type,
                memberId);

        /// <summary>
        /// Create a compatibility difference object with default left and right metadata for which the difference occurred.
        /// </summary>
        public static CompatDifference CreateWithDefaultMetadata(string diagnosticId, string message, DifferenceType type, ISymbol member) =>
            new(MetadataInformation.DefaultLeft,
                MetadataInformation.DefaultRight,
                diagnosticId,
                message,
                type,
                member);

        /// <summary>
        /// Gets a <see cref="string"/> representation of the difference.
        /// </summary>
        /// <returns><see cref="string"/> describing the difference.</returns>
        public override string ToString() => $"{DiagnosticId} : {Message}";

        /// <inheritdoc />
        public bool Equals(CompatDifference other) =>
            Left.Equals(other.Left) &&
            Right.Equals(other.Right) &&
            DiagnosticId.Equals(other.DiagnosticId, StringComparison.InvariantCultureIgnoreCase) &&
            Type.Equals(other.Type) &&
            string.Equals(ReferenceId, other.ReferenceId, StringComparison.InvariantCultureIgnoreCase);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is CompatDifference difference && Equals(difference);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hashCode = 1447485498;
            hashCode = hashCode * -1521134295 + Left.GetHashCode();
            hashCode = hashCode * -1521134295 + Right.GetHashCode();
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
