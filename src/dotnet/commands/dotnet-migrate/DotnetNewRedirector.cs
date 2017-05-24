// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MigrateCommand;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Migrate
{
    public class DotnetNewRedirector : ICanCreateDotnetCoreTemplate
    {
        public void CreateWithEphemeralHiveAndNoRestore(
            string templateName,
            string outputDirectory,
            string workingDirectory)
        {
            RunCommand("new", new string[] { "console", "-o", workingDirectory, "--debug:ephemeral-hive", "--no-restore" }, workingDirectory);
        }
        private void RunCommand(string commandToExecute, IEnumerable<string> args, string workingDirectory)
        {
            var command = new DotNetCommandFactory()
                .Create(commandToExecute, args)
                .WorkingDirectory(workingDirectory)
                .CaptureStdOut()
                .CaptureStdErr();

            var commandResult = command.Execute();

            if (commandResult.ExitCode != 0)
            {
                string argList = string.Join(", ", args);
                throw new GracefulException($"Failed to run {commandToExecute} with " +
                    $"args: {argList} ... " +
                    $"workingDirectory = {workingDirectory}, " +
                    $"StdOut = {commandResult.StdOut}, " +
                    $"StdErr = {commandResult.StdErr}");
            }
        }
    }
}