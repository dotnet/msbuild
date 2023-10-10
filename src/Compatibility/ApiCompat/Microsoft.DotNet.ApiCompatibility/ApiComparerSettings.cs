// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Comparing;
using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiCompatibility.Rules;
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
