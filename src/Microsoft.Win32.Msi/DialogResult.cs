// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Specifies the return value of a dialog box. An external user interface handler uses these values
    /// to indicate how it handled an installer message.
    /// </summary>
    /// <remarks>
    /// See  https://docs.microsoft.com/en-us/windows/win32/msi/returning-values-from-an-external-user-interface-handler
    /// for more information.
    /// </remarks>
    public enum DialogResult
    {
        /// <summary>
        /// An internal error occurred in the external UI handler.
        /// </summary>
        Error = -1,

        /// <summary>
        /// The external UI handler did not handle the installer message. The installer must handle the message itself.
        /// </summary>
        None = 0,

        /// <summary>
        /// The OK button was pressed by the user. The message information was understood.
        /// </summary>
        IDOK = 1,

        /// <summary>
        /// The CANCEL button was pressed. Cancel the installation.
        /// </summary>
        IDCANCEL = 2,

        /// <summary>
        /// The ABORT button was pressed. Abort the installation.
        /// </summary>
        IDABORT = 3,

        /// <summary>
        /// The RETRY button was pressed. Try the action again.
        /// </summary>
        IDRETRY = 4,

        /// <summary>
        /// The IGNORE button was pressed. Ignore the error and continue.
        /// </summary>
        IDIGNORE = 5,

        /// <summary>
        /// The YES button was pressed. The affirmative response, continue with current sequence of events.
        /// </summary>
        IDYES = 6,

        /// <summary>
        /// The NO button was pressed. The negative response, do not continue with current sequence of events.
        /// </summary>
        IDNO = 7
    }
}
