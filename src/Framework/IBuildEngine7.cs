// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends IBuildEngine6 to allow tasks and build scheduler to coordinate resource (cores) usage.
    /// </summary>

    public interface IBuildEngine7 : IBuildEngine6
    {
        /// <summary>
        /// If a task launches multiple parallel processes, it should ask how many cores it can use.
        /// </summary>
        /// <param name="requestedCores">The number of cores a task can potentially use</param>
        /// <returns>The number of cores a task is allowed to use</returns>
        int RequestCores(ITask task, int requestedCores);

        /// <summary>
        /// A task should notify the build manager when all or some of the requested cores are not used anymore.
        /// When task is finished, the cores it requested are automatically released.
        /// </summary>
        /// <param name="releasedCores"></param>
        void ReleaseCores(ITask task, int releasedCores);
    }
}
