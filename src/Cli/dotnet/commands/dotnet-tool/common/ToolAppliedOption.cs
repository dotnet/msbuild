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
        public static readonly Option GlobalOption = new Option(new string[] { "-g", "--global" }, Install.LocalizableStrings.GlobalOptionDescription);

        public static readonly Option LocalOption = new Option($"--local", Install.LocalizableStrings.LocalOptionDescription);

        public static readonly Option ToolPathOption = new Option($"--tool-path", Install.LocalizableStrings.ToolPathOptionDescription)
        {
            Argument = new Argument(Install.LocalizableStrings.ToolPathOptionName)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        };

        public static readonly Option ToolManifestOption = new Option($"--tool-manifest", Install.LocalizableStrings.ManifestPathOptionDescription)
        {
            Argument = new Argument(Install.LocalizableStrings.ManifestPathOptionName)
            {
                Arity = ArgumentArity.ZeroOrOne
            }
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

            if (!String.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(ToolPathOption)))
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
                !string.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(ToolManifestOption)))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.OnlyLocalOptionSupportManifestFileOption));
            }
        }

        private static bool GlobalOrToolPath(ParseResult parseResult)
        {
            return parseResult.HasOption(GlobalOption) ||
                   !string.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(ToolPathOption));
        }
    }
}
