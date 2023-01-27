// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompat.Tool
{
    /// <summary>
    /// Class that can log Suppressions to the Console, by implementing ConsoleLog and ISuppressableLog.
    /// </summary>
    internal sealed class SuppressableConsoleLog : ConsoleLog, ISuppressableLog
    {
        private readonly ISuppressionEngine _suppressionEngine;

        /// <inheritdoc />
        public bool HasLoggedSuppressions { get; private set; }

        public SuppressableConsoleLog(ISuppressionEngine suppressionEngine,
            MessageImportance messageImportance)
            : base(messageImportance)
        {
            _suppressionEngine = suppressionEngine;
        }

        /// <inheritdoc />
        public bool LogError(Suppression suppression, string code, string message)
        {
            if (_suppressionEngine.IsErrorSuppressed(suppression))
                return false;
            
            HasLoggedSuppressions = true;
            base.LogError(code, message);
            
            return true;
        }

        /// <inheritdoc />
        public bool LogWarning(Suppression suppression, string code, string message)
        {
            if (_suppressionEngine.IsErrorSuppressed(suppression))
                return false;
            
            HasLoggedSuppressions = true;
            base.LogWarning(code, message);
            
            return true;
        }
    }
}
