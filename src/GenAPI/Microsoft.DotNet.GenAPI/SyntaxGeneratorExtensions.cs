// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxGeneratorExtensions
    {
        /// <summary>
        /// Creates a declaration matching an existing symbol.
        ///     The reson of having this similar to `SyntaxGenerator.Declaration` extention method is that
        ///     SyntaxGenerator does not generates attributes neither for types, neiter for members.
        /// </summary>
        public static SyntaxNode DeclarationExt(this SyntaxGenerator syntaxGenerator, ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.NamedType)
            {
                SyntaxNode? declaration = null;
                var type = (INamedTypeSymbol)symbol;
                switch (type.TypeKind)
                {
                    case TypeKind.Class:
                        declaration = syntaxGenerator.ClassDeclaration(
                            type.Name,
                            accessibility: type.DeclaredAccessibility,
                            modifiers: DeclarationModifiers.From(type),
                            baseType: type.BaseType is null ? null : syntaxGenerator.TypeExpression(type.BaseType),
                            interfaceTypes: type.Interfaces.Select(i => syntaxGenerator.TypeExpression(i)));
                        break;
                    case TypeKind.Struct:
                        declaration = syntaxGenerator.StructDeclaration(
                            type.Name,
                            accessibility: type.DeclaredAccessibility,
                            modifiers: DeclarationModifiers.From(type),
                            interfaceTypes: type.Interfaces.Select(i => syntaxGenerator.TypeExpression(i)));
                        break;
                    case TypeKind.Interface:
                        declaration = syntaxGenerator.InterfaceDeclaration(
                            type.Name,
                            accessibility: type.DeclaredAccessibility,
                            interfaceTypes: type.Interfaces.Select(i => syntaxGenerator.TypeExpression(i)));
                        break;
                    case TypeKind.Enum:
                        declaration = syntaxGenerator.EnumDeclaration(
                            type.Name,
                            accessibility: type.DeclaredAccessibility);
                        break;
                }

                /// this is copy/paste from private method `SyntaxGenerator.WithTypeParametersAndConstraints`
                if (declaration != null)
                {
                    if (type.TypeParameters.Length > 0)
                    {
                        declaration = syntaxGenerator.WithTypeParameters(declaration, type.TypeParameters.Select(tp => tp.Name));

                        foreach (var tp in type.TypeParameters)
                        {
                            if (tp.HasConstructorConstraint || tp.HasReferenceTypeConstraint || tp.HasValueTypeConstraint || tp.ConstraintTypes.Length > 0)
                            {
                                declaration = syntaxGenerator.WithTypeConstraint(declaration, tp.Name,
                                    kinds: (tp.HasConstructorConstraint ? SpecialTypeConstraintKind.Constructor : SpecialTypeConstraintKind.None)
                                           | (tp.HasReferenceTypeConstraint ? SpecialTypeConstraintKind.ReferenceType : SpecialTypeConstraintKind.None)
                                           | (tp.HasValueTypeConstraint ? SpecialTypeConstraintKind.ValueType : SpecialTypeConstraintKind.None),
                                    types: tp.ConstraintTypes.Select(t => syntaxGenerator.TypeExpression(t)));
                            }
                        }
                    }

                    return declaration;
                }
            }

            return syntaxGenerator.Declaration(symbol);
        }
    }
}
