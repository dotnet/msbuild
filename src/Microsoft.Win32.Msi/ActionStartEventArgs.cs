// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Represents event data associated with an ActionStart message.
    /// </summary>
    public class ActionStartEventArgs : InstallMessageEventArgs
    {
        /// <summary>
        /// The name of the action that was started.
        /// </summary>
        public readonly string? ActionName;

        /// <summary>
        /// The description of the action that was started. 
        /// </summary>
        public readonly string? ActionDescription;

        /// <summary>
        /// The string expressing the time the action was started, formatted as HH:MM:SS.
        /// </summary>
        /// <remarks>
        /// See https://docs.microsoft.com/en-us/windows/win32/msi/time for additional information.
        /// </remarks>
        public readonly string? ActionTime;

        /// <summary>
        /// Creates a new <see cref="ActionStartEventArgs"/> instance.
        /// </summary>
        /// <param name="message">The install message text.</param>
        /// <param name="messageType">The type of install message.</param>
        /// <param name="style"></param>
        public ActionStartEventArgs(string message, InstallMessage messageType, MessageBox style) :
            base(message, messageType, style)
        {
            // This message provides information about the current action.
            // The format is "Action [1]: [2]. [3]", where a colon is used to separate Field 1 and Field 2 and a period is used to separate
            // Field 2 and Field 3. Field [1] contains the time the action was started using the Time property format (24-hour clock).
            // Field [2] contains the action's name from the sequence table. Field [3] gives the action's description from the ActionText table
            // or from the MsiProcessMessage function.
            //
            // See https://docs.microsoft.com/en-us/windows/win32/msi/parsing-windows-installer-messages

            Match match = Regex.Match(message, @"Action\s(?<actionTime>\d+:\d+:\d+):\s+(?<actionName>.*)\.\s+(?<actionDescription>.*)");

            ActionTime = match.Success ? match.Groups["actionTime"].Value : null;
            ActionName = match.Success ? match.Groups["actionName"].Value : null;
            ActionDescription = match.Success ? match.Groups["actionDescription"].Value : null;
        }
    }
}
