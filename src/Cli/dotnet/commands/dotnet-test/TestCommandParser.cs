// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class TestCommandParser
    {
        public static readonly Argument SlnOrProjectArgument = new Argument(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore,
        };

        public static readonly Option SettingsOption = new Option(new string[] { "-s", "--settings" }, LocalizableStrings.CmdSettingsDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdSettingsFile)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"-property:VSTestSetting={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option ListTestsOption = new Option(new string[] { "-t", "--list-tests" }, LocalizableStrings.CmdListTestsDescription)
              .ForwardAs("-property:VSTestListTests=true");

        public static readonly Option EnvOption = new Option(new string[] { "-e", "--environment" }, LocalizableStrings.CmdEnvironmentVariableDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdEnvironmentVariableExpression)
            {
                Arity = ArgumentArity.OneOrMore
            }
        };

        public static readonly Option FilterOption = new Option("--filter", LocalizableStrings.CmdTestCaseFilterDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdTestCaseFilterExpression)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"-property:VSTestTestCaseFilter={o}");

        public static readonly Option AdapterOption = new Option(
              new string[] { "-a", "--test-adapter-path" },
              LocalizableStrings.CmdTestAdapterPathDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdTestAdapterPath)
            {
                Arity = ArgumentArity.OneOrMore
            }
        }.ForwardAsSingle<IReadOnlyCollection<string>>(o => $"-property:VSTestTestAdapterPath=\"{string.Join(";", o.Select(CommandDirectoryContext.GetFullPath))}\"");

        public static readonly Option LoggerOption = new Option(new string[] { "-l", "--logger" }, LocalizableStrings.CmdLoggerDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdLoggerOption)
            {
                Arity = ArgumentArity.OneOrMore
            }
        }.ForwardAsSingle<IReadOnlyCollection<string>>(o => {
            var loggersString = string.Join(";", GetSemiColonEscapedArgs(o));

            return $"-property:VSTestLogger=\"{loggersString}\"";
        });

        public static readonly Option OutputOption = new Option(new string[] { "-o", "--output" }, LocalizableStrings.CmdOutputDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdOutputDir)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"-property:OutputPath={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option DiagOption = new Option(new string[] { "-d", "--diag" }, LocalizableStrings.CmdPathTologFileDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdPathToLogFile)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"-property:VSTestDiag={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option NoBuildOption = new Option("--no-build", LocalizableStrings.CmdNoBuildDescription)
            .ForwardAs("-property:VSTestNoBuild=true");

        public static readonly Option ResultsOption = new Option(new string[] { "-r", "--results-directory" }, LocalizableStrings.CmdResultsDirectoryDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdPathToResultsDirectory)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"-property:VSTestResultsDirectory={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option CollectOption = new Option("--collect", LocalizableStrings.cmdCollectDescription)
        {
            Argument = new Argument(LocalizableStrings.cmdCollectFriendlyName)
            {
                Arity = ArgumentArity.OneOrMore
            }
        }.ForwardAsSingle<IReadOnlyCollection<string>>(o => $"-property:VSTestCollect=\"{string.Join(";", o)}\"");

        public static readonly Option BlameOption = new Option("--blame", LocalizableStrings.CmdBlameDescription)
            .ForwardAs("-property:VSTestBlame=true");

        public static readonly Option BlameCrashOption = new Option("--blame-crash", LocalizableStrings.CmdBlameCrashDescription)
            .ForwardAs("-property:VSTestBlameCrash=true");

        public static readonly Argument BlameCrashDumpArgument = new Argument(LocalizableStrings.CrashDumpTypeArgumentName).FromAmong(new string[] { "full", "mini" });

        public static readonly Option BlameCrashDumpOption = new Option("--blame-crash-dump-type", LocalizableStrings.CmdBlameCrashDumpTypeDescription)
            .ForwardAsMany<string>(o => new[] { "-property:VSTestBlameCrash=true", $"-property:VSTestBlameCrashDumpType={o}" });

        public static readonly Option BlameCrashAlwaysOption = new Option("--blame-crash-collect-always", LocalizableStrings.CmdBlameCrashCollectAlwaysDescription)
            .ForwardAsMany<string>(o => new[] {"-property:VSTestBlameCrash=true", "-property:VSTestBlameCrashCollectAlways=true"});

        public static readonly Option BlameHangOption = new Option("--blame-hang", LocalizableStrings.CmdBlameHangDescription)
            .ForwardAs("-property:VSTestBlameHang=true");

        public static readonly Argument BlameHangDumpArgument = new Argument(LocalizableStrings.HangDumpTypeArgumentName).FromAmong(new string[] { "full", "mini", "none" });

        public static readonly Option BlameHangDumpOption = new Option("--blame-hang-dump-type", LocalizableStrings.CmdBlameHangDumpTypeDescription)
        {
        }.ForwardAsMany<string>(o => new[] { "-property:VSTestBlameHang=true", $"-property:VSTestBlameHangDumpType={o}" });

        public static readonly Option BlameHangTimeoutOption = new Option("--blame-hang-timeout", LocalizableStrings.CmdBlameHangTimeoutDescription)
        {
            Argument = new Argument(LocalizableStrings.HangTimeoutArgumentName)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsMany<string>(o => new[] { "-property:VSTestBlameHang=true", $"-property:VSTestBlameHangTimeout={o}" });

        public static readonly Option NoLogoOption = new Option("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-property:VSTestNoLogo=nologo");

        public static readonly Option NoRestoreOption = CommonOptions.NoRestoreOption();

        public static Command GetCommand()
        {
            var command = new Command("test", LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);

            BlameCrashDumpArgument.SetDefaultValue("full");
            BlameCrashDumpOption.Argument = BlameCrashDumpArgument;

            BlameHangDumpArgument.SetDefaultValue("full");
            BlameHangDumpOption.Argument = BlameHangDumpArgument;

            command.AddOption(SettingsOption);
            command.AddOption(ListTestsOption);
            command.AddOption(EnvOption);
            command.AddOption(FilterOption);
            command.AddOption(AdapterOption);
            command.AddOption(LoggerOption);
            command.AddOption(OutputOption);
            command.AddOption(DiagOption);
            command.AddOption(NoBuildOption);
            command.AddOption(ResultsOption);
            command.AddOption(CollectOption);
            command.AddOption(BlameOption);
            command.AddOption(BlameCrashOption);
            command.AddOption(BlameCrashDumpOption);
            command.AddOption(BlameCrashAlwaysOption);
            command.AddOption(BlameHangOption);
            command.AddOption(BlameHangDumpOption);
            command.AddOption(BlameHangTimeoutOption);
            command.AddOption(NoLogoOption);
            command.AddOption(CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription));
            command.AddOption(CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription));
            command.AddOption(CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription, withShortOption: false));
            command.AddOption(NoRestoreOption);
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption());
            command.AddOption(CommonOptions.VerbosityOption());

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
