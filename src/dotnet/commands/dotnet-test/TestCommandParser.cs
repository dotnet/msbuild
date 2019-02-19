// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class TestCommandParser
    {
        public static Command Test() =>
            Create.Command(
                  "test",
                  LocalizableStrings.AppFullName,
                  Accept.ZeroOrMoreArguments()
                        .With(name: CommonLocalizableStrings.SolutionOrProjectArgumentName,
                              description: CommonLocalizableStrings.SolutionOrProjectArgumentDescription),
                  false,
                  CommonOptions.HelpOption(),
                  Create.Option(
                        "-s|--settings",
                        LocalizableStrings.CmdSettingsDescription,
                        Accept.ExactlyOneArgument()
                              .With(name: LocalizableStrings.CmdSettingsFile)
                              .ForwardAsSingle(o => $"-property:VSTestSetting={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                  Create.Option(
                        "-t|--list-tests",
                        LocalizableStrings.CmdListTestsDescription,
                        Accept.NoArguments()
                              .ForwardAsSingle(o => "-property:VSTestListTests=true")),
                  Create.Option(
                        "--filter",
                        LocalizableStrings.CmdTestCaseFilterDescription,
                        Accept.ExactlyOneArgument()
                              .With(name: LocalizableStrings.CmdTestCaseFilterExpression)
                              .ForwardAsSingle(o => $"-property:VSTestTestCaseFilter={o.Arguments.Single()}")),
                  Create.Option(
                        "-a|--test-adapter-path",
                        LocalizableStrings.CmdTestAdapterPathDescription,
                        Accept.OneOrMoreArguments()
                              .With(name: LocalizableStrings.CmdTestAdapterPath)
                              .ForwardAsSingle(o => $"-property:VSTestTestAdapterPath=\"{string.Join(";", o.Arguments.Select(CommandDirectoryContext.GetFullPath))}\"")),
                  Create.Option(
                        "-l|--logger",
                        LocalizableStrings.CmdLoggerDescription,
                        Accept.OneOrMoreArguments()
                              .With(name: LocalizableStrings.CmdLoggerOption)
                              .ForwardAsSingle(o =>
                                    {
                                          var loggersString = string.Join(";", GetSemiColonEscapedArgs(o.Arguments));

                                          return $"-property:VSTestLogger=\"{loggersString}\"";
                                    })),
                  CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription),
                  CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription),
                  CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription, withShortOption: false),
                  Create.Option(
                        "-o|--output",
                        LocalizableStrings.CmdOutputDescription,
                        Accept.ExactlyOneArgument()
                              .With(name: LocalizableStrings.CmdOutputDir)
                              .ForwardAsSingle(o => $"-property:OutputPath={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                  Create.Option(
                        "-d|--diag",
                        LocalizableStrings.CmdPathTologFileDescription,
                        Accept.ExactlyOneArgument()
                              .With(name: LocalizableStrings.CmdPathToLogFile)
                              .ForwardAsSingle(o => $"-property:VSTestDiag={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                  Create.Option(
                        "--no-build",
                        LocalizableStrings.CmdNoBuildDescription,
                        Accept.NoArguments()
                              .ForwardAsSingle(o => "-property:VSTestNoBuild=true")),
                  Create.Option(
                        "-r|--results-directory",
                        LocalizableStrings.CmdResultsDirectoryDescription,
                        Accept.ExactlyOneArgument()
                              .With(name: LocalizableStrings.CmdPathToResultsDirectory)
                              .ForwardAsSingle(o => $"-property:VSTestResultsDirectory={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                  Create.Option(
                        "--collect",
                        LocalizableStrings.cmdCollectDescription,
                        Accept.OneOrMoreArguments()
                              .With(name: LocalizableStrings.cmdCollectFriendlyName)
                              .ForwardAsSingle(o => $"-property:VSTestCollect=\"{string.Join(";", o.Arguments)}\"")),
                  Create.Option(
                        "--blame",
                        LocalizableStrings.CmdBlameDescription,
                        Accept.NoArguments()
                              .ForwardAsSingle(o => "-property:VSTestBlame=true")),
                  Create.Option(
                        "--nologo|/nologo",
                        LocalizableStrings.CmdNoLogo,
                        Accept.NoArguments()
                              .ForwardAsSingle(o => "-property:VSTestNoLogo=nologo")),
                  CommonOptions.NoRestoreOption(),
                  CommonOptions.InteractiveMsBuildForwardOption(),
                  CommonOptions.VerbosityOption());

        private static string GetSemiColonEscapedstring(string arg)
        {
            if (arg.IndexOf(";") != -1)
            {
                return arg.Replace(";", "%3b");
            }

            return arg;
        }

        private static string[] GetSemiColonEscapedArgs(IReadOnlyCollection<string> args)
        {
            int counter = 0;
            string[] array = new string[args.Count];

            foreach (string arg in args)
            {
                array[counter++] = GetSemiColonEscapedstring(arg);
            }

            return array;
        }
    }
}
