// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create an ApiComparer
    /// </summary>
    public interface IApiComparerFactory
    {
        /// <summary>
        /// Create an ApiComparer
        /// </summary>
        /// <returns>Returns an ApiComparer instance</returns>
        IApiComparer Create();
    }
}
