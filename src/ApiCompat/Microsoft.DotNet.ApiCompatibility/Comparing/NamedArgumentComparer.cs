// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Comparing
{
    /// <summary>
    /// Defines methods to support the comparison of named arguments for equality.
    /// </summary>
    public sealed class NamedArgumentComparer : IEqualityComparer<KeyValuePair<string, TypedConstant>>
    {
        private readonly IEqualityComparer<TypedConstant> _typedConstantEqualityComparer;

        public NamedArgumentComparer(IEqualityComparer<TypedConstant> typedConstantEqualityComparer) =>
            _typedConstantEqualityComparer = typedConstantEqualityComparer;

        /// <inheritdoc />
        public int GetHashCode([DisallowNull] KeyValuePair<string, TypedConstant> obj) => throw new NotImplementedException();

        /// <inheritdoc />
        public bool Equals(KeyValuePair<string, TypedConstant> x, KeyValuePair<string, TypedConstant> y) =>
            x.Key.Equals(y.Key) && _typedConstantEqualityComparer.Equals(x.Value, y.Value);
    }
}
