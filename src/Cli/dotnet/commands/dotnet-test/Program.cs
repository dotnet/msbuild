// Copyright(c) .NET Foundation and contributors.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestCommand : RestoringCommand
    {
        public TestCommand(
            IEnumerable<string> msbuildArgs,
            IEnumerable<string> userDefinedArguments,
            IEnumerable<string> trailingArguments,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, userDefinedArguments, trailingArguments, noRestore, msbuildPath)
        {
        }

        public static TestCommand FromArgs(string[] args, string[] settings, string msbuildPath = null)
        {
            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet test", args);

            UpdateRunSettingsArgumentsText();
            result.ShowHelpOrErrorIfAppropriate();

            var parsedTest = result["dotnet"]["test"];

            var msbuildArgs = new List<string>()
            {
                "-target:VSTest",
                "-nodereuse:false", // workaround for https://github.com/Microsoft/vstest/issues/1503
                "-nologo"
            };

            msbuildArgs.AddRange(parsedTest.OptionValuesToBeForwarded());

            msbuildArgs.AddRange(parsedTest.Arguments);

            if (settings.Any())
            {
                // skip '--' and escape every \ to be \\ and every " to be \" to survive the next hop
                var escaped = settings.Skip(1).Select(s => s.Replace("\\", "\\\\").Replace("\"", "\\\"")).ToArray();

                var runSettingsArg = string.Join(";", escaped);
                msbuildArgs.Add($"-property:VSTestCLIRunSettings=\"{runSettingsArg}\"");
            }

            var verbosityArg = parsedTest.ForwardedOptionValues("verbosity").SingleOrDefault();
            if (verbosityArg != null)
            {
                var verbosity = verbosityArg.Split(':', 2);
                if (verbosity.Length == 2)
                {
                    msbuildArgs.Add($"-property:VSTestVerbosity={verbosity[1]}");
                }
            }

            bool noRestore = parsedTest.HasOption("--no-restore") || parsedTest.HasOption("--no-build");

            TestCommand testCommand = new TestCommand(
                msbuildArgs,
                parsedTest.OptionValuesToBeForwarded(),
                parsedTest.Arguments,
                noRestore,
                msbuildPath);

            var rootVariableName = Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)";
            if (Environment.GetEnvironmentVariable(rootVariableName) == null)
            {
                testCommand.EnvironmentVariable(rootVariableName, Path.GetDirectoryName(new Muxer().MuxerPath));
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

        private static string GetSemiColonEscapedString(string arg)
        {
            if (arg.IndexOf(";") != -1)
            {
                return arg.Replace(";", "%3b");
            }

            return arg;
        }

        private static string[] GetSemiColonEscapedArgs(List<string> args)
        {
            int counter = 0;
            string[] array = new string[args.Count];

            foreach (string arg in args)
            {
                array[counter++] = GetSemiColonEscapedString(arg);
            }

            return array;
        }

        private static void UpdateRunSettingsArgumentsText()
        {
            DefaultHelpViewText.Synopsis.AdditionalArguments = " [[--] <RunSettings arguments>...]]";
            DefaultHelpViewText.AdditionalArgumentsSection = LocalizableStrings.RunSettingsArgumentsDescription;
        }
    }
}
