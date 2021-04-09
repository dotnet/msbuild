using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiCompatibility.Extensions
{
    internal static class TypeSymbolExtensions
    {
        internal static IEnumerable<ITypeSymbol> GetAllBaseTypes(this ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Interface)
            {
                foreach (ITypeSymbol @interface in type.Interfaces)
                {
                    yield return @interface;
                    foreach (ITypeSymbol baseInterface in @interface.GetAllBaseTypes())
                        yield return baseInterface;
                }
            }
            else if (type.BaseType != null)
            {
                yield return type.BaseType;
                foreach (ITypeSymbol baseType in type.BaseType.GetAllBaseTypes())
                    yield return baseType;
            }
        }
    }
}
