// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
                ProcessStartInfo startInfo = new(Environment.ProcessPath,
                        $"{Assembly.GetExecutingAssembly().Location} workload elevate")
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                Process serverProcess = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true,
                };

                if (serverProcess.Start())
                {
                    InitializeDispatcher(new NamedPipeClientStream(".", WindowsUtils.CreatePipeName(serverProcess.Id), PipeDirection.InOut));
                    Dispatcher.Connect();

                    // Add a pipe to the logger to allow the server to send log requests. This avoids having an elevated process writing
                    // to a less privileged location. It also simplifies troubleshooting because log events will be chronologically
                    // ordered in a single file. 
                    _log.AddNamedPipe(WindowsUtils.CreatePipeName(serverProcess.Id, "log"));

                    HasElevated = true;

                    _log.LogMessage("Elevated command instance started.");
                }
                else
                {
                    throw new Exception("Failed to start the server.");
                }
            }
        }
    }
}
