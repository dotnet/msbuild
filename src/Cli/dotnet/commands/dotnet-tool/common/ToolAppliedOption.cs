// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Tool.Common
{
    internal class ToolAppliedOption
    {
        public static string[] GlobalOptionAliases = new string[] { "--global", "-g" };
        public static Option<bool> GlobalOption(string description) => new Option<bool>(GlobalOptionAliases, description);

        public static string LocalOptionAlias = "--local";
        public static Option<bool> LocalOption(string description) => new Option<bool>(LocalOptionAlias, description);

        public static string ToolPathOptionAlias = "--tool-path";
        public static Option<string> ToolPathOption(string description, string argumentName) => new Option<string>(ToolPathOptionAlias, description)
        {
            ArgumentHelpName = argumentName
        };

        public static string ToolManifestOptionAlias = "--tool-manifest";
        public static Option<string> ToolManifestOption(string description, string argumentName) => new Option<string>(ToolManifestOptionAlias, description)
        {
            ArgumentHelpName = argumentName,
            Arity = ArgumentArity.ZeroOrOne
        };

        internal static void EnsureNoConflictGlobalLocalToolPathOption(
            ParseResult parseResult,
            string message)
        {
            List<string> options = new List<string>();
            if (parseResult.HasOption(GlobalOptionAliases.First()))
            {
                options.Add(GlobalOptionAliases.First().Trim('-'));
            }

            if (parseResult.HasOption(LocalOptionAlias))
            {
                options.Add(LocalOptionAlias.Trim('-'));
            }

            if (!String.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(ToolPathOptionAlias)))
            {
                options.Add(ToolPathOptionAlias.Trim('-'));
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
                !string.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(ToolManifestOptionAlias)))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.OnlyLocalOptionSupportManifestFileOption));
            }
        }

        private static bool GlobalOrToolPath(ParseResult parseResult)
        {
            return parseResult.HasOption(GlobalOptionAliases.First()) ||
                   !string.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(ToolPathOptionAlias));
        }
    }
}
