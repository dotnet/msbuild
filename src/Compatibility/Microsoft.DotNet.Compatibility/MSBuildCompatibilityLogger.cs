// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.Compatibility
{
    /// <summary>
    /// An MSBuild based compatibility logger
    /// </summary>
    internal sealed class MSBuildCompatibilityLogger : CompatibilityLoggerBase
    {
        private readonly Logger _log;

        public MSBuildCompatibilityLogger(Logger log, string? suppressionsFile, bool baselineAllErrors, string? noWarn)
            : base(suppressionsFile, baselineAllErrors, noWarn)
        {
            _log = log;
        }

        public override bool LogError(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(MessageLevel.Error, suppression, code, format, args);

        public override bool LogWarning(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(MessageLevel.Warning, suppression, code, format, args);

        public override void LogMessage(MessageImportance importance, string format, params string[] args) =>
            _log.LogMessage((Build.Framework.MessageImportance)importance, format, args);

        private bool LogSuppressableMessage(MessageLevel messageLevel, Suppression suppression, string code, string format, params string[] args)
        {
            if (SuppressionEngine.IsErrorSuppressed(suppression))
                return false;

            if (BaselineAllErrors)
            {
                SuppressionEngine.AddSuppression(suppression);
                return false;
            }

            _log.Log(new Message(messageLevel, string.Format(format, args), code));
            return true;
        }
    }
}
