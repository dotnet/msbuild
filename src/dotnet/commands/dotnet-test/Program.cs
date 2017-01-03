// Copyright(c) .NET Foundation and contributors.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var cmd = new CommandLineApplication(throwOnUnexpectedArg: false)
            {
                Name = "dotnet test",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.RunSettingsArgsHelpText
            };

            cmd.HelpOption("-h|--help");

            var argRoot = cmd.Argument(
                $"<{LocalizableStrings.CmdArgProject}>",
                LocalizableStrings.CmdArgDescription,
                multipleValues: false);

            var settingOption = cmd.Option(
                $"-s|--settings <{LocalizableStrings.CmdSettingsFile}>",
                LocalizableStrings.CmdSettingsDescription,
                CommandOptionType.SingleValue);

            var listTestsOption = cmd.Option(
                "-t|--list-tests",
                LocalizableStrings.CmdListTestsDescription,
                CommandOptionType.NoValue);

            var testCaseFilterOption = cmd.Option(
                $"--filter <{LocalizableStrings.CmdTestCaseFilterExpression}>",
                LocalizableStrings.CmdTestCaseFilterDescription,
                CommandOptionType.SingleValue);

            var testAdapterPathOption = cmd.Option(
                "-a|--test-adapter-path",
                LocalizableStrings.CmdTestAdapterPathDescription,
                CommandOptionType.SingleValue);

            var loggerOption = cmd.Option(
                $"-l|--logger <{LocalizableStrings.CmdLoggerOption}>",
                LocalizableStrings.CmdLoggerDescription,
                CommandOptionType.SingleValue);

            var configurationOption = cmd.Option(
                $"-c|--configuration <{LocalizableStrings.CmdConfiguration}>",
                LocalizableStrings.CmdConfigDescription,
                CommandOptionType.SingleValue);

            var frameworkOption = cmd.Option(
                $"-f|--framework <{LocalizableStrings.CmdFramework}>",
                LocalizableStrings.CmdFrameworkDescription,
                CommandOptionType.SingleValue);

            var outputOption = cmd.Option(
                $"-o|--output <{LocalizableStrings.CmdOutputDir}>",
                LocalizableStrings.CmdOutputDescription,
                CommandOptionType.SingleValue);

            var diagOption = cmd.Option(
                $"-d|--diag <{LocalizableStrings.CmdPathToLogFile}>",
                LocalizableStrings.CmdPathTologFileDescription,
                CommandOptionType.SingleValue);

            var noBuildtOption = cmd.Option(
               "--no-build",
               LocalizableStrings.CmdNoBuildDescription,
               CommandOptionType.NoValue);

            CommandOption verbosityOption = MSBuildForwardingApp.AddVerbosityOption(cmd);

            cmd.OnExecute(() =>
            {
                var msbuildArgs = new List<string>()
                {
                    "/t:VSTest"
                };

                msbuildArgs.Add("/nologo");

                if (settingOption.HasValue())
                {
                    msbuildArgs.Add($"/p:VSTestSetting={settingOption.Value()}");
                }

                if (listTestsOption.HasValue())
                {
                    msbuildArgs.Add($"/p:VSTestListTests=true");
                }

                if (testCaseFilterOption.HasValue())
                {
                    msbuildArgs.Add($"/p:VSTestTestCaseFilter={testCaseFilterOption.Value()}");
                }

                if (testAdapterPathOption.HasValue())
                {
                    msbuildArgs.Add($"/p:VSTestTestAdapterPath={testAdapterPathOption.Value()}");
                }

                if (loggerOption.HasValue())
                {
                    msbuildArgs.Add($"/p:VSTestLogger={string.Join(";", GetSemiColonEscapedArgs(loggerOption.Values))}");
                }

                if (configurationOption.HasValue())
                {
                    msbuildArgs.Add($"/p:Configuration={configurationOption.Value()}");
                }

                if (frameworkOption.HasValue())
                {
                    msbuildArgs.Add($"/p:TargetFramework={frameworkOption.Value()}");
                }

                if (outputOption.HasValue())
                {
                    msbuildArgs.Add($"/p:OutputPath={outputOption.Value()}");
                }

                if (diagOption.HasValue())
                {
                    msbuildArgs.Add($"/p:VSTestDiag={diagOption.Value()}");
                }

                if (noBuildtOption.HasValue())
                {
                    msbuildArgs.Add($"/p:VSTestNoBuild=true");
                }

                if (verbosityOption.HasValue())
                {
                    msbuildArgs.Add($"/verbosity:{verbosityOption.Value()}");
                }
                else
                {
                    msbuildArgs.Add("/verbosity:quiet");
                }

                string defaultproject = GetSingleTestProjectToRunTestIfNotProvided(argRoot.Value, cmd.RemainingArguments);

                if (!string.IsNullOrEmpty(defaultproject))
                {
                    msbuildArgs.Add(defaultproject);
                }

                if (!string.IsNullOrEmpty(argRoot.Value))
                {
                    msbuildArgs.Add(argRoot.Value);
                }

                // Get runsetings options specified after -- 
                if (cmd.RemainingArguments != null && cmd.RemainingArguments.Count > 0)
                {
                    var runSettingsOptions = GetRunSettingsOptions(cmd.RemainingArguments);
                    msbuildArgs.Add(string.Format("/p:VSTestCLIRunSettings=\"{0}\"", string.Join(";", runSettingsOptions)));
                }

                // Add remaining arguments that the parser did not understand,
                msbuildArgs.AddRange(cmd.RemainingArguments);

                return new MSBuildForwardingApp(msbuildArgs).Execute();
            });

            return cmd.Execute(args);
        }

        private static string GetSingleTestProjectToRunTestIfNotProvided(string args, List<string> remainingArguments)
        {
            string result = string.Empty;
            int projectFound = NumberOfTestProjectInRemainingArgs(remainingArguments) + NumberOfTestProjectInArgsRoot(args);

            if (projectFound > 1)
            {
                throw new GracefulException(
                $"Specify a single project file to run tests from.");
            }
            else if (projectFound == 0)
            {
                result = GetDefaultTestProject();
            }

            return result;
        }

        private static int NumberOfTestProjectInArgsRoot(string args)
        {
            Regex pattern = new Regex(@"^.*\..*proj$");

            if (!string.IsNullOrEmpty(args))
            {
                return pattern.IsMatch(args) ? 1 : 0;
            }

            return 0;
        }

        private static int NumberOfTestProjectInRemainingArgs(List<string> remainingArguments)
        {
            int count = 0;
            if (remainingArguments.Count != 0)
            {
                Regex pattern = new Regex(@"^.*\..*proj$");

                foreach (var x in remainingArguments)
                {
                    if (pattern.IsMatch(x))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static string GetDefaultTestProject()
        {
            string directory = Directory.GetCurrentDirectory();
            string[] projectFiles = Directory.GetFiles(directory, "*.*proj");

            if (projectFiles.Length == 0)
            {
                throw new GracefulException(
                    $"Couldn't find a project to run test from. Ensure a project exists in {directory}." + Environment.NewLine +
                    "Or pass the path to the project");
            }
            else if (projectFiles.Length > 1)
            {
                throw new GracefulException(
                    $"Specify which project file to use because this '{directory}' contains more than one project file.");
            }

            return projectFiles[0];
        }

        private static string[] GetRunSettingsOptions(List<string> remainingArgs)
        {
            List<string> runsettingsArgs = new List<string>();
            List<string> argsToRemove = new List<string>();

            bool readRunSettings = false;
            foreach (string arg in remainingArgs)
            {
                if (!readRunSettings)
                {
                    if (arg.Equals("--"))
                    {
                        readRunSettings = true;
                        argsToRemove.Add(arg);
                    }

                    continue;
                }

                runsettingsArgs.Add(GetSemiColonEsacpedstring(arg));
                argsToRemove.Add(arg);
            }

            foreach (string arg in argsToRemove)
            {
                remainingArgs.Remove(arg);
            }

            return runsettingsArgs.ToArray();
        }

        private static string GetSemiColonEsacpedstring(string arg)
        {
            return string.IsNullOrEmpty(arg) ? arg : arg.Replace(";", "%3b");
        }

        private static string[] GetSemiColonEscapedArgs(List<string> args)
        {
            int counter = 0;
            string[] array = new string[args.Count];

            foreach (string arg in args)
            {
                if (arg.IndexOf(";") != -1)
                {
                    array[counter] = arg.Replace(";", "%3b");
                }
                else
                {
                    array[counter] = arg;
                }

                counter++;
            }

            return array;
        }
    }
}
