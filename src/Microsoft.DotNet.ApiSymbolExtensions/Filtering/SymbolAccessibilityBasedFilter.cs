// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    public class SymbolAccessibilityBasedFilter : ISymbolFilter
    {
        private readonly bool _includeInternalSymbols;

        public SymbolAccessibilityBasedFilter(bool includeInternalSymbols)
        {
            _includeInternalSymbols = includeInternalSymbols;
        }

        public bool Include(ISymbol symbol) =>
            symbol.IsVisibleOutsideOfAssembly(_includeInternalSymbols);
    }
}
