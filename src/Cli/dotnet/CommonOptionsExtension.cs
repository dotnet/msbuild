// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Cli
{
    internal static class CommonOptionsExtension
    {
        public static LoggerVerbosity ToLoggerVerbosity(this VerbosityOptions verbosityOptions)
        {
            LoggerVerbosity verbosity = LoggerVerbosity.Normal;
            switch (verbosityOptions)
            {
                case VerbosityOptions.d:
                case VerbosityOptions.detailed:
                    verbosity = LoggerVerbosity.Detailed;
                    break;
                case VerbosityOptions.diag:
                case VerbosityOptions.diagnostic:
                    verbosity = LoggerVerbosity.Diagnostic;
                    break;
                case VerbosityOptions.m:
                case VerbosityOptions.minimal:
                    verbosity = LoggerVerbosity.Minimal;
                    break;
                case VerbosityOptions.n:
                case VerbosityOptions.normal:
                    verbosity = LoggerVerbosity.Normal;
                    break;
                case VerbosityOptions.q:
                case VerbosityOptions.quiet:
                    verbosity = LoggerVerbosity.Quiet;
                    break;
            }
            return verbosity;
        }

        public static bool IsDetailedOrDiagnostic(this VerbosityOptions verbosity)
        {
            return verbosity.Equals(VerbosityOptions.diag) ||
                verbosity.Equals(VerbosityOptions.diagnostic) ||
                verbosity.Equals(VerbosityOptions.d) ||
                verbosity.Equals(VerbosityOptions.detailed);
        }

        public static bool IsQuiet(this VerbosityOptions verbosity)
        {
            return verbosity.Equals(VerbosityOptions.q) ||
                verbosity.Equals(VerbosityOptions.quiet);
        }
        public static bool IsMinimal(this VerbosityOptions verbosity)
        {
            return verbosity.Equals(VerbosityOptions.m) ||
                verbosity.Equals(VerbosityOptions.minimal);
        }
        public static bool IsNormal(this VerbosityOptions verbosity)
        {
            return verbosity.Equals(VerbosityOptions.normal) ||
                verbosity.Equals(VerbosityOptions.n);
        }

        /// <summary>
        /// Converts <see cref="VerbosityOptions"/> to Microsoft.Extensions.Logging.<see cref="LogLevel"/>.
        /// </summary>
        public static LogLevel ToLogLevel(this VerbosityOptions verbosityOptions)
        {
            LogLevel logLevel = LogLevel.Information;
            switch (verbosityOptions)
            {
                case VerbosityOptions.d:
                case VerbosityOptions.detailed:
                    logLevel = LogLevel.Debug;
                    break;
                case VerbosityOptions.diag:
                case VerbosityOptions.diagnostic:
                    logLevel = LogLevel.Trace;
                    break;
                case VerbosityOptions.m:
                case VerbosityOptions.minimal:
                    logLevel = LogLevel.Error;
                    break;
                case VerbosityOptions.n:
                case VerbosityOptions.normal:
                    logLevel = LogLevel.Information;
                    break;
                case VerbosityOptions.q:
                case VerbosityOptions.quiet:
                    logLevel = LogLevel.None;
                    break;
            }
            return logLevel;
        }
    }
}
