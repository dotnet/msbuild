// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

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
            symbol.DeclaredAccessibility == Accessibility.Public ||
            symbol.DeclaredAccessibility == Accessibility.Protected ||
            symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal ||
            (_includeInternalSymbols && symbol.DeclaredAccessibility != Accessibility.Private);
    }
}
