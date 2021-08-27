// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Compatibility.ErrorSuppression;
using Microsoft.DotNet.PackageValidation;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.Compatibility
{
    internal class CompatibilityLogger : ICompatibilityLogger
    {
        private readonly Logger _log;
        private readonly SuppressionEngine _suppressionEngine;
        private readonly bool _baselineAllErrors;

        public CompatibilityLogger(Logger log, string suppressionsFile)
            : this(log, suppressionsFile, false) {}

        public CompatibilityLogger(Logger log, string suppressionsFile, bool baselineAllErrors)
        {
            _log = log;
            _suppressionEngine = baselineAllErrors && !File.Exists(suppressionsFile) ? SuppressionEngine.Create() : SuppressionEngine.CreateFromFile(suppressionsFile);
            _baselineAllErrors = baselineAllErrors;
        }

        public void LogError(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(MessageLevel.Error, suppression, code, format, args);

        public void LogWarning(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(MessageLevel.Warning, suppression, code, format, args);

        private void LogSuppressableMessage(MessageLevel messageLevel, Suppression suppression, string code, string format, params string[] args)
        {
            if (!_suppressionEngine.IsErrorSuppressed(suppression))
            {
                if (_baselineAllErrors)
                {
                    _suppressionEngine.AddSuppression(suppression);
                }
                else
                {
                    _log.Log(new Message(messageLevel, string.Format(format, args), code));
                }
            }
        }

        public void LogMessage(MessageImportance importance, string format, params string[] args) => _log.LogMessage(importance, format, args);

        public void GenerateSuppressionsFile(string suppressionsFile)
        {
            if (_suppressionEngine.WriteSuppressionsToFile(suppressionsFile))
                LogMessage(MessageImportance.High, Resources.WroteSuppressions, suppressionsFile);
        }
    }
}
