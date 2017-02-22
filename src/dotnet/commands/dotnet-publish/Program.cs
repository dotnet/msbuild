// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Publish
{
    public partial class PublishCommand
    {
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

            CommandOption verbosityOption = MSBuildForwardingApp.AddVerbosityOption(app);

            var publish = new PublishCommand(msbuildPath);
            bool commandExecuted = false;
            app.OnExecute(() =>
            {
                commandExecuted = true;
                publish.ProjectPath = projectArgument.Value;
                publish.Framework = frameworkOption.Value();
                publish.Runtime = runtimeOption.Value();
                publish.OutputPath = outputOption.Value();
                publish.Configuration = configurationOption.Value();
                publish.VersionSuffix = versionSuffixOption.Value();
                publish.FilterProject = filterProjOption.Value();
                publish.Verbosity = verbosityOption.Value();
                publish.ExtraMSBuildArguments = app.RemainingArguments;

                return 0;
            });

            int exitCode = app.Execute(args);
            if (!commandExecuted)
            {
                throw new CommandCreationException(exitCode);
            }

            return publish;
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

        public ProcessStartInfo GetProcessStartInfo()
        {
            return CreateForwardingApp(_msbuildPath).GetProcessStartInfo();
        }

        public int Execute()
        {
            return GetProcessStartInfo().Execute();
        }
    }
}
