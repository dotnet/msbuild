// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class that contains all the settings used to filter metadata, compare symbols and run rules.
    /// </summary>
    public readonly struct MapperSettings
    {
        /// <summary>
        /// The metadata filter to use when creating the <see cref="ElementMapper{T}"/>.
        /// </summary>
        public readonly ISymbolFilter Filter;

        /// <summary>
        /// The comparer to map metadata.
        /// </summary>
        public readonly IEqualityComparer<ISymbol> EqualityComparer;

        /// <summary>
        /// Indicates if we should warn on missing references.
        /// </summary>
        public readonly bool WarnOnMissingReferences;

        /// <summary>
        /// Instantiates default MapperSettings.
        /// </summary>
        public MapperSettings()
        {
            Filter = new SymbolAccessibilityBasedFilter(false);
            EqualityComparer = new DefaultSymbolsEqualityComparer();
            WarnOnMissingReferences = false;
        }

        /// <summary>
        /// Instantiates MapperSettings with the desired comparison settings.
        /// </summary>
        /// <param name="ruleRunner">The rule runner.</param>
        /// <param name="filter">The symbol filter.</param>
        /// <param name="equalityComparer">The comparer to map metadata.</param>
        public MapperSettings(ISymbolFilter? filter = null,
            IEqualityComparer<ISymbol>? equalityComparer = null,
            bool warnOnMissingReferences = false,
            bool includeInternalSymbols = false)
        {
            Filter = filter ?? new SymbolAccessibilityBasedFilter(includeInternalSymbols);
            EqualityComparer = equalityComparer ?? new DefaultSymbolsEqualityComparer();
            WarnOnMissingReferences = warnOnMissingReferences;
        }
    }
}
