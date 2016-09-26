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

            CommandArgument projectArgument = app.Argument("<PROJECT_JSON/PROJECT_DIR>",
                "The path to project.json file or a directory to migrate." +
                " If a directory is specified, then it will recursively search for project.json files to migrate." +
                " Defaults to current directory if nothing is specified.");

            CommandOption template = app.Option("-t|--template-file", "Base MSBuild template to use for migrated app. The default is the project included in dotnet new -t msbuild", CommandOptionType.SingleValue);
            CommandOption sdkVersion = app.Option("-v|--sdk-package-version", "The version of the sdk package that will be referenced in the migrated app. The default is the version of the sdk in dotnet new -t msbuild", CommandOptionType.SingleValue);
            CommandOption xprojFile = app.Option("-x|--xproj-file", "The path to the xproj file to use. Required when there is more than one xproj in a project directory.", CommandOptionType.SingleValue);
            CommandOption skipProjectReferences = app.Option("-s|--skip-project-references", "Skip migrating project references. By default project references are migrated recursively", CommandOptionType.BoolValue);

            app.OnExecute(() =>
            {
                MigrateCommand migrateCommand = new MigrateCommand(
                    template.Value(),
                    projectArgument.Value,
                    sdkVersion.Value(),
                    xprojFile.Value(),
                    skipProjectReferences.BoolValue.HasValue ? skipProjectReferences.BoolValue.Value : false);

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
