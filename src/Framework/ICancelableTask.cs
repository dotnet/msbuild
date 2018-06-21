// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
