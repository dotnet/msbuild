// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Encapsulates information about elevation to support workload installations.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class InstallClientElevationContext : InstallElevationContextBase
    {
        private TimestampedFileLogger _log;

        private Process _serverProcess;

        public override bool IsClient => true;

        public InstallClientElevationContext(TimestampedFileLogger logger)
        {
            _log = logger;
        }

        /// <summary>
        /// Starts the elevated install server.
        /// </summary>
        public override void Elevate()
        {
            if (!IsElevated && !HasElevated)
            {
                // Use the path of the current host, otherwise we risk resolving against the wrong SDK version.
                // To trigger UAC, UseShellExecute must be true and Verb must be "runas".
                ProcessStartInfo startInfo = new($@"""{Environment.ProcessPath}""",
                        $@"""{Assembly.GetExecutingAssembly().Location}"" workload elevate")
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                _log?.LogMessage($"Attempting to start the elevated command instance. {startInfo.FileName} {startInfo.Arguments}.");

                _serverProcess = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true,
                };

                _serverProcess.Exited += ServerExited;

                if (_serverProcess.Start())
                {
                    InitializeDispatcher(new NamedPipeClientStream(".", WindowsUtils.CreatePipeName(_serverProcess.Id), PipeDirection.InOut));
                    Dispatcher.Connect();

                    // Add a pipe to the logger to allow the server to send log requests. This avoids having an elevated process writing
                    // to a less privileged location. It also simplifies troubleshooting because log events will be chronologically
                    // ordered in a single file. 
                    _log.AddNamedPipe(WindowsUtils.CreatePipeName(_serverProcess.Id, "log"));

                    HasElevated = true;

                    _log.LogMessage("Elevated command instance started.");
                }
                else
                {
                    _log?.LogMessage($"Failed to start the elevated command instance.");
                    throw new Exception("Failed to start the elevated command instance.");
                }
            }
        }

        private void ServerExited(object sender, EventArgs e)
        {
            _log?.LogMessage($"Elevated command instance has exited.");
        }
    }
}
