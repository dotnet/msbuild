// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    ///     Defines methods to support the comparison of objects for
    ///     equality over constrained inputs.
    /// </summary>
    internal interface IConstrainedEqualityComparer<in T> : IEqualityComparer<T>
    {
        /// <summary>
        /// Determines whether the specified objects are equal, factoring in the specified bounds when comparing <paramref name="y"/>.
        /// </summary>
        bool Equals(T x, T y, int indexY, int length);

        /// <summary>
        /// Returns a hash code for the specified object factoring in the specified bounds.
        /// </summary>
        int GetHashCode(T obj, int index, int length);
    }
}
