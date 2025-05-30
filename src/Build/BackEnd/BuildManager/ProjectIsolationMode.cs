// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// The isolation mode to use.
    /// </summary>
    public enum ProjectIsolationMode
    {
        /// <summary>
        /// Do not enable isolation.
        /// </summary>
        False,

        /// <summary>
        /// Enable isolation and log isolation violations as messages.
        /// </summary>
        /// <remarks>
        /// Under this mode, only the results from top-level targets
        /// are serialized if the -orc switch is supplied to mitigate
        /// the chances of an isolation-violating target on a
        /// dependency project using incorrect state due to its
        /// dependency on a cached target whose side effects would
        /// not be taken into account. (E.g., the definition of a property.)
        /// </remarks>
        MessageUponIsolationViolation,

        /// <summary>
        /// Enable isolation and log isolation violations as errors.
        /// </summary>
        True,
    }
}
