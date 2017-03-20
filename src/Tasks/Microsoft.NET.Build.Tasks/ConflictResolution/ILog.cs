// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    public enum LogImportance
    {
        Low = MessageImportance.Low,
        Normal = MessageImportance.Normal,
        High = MessageImportance.High
    }


    public interface ILog
    {
        //
        // Summary:
        //     Logs an error with the specified message.
        //
        // Parameters:
        //   message:
        //     The message.
        //
        //   messageArgs:
        //     Optional arguments for formatting the message string.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     message is null.
        void LogError(string message, params object[] messageArgs);

        //
        // Summary:
        //     Logs a message with the specified string.
        //
        // Parameters:
        //   message:
        //     The message.
        //
        //   messageArgs:
        //     The arguments for formatting the message.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     message is null.
        void LogMessage(string message, params object[] messageArgs);

        //
        // Summary:
        //     Logs a message with the specified string and importance.
        //
        // Parameters:
        //   importance:
        //     One of the enumeration values that specifies the importance of the message.
        //
        //   message:
        //     The message.
        //
        //   messageArgs:
        //     The arguments for formatting the message.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     message is null.
        void LogMessage(LogImportance importance, string message, params object[] messageArgs);

        //
        // Summary:
        //     Logs a warning with the specified message.
        //
        // Parameters:
        //   message:
        //     The message.
        //
        //   messageArgs:
        //     Optional arguments for formatting the message string.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     message is null.
        void LogWarning(string message, params object[] messageArgs);
    }
}
