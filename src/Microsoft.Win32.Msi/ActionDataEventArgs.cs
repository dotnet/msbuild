// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Represents event data associated with the ActionData message.
    /// </summary>
    public class ActionDataEventArgs : InstallMessageEventArgs
    {
        public ActionDataEventArgs(string message, InstallMessage messageType, MessageBox style) :
            base(message, messageType, style)
        {

        }
    }
}
