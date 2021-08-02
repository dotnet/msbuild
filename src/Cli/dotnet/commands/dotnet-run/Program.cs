// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.CommandLine.Parsing;
using System;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public static RunCommand FromArgs(string[] args)
        {
            var parseResult = Parser.Instance.ParseFrom("dotnet run", args);

            if (parseResult.HasOption("--help"))
            {
                parseResult.ShowHelp();
                throw new HelpException(string.Empty);
            }

            var project = parseResult.ValueForOption(RunCommandParser.ProjectOption);
            if (parseResult.UsingRunCommandShorthandProjectOption())
            {
                Console.WriteLine(LocalizableStrings.RunCommandProjectAbbreviationDeprecated.Yellow());
                project = parseResult.GetRunCommandShorthandProjectValues().FirstOrDefault();
            }

            var command = new RunCommand(
                configuration: parseResult.ValueForOption<string>(RunCommandParser.ConfigurationOption),
                framework: parseResult.ValueForOption<string>(RunCommandParser.FrameworkOption),
                runtime: parseResult.GetCommandLineRuntimeIdentifier(),
                noBuild: parseResult.HasOption(RunCommandParser.NoBuildOption),
                project: project,
                launchProfile: parseResult.ValueForOption<string>(RunCommandParser.LaunchProfileOption),
                noLaunchProfile: parseResult.HasOption(RunCommandParser.NoLaunchProfileOption),
                noRestore: parseResult.HasOption(RunCommandParser.NoRestoreOption) || parseResult.HasOption(RunCommandParser.NoBuildOption),
                interactive: parseResult.HasOption(RunCommandParser.InteractiveOption),
                restoreArgs: parseResult.OptionValuesToBeForwarded(RunCommandParser.GetCommand()),
                args: (parseResult.UnparsedTokens ?? Array.Empty<string>()).Concat(parseResult.UnmatchedTokens ?? Array.Empty<string>())
            );

            return command;
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return FromArgs(args).Execute();
        }
    }
}
