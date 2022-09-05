// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.s

using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Rule runner interface that exposes functionality to initialize rules and run element mapper objects.
    /// </summary>
    public interface IRuleRunner
    {
        /// <summary>
        /// Initializes the rules provided by the <see cref="IRuleFactory" /> based on given rule settings.
        /// </summary>
        /// <param name="settings">The rule settings.</param>
        void InitializeRules(RuleSettings settings);

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
