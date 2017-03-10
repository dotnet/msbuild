// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Cli;
using System.Diagnostics;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Cache
{
    public partial class CacheCommand : MSBuildForwardingApp
    {
        private CacheCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
        }

        public static CacheCommand FromArgs(string[] args, string msbuildPath = null)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet cache";
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;
            app.AllowArgumentSeparator = true;
            app.ArgumentSeparatorHelpText = HelpMessageStrings.MSBuildAdditionalArgsHelpText;
            app.HelpOption("-h|--help");

            CommandOption projectArguments = app.Option(
                $"-e|--entries <{LocalizableStrings.ProjectEntries}>", LocalizableStrings.ProjectEntryDescription,
                CommandOptionType.MultipleValue);

            CommandOption frameworkOption = app.Option(
                $"-f|--framework <{LocalizableStrings.FrameworkOption}>", LocalizableStrings.FrameworkOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption runtimeOption = app.Option(
                $"-r|--runtime <{LocalizableStrings.RuntimeOption}>", LocalizableStrings.RuntimeOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption outputOption = app.Option(
                $"-o|--output <{LocalizableStrings.OutputOption}>", LocalizableStrings.OutputOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption fxOption = app.Option(
                $"--framework-version <{LocalizableStrings.FrameworkVersionOption}>", LocalizableStrings.FrameworkVersionOptionDescription,
                CommandOptionType.SingleValue);

            CommandOption skipOptimizationOption = app.Option(
                $"--skip-optimization", LocalizableStrings.SkipOptimizationOptionDescription,
                CommandOptionType.NoValue);

            CommandOption workingDir = app.Option(
               $"-w |--working-dir <{LocalizableStrings.IntermediateWorkingDirOption}>", LocalizableStrings.IntermediateWorkingDirOptionDescription,
               CommandOptionType.SingleValue);

            CommandOption preserveWorkingDir = app.Option(
               $"--preserve-working-dir", LocalizableStrings.PreserveIntermediateWorkingDirOptionDescription,
               CommandOptionType.NoValue);

            CommandOption verbosityOption = MSBuildForwardingApp.AddVerbosityOption(app);

            List<string> msbuildArgs = null;
            app.OnExecute(() =>
            {
                msbuildArgs = new List<string>();

                if (!projectArguments.HasValue())
                {
                    throw new InvalidOperationException(LocalizableStrings.SpecifyEntries).DisplayAsError();
                }

                msbuildArgs.Add("/t:ComposeCache");
                msbuildArgs.Add(projectArguments.Values[0]);
                var additionalProjectsargs = projectArguments.Values.Skip(1);

                if (additionalProjectsargs.Count() > 0)
                {
                    msbuildArgs.Add($"/p:AdditionalProjects={string.Join("%3B", additionalProjectsargs)}");
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
                    var outputPath = Path.GetFullPath(outputOption.Value());
                    msbuildArgs.Add($"/p:ComposeDir={outputPath}");
                }

                if (!string.IsNullOrEmpty(fxOption.Value()))
                {
                    msbuildArgs.Add($"/p:FX_Version={fxOption.Value()}");
                }

                if (!string.IsNullOrEmpty(workingDir.Value()))
                {
                    msbuildArgs.Add($"/p:ComposeWorkingDir={workingDir.Value()}");
                }

                if (skipOptimizationOption.HasValue())
                {
                    msbuildArgs.Add($"/p:SkipOptimization=true");
                }

                if (preserveWorkingDir.HasValue())
                {
                    msbuildArgs.Add($"/p:PreserveComposeWorkingDir=true");
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

            return new CacheCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CacheCommand cmd;
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
