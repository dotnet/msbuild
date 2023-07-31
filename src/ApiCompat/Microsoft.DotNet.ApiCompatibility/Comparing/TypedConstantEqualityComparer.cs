// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Comparing
{
    /// <summary>
    /// Defines methods to support the comparison of <see cref="TypedConstant"/> for equality.
    /// </summary>
    public sealed class TypedConstantEqualityComparer : IEqualityComparer<TypedConstant>
    {
        private readonly IEqualityComparer<ISymbol> _symbolEqualityComparer;

        public TypedConstantEqualityComparer(IEqualityComparer<ISymbol> symbolEqualityComparer) =>
            _symbolEqualityComparer = symbolEqualityComparer;

        /// <inheritdoc />
        public int GetHashCode([DisallowNull] TypedConstant obj) => throw new NotImplementedException();

        /// <inheritdoc />
        public bool Equals(TypedConstant x, TypedConstant y)
        {
            if (x.Kind != y.Kind)
                return false;

            switch (x.Kind)
            {
                case TypedConstantKind.Array:
                    if (!x.Values.SequenceEqual(y.Values, this))
                        return false;
                    break;
                case TypedConstantKind.Type:
                    if (!_symbolEqualityComparer.Equals((x.Value as INamedTypeSymbol)!, (y.Value as INamedTypeSymbol)!))
                        return false;
                    break;
                default:
                    if (!Equals(x.Value, y.Value))
                        return false;
                    break;
            }

            return _symbolEqualityComparer.Equals(x.Type!, y.Type!);
        }
    }
}
