// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// General rule settings that are passed to the rules.
    /// </summary>
    public readonly struct RuleSettings
    {
        /// <summary>
        /// Flag indicating whether api comparison should be performed in strict mode.
        /// If true, the behavior of some rules will change and some other rules will be
        /// executed when getting the differences. This is useful when both sides's surface area
        /// which are compared, should not differ.
        /// </summary>
        public readonly bool StrictMode;

        /// <summary>
        /// Determines if internal members should be validated.
        /// </summary>
        public readonly bool IncludeInternalSymbols;

        /// <summary>
        /// If true, references are available. Necessary to know for following type forwards.
        /// </summary>
        public readonly bool WithReferences;

        /// <summary>
        /// The symbol comparer to check for equality of a given left and right.
        /// </summary>
        public readonly IEqualityComparer<ISymbol> SymbolComparer;

        public RuleSettings(bool strictMode = false,
            bool includeInternalSymbols = false,
            bool withReferences = false,
            IEqualityComparer<ISymbol>? symbolComparer = null)
        {
            StrictMode = strictMode;
            IncludeInternalSymbols = includeInternalSymbols;
            WithReferences = withReferences;
            SymbolComparer = symbolComparer ?? new DefaultSymbolsEqualityComparer();
        }
    }
}
