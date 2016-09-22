// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Migrate
{
    public partial class MigrateCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication();
            app.Name = "dotnet migrate";
            app.FullName = ".NET Migrate Command";
            app.Description = "Command used to migrate project.json projects to msbuild";
            app.HandleResponseFiles = true;
            app.HelpOption("-h|--help");

            CommandOption template = app.Option("-t|--template-file", "Base MSBuild template to use for migrated app. The default is the project included in dotnet new -t msbuild", CommandOptionType.SingleValue);
            CommandOption output = app.Option("-o|--output", "Directory to output migrated project to. The default is the project directory", CommandOptionType.SingleValue);
            CommandOption project = app.Option("-p|--project", "The path to the project to run (defaults to the current directory). Can be a path to a project.json or a project directory", CommandOptionType.SingleValue);
            CommandOption sdkVersion = app.Option("-v|--sdk-package-version", "The version of the sdk package that will be referenced in the migrated app. The default is the version of the sdk in dotnet new -t msbuild", CommandOptionType.SingleValue);
            CommandOption xprojFile = app.Option("-x|--xproj-file", "The path to the xproj file to use. Required when there is more than one xproj in a project directory.", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                MigrateCommand migrateCommand = new MigrateCommand(
                    template.Value(), 
                    output.Value(), 
                    project.Value(), 
                    sdkVersion.Value(),
                    xprojFile.Value());

                return migrateCommand.Execute();
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
