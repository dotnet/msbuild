// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ToolPackage
{
    internal class ResolverCacheInconsistentException : Exception
    {
        public ResolverCacheInconsistentException(string message) : base(message)
        {
        }
    }
}
