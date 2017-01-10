// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Tools.Publish
{
    public partial class PublishCommand
    {
        public static int Run(string[] args)
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
            CommandOption verbosityOption = MSBuildForwardingApp.AddVerbosityOption(app);

            app.OnExecute(() =>
            {
                var publish = new PublishCommand();

                publish.ProjectPath = projectArgument.Value;
                publish.Framework = frameworkOption.Value();
                publish.Runtime = runtimeOption.Value();
                publish.OutputPath = outputOption.Value();
                publish.Configuration = configurationOption.Value();
                publish.VersionSuffix = versionSuffixOption.Value();
                publish.Verbosity = verbosityOption.Value();
                publish.ExtraMSBuildArguments = app.RemainingArguments;

                return publish.Execute();
            });

            return app.Execute(args);
        }
    }
}
