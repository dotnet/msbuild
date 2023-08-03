// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Mapping;

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
        void InitializeRules(IRuleSettings settings);

        /// <summary>
        /// Runs the registered rules on the mapper.
        /// </summary>
        /// <typeparam name="T">The underlying type on the mapper.</typeparam>
        /// <param name="mapper">The mapper to run the rules on.</param>
        /// <returns>A list containing the list of differences for each possible combination of
        /// (<see cref="IElementMapper{T}.Left"/>, <see cref="IElementMapper{T}.Right"/>).
        /// One list of <see cref="CompatDifference"/> per the number of right elements that the <see cref="IElementMapper{T}"/> contains.
        /// </returns>
        IEnumerable<CompatDifference> Run<T>(IElementMapper<T> mapper);
    }
}
