// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Transactions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxGeneratorExtensions
    {
        /// <summary>
        /// Creates a declaration matching an existing symbol.
        ///     The reason of having this similar to `SyntaxGenerator.Declaration` extension method is that
        ///     SyntaxGenerator does not generates attributes neither for types, neither for members.
        /// </summary>
        public static SyntaxNode DeclarationExt(this SyntaxGenerator syntaxGenerator, ISymbol symbol, ISymbolFilter symbolFilter)
        {
            if (symbol.Kind == SymbolKind.NamedType)
            {
                INamedTypeSymbol type = (INamedTypeSymbol)symbol;
                switch (type.TypeKind)
                {
                    case TypeKind.Class:
                    case TypeKind.Struct:
                    case TypeKind.Interface:
                        TypeDeclarationSyntax typeDeclaration = (TypeDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                        if (type.IsRecord && type.TryGetRecordConstructor(out IMethodSymbol? recordConstructor))
                        {
                            // if the type is a record and we can find it's parameters, use `record Name(parameters...)` syntax.
                            typeDeclaration = typeDeclaration.WithParameterList(
                                SyntaxFactory.ParameterList(
                                    SyntaxFactory.SeparatedList<ParameterSyntax>(
                                        recordConstructor.Parameters.Select(p => (ParameterSyntax)syntaxGenerator.ParameterDeclaration(p)))));
                        }
                        return typeDeclaration
                            .WithBaseList(syntaxGenerator.GetBaseTypeList(type, symbolFilter))
                            .WithMembers(new SyntaxList<MemberDeclarationSyntax>());

                    case TypeKind.Enum:
                        EnumDeclarationSyntax enumDeclaration = (EnumDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                        return enumDeclaration.WithMembers(new SeparatedSyntaxList<EnumMemberDeclarationSyntax>());
                }
            }

            if (symbol.Kind == SymbolKind.Method)
            {
                IMethodSymbol method = (IMethodSymbol)symbol;
                if (method.MethodKind == MethodKind.Constructor)
                {
                    INamedTypeSymbol? baseType = method.ContainingType.BaseType;
                    if (baseType != null)
                    {
                        IEnumerable<IMethodSymbol> baseConstructors = baseType.Constructors.Where(symbolFilter.Include);
                        // If the base type does not have default constructor.
                        if (baseConstructors.Any() && baseConstructors.All(c => !c.Parameters.IsEmpty))
                        {
                            IOrderedEnumerable<IMethodSymbol> baseTypeConstructors = baseConstructors
                                .Where(c => c.GetAttributes().All(a => !a.IsObsoleteWithUsageTreatedAsCompilationError()))
                                .OrderBy(c => c.Parameters.Length);

                            if (baseTypeConstructors.Any())
                            {
                                IMethodSymbol constructor = baseTypeConstructors.First();

                                ConstructorDeclarationSyntax declaration = (ConstructorDeclarationSyntax)syntaxGenerator.Declaration(method);
                                if (!declaration.Modifiers.Any(m => m.RawKind == (int)SyntaxKind.UnsafeKeyword) &&
                                    // if at least one parameter of a base constructor is raw pointer type
                                    constructor.Parameters.Any(p => p.Type.TypeKind == TypeKind.Pointer))
                                {
                                    declaration = declaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword));
                                }
                                return declaration.WithInitializer(constructor.GenerateBaseConstructorInitializer());
                            }
                        }
                    }
                }
            }

            if (symbol is IEventSymbol eventSymbol)
            {
                if (eventSymbol.IsAbstract)
                {
                    // TODO: remove a work around solution after the Roslyn issue https://github.com/dotnet/roslyn/issues/66966 is fixed
                    EventFieldDeclarationSyntax eventDeclaration = (EventFieldDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                    return eventDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.AbstractKeyword));
                }
                else
                {
                    // adds generation of add & remove accessors for the non abstract events.
                    return syntaxGenerator.CustomEventDeclaration(eventSymbol.Name,
                        syntaxGenerator.TypeExpression(eventSymbol.Type),
                        eventSymbol.DeclaredAccessibility,
                        DeclarationModifiers.From(eventSymbol));
                }
            }

            if (symbol is IPropertySymbol propertySymbol)
            {
                // Explicitly implemented indexers do not set IsIndexer
                // https://github.com/dotnet/roslyn/issues/53911
                if (!propertySymbol.IsIndexer && propertySymbol.ExplicitInterfaceImplementations.Any(i => i.IsIndexer))
                {
                    return syntaxGenerator.IndexerDeclaration(propertySymbol);
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

        // Gets the list of base class and interfaces for a given symbol INamedTypeSymbol.
        private static BaseListSyntax? GetBaseTypeList(this SyntaxGenerator syntaxGenerator,
            INamedTypeSymbol type,
            ISymbolFilter symbolFilter)
        {
            List<BaseTypeSyntax> baseTypes = new();

            if (type.TypeKind == TypeKind.Class && type.BaseType != null && symbolFilter.Include(type.BaseType))
            {
                TypeSyntax baseTypeSyntax = (TypeSyntax)syntaxGenerator.TypeExpression(type.BaseType);

                if (type.BaseType.IsRecord && type.BaseType.TryGetRecordConstructor(out IMethodSymbol? recordConstructor))
                {
                    baseTypes.Add(SyntaxFactory.PrimaryConstructorBaseType(baseTypeSyntax, recordConstructor.CreateDefaultArgumentList()));
                }
                else
                {
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(baseTypeSyntax));
                }
            }

            // includes only interfaces that were not filtered out by the given ISymbolFilter or none of TypeParameters were filtered out.
            baseTypes.AddRange(type.Interfaces
                .Where(i => symbolFilter.Include(i) && !i.HasInaccessibleTypeArgument(symbolFilter))
                .Select(i => SyntaxFactory.SimpleBaseType((TypeSyntax)syntaxGenerator.TypeExpression(i))));
            return baseTypes.Count > 0 ?
                SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(baseTypes)) :
                null;
        }
    }
}
