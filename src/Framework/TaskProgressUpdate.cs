// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Describes the latest known progress of a task operation.
    /// </summary>
    [Serializable]
    public readonly struct TaskProgressUpdate
    {
        /// <summary>
        /// Initializes a progress update.
        /// </summary>
        /// <param name="completed">The absolute amount of work completed.</param>
        /// <param name="total">The current total amount of work, or <see langword="null"/> when it is unknown.</param>
        /// <param name="status">An optional description of the operation's current activity.</param>
        public TaskProgressUpdate(long completed, long? total = null, string? status = null)
        {
            Completed = completed;
            Total = total;
            Status = status;
        }

        /// <summary>
        /// Gets the absolute amount of work completed.
        /// </summary>
        public long Completed { get; }

        /// <summary>
        /// Gets the current total amount of work, or <see langword="null"/> when it is unknown.
        /// </summary>
        public long? Total { get; }

        /// <summary>
        /// Gets an optional description of the operation's current activity.
        /// </summary>
        public string? Status { get; }
    }
}
