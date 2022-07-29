// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.ApiCompat.Task
{
    /// <summary>
    /// An MSBuild based compatibility logger
    /// </summary>
    internal sealed class MSBuildCompatibilityLogger : ICompatibilityLogger
    {
        private readonly Logger _log;
        private readonly ISuppressionEngine _suppressionEngine;

        public MSBuildCompatibilityLogger(Logger log,
            ISuppressionEngine suppressionEngine)
        {
            _log = log;
            _suppressionEngine = suppressionEngine;
        }

        /// <inheritdoc />
        public bool LogError(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(MessageLevel.Error, suppression, code, format, args);

        /// <inheritdoc />
        public bool LogWarning(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(MessageLevel.Warning, suppression, code, format, args);

        /// <inheritdoc />
        public void LogMessage(MessageImportance importance, string format, params string[] args) =>
            _log.LogMessage((Build.Framework.MessageImportance)importance, format, args);

        private bool LogSuppressableMessage(MessageLevel messageLevel, Suppression suppression, string code, string format, params string[] args)
        {
            if (_suppressionEngine.IsErrorSuppressed(suppression))
                return false;

            _log.Log(new Message(messageLevel, string.Format(format, args), code));

            return true;
        }
    }
}
