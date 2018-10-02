// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;

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

            if ((Importance == null) || (Importance.Length == 0))
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
