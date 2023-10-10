// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class INamedTypeSymbolExtension
    {
        /// <summary>
        /// Checks if any of the type's members is a property with an indexer.
        /// </summary>
        public static bool HasIndexer(this INamedTypeSymbol type) =>
            type.GetMembers().Any(member => member is IPropertySymbol propertySymbol && propertySymbol.IsIndexer);

        // Visit a type and all its members, checking for cycles. Return true if the visitor returns true.
        private static bool WalkTypeSymbol(ITypeSymbol ty, HashSet<ITypeSymbol> visited, Func<ITypeSymbol, bool> f)
        {
            visited.Add(ty);

            if (f(ty))
            {
                return true;
            }

            foreach (INamedTypeSymbol memberType in ty.GetTypeMembers())
            {
                if (!visited.Contains(memberType) && WalkTypeSymbol(memberType, visited, f))
                {
                    return true;
                }
            }

            return false;
        }

        // Walk type with predicate that checks if a type is a reference type or ref-like (e.g. ByReference<T>).
        private static bool IsOrContainsReferenceType(ITypeSymbol ty) =>
            WalkTypeSymbol(ty, new(SymbolEqualityComparer.Default), ty => ty.IsRefLikeType || ty.IsReferenceType);

        // Walk type with predicate that checks if a type is unmanaged or a reference that's not the root.
        private static bool IsOrContainsNonEmptyStruct(ITypeSymbol root) =>
            WalkTypeSymbol(root, new(SymbolEqualityComparer.Default), ty =>
                ty.IsUnmanagedType ||
                    ((ty.IsReferenceType || ty.IsRefLikeType) && !SymbolEqualityComparer.Default.Equals(root, ty)));

        // Convert IEnumerable<AttributeData> to a SyntaxList<AttributeListSyntax>.
        private static SyntaxList<AttributeListSyntax> FromAttributeData(IEnumerable<AttributeData> attrData)
        {
            IEnumerable<SyntaxNode?> syntaxNodes = attrData.Select(ad =>
                ad.ApplicationSyntaxReference?.GetSyntax(new System.Threading.CancellationToken(false)));

            IEnumerable<AttributeListSyntax?> asNodes = syntaxNodes.Select(sn =>
            {
                if (sn is AttributeSyntax atSyntax)
                {
                    SeparatedSyntaxList<AttributeSyntax> singletonList = SyntaxFactory.SingletonSeparatedList<AttributeSyntax>(atSyntax);
                    AttributeListSyntax alSyntax = SyntaxFactory.AttributeList(singletonList);
                    return alSyntax;
                }

                return null;
            });

            List<AttributeListSyntax> asList = asNodes.Where(a => a != null).OfType<AttributeListSyntax>().ToList();
            return SyntaxFactory.List(asList);
        }

        // Build dummy field from a type, field name, and attribute list.
        private static SyntaxNode CreateDummyField(string typ, string fieldName, SyntaxList<AttributeListSyntax> attrs, bool isReadonly)
        {
            List<SyntaxToken> modifiers = new() { SyntaxFactory.Token(SyntaxKind.PrivateKeyword) };
            if (isReadonly)
                modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
            SyntaxNode declaration = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.ParseTypeName(typ))
                .WithVariables(
                    SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                        SyntaxFactory.VariableDeclarator(
                            SyntaxFactory.Identifier(fieldName)))))
                .WithModifiers(SyntaxFactory.TokenList(modifiers))
                .WithAttributeLists(attrs);
            return declaration;
        }

        /// <summary>
        /// SynthesizeDummyFields yields private fields for the namedType, because they can be part of the API contract.
        /// - A struct containing a field that is a reference type cannot be used as a reference.
        /// - A struct containing nonempty fields needs to be fully initialized. (See "definite assignment" rules)
        ///   "non-empty" means either unmanaged types like ints and enums, or reference types that are not the root.
        /// - A struct containing generic fields cannot have struct layout cycles.
        /// </summary>
        public static IEnumerable<SyntaxNode> SynthesizeDummyFields(this INamedTypeSymbol namedType, ISymbolFilter symbolFilter)
        {
            // Collect all excluded fields
            IEnumerable<IFieldSymbol> excludedFields = namedType.GetMembers()
                .Where(member => !symbolFilter.Include(member) && member is IFieldSymbol)
                .Select(m => (IFieldSymbol)m);

            if (excludedFields.Any())
            {
                // Collect generic excluded fields
                IEnumerable<IFieldSymbol> genericTypedFields = excludedFields.Where(f =>
                {
                    if (f.Type is INamedTypeSymbol ty)
                    {
                        return !ty.IsBoundGenericType() && symbolFilter.Include(ty);
                    }
                    return f.Type is ITypeParameterSymbol;
                });

                // Sometimes the metadata can contain names that are not valid C# identifiers. For example,
                // to express a set of type parameters, they can be prefixed with angle brackets.
                // Normalize them by replacing these special characters with '_'.
                static string NormalizeIdentifier(string s) => s.Replace('<', '_').Replace('>', '_');

                // Add a dummy field for each generic excluded field
                foreach (IFieldSymbol genericField in genericTypedFields)
                {
                    yield return CreateDummyField(genericField.Type.ToDisplayString(),
                        NormalizeIdentifier(genericField.Name),
                        FromAttributeData(genericField.GetAttributes().ExcludeNonVisibleOutsideOfAssembly(symbolFilter)),
                        namedType.IsReadOnly);
                }

                // If any field's type is transitively a reference type.
                if (excludedFields.Any(f => IsOrContainsReferenceType(f.Type)))
                {
                    // add reference type dummy field
                    yield return CreateDummyField("object", "_dummy", new(), namedType.IsReadOnly);

                    // add int field
                    yield return CreateDummyField("int", "_dummyPrimitive", new(), namedType.IsReadOnly);
                }
                // Otherwise, if the field transitively contains a field whose type is non-empty.
                else if (excludedFields.Any(f => IsOrContainsNonEmptyStruct(f.Type)))
                {
                    // add int field
                    yield return CreateDummyField("int", "_dummyPrimitive", new(), namedType.IsReadOnly);
                }
            }
        }

        /// <summary>
        /// Check that the named type is fully bound in all its type arguments.
        /// </summary>
        /// <returns></returns>
        public static bool IsBoundGenericType(this INamedTypeSymbol namedType)
        {
            foreach (ITypeSymbol arg in namedType.TypeArguments)
            {
                if (arg is ITypeParameterSymbol)
                {
                    return false;
                }

                if (arg is INamedTypeSymbol nt && !nt.IsBoundGenericType())
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// If the named type is a generic and not already unbound construct an unbound generic type.
        /// Unbound types are required for typeof expressions.
        /// </summary>
        /// <param name="namedType">type to consider</param>
        /// <returns>Unbound generic type if necessary, otherwise the original named type.</returns>
        public static INamedTypeSymbol MakeUnboundIfGeneric(this INamedTypeSymbol namedType) =>
            namedType.IsGenericType && !namedType.IsUnboundGenericType ? namedType.ConstructUnboundGenericType() : namedType;

        /// <summary>
        /// Detects if a generic type contains inaccessible type arguments.
        /// </summary>
        /// <param name="namedType">A loaded named type symbol <see cref="INamedTypeSymbol"/>.</param>
        /// <param name="symbolFilter">Assembly symbol filter <see cref="ISymbolFilter"/>.</param>
        /// <returns>Boolean</returns>
        public static bool HasInaccessibleTypeArgument(this INamedTypeSymbol namedType, ISymbolFilter symbolFilter)
            => namedType.IsGenericType && namedType.TypeArguments.Any(a => a.DeclaredAccessibility != Accessibility.NotApplicable && !symbolFilter.Include(a));

        /// <summary>
        /// Synthesize an internal default constructor for the type where all constructors are filtered.
        /// </summary>
        /// <param name="namedType">A loaded named type symbol <see cref="INamedTypeSymbol"/>.</param>
        /// <param name="symbolFilter">Assembly symbol filter <see cref="ISymbolFilter"/>.</param>
        public static IEnumerable<SyntaxNode> TryGetInternalDefaultConstructor(this INamedTypeSymbol namedType, ISymbolFilter symbolFilter)
        {
            // only non-static classes can have a default constructor that differs in visibility
            if (namedType.IsStatic ||
                namedType.TypeKind != TypeKind.Class)
            {
                yield break;
            }

            // records with a record constructor don't require a default constructor
            if (namedType.IsRecord && namedType.TryGetRecordConstructor(out _))
            {
                yield break;
            }

            // Nothing to do if type already exposes constructor, or has an excluded implicit constructor (since it would match visibility)
            if (namedType.InstanceConstructors.Any(c => symbolFilter.Include(c) || c.IsImplicitlyDeclared))
            {
                yield break;
            }

            SyntaxKind visibility = SyntaxKind.InternalKeyword;

            static bool IncludeInternalSymbols(ISymbolFilter filter) =>
                filter is AccessibilitySymbolFilter accessibilityFilter && accessibilityFilter.IncludeInternalSymbols;

            // Use the `Private` visibility if internal symbols are not filtered out.
            if (IncludeInternalSymbols(symbolFilter) ||
                (symbolFilter is CompositeSymbolFilter compositeSymbolFilter &&
                    compositeSymbolFilter.Filters.Any(filter => IncludeInternalSymbols(filter))))
            {
                visibility = SyntaxKind.PrivateKeyword;
            }

            ConstructorDeclarationSyntax constructor = SyntaxFactory.ConstructorDeclaration(
                new SyntaxList<AttributeListSyntax>(),
                SyntaxFactory.TokenList(new[] { SyntaxFactory.Token(visibility) }),
                SyntaxFactory.Identifier(namedType.ToDisplayString()),
                SyntaxFactory.ParameterList(),
                default!,
                default(BlockSyntax)!);

            // find base constructor to call if it's not implicit.
            IEnumerable<IMethodSymbol> baseConstructors = namedType.BaseType?.Constructors.Where(symbolFilter.Include) ?? Enumerable.Empty<IMethodSymbol>();
            if (baseConstructors.Any() && baseConstructors.All(c => !c.Parameters.IsEmpty))
            {
                constructor = constructor.WithInitializer(baseConstructors.First().GenerateBaseConstructorInitializer());
            }

            yield return constructor;
        }

        /// <summary>
        /// Synthesize a base class initializer. 
        /// </summary>
        /// <param name="baseTypeConstructor">Represents a base class constructor <see cref="IMethodSymbol"/>.</param>
        /// <returns>Returns the syntax node <see cref="ConstructorInitializerSyntax"/>.</returns>
        public static ConstructorInitializerSyntax GenerateBaseConstructorInitializer(this IMethodSymbol baseTypeConstructor)
        {
            return SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, baseTypeConstructor.CreateDefaultArgumentList());
        }

        /// <summary>
        /// Create an argument list to call a base method with default parameter values that satisfy the signature.
        /// </summary>
        /// <param name="method">Method to satisfy the signature.</param>
        /// <returns>ArgumentListSyntax that satisfies the method.</returns>
        public static ArgumentListSyntax CreateDefaultArgumentList(this IMethodSymbol method)
        {
            var argumentList = SyntaxFactory.ArgumentList();

            foreach (IParameterSymbol parameter in method.Parameters)
            {
                ExpressionSyntax expression = SyntaxFactory.DefaultExpression(SyntaxFactory.ParseTypeName(parameter.Type.ToDisplayString()));

                // If the parameter is not value type and isn't annotated to accept null, suppress the nullable warning with !
                if (!parameter.Type.IsValueType && parameter.NullableAnnotation != NullableAnnotation.Annotated)
                    expression = SyntaxFactory.PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, expression);

                argumentList = argumentList.AddArguments(SyntaxFactory.Argument(expression));
            }

            return argumentList;
        }

        /// <summary>
        /// Locates constructor generated by the compiler for `record Foo(...)` syntax
        /// </summary>
        /// <param name="type">Record type to examine</param>
        /// <param name="recordConstructor">If the type is a record and the compiler generated constructor is found it will be returned, otherwise null.
        /// The compiler will not generate a constructor in the case where the user defined it themself without using an argument list
        /// in the record declaration, or if the record has no parameters</param>
        /// <returns>True If the type is a record and the compiler generated constructor is found</returns>
        public static bool TryGetRecordConstructor(this INamedTypeSymbol type, [NotNullWhen(true)] out IMethodSymbol? recordConstructor)
        {
            if (!type.IsRecord)
            {
                recordConstructor = null;
                return false;
            }

            // Locate the compiler generated Deconstruct method.
            var deconstructMethod = (IMethodSymbol?)type.GetMembers("Deconstruct")
                .FirstOrDefault(m => m is IMethodSymbol && m.IsCompilerGenerated());

            // Locate the compiler generated constructor by matching parameters to Deconstruct - since we cannot locate it with an attribute.
            recordConstructor = (IMethodSymbol?)type.GetMembers(".ctor")
                .FirstOrDefault(m => m is IMethodSymbol method &&
                    method.MethodKind == MethodKind.Constructor &&
                    (deconstructMethod == null ?
                        method.Parameters.IsEmpty :
                        method.Parameters.Select(p => p.Type).SequenceEqual(
                            deconstructMethod.Parameters.Select(p => p.Type), SymbolEqualityComparer.Default)));

            return recordConstructor != null;
        }
    }
}
