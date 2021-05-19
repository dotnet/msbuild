// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class that performs api comparison based on ISymbol inputs.
    /// </summary>
    public class ApiComparer
    {
        /// <summary>
        /// Flag indicating whether internal symbols should be included in the comparisons or not.
        /// </summary>
        public bool IncludeInternalSymbols { get; set; }

        /// <summary>
        /// Comma separated list to ignore diagnostic IDs.
        /// </summary>
        public string NoWarn { get; set; } = string.Empty;

        /// <summary>
        /// Array of diagnosticId and memberId to ignore those differences.
        /// </summary>
        public (string diagnosticId, string memberId)[] IgnoredDifferences { get; set; }

        /// <summary>
        /// Flag indicating whether we should run on strict mode or not.
        /// If StrictMode is set, the behavior of some rules will change and some other rules will be
        /// executed when getting the differences. This is useful when you want both sides we are comparing
        /// to be strictly the same on their surface area.
        /// </summary>
        public bool StrictMode { get; set; }

        /// <summary>
        /// Callback function to get the <see cref="ComparingSettings"/> to be used when creating the settings to get the differences.
        /// This callback is called at the beginning of every <see cref="GetDifferences"/> overload.
        /// </summary>
        public Func<ComparingSettings> GetComparingSettings { get; set; }

        /// <summary>
        /// Get's the differences when comparing Left vs Right based on the settings at the moment this method is called.
        /// It compares two lists of symbols.
        /// </summary>
        /// <param name="left">Left symbols to compare against.</param>
        /// <param name="right">Right symbols to compare against.</param>
        /// <returns>List of found differences.</returns>
        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left, IEnumerable<IAssemblySymbol> right)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            AssemblySetMapper mapper = new(GetComparingSettingsCore());
            mapper.AddElement(left, ElementSide.Left);
            mapper.AddElement(right, ElementSide.Right);

            DifferenceVisitor visitor = new(noWarn: NoWarn, ignoredDifferences: IgnoredDifferences);
            visitor.Visit(mapper);
            return visitor.DiagnosticBags.First().Differences;
        }

        /// <summary>
        /// Get's the differences when comparing Left vs Right based on the settings at the moment this method is called.
        /// It compares two symbols.
        /// </summary>
        /// <param name="left">Left symbol to compare against.</param>
        /// <param name="right">Right symbol to compare against.</param>
        /// <returns>List of found differences.</returns>
        public IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left, IAssemblySymbol right)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            AssemblyMapper mapper = new(GetComparingSettingsCore());
            mapper.AddElement(left, ElementSide.Left);
            mapper.AddElement(right, ElementSide.Right);

            DifferenceVisitor visitor = new(noWarn: NoWarn, ignoredDifferences: IgnoredDifferences);
            visitor.Visit(mapper);
            return visitor.DiagnosticBags.First().Differences;
        }

        /// <summary>
        /// Get the differences for all the combinations of <paramref name="left"/> against each <paramref name="right"/>
        /// </summary>
        /// <param name="left">The left that we are going to use to compare against the multiple rights.</param>
        /// <param name="right">Multiple elements to compare as the right hand side against the provided left.</param>
        /// <returns>Return a list containing the (left, right) tuple and it's list of <see cref="CompatDifference"/>.
        /// The returning list contains one element per (left, right) combination, which is the same length as <paramref name="right"/>.
        /// </returns>
        public IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> GetDifferences(ElementContainer<IAssemblySymbol> left, IList<ElementContainer<IAssemblySymbol>> right)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            int rightCount = right.Count;
            AssemblyMapper mapper = new(GetComparingSettingsCore(), rightSetSize: rightCount);
            mapper.AddElement(left.Element, ElementSide.Left);

            
            for (int i = 0; i < rightCount; i++)
            {
                if (right[i] == null)
                {
                    throw new ArgumentNullException(nameof(right), string.Format(Resources.ElementShouldNotBeNullAtIndex, i));
                }

                mapper.AddElement(right[i].Element, ElementSide.Right, i);
            }

            DifferenceVisitor visitor = new(rightCount: rightCount, noWarn: NoWarn, ignoredDifferences: IgnoredDifferences);
            visitor.Visit(mapper);

            (MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)[] result = new (MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)[rightCount];

            int count = 0;
            foreach (DiagnosticBag<CompatDifference> bag in visitor.DiagnosticBags)
            {
                result[count] = (left.MetadataInformation, right[count].MetadataInformation, bag.Differences);
                count++;
            }
            
            return result;
        }

        private ComparingSettings GetComparingSettingsCore()
        {
            if (GetComparingSettings != null)
                return GetComparingSettings();

            return new ComparingSettings(filter: new SymbolAccessibilityBasedFilter(IncludeInternalSymbols), strictMode: StrictMode);
        }
    }
}
