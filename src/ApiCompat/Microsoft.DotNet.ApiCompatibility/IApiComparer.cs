// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

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
        /// Gets the differences when comparing a left assembly against a right based on the comparison settings.
        /// It compares two symbols.
        /// </summary>
        /// <param name="left">Left assembly symbol to compare against.</param>
        /// <param name="right">Right assembly symbol to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left, IAssemblySymbol right);

        /// <summary>
        /// Gets the differences when comparing a left assembly against a right based on the comparison settings.
        /// It compares two symbols.
        /// </summary>
        /// <param name="left">Left assembly symbol including metadata to compare against.</param>
        /// <param name="right">Right assembly symbol including metadata to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(ElementContainer<IAssemblySymbol> left, ElementContainer<IAssemblySymbol> right);

        /// <summary>
        /// Gets the differences when comparing a left assembly against multiple rights based on the comparison settings.
        /// It compares multiple symbols.
        /// </summary>
        /// <param name="left">The left that we are going to use to compare against the multiple rights.</param>
        /// <param name="right">Multiple elements to compare as the right hand side against the provided left.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(ElementContainer<IAssemblySymbol> left, IReadOnlyList<ElementContainer<IAssemblySymbol>> right);

        /// <summary>
        /// Gets the differences when comparing a left assembly set against a right set based on the comparison settings.
        /// It compares two symbol sets.
        /// </summary>
        /// <param name="left">Left assembly symbols to compare against.</param>
        /// <param name="right">Right assembly symbols to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left, IEnumerable<IAssemblySymbol> right);

        /// <summary>
        /// Gets the differences when comparing a left assembly set against a right set based on the comparison settings.
        /// It compares two symbol sets.
        /// </summary>
        /// <param name="left">Left assembly symbols including metadata to compare against.</param>
        /// <param name="right">Right assembly symbols including metadata to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(IEnumerable<ElementContainer<IAssemblySymbol>> left, IEnumerable<ElementContainer<IAssemblySymbol>> right);

        /// <summary>
        /// Get the differences when comparing a left assembly set against multiple right sets.
        /// It compares multiple symbol sets.
        /// </summary>
        /// <param name="left">Left assembly symbols including metadata to compare against.</param>
        /// <param name="right">Right assembly symbols including metadata to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(IEnumerable<ElementContainer<IAssemblySymbol>> left, IReadOnlyList<IEnumerable<ElementContainer<IAssemblySymbol>>> right);
    }
}
