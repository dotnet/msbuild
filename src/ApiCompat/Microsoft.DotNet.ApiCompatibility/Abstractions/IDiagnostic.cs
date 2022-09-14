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
        /// A diagnostic message for the difference.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// A unique ID in order to identify the API that the difference was raised for.
        /// </summary>
        string? ReferenceId { get; }
    }
}
