// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Tool.Common
{
    internal class ToolAppliedOption
    {
        public static Option<bool> GlobalOption = new Option<bool>(new string[] { "--global", "-g" });

        public static Option<bool> LocalOption = new Option<bool>("--local");

        public static Option<string> ToolPathOption = new Option<string>("--tool-path")
        {
            ArgumentHelpName = Install.LocalizableStrings.ToolPathOptionName
        };

        public static Option<string> ToolManifestOption = new Option<string>("--tool-manifest")
        {
            ArgumentHelpName = Install.LocalizableStrings.ManifestPathOptionName,
            Arity = ArgumentArity.ZeroOrOne
        };

        internal static void EnsureNoConflictGlobalLocalToolPathOption(
            ParseResult parseResult,
            string message)
        {
            List<string> options = new List<string>();
            if (parseResult.HasOption(GlobalOption))
            {
                options.Add(GlobalOption.Name);
            }

            if (parseResult.HasOption(LocalOption))
            {
                options.Add(LocalOption.Name);
            }

            if (!String.IsNullOrWhiteSpace(parseResult.GetValueForOption(ToolPathOption)))
            {
                options.Add(ToolPathOption.Name);
            }

            if (options.Count > 1)
            {

                throw new GracefulException(
                    string.Format(
                        message,
                        string.Join(" ", options)));
            }
        }

        internal static void EnsureToolManifestAndOnlyLocalFlagCombination(ParseResult parseResult)
        {
            if (GlobalOrToolPath(parseResult) &&
                !string.IsNullOrWhiteSpace(parseResult.GetValueForOption(ToolManifestOption)))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.OnlyLocalOptionSupportManifestFileOption));
            }
        }

        private static bool GlobalOrToolPath(ParseResult parseResult)
        {
            return parseResult.HasOption(GlobalOption) ||
                   !string.IsNullOrWhiteSpace(parseResult.GetValueForOption(ToolPathOption));
        }
    }
}
