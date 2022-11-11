// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxGeneratorExtensions
    {
        /// <summary>
        /// Creates a declaration matching an existing symbol.
        /// </summary>
        public static SyntaxNode DeclarationExt(this SyntaxGenerator syntaxGenerator, ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.NamedType)
            {
                var type = (INamedTypeSymbol)symbol;
                switch (type.TypeKind)
                {
                    case TypeKind.Class:
                        return syntaxGenerator.ClassDeclaration(
                            type.Name,
                            accessibility: type.DeclaredAccessibility,
                            modifiers: DeclarationModifiers.From(type),
                            baseType: syntaxGenerator.TypeExpression(type.BaseType),
                            interfaceTypes: type.Interfaces.Select(i => syntaxGenerator.TypeExpression(i)));
                    case TypeKind.Struct:
                        return syntaxGenerator.StructDeclaration(
                            type.Name,
                            accessibility: type.DeclaredAccessibility,
                            modifiers: DeclarationModifiers.From(type),
                            interfaceTypes: type.Interfaces.Select(i => syntaxGenerator.TypeExpression(i)));
                    case TypeKind.Interface:
                        return syntaxGenerator.InterfaceDeclaration(
                            type.Name,
                            accessibility: type.DeclaredAccessibility,
                            interfaceTypes: type.Interfaces.Select(i => syntaxGenerator.TypeExpression(i)));
                    case TypeKind.Enum:
                        return syntaxGenerator.EnumDeclaration(
                            type.Name,
                            accessibility: type.DeclaredAccessibility);
                }
            }

            return syntaxGenerator.Declaration(symbol);
        }
    }
}
