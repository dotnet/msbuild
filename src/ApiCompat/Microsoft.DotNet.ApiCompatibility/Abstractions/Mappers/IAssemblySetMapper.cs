// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
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
        /// <returns>The list of <see cref="AssemblyMapper"/> representing the underlying assemblies.</returns>
        /// </summary>
        IEnumerable<IAssemblyMapper> GetAssemblies();
    }
}
