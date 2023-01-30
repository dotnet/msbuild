// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for tasks which can be cancelled.
    /// </summary>
    public interface ICancelableTask : ITask
    {
        /// <summary>
        /// Instructs the task to exit as soon as possible, or to immediately exit if Execute is invoked after this method.
        /// </summary>
        /// <remarks>
        /// Cancel() may be called at any time after the task has been instantiated, even before <see cref="ITask.Execute()"/> is called.
        /// Cancel calls may come in from any thread.  The implementation of this method should not block indefinitely.
        /// </remarks>
        void Cancel();
    }
}
