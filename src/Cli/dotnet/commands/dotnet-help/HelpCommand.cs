// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Help
{
    public class HelpCommand
    {
        private readonly ParseResult _parseResult;

        public HelpCommand(ParseResult parseResult)
        {
            _parseResult = parseResult;
        }

        public static int Run(ParseResult result)
        {
            result.HandleDebugSwitch();

            result.ShowHelpOrErrorIfAppropriate();

            if (!string.IsNullOrEmpty(result.GetValueForArgument(HelpCommandParser.Argument)))
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
            if (TryGetDocsLink(
                _parseResult.GetValueForArgument(HelpCommandParser.Argument),
                out var docsLink) &&
                !string.IsNullOrEmpty(docsLink))
            {
                var process = ConfigureProcess(docsLink);
                process.Start();
                process.WaitForExit();
                return 0;
            }
            else
            {
                Reporter.Error.WriteLine(
                    string.Format(
                        LocalizableStrings.CommandDoesNotExist,
                        _parseResult.GetValueForArgument(HelpCommandParser.Argument)).Red());
                Reporter.Output.WriteLine(HelpUsageText.UsageText);
                return 1;
            }
        }

        private bool TryGetDocsLink(string commandName, out string docsLink)
        {
            var command = Cli.Parser.GetBuiltInCommand(commandName);
            if (command != null && command as DocumentedCommand != null)
            {
                docsLink = (command as DocumentedCommand).DocsLink;
                return true;
            }
            docsLink = null;
            return false;
        }
    }
}
