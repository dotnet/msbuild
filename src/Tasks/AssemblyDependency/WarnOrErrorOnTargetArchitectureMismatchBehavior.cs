// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Enum describing the behavior when a a primary reference has an architecture different from the project
    /// </summary>
    internal enum WarnOrErrorOnTargetArchitectureMismatchBehavior
    {
        /// <summary>
        /// Print an error
        /// </summary>
        Error,

        /// <summary>
        /// Print a warning
        /// </summary>
        Warning,

        /// <summary>
        /// Do nothing
        /// </summary>
        None
    }
}
