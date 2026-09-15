// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    internal interface ITaskLogger
    {
        bool IsEnabled { get; }

        void LogErrorWithCodeFromResources(string messageResourceName, params object[] messageArgs);

        void LogWarningWithCodeFromResources(string messageResourceName, params object[] messageArgs);

        void LogMessageFromResources(MessageImportance importance, string messageResourceName, params object[] messageArgs);

        bool LogMessageFromText(string message, MessageImportance importance);
    }

    internal readonly struct NullTaskLogger : ITaskLogger
    {
        public bool IsEnabled => false;

        public void LogErrorWithCodeFromResources(string messageResourceName, params object[] messageArgs)
        {
        }

        public void LogWarningWithCodeFromResources(string messageResourceName, params object[] messageArgs)
        {
        }

        public void LogMessageFromResources(MessageImportance importance, string messageResourceName, params object[] messageArgs)
        {
        }

        public bool LogMessageFromText(string message, MessageImportance importance)
        {
            return false;
        }
    }
}
