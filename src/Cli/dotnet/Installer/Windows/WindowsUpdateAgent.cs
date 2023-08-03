// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.ServiceProcess;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Provides a simple abstraction of the Windows Update service.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WindowsUpdateAgent
    {
        private readonly ServiceController _wuauserv;

        private ISetupLogger _log;

        private bool _wasRunning;
        private bool _wasStopped;

        /// <summary>
        /// Creates a new <see cref="WindowsUpdateAgent"/> instance.
        /// </summary>
        /// <param name="logger">The <see cref="ISetupLogger"/> to write to.</param>
        public WindowsUpdateAgent(ISetupLogger logger)
        {
            _wuauserv = ServiceController.GetServices().Where(s => string.Equals(s.ServiceName, "wuauserv")).FirstOrDefault();
            _log = logger;

            if (_wuauserv == null)
            {
                _log?.LogMessage("Unable to locate wuaserv.");
            }
        }

        /// <summary>
        /// Stops the Windows Update service (wuauserv) if it is currently running and stoppable, otherwise
        /// logs the status of the service.
        /// </summary>
        public void Stop()
        {
            if (_wuauserv == null)
            {
                return;
            }

            if ((_wuauserv.Status == ServiceControllerStatus.Running) && _wuauserv.CanStop)
            {
                try
                {
                    // Remember that the service was running so when we exit, we can restart it.
                    _wasRunning = true;
                    _log?.LogMessage("Stopping automatic updates.");

                    // Ideally we want to pause and resume the service using WUAPI, but that is no longer
                    // supported on Windows 10. See https://docs.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iautomaticupdates-pause)
                    _wuauserv.Stop();
                    _wuauserv.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 30));
                    _wasStopped = true;
                }
                catch (Exception e)
                {
                    _log?.LogMessage($"Failed to stop automatic updates: {e.Message}");
                }
            }
            else
            {
                _log?.LogMessage($"wuauserv, status: {_wuauserv.Status}, can stop: {_wuauserv.CanStop}");
            }
        }

        /// <summary>
        /// Starts the Windows Update service (wuauserv) if it was previously stopped by calling <see cref="Stop"/>
        /// and it is currently stopped.
        /// </summary>
        public void Start()
        {
            if (_wuauserv == null)
            {
                return;
            }

            // Only start the service if it was running, we stopped it and it is currently stopped.
            if (_wuauserv.Status == ServiceControllerStatus.Stopped && _wasRunning && _wasStopped)
            {
                try
                {
                    _log?.LogMessage("Starting automatic updates.");
                    _wuauserv.Start();
                    _wuauserv.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 30));
                }
                catch (Exception e)
                {
                    _log?.LogMessage($"Failed to start automatic updates: {e.Message}");
                }
            }
            else
            {
                _log?.LogMessage($"wuauserv, status: {_wuauserv.Status}, was running: {_wasRunning}, was stopped: {_wasStopped}");
            }
        }
    }
}
