﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for custom build events.
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing
    // ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is
    // immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both
    // forward and backward compatibility
    [Serializable]
    public abstract class CustomBuildEventArgs : LazyFormattedBuildEventArgs
    {
        /// <summary>
        /// Default constructor 
        /// </summary>
        protected CustomBuildEventArgs()
            : base()
        {
            // do nothing
        }

        /// <summary>
        /// This constructor allows event data to be initialized.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="senderName">name of sender</param>
        protected CustomBuildEventArgs(
            string message,
            string helpKeyword,
            string senderName)
            : this(message, helpKeyword, senderName, DateTime.UtcNow)
        {
            // do nothing
        }

        /// <summary>
        /// This constructor allows event data to be initialized including timestamp.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="senderName">name of sender</param>
        /// <param name="eventTimestamp">Timestamp when event was created</param>
        protected CustomBuildEventArgs(
            string message,
            string helpKeyword,
            string senderName,
            DateTime eventTimestamp)
            : this(message, helpKeyword, senderName, eventTimestamp, null)
        {
            // do nothing
        }

        /// <summary>
        /// This constructor allows event data to be initialized including timestamp.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="senderName">name of sender</param>
        /// <param name="eventTimestamp">Timestamp when event was created</param>
        /// <param name="messageArgs">Message arguments</param>
        protected CustomBuildEventArgs(
            string message,
            string helpKeyword,
            string senderName,
            DateTime eventTimestamp,
            params object[] messageArgs)
            : base(message, helpKeyword, senderName, eventTimestamp, messageArgs)
        {
            // do nothing
        }
    }
}
