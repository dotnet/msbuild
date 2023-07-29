// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Test;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class TestCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-test";

        public static readonly CliOption<string> SettingsOption = new ForwardedOption<string>("--settings", "-s")
        {
            Description = LocalizableStrings.CmdSettingsDescription,
            HelpName = LocalizableStrings.CmdSettingsFile
        }.ForwardAsSingle(o => $"-property:VSTestSetting={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public static readonly CliOption<bool> ListTestsOption = new ForwardedOption<bool>("--list-tests", "-t")
        {
            Description = LocalizableStrings.CmdListTestsDescription
        }.ForwardAs("-property:VSTestListTests=true");

        public static readonly CliOption<IEnumerable<string>> EnvOption = new CliOption<IEnumerable<string>>("--environment", "-e")
        {
            Description = LocalizableStrings.CmdEnvironmentVariableDescription,
            HelpName = LocalizableStrings.CmdEnvironmentVariableExpression
        }.AllowSingleArgPerToken();

        public static readonly CliOption<string> FilterOption = new ForwardedOption<string>("--filter")
        {
            Description = LocalizableStrings.CmdTestCaseFilterDescription,
            HelpName = LocalizableStrings.CmdTestCaseFilterExpression
        }.ForwardAsSingle(o => $"-property:VSTestTestCaseFilter={SurroundWithDoubleQuotes(o)}");

        public static readonly CliOption<IEnumerable<string>> AdapterOption = new ForwardedOption<IEnumerable<string>>("--test-adapter-path")
        {
            Description = LocalizableStrings.CmdTestAdapterPathDescription,
            HelpName = LocalizableStrings.CmdTestAdapterPath
        }.ForwardAsSingle(o => $"-property:VSTestTestAdapterPath={SurroundWithDoubleQuotes(string.Join(";", o.Select(CommandDirectoryContext.GetFullPath)))}")
        .AllowSingleArgPerToken();

        public static readonly CliOption<IEnumerable<string>> LoggerOption = new ForwardedOption<IEnumerable<string>>("--logger", "-l")
        {
            Description = LocalizableStrings.CmdLoggerDescription,
            HelpName = LocalizableStrings.CmdLoggerOption
        }.ForwardAsSingle(o =>
        {
            var loggersString = string.Join(";", GetSemiColonEscapedArgs(o));

            return $"-property:VSTestLogger={SurroundWithDoubleQuotes(loggersString)}";
        })
        .AllowSingleArgPerToken();

        public static readonly CliOption<string> OutputOption = new ForwardedOption<string>("--output", "-o")
        {
            Description = LocalizableStrings.CmdOutputDescription,
            HelpName = LocalizableStrings.CmdOutputDir
        }
        .ForwardAsOutputPath("OutputPath", true);

        public static readonly CliOption<string> DiagOption = new ForwardedOption<string>("--diag", "-d")
        {
            Description = LocalizableStrings.CmdPathTologFileDescription,
            HelpName = LocalizableStrings.CmdPathToLogFile
        }
        .ForwardAsSingle(o => $"-property:VSTestDiag={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public static readonly CliOption<bool> NoBuildOption = new ForwardedOption<bool>("--no-build")
        {
            Description = LocalizableStrings.CmdNoBuildDescription
        }.ForwardAs("-property:VSTestNoBuild=true");

        public static readonly CliOption<string> ResultsOption = new ForwardedOption<string>("--results-directory")
        {
            Description = LocalizableStrings.CmdResultsDirectoryDescription,
            HelpName = LocalizableStrings.CmdPathToResultsDirectory
        }.ForwardAsSingle(o => $"-property:VSTestResultsDirectory={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public static readonly CliOption<IEnumerable<string>> CollectOption = new ForwardedOption<IEnumerable<string>>("--collect")
        {
            Description = LocalizableStrings.cmdCollectDescription,
            HelpName = LocalizableStrings.cmdCollectFriendlyName
        }.ForwardAsSingle(o => $"-property:VSTestCollect=\"{string.Join(";", GetSemiColonEscapedArgs(o))}\"")
        .AllowSingleArgPerToken();

        public static readonly CliOption<bool> BlameOption = new ForwardedOption<bool>("--blame")
        {
            Description = LocalizableStrings.CmdBlameDescription
        }.ForwardAs("-property:VSTestBlame=true");

        public static readonly CliOption<bool> BlameCrashOption = new ForwardedOption<bool>("--blame-crash")
        {
            Description = LocalizableStrings.CmdBlameCrashDescription
        }.ForwardAs("-property:VSTestBlameCrash=true");

        public static readonly CliOption<string> BlameCrashDumpOption = CreateBlameCrashDumpOption();

        private static CliOption<string> CreateBlameCrashDumpOption()
        {
            CliOption<string> result = new ForwardedOption<string>("--blame-crash-dump-type")
            {
                Description = LocalizableStrings.CmdBlameCrashDumpTypeDescription,
                HelpName = LocalizableStrings.CrashDumpTypeArgumentName,
            }
            .ForwardAsMany(o => new[] { "-property:VSTestBlameCrash=true", $"-property:VSTestBlameCrashDumpType={o}" });
            result.AcceptOnlyFromAmong(new string[] { "full", "mini" });
            return result;
        }

        public static readonly CliOption<bool> BlameCrashAlwaysOption = new ForwardedOption<bool>("--blame-crash-collect-always")
        {
            Description = LocalizableStrings.CmdBlameCrashCollectAlwaysDescription
        }.ForwardAsMany(o => new[] { "-property:VSTestBlameCrash=true", "-property:VSTestBlameCrashCollectAlways=true" });

        public static readonly CliOption<bool> BlameHangOption = new ForwardedOption<bool>("--blame-hang")
        {
            Description = LocalizableStrings.CmdBlameHangDescription
        }.ForwardAs("-property:VSTestBlameHang=true");

        public static readonly CliOption<string> BlameHangDumpOption = CreateBlameHangDumpOption();

        private static CliOption<string> CreateBlameHangDumpOption()
        {
            CliOption<string> result = new ForwardedOption<string>("--blame-hang-dump-type")
            {
                Description = LocalizableStrings.CmdBlameHangDumpTypeDescription,
                HelpName = LocalizableStrings.HangDumpTypeArgumentName
            }
            .ForwardAsMany(o => new[] { "-property:VSTestBlameHang=true", $"-property:VSTestBlameHangDumpType={o}" });
            result.AcceptOnlyFromAmong(new string[] { "full", "mini", "none" });
            return result;
        }

        public static readonly CliOption<string> BlameHangTimeoutOption = new ForwardedOption<string>("--blame-hang-timeout")
        {
            Description = LocalizableStrings.CmdBlameHangTimeoutDescription,
            HelpName = LocalizableStrings.HangTimeoutArgumentName
        }.ForwardAsMany(o => new[] { "-property:VSTestBlameHang=true", $"-property:VSTestBlameHangTimeout={o}" });

        public static readonly CliOption<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
        {
            Description = LocalizableStrings.CmdNoLogo
        }.ForwardAs("-property:VSTestNoLogo=nologo");

        public static readonly CliOption<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly CliOption<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly CliOption ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("test", DocsLink, LocalizableStrings.AppFullName);
            command.TreatUnmatchedTokensAsErrors = false;

            // We are on purpose not capturing the solution, project or directory here. We want to pass it to the
            // MSBuild command so we are letting it flow.

            command.Options.Add(SettingsOption);
            command.Options.Add(ListTestsOption);
            command.Options.Add(EnvOption);
            command.Options.Add(FilterOption);
            command.Options.Add(AdapterOption);
            command.Options.Add(LoggerOption);
            command.Options.Add(OutputOption);
            command.Options.Add(CommonOptions.ArtifactsPathOption);
            command.Options.Add(DiagOption);
            command.Options.Add(NoBuildOption);
            command.Options.Add(ResultsOption);
            command.Options.Add(CollectOption);
            command.Options.Add(BlameOption);
            command.Options.Add(BlameCrashOption);
            command.Options.Add(BlameCrashDumpOption);
            command.Options.Add(BlameCrashAlwaysOption);
            command.Options.Add(BlameHangOption);
            command.Options.Add(BlameHangDumpOption);
            command.Options.Add(BlameHangTimeoutOption);
            command.Options.Add(NoLogoOption);
            command.Options.Add(ConfigurationOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(CommonOptions.RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.Options.Add(NoRestoreOption);
            command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(CommonOptions.ArchitectureOption);
            command.Options.Add(CommonOptions.OperatingSystemOption);
            command.Options.Add(CommonOptions.DisableBuildServersOption);

            command.SetAction(TestCommand.Run);

            return command;
        }

        private static string GetSemiColonEscapedstring(string arg)
        {
            if (arg.IndexOf(";") != -1)
            {
                return arg.Replace(";", "%3b");
            }

            return arg;
        }

        private static string[] GetSemiColonEscapedArgs(IEnumerable<string> args)
        {
            int counter = 0;
            string[] array = new string[args.Count()];

            foreach (string arg in args)
            {
                array[counter++] = GetSemiColonEscapedstring(arg);
            }

            return array;
        }

        /// <summary>
        /// Adding double quotes around the property helps MSBuild arguments parser and avoid incorrect splits on ',' or ';'.
        /// </summary>
        internal /* for testing purposes */ static string SurroundWithDoubleQuotes(string input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            // If already escaped by double quotes then return original string.
            if (input.StartsWith("\"", StringComparison.Ordinal)
                && input.EndsWith("\"", StringComparison.Ordinal))
            {
                return input;
            }

            // We want to count the number of trailing backslashes to ensure
            // we will have an even number before adding the final double quote.
            // Otherwise the last \" will be interpreted as escaping the double
            // quote rather than a backslash and a double quote.
            var trailingBackslashesCount = 0;
            for (int i = input.Length - 1; i >= 0; i--)
            {
                if (input[i] == '\\')
                {
                    trailingBackslashesCount++;
                }
                else
                {
                    break;
                }
            }

            return trailingBackslashesCount % 2 == 0
                ? string.Concat("\"", input, "\"")
                : string.Concat("\"", input, "\\\"");
        }
    }
}
