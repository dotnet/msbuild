// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel
{
    internal static class EmptyArray<T>
    {
#if NET451
        public static readonly T[] Value = new T[0];
#else
        public static readonly T[] Value = System.Array.Empty<T>();
#endif
    }
}
