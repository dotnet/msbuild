// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Tool.Common
{
    internal class ToolAppliedOption
    {
        public static CliOption<bool> GlobalOption = new("--global", "-g");

        public static CliOption<bool> LocalOption = new("--local");

        public static CliOption<string> ToolPathOption = new("--tool-path")
        {
            HelpName = Install.LocalizableStrings.ToolPathOptionName
        };

        public static CliOption<string> ToolManifestOption = new("--tool-manifest")
        {
            HelpName = Install.LocalizableStrings.ManifestPathOptionName,
            Arity = ArgumentArity.ZeroOrOne
        };

        internal static void EnsureNoConflictGlobalLocalToolPathOption(
            ParseResult parseResult,
            string message)
        {
            List<string> options = new List<string>();
            if (parseResult.GetResult(GlobalOption) is not null)
            {
                options.Add(GlobalOption.Name);
            }

            if (parseResult.GetResult(LocalOption) is not null)
            {
                options.Add(LocalOption.Name);
            }

            if (!string.IsNullOrWhiteSpace(parseResult.GetValue(ToolPathOption)))
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
                !string.IsNullOrWhiteSpace(parseResult.GetValue(ToolManifestOption)))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.OnlyLocalOptionSupportManifestFileOption));
            }
        }

        private static bool GlobalOrToolPath(ParseResult parseResult)
        {
            return parseResult.GetResult(GlobalOption) is not null ||
                   !string.IsNullOrWhiteSpace(parseResult.GetValue(ToolPathOption));
        }
    }
}
