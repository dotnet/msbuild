﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the environment variable read event.
    /// </summary>
    [Serializable]
    public class EnvironmentVariableReadEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes an instance of the EnvironmentVariableReadEventArgs class.
        /// </summary>
        public EnvironmentVariableReadEventArgs()
        {
        }

        /// <summary>
        /// Initializes an instance of the EnvironmentVariableReadEventArgs class.
        /// </summary>
        /// <param name="environmentVariableName">The name of the environment variable that was read.</param>
        /// <param name="message">The value of the environment variable that was read.</param>
        /// <param name="helpKeyword">Help keyword.</param>
        /// <param name="senderName">The name of the sender of the event.</param>
        /// <param name="importance">The importance of the message.</param>
        public EnvironmentVariableReadEventArgs(
            string environmentVariableName,
            string message,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low) : base(message, helpKeyword, senderName, importance)
        {
            this.EnvironmentVariableName = environmentVariableName;
        }

        /// <summary>
        /// The name of the environment variable that was read.
        /// </summary>
        public string EnvironmentVariableName { get; set; }
    }
}
