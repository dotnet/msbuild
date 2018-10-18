// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.BuildServer
{
    internal class RazorServer : IBuildServer
    {
        private readonly ICommandFactory _commandFactory;
        private readonly IFileSystem _fileSystem;

        public RazorServer(
            RazorPidFile pidFile,
            ICommandFactory commandFactory = null,
            IFileSystem fileSystem = null)
        {
            PidFile = pidFile ?? throw new ArgumentNullException(nameof(pidFile));
            _commandFactory = commandFactory ?? new DotNetCommandFactory(alwaysRunOutOfProc: true);
            _fileSystem = fileSystem ?? FileSystemWrapper.Default;
        }

        public int ProcessId => PidFile.ProcessId;

        public string Name => LocalizableStrings.RazorServer;

        public RazorPidFile PidFile { get; }

        public void Shutdown()
        {
            var command = _commandFactory
                .Create(
                    "exec",
                    new string[] {
                        PidFile.ServerPath.Value,
                        "shutdown",
                        "-w",   // Wait for exit
                        "-p",   // Pipe name
                        PidFile.PipeName
                    })
                .CaptureStdOut()
                .CaptureStdErr();

            var result = command.Execute();
            if (result.ExitCode != 0)
            {
                throw new BuildServerException(
                    string.Format(
                        LocalizableStrings.ShutdownCommandFailed,
                        result.StdErr));
            }

            // After a successful shutdown, ensure the pid file is deleted
            // If the pid file was left behind due to a rude exit, this ensures we don't try to shut it down again
            try
            {
                if (_fileSystem.File.Exists(PidFile.Path.Value))
                {
                    _fileSystem.File.Delete(PidFile.Path.Value);
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }
    }
}
