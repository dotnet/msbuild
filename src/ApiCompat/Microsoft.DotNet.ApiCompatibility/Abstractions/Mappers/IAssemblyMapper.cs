// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
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
        /// Gets the mappers for the namespaces contained in <see cref="ElementMapper{T}.Left"/> and <see cref="ElementMapper{T}.Right"/>
        /// </summary>
        /// <returns>The list of <see cref="NamespaceMapper"/>.</returns>
        IEnumerable<INamespaceMapper> GetNamespaces();
    }
}
