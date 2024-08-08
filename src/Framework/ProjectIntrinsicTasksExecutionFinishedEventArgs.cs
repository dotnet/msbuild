// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the project intrinsic tasks execution finished event.
    /// </summary>
    public sealed class ProjectIntrinsicTasksExecutionFinishedEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectIntrinsicTasksExecutionFinishedEventArgs"/> class.
        /// </summary>
        public ProjectIntrinsicTasksExecutionFinishedEventArgs() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectIntrinsicTasksExecutionFinishedEventArgs"/> class.
        /// </summary>
        public ProjectIntrinsicTasksExecutionFinishedEventArgs(string? message, params object[]? messageArgs)
            : base(message, helpKeyword: null, senderName: null, MessageImportance.Low) { }
    }
}
