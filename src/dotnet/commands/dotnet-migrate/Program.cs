// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Migrate
{
    public partial class MigrateCommand
    {
        public static MigrateCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var msbuildArgs = new List<string>();

            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet migrate", args);

            Reporter.Output.WriteLine(result.Diagram());

            result.ShowHelpIfRequested();

            return result["dotnet"]["migrate"].Value<MigrateCommand>();
        }


        public static int Run(string[] args)
        {

            // IMPORTANT:
            // When updating the command line args for dotnet-migrate, we need to update the in-VS caller of dotnet migrate as well.
            // It is located at dotnet/roslyn-project-system:
            //     src/Microsoft.VisualStudio.ProjectSystem.CSharp.VS/ProjectSystem/VS/Xproj/MigrateXprojFactory.cs

            DebugHelper.HandleDebugSwitch(ref args);

            MigrateCommand cmd;
            try
            {
                cmd = FromArgs(args);
            }
            catch (CommandCreationException e)
            {
                return e.ExitCode;
            }

            try
            {
                return cmd.Execute();
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
