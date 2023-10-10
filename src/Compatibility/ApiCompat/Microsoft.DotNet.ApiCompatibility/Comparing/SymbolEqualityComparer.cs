// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility.Comparing
{
    /// <summary>
    /// Defines methods to support the comparison of <see cref="ISymbol"/> for equality.
    /// </summary>
    public class SymbolEqualityComparer : IEqualityComparer<ISymbol>
    {
        /// <inheritdoc />
        public bool Equals(ISymbol? x, ISymbol? y) =>
            string.Equals(x != null ? GetKey(x) : null, y != null ? GetKey(y) : null, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public int GetHashCode([DisallowNull] ISymbol obj) => GetKey(obj).GetHashCode();

        private static string GetKey(ISymbol symbol)
        {
            if (symbol is IMethodSymbol method)
            {
                // The display string for event add and remove varies
                // depending if we have references or not.
                // As these can't have different overrides we don't care
                // about the full display string.
                if (method.IsEventAdderOrRemover())
                    return method.Name;
            }

            if (symbol is IAssemblySymbol assembly)
            {
                // When mapping assemblies, we must ignore the version, culture and token.
                return assembly.Identity.Name;
            }

            return symbol.ToComparisonDisplayString();
        }
    }
}
