// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.Compatibility.Console
{
    /// <summary>
    /// A console based compatibility logger
    /// </summary>
    internal sealed class ConsoleCompatibilityLogger : CompatibilityLoggerBase
    {
        public ConsoleCompatibilityLogger(string? suppressionsFile, bool baselineAllErrors, string? noWarn)
            : base(suppressionsFile, baselineAllErrors, noWarn)
        {
        }

        public override bool LogError(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(errorOutput: true, suppression, code, format, args);

        public override bool LogWarning(Suppression suppression, string code, string format, params string[] args) =>
            LogSuppressableMessage(errorOutput: false, suppression, code, format, args);

        public override void LogMessage(MessageImportance importance, string format, params string[] args) =>
            System.Console.WriteLine(format, args);

        private bool LogSuppressableMessage(bool errorOutput, Suppression suppression, string code, string format, params string[] args)
        {
            if (SuppressionEngine.IsErrorSuppressed(suppression))
                return false;

            if (BaselineAllErrors)
            {
                SuppressionEngine.AddSuppression(suppression);
                return false;
            }

            TextWriter textWriter = errorOutput ? System.Console.Error : System.Console.Out;
            textWriter.WriteLine(code + ": " + string.Format(format, args));
            return true;
        }
    }
}
