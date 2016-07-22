// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class Run3Command
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet run3";
            app.FullName = ".NET Run3 Command";
            app.Description = "Command used to run .NET apps";
            app.HandleResponseFiles = true;
            app.AllowArgumentSeparator = true;
            app.HelpOption("-h|--help");

            CommandOption configuration = app.Option("-c|--configuration", "Configuration under which to build", CommandOptionType.SingleValue);
            CommandOption project = app.Option("-p|--project", "The path to the project file to run (defaults to the current directory if there is only one project).", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                Run3Command runCmd = new Run3Command();

                runCmd.Configuration = configuration.Value();
                runCmd.Project = project.Value();
                runCmd.Args = app.RemainingArguments;

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
                if (Reporter.IsVerbose)
                {
                    Reporter.Error.WriteLine(ex.ToString());
                }
                else
                {
                    Reporter.Error.WriteLine(ex.Message);
                }
#endif
                return 1;
            }
        }
    }
}
