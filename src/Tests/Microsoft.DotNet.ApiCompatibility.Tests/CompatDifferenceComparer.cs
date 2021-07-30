// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    internal class CompatDifferenceComparer : IEqualityComparer<CompatDifference>
    {
        public static CompatDifferenceComparer Default => new CompatDifferenceComparer();

        public bool Equals(CompatDifference x, CompatDifference y) =>
            (x == null && y == null) ||
            x != null && y != null &&
            string.Equals(x.DiagnosticId, y.DiagnosticId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ReferenceId, y.ReferenceId, StringComparison.OrdinalIgnoreCase) &&
            x.Type == y.Type;

        public int GetHashCode(CompatDifference difference)
        {
            int hashCode = 1447485498;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(difference.DiagnosticId);
            hashCode = hashCode * -1521134295 + EqualityComparer<DifferenceType>.Default.GetHashCode(difference.Type);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(difference.ReferenceId);
            return hashCode;
        }
    }
}
