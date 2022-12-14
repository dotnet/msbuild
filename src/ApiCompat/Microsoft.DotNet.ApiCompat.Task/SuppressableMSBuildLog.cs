// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompat.Task
{
    /// <summary>
    /// Class that can log Suppressions in an MSBuild task, by implementing MSBuildLog and ISuppressableLog.
    /// </summary>
    internal class SuppressableMSBuildLog : MSBuildLog, ISuppressableLog
    {
        private readonly ISuppressionEngine _suppressionEngine;

        public SuppressableMSBuildLog(NET.Build.Tasks.Logger log, ISuppressionEngine suppressionEngine) : base(log)
        {
            _suppressionEngine = suppressionEngine;
        }
        public bool SuppressionWasLogged { get; private set; }

        /// <inheritdoc />
        public bool LogError(Suppression suppression, string code, string format, params string[] args) => LogSuppressableMessage(MessageLevel.Error, suppression, code, format, args);

        /// <inheritdoc />
        public bool LogWarning(Suppression suppression, string code, string format, params string[] args) => LogSuppressableMessage(MessageLevel.Warning, suppression, code, format, args);

        private bool LogSuppressableMessage(MessageLevel messageLevel, Suppression suppression, string code, string format, params string[] args)
        {
            if (_suppressionEngine.IsErrorSuppressed(suppression))
            {
                return false;
            }
            SuppressionWasLogged = true;
            _log.Log(new NET.Build.Tasks.Message((NET.Build.Tasks.MessageLevel)messageLevel, string.Format(format, args), code));
            return true;
        }
    }
}
