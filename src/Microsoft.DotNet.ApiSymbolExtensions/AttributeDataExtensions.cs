// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Extension methods for interacting with <see cref="AttributeData"/>.
    /// </summary>
    public static class AttributeDataExtensions
    {
        /// <summary>
        /// Determines if an <see cref="AttributeData"/> object is visible outside of the containing assembly.
        /// By default also verifies the visibility of the attribute's arguments.
        /// </summary>
        public static bool IsVisibleOutsideOfAssembly(this AttributeData attributeData,
            ISymbolFilter symbolFilter,
            bool excludeWithTypeArgumentsNotVisibleOutsideOfAssembly = true) =>
            attributeData.AttributeClass != null &&
            symbolFilter.Include(attributeData.AttributeClass) &&
            (!excludeWithTypeArgumentsNotVisibleOutsideOfAssembly ||
             !HasTypeArgumentsNotVisibleOutsideOfAssembly(attributeData, symbolFilter));

        /// <summary>
        /// Excludes <see cref="AttributeData"/> that is not visible outside of an assembly.
        /// </summary>
        public static ImmutableArray<AttributeData> ExcludeNonVisibleOutsideOfAssembly(this ImmutableArray<AttributeData> attributes,
            ISymbolFilter symbolFilter,
            bool excludeWithTypeArgumentsNotVisibleOutsideOfAssembly = true) =>
            attributes.Where(attribute => attribute.IsVisibleOutsideOfAssembly(symbolFilter, excludeWithTypeArgumentsNotVisibleOutsideOfAssembly)).ToImmutableArray();

        /// <summary>
        /// Checks if an <see cref="AttributeData"/> has <see cref="INamedTypeSymbol"/> arguments that point to a <see cref="Type"/> that isn't visible outside of the containing assembly.
        /// </summary>
        public static bool HasTypeArgumentsNotVisibleOutsideOfAssembly(this AttributeData attributeData, ISymbolFilter symbolFilter) =>
            attributeData.NamedArguments.Select(namedArgument => namedArgument.Value)
                .Concat(attributeData.ConstructorArguments)
                .Any(typedConstant => typedConstant.Kind == TypedConstantKind.Type
                    && typedConstant.Value is INamedTypeSymbol namedTypeSymbol
                    && !symbolFilter.Include(namedTypeSymbol));
    }
}
