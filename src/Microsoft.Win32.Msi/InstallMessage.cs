// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Describes the different types of installer messages an external user interface can receive.
    /// </summary>
    public enum InstallMessage
    {
        /// <summary>
        /// Premature termination, possibly fatal out of memory.
        /// </summary>
        FATALEXIT = 0,

        /// <summary>
        /// Formatted error message.
        /// </summary>
        ERROR = 0x01000000,

        /// <summary>
        /// Formatted warning message.
        /// </summary>
        WARNING = 0x02000000,

        /// <summary>
        /// Formatted user request message.
        /// </summary>
        USER = 0x03000000,

        /// <summary>
        /// Informative message for the log, not intended to be displayed.
        /// </summary>
        INFO = 0x04000000,

        /// <summary>
        /// List of files currently in use that must be closed before being replaced.
        /// </summary>
        FILESINUSE = 0x05000000,

        /// <summary>
        /// Request to determine a valid source location
        /// </summary>
        RESOLVESOURCE = 0x06000000,

        /// <summary>
        /// Insufficient disk space message.
        /// </summary>
        OUTOFDISKSPACE = 0x07000000,

        /// <summary>
        /// Progress: start of action.
        /// </summary>
        ACTIONSTART = 0x08000000,

        /// <summary>
        /// Action data. Record fields correspond to the template of <see cref="ACTIONSTART"/>.
        /// </summary>
        ACTIONDATA = 0x09000000,

        /// <summary>
        /// Progress bar information.
        /// </summary>
        PROGRESS = 0x0a000000,

        /// <summary>
        /// The parameters for user-interface initialization are logged.
        /// </summary>
        COMMONDATA = 0x0b000000,

        /// <summary>
        /// A message indicating the UI sequence has started. The message string is <see langword="null"/>.
        /// </summary>
        INITIALIZE = 0x0c000000,

        /// <summary>
        /// The message indicates the end of the UI sequence. The message string is <see langword="null"/>.
        /// </summary>
        TERMINATE = 0x0d000000,

        /// <summary>
        /// The message contains the name of the current dialog box.
        /// </summary>
        SHOWDIALOG = 0x0e000000,

        /// <summary>
        /// Log performance numbers, e.g. action time (Windows Installer 5.0 or later).
        /// </summary>
        PERFORMANCE = 0x0f000000,

        /// <summary>
        /// The list of applications the user can request Restart Manager to shut down and restart (Windows Installer 4.0 or later).
        /// </summary>
        RMFILESINUSE = 0x19000000,

        /// <summary>
        /// Sent prior to server-side install of a product (Windows Installer 4.5 or later).
        /// </summary>
        INSTALLSTART = 0x1a000000,

        /// <summary>
        /// Sent after server-side install (Windows Installer 4.5 or later).
        /// </summary>
        INSTALLEND = 0x1b000000
    }
}
