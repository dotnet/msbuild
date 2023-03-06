// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    internal sealed class MockSdkResolverService : IBuildComponent, ISdkResolverService
    {
        public Action<INodePacket> SendPacket { get; }

        public void ClearCache(int submissionId)
        {
        }

        public void ClearCaches()
        {
        }

        public Build.BackEnd.SdkResolution.SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio, bool failOnUnresolvedSdk)
        {
            return null;
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
        }

        public void ShutdownComponent()
        {
        }
    }
}
