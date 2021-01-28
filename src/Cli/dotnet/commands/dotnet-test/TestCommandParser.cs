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
        public static readonly Argument SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore,
        };

        public static readonly Option SettingsOption = new Option<string>(new string[] { "-s", "--settings" }, LocalizableStrings.CmdSettingsDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.CmdSettingsFile)
        }.ForwardAsSingle(o => $"-property:VSTestSetting={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option ListTestsOption = new Option<bool>(new string[] { "-t", "--list-tests" }, LocalizableStrings.CmdListTestsDescription)
              .ForwardAs("-property:VSTestListTests=true");

        public static readonly Option EnvOption = new Option<IEnumerable<string>>(new string[] { "-e", "--environment" }, LocalizableStrings.CmdEnvironmentVariableDescription)
        {
            Argument = new Argument<IEnumerable<string>>(LocalizableStrings.CmdEnvironmentVariableExpression)
            {
                Arity = ArgumentArity.OneOrMore
            }
        }.AllowSingleArgPerToken();

        public static readonly Option FilterOption = new Option<string>("--filter", LocalizableStrings.CmdTestCaseFilterDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.CmdTestCaseFilterExpression)
        }.ForwardAsSingle(o => $"-property:VSTestTestCaseFilter={o}");

        public static readonly Option AdapterOption = new Option<IEnumerable<string>>(new string[] { "-a", "--test-adapter-path" }, LocalizableStrings.CmdTestAdapterPathDescription)
        {
            Argument = new Argument<IEnumerable<string>>(LocalizableStrings.CmdTestAdapterPath)
            {
                Arity = ArgumentArity.OneOrMore
            }
        }.ForwardAsSingle(o => $"-property:VSTestTestAdapterPath=\"{string.Join(";", o.Select(CommandDirectoryContext.GetFullPath))}\"")
        .AllowSingleArgPerToken();

        public static readonly Option LoggerOption = new Option<IEnumerable<string>>(new string[] { "-l", "--logger" }, LocalizableStrings.CmdLoggerDescription)
        {
            Argument = new Argument<IEnumerable<string>>(LocalizableStrings.CmdLoggerOption)
            {
                Arity = ArgumentArity.OneOrMore
            }
        }.ForwardAsSingle(o => {
            var loggersString = string.Join(";", GetSemiColonEscapedArgs(o));

            return $"-property:VSTestLogger=\"{loggersString}\"";
        })
        .AllowSingleArgPerToken();

        public static readonly Option OutputOption = new Option<string>(new string[] { "-o", "--output" }, LocalizableStrings.CmdOutputDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.CmdOutputDir)
        }.ForwardAsSingle(o => $"-property:OutputPath={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option DiagOption = new Option<string>(new string[] { "-d", "--diag" }, LocalizableStrings.CmdPathTologFileDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.CmdPathToLogFile)
        }.ForwardAsSingle(o => $"-property:VSTestDiag={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option NoBuildOption = new Option<bool>("--no-build", LocalizableStrings.CmdNoBuildDescription)
            .ForwardAs("-property:VSTestNoBuild=true");

        public static readonly Option ResultsOption = new Option<string>(new string[] { "-r", "--results-directory" }, LocalizableStrings.CmdResultsDirectoryDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.CmdPathToResultsDirectory)
        }.ForwardAsSingle(o => $"-property:VSTestResultsDirectory={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option CollectOption = new Option<IEnumerable<string>>("--collect", LocalizableStrings.cmdCollectDescription)
        {
            Argument = new Argument<IEnumerable<string>>(LocalizableStrings.cmdCollectFriendlyName)
            {
                Arity = ArgumentArity.OneOrMore
            }
        }.ForwardAsSingle(o => $"-property:VSTestCollect=\"{string.Join(";", o)}\"")
        .AllowSingleArgPerToken();

        public static readonly Option BlameOption = new Option<bool>("--blame", LocalizableStrings.CmdBlameDescription)
            .ForwardAs("-property:VSTestBlame=true");

        public static readonly Option BlameCrashOption = new Option<bool>("--blame-crash", LocalizableStrings.CmdBlameCrashDescription)
            .ForwardAs("-property:VSTestBlameCrash=true");

        public static readonly Argument BlameCrashDumpArgument = new Argument<string>(LocalizableStrings.CrashDumpTypeArgumentName).FromAmong(new string[] { "full", "mini" });

        public static readonly Option BlameCrashDumpOption = new Option<string>("--blame-crash-dump-type", LocalizableStrings.CmdBlameCrashDumpTypeDescription)
            .ForwardAsMany(o => new[] { "-property:VSTestBlameCrash=true", $"-property:VSTestBlameCrashDumpType={o}" });

        public static readonly Option BlameCrashAlwaysOption = new Option<string>("--blame-crash-collect-always", LocalizableStrings.CmdBlameCrashCollectAlwaysDescription)
            .ForwardAsMany(o => new[] {"-property:VSTestBlameCrash=true", "-property:VSTestBlameCrashCollectAlways=true"});

        public static readonly Option BlameHangOption = new Option<bool>("--blame-hang", LocalizableStrings.CmdBlameHangDescription)
            .ForwardAs("-property:VSTestBlameHang=true");

        public static readonly Argument BlameHangDumpArgument = new Argument<string>(LocalizableStrings.HangDumpTypeArgumentName).FromAmong(new string[] { "full", "mini", "none" });

        public static readonly Option BlameHangDumpOption = new Option<string>("--blame-hang-dump-type", LocalizableStrings.CmdBlameHangDumpTypeDescription)
            .ForwardAsMany(o => new[] { "-property:VSTestBlameHang=true", $"-property:VSTestBlameHangDumpType={o}" });

        public static readonly Option BlameHangTimeoutOption = new Option<string>("--blame-hang-timeout", LocalizableStrings.CmdBlameHangTimeoutDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.HangTimeoutArgumentName)
        }.ForwardAsMany(o => new[] { "-property:VSTestBlameHang=true", $"-property:VSTestBlameHangTimeout={o}" });

        public static readonly Option NoLogoOption = new Option<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-property:VSTestNoLogo=nologo");

        public static readonly Option NoRestoreOption = CommonOptions.NoRestoreOption();

        public static Command GetCommand()
        {
            var command = new Command("test", LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);

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
    }
}
