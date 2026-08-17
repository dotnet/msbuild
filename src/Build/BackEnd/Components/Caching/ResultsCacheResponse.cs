// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using BuildResult = Microsoft.Build.Execution.BuildResult;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The type of response.
    /// </summary>
    internal enum ResultsCacheResponseType
    {
        /// <summary>
        /// There were no matching results, or some implicit targets need to be built.
        /// </summary>
        NotSatisfied,

        /// <summary>
        /// All explicit and implicit targets have results.
        /// </summary>
        Satisfied
    }

    /// <summary>
    /// Container for results of IResultsCache.SatisfyRequest
    /// </summary>
    internal struct ResultsCacheResponse
    {
        /// <summary>
        /// The results type.
        /// </summary>
        public ResultsCacheResponseType Type;

        /// <summary>
        /// The actual results, if the request was satisfied.
        /// </summary>
        public BuildResult Results;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type">The response type.</param>
        public ResultsCacheResponse(ResultsCacheResponseType type)
        {
            Type = type;
            Results = null;
        }
    }
}
