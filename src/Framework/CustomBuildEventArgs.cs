// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for custom build events.
    /// </summary>
    /// <remarks>
    /// <format type="text/markdown"><![CDATA[
    /// ## Remarks
    /// > [!CAUTION]
    /// In .NET 8 and later and Visual Studio 17.8 and later, this type is deprecated; instead use <see cref="ExtendedCustomBuildEventArgs"/>.
    /// For more information, see <see href="https://learn.microsoft.com/dotnet/core/compatibility/sdk/8.0/custombuildeventargs"/>
    /// For recommended replacement, see <see href="https://learn.microsoft.com/dotnet/core/compatibility/sdk/8.0/custombuildeventargs#recommended-action" />.
    /// ]]></format>
    /// </remarks>
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
