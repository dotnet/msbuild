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

            // IMPORTANT:
            // When updating the command line args for dotnet-migrate, we need to update the in-VS caller of dotnet migrate as well.
            // It is located at dotnet/roslyn-project-system:
            //     src/Microsoft.VisualStudio.ProjectSystem.CSharp.VS/ProjectSystem/VS/Xproj/MigrateXprojFactory.cs

            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication();
            app.Name = "dotnet migrate";
            app.FullName = ".NET Migrate Command";
            app.Description = "Command used to migrate project.json projects to msbuild";
            app.HandleResponseFiles = true;
            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument("<PROJECT_JSON/GLOBAL_JSON/PROJECT_DIR>",
                string.Join(Environment.NewLine,
                "The path to ",
                " - a project.json file to migrate.",
                "or",
                " - a global.json file, it will migrate the folders specified in global.json.",
                "or",
                " - a directory to migrate, it will recursively search for project.json files to migrate.",
                "Defaults to current directory if nothing is specified."));

            CommandOption template = app.Option("-t|--template-file", "Base MSBuild template to use for migrated app. The default is the project included in dotnet new -t msbuild", CommandOptionType.SingleValue);
            CommandOption sdkVersion = app.Option("-v|--sdk-package-version", "The version of the sdk package that will be referenced in the migrated app. The default is the version of the sdk in dotnet new -t msbuild", CommandOptionType.SingleValue);
            CommandOption xprojFile = app.Option("-x|--xproj-file", "The path to the xproj file to use. Required when there is more than one xproj in a project directory.", CommandOptionType.SingleValue);
            CommandOption skipProjectReferences = app.Option("-s|--skip-project-references", "Skip migrating project references. By default project references are migrated recursively", CommandOptionType.BoolValue);
            
            CommandOption reportFile = app.Option("-r|--report-file", "Output migration report to a file in addition to the console.", CommandOptionType.SingleValue);
            CommandOption structuredReportOutput = app.Option("--format-report-file-json", "Output migration report file as json rather than user messages", CommandOptionType.BoolValue); 

            app.OnExecute(() =>
            {
                MigrateCommand migrateCommand = new MigrateCommand(
                    template.Value(),
                    projectArgument.Value,
                    sdkVersion.Value(),
                    xprojFile.Value(),
                    reportFile.Value(),
                    skipProjectReferences.BoolValue.HasValue ? skipProjectReferences.BoolValue.Value : false,
                    structuredReportOutput.BoolValue.HasValue ? structuredReportOutput.BoolValue.Value : false);

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
