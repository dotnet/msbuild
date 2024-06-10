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
        /// Sender field cannot be set here and is assumed to be "MSBuild"
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        public BuildCanceledEventArgs(
            string? message,
            string? helpKeyword)
            : this(message, helpKeyword, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Constructor which allows the timestamp to be set
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        public BuildCanceledEventArgs(
            string? message,
            string? helpKeyword,
            DateTime eventTimestamp)
            : this(message, helpKeyword, eventTimestamp, null)
        {
            // do nothing
        }

        /// <summary>
        /// Constructor which allows the timestamp to be set
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        /// <param name="messageArgs">message arguments</param>
        public BuildCanceledEventArgs(
            string? message,
            string? helpKeyword,
            DateTime eventTimestamp,
            params object[]? messageArgs)
            : base(message, helpKeyword, "MSBuild", eventTimestamp, messageArgs)
        {
            // do nothing
        }
    }
}
