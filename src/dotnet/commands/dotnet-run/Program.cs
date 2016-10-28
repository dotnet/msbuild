// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet run";
            app.FullName = ".NET Run Command";
            app.Description = "Command used to run .NET apps";
            app.HandleResponseFiles = true;
            app.AllowArgumentSeparator = true;
            app.ArgumentSeparatorHelpText = HelpMessageStrings.MSBuildAdditionalArgsHelpText;
            app.HelpOption("-h|--help");

            CommandOption configuration = app.Option(
                "-c|--configuration", "Configuration under which to build",
                CommandOptionType.SingleValue);
            CommandOption framework = app.Option(
                "-f|--framework <FRAMEWORK>", "Compile a specific framework",
                CommandOptionType.SingleValue);
            CommandOption project = app.Option(
                "-p|--project", "The path to the project file to run (defaults to the current directory if there is only one project).",
                CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                RunCommand runCmd = new RunCommand();

                runCmd.Configuration = configuration.Value();
                runCmd.Framework = framework.Value();
                runCmd.Project = project.Value();
                runCmd.Args = app.RemainingArguments;

                return runCmd.Start();
            });

            return app.Execute(args);
        }
    }
}
