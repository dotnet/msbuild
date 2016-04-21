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

            CommandLineApplication app = new CommandLineApplication();
            app.Name = "dotnet run";
            app.FullName = ".NET Run Command";
            app.Description = "Command used to run .NET apps";
            app.HandleResponseFiles = true;
            app.HelpOption("-h|--help");

            CommandOption framework = app.Option("-f|--framework", "Compile a specific framework", CommandOptionType.SingleValue);
            CommandOption configuration = app.Option("-c|--configuration", "Configuration under which to build", CommandOptionType.SingleValue);
            CommandOption project = app.Option("-p|--project", "The path to the project to run (defaults to the current directory). Can be a path to a project.json or a project directory", CommandOptionType.SingleValue);

            // TODO: this is not supporting args which can be switches (i.e. --test)
            // TODO: we need to make a change in CommandLine utils or parse args ourselves.
            CommandArgument runArgs = app.Argument("args", "Arguments to pass to the executable or script", multipleValues: true);

            app.OnExecute(() =>
            {
                RunCommand runCmd = new RunCommand();

                runCmd.Framework = framework.Value();
                runCmd.Configuration = configuration.Value();
                runCmd.Project = project.Value();
                runCmd.Args = runArgs.Values;

                return runCmd.Start();
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
#if DEBUG
                Reporter.Error.WriteLine(ex.ToString());
#else
                Reporter.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }
    }
}
