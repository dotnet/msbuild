// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiSymbolExtensions
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
