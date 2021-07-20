// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Extensions;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    internal class SymbolAccessibilityBasedFilter : ISymbolFilter
    {
        private readonly bool _includeInternalSymbols;

        internal SymbolAccessibilityBasedFilter(bool includeInternalSymbols)
        {
            _includeInternalSymbols = includeInternalSymbols;
        }

        public bool Include(ISymbol symbol) =>
            symbol.IsVisibleOutsideOfAssembly(_includeInternalSymbols);
    }
}
