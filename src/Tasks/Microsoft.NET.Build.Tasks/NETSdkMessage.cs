// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Provides a localizable mechanism for logging messages with different levels of importance from the SDK targets.
    /// </summary>
    public class NETSdkMessage : MessageBase
    {
        public string Importance { get; set; } = "Normal";

        private MessageImportance MessageImportance
        {
            get
            {
                MessageImportance importance = MessageImportance.Normal;

                if (string.Equals(Importance, "High", StringComparison.OrdinalIgnoreCase))
                {
                    importance = MessageImportance.High;
                }
                else if (string.Equals(Importance, "Low", StringComparison.OrdinalIgnoreCase))
                {
                    importance = MessageImportance.Low;
                }
                else if (string.Equals(Importance, "Normal", StringComparison.OrdinalIgnoreCase))
                {
                    importance = MessageImportance.Normal;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(importance));
                }

                return importance;
            }
        }

        protected override void LogMessage(string message)
        {
            Log.LogMessage(MessageImportance, message);
        }
    }
}
