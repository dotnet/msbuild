// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Base class for install message event arguments.
    /// </summary>
    public class InstallMessageEventArgs : EventArgs
    {
        /// <summary>
        /// The type of message that was received.
        /// </summary>
        public readonly InstallMessage MessageType;

        /// <summary>
        /// The text of the install message.
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// The result to return after handling the event. Default is <see cref="DialogResult.IDOK"/>.
        /// </summary>
        public DialogResult Result
        {
            get;
            set;
        } = DialogResult.IDOK;

        /// <summary>
        /// The style of the message box, including its buttons, icon and default button.
        /// </summary>
        public readonly MessageBox Style;

        public InstallMessageEventArgs(string message, InstallMessage messageType, MessageBox style)
        {
            MessageType = messageType;
            Message = message;
            Style = style;
        }
    }
}
