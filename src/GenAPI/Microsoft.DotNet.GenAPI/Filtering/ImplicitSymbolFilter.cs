// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI.Filtering
{
    /// <summary>
    /// Filter out implicitly generated members for properties, events, etc.
    /// </summary>
    public class ImplicitSymbolFilter : ISymbolFilter
    {
        /// <summary>
        /// Determines whether implicitly generated symbols <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol symbol)
        {
            if (symbol is IMethodSymbol method)
            {
                if (method.IsImplicitlyDeclared ||
                    method.Kind == SymbolKind.NamedType ||
                    method.MethodKind == MethodKind.PropertyGet ||
                    method.MethodKind == MethodKind.PropertySet ||
                    method.MethodKind == MethodKind.EventAdd ||
                    method.MethodKind == MethodKind.EventRemove ||
                    method.MethodKind == MethodKind.EventRaise ||
                    method.MethodKind == MethodKind.DelegateInvoke)
                    return false;
            }
            return true;
        }
    }
}
