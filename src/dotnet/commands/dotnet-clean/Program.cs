// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Tools.Clean
{
    public class CleanCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false)
            {
                Name = "dotnet clean",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = HelpMessageStrings.MSBuildAdditionalArgsHelpText
            };
            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument(
                $"<{LocalizableStrings.CmdArgProject}>",
                LocalizableStrings.CmdArgProjDescription);

            CommandOption outputOption = app.Option(
                $"-o|--output <{LocalizableStrings.CmdOutputDir}>", 
                LocalizableStrings.CmdOutputDirDescription, 
                CommandOptionType.SingleValue);
            CommandOption frameworkOption = app.Option(
                $"-f|--framework <{LocalizableStrings.CmdFramework}>", 
                LocalizableStrings.CmdFrameworkDescription, 
                CommandOptionType.SingleValue);
            CommandOption configurationOption = app.Option(
                $"-c|--configuration <{LocalizableStrings.CmdConfiguration}>", 
                LocalizableStrings.CmdConfigurationDescription, 
                CommandOptionType.SingleValue);
            CommandOption verbosityOption = MSBuildForwardingApp.AddVerbosityOption(app);

            app.OnExecute(() =>
            {
                List<string> msbuildArgs = new List<string>();

                if (!string.IsNullOrEmpty(projectArgument.Value))
                {
                    msbuildArgs.Add(projectArgument.Value);
                }

                msbuildArgs.Add("/t:Clean");

                if (outputOption.HasValue())
                {
                    msbuildArgs.Add($"/p:OutputPath={outputOption.Value()}");
                }

                if (frameworkOption.HasValue())
                {
                    msbuildArgs.Add($"/p:TargetFramework={frameworkOption.Value()}");
                }

                if (configurationOption.HasValue())
                {
                    msbuildArgs.Add($"/p:Configuration={configurationOption.Value()}");
                }

                if (verbosityOption.HasValue())
                {
                    msbuildArgs.Add($"/verbosity:{verbosityOption.Value()}");
                }

                msbuildArgs.AddRange(app.RemainingArguments);

                return new MSBuildForwardingApp(msbuildArgs).Execute();
            });

            return app.Execute(args);
        }
    }
}
