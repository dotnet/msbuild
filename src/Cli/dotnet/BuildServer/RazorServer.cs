// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.CommandFactory;
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
            if(!_fileSystem.File.Exists(PidFile.ServerPath.Value))
            {
                // The razor server path doesn't exist anymore so trying to shut it down would fail
                // Ensure the pid file is cleaned up so we don't try to shut it down again
                DeletePidFile();
                return;
            }

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
            DeletePidFile();
        }

        void DeletePidFile()
        {
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
