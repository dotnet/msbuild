// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    internal static class ParseResultExtensions
    {
        internal static bool HasAppliedOption(this ParseResult parseResult, params string[] optionPath)
        {
            if (optionPath.Length == 0)
            {
                return false;
            }

            if (!parseResult.HasOption(optionPath[0]))
            {
                return false;
            }

            AppliedOption workingOptionPath = parseResult[optionPath[0]];

            for (int i = 1; i < optionPath.Length; i++)
            {
                if (!workingOptionPath.HasOption(optionPath[i]))
                {
                    return false;
                }

                workingOptionPath = workingOptionPath[optionPath[i]];
            }

            return true;
        }

        // If the argument path exists, return true.
        // If the argument has a value, set it to the out param.
        // This allows checking for args existence whose type is not known, which makes it safe to check for bool flags without values
        // in addition to checking for string value args.
        internal static bool TryGetArgumentValueAtPath(this ParseResult parseResult, out string argValue, params string[] optionsPath)
        {
            if (parseResult.TryTraversePath(out AppliedOption option, optionsPath))
            {
                if (option.Arguments.Count > 0)
                {
                    argValue = option.Arguments.ElementAt(0);
                }
                else
                {
                    argValue = null;
                }

                return true;
            }

            argValue = null;
            return false;
        }

        internal static string GetArgumentValueAtPath(this ParseResult parseResult, params string[] optionsPath)
        {
            if (parseResult.TryTraversePath(out AppliedOption option, optionsPath) && (option.Arguments.Count > 0))
            {
                return option.Arguments.ToList()[0];
            }

            return null;
        }

        internal static IReadOnlyCollection<string> GetArgumentListAtPath(this ParseResult parseResult, params string[] optionPath)
        {
            if (parseResult.TryTraversePath(out AppliedOption option, optionPath))
            {
                return option.Arguments;
            }

            return null;
        }

        private static bool TryTraversePath(this ParseResult parseResult, out AppliedOption option, params string[] optionPath)
        {
            if (optionPath.Length == 0)
            {
                option = null;
                return false;
            }

            if (!parseResult.HasOption(optionPath[0]))
            {
                option = null;
                return false;
            }

            AppliedOption workingOptionPath = parseResult[optionPath[0]];

            for (int i = 1; i < optionPath.Length; i++)
            {
                if (!workingOptionPath.HasOption(optionPath[i]))
                {
                    option = null;
                    return false;
                }

                workingOptionPath = workingOptionPath[optionPath[i]];
            }

            option = workingOptionPath;
            return true;
        }
    }
}
