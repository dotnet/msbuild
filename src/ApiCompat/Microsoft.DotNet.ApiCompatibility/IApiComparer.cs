// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Performs api comparison based on ISymbol inputs.
    /// </summary>
    public interface IApiComparer
    {
        /// <summary>
        /// Flag indicating whether internal symbols should be included in the comparisons or not.
        /// </summary>
        bool IncludeInternalSymbols { get; set; }

        /// <summary>
        /// Flag indicating whether we should run on strict mode or not.
        /// If StrictMode is set, the behavior of some rules will change and some other rules will be
        /// executed when getting the differences. This is useful when you want both sides we are comparing
        /// to be strictly the same on their surface area.
        /// </summary>
        bool StrictMode { get; set; }

        /// <summary>
        /// Flag indicating whether the API comparison should warn when references are missing.
        /// </summary>
        bool WarnOnMissingReferences { get; set; }

        /// <summary>
        /// <see cref="ComparingSettings"/> to be used when creating the mapping to get the differences.
        /// </summary>
        ComparingSettings ComparingSettings { get; set; }

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
