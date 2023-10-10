// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Provides extension methods for ordering namespace, type and member symbols.
    /// </summary>
    public static class AssemblySymbolOrderer
    {
        /// <summary>
        /// Sorts <see cref="INamespaceSymbol" /> elements.
        /// </summary>
        /// <param name="namespaceSymbols">List of namespaces to be sorted.</param>
        /// <returns>Returns namespaces in sorted order.</returns>
        public static IEnumerable<INamespaceSymbol> Order(this IEnumerable<INamespaceSymbol> namespaceSymbols) =>
            namespaceSymbols.OrderBy(s => s.GetDocumentationCommentId(), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Sorts <see cref="ITypeSymbol" /> elements.
        /// </summary>
        /// <param name="typeSymbols">List of TypeMembers to be sorted.</param>
        /// <returns>Returns TypeMembers in sorted order.</returns>
        public static IEnumerable<T> Order<T>(this IEnumerable<T> typeSymbols) where T : ITypeSymbol =>
            typeSymbols.OrderBy(s => s.GetDocumentationCommentId(), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Sorts <see cref="ISymbol" /> elements.
        /// </summary>
        /// <param name="symbols">List of symbols to be sorted.</param>
        /// <returns>Returns symbols in sorted order.</returns>
        public static IEnumerable<ISymbol> Order(this IEnumerable<ISymbol> symbols) =>
            symbols is IOrderedEnumerable<ITypeSymbol> orderedTypeDefinitionMembers ?
            orderedTypeDefinitionMembers.ThenBy(s => s.GetDocumentationCommentId(), StringComparer.OrdinalIgnoreCase) :
            symbols.OrderBy(s => (GetMemberOrder(s), s.GetDocumentationCommentId(), StringComparer.OrdinalIgnoreCase));

        private static int GetMemberOrder(ISymbol symbol) =>
            symbol switch
            {
                IFieldSymbol fieldSymbol when fieldSymbol.ContainingType.TypeKind == TypeKind.Enum
                    => (int)Convert.ToInt64(fieldSymbol.ConstantValue),
                IFieldSymbol _ => 0,
                IMethodSymbol methodSymbol when methodSymbol.MethodKind == MethodKind.Constructor => 1,
                IPropertySymbol _ => 2,
                IEventSymbol => 3,
                IMethodSymbol => 4,
                _ => 5
            };
    }
}
