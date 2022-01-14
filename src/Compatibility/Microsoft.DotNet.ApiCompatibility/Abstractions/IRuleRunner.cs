// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.s

using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Interface for rule drivers to implement in order to be used returned by the <see cref="IRuleRunnerFactory"/>
    /// </summary>
    public interface IRuleRunner
    {
        /// <summary>
        /// Runs the registered rules on the mapper.
        /// </summary>
        /// <typeparam name="T">The underlying type on the mapper.</typeparam>
        /// <param name="mapper">The mapper to run the rules on.</param>
        /// <returns>A list containing the list of differences for each possible combination of
        /// (<see cref="ElementMapper{T}.Left"/>, <see cref="ElementMapper{T}.Right"/>).
        /// One list of <see cref="CompatDifference"/> per the number of right elements that the <see cref="ElementMapper{T}"/> contains.
        /// </returns>
        IReadOnlyList<IEnumerable<CompatDifference>> Run<T>(ElementMapper<T> mapper);
    }
}
