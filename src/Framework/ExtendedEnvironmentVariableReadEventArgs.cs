// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the environment variable read event.
    /// </summary>
    [Serializable]
    public class ExtendedEnvironmentVariableReadEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes an instance of the ExtendedEnvironmentVariableReadEventArgs class.
        /// </summary>
        /// <param name="envVarName">The name of the environment variable that was read.</param>
        /// <param name="envVarValue">The value of the environment variable that was read.</param>
        /// <param name="file">file associated with the event</param>
        /// <param name="line">line number (0 if not applicable)</param>
        /// <param name="column">column number (0 if not applicable)</param>
        /// <param name="helpKeyword">Help keyword.</param>
        /// <param name="senderName">The name of the sender of the event.</param>
        /// <param name="importance">The importance of the message.</param>
        public ExtendedEnvironmentVariableReadEventArgs(
            string envVarName,
            string envVarValue,
            string file,
            int line,
            int column,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low)
            : base("", "", file, line, column, 0, 0, envVarValue, helpKeyword, senderName, importance) => EnvironmentVariableName = envVarName;

        /// <summary>
        /// The name of the environment variable that was read.
        /// </summary>
        public string EnvironmentVariableName { get; set; }
    }
}
