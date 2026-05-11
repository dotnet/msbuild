// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that simply emits a message. Importance defaults to high if not specified.
    /// </summary>
    public sealed class Message : TaskExtension
    {
        /// <summary>
        /// Text to log.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Importance: high, normal, low (default normal)
        /// </summary>
        public string Importance { get; set; }

        /// <summary>
        /// Message code
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Relevant file if any.
        /// If none is provided and this is a critical message, the file containing the Message
        /// task will be used.
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// Message help keyword
        /// </summary>
        public string HelpKeyword { get; set; }

        /// <summary>
        /// Indicates if this is a critical message
        /// </summary>
        public bool IsCritical { get; set; }

        public override bool Execute()
        {
            MessageImportance messageImportance;

            if (string.IsNullOrEmpty(Importance))
            {
                messageImportance = MessageImportance.Normal;
            }
            else
            {
                try
                {
                    // Parse the raw importance string into a strongly typed enumeration.
                    messageImportance = (MessageImportance)Enum.Parse(typeof(MessageImportance), Importance, true /* case-insensitive */);
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("Message.InvalidImportance", Importance);
                    return false;
                }
            }

            if (Text != null)
            {
                if (IsCritical)
                {
                    Log.LogCriticalMessage(null, Code, HelpKeyword, File, 0, 0, 0, 0, "{0}", Text);
                }
                else
                {
                    if (File != null)
                    {
                        Log.LogMessage(null, Code, HelpKeyword, File, 0, 0, 0, 0, messageImportance, "{0}", Text);
                    }
                    else
                    {
                        Log.LogMessage(messageImportance, "{0}", Text);
                    }
                }
            }

            return true;
        }
    }
}
