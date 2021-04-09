using Microsoft.CodeAnalysis;
using System;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class CompatDifference : IDiagnostic, IEquatable<CompatDifference>
    {
        public string DiagnosticId { get; }
        public DifferenceType Type { get; }
        public virtual string Message { get; }
        public string ReferenceId { get; }

        private CompatDifference() { }

        public CompatDifference(string id, string message, DifferenceType type, ISymbol member)
            : this(id, message, type, member.GetDocumentationCommentId())
        {
        }

        public CompatDifference(string id, string message, DifferenceType type, string memberId)
        {
            DiagnosticId = id;
            Message = message;
            Type = type;
            ReferenceId = memberId;
        }

        public bool Equals(CompatDifference other) => 
            Type == other.Type &&
            DiagnosticId.Equals(other.DiagnosticId, StringComparison.OrdinalIgnoreCase) &&
            ReferenceId.Equals(other.ReferenceId, StringComparison.OrdinalIgnoreCase) &&
            Message.Equals(other.Message, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() =>
            HashCode.Combine(ReferenceId, DiagnosticId, Message, Type);

        public override string ToString() => $"{DiagnosticId} : {Message}";
    }
}
