// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        /// Under this mode, only the results from specific (usually
        /// top-level) targets are serialized if the -orc switch is
        /// supplied. This is to mitigate the chances of an isolation-
        /// violating target on a dependency project using incorrect state
        /// due to its dependency on a cached target whose side effects would
        /// not be taken into account. (E.g., the definition of a property.)
        /// </remarks>
        MessageUponIsolationViolation,

        /// <summary>
        /// Enable isolation and log isolation violations as errors.
        /// </summary>
        True,
    }
}
