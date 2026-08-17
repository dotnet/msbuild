// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework.Logging
{
    internal static class LoggerParametersHelper
    {
        // Logger parameters delimiters.
        public static readonly char[] s_parameterDelimiters = MSBuildConstants.SemicolonChar;

        // Logger parameter value split character.
        public static readonly char[] s_parameterValueSplitCharacter = MSBuildConstants.EqualsChar;

        public static bool TryParseVerbosityParameter(string parameterValue, [NotNullWhen(true)] out LoggerVerbosity? verbosity)
        {
            switch (parameterValue.ToUpperInvariant())
            {
                case "Q":
                case "QUIET":
                    verbosity = LoggerVerbosity.Quiet;
                    return true;
                case "M":
                case "MINIMAL":
                    verbosity = LoggerVerbosity.Minimal;
                    return true;
                case "N":
                case "NORMAL":
                    verbosity = LoggerVerbosity.Normal;
                    return true;
                case "D":
                case "DETAILED":
                    verbosity = LoggerVerbosity.Detailed;
                    return true;
                case "DIAG":
                case "DIAGNOSTIC":
                    verbosity = LoggerVerbosity.Diagnostic;
                    return true;
                default:
                    verbosity = null;
                    return false;
            }
        }

        public static IEnumerable<Tuple<string, string?>> ParseParameters(string? parametersString)
        {
            if (parametersString is not null)
            {
                foreach (string parameter in parametersString.Split(s_parameterDelimiters))
                {
                    if (string.IsNullOrWhiteSpace(parameter))
                    {
                        continue;
                    }

                    string[] parameterAndValue = parameter.Split(s_parameterValueSplitCharacter);
                    yield return new Tuple<string, string?>(parameterAndValue[0], parameterAndValue.Length > 1 ? parameterAndValue[1] : null);
                }
            }
        }
    }
}
