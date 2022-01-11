// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Help
{
    public class HelpCommand
    {
        private readonly ParseResult _parseResult;

        public HelpCommand(ParseResult parseResult)
        {
            _parseResult = parseResult;
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var parser = Parser.Instance;
            var result = parser.ParseFrom("dotnet help", args);

            result.ShowHelpOrErrorIfAppropriate();

            if (!string.IsNullOrEmpty(result.ValueForArgument<string>(HelpCommandParser.Argument)))
            {
                return new HelpCommand(result).Execute();
            }

            PrintHelp();
            return 0;
        }

        public static void PrintHelp()
        {
            PrintVersionHeader();
            Reporter.Output.WriteLine(HelpUsageText.UsageText);
        }

        public static void PrintVersionHeader()
        {
            var versionString = string.IsNullOrEmpty(Product.Version) ? string.Empty : $" ({Product.Version})";
            Reporter.Output.WriteLine(Product.LongName + versionString);
        }

        public static Process ConfigureProcess(string docUrl)
        {
            ProcessStartInfo psInfo;
            if (OperatingSystem.IsWindows())
            {
                psInfo = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start {docUrl}"
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                psInfo = new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = docUrl
                };
            }
            else
            {
                psInfo = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = docUrl
                };
            }

            return new Process
            {
                StartInfo = psInfo
            };
        }

        public int Execute()
        {
            if (BuiltInCommandsCatalog.Commands.TryGetValue(
                _parseResult.ValueForArgument<string>(HelpCommandParser.Argument),
                out BuiltInCommandMetadata builtIn) &&
                !string.IsNullOrEmpty(builtIn.DocLink))
            {
                var process = ConfigureProcess(builtIn.DocLink);
                process.Start();
                process.WaitForExit();
                return 0;
            }
            else
            {
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.CommandDoesNotExist,
                        _parseResult.ValueForArgument<string>(HelpCommandParser.Argument)).Red());
                Reporter.Output.WriteLine(HelpUsageText.UsageText);
                return 1;
            }
        }
    }
}
