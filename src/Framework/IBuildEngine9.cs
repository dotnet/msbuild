// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine8" /> to provide resource management API to tasks.
    /// </summary>
    public interface IBuildEngine9 : IBuildEngine8
    {
        /// <summary>
        /// If a task launches multiple parallel processes, it should ask how many cores it can use.
        /// </summary>
        /// <param name="requestedCores">The number of cores a task can potentially use.</param>
        /// <returns>The number of cores a task is allowed to use.</returns>
        int RequestCores(int requestedCores);

        /// <summary>
        /// A task should notify the build manager when all or some of the requested cores are not used anymore.
        /// When task is finished, the cores it requested are automatically released.
        /// </summary>
        /// <param name="coresToRelease">Number of cores no longer in use.</param>
        void ReleaseCores(int coresToRelease);
    }
}
