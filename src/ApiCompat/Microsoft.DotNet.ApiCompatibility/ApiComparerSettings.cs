// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Comparing;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Settings for an ApiComparer instance.
    /// </summary>
    public class ApiComparerSettings : IMapperSettings, IRuleSettings
    {
        /// <inheritdoc />
        public ISymbolFilter SymbolFilter { get; set; }

        /// <inheritdoc />
        public IEqualityComparer<ISymbol> SymbolEqualityComparer { get; set; }

        /// <inheritdoc />
        public IEqualityComparer<AttributeData> AttributeDataEqualityComparer { get; set; }

        /// <inheritdoc />
        public bool IncludeInternalSymbols { get; set; }

        /// <inheritdoc />
        public bool StrictMode { get; set; }

        /// <inheritdoc />
        public bool WithReferences { get; set; }

        public ApiComparerSettings(ISymbolFilter? symbolFilter = null,
            IEqualityComparer<ISymbol>? symbolEqualityComparer = null,
            IEqualityComparer<AttributeData>? attributeDataEqualityComparer = null,
            bool includeInternalSymbols = false,
            bool strictMode = false,
            bool withReferences = false)
        {
            SymbolFilter = symbolFilter ?? new AccessibilitySymbolFilter(includeInternalSymbols);
            SymbolEqualityComparer = symbolEqualityComparer ?? new Comparing.SymbolEqualityComparer();
            AttributeDataEqualityComparer = attributeDataEqualityComparer ?? new AttributeDataEqualityComparer(SymbolEqualityComparer, new TypedConstantEqualityComparer(SymbolEqualityComparer));
            IncludeInternalSymbols = includeInternalSymbols;
            StrictMode = strictMode;
            WithReferences = withReferences;
        }
    }
}
