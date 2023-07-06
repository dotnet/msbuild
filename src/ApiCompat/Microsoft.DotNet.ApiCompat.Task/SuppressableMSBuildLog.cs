// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompat.Task
{
    /// <summary>
    /// Class that can log Suppressions in an MSBuild task, by implementing MSBuildLog and ISuppressableLog.
    /// </summary>
    internal sealed class SuppressableMSBuildLog : MSBuildLog, ISuppressableLog
    {
        private readonly ISuppressionEngine _suppressionEngine;

        /// <inheritdoc />
        public bool HasLoggedErrorSuppressions { get; private set; }

        public SuppressableMSBuildLog(NET.Build.Tasks.Logger log,
            ISuppressionEngine suppressionEngine)
            : base(log)
        {
            _suppressionEngine = suppressionEngine;
        }

        /// <inheritdoc />
        public bool LogError(Suppression suppression, string code, string message)
        {
            if (_suppressionEngine.IsErrorSuppressed(suppression))
                return false;

            HasLoggedErrorSuppressions = true;
            LogError(code, message);

            return true;
        }

        /// <inheritdoc />
        public bool LogWarning(Suppression suppression, string code, string message)
        {
            if (_suppressionEngine.IsErrorSuppressed(suppression))
                return false;

            LogWarning(code, message);

            return true;
        }
    }
}
