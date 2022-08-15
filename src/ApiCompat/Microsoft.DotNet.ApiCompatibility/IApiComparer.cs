// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Performs api comparison based on ISymbol inputs.
    /// </summary>
    public interface IApiComparer
    {
        /// <summary>
        /// <see cref="ApiComparerSettings"/> to be used when creating the mapping and getting the differences.
        /// </summary>
        ApiComparerSettings Settings { get; }

        /// <summary>
        /// Get's the differences when comparing a left assembly against a right based on the comparison settings.
        /// It compares two symbols.
        /// </summary>
        /// <param name="left">Left assembly symbol to compare against.</param>
        /// <param name="right">Right assembly symbol to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left, IAssemblySymbol right);

        /// <summary>
        /// Get's the differences when comparing a left assembly against a right based on the comparison settings.
        /// It compares two symbols.
        /// </summary>
        /// <param name="left">Left assembly symbol including metadata to compare against.</param>
        /// <param name="right">Right assembly symbol including metadata to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(ElementContainer<IAssemblySymbol> left, ElementContainer<IAssemblySymbol> right);

        /// <summary>
        /// Get's the differences when comparing a left assembly set against a right set based on the comparison settings.
        /// It compares two symbol sets.
        /// </summary>
        /// <param name="left">Left assembly symbols to compare against.</param>
        /// <param name="right">Right assembly symbols to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left, IEnumerable<IAssemblySymbol> right);

        /// <summary>
        /// Get's the differences when comparing a left assembly set against a right set based on the comparison settings.
        /// It compares two symbol sets.
        /// </summary>
        /// <param name="left">Left assembly symbols including metadata to compare against.</param>
        /// <param name="right">Right assembly symbols including metadata to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(IEnumerable<ElementContainer<IAssemblySymbol>> left, IEnumerable<ElementContainer<IAssemblySymbol>> right);

        /// <summary>
        /// Get the differences for all the combinations of <paramref name="left"/> against each <paramref name="right"/>
        /// </summary>
        /// <param name="left">The left that we are going to use to compare against the multiple rights.</param>
        /// <param name="right">Multiple elements to compare as the right hand side against the provided left.</param>
        /// <returns>Return a list containing the (left, right) tuple and it's list of <see cref="CompatDifference"/>.
        /// The returning list contains one element per (left, right) combination, which is the same length as <paramref name="right"/>.
        /// </returns>
        IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> GetDifferences(ElementContainer<IAssemblySymbol> left, IReadOnlyList<ElementContainer<IAssemblySymbol>> right);
    }
}
