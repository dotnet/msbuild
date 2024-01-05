// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_REPORTFILEACCESSES
using System;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Experimental.FileAccess;

namespace Microsoft.Build.FileAccesses
{
    internal interface IFileAccessManager
    {
        void ReportFileAccess(FileAccessData fileAccessData, int nodeId);

        void ReportProcess(ProcessData processData, int nodeId);

        // Note: The return type of FileAccessManager.HandlerRegistration is exposed directly instead of IDisposable to avoid boxing.
        FileAccessManager.HandlerRegistration RegisterHandlers(
            Action<BuildRequest, FileAccessData> fileAccessHandler,
            Action<BuildRequest, ProcessData> processHandler);

        void WaitForFileAccessReportCompletion(int globalRequestId, CancellationToken cancellationToken);
    }
}
#endif
