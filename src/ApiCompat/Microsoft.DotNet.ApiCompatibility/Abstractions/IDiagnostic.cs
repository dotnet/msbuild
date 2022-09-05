// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Interface that describes a diagnostic.
    /// </summary>
    public interface IDiagnostic
    {
        /// <summary>
        /// String representing the diagnostic ID.
        /// </summary>
        string DiagnosticId { get; }

        /// <summary>
        /// String representing the ID for the object that the diagnostic was created for.
        /// </summary>
        string? ReferenceId { get; }

        /// <summary>
        /// String describing the diagnostic.
        /// </summary>
        string Message { get; }
    }
}
