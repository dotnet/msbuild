// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Interface that represents a mapping between two <see cref="INamespaceSymbol"/> objects.
    /// This also holds a list of <see cref="ITypeMapper"/> to represent the mapping of types in between
    /// <see cref="IElementMapper{T}.Left"/> and <see cref="IElementMapper{T}.Right"/>.
    /// </summary>
    public interface INamespaceMapper : IElementMapper<INamespaceSymbol>
    {
        /// <summary>
        /// The containing assembly of this namespace.
        /// </summary>
        IAssemblyMapper ContainingAssembly { get; }

        /// <summary>
        /// Gets all the <see cref="TypeMapper"/> representing the types defined in the namespace including the type forwards.
        /// </summary>
        /// <returns>The mapper representing the types in the namespace</returns>
        IEnumerable<ITypeMapper> GetTypes();

        /// <summary>
        /// Adds forwarded types to the mapper to the index specified in the mapper.
        /// </summary>
        /// <param name="forwardedTypes">List containing the <see cref="INamedTypeSymbol"/> that represents the forwarded types.</param>
        /// <param name="side">Side to add the forwarded types into, 0 (Left) or 1 (Right).</param>
        /// <param name="setIndex">Value representing the index on the set of elements corresponding to the compared side.</param>
        void AddForwardedTypes(IEnumerable<INamedTypeSymbol>? forwardedTypes, ElementSide side, int setIndex);
    }
}
