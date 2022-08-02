// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.ApiCompat.Tool
{
    /// <summary>
    /// A console based compatibility logger
    /// </summary>
    internal sealed class ConsoleCompatibilityLogger : ICompatibilityLogger
    {
        private readonly ISuppressionEngine _suppressionEngine;
        private readonly MessageImportance _messageImportance;

        public ConsoleCompatibilityLogger(ISuppressionEngine suppressionEngine,
            MessageImportance messageImportance)
        {
            _suppressionEngine = suppressionEngine;
            _messageImportance = messageImportance;
        }

        /// <inheritdoc />
        public bool LogError(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(errorOutput: true, suppression, code, format, args);

        /// <inheritdoc />
        public bool LogWarning(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(errorOutput: false, suppression, code, format, args);

        /// <inheritdoc />
        public void LogMessage(MessageImportance importance, string format, params string[] args)
        {
            if (importance > _messageImportance)
                return;

            Console.WriteLine(format, args);
        }

        private bool LogSuppressableMessage(bool errorOutput, Suppression suppression, string code, string format, params string[] args)
        {
            if (_suppressionEngine.IsErrorSuppressed(suppression))
                return false;

            TextWriter textWriter = errorOutput ? Console.Error : Console.Out;
            textWriter.WriteLine(code + ": " + string.Format(format, args));

            return true;
        }
    }
}
