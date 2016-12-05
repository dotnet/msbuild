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
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;
            app.HandleResponseFiles = true;
            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument(
                $"<{LocalizableStrings.CmdProjectArgument}>",
                LocalizableStrings.CmdProjectArgumentDescription);

            CommandOption template = app.Option(
                "-t|--template-file",
                LocalizableStrings.CmdTemplateDescription,
                CommandOptionType.SingleValue);
            CommandOption sdkVersion = app.Option(
                "-v|--sdk-package-version", 
                LocalizableStrings.CmdVersionDescription, 
                CommandOptionType.SingleValue);
            CommandOption xprojFile = app.Option(
                "-x|--xproj-file", 
                LocalizableStrings.CmdXprojFileDescription, 
                CommandOptionType.SingleValue);
            CommandOption skipProjectReferences = app.Option(
                "-s|--skip-project-references", 
                LocalizableStrings.CmdSkipProjectReferencesDescription, 
                CommandOptionType.BoolValue);

            CommandOption reportFile = app.Option(
                "-r|--report-file", 
                LocalizableStrings.CmdReportFileDescription, 
                CommandOptionType.SingleValue);
            CommandOption structuredReportOutput = app.Option(
                "--format-report-file-json", 
                LocalizableStrings.CmdReportOutputDescription, 
                CommandOptionType.BoolValue);
            CommandOption skipBackup = app.Option("--skip-backup", 
                LocalizableStrings.CmdSkipBackupDescription, 
                CommandOptionType.BoolValue);

            app.OnExecute(() =>
            {
                MigrateCommand migrateCommand = new MigrateCommand(
                    template.Value(),
                    projectArgument.Value,
                    sdkVersion.Value(),
                    xprojFile.Value(),
                    reportFile.Value(),
                    skipProjectReferences.BoolValue.HasValue ? skipProjectReferences.BoolValue.Value : false,
                    structuredReportOutput.BoolValue.HasValue ? structuredReportOutput.BoolValue.Value : false,
                    skipBackup.BoolValue.HasValue ? skipBackup.BoolValue.Value : false);

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
                Reporter.Error.WriteLine(LocalizableStrings.MigrationFailedError);
                return 1;
            }
        }
    }
}
