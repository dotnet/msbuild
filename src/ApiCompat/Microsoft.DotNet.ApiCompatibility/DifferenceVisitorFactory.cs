// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create a DifferenceVisitor instance.
    /// </summary>
    public sealed class DifferenceVisitorFactory : IDifferenceVisitorFactory
    {
        /// <inheritdoc />
        public IDifferenceVisitor Create() => new DifferenceVisitor();
    }
}
