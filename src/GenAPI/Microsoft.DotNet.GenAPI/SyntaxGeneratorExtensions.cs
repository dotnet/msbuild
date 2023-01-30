// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.ApiSymbolExtensions;

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

                if (declaration != null)
                {
                    return syntaxGenerator.WithTypeParametersAndConstraintsCopyExt(declaration, type.TypeParameters);
                }
            }
            else if (symbol.Kind == SymbolKind.Method)
            {
                var method = (IMethodSymbol)symbol;
                if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
                {
                    return syntaxGenerator.ExplicitInterfaceImplementationMethodDeclaration(method, method.Name);
                }
                else if (method.MethodKind == MethodKind.Destructor)
                {
                    return syntaxGenerator.DestructorDeclaration(method);
                }
            }
            else if (symbol.Kind == SymbolKind.Property)
            {
                var property = (IPropertySymbol)symbol;
                if (property.IsExplicitInterfaceImplementation())
                {
                    return syntaxGenerator.PropertyDeclaration(
                        property.Name,
                        syntaxGenerator.TypeExpression(property.Type),
                        Accessibility.NotApplicable,
                        DeclarationModifiers.From(property));
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

        // TODO: Temporary solution till corresponding Roslyn API is added: https://github.com/dotnet/arcade/issues/11895.
        private static SyntaxNode ExplicitInterfaceImplementationMethodDeclaration(this SyntaxGenerator syntaxGenerator, IMethodSymbol method, string name, IEnumerable<SyntaxNode>? statements = null)
        {
            var decl = syntaxGenerator.MethodDeclaration(
                name,
                parameters: method.Parameters.Select(p => syntaxGenerator.ParameterDeclaration(p)),
                returnType: method.ReturnType?.SpecialType == SpecialType.System_Void ? null : syntaxGenerator.TypeExpression(method.ReturnType!),
                modifiers: DeclarationModifiers.From(method),
                statements: statements);

            if (!method.TypeParameters.IsEmpty)
            {
                decl = syntaxGenerator.WithTypeParametersAndConstraintsCopyExt(decl, method.TypeParameters);
            }

            return decl;
        }

        // this is copy/paste from private method `SyntaxGenerator.WithTypeParametersAndConstraints`
        private static SyntaxNode WithTypeParametersAndConstraintsCopyExt(this SyntaxGenerator syntaxGenerator, SyntaxNode declaration, ImmutableArray<ITypeParameterSymbol> typeParameters)
        {
            if (typeParameters.IsEmpty)
            {
                return declaration;
            }

            declaration = syntaxGenerator.WithTypeParameters(declaration, typeParameters.Select(tp => tp.Name));

            foreach (var tp in typeParameters)
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

            return declaration;
        }

        // TODO: temporary solution. Remove after the issue https://github.com/dotnet/arcade/issues/11938 is fixed.
        private static SyntaxNode DestructorDeclaration(this SyntaxGenerator syntaxGenerator, IMethodSymbol method)
        {
            MethodDeclarationSyntax decl = (MethodDeclarationSyntax)syntaxGenerator.MethodDeclaration("~" + method.ContainingType.Name);
            return decl.ReplaceNode(decl.ReturnType, SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(string.Empty)));
        }
    }
}
