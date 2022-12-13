// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    /// <summary>
    /// Class to define common logging abstraction to the console across the APICompat and GenAPI codebases.
    /// </summary>
    public class ConsoleLog : ILog
    {
        private readonly MessageImportance _messageImportance;

        public ConsoleLog(MessageImportance messageImportance)
        {
            _messageImportance = messageImportance;
        }

        /// <inheritdoc />
        public void LogError(string code, string format, params string[] args) => Console.Error.WriteLine($"{code}: {string.Format(format, args)}");

        /// <inheritdoc />
        public void LogWarning(string code, string format, params string[] args) => Console.WriteLine($"{code}: {string.Format(format, args)}");

        /// <inheritdoc />
        public void LogMessage(MessageImportance importance, string format, params string[] args)
        {
            if (importance > _messageImportance)
            {
                return;
            }
            Console.WriteLine(format, args);
        }
    }
}
