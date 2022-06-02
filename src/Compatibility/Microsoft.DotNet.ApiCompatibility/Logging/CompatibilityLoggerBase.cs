// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.IO;

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    public abstract class CompatibilityLoggerBase
    {
        public bool BaselineAllErrors { get; }

        public SuppressionEngine SuppressionEngine { get; }

        public string? SuppressionsFile { get; }

        public CompatibilityLoggerBase(string? suppressionsFile, bool baselineAllErrors, string? noWarn)
        {
            SuppressionsFile = suppressionsFile;
            BaselineAllErrors = baselineAllErrors;
            SuppressionEngine = baselineAllErrors && !File.Exists(suppressionsFile) ?
                new SuppressionEngine(noWarn) :
                new SuppressionEngine(noWarn, suppressionsFile);
        }

        public abstract bool LogError(Suppression suppression, string code, string format, params string[] args);

        public abstract bool LogWarning(Suppression suppression, string code, string format, params string[] args);

        public abstract void LogMessage(MessageImportance importance, string format, params string[] args);

        public void WriteSuppressionFile()
        {
            if (SuppressionsFile is null)
                throw new ArgumentNullException(nameof(SuppressionsFile));

            if (SuppressionEngine.WriteSuppressionsToFile(SuppressionsFile))
            {
                LogMessage(MessageImportance.High, Resources.WroteSuppressions, SuppressionsFile);
            }
        }
    }
}
