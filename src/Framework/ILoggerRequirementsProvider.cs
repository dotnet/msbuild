// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Standard requirements for loggers.
    /// </summary>
    public static class StandardRequirements
    {
        /// <summary>
        /// The logger requires evaluation profile information.
        /// </summary>
        public const string EvaluationProfile = "EvaluationProfile";

        /// <summary>
        /// The logger requires the input items for tasks.
        /// </summary>
        public const string TaskInputs = "TaskInputs";
    }

    /// <summary>
    /// An interface that allows a logger to specify requirements that it has in terms of events
    /// provided and other capabilities such as task input logging and evaluation profiling.
    /// </summary>
    public interface ILoggerRequirementsProvider
    {
        IEnumerable<string> Requirements { get; }
    }
}
