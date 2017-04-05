// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using static HelpUsageText;

namespace Microsoft.DotNet.Tools.Help
{
    public class HelpCommand
    {
        public static int Run(string[] args)
        {
            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet help";
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;

            CommandArgument commandNameArgument = app.Argument($"<{LocalizableStrings.CommandArgumentName}>", LocalizableStrings.CommandArgumentDescription);

            app.OnExecute(() => 
            {
                BuiltInCommandMetadata builtIn;
                if (BuiltInCommandsCatalog.Commands.TryGetValue(commandNameArgument.Value, out builtIn))
                {
                    var process = ConfigureProcess(builtIn.DocLink);
                    process.Start();
                    process.WaitForExit();
                }
                else
                {
                    Reporter.Error.WriteLine(String.Format(LocalizableStrings.CommandDoesNotExist, commandNameArgument.Value));
                    Reporter.Output.WriteLine(UsageText);
                    return 1;
                }
                return 0;
            });
            
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }
            else
            {
                return app.Execute(args);
            }
        }

        public static void PrintHelp()
        {
            PrintVersionHeader();
            Reporter.Output.WriteLine(UsageText);
        }

        public static void PrintVersionHeader()
        {
            var versionString = string.IsNullOrEmpty(Product.Version) ?
                string.Empty :
                $" ({Product.Version})";
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
    }
}
