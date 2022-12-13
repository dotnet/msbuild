// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Provides ordering for namespaces, types and members.
    /// </summary>
    public static class AssemblySymbolOrderer
    {
        /// <summary>
        /// Sorts the elements of a INamespaceSymbol.
        /// </summary>
        /// <param name="namespaces">List of namespaces to be sorted.</param>
        /// <returns>Returns namespaces in sorted order.</returns>
        public static IEnumerable<INamespaceSymbol> Order(this IEnumerable<INamespaceSymbol> namespaces)
        {
            return namespaces.OrderBy(s => s.GetDocumentationCommentId(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sorts the elements of a ITypeSymbol.
        /// </summary>
        /// <param name="namespaces">List of TypeMembers to be sorted.</param>
        /// <returns>Returns TypeMembers in sorted order.</returns>
        public static IEnumerable<T> Order<T>(this IEnumerable<T> symbols) where T : ITypeSymbol
        {
            return symbols.OrderBy(s => s.GetDocumentationCommentId(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sorts the elements of a ISymbol.
        /// </summary>
        /// <param name="namespaces">List of Members to be sorted.</param>
        /// <returns>Returns Members in sorted order.</returns>
        public static IEnumerable<ISymbol> Order(this IEnumerable<ISymbol> members)
        {
            if (members is IOrderedEnumerable<ITypeSymbol> orderedTypeDefinitionMembers)
            {
                return orderedTypeDefinitionMembers.ThenBy(s => s.GetDocumentationCommentId(), StringComparer.OrdinalIgnoreCase);
            }

            return members.OrderBy(s => (GetMemberOrder(s), s.GetDocumentationCommentId(), StringComparer.OrdinalIgnoreCase));
        }

        private static int GetMemberOrder(ISymbol symbol)
        {
            return symbol switch
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
}
