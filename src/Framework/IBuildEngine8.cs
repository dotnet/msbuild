// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine7" /> to allow tasks to set whether they want to
    /// log an error when a task returns without logging an error.
    /// </summary>
    public interface IBuildEngine8 : IBuildEngine7
    {
        /// <summary>
        /// If a task launches multiple parallel processes, it should ask how many cores it can use.
        /// </summary>
        /// <param name="requestedCores">The number of cores a task can potentially use.</param>
        /// <returns>The number of cores a task is allowed to use.</returns>
        int? RequestCores(int requestedCores);

        /// <summary>
        /// A task should notify the build manager when all or some of the requested cores are not used anymore.
        /// When task is finished, the cores it requested are automatically released.
        /// </summary>
        /// <param name="coresToRelease">Number of cores no longer in use.</param>
        void ReleaseCores(int coresToRelease);
    }
}
