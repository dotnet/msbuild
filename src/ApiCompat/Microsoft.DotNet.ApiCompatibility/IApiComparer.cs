// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;
using System.Collections.Generic;

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
        /// Flag indicating whether the ApiComparison will warn if there are missing references
        /// </summary>
        bool WarnOnMissingReferences { get; set; }

        /// <summary>
        /// Callback function to get the <see cref="ComparingSettings"/> to be used when creating the settings to get the differences.
        /// The callback takes a string leftName and string[] rightNames parameters to indicate API Compat via the settings what the 
        /// name for the left and right the user specified.
        /// This callback is called at the beginning of every <see cref="GetDifferences"/> overload.
        /// </summary>
        Func<string?, string[]?, ComparingSettings>? GetComparingSettings { get; set; }

        /// <summary>
        /// Get's the differences when comparing Left vs Right based on the settings at the moment this method is called.
        /// It compares two lists of symbols.
        /// </summary>
        /// <param name="left">Left symbols to compare against.</param>
        /// <param name="right">Right symbols to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left, IEnumerable<IAssemblySymbol> right, string? leftName = null, string? rightName = null);

        /// <summary>
        /// Get's the differences when comparing Left vs Right based on the settings at the moment this method is called.
        /// It compares two symbols.
        /// </summary>
        /// <param name="left">Left symbol to compare against.</param>
        /// <param name="right">Right symbol to compare against.</param>
        /// <returns>List of found differences.</returns>
        IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left, IAssemblySymbol right, string? leftName = null, string? rightName = null);

        /// <summary>
        /// Get the differences for all the combinations of <paramref name="left"/> against each <paramref name="right"/>
        /// </summary>
        /// <param name="left">The left that we are going to use to compare against the multiple rights.</param>
        /// <param name="right">Multiple elements to compare as the right hand side against the provided left.</param>
        /// <returns>Return a list containing the (left, right) tuple and it's list of <see cref="CompatDifference"/>.
        /// The returning list contains one element per (left, right) combination, which is the same length as <paramref name="right"/>.
        /// </returns>
        IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> GetDifferences(ElementContainer<IAssemblySymbol> left, IList<ElementContainer<IAssemblySymbol>> right);
    }
}
