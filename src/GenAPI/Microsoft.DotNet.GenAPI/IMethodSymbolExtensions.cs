// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class IMethodSymbolExtensions
    {
        /// <summary>
        /// Compare the equality of two method signatures for the purpose of emitting a "new"
        /// keyword on a method's return type. This is *not* meant to be complete implementation,
        /// but rather a heuristic to check that one method may hide another.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="otherMethod"></param>
        /// <returns>true if the signatures are determined to be equal</returns>
        public static bool SignatureEquals(this IMethodSymbol? method, IMethodSymbol? otherMethod)
        {
            if (method is null || otherMethod is null)
            {
                return false;
            }

            if (method.Equals(otherMethod, SymbolEqualityComparer.Default))
            {
                return true;
            }

            if (method.Name != otherMethod.Name)
            {
                return false;
            }

            if (method.Arity != otherMethod.Arity || method.Parameters.Length != otherMethod.Parameters.Length)
            {
                return false;
            }

            // compare parameter types
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (!method.Parameters[i].Type.Equals(otherMethod.Parameters[i].Type, SymbolEqualityComparer.Default))
                {
                    return false;
                }
            }

            // TODO: GenAPI does not currently preserve __arglist as a parameter.
            // Add test case for this branch when that is fixed.
            if (method.IsVararg != otherMethod.IsVararg)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Examines the a method to determine if it could be an implicit default constructor that can be removed from source and impliicitly provided by the compiler.
        /// </summary>
        /// <param name="method">Method to examine.</param>
        /// <param name="symbolFilter">Symbol filter to apply in order to determine if other constructors are included from the containing type or its base type.</param>
        /// <returns>True if the method could be an implicit default constructor.</returns>
        public static bool IsImplicitDefaultConstructor(this IMethodSymbol method, ISymbolFilter symbolFilter)
        {
            // ensure this is a parameterless constructor
            if (method.MethodKind != MethodKind.Constructor || !method.Parameters.IsEmpty)
            {
                return false;
            }

            // see https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/classes#15115-default-constructors
            // these are the only two accessibilities given to the implicit default constructor, regardless of containing type accessibility.
            if (method.ContainingType.IsAbstract ?
                method.DeclaredAccessibility != Accessibility.Protected :
                method.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            // if the type defines any other instance constructors the default constructor cannot be implicit 
            if (method.ContainingType.InstanceConstructors.Any(c => symbolFilter.Include(c) && !c.Parameters.IsEmpty))
            {
                return false;
            }

            // if the base type does not have a parameterless constructor the default constructor cannot be implicit
            INamedTypeSymbol? baseType = method.ContainingType.BaseType;
            if (baseType != null)
            {
                return baseType.InstanceConstructors.Any(c => symbolFilter.Include(c) && c.Parameters.IsEmpty);
            }

            return true;
        }
    }
}
