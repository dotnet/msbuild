// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ToolPackage
{
    internal class ResolverCacheInconsistentException : Exception
    {
        public ResolverCacheInconsistentException(string message) : base(message)
        {
        }
    }
}
