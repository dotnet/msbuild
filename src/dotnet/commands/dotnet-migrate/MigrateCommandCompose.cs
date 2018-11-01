// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Migrate
{
    public class MigrateCommandCompose
    {
        public static MigrateCommand.MigrateCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet migrate", args);

            result.ShowHelpOrErrorIfAppropriate();

            var parsedMigrateResult = result["dotnet"]["migrate"];
            var migrateCommand = new MigrateCommand.MigrateCommand(
                new DotnetSlnRedirector(),
                new DotnetNewRedirector(),
                parsedMigrateResult.ValueOrDefault<string>("--template-file"),
                parsedMigrateResult.Arguments.FirstOrDefault(),
                parsedMigrateResult.ValueOrDefault<string>("--sdk-package-version"),
                parsedMigrateResult.ValueOrDefault<string>("--xproj-file"),
                parsedMigrateResult.ValueOrDefault<string>("--report-file"),
                parsedMigrateResult.ValueOrDefault<bool>("--skip-project-references"),
                parsedMigrateResult.ValueOrDefault<bool>("--format-report-file-json"),
                parsedMigrateResult.ValueOrDefault<bool>("--skip-backup"));
            return migrateCommand;
        }

        public static int Run(string[] args)
        {
            // IMPORTANT:
            // When updating the command line args for dotnet-migrate, we need to update the in-VS caller of dotnet migrate as well.
            // It is located at dotnet/roslyn-project-system:
            //     src/Microsoft.VisualStudio.ProjectSystem.CSharp.VS/ProjectSystem/VS/Xproj/MigrateXprojFactory.cs

            DebugHelper.HandleDebugSwitch(ref args);

            try
            {
                return FromArgs(args).Execute();
            }
            catch (GracefulException e)
            {
                Reporter.Error.WriteLine(e.Message);
                Reporter.Error.WriteLine(LocalizableStrings.MigrationFailedError);
                return 1;
            }
            catch (Exception)
            {
                Reporter.Error.WriteLine(LocalizableStrings.MigrationFailedError);
                throw;
            }
        }
    }
}
