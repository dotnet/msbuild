// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// A base class for "hosted" ISdkResolverService implementations which are registered by an <see cref="IBuildComponentHost"/>.
    /// </summary>
    internal abstract class HostedSdkResolverServiceBase : IBuildComponent, INodePacketHandler, ISdkResolverService
    {
        /// <summary>
        /// An event to signal for waiting threads when the <see cref="IBuildComponent"/> is being shut down.
        /// </summary>
        protected readonly AutoResetEvent ShutdownEvent = new AutoResetEvent(initialState: false);

        /// <summary>
        /// The current <see cref="IBuildComponentHost"/> which is hosting this component.
        /// </summary>
        protected IBuildComponentHost Host;

        /// <inheritdoc cref="ISdkResolverService.SendPacket"/>
        public Action<INodePacket> SendPacket { get; set; }

        /// <inheritdoc cref="ISdkResolverService.ClearCache"/>
        public virtual void ClearCache(int submissionId)
        {
        }

        public virtual void ClearCaches()
        {
        }

        /// <inheritdoc cref="IBuildComponent.InitializeComponent"/>
        public virtual void InitializeComponent(IBuildComponentHost host)
        {
            Host = host;
        }

        /// <inheritdoc cref="INodePacketHandler.PacketReceived"/>
        ///
        public abstract void PacketReceived(int node, INodePacket packet);

        /// <inheritdoc cref="ISdkResolverService.ResolveSdk"/>
        public abstract SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio, bool failOnUnresolvedSdk);

        /// <inheritdoc cref="IBuildComponent.ShutdownComponent"/>
        public virtual void ShutdownComponent()
        {
            ShutdownEvent.Set();
        }
    }
}
