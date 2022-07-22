// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.CommandLine;
using System.CommandLine.Parsing;
using System;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public static RunCommand FromArgs(string[] args)
        {
            var parseResult = Parser.Instance.ParseFrom("dotnet run", args);
            return FromParseResult(parseResult);
        }

        public static RunCommand FromParseResult(ParseResult parseResult)
        {
            var project = parseResult.GetValueForOption(RunCommandParser.ProjectOption);
            if (parseResult.UsingRunCommandShorthandProjectOption())
            {
                Reporter.Output.WriteLine(LocalizableStrings.RunCommandProjectAbbreviationDeprecated.Yellow());
                project = parseResult.GetRunCommandShorthandProjectValues().FirstOrDefault();
            }

            var command = new RunCommand(
                configuration: parseResult.GetValueForOption(RunCommandParser.ConfigurationOption),
                framework: parseResult.GetValueForOption(RunCommandParser.FrameworkOption),
                runtime: parseResult.GetCommandLineRuntimeIdentifier(),
                noBuild: parseResult.HasOption(RunCommandParser.NoBuildOption),
                project: project,
                launchProfile: parseResult.GetValueForOption(RunCommandParser.LaunchProfileOption),
                noLaunchProfile: parseResult.HasOption(RunCommandParser.NoLaunchProfileOption),
                noRestore: parseResult.HasOption(RunCommandParser.NoRestoreOption) || parseResult.HasOption(RunCommandParser.NoBuildOption),
                interactive: parseResult.HasOption(RunCommandParser.InteractiveOption),
                restoreArgs: parseResult.OptionValuesToBeForwarded(RunCommandParser.GetCommand()),
                args: parseResult.GetValueForArgument(RunCommandParser.ApplicationArguments)
            );

            return command;
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
