// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

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

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            FeatureFlag.Instance.PrintFlagFeatureState();

            // We use also current process id for the correlation id for possible future usage in case we need to know the parent process
            // from the VSTest side.
            string testSessionCorrelationId = $"{Environment.ProcessId}_{Guid.NewGuid()}";

            string[] args = parseResult.GetArguments();

            if (VSTestTrace.TraceEnabled)
            {
                string commandLineParameters = "";
                if (args?.Length > 0)
                {
                    commandLineParameters = args.Aggregate((a, b) => $"{a} | {b}");
                }
                VSTestTrace.SafeWriteTrace(() => $"Argument list: '{commandLineParameters}'");
            }

            // settings parameters are after -- (including --), these should not be considered by the parser
            string[] settings = args.SkipWhile(a => a != "--").ToArray();
            // all parameters before --
            args = args.TakeWhile(a => a != "--").ToArray();

            // Fix for https://github.com/Microsoft/vstest/issues/1453
            // Run dll/exe directly using the VSTestForwardingApp
            if (ContainsBuiltTestSources(args))
            {
                return ForwardToVSTestConsole(parseResult, args, settings, testSessionCorrelationId);
            }

            return ForwardToMsbuild(parseResult, settings, testSessionCorrelationId);
        }

        private static int ForwardToMsbuild(ParseResult parseResult, string[] settings, string testSessionCorrelationId)
        {
            // Workaround for https://github.com/Microsoft/vstest/issues/1503
            const string NodeWindowEnvironmentName = "MSBUILDENSURESTDOUTFORTASKPROCESSES";
            string previousNodeWindowSetting = Environment.GetEnvironmentVariable(NodeWindowEnvironmentName);
            try
            {
                Environment.SetEnvironmentVariable(NodeWindowEnvironmentName, "1");
                int exitCode = FromParseResult(parseResult, settings, testSessionCorrelationId).Execute();

                // We run post processing also if execution is failed for possible partial successful result to post process.
                exitCode |= RunArtifactPostProcessingIfNeeded(testSessionCorrelationId, parseResult, FeatureFlag.Instance);

                return exitCode;
            }
            finally
            {
                Environment.SetEnvironmentVariable(NodeWindowEnvironmentName, previousNodeWindowSetting);
            }
        }

        private static int ForwardToVSTestConsole(ParseResult parseResult, string[] args, string[] settings, string testSessionCorrelationId)
        {
            List<string> convertedArgs = new VSTestArgumentConverter().Convert(args, out List<string> ignoredArgs);
            if (ignoredArgs.Any())
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.IgnoredArgumentsMessage, string.Join(" ", ignoredArgs)).Yellow());
            }

            // merge the args settings, we don't need to escape
            // one more time, there is no extra hop via msbuild
            convertedArgs.AddRange(settings);

            if (!FeatureFlag.Instance.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                // Add artifacts processing mode and test session id for the artifact post-processing
                convertedArgs.Add("--artifactsProcessingMode-collect");
                convertedArgs.Add($"--testSessionCorrelationId:{testSessionCorrelationId}");
            }

            int exitCode = new VSTestForwardingApp(convertedArgs).Execute();

            // We run post processing also if execution is failed for possible partial successful result to post process.
            exitCode |= RunArtifactPostProcessingIfNeeded(testSessionCorrelationId, parseResult, FeatureFlag.Instance);

            return exitCode;
        }

        private static TestCommand FromParseResult(ParseResult result, string[] settings, string testSessionCorrelationId, string msbuildPath = null)
        {
            result.ShowHelpOrErrorIfAppropriate();

            var msbuildArgs = new List<string>()
            {
                "-target:VSTest",
                "-nodereuse:false", // workaround for https://github.com/Microsoft/vstest/issues/1503
                "-nologo"
            };

            msbuildArgs.AddRange(result.OptionValuesToBeForwarded(TestCommandParser.GetCommand()));

            if (settings.Any())
            {
                //workaround for correct -- logic
                var commandArgument = result.GetValueForArgument(TestCommandParser.SlnOrProjectArgument);
                if(!string.IsNullOrWhiteSpace(commandArgument) && !settings.Contains(commandArgument))
                {
                    msbuildArgs.Add(result.GetValueForArgument(TestCommandParser.SlnOrProjectArgument));
                }

                // skip '--' and escape every \ to be \\ and every " to be \" to survive the next hop
                string[] escaped = settings.Skip(1).Select(s => s.Replace("\\", "\\\\").Replace("\"", "\\\"")).ToArray();

                string runSettingsArg = string.Join(";", escaped);
                msbuildArgs.Add($"-property:VSTestCLIRunSettings=\"{runSettingsArg}\"");
            }
            else
            {
                var argument = result.GetValueForArgument(TestCommandParser.SlnOrProjectArgument);
                if(!string.IsNullOrWhiteSpace(argument))
                    msbuildArgs.Add(argument);
            }

            string verbosityArg = result.ForwardedOptionValues<IReadOnlyCollection<string>>(TestCommandParser.GetCommand(), "verbosity")?.SingleOrDefault() ?? null;
            if (verbosityArg != null)
            {
                string[] verbosity = verbosityArg.Split(':', 2);
                if (verbosity.Length == 2)
                {
                    msbuildArgs.Add($"-property:VSTestVerbosity={verbosity[1]}");
                }
            }

            if (!FeatureFlag.Instance.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                // Add artifacts processing mode and test session id for the artifact post-processing
                msbuildArgs.Add("-property:VSTestArtifactsProcessingMode=collect");
                msbuildArgs.Add($"-property:VSTestSessionCorrelationId={testSessionCorrelationId}");
            }

            bool noRestore = result.HasOption(TestCommandParser.NoRestoreOption) || result.HasOption(TestCommandParser.NoBuildOption);

            TestCommand testCommand = new(
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
                VSTestTrace.SafeWriteTrace(() => $"Root variable set {rootVariableName}:{rootValue}");
            }

            VSTestTrace.SafeWriteTrace(() => $"Starting test using MSBuild with arguments '{testCommand.GetArgumentsToMSBuild()}' custom MSBuild path '{msbuildPath}' norestore '{noRestore}'");
            return testCommand;
        }

        internal static int RunArtifactPostProcessingIfNeeded(string testSessionCorrelationId, ParseResult parseResult, FeatureFlag disableFeatureFlag)
        {
            if (disableFeatureFlag.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                return 0;
            }

            // VSTest runner will save artifacts inside a temp folder if needed.
            string expectedArtifactDirectory = Path.Combine(Path.GetTempPath(), testSessionCorrelationId);
            if (!Directory.Exists(expectedArtifactDirectory))
            {
                VSTestTrace.SafeWriteTrace(() => "No artifact found, post-processing won't run.");
                return 0;
            }

            VSTestTrace.SafeWriteTrace(() => $"Artifacts directory found '{expectedArtifactDirectory}', running post-processing.");

            var artifactsPostProcessArgs = new List<string> { "--artifactsProcessingMode-postprocess", $"--testSessionCorrelationId:{testSessionCorrelationId}" };

            if (parseResult.HasOption(TestCommandParser.DiagOption))
            {
                artifactsPostProcessArgs.Add($"--diag:{parseResult.GetValueForOption(TestCommandParser.DiagOption)}");
            }

            try
            {
                return new VSTestForwardingApp(artifactsPostProcessArgs).Execute();
            }
            finally
            {
                if (Directory.Exists(expectedArtifactDirectory))
                {
                    VSTestTrace.SafeWriteTrace(() => $"Cleaning artifact directory '{expectedArtifactDirectory}'.");
                    try
                    {
                        Directory.Delete(expectedArtifactDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        VSTestTrace.SafeWriteTrace(() => $"Exception during artifact cleanup: \n{ex}");
                    }
                }
            }
        }

        private static bool ContainsBuiltTestSources(string[] args)
        {
            foreach (string arg in args)
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
            Option<IEnumerable<string>> option = TestCommandParser.EnvOption;

            if (!parseResult.HasOption(option))
            {
                return;
            }

            foreach (string env in parseResult.GetValueForOption(option))
            {
                string name = env;
                string value = string.Empty;

                int equalsIndex = env.IndexOf('=');
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
