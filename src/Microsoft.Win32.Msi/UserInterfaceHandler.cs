// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// External user-interface handler that generates events from install messages.
    /// </summary>
    public class UserInterfaceHandler
    {
        private readonly InstallUIHandler _previousHandler;

        /// <summary>
        /// Event handler for <see cref="InstallMessage.ACTIONDATA"/>.
        /// </summary>
        public event EventHandler<ActionDataEventArgs>? ActionData;

        /// <summary>
        /// Event handler for <see cref="InstallMessage.ACTIONSTART"/>.
        /// </summary>
        public event EventHandler<ActionStartEventArgs>? ActionStart;

        /// <summary>
        /// Event handler for <see cref="InstallMessage.PROGRESS"/>.
        /// </summary>
        public event EventHandler<ProgressEventArgs>? Progress;

        /// <summary>
        /// Creates a new <see cref="UserInterfaceHandler"/> instance to register an external event handler to process 
        /// install messages and triggering separate events for each type of <see cref="InstallMessage"/>.
        /// </summary>
        /// <param name="messageFilter">Specifies which messages to handle.</param>
        public UserInterfaceHandler(InstallLogMode messageFilter)
        {
            _previousHandler = NativeMethods.MsiSetExternalUI(Handler, (uint)messageFilter, IntPtr.Zero);
        }

        protected virtual DialogResult OnActionData(ActionDataEventArgs e)
        {
            EventHandler<ActionDataEventArgs>? handler = ActionData;
            handler?.Invoke(this, e);
            return e.Result;
        }

        protected virtual DialogResult OnActionStart(ActionStartEventArgs e)
        {
            EventHandler<ActionStartEventArgs>? handler = ActionStart;
            handler?.Invoke(this, e);
            return e.Result;
        }

        protected virtual DialogResult OnProgress(ProgressEventArgs e)
        {
            EventHandler<ProgressEventArgs>? handler = Progress;
            handler?.Invoke(this, e);
            return e.Result;
        }

        /// <summary>
        /// Message handler for callbacks from the installer.
        /// </summary>
        /// <param name="pvContext">Pointer to the application context.</param>
        /// <param name="iMessageType">A combination of a message box style, icon type, one default button and an installation
        /// message type.</param>
        /// <param name="message">The message text.</param>
        /// <returns>-1 if an internal error occurred or 0 if the message was not handled, otherwise a result corresponding
        /// to the button type in the message can be returned.
        /// </returns>
        /// <remarks>
        /// See https://docs.microsoft.com/en-us/windows/win32/api/msi/nc-msi-installui_handlerw
        /// </remarks>
        private DialogResult Handler(IntPtr pvContext, uint iMessageType, [MarshalAs(UnmanagedType.LPWStr)] string message)
        {
            // The message type value is composed from multiple different fields and includes
            // flags for the message type, along with button controls and icons.
            InstallMessage messageType = (InstallMessage)(iMessageType & 0xff000000);
            MessageBox messageBoxStyle = (MessageBox)(iMessageType & (uint)(MessageBox.TYPEMASK | MessageBox.ICONMASK | MessageBox.DEFMASK));

            return messageType switch
            {
                InstallMessage.ACTIONDATA => OnActionData(new ActionDataEventArgs(message, messageType, messageBoxStyle)),
                InstallMessage.ACTIONSTART => OnActionStart(new ActionStartEventArgs(message, messageType, messageBoxStyle)),
                InstallMessage.PROGRESS => OnProgress(new ProgressEventArgs(message, messageType, messageBoxStyle)),

                // The handler must return 0 and allow Windows Installer to handle the message. The external user
                // interface handler can monitor for this message, but it should not perform any action that affects the installation.
                InstallMessage.RESOLVESOURCE => DialogResult.None,

                _ => DialogResult.IDOK,
            };
        }
    }
}
