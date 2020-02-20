// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Command = Microsoft.DotNet.Cli.CommandLine.Command;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Help
{
    public class HelpCommand
    {
        private readonly AppliedOption _appliedOption;

        public HelpCommand(AppliedOption appliedOption)
        {
            _appliedOption = appliedOption;
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var parser = Parser.Instance;
            var result = parser.ParseFrom("dotnet help", args);
            var helpAppliedOption = result["dotnet"]["help"];

            result.ShowHelpIfRequested();

            if (helpAppliedOption.Arguments.Any())
            {
                return new HelpCommand(helpAppliedOption).Execute();
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                psInfo = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start {docUrl}"
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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
                _appliedOption.Arguments.Single(),
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
                        _appliedOption.Arguments.Single()).Red());
                Reporter.Output.WriteLine(HelpUsageText.UsageText);
                return 1;
            }
        }
    }
}
