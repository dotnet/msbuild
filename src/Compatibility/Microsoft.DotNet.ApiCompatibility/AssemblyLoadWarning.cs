// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class that represents a warning that occurred while trying to load a specific assembly.
    /// </summary>
    public class AssemblyLoadWarning : IDiagnostic, IEquatable<AssemblyLoadWarning>
    {
        private readonly StringComparer _ordinalComparer = StringComparer.Ordinal;

        /// <summary>
        /// Creates a new instance of an <see cref="AssemblyLoadWarning"/> class with a given <paramref name="diagnosticId"/>,
        /// <paramref name="referenceId"/> and <paramref name="message"/>.
        /// </summary>
        /// <param name="diagnosticId">String representing the diagnostic ID.</param>
        /// <param name="referenceId">String representing the ID for the object that the diagnostic was created for.</param>
        /// <param name="message">String describing the diagnostic.</param>
        public AssemblyLoadWarning(string diagnosticId, string referenceId, string message)
        {
            DiagnosticId = diagnosticId;
            ReferenceId = referenceId;
            Message = message;
        }

        /// <inheritdoc/>
        public string DiagnosticId { get; }

        /// <inheritdoc/>
        public string ReferenceId { get; }

        /// <inheritdoc/>
        public string Message { get; }

        /// <inheritdoc/>
        public bool Equals(AssemblyLoadWarning other) => _ordinalComparer.Equals(DiagnosticId, other.DiagnosticId) &&
                                                         _ordinalComparer.Equals(ReferenceId, other.ReferenceId) &&
                                                         _ordinalComparer.Equals(Message, other.Message);
    }
}
