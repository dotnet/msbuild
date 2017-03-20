// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    internal class ReferenceComparer<T> : IEqualityComparer, IEqualityComparer<T>
    {
        public static ReferenceComparer<T> Instance { get; } = new ReferenceComparer<T>();

        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        bool IEqualityComparer.Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
