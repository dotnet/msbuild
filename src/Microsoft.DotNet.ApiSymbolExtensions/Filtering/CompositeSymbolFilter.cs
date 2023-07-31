// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the composite pattern, group the list of <see cref="ISymbol"/> and interact with them
    /// the same way as a single instance of a <see cref="ISymbol"/> object.
    /// </summary>
    public sealed class CompositeSymbolFilter : ISymbolFilter
    {
        /// <summary>
        /// List on inner filters.
        /// </summary>
        public List<ISymbolFilter> Filters { get; } = new();

        /// <summary>
        /// Determines whether the <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol symbol) => Filters.All(f => f.Include(symbol));

        /// <summary>
        /// Add a filter object to a list of filters.
        /// </summary>
        /// <param name="filter">The <see cref="ISymbolFilter" /> to include to the list of filters.</param>
        /// <returns>Returns the current instance of the class.</returns>
        public CompositeSymbolFilter Add(ISymbolFilter filter)
        {
            Filters.Add(filter);
            return this;
        }
    }
}
