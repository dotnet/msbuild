// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// Represents a Suppression for a validation error.
    /// </summary>
    public class Suppression : IEquatable<Suppression>
    {
        /// <summary>
        /// The DiagnosticId representing the error to be suppressed.
        /// </summary>
        public string DiagnosticId { get; set; }

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

        // Necessary for XmlSerializer to instantiate an object of this class.
        private Suppression()
        {
            DiagnosticId = string.Empty;
        }

        public Suppression(string diagnosticId,
            string? target = null,
            string? left = null,
            string? right = null,
            bool isBaselineSuppression = false)
        {
            DiagnosticId = diagnosticId;
            Target = target;
            Left = left;
            Right = right;
            IsBaselineSuppression = isBaselineSuppression;
        }

        /// <summary>
        /// Only serialize the IsBaselineSuppression property when this is a baseline suppression. If it is not,
        /// the property won't be serialized to keep the baseline file minimal. The method's name is important as
        /// XmlSerializer will look for methods called ShouldSerializeX to determine if properties should be serialized.
        /// </summary>
        /// <returns>Returns true when IsBaselineSuppression should be serialized.</returns>
        public bool ShouldSerializeIsBaselineSuppression() => IsBaselineSuppression;

        /// <summary>
        /// Only serialize the DiagnosticId property when it is not empty. This can happen when a global suppression
        /// without a diagnostic id was manually added to the suppression file. The method's name is important as
        /// XmlSerializer will look for methods called ShouldSerializeX to determine if properties should be serialized.
        /// </summary>
        /// <returns>Returns true when DiagnosticId should be serialized.</returns>
        public bool ShouldSerializeDiagnosticId() => DiagnosticId != string.Empty;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as Suppression);

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

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = 1447485498;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(DiagnosticId.ToLowerInvariant());
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Target?.ToLowerInvariant() ?? string.Empty);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Left?.ToLowerInvariant() ?? string.Empty);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Right?.ToLowerInvariant() ?? string.Empty);
            return hashCode;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            StringBuilder stringBuilder = new();
            if (IsBaselineSuppression)
            {
                stringBuilder.Append("[Baseline] ");
            }

            stringBuilder.Append(DiagnosticId);
            stringBuilder.Append(" (");

            bool requiresDelimiter = false;

            if (Target is not null)
            {
                stringBuilder.AppendFormat("Target: '{0}'", Target);
                requiresDelimiter = true;
            }

            if (Left is not null)
            {
                if (requiresDelimiter)
                {
                    stringBuilder.Append(", ");
                }

                stringBuilder.AppendFormat("Left: '{0}'", Left);
                requiresDelimiter = true;
            }

            if (Right is not null)
            {
                if (requiresDelimiter)
                {
                    stringBuilder.Append(", ");
                }

                stringBuilder.AppendFormat("Right: '{0}'", Right);
            }

            stringBuilder.Append(')');

            return stringBuilder.ToString();
        }
    }
}
