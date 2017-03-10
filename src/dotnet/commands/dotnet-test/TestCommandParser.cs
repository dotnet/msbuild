using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class TestCommandParser
    {
        public static Command Test() =>
            Create.Command(
                  "test",
                  LocalizableStrings.AppFullName,
                  Accept.ZeroOrMoreArguments,
                  CommonOptions.HelpOption(),
                  Create.Option(
                        "-s|--settings",
                        LocalizableStrings.CmdSettingsDescription,
                        Accept.ExactlyOneArgument
                              .With(name: LocalizableStrings.CmdSettingsFile)
                              .ForwardAs(o => $"/p:VSTestSetting={o.Arguments.Single()}")),
                  Create.Option(
                        "-t|--list-tests",
                        LocalizableStrings.CmdListTestsDescription,
                        Accept.NoArguments
                              .ForwardAs(o => "/p:VSTestListTests=true")),
                  Create.Option(
                        "--filter",
                        LocalizableStrings.CmdTestCaseFilterDescription,
                        Accept.ExactlyOneArgument
                              .With(name: LocalizableStrings.CmdTestCaseFilterExpression)
                              .ForwardAs(o => $"/p:VSTestTestCaseFilter={o.Arguments.Single()}")),
                  Create.Option(
                        "-a|--test-adapter-path",
                        LocalizableStrings.CmdTestAdapterPathDescription,
                        Accept.ExactlyOneArgument
                              .With(name: LocalizableStrings.CmdTestAdapterPath)
                              .ForwardAs(o => $"/p:VSTestTestAdapterPath={o.Arguments.Single()}")),
                  Create.Option(
                        "-l|--logger",
                        LocalizableStrings.CmdLoggerDescription,
                        Accept.ExactlyOneArgument
                              .With(name: LocalizableStrings.CmdLoggerOption)
                              .ForwardAs(o => 
                                    {
                                          var loggersString = string.Join(";", GetSemiColonEscapedArgs(o.Arguments)); 
                                          
                                          return $"/p:VSTestLogger={loggersString}";
                                    })),
                  CommonOptions.ConfigurationOption(),
                  CommonOptions.FrameworkOption(),
                  Create.Option(
                        "-o|--output",
                        LocalizableStrings.CmdOutputDescription,
                        Accept.ExactlyOneArgument
                              .With(name: LocalizableStrings.CmdOutputDir)
                              .ForwardAs(o => $"/p:OutputPath={o.Arguments.Single()}")),
                  Create.Option(
                        "-d|--diag",
                        LocalizableStrings.CmdPathTologFileDescription,
                        Accept.ExactlyOneArgument
                              .With(name: LocalizableStrings.CmdPathToLogFile)
                              .ForwardAs(o => $"/p:VSTestDiag={o.Arguments.Single()}")),
                  Create.Option(
                        "--no-build",
                        LocalizableStrings.CmdNoBuildDescription,
                        Accept.NoArguments
                              .ForwardAs(o => "/p:VSTestNoBuild=true")),
                  CommonOptions.VerbosityOption());

        private static string GetSemiColonEsacpedstring(string arg)
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
                array[counter++] = GetSemiColonEsacpedstring(arg);
            }

            return array;
        }
    }
}