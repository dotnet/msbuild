// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
