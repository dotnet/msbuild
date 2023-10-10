// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Interface that represents a mapping between two lists of <see cref="IAssemblySymbol"/>.
    /// </summary>
    public interface IAssemblySetMapper : IElementMapper<IEnumerable<ElementContainer<IAssemblySymbol>>>
    {
        /// <summary>
        /// The number of assemblies mapped.
        /// </summary>
        int AssemblyCount { get; }

        /// <summary>
        /// Gets the assembly mappers built from the provided lists of <see cref="IAssemblySymbol"/>.
        /// <returns>The list of <see cref="IAssemblyMapper"/> representing the underlying assemblies.</returns>
        /// </summary>
        IEnumerable<IAssemblyMapper> GetAssemblies();
    }
}
