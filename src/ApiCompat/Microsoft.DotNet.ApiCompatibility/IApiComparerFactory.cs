// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create an <see cref="IApiComparer"/> instance.
    /// </summary>
    public interface IApiComparerFactory
    {
        /// <summary>
        /// Creates an IApiComparer.
        /// </summary>
        /// <returns>Returns an <see cref="IApiComparer"/> instance</returns>
        IApiComparer Create();
    }
}
