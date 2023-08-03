// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Bit flags for MsiEnableLog and MsiSetExternalUI.
    /// </summary>
    [Flags]
    public enum InstallLogMode
    {
        /// <summary>
        /// Logs out of memory or fatal exit information.
        /// </summary>
        FATALEXIT = (1 << (InstallMessage.FATALEXIT >> 24)),

        /// <summary>
        /// Logs error messages.
        /// </summary>
        ERROR = (1 << (InstallMessage.ERROR >> 24)),

        /// <summary>
        /// Logs warning messages.
        /// </summary>
        WARNING = (1 << (InstallMessage.WARNING >> 24)),

        /// <summary>
        /// Logs user requests.
        /// </summary>
        USER = (1 << (InstallMessage.USER >> 24)),

        /// <summary>
        /// Logs status messages that are not displayed.
        /// </summary>
        INFO = (1 << (InstallMessage.INFO >> 24)),

        /// <summary>
        /// Logs requests to determine a valid source location.
        /// </summary>
        RESOLVESOURCE = (1 << (InstallMessage.RESOLVESOURCE >> 24)),

        /// <summary>
        /// Logs insufficient disk space.
        /// </summary>
        OUTOFDISKSPACE = (1 << (InstallMessage.OUTOFDISKSPACE >> 24)),

        /// <summary>
        /// Logs the start of new installation actions.
        /// </summary>
        ACTIONSTART = (1 << (InstallMessage.ACTIONSTART >> 24)),

        /// <summary>
        /// Logs the data record with the installation action.
        /// </summary>
        ACTIONDATA = (1 << (InstallMessage.ACTIONDATA >> 24)),

        /// <summary>
        /// Logs the parameters for user-interface initialization.
        /// </summary>
        COMMONDATA = (1 << (InstallMessage.COMMONDATA >> 24)),

        /// <summary>
        /// Logs the property values at termination.
        /// </summary>
        PROPERTYDUMP = (1 << (InstallMessage.PROGRESS >> 24)),

        /// <summary>
        /// Logs all information from other log modes, except <see cref="EXTRADEBUG"/>.
        /// </summary>
        VERBOSE = (1 << (InstallMessage.INITIALIZE >> 24)),

        /// <summary>
        /// Logs extra information such as handle creation. Not supported on Windows 2000 or XP.
        /// </summary>
        EXTRADEBUG = (1 << (InstallMessage.TERMINATE >> 24)),

        /// <summary>
        /// Collects logging information, but saves information less frequently. Deletes the log
        /// file if the install operation succeeds. If the installation fails, all logging information
        /// is saved to the log file.
        /// </summary>
        LOGONLYONERROR = (1 << (InstallMessage.SHOWDIALOG >> 24)),


        LOGPERFORMANCE = (1 << (InstallMessage.PERFORMANCE >> 24)),
        PROGRESS = (1 << (InstallMessage.PROGRESS >> 24)),
        INITIALIZE = (1 << (InstallMessage.INITIALIZE >> 24)),
        TERMINATE = (1 << (InstallMessage.TERMINATE >> 24)),
        SHOWDIALOG = (1 << (InstallMessage.SHOWDIALOG >> 24)),
        FILESINUSE = (1 << (InstallMessage.FILESINUSE >> 24)),
        RMFILESINUSE = (1 << (InstallMessage.RMFILESINUSE >> 24)),
        INSTALLSTART = (1 << (InstallMessage.INSTALLSTART >> 24)),
        INSTALLEND = (1 << (InstallMessage.INSTALLEND >> 24)),

        DEFAULT = FATALEXIT | ERROR | WARNING | USER | INFO | RESOLVESOURCE |
            OUTOFDISKSPACE | ACTIONSTART | ACTIONDATA | COMMONDATA | PROPERTYDUMP
    }
}
