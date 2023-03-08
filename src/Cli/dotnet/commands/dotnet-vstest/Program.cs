// Copyright(c) .NET Foundation and contributors.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Microsoft.DotNet.Tools.VSTest
{
    public class VSTestCommand
    {
        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            VSTestForwardingApp vsTestforwardingApp = new VSTestForwardingApp(GetArgs(parseResult));

            return vsTestforwardingApp.Execute();
        }

        private static string[] GetArgs(ParseResult parseResult)
        {
            IEnumerable<string> args = parseResult.GetArguments();

            if (parseResult.HasOption(CommonOptions.TestLoggerOption))
            {
                // System command line might have mutated the options, reformat test logger option so vstest recognizes it
                var loggerValues = parseResult.GetValueForOption(CommonOptions.TestLoggerOption);
                var loggerArgs = loggerValues.Select(loggerValue => $"{CommonOptions.TestLoggerOption.Aliases.First()}:{loggerValue}");
                args = args.Where(a => !loggerValues.Contains(a) && !CommonOptions.TestLoggerOption.Aliases.Contains(a));
                args = loggerArgs.Concat(args);
            }

            return args.ToArray();
        }
    }
}
