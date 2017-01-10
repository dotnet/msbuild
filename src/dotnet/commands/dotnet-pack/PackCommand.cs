// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Tools.Pack
{
    public class PackCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication cmd = new CommandLineApplication(throwOnUnexpectedArg: false)
            {
                Name = "pack",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = HelpMessageStrings.MSBuildAdditionalArgsHelpText
            };

            cmd.HelpOption("-h|--help");

            var output = cmd.Option(
                $"-o|--output <{LocalizableStrings.CmdOutputDir}>",
                LocalizableStrings.CmdOutputDirDescription,
                CommandOptionType.SingleValue);
            var noBuild = cmd.Option(
                "--no-build",
                LocalizableStrings.CmdNoBuildOptionDescription, 
                CommandOptionType.NoValue);
            var includeSymbols = cmd.Option(
                "--include-symbols",
                LocalizableStrings.CmdIncludeSymbolsDescription,
                CommandOptionType.NoValue);
            var includeSource = cmd.Option(
                "--include-source",
                LocalizableStrings.CmdIncludeSourceDescription,
                CommandOptionType.NoValue);
            var configuration = cmd.Option(
                $"-c|--configuration <{LocalizableStrings.CmdConfig}>",
                LocalizableStrings.CmdConfigDescription, 
                CommandOptionType.SingleValue);
            var versionSuffix = cmd.Option(
                $"--version-suffix <{LocalizableStrings.CmdVersionSuffix}>",
                LocalizableStrings.CmdVersionSuffixDescription,
                CommandOptionType.SingleValue);
            var serviceable = cmd.Option(
                "-s|--serviceable",
                LocalizableStrings.CmdServiceableDescription, 
                CommandOptionType.NoValue);
            var argRoot = cmd.Argument(
                $"<{LocalizableStrings.CmdArgumentProject}>",
                LocalizableStrings.CmdArgumentDescription,
                multipleValues:true);
            CommandOption verbosityOption = MSBuildForwardingApp.AddVerbosityOption(cmd);

            cmd.OnExecute(() =>
            {
                var msbuildArgs = new List<string>()
                {
                     "/t:pack"
                };

                if (noBuild.HasValue())
                {
                    msbuildArgs.Add($"/p:NoBuild=true");
                }

                if (includeSymbols.HasValue())
                {
                    msbuildArgs.Add($"/p:IncludeSymbols=true");
                }

                if (includeSource.HasValue())
                {
                    msbuildArgs.Add($"/p:IncludeSource=true");
                }

                if (output.HasValue())
                {
                    msbuildArgs.Add($"/p:PackageOutputPath={output.Value()}");
                }

                if (configuration.HasValue())
                {
                    msbuildArgs.Add($"/p:Configuration={configuration.Value()}");
                }

                if (versionSuffix.HasValue())
                {
                    msbuildArgs.Add($"/p:VersionSuffix={versionSuffix.Value()}");
                }

                if (serviceable.HasValue())
                {
                    msbuildArgs.Add($"/p:Serviceable=true");
                }

                if (verbosityOption.HasValue())
                {
                    msbuildArgs.Add($"/verbosity:{verbosityOption.Value()}");
                }

                msbuildArgs.AddRange(argRoot.Values);

                msbuildArgs.AddRange(cmd.RemainingArguments);
                return new MSBuildForwardingApp(msbuildArgs).Execute();
            });

            return cmd.Execute(args);
        }
    }
}
