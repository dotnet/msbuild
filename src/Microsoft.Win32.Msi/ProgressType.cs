// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Defines sub-types associated with <see cref="InstallMessage.PROGRESS"/>.
    /// </summary>
    public enum ProgressType
    {
        /// <summary>
        /// The progress bar should be reset. Field 2 of the message contains the number of ticks the bar
        /// moves for each <see cref="InstallMessage.ACTIONDATA"/> message. Field 3 indicates the direction of the progress
        /// bar (1 for backward progress, 0 for forward).
        /// </summary>
        Reset = 0,

        /// <summary>
        /// Indicates that the progress message contains action information. Field 2 contains the number of ticks the progress bar moves
        /// for each <see cref="InstallMessage.ACTIONDATA"/>. If Field 3 is 0, Field 2 should be ignored. If Field 3 is 1, increment
        /// the progress bar by the value in Field 2. Field 4 is unused.
        /// </summary>
        ActionInfo,

        /// <summary>
        /// Progress information update. Field 2 contains the number of ticks the bar has moved. Field 3 and 4 are unused.
        /// </summary>
        ProgressReport,

        /// <summary>
        /// Indicates that additional ticks can be added by an action, e.g. when a custom action executes.
        /// </summary>
        ProgressAddition
    }
}
