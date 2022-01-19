// Copyright(c) .NET Foundation and contributors.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestCommand : RestoringCommand
    {
        public TestCommand(
            IEnumerable<string> msbuildArgs,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, noRestore, msbuildPath)
        {
        }

        public static TestCommand FromArgs(string[] args, string[] settings, string msbuildPath = null)
        {
            var parser = Parser.Instance;
            var result = parser.ParseFrom("dotnet test", args);

            result.ShowHelpOrErrorIfAppropriate();

            var msbuildArgs = new List<string>()
            {
                "-target:VSTest",
                "-nodereuse:false", // workaround for https://github.com/Microsoft/vstest/issues/1503
                "-nologo"
            };

            msbuildArgs.AddRange(result.OptionValuesToBeForwarded(TestCommandParser.GetCommand()));

            msbuildArgs.AddRange(result.ValueForArgument<IEnumerable<string>>(TestCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>());

            if (settings.Any())
            {
                // skip '--' and escape every \ to be \\ and every " to be \" to survive the next hop
                var escaped = settings.Skip(1).Select(s => s.Replace("\\", "\\\\").Replace("\"", "\\\"")).ToArray();

                var runSettingsArg = string.Join(";", escaped);
                msbuildArgs.Add($"-property:VSTestCLIRunSettings=\"{runSettingsArg}\"");
            }

            var verbosityArg = result.ForwardedOptionValues<IReadOnlyCollection<string>>(TestCommandParser.GetCommand(), "verbosity")?.SingleOrDefault() ?? null;
            if (verbosityArg != null)
            {
                var verbosity = verbosityArg.Split(':', 2);
                if (verbosity.Length == 2)
                {
                    msbuildArgs.Add($"-property:VSTestVerbosity={verbosity[1]}");
                }
            }

            bool noRestore = result.HasOption(TestCommandParser.NoRestoreOption) || result.HasOption(TestCommandParser.NoBuildOption);

            TestCommand testCommand = new TestCommand(
                msbuildArgs,
                noRestore,
                msbuildPath);

            // Apply environment variables provided by the user via --environment (-e) parameter, if present
            SetEnvironmentVariablesFromParameters(testCommand, result);

            // Set DOTNET_PATH if it isn't already set in the environment as it is required
            // by the testhost which uses the apphost feature (Windows only).
            (bool hasRootVariable, string rootVariableName, string rootValue) = VSTestForwardingApp.GetRootVariable();
            if (!hasRootVariable)
            {
                testCommand.EnvironmentVariable(rootVariableName, rootValue);
            }

            return testCommand;
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            // settings parameters are after -- (including --), these should not be considered by the parser
            var settings = args.SkipWhile(a => a != "--").ToArray();
            // all parameters before --
            args = args.TakeWhile(a => a != "--").ToArray();

            // Fix for https://github.com/Microsoft/vstest/issues/1453
            // Try to run dll/exe directly using the VSTestForwardingApp
            if (ContainsBuiltTestSources(args))
            {
                var convertedArgs = new VSTestArgumentConverter().Convert(args, out var ignoredArgs);
                if (ignoredArgs.Any())
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.IgnoredArgumentsMessage, string.Join(" ", ignoredArgs)).Yellow());
                }

                // merge the args settings, we don't need to escape
                // one more time, there is no extra hop via msbuild
                convertedArgs.AddRange(settings);

                return new VSTestForwardingApp(convertedArgs).Execute();
            }

            // Workaround for https://github.com/Microsoft/vstest/issues/1503
            const string NodeWindowEnvironmentName = "MSBUILDENSURESTDOUTFORTASKPROCESSES";
            string previousNodeWindowSetting = Environment.GetEnvironmentVariable(NodeWindowEnvironmentName);

            try
            {
                Environment.SetEnvironmentVariable(NodeWindowEnvironmentName, "1");
                return FromArgs(args, settings).Execute();
            }
            finally
            {
                Environment.SetEnvironmentVariable(NodeWindowEnvironmentName, previousNodeWindowSetting);
            }
        }

        private static bool ContainsBuiltTestSources(string[] args)
        {
            foreach (var arg in args)
            {
                if (!arg.StartsWith("-") &&
                    (arg.EndsWith("dll", StringComparison.OrdinalIgnoreCase) || arg.EndsWith("exe", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }

        private static void SetEnvironmentVariablesFromParameters(TestCommand testCommand, ParseResult parseResult)
        {
            var option = TestCommandParser.EnvOption;

            if (!parseResult.HasOption(option))
            {
                return;
            }

            foreach (var env in parseResult.ValueForOption<IEnumerable<string>>(option))
            {
                var name = env;
                var value = string.Empty;

                var equalsIndex = env.IndexOf('=');
                if (equalsIndex > 0)
                {
                    name = env.Substring(0, equalsIndex);
                    value = env.Substring(equalsIndex + 1);
                }

                testCommand.EnvironmentVariable(name, value);
            }
        }
    }
}
