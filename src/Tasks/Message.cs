// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Globalization;
using System.Resources;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that simply emits a message. Importance defaults to high if not specified.
    /// </summary>
    public sealed class Message : TaskExtension
    {
        private string _text;

        /// <summary>
        /// Text to log.
        /// </summary>
        public string Text
        {
            get
            {
                return _text;
            }

            set
            {
                _text = value;
            }
        }

        private string _importance;

        /// <summary>
        /// Importance: high, normal, low (default normal)
        /// </summary>
        public string Importance
        {
            get
            {
                return _importance;
            }

            set
            {
                _importance = value;
            }
        }

        private string _code;

        /// <summary>
        /// Message code
        /// </summary>
        public string Code
        {
            get
            {
                return _code;
            }
            set
            {
                _code = value;
            }
        }

        private string _file;

        /// <summary>
        /// Relevant file if any.
        /// If none is provided and this is a critical message, the file containing the Message
        /// task will be used.
        /// </summary>
        public string File
        {
            get
            {
                return _file;
            }
            set
            {
                _file = value;
            }
        }

        private string _helpKeyword;

        /// <summary>
        /// Message help keyword
        /// </summary>
        public string HelpKeyword
        {
            get
            {
                return _helpKeyword;
            }
            set
            {
                _helpKeyword = value;
            }
        }

        private bool _isCritical;

        /// <summary>
        /// Indicates if this is a critical message
        /// </summary>
        public bool IsCritical
        {
            get
            {
                return _isCritical;
            }
            set
            {
                _isCritical = value;
            }
        }

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
