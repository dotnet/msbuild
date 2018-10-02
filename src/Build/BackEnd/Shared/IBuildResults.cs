// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Execution;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// An interface representing results for a build request
    /// </summary>
    internal interface IBuildResults
    {
        /// <summary>
        /// The exception, if any, generated while the build ran.
        /// </summary>
        Exception Exception { get; }

        /// <summary>
        /// The overall build result code.
        /// </summary>
        BuildResultCode OverallResult { get; }

        /// <summary>
        /// Returns an enumerator for all target results in this build result
        /// </summary>
        IDictionary<string, TargetResult> ResultsByTarget { get; }

        /// <summary>
        /// Set of environment variables for the configuration this result came from
        /// </summary>
        Dictionary<string, string> SavedEnvironmentVariables { get; set; }

        /// <summary>
        /// The current directory for the configuration this result came from
        /// </summary>
        string SavedCurrentDirectory { get; set; }

        /// <summary>
        /// Gets the results for a target in the build request
        /// </summary>
        /// <param name="target">The target name</param>
        /// <returns>The target results</returns>
        ITargetResult this[string target] { get; }

        /// <summary>
        /// Returns true if there are results for the specified target
        /// </summary>
        /// <param name="target">The target name</param>
        /// <returns>True if results exist, false otherwise.</returns>
        bool HasResultsForTarget(string target);
    }
}
