// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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
