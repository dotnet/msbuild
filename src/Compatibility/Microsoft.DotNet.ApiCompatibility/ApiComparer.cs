// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class that performs api comparison based on ISymbol inputs.
    /// </summary>
    public class ApiComparer
    {
        private readonly ComparingSettings _settings;

        /// <summary>
        /// Instantiate an object with the default <see cref="ComparingSettings"/>.
        /// </summary>
        public ApiComparer() : this(new ComparingSettings()) { }

        /// <summary>
        /// Instantiate an object with the provided <see cref="ComparingSettings"/>
        /// </summary>
        /// <param name="settings">Settings to use for comparison.</param>
        public ApiComparer(ComparingSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Instantiate an object that includes comparing internal symbols.
        /// </summary>
        /// <param name="includeInternalSymbols">Indicates whether internal symbols should be included or not.</param>
        public ApiComparer(bool includeInternalSymbols)
        {
            _settings = new ComparingSettings(filter: new SymbolAccessibilityBasedFilter(includeInternalSymbols));
        }

        /// <summary>
        /// Comma separated list to ignore diagnostic IDs.
        /// </summary>
        public string NoWarn { get; set; } = string.Empty;

        /// <summary>
        /// Array of diagnosticId and memberId to ignore those differences.
        /// </summary>
        public (string diagnosticId, string memberId)[] IgnoredDifferences { get; set; }

        /// <summary>
        /// Get's the differences when comparing Left vs Right based on the settings when instanciating the object.
        /// It compares two lists of symbols.
        /// </summary>
        /// <param name="left">Left symbols to compare against.</param>
        /// <param name="right">Right symbols to compare against.</param>
        /// <returns>List of found differences.</returns>
        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left, IEnumerable<IAssemblySymbol> right)
        {
            AssemblySetMapper mapper = new(_settings);
            mapper.AddElement(left, 0);
            mapper.AddElement(right, 1);

            DiferenceVisitor visitor = new(noWarn: NoWarn, ignoredDifferences: IgnoredDifferences);
            visitor.Visit(mapper);
            return visitor.Differences;
        }

        /// <summary>
        /// Get's the differences when comparing Left vs Right based on the settings when instanciating the object.
        /// It compares two symbols.
        /// </summary>
        /// <param name="left">Left symbol to compare against.</param>
        /// <param name="right">Right symbol to compare against.</param>
        /// <returns>List of found differences.</returns>
        public IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left, IAssemblySymbol right)
        {
            AssemblyMapper mapper = new(_settings);
            mapper.AddElement(left, 0);
            mapper.AddElement(right, 1);

            DiferenceVisitor visitor = new(noWarn: NoWarn, ignoredDifferences: IgnoredDifferences);
            visitor.Visit(mapper);
            return visitor.Differences;
        }
    }
}
