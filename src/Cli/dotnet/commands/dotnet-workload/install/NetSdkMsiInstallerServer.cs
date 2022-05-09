// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    [SupportedOSPlatform("windows")]
    internal class NetSdkMsiInstallerServer : MsiInstallerBase
    {
        private bool _done;
        private bool _shutdownRequested;

        public NetSdkMsiInstallerServer(InstallElevationContextBase elevationContext, PipeStreamSetupLogger logger, bool verifySignatures)
            : base(elevationContext, logger, verifySignatures)
        {
            // Establish a connection with the install client and logger. We're relying on tasks to handle
            // this, otherwise, the ordering needs to be lined up with how the client configures
            // the underlying pipe streams to avoid deadlock.
            Task dispatchTask = new Task(() => Dispatcher.Connect());
            Task loggerTask = new Task(() => logger.Connect());

            dispatchTask.Start();
            loggerTask.Start();

            Task.WaitAll(dispatchTask, loggerTask);
        }

        /// <summary>
        /// Starts the execution loop of the server.
        /// </summary>
        public void Run()
        {
            // Turn off automatic updates to reduce the risk of running into ERROR_INSTALL_ALREADY_RUNNING. We
            // also don't want MU to potentially patch the SDK while performing workload installations.
            UpdateAgent.Stop();

            while (!_done)
            {
                if (!Dispatcher.IsConnected || !IsParentProcessRunning)
                {
                    _done = true;
                    break;
                }

                InstallRequestMessage request = Dispatcher.ReceiveRequest();

                try
                {
                    switch (request.RequestType)
                    {
                        case InstallRequestType.Shutdown:
                            _shutdownRequested = true;
                            _done = true;
                            break;

                        case InstallRequestType.CachePayload:
                            Cache.CachePayload(request.PackageId, request.PackageVersion, request.ManifestPath);
                            Dispatcher.ReplySuccess($"Package Cached");
                            break;

                        case InstallRequestType.WriteWorkloadInstallationRecord:
                            RecordRepository.WriteWorkloadInstallationRecord(new WorkloadId(request.WorkloadId), new SdkFeatureBand(request.SdkFeatureBand));
                            Dispatcher.ReplySuccess($"Workload record created.");
                            break;

                        case InstallRequestType.DeleteWorkloadInstallationRecord:
                            RecordRepository.DeleteWorkloadInstallationRecord(new WorkloadId(request.WorkloadId), new SdkFeatureBand(request.SdkFeatureBand));
                            Dispatcher.ReplySuccess($"Workload record deleted.");
                            break;

                        case InstallRequestType.InstallMsi:
                            Dispatcher.Reply(InstallMsi(request.PackagePath, request.LogFile));
                            break;

                        case InstallRequestType.UninstallMsi:
                            Dispatcher.Reply(UninstallMsi(request.ProductCode, request.LogFile));
                            break;

                        case InstallRequestType.RepairMsi:
                            Dispatcher.Reply(RepairMsi(request.ProductCode, request.LogFile));
                            break;

                        case InstallRequestType.AddDependent:
                        case InstallRequestType.RemoveDependent:
                            UpdateDependent(request.RequestType, request.ProviderKeyName, request.Dependent);
                            Dispatcher.ReplySuccess($"Updated dependent '{request.Dependent}' for provider key '{request.ProviderKeyName}'");
                            break;

                        default:
                            throw new InvalidOperationException($"Unknown message request: {(int)request.RequestType}");
                    }
                }
                catch (Exception e)
                {
                    LogException(e);
                    Dispatcher.Reply(e);
                }
            }
        }

        public void Shutdown()
        {
            // Restart the update agent if we shut it down.
            UpdateAgent.Start();

            Log?.LogMessage("Shutting down server.");

            if (_shutdownRequested)
            {
                Dispatcher.Reply(new InstallResponseMessage());
            }
        }

        /// <summary>
        /// Creates a new <see cref="NetSdkMsiInstallerServer"/> instance.
        /// </summary>
        /// <returns>A new install server.</returns>
        public static NetSdkMsiInstallerServer Create(bool verifySignatures)
        {
            if (!WindowsUtils.IsAdministrator())
            {
                throw new UnauthorizedAccessException(LocalizableStrings.InsufficientPrivilegeToStartServer);
            }

            // Best effort to verify that the server was not started indirectly or being spoofed.
            if ((ParentProcess == null) || (ParentProcess.StartTime > CurrentProcess.StartTime) ||
                !string.Equals(ParentProcess.MainModule.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException(String.Format(LocalizableStrings.NoTrustWithParentPID, ParentProcess?.Id));
            }

            // Configure pipe DACLs
            SecurityIdentifier authenticatedUserIdentifier = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            SecurityIdentifier currentOwnerIdentifier = WindowsIdentity.GetCurrent().Owner;
            PipeSecurity pipeSecurity = new();

            // The current user has full control and should be running as Administrator.
            pipeSecurity.SetOwner(currentOwnerIdentifier);
            pipeSecurity.AddAccessRule(new PipeAccessRule(currentOwnerIdentifier, PipeAccessRights.FullControl, AccessControlType.Allow));

            // Restrict read/write access to authenticated users
            pipeSecurity.AddAccessRule(new PipeAccessRule(authenticatedUserIdentifier,
                PipeAccessRights.Read | PipeAccessRights.Write | PipeAccessRights.Synchronize, AccessControlType.Allow));

            // Initialize the named pipe for dispatching commands. The name of the pipe is based off the server PID since
            // the client knows this value and ensures both processes can generate the same name.
            string pipeName = WindowsUtils.CreatePipeName(CurrentProcess.Id);
            NamedPipeServerStream serverPipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message,
                PipeOptions.None, 65535, 65535, pipeSecurity);
            InstallMessageDispatcher dispatcher = new(serverPipe);

            // The client process will generate the actual log file. The server will log messages through a separate pipe.
            string logPipeName = WindowsUtils.CreatePipeName(CurrentProcess.Id, "log");
            NamedPipeServerStream logPipe = NamedPipeServerStreamAcl.Create(logPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message,
                PipeOptions.None, 65535, 65535, pipeSecurity);
            PipeStreamSetupLogger logger = new(logPipe, logPipeName);
            InstallServerElevationContext elevationContext = new(serverPipe);

            return new NetSdkMsiInstallerServer(elevationContext, logger, verifySignatures);
        }
    }
}
