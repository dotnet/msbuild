// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Interface representing item definition objects for use by the Evaulator.
    /// </summary>
    /// <typeparam name="M">Type of metadata objects.</typeparam>
    internal interface IItemDefinition<M> : IMetadataTable
        where M : class, IMetadatum
    {
        /// <summary>
        /// Gets any metadatum on this item definition with the specified name.
        /// </summary>
        M GetMetadata(string name);

        /// <summary>
        /// Adds the specified metadata to the item definition.
        /// </summary>
        M SetMetadata(ProjectMetadataElement metadataElement, string evaluatedValue, M predecessor);
    }
}
