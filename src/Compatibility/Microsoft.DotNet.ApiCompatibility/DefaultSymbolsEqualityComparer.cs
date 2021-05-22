// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Extensions;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility
{
    internal class DefaultSymbolsEqualityComparer : IEqualityComparer<ISymbol>
    {
        public bool Equals(ISymbol x, ISymbol y) =>
            string.Equals(GetKey(x), GetKey(y), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ISymbol obj) =>
            GetKey(obj).GetHashCode();

        private static string GetKey(ISymbol symbol) => symbol.ToComparisonDisplayString();
    }
}
