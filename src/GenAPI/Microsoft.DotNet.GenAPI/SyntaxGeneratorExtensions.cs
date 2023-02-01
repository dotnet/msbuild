// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxGeneratorExtensions
    {
        /// <summary>
        /// Creates a declaration matching an existing symbol.
        ///     The reason of having this similar to `SyntaxGenerator.Declaration` extension method is that
        ///     SyntaxGenerator does not generates attributes neither for types, neither for members.
        /// </summary>
        public static SyntaxNode DeclarationExt(this SyntaxGenerator syntaxGenerator, ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.NamedType)
            {
                var type = (INamedTypeSymbol)symbol;
                switch (type.TypeKind)
                {
                    case TypeKind.Class:
                    case TypeKind.Struct:
                    case TypeKind.Interface:
                        TypeDeclarationSyntax typeDeclaration = (TypeDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                        return typeDeclaration.WithMembers(new SyntaxList<MemberDeclarationSyntax>());
                    case TypeKind.Enum:
                        EnumDeclarationSyntax enumDeclaration = (EnumDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                        return enumDeclaration.WithMembers(new SeparatedSyntaxList<EnumMemberDeclarationSyntax>());
                }
            }

            try
            {
                return syntaxGenerator.Declaration(symbol);
            }
            catch (ArgumentException ex)
            {
                // re-throw the ArgumentException with the symbol that caused it.
                throw new ArgumentException(ex.Message, symbol.ToDisplayString());
            }
        }
    }
}
