// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Tools;
using System.CommandLine;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli
{
    internal static class CommonOptionsExtension
    {
        public static LoggerVerbosity ToLoggerVerbosity(this VerbosityOptions verbosityOptions)
        {
            LoggerVerbosity verbosity = Build.Framework.LoggerVerbosity.Normal;
            switch (verbosityOptions)
            {
                case VerbosityOptions.d:
                case VerbosityOptions.detailed:
                    verbosity = Build.Framework.LoggerVerbosity.Detailed;
                    break;
                case VerbosityOptions.diag:
                case VerbosityOptions.diagnostic:
                    verbosity = Build.Framework.LoggerVerbosity.Diagnostic;
                    break;
                case VerbosityOptions.m:
                case VerbosityOptions.minimal:
                    verbosity = Build.Framework.LoggerVerbosity.Minimal;
                    break;
                case VerbosityOptions.n:
                case VerbosityOptions.normal:
                    verbosity = Build.Framework.LoggerVerbosity.Normal;
                    break;
                case VerbosityOptions.q:
                case VerbosityOptions.quiet:
                    verbosity = Build.Framework.LoggerVerbosity.Quiet;
                    break;
            }


            return verbosity;
        }
    }
}
