// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
