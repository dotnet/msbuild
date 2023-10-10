// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// General rule settings that are passed to the rules.
    /// </summary>
    public interface IRuleSettings
    {
        /// <summary>
        /// The symbol filter.
        /// </summary>
        ISymbolFilter SymbolFilter { get; }

        /// <summary>
        /// The symbol equality comparer.
        /// </summary>
        IEqualityComparer<ISymbol> SymbolEqualityComparer { get; }

        /// <summary>
        /// The attribute data equality comparer.
        /// </summary>
        IEqualityComparer<AttributeData> AttributeDataEqualityComparer { get; }

        /// <summary>
        /// Determines if internal members should be validated.
        /// </summary>
        bool IncludeInternalSymbols { get; }

        /// <summary>
        /// Flag indicating whether api comparison should be performed in strict mode.
        /// If true, the behavior of some rules will change and some other rules will be
        /// executed when getting the differences. This is useful when both sides' surface area
        /// which are compared, should not differ.
        /// </summary>
        bool StrictMode { get; }

        /// <summary>
        /// If true, references are available. Necessary to know for following type forwards.
        /// </summary>
        bool WithReferences { get; }
    }
}
