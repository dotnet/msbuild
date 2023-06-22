// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create an <see cref="IDifferenceVisitor"/> instance.
    /// </summary>
    public interface IDifferenceVisitorFactory
    {
        /// <summary>
        /// Factory to create an <see cref="IDifferenceVisitor"/>.
        /// </summary>
        /// <returns></returns>
        IDifferenceVisitor Create();
    }
}
