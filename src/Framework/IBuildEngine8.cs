// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine7" /> to let tasks know if a warning
    /// they are about to log will be converted into an error.
    /// </summary>
    public interface IBuildEngine8 : IBuildEngine7
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

        /// <summary>
        /// Determines whether the logging service will convert the specified
        /// warning code into an error.
        /// </summary>
        /// <param name="warningCode">The warning code to check.</param>
        /// <returns>A boolean to determine whether the warning should be treated as an error.</returns>
        public bool ShouldTreatWarningAsError(string warningCode);
    }
}
