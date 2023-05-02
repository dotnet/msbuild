// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the logic of filtering out symbols based on a provided accessibility level (i.e. public / internal).
    /// </summary>
    public class AccessibilitySymbolFilter : ISymbolFilter
    {
        /// <summary>
        /// Include internal API.
        /// </summary>
        public bool IncludeInternalSymbols { get; }

        /// <summary>
        /// Include effectively private API.
        /// </summary>
        public bool IncludeEffectivelyPrivateSymbols { get; }

        /// <summary>
        /// Include explicit interface implementation API.
        /// </summary>
        public bool IncludeExplicitInterfaceImplementationSymbols { get; }

        public AccessibilitySymbolFilter(bool includeInternalSymbols,
            bool includeEffectivelyPrivateSymbols = false,
            bool includeExplicitInterfaceImplementationSymbols = false)
        {
            IncludeInternalSymbols = includeInternalSymbols;
            IncludeEffectivelyPrivateSymbols = includeEffectivelyPrivateSymbols;
            IncludeExplicitInterfaceImplementationSymbols = includeExplicitInterfaceImplementationSymbols;
        }

        /// <inheritdoc />
        public bool Include(ISymbol symbol) =>
            symbol.IsVisibleOutsideOfAssembly(IncludeInternalSymbols,
                IncludeEffectivelyPrivateSymbols,
                IncludeExplicitInterfaceImplementationSymbols);
    }
}
