// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Tools.Build
{
    public class BuildCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet build";
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;
            app.AllowArgumentSeparator = true;
            app.ArgumentSeparatorHelpText = HelpMessageStrings.MSBuildAdditionalArgsHelpText;
            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument($"<{LocalizableStrings.ProjectArgumentValueName}>", LocalizableStrings.ProjectArgumentDescription);

            CommandOption outputOption = app.Option($"-o|--output <{LocalizableStrings.OutputOptionName}>", LocalizableStrings.OutputOptionDescription, CommandOptionType.SingleValue);
            CommandOption frameworkOption = app.Option($"-f|--framework <{LocalizableStrings.FrameworkOptionName}>", LocalizableStrings.FrameworkOptionDescription, CommandOptionType.SingleValue);
            CommandOption runtimeOption = app.Option(
                $"-r|--runtime <{LocalizableStrings.RuntimeOptionName}>", LocalizableStrings.RuntimeOptionDescription,
                CommandOptionType.SingleValue);
            CommandOption configurationOption = app.Option($"-c|--configuration <{LocalizableStrings.ConfigurationOptionName}>", LocalizableStrings.FrameworkOptionDescription, CommandOptionType.SingleValue);
            CommandOption versionSuffixOption = app.Option($"--version-suffix <{LocalizableStrings.VersionSuffixOptionName}>", LocalizableStrings.VersionSuffixOptionDescription, CommandOptionType.SingleValue);

            CommandOption noIncrementalOption = app.Option("--no-incremental", LocalizableStrings.NoIncrementialOptionDescription, CommandOptionType.NoValue);
            CommandOption noDependenciesOption = app.Option("--no-dependencies", LocalizableStrings.NoDependenciesOptionDescription, CommandOptionType.NoValue);
            CommandOption verbosityOption = MSBuildForwardingApp.AddVerbosityOption(app);

            app.OnExecute(() =>
            {
                List<string> msbuildArgs = new List<string>();

                if (!string.IsNullOrEmpty(projectArgument.Value))
                {
                    msbuildArgs.Add(projectArgument.Value);
                }

                if (noIncrementalOption.HasValue())
                {
                    msbuildArgs.Add("/t:Rebuild");
                }
                else
                {
                    msbuildArgs.Add("/t:Build");
                }

                if (outputOption.HasValue())
                {
                    msbuildArgs.Add($"/p:OutputPath={outputOption.Value()}");
                }

                if (frameworkOption.HasValue())
                {
                    msbuildArgs.Add($"/p:TargetFramework={frameworkOption.Value()}");
                }

                if (runtimeOption.HasValue())
                {
                    msbuildArgs.Add($"/p:RuntimeIdentifier={runtimeOption.Value()}");
                }

                if (configurationOption.HasValue())
                {
                    msbuildArgs.Add($"/p:Configuration={configurationOption.Value()}");
                }

                if (versionSuffixOption.HasValue())
                {
                    msbuildArgs.Add($"/p:VersionSuffix={versionSuffixOption.Value()}");
                }

                if (noDependenciesOption.HasValue())
                {
                    msbuildArgs.Add("/p:BuildProjectReferences=false");
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
