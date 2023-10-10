// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Interface that represents a mapping between multiple <see cref="IAssemblySymbol"/> objects.
    /// This also holds a list of <see cref="INamespaceMapper"/> to represent the mapping of namespaces in between
    /// <see cref="IElementMapper{T}.Left"/> and <see cref="IElementMapper{T}.Right"/>.
    /// </summary>
    public interface IAssemblyMapper : IElementMapper<ElementContainer<IAssemblySymbol>>
    {
        /// <summary>
        /// The containing assembly set of this assembly. Null if the assembly is not part of a set.
        /// </summary>
        IAssemblySetMapper? ContainingAssemblySet { get; }

        /// <summary>
        /// Gets the assembly load errors that happened when trying to follow type forwards.
        /// </summary>
        IEnumerable<CompatDifference> AssemblyLoadErrors { get; }

        /// <summary>
        /// Gets the mappers for the namespaces contained in <see cref="IElementMapper{T}.Left"/> and <see cref="IElementMapper{T}.Right"/>
        /// </summary>
        /// <returns>The list of <see cref="INamespaceMapper"/>.</returns>
        IEnumerable<INamespaceMapper> GetNamespaces();
    }
}
