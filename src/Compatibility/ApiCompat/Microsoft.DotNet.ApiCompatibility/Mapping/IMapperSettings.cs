// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Class that contains all the settings used to filter metadata, compare symbols and run rules.
    /// </summary>
    public interface IMapperSettings
    {
        /// <summary>
        /// The symbol filter to use when creating the <see cref="IElementMapper{T}"/>.
        /// </summary>
        ISymbolFilter SymbolFilter { get; }

        /// <summary>
        /// The comparer to map metadata.
        /// </summary>
        IEqualityComparer<ISymbol> SymbolEqualityComparer { get; }

        /// <summary>
        /// If true, references are available. Necessary to know for following type forwards.
        /// </summary>
        bool WithReferences { get; }
    }
}
