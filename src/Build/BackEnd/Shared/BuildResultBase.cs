// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Execution
{
    public abstract class BuildResultBase
    {
        /// <summary>
        /// Returns the submission id.
        /// </summary>
        public abstract int SubmissionId { get; }

        /// <summary>
        /// Returns a flag indicating if a circular dependency was detected.
        /// </summary>
        public abstract bool CircularDependency { get; }

        /// <summary>
        /// Returns the exception generated while this result was run, if any.
        /// </summary>
        public abstract Exception? Exception { get; internal set; }

        /// <summary>
        /// Returns the overall result for this result set.
        /// </summary>
        public abstract BuildResultCode OverallResult { get; }
    }
}
