// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_REPORTFILEACCESSES
using System;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Experimental.FileAccess;
using Microsoft.Build.Shared;

namespace Microsoft.Build.FileAccesses
{
    /// <summary>
    /// Reports file accesses and process data to the in-proc node.
    /// </summary>
    internal sealed class OutOfProcNodeFileAccessManager : IFileAccessManager, IBuildComponent
    {
        /// <summary>
        /// The <see cref="Action"/> to report file accesses and process
        /// data to the in-proc node.
        /// </summary>
        private readonly Action<INodePacket> _sendPacket;

        private OutOfProcNodeFileAccessManager(Action<INodePacket> sendPacket) => _sendPacket = sendPacket;

        public static IBuildComponent CreateComponent(BuildComponentType type, Action<INodePacket> sendPacket)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(type == BuildComponentType.FileAccessManager, nameof(type));
            return new OutOfProcNodeFileAccessManager(sendPacket);
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
        }

        public void ShutdownComponent()
        {
        }

        /// <summary>
        /// Reports a file access to the in-proc node.
        /// </summary>
        /// <param name="fileAccessData">The file access to report to the in-proc node.</param>
        /// <param name="nodeId">The id of the reporting out-of-proc node.</param>
        public void ReportFileAccess(FileAccessData fileAccessData, int nodeId) => _sendPacket(new FileAccessReport(fileAccessData));

        /// <summary>
        /// Reports process data to the in-proc node.
        /// </summary>
        /// <param name="processData">The process data to report to the in-proc node.</param>
        /// <param name="nodeId">The id of the reporting out-of-proc node.</param>
        public void ReportProcess(ProcessData processData, int nodeId) => _sendPacket(new ProcessReport(processData));

        public FileAccessManager.HandlerRegistration RegisterHandlers(
            Action<BuildRequest, FileAccessData> fileAccessHandler,
            Action<BuildRequest, ProcessData> processHandler) =>
            throw new NotImplementedException("This method should not be called in OOP nodes.");

        public void WaitForFileAccessReportCompletion(int globalRequestId, CancellationToken cancellationToken) =>
            throw new NotImplementedException("This method should not be called in OOP nodes.");
    }
}
#endif
