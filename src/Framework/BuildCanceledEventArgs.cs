// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This class represents the event arguments for build canceled events.
    /// </summary>
    public sealed class BuildCanceledEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Constructor to initialize all parameters.
        /// Sender field cannot be set here and is assumed to be "MSBuild".
        /// </summary>
        /// <param name="message">text message</param>
        public BuildCanceledEventArgs(string message)
            : this(message, DateTime.UtcNow)
        { 
        }

        /// <summary>
        /// Constructor which allows the timestamp to be set.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        public BuildCanceledEventArgs(
            string message,
            DateTime eventTimestamp)
            : this(message, eventTimestamp, null)
        {
        }

        /// <summary>
        /// Constructor which allows the timestamp to be set.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        /// <param name="messageArgs">message arguments</param>
        public BuildCanceledEventArgs(
            string message,
            DateTime eventTimestamp,
            params object[]? messageArgs)
            : base(message, null, "MSBuild", eventTimestamp, messageArgs)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new InternalErrorException("Message cannot be null or consist only white-space characters.");
            }
        }
    }
}
