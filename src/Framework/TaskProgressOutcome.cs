// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Describes how a task progress operation ended.
    /// </summary>
    /// <remarks>
    /// The outcome is presentation and diagnostic data. <see cref="Failed"/> does not fail the task, and
    /// <see cref="Completed"/> does not make it succeed. Task return values and logged errors remain authoritative.
    /// </remarks>
    public enum TaskProgressOutcome
    {
        /// <summary>
        /// The operation finished normally.
        /// </summary>
        Completed,

        /// <summary>
        /// The operation was canceled.
        /// </summary>
        Canceled,

        /// <summary>
        /// The operation did not finish successfully.
        /// </summary>
        Failed,

        /// <summary>
        /// The reporter was disposed, or task execution ended, without an explicit outcome.
        /// </summary>
        Abandoned,
    }
}
