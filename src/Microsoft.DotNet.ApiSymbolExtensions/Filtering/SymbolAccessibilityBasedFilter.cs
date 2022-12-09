// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    public class SymbolAccessibilityBasedFilter : ISymbolFilter
    {
        private readonly bool _includeInternalSymbols;
        private readonly bool _includeEffectivelyPrivateSymbols;

        public SymbolAccessibilityBasedFilter(bool includeInternalSymbols, bool includeEffectivelyPrivateSymbols = false)
        {
            _includeInternalSymbols = includeInternalSymbols;
            _includeEffectivelyPrivateSymbols = includeEffectivelyPrivateSymbols;
        }

        public bool Include(ISymbol symbol) =>
            symbol.IsVisibleOutsideOfAssembly(_includeInternalSymbols, _includeEffectivelyPrivateSymbols);
    }
}
