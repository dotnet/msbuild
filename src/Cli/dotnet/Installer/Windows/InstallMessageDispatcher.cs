// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Runtime.Versioning;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using static Microsoft.Win32.Msi.Error;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Provides basic interprocess communication primitives for sending and receiving install messages.
    /// over a <see cref="PipeStream"/>.
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    internal class InstallMessageDispatcher : PipeStreamMessageDispatcherBase, IInstallMessageDispatcher
    {
        public InstallMessageDispatcher(PipeStream pipeStream) : base(pipeStream)
        {
        }

        /// <summary>
        /// Sends a message and blocks until a reply is received.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>The response message.</returns>
        public InstallResponseMessage Send(InstallRequestMessage message)
        {
            WriteMessage(message.ToByteArray());
            return InstallResponseMessage.Create(ReadMessage());
        }

        public void Reply(InstallResponseMessage message)
        {
            WriteMessage(message.ToByteArray());
        }

        public void Reply(Exception e)
        {
            Reply(new InstallResponseMessage { HResult = e.HResult, Message = e.Message });
        }

        /// <summary>
        /// Sends a reply with the specified error.
        /// </summary>
        /// <param name="error">The error code to include in the response message.</param>
        public void Reply(uint error)
        {
            Reply(new InstallResponseMessage { Error = error });
        }


        public void ReplySuccess(string message)
        {
            Reply(new InstallResponseMessage { Message = message, HResult = S_OK, Error = SUCCESS });
        }

        public InstallRequestMessage ReceiveRequest()
        {
            return InstallRequestMessage.Create(ReadMessage());
        }

        /// <summary>
        /// Sends an <see cref="InstallRequestMessage"/> to update the MSI package cache and blocks
        /// until a response is received from the server.
        /// </summary>
        /// <param name="requestType">The action to perform</param>
        /// <param name="manifestPath">The JSON manifest associated with the MSI.</param>
        /// <param name="packageId">The ID of the workload pack package containing an MSI.</param>
        /// <param name="packageVersion">The package version.</param>
        /// <returns></returns>
        public InstallResponseMessage SendCacheRequest(InstallRequestType requestType, string manifestPath,
            string packageId, string packageVersion)
        {
            return Send(new InstallRequestMessage
            {
                RequestType = requestType,
                ManifestPath = manifestPath,
                PackageId = packageId,
                PackageVersion = packageVersion,
            });
        }

        /// <summary>
        /// Sends an <see cref="InstallRequestMessage"/> to modify the dependent of a provider key and
        /// blocks until a response is received from the server.
        /// </summary>
        /// <param name="requestType">The action to perform on the provider key.</param>
        /// <param name="providerKeyName">The name of the provider key.</param>
        /// <param name="dependent">The dependent value.</param>
        /// <returns></returns>
        public InstallResponseMessage SendDependentRequest(InstallRequestType requestType, string providerKeyName, string dependent)
        {
            return Send(new InstallRequestMessage
            {
                RequestType = requestType,
                ProviderKeyName = providerKeyName,
                Dependent = dependent,
            });
        }

        /// <summary>
        /// Sends an <see cref="InstallRequestMessage"/> to install, repair, or uninstall an MSI and
        /// blocks until a response is received from the server.
        /// </summary>
        /// <param name="requestType">The action to perform on the MSI.</param>
        /// <param name="logFile">The log file to created when performing the action.</param>
        /// <param name="packagePath">The path to the MSI package.</param>
        /// <param name="productCode">The product code of the installer package.</param>
        /// <returns></returns>
        public InstallResponseMessage SendMsiRequest(InstallRequestType requestType, string logFile, string packagePath = null, string productCode = null)
        {
            return Send(new InstallRequestMessage
            {
                RequestType = requestType,
                LogFile = logFile,
                PackagePath = packagePath,
                ProductCode = productCode,
            });
        }

        /// <summary>
        /// Sends an <see cref="InstallRequestMessage"/> to shut down the server.
        /// </summary>
        /// <returns></returns>
        public InstallResponseMessage SendShutdownRequest()
        {
            return Send(new InstallRequestMessage { RequestType = InstallRequestType.Shutdown });
        }

        /// <summary>
        /// Sends an <see cref="InstallRequestMessage"/> to create or delete a workload installation record.
        /// </summary>
        /// <param name="requestType">The action to perform on the workload record.</param>
        /// <param name="workloadId">The workload identifier.</param>
        /// <param name="sdkFeatureBand">The SDK feature band associated with the record.</param>
        /// <returns></returns>
        public InstallResponseMessage SendWorkloadRecordRequest(InstallRequestType requestType, WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            return Send(new InstallRequestMessage
            {
                RequestType = requestType,
                WorkloadId = workloadId.ToString(),
                SdkFeatureBand = sdkFeatureBand.ToString(),
            });
        }
    }
}
