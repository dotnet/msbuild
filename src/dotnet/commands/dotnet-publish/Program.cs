// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Publish
{
    public partial class PublishCommand : MSBuildForwardingApp
    {
        private PublishCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
        }

        public static PublishCommand FromArgs(string[] args, string msbuildPath = null)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet publish";
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;
            app.HandleRemainingArguments = true;
            app.ArgumentSeparatorHelpText = HelpMessageStrings.MSBuildAdditionalArgsHelpText;
            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument($"<{LocalizableStrings.ProjectArgument}>",
                LocalizableStrings.ProjectArgDescription);

            CommandOption frameworkOption = app.Option(
                $"-f|--framework <{LocalizableStrings.FrameworkOption}>", LocalizableStrings.FrameworkOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption runtimeOption = app.Option(
                $"-r|--runtime <{LocalizableStrings.RuntimeOption}>", LocalizableStrings.RuntimeOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption outputOption = app.Option(
                $"-o|--output <{LocalizableStrings.OutputOption}>", LocalizableStrings.OutputOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption configurationOption = app.Option(
                $"-c|--configuration <{LocalizableStrings.ConfigurationOption}>", LocalizableStrings.ConfigurationOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption versionSuffixOption = app.Option(
               $"--version-suffix <{LocalizableStrings.VersionSuffixOption}>", LocalizableStrings.VersionSuffixOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption filterProjOption = app.Option(
               $"--filter <{LocalizableStrings.FilterProjOption}>", LocalizableStrings.FilterProjOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption verbosityOption = AddVerbosityOption(app);

            List<string> msbuildArgs = null;
            app.OnExecute(() =>
            {
                msbuildArgs = new List<string>();

                msbuildArgs.Add("/t:Publish");

                if (!string.IsNullOrEmpty(projectArgument.Value))
                {
                    msbuildArgs.Add(projectArgument.Value);
                }

                if (!string.IsNullOrEmpty(frameworkOption.Value()))
                {
                    msbuildArgs.Add($"/p:TargetFramework={frameworkOption.Value()}");
                }

                if (!string.IsNullOrEmpty(runtimeOption.Value()))
                {
                    msbuildArgs.Add($"/p:RuntimeIdentifier={runtimeOption.Value()}");
                }

                if (!string.IsNullOrEmpty(outputOption.Value()))
                {
                    msbuildArgs.Add($"/p:PublishDir={outputOption.Value()}");
                }

                if (!string.IsNullOrEmpty(configurationOption.Value()))
                {
                    msbuildArgs.Add($"/p:Configuration={configurationOption.Value()}");
                }

                if (!string.IsNullOrEmpty(versionSuffixOption.Value()))
                {
                    msbuildArgs.Add($"/p:VersionSuffix={versionSuffixOption.Value()}");
                }

                if (!string.IsNullOrEmpty(filterProjOption.Value()))
                {
                    msbuildArgs.Add($"/p:FilterProjectFiles={filterProjOption.Value()}");
                }

                if (!string.IsNullOrEmpty(verbosityOption.Value()))
                {
                    msbuildArgs.Add($"/verbosity:{verbosityOption.Value()}");
                }

                msbuildArgs.AddRange(app.RemainingArguments);

                return 0;
            });

            int exitCode = app.Execute(args);
            if (msbuildArgs == null)
            {
                throw new CommandCreationException(exitCode);
            }

            return new PublishCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            PublishCommand cmd;
            try
            {
                cmd = FromArgs(args);
            }
            catch (CommandCreationException e)
            {
                return e.ExitCode;
            }

            return cmd.Execute();
        }
    }
}
