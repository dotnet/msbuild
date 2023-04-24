// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the logic of filtering out symbols based on a provided accessibility level (i.e. public / internal).
    /// </summary>
    public class AccessibilitySymbolFilter : ISymbolFilter
    {
        private readonly bool _includeInternalSymbols;
        private readonly bool _includeEffectivelyPrivateSymbols;
        private readonly bool _includeExplicitInterfaceImplementationSymbols;

        public AccessibilitySymbolFilter(bool includeInternalSymbols,
            bool includeEffectivelyPrivateSymbols = false,
            bool includeExplicitInterfaceImplementationSymbols = false)
        {
            _includeInternalSymbols = includeInternalSymbols;
            _includeEffectivelyPrivateSymbols = includeEffectivelyPrivateSymbols;
            _includeExplicitInterfaceImplementationSymbols = includeExplicitInterfaceImplementationSymbols;
        }

        /// <inheritdoc />
        public bool Include(ISymbol symbol) =>
            symbol.IsVisibleOutsideOfAssembly(_includeInternalSymbols,
                _includeEffectivelyPrivateSymbols, _includeExplicitInterfaceImplementationSymbols);
    }
}
