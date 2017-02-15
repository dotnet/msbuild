// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Tools.Cache
{
    public partial class CacheCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet cache";
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;
            app.AllowArgumentSeparator = true;
            app.ArgumentSeparatorHelpText = HelpMessageStrings.MSBuildAdditionalArgsHelpText;
            app.HelpOption("-h|--help");

            CommandOption projectArgument = app.Option(
                $"-e|--entries <{LocalizableStrings.ProjectEntries}>", LocalizableStrings.ProjectEntryDescription,
                CommandOptionType.SingleValue);

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

            app.OnExecute(() =>
            {
                var cache = new CacheCommand();

                cache.Framework = frameworkOption.Value();
                cache.Runtime = runtimeOption.Value();
                cache.OutputPath = outputOption.Value();
                cache.FrameworkVersion = fxOption.Value();
                cache.Verbosity = verbosityOption.Value();
                cache.SkipOptimization = skipOptimizationOption.HasValue();
                cache.IntermediateDir = workingDir.Value();
                cache.PreserveIntermediateDir = preserveWorkingDir.HasValue();
                cache.ExtraMSBuildArguments = app.RemainingArguments;
                cache.ProjectArgument = projectArgument.Value();
               
                return cache.Execute();
            });

            return app.Execute(args);
        }
    }
}
