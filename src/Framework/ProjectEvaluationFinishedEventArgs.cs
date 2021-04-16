// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using Microsoft.Build.Framework.Profiler;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the project evaluation finished event.
    /// </summary>
    [Serializable]
    public sealed class ProjectEvaluationFinishedEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the ProjectEvaluationFinishedEventArgs class.
        /// </summary>
        public ProjectEvaluationFinishedEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ProjectEvaluationFinishedEventArgs class.
        /// </summary>
        public ProjectEvaluationFinishedEventArgs(string message, params object[] messageArgs)
            : base(message, helpKeyword: null, senderName: null, DateTime.UtcNow, messageArgs)
        {
        }

        /// <summary>
        /// Gets or sets the full path of the project that started evaluation.
        /// </summary>
        public string ProjectFile { get; set; }

        /// <summary>
        /// Global properties used during this evaluation.
        /// </summary>
        public IEnumerable GlobalProperties { get; set; }

        /// <summary>
        /// Final set of properties produced by this evaluation.
        /// </summary>
        public IEnumerable Properties { get; set; }

        /// <summary>
        /// Final set of items produced by this evaluation.
        /// </summary>
        public IEnumerable Items { get; set; }

        /// <summary>
        /// The result of profiling a project.
        /// </summary>
        /// <remarks>
        /// Null if profiling is not turned on
        /// </remarks>
        public ProfilerResult? ProfilerResult { get; set; }
    }
}
