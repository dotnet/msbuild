// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks
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
