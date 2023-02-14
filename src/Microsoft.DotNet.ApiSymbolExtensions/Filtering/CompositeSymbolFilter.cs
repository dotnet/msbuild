// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the composite pattern, group the list of <see cref="ISymbol"/> and interact with them
    /// the same way as a single instance of a <see cref="ISymbol"/> object.
    /// </summary>
    public class CompositeSymbolFilter : ISymbolFilter
    {
        private readonly List<ISymbolFilter> _innerFilters = new();

        /// <summary>
        /// Determines whether the <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol symbol) => _innerFilters.All(f => f.Include(symbol));

        /// <summary>
        /// Construct and add a new filter object to a list of filters.
        /// </summary>
        /// <typeparam name="T"><see cref="ISymbolFilter" /> to evaluate.</typeparam>
        /// <returns>Returns the current instance of the class.</returns>
        public CompositeSymbolFilter Add<T>() where T : ISymbolFilter, new()
        {
            _innerFilters.Add(new T());
            return this;
        }

        /// <summary>
        /// Add a filter object to a list of filters.
        /// </summary>
        /// <typeparam name="T"><see cref="ISymbolFilter" /> to evaluate.</typeparam>
        /// <returns>Returns the current instance of the class.</returns>
        public CompositeSymbolFilter Add(ISymbolFilter filter)
        {
            _innerFilters.Add(filter);
            return this;
        }
    }
}
