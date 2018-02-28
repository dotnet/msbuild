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

        private MessageImportance GetMessageImportance(string importance)
        {
            MessageImportance messageImportance = MessageImportance.Normal;

            if (string.Equals(Importance, "High", StringComparison.OrdinalIgnoreCase))
            {
                messageImportance = MessageImportance.High;
            }
            else if (string.Equals(Importance, "Low", StringComparison.OrdinalIgnoreCase))
            {
                messageImportance = MessageImportance.Low;
            }
            else if (string.Equals(Importance, "Normal", StringComparison.OrdinalIgnoreCase))
            {
                messageImportance = MessageImportance.Normal;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(importance));
            }

            return messageImportance;
        }

        protected override void LogMessage(string message)
        {
            Log.LogMessage(GetMessageImportance(Importance), message);
        }
    }
}
