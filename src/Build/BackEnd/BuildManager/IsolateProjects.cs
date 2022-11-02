// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// The isolation mode to use.
    /// </summary>
    public enum IsolateProjects
    {
        /// <summary>
        /// Do not enable isolation.
        /// </summary>
        False,

        /// <summary>
        /// Enable isolation and log isolation violations as messages.
        /// </summary>
        Message,

        /// <summary>
        /// Enable isolation and log isolation violations as errors.
        /// </summary>
        True,
    }
}
